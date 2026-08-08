using System.Collections.Generic;
using Atmosphere;
using ShaderLoader;
using UnityEngine;
using Utils;

namespace Thunderbolt
{
    /// <summary>
    /// Vessel-targeted bolt that reuses EVE's LightningBolt shader, sheet texture, and thunder sounds.
    /// Visual timing/lighting prefer EVE lightning config values.
    /// </summary>
    public class ThunderboltFx : MonoBehaviour
    {
        private static Material sharedTemplate;
        private static Texture2D whiteOcclusion;
        private static bool templateTried;
        private static bool soundsTried;
        private static readonly List<AudioClip> NearClips = new List<AudioClip>();
        private static readonly List<AudioClip> FarClips = new List<AudioClip>();

        private GameObject boltQuad;
        private Material boltMaterial;
        private Light pointLight;
        private LineRenderer fallbackLine;
        private float life;
        private float startLife;
        private float startIntensity;
        private Vector3 boltUpAxis = Vector3.up;

        public static void Spawn(Vector3 cloudPoint, Vector3 strikePoint)
        {
            GameObject go = new GameObject("Thunderbolt_Bolt");
            go.layer = 15; // Local
            ThunderboltFx fx = go.AddComponent<ThunderboltFx>();
            fx.Init(cloudPoint, strikePoint);
        }

        private void Init(Vector3 cloudPoint, Vector3 strikePoint)
        {
            EnsureTemplate();
            EnsureSounds();

            LightningConfig eveCfg = TryGetEveLightningConfig();
            startLife = Mathf.Max(0.08f, eveCfg != null ? eveCfg.LifeTime : 0.5f);
            life = startLife;
            startIntensity = eveCfg != null ? eveCfg.LightIntensity : 4.5f;
            float lightRange = eveCfg != null ? eveCfg.LightRange : 9000f;

            Vector3 delta = cloudPoint - strikePoint;
            float height = Mathf.Max(50f, delta.magnitude);
            boltUpAxis = delta.sqrMagnitude > 1f ? delta.normalized : Vector3.up;
            Vector3 mid = Vector3.Lerp(strikePoint, cloudPoint, 0.5f);

            transform.position = mid;

            pointLight = gameObject.AddComponent<Light>();
            pointLight.type = LightType.Point;
            pointLight.color = GetBoltLightColor();
            pointLight.intensity = startIntensity;
            pointLight.range = lightRange;
            pointLight.cullingMask = ~0;
            pointLight.transform.position = Vector3.Lerp(strikePoint, cloudPoint, 0.25f);

            if (sharedTemplate != null)
            {
                SpawnEveStyleQuad(mid, height, eveCfg);
            }
            else
            {
                SpawnFallbackLine(cloudPoint, strikePoint);
            }

            PlayEveThunderSound(mid);
        }

        private void SpawnEveStyleQuad(Vector3 mid, float height, LightningConfig eveCfg)
        {
            boltQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Collider col = boltQuad.GetComponent<Collider>();
            if (col != null)
            {
                Destroy(col);
            }

            boltQuad.layer = 15;
            boltQuad.transform.position = mid;
            boltQuad.transform.parent = transform;

            boltMaterial = new Material(sharedTemplate);
            boltMaterial.renderQueue = 2999;
            boltMaterial.SetFloat(ShaderProperties.alpha_PROPERTY, 1f);
            boltMaterial.SetColor(ShaderProperties.color_PROPERTY, GetBoltColor());
            boltMaterial.SetVector(ShaderProperties.randomIndexes_PROPERTY, new Vector2(Random.Range(0f, 1f), Random.Range(0f, 1f)));
            boltMaterial.SetVector(ShaderProperties.lightningSheetCount_PROPERTY, GetSheetCount());
            boltMaterial.SetFloat(ShaderProperties.maxConcurrentLightning_PROPERTY, 1f);
            boltMaterial.SetFloat(ShaderProperties.lightningIndex_PROPERTY, 0f);
            if (whiteOcclusion != null)
            {
                boltMaterial.SetTexture("lightningOcclusion", whiteOcclusion);
            }

            MeshRenderer renderer = boltQuad.GetComponent<MeshRenderer>();
            renderer.material = boltMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            float width = Mathf.Clamp(height * 0.45f, 400f, 2800f);
            if (eveCfg != null)
            {
                width = Mathf.Clamp(
                    eveCfg.BoltWidth * Mathf.Clamp(height / Mathf.Max(1f, eveCfg.BoltHeight), 0.35f, 2.5f),
                    300f,
                    4000f);
            }

            boltQuad.transform.localScale = new Vector3(width, height, 1f);
            OrientBillboard(boltUpAxis);
        }

