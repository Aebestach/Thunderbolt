using UnityEngine;

namespace Thunderbolt
{
    /// <summary>
    /// Runtime settings facade over the three difficulty-parameter columns.
    /// </summary>
    public static class ThunderboltSettings
    {
        // Column 1 — strike
        public static float CheckInterval => Strike?.checkInterval ?? 3.5f;
        public static float BaseChancePerCheck => Strike?.baseChancePerCheck ?? 0.018f;
        public static float MaxTimeWarp => Strike?.maxTimeWarp ?? 4f;
        public static float VesselCooldown => Strike?.vesselCooldown ?? 45f;
        public static float MinCoverage => Strike?.minCoverage ?? 0.35f;
        public static float MinLightningFrequency => Strike?.minLightningFrequency ?? 0.15f;
        public static bool OnlyActiveVessel => Strike?.onlyActiveVessel ?? true;
        public static float InsideCloudChanceMultiplier => Strike?.insideCloudChanceMultiplier ?? 5f;

        /// <summary>
        /// Extra coverage demanded when the vessel is below the cloud slab.
        /// Blocks thin/high samples that still look like clear sky from the ground.
        /// </summary>
        public const float BelowCloudCoverageFloor = 0.5f;

        // Column 2 — damage
        public static float PartDestroyChance => Damage?.partDestroyChance ?? 0.45f;
        public static float VulnerableDestroyChance => Damage?.vulnerableDestroyChance ?? 0.8f;
        public static float CommandDestroyChance => Damage?.commandDestroyChance ?? 0.12f;
        public static float EvaKillChance => Damage?.evaKillChance ?? 0.08f;
        public static bool ProtectRootPart => Damage?.protectRootPart ?? false;
        public static bool EnableDamage => Damage?.enableDamage ?? true;
        public static bool ScreenMessages => Damage?.screenMessages ?? true;

        // Column 3 — debug
        public static bool DebugLogging => Visual?.debugLogging ?? false;
        public static bool DebugMode => Visual?.debugMode ?? false;
        public static bool DebugApplyDamage => Visual?.debugApplyDamage ?? false;
        public static KeyCode DebugStrikeKey => Visual?.ResolvedStrikeKey ?? KeyCode.L;

        private static ThunderboltStrikeParameters Strike => ThunderboltStrikeParameters.Instance;
        private static ThunderboltDamageParameters Damage => ThunderboltDamageParameters.Instance;
        private static ThunderboltVisualParameters Visual => ThunderboltVisualParameters.Instance;

        public static void Load()
        {
            Log("Using difficulty settings (Strike / Damage / Debug). Bolt visuals from Thunderbolt/ProceduralBolt.");
        }

        public static void SetDebugMode(bool enabled)
        {
            if (Visual != null)
            {
                Visual.debugMode = enabled;
            }
        }

        public static void SetDebugApplyDamage(bool enabled)
        {
            if (Visual != null)
            {
                Visual.debugApplyDamage = enabled;
            }
        }

        public static void Log(string message)
        {
            if (DebugLogging)
            {
                Debug.Log("[Thunderbolt] " + message);
            }
        }

        public static void LogWarning(string message)
        {
            if (DebugLogging)
            {
                Debug.LogWarning("[Thunderbolt] " + message);
            }
        }

        public static void LogError(string message)
        {
            if (DebugLogging)
            {
                Debug.LogError("[Thunderbolt] " + message);
            }
        }
    }
}
