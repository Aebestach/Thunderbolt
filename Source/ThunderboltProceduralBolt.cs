using UnityEngine;

namespace Thunderbolt
{
    /// <summary>
    /// Pierce-bolt materials from Thunderbolt/ProceduralBolt GPU shader only (no textures).
    /// </summary>
    internal static class ThunderboltProceduralBolt
    {
        internal const float MinWorldWidth = 14f;
        internal const float MaxWorldWidth = 48f;
        internal const float LengthToWidth = 0.032f;

        internal static float WidthForLength(float length)
        {
            return Mathf.Clamp(length * LengthToWidth, MinWorldWidth, MaxWorldWidth);
        }

        internal static bool TryCreateMaterial(float seed, Color color, out Material material)
        {
            material = null;

            Shader gpu = ThunderboltShaderLoader.Find(ThunderboltShaderLoader.ProceduralShaderName);
            if (gpu == null)
            {
                return false;
            }

            material = new Material(gpu);
            material.SetColor("_Color", color);
            material.SetFloat("_Fade", 1f);
            material.SetFloat("_Seed", seed);
            material.SetFloat("_CoreWidth", 0.014f);
            material.SetFloat("_GlowWidth", 0.065f);
            material.SetFloat("_Intensity", 8f);
            material.SetFloat("_Bend", 0.65f);
            material.SetFloat("_Branch", 1.0f);
            material.renderQueue = 2999;
            return true;
        }

        internal static void SetFade(Material material, float fade)
        {
            if (material == null)
            {
                return;
            }

            material.SetFloat("_Fade", Mathf.Clamp01(fade));
        }
    }
}