        private void PlayEveThunderSound(Vector3 worldPosition)
        {
            if (FlightCamera.fetch == null || NearClips.Count == 0)
            {
                return;
            }

            LightningConfig cfg = TryGetEveLightningConfig();
            float maxDist = cfg != null ? cfg.SoundMaxDistance : 15000f;
            float minDist = cfg != null ? cfg.SoundMinDistance : 2000f;
            float farThreshold = cfg != null ? cfg.SoundFarThreshold : 5000f;
            float delayMult = cfg != null ? cfg.RealisticAudioDelayMultiplier : 0f;

            float soundDistance = (worldPosition - FlightCamera.fetch.transform.position).magnitude;
            if (soundDistance >= maxDist)
            {
                return;
            }

            bool useFar = soundDistance > farThreshold && FarClips.Count > 0;
            List<AudioClip> pool = useFar ? FarClips : NearClips;
            if (pool.Count == 0)
            {
                return;
            }

            AudioClip clip = pool[Random.Range(0, pool.Count)];
            if (clip == null)
            {
                return;
            }

            GameObject soundGo = new GameObject("Thunderbolt_Sound");
            soundGo.layer = 15;
            soundGo.transform.position = worldPosition;

            CelestialBody body = FlightGlobals.currentMainBody;
            if (body != null && body.bodyTransform != null)
            {
                soundGo.transform.parent = body.bodyTransform;
            }

            AudioSource source = soundGo.AddComponent<AudioSource>();
            source.clip = clip;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.spatialBlend = 1f;
            source.minDistance = minDist;
            source.maxDistance = maxDist;
            source.volume = Mathf.Clamp01(GameSettings.SHIP_VOLUME);
            source.playOnAwake = false;
            source.loop = false;

            if (delayMult > 0f)
            {
                source.PlayDelayed(delayMult * soundDistance / 343f);
            }
            else
            {
                source.Play();
            }

            soundGo.AddComponent<ThunderboltSoundJanitor>().Init(source);
        }

        private void Update()
        {
            float dt = TimeWarp.deltaTime > 0f ? TimeWarp.deltaTime : Time.deltaTime;
            life -= dt;
            float t = Mathf.Clamp01(life / startLife);

            if (pointLight != null)
            {
                pointLight.intensity = startIntensity * t;
            }

            if (boltMaterial != null)
            {
                boltMaterial.SetFloat(ShaderProperties.alpha_PROPERTY, t);
                if (boltQuad != null)
                {
                    OrientBillboard(boltUpAxis);
                }
            }

            if (fallbackLine != null)
            {
                Color c0 = fallbackLine.startColor;
                Color c1 = fallbackLine.endColor;
                c0.a = t;
                c1.a = t * 0.85f;
                fallbackLine.startColor = c0;
                fallbackLine.endColor = c1;
            }

            if (life <= 0f)
            {
                Cleanup();
                Destroy(gameObject);
            }
        }

        private void OrientBillboard(Vector3 upAxis)
        {
            if (boltQuad == null || FlightCamera.fetch == null)
            {
                return;
            }

            Vector3 camForward = FlightCamera.fetch.transform.forward;
            Vector3 right = Vector3.Cross(upAxis, Vector3.Cross(upAxis, camForward));
            if (right.sqrMagnitude < 1e-6f)
            {
                right = Vector3.Cross(upAxis, Vector3.right);
            }

            boltQuad.transform.rotation = Quaternion.LookRotation(right.normalized, upAxis.normalized);
        }

        private void Cleanup()
        {
            if (boltMaterial != null)
            {
                Destroy(boltMaterial);
                boltMaterial = null;
            }

            if (boltQuad != null)
            {
                Destroy(boltQuad);
                boltQuad = null;
            }

            if (fallbackLine != null && fallbackLine.material != null)
            {
                Destroy(fallbackLine.material);
            }
        }

        private static void EnsureTemplate()
        {
            if (templateTried)
            {
                return;
            }

            templateTried = true;

            try
            {
                Shader shader = ShaderLoaderClass.FindShader("EVE/LightningBolt");
                if (shader == null)
                {
                    ThunderboltSettings.LogWarning("EVE/LightningBolt shader not found — using fallback line bolt.");
                    return;
                }

                sharedTemplate = new Material(shader);
                if (!TryApplyEveBoltTexture(sharedTemplate))
                {
                    Texture2D tex = GameDatabase.Instance.GetTexture(
                        "StockVolumetricClouds/Clouds/Textures/PluginData/LightningSheet1",
                        false);
                    if (tex != null)
                    {
                        sharedTemplate.mainTexture = tex;
                        sharedTemplate.SetTexture("_MainTex", tex);
                    }
                    else
                    {
                        ThunderboltSettings.LogWarning("Lightning sheet texture not found — using fallback line bolt.");
                        sharedTemplate = null;
                        return;
                    }
                }

                whiteOcclusion = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                whiteOcclusion.SetPixel(0, 0, Color.white);
                whiteOcclusion.Apply(false, true);
                sharedTemplate.SetTexture("lightningOcclusion", whiteOcclusion);
                sharedTemplate.SetFloat(ShaderProperties.maxConcurrentLightning_PROPERTY, 1f);
                sharedTemplate.SetFloat(ShaderProperties.lightningIndex_PROPERTY, 0f);
                sharedTemplate.SetVector(ShaderProperties.lightningSheetCount_PROPERTY, GetSheetCount());

                ThunderboltSettings.Log("Using EVE LightningBolt visual style.");
            }
            catch (System.Exception ex)
            {
                ThunderboltSettings.LogWarning("Failed to init EVE bolt style: " + ex.Message);
                sharedTemplate = null;
            }
        }

