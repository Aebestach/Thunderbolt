using System.Collections.Generic;
using Atmosphere;
using UnityEngine;

namespace Thunderbolt
{
    /// <summary>
    /// Vessel-targeted bolt FX via Thunderbolt/ProceduralBolt only.
    /// EVE LightningConfig is still used for lifetime, light, colour, and thunder clips.
    /// </summary>
    public class ThunderboltFx : MonoBehaviour
    {
        private static bool soundsTried;
        private static readonly List<AudioClip> NearClips = new List<AudioClip>();
        private static readonly List<AudioClip> FarClips = new List<AudioClip>();

        private readonly List<GameObject> boltQuads = new List<GameObject>();
        private readonly List<Material> boltMaterials = new List<Material>();
        private readonly List<Vector3> segmentAxes = new List<Vector3>();

        private Light pointLight;
        private LineRenderer fallbackLine;
        private float life;
        private float startLife;
        private float startIntensity;
        private Vector3 boltUpAxis = Vector3.up;

        /// <summary>
        /// Simple cloud→hit procedural bolt (rods / EVA / last-resort fallback).
        /// </summary>
        public static void Spawn(Vector3 cloudPoint, Vector3 strikePoint)
        {
            if (ThunderboltPiercingPath.TryBuildToPoint(null, cloudPoint, strikePoint, out List<Vector3> path))
            {
                SpawnPath(path, hiddenSegment: -1);
                return;
            }

            GameObject go = new GameObject("Thunderbolt_Bolt");
            go.layer = 15;
            ThunderboltFx fx = go.AddComponent<ThunderboltFx>();
            fx.InitPath(new[] { cloudPoint, strikePoint }, hiddenSegment: -1);
        }

        /// <summary>
        /// Multi-segment procedural bolt. Optional hiddenSegment skips one polyline edge
        /// (used for the nose→tail span inside the hull).
        /// </summary>
        public static void SpawnPath(IList<Vector3> points, int hiddenSegment = -1)
        {
            if (points == null || points.Count < 2)
            {
                return;
            }

            GameObject go = new GameObject(hiddenSegment >= 0 ? "Thunderbolt_Pierce" : "Thunderbolt_Path");
            go.layer = 15;
            ThunderboltFx fx = go.AddComponent<ThunderboltFx>();
            fx.InitPath(points, hiddenSegment);
        }

        private void InitPath(IList<Vector3> points, int hiddenSegment)
        {
            EnsureSounds();

            LightningConfig eveCfg = TryGetEveLightningConfig();
            BeginLifetime(eveCfg);

            Vector3 mid = points[points.Count / 2];
            transform.position = mid;
            AttachLight(points.Count > 1 ? points[1] : mid, eveCfg);

            Color boltColor = GetBoltColor();
            bool anySegment = false;
            for (int i = 0; i < points.Count - 1; i++)
            {
                if (i == hiddenSegment)
                {
                    continue;
                }

                float seed = 11.7f + i * 5.3f;
                if (SpawnProceduralSegment(points[i + 1], points[i], seed, boltColor))
                {
                    anySegment = true;
                }
            }

            if (!anySegment)
            {
                Vector3[] arr = new Vector3[points.Count];
                for (int i = 0; i < points.Count; i++)
                {
                    arr[i] = points[i];
                }

                SpawnFallbackLine(arr);
            }

            PlayEveThunderSound(mid);
        }

        private void BeginLifetime(LightningConfig eveCfg)
        {
            startLife = Mathf.Max(0.08f, eveCfg != null ? eveCfg.LifeTime : 0.5f);
            life = startLife;
            startIntensity = eveCfg != null ? eveCfg.LightIntensity : 4.5f;
        }

        private void AttachLight(Vector3 position, LightningConfig eveCfg)
        {
            float lightRange = eveCfg != null ? eveCfg.LightRange : 9000f;
            pointLight = gameObject.AddComponent<Light>();
            pointLight.type = LightType.Point;
            pointLight.color = GetBoltLightColor();
            pointLight.intensity = startIntensity;
            pointLight.range = lightRange;
            pointLight.cullingMask = ~0;
            pointLight.transform.position = position;
        }

