using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Thunderbolt
{
    /// <summary>
    /// Loads Thunderbolt/ProceduralBolt from thunderboltshaders.bundle in this folder.
    /// Shader source lives under Unity/ThunderboltShaders — not in GameData.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class ThunderboltShaderLoader : MonoBehaviour
    {
        public const string ProceduralShaderName = "Thunderbolt/ProceduralBolt";

        private static readonly Dictionary<string, Shader> Shaders =
            new Dictionary<string, Shader>();

        public static bool Ready { get; private set; }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            LoadBundle();
            Ready = true;
        }

        public static Shader Find(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            if (Shaders.TryGetValue(name, out Shader shader) && shader != null)
            {
                return shader;
            }

            shader = Shader.Find(name);
            if (shader != null)
            {
                Shaders[name] = shader;
            }

            return shader;
        }

        private static void LoadBundle()
        {
            try
            {
                string root = KSPUtil.ApplicationRootPath + "GameData/Thunderbolt/Shaders/";
                string bundlePath = Path.Combine(root, "thunderboltshaders.bundle");
                if (!File.Exists(bundlePath))
                {
                    // Also accept a Shabby-style name if the user built one.
                    string shabPath = Path.Combine(root, "thunderbolt.shab");
                    bundlePath = File.Exists(shabPath) ? shabPath : bundlePath;
                }

                if (!File.Exists(bundlePath))
                {
                    Debug.Log(
                        "[Thunderbolt] No thunderboltshaders.bundle — pierce bolts fall back to line FX until you build " +
                        "GameData/Thunderbolt/Shaders/thunderboltshaders.bundle from ProceduralBolt.shader.");
                    return;
                }

                AssetBundle bundle = AssetBundle.LoadFromFile(bundlePath);
                if (bundle == null)
                {
                    Debug.LogWarning("[Thunderbolt] Failed to load shader bundle: " + bundlePath);
                    return;
                }

                Shader[] loaded = bundle.LoadAllAssets<Shader>();
                for (int i = 0; i < loaded.Length; i++)
                {
                    Shader s = loaded[i];
                    if (s == null || string.IsNullOrEmpty(s.name))
                    {
                        continue;
                    }

                    Shaders[s.name] = s;
                    Debug.Log("[Thunderbolt] Loaded shader " + s.name);
                }

                bundle.Unload(false);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[Thunderbolt] Shader bundle load error: " + ex.Message);
            }
        }
    }
}
