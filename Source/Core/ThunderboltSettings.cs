using UnityEngine;

namespace Thunderbolt
{
    /// <summary>
    /// Runtime settings facade over the difficulty-parameter columns.
    /// </summary>
    public static class ThunderboltSettings
    {
        // Shared timing / eligibility (page 1 column 3)
        public static float CheckInterval => Shared?.checkInterval ?? 3.5f;
        public static float MaxTimeWarp => Shared?.maxTimeWarp ?? 4f;
        public static float VesselCooldown => Shared?.vesselCooldown ?? 45f;
        public static bool OnlyActiveVessel => Shared?.onlyActiveVessel ?? true;

        // EVE cloud gates
        public static float MinCoverage => Strike?.minCoverage ?? 0.40f;
        public static float MinLightningFrequency => Strike?.minLightningFrequency ?? 0.25f;

        /// <summary>
        /// Base chance for the active storm provider (EVE vs NonEVE).
        /// </summary>
        public static float BaseChancePerCheck =>
            StormSampler.HasEveProvider
                ? (Strike?.baseChancePerCheck ?? 0.018f)
                : (NonEve?.baseChancePerCheck ?? 0.018f);

        public static float InsideCloudChanceMultiplier =>
            StormSampler.HasEveProvider
                ? (Strike?.insideCloudChanceMultiplier ?? 5f)
                : (NonEve?.insideStormChanceMultiplier ?? 5f);

        // NonEVE / atmospheric (always available when EVE is absent)
        public static float NonEveStormCellChance => NonEve?.stormCellChance ?? 0.38f;
        public static float NonEveDensityPower => NonEve?.densityPower ?? 0.85f;
        public static float NonEveStormStrengthScale => NonEve?.stormStrengthScale ?? 0.45f;
        public static float NonEveMaxAltitudeFraction => NonEve?.maxAltitudeFraction ?? 0.42f;
        public static float NonEveMaxAltitudeCap => NonEve?.maxAltitudeCap ?? 14000f;

        /// <summary>
        /// Extra coverage demanded when the vessel is below the cloud slab.
        /// Blocks thin/high samples that still look like clear sky from the ground.
        /// </summary>
        public const float BelowCloudCoverageFloor = 0.65f;

        /// <summary>Skip volumes whose EVE time-fade has effectively disabled the layer.</summary>
        public const float MinTimeFade = 0.05f;

        /// <summary>
        /// Sampled lightningFrequency must be at least this fraction of the volume's
        /// highest cloud-type lightningFrequency (EVE-authored storm types).
        /// </summary>
        public const float MinRelativeLightningFrequency = 0.40f;

        /// <summary>Minimum coverage × lightningFrequency when inside the cloud slab.</summary>
        public const float StormProductFloorInside = 0.14f;

        /// <summary>Minimum coverage × lightningFrequency when below the cloud slab.</summary>
        public const float StormProductFloorBelow = 0.28f;

        /// <summary>
        /// Strong lightning column can pass below-cloud gates without precip corroboration.
        /// </summary>
        public const float StrongStormProduct = 0.48f;

        /// <summary>
        /// Minimum EVE precip/wet/droplet density used as storm corroboration below clouds.
        /// </summary>
        public const float MinPrecipSignal = 0.20f;

        /// <summary>Point-light intensity multiplier at full night.</summary>
        public const float NightLightBoost = 2.4f;

        /// <summary>Procedural bolt shader intensity multiplier at full night.</summary>
        public const float NightBoltBoost = 1.85f;

        // Damage
        public static float PartDestroyChance => Damage?.partDestroyChance ?? 0.45f;
        public static float VulnerableDestroyChance => Damage?.vulnerableDestroyChance ?? 0.8f;
        public static float CommandDestroyChance => Damage?.commandDestroyChance ?? 0.12f;
        public static float EvaKillChance => Damage?.evaKillChance ?? 0.08f;
        public static bool ProtectRootPart => Damage?.protectRootPart ?? false;
        public static bool EnableDamage => Damage?.enableDamage ?? true;
        public static bool ScreenMessages => Damage?.screenMessages ?? true;

        // Debug
        public static bool DebugLogging => Visual?.debugLogging ?? false;
        public static bool DebugMode => Visual?.debugMode ?? false;
        public static bool DebugApplyDamage => Visual?.debugApplyDamage ?? false;
        public static KeyCode DebugStrikeKey => Visual?.ResolvedStrikeKey ?? KeyCode.L;

        private static ThunderboltStrikeParameters Strike => ThunderboltStrikeParameters.Instance;
        private static ThunderboltNonEveParameters NonEve => ThunderboltNonEveParameters.Instance;
        private static ThunderboltSharedParameters Shared => ThunderboltSharedParameters.Instance;
        private static ThunderboltDamageParameters Damage => ThunderboltDamageParameters.Instance;
        private static ThunderboltVisualParameters Visual => ThunderboltVisualParameters.Instance;

        public static void Load()
        {
            Log("Using difficulty settings (EVE / NonEVE / Shared / Damage / Debug).");
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

        /// <summary>
        /// 1 at daytime, up to the given night multiplier after sunset / in darkness.
        /// </summary>
        public static float GetNightBrightnessMultiplier(Vector3 worldPos, float nightMultiplier)
        {
            CelestialBody body = FlightGlobals.currentMainBody;
            if (body == null || Sun.Instance == null || Sun.Instance.sun == null)
            {
                return 1f;
            }

            Vector3 up = (worldPos - body.position).normalized;
            Vector3 toSun = (Sun.Instance.sun.position - body.position).normalized;
            float sunElevation = Vector3.Dot(up, toSun);

            // ~day above +0.12, full night below -0.08, smooth twilight in between.
            float night = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.12f, -0.08f, sunElevation));
            return Mathf.Lerp(1f, nightMultiplier, night);
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