        private bool SpawnProceduralSegment(Vector3 from, Vector3 to, float seed, Color color)
        {
            if (!ThunderboltProceduralBolt.TryCreateMaterial(seed, color, out Material mat))
            {
                return false;
            }

            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length < 0.5f)
            {
                Destroy(mat);
                return false;
            }

            // Slight overlap so short pieces meet even if billboards leave a hairline.
            Vector3 dir = delta / length;
            const float overlap = 2f;
            Vector3 fromEx = from - dir * overlap;
            Vector3 toEx = to + dir * overlap;
            float drawLen = length + overlap * 2f;
            float width = ThunderboltProceduralBolt.WidthForLength(length);
            return FinishSegmentQuad(fromEx, toEx, width, drawLen, mat);
        }

        private bool FinishSegmentQuad(
            Vector3 from,
            Vector3 to,
            float width,
            float length,
            Material mat)
        {
            Vector3 delta = to - from;
            if (length < 0.5f)
            {
                length = delta.magnitude;
            }

            if (length < 0.5f)
            {
                Destroy(mat);
                return false;
            }

            Vector3 axis = delta / length;
            Vector3 mid = Vector3.Lerp(from, to, 0.5f);

            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Collider col = quad.GetComponent<Collider>();
            if (col != null)
            {
                Destroy(col);
            }

            quad.layer = 15;
            quad.name = "Thunderbolt_ProcSegment";
            quad.transform.position = mid;
            quad.transform.parent = transform;

            MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
            renderer.material = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            quad.transform.localScale = new Vector3(width, length, 1f);
            OrientBillboard(quad, axis);

            boltQuads.Add(quad);
            boltMaterials.Add(mat);
            segmentAxes.Add(axis);

            if (boltQuads.Count == 1)
            {
                boltUpAxis = axis;
            }

            return true;
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

            for (int i = 0; i < boltMaterials.Count; i++)
            {
                Material mat = boltMaterials[i];
                if (mat != null)
                {
                    ThunderboltProceduralBolt.SetFade(mat, t);
                }

                if (i < boltQuads.Count && boltQuads[i] != null && i < segmentAxes.Count)
                {
                    OrientBillboard(boltQuads[i], segmentAxes[i]);
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

        private static void OrientBillboard(GameObject quad, Vector3 upAxis)
        {
            if (quad == null || FlightCamera.fetch == null)
            {
                return;
            }

            Vector3 camForward = FlightCamera.fetch.transform.forward;
            Vector3 right = Vector3.Cross(upAxis, Vector3.Cross(upAxis, camForward));
            if (right.sqrMagnitude < 1e-6f)
            {
                right = Vector3.Cross(upAxis, Vector3.right);
            }

            quad.transform.rotation = Quaternion.LookRotation(right.normalized, upAxis.normalized);
        }

        private void Cleanup()
        {
            for (int i = 0; i < boltMaterials.Count; i++)
            {
                if (boltMaterials[i] != null)
                {
                    Destroy(boltMaterials[i]);
                }
            }

            boltMaterials.Clear();

            for (int i = 0; i < boltQuads.Count; i++)
            {
                if (boltQuads[i] != null)
                {
                    Destroy(boltQuads[i]);
                }
            }

            boltQuads.Clear();
            segmentAxes.Clear();

            if (fallbackLine != null && fallbackLine.material != null)
            {
                Destroy(fallbackLine.material);
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

        private void SpawnFallbackLine(IList<Vector3> points)
        {
            fallbackLine = gameObject.AddComponent<LineRenderer>();
            fallbackLine.useWorldSpace = true;
            fallbackLine.material = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color"));
            fallbackLine.startColor = new Color(0.85f, 0.92f, 1f, 1f);
            fallbackLine.endColor = new Color(0.65f, 0.78f, 1f, 0.85f);
            fallbackLine.startWidth = 18f;
            fallbackLine.endWidth = 6f;
            fallbackLine.positionCount = points.Count;
            for (int i = 0; i < points.Count; i++)
            {
                fallbackLine.SetPosition(i, points[i]);
            }
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