        private static void EnsureSounds()
        {
            if (soundsTried)
            {
                return;
            }

            soundsTried = true;
            NearClips.Clear();
            FarClips.Clear();

            LightningConfig cfg = TryGetEveLightningConfig();
            if (cfg == null)
            {
                ThunderboltSettings.LogWarning("No EVE lightning config — thunder sounds unavailable.");
                return;
            }

            LoadClips(cfg.NearSoundNames, NearClips);
            LoadClips(cfg.FarSoundNames, FarClips);
            ThunderboltSettings.Log($"Loaded EVE thunder clips: near={NearClips.Count}, far={FarClips.Count}");
        }

        private static void LoadClips(List<LightningSoundConfig> names, List<AudioClip> into)
        {
            if (names == null)
            {
                return;
            }

            for (int i = 0; i < names.Count; i++)
            {
                string path = names[i]?.SoundName;
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                if (GameDatabase.Instance.ExistsAudioClip(path))
                {
                    into.Add(GameDatabase.Instance.GetAudioClip(path));
                }
            }
        }

        private static bool TryApplyEveBoltTexture(Material mat)
        {
            LightningConfig cfg = TryGetEveLightningConfig();
            if (cfg?.BoltTexture == null)
            {
                return false;
            }

            cfg.BoltTexture.ApplyTexture(mat, "_MainTex");
            return true;
        }

        private static LightningConfig TryGetEveLightningConfig()
        {
            try
            {
                List<LightningConfig> list = LightningManager.GetObjectList();
                if (list == null || list.Count == 0)
                {
                    return null;
                }

                string bodyName = FlightGlobals.currentMainBody != null ? FlightGlobals.currentMainBody.bodyName : null;
                if (!string.IsNullOrEmpty(bodyName))
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (list[i]?.Name != null && list[i].Name.IndexOf(bodyName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return list[i];
                        }
                    }
                }

                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] != null && list[i].Name != null && list[i].Name.IndexOf("Kerbin", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return list[i];
                    }
                }

                return list[0];
            }
            catch
            {
                return null;
            }
        }

        private static Vector2 GetSheetCount()
        {
            LightningConfig cfg = TryGetEveLightningConfig();
            return cfg != null ? cfg.LightningSheetCount : new Vector2(4f, 3f);
        }

        private static Color GetBoltColor()
        {
            LightningConfig cfg = TryGetEveLightningConfig();
            return cfg != null ? cfg.BoltColor : new Color(1.2f, 1.2f, 1.2f, 1f);
        }

        private static Color GetBoltLightColor()
        {
            LightningConfig cfg = TryGetEveLightningConfig();
            return cfg != null ? cfg.LightColor : new Color(0.55f, 0.6f, 1f, 1f);
        }

        private void SpawnFallbackLine(Vector3 cloudPoint, Vector3 strikePoint)
        {
            fallbackLine = gameObject.AddComponent<LineRenderer>();
            fallbackLine.useWorldSpace = true;
            fallbackLine.material = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color"));
            fallbackLine.startColor = new Color(0.85f, 0.92f, 1f, 1f);
            fallbackLine.endColor = new Color(0.65f, 0.78f, 1f, 0.85f);
            fallbackLine.startWidth = 18f;
            fallbackLine.endWidth = 6f;
            fallbackLine.positionCount = 2;
            fallbackLine.SetPosition(0, cloudPoint);
            fallbackLine.SetPosition(1, strikePoint);
        }
    }

    public class ThunderboltSoundJanitor : MonoBehaviour
    {
        private AudioSource source;
        private float timeout;

        public void Init(AudioSource audioSource)
        {
            source = audioSource;
            float clipLen = source.clip != null ? source.clip.length : 5f;
            timeout = Mathf.Max(2f, clipLen + 8f);
        }

        private void Update()
        {
            timeout -= Time.unscaledDeltaTime;
            if (source == null || (!source.isPlaying && timeout < 7.5f) || timeout <= 0f)
            {
                Destroy(gameObject);
            }
        }
    }
}
