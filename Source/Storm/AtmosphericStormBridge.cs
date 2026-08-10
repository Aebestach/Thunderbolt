using UnityEngine;

namespace Thunderbolt
{
    /// <summary>
    /// No-EVE lightning mode: place storms in a lower-atmosphere band using
    /// ASL density relative to Kerbin (home body), with spatial/temporal randomness.
    /// Bodies without atmosphere never produce samples.
    /// </summary>
    internal static class AtmosphericStormBridge
    {
        private const float FallbackKerbinAslDensity = 1.2250f;

        internal static bool TrySampleStormAboveVessel(Vessel vessel, out StormSample sample)
        {
            sample = default;
            if (vessel == null || vessel.mainBody == null)
            {
                return false;
            }

            CelestialBody body = vessel.mainBody;
            if (!body.atmosphere)
            {
                return false;
            }

            float bodyAsl = (float)body.atmDensityASL;
            if (bodyAsl <= 1e-5f)
            {
                return false;
            }

            float kerbinAsl = ResolveKerbinAslDensity();
            float densityRatio = bodyAsl / Mathf.Max(kerbinAsl, 1e-5f);

            double altitude = vessel.altitude;
            float atmoDepth = (float)body.atmosphereDepth;
            float maxStrikeAlt = Mathf.Min(
                atmoDepth * ThunderboltSettings.NonEveMaxAltitudeFraction,
                ThunderboltSettings.NonEveMaxAltitudeCap);
            if (altitude > maxStrikeAlt || altitude < -200.0)
            {
                return false;
            }

            double sampleAlt = System.Math.Max(0.0, altitude);
            double pressure = body.GetPressure(sampleAlt);
            double temperature = body.GetTemperature(sampleAlt);
            float localDensity = (float)body.GetDensity(pressure, temperature);
            if (localDensity < bodyAsl * 0.08f)
            {
                return false;
            }

            int cellLat = Mathf.FloorToInt((float)vessel.latitude / 4f);
            int cellLon = Mathf.FloorToInt((float)vessel.longitude / 4f);
            int timeBucket = (int)(Planetarium.GetUniversalTime() / 180.0);
            int bodyKey = body.flightGlobalsIndex;

            float cellNoise = Hash01(bodyKey, cellLat, cellLon, timeBucket);
            float densityPower = Mathf.Max(0.05f, ThunderboltSettings.NonEveDensityPower);
            float stormChance = Mathf.Clamp(
                ThunderboltSettings.NonEveStormCellChance * Mathf.Pow(densityRatio, densityPower),
                0.05f,
                0.90f);
            if (cellNoise > stormChance)
            {
                return false;
            }

            float storminess = Mathf.Lerp(0.35f, 1f, cellNoise / Mathf.Max(stormChance, 0.01f));
            float jitter = Mathf.Lerp(
                0.75f,
                1.15f,
                Hash01(bodyKey, cellLat ^ 17, cellLon ^ 31, timeBucket + 3));

            float stormStrength = Mathf.Clamp(
                ThunderboltSettings.NonEveStormStrengthScale * densityRatio * storminess * jitter,
                0.05f,
                2.5f);
            float coverage = Mathf.Clamp01(0.55f + 0.35f * storminess);
            float frequency = Mathf.Clamp01(
                0.40f + 0.45f * storminess * Mathf.Min(densityRatio, 1.5f));

            float cloudAlt = Mathf.Lerp(
                Mathf.Max((float)altitude + 600f, 800f),
                Mathf.Min(maxStrikeAlt, (float)altitude + 4500f),
                Hash01(bodyKey, cellLat, cellLon + 5, timeBucket));
            cloudAlt = Mathf.Min(cloudAlt, maxStrikeAlt * 0.95f);

            Vector3 bodyPos = body.position;
            Vector3 vesselPos = vessel.GetWorldPos3D();
            Vector3 up = (vesselPos - bodyPos).normalized;
            Vector3 cloudPos = bodyPos + up * ((float)body.Radius + cloudAlt);

            bool inside = altitude > cloudAlt - 800f
                && altitude < cloudAlt + 400f
                && cellNoise < stormChance * 0.25f;

            sample = new StormSample(coverage, frequency, stormStrength, cloudPos, inside);
            return true;
        }

        private static float ResolveKerbinAslDensity()
        {
            CelestialBody home = FlightGlobals.GetHomeBody();
            if (home != null && home.atmosphere && home.atmDensityASL > 1e-5)
            {
                return (float)home.atmDensityASL;
            }

            if (FlightGlobals.Bodies != null)
            {
                for (int i = 0; i < FlightGlobals.Bodies.Count; i++)
                {
                    CelestialBody body = FlightGlobals.Bodies[i];
                    if (body != null
                        && body.bodyName != null
                        && body.bodyName.IndexOf("Kerbin", System.StringComparison.OrdinalIgnoreCase) >= 0
                        && body.atmosphere
                        && body.atmDensityASL > 1e-5)
                    {
                        return (float)body.atmDensityASL;
                    }
                }
            }

            return FallbackKerbinAslDensity;
        }

        private static float Hash01(int a, int b, int c, int d)
        {
            unchecked
            {
                int h = a * 374761393 + b * 668265263 + c * 2147483647 + d * 1274126177;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0x7FFFFFFF) / (float)int.MaxValue;
            }
        }
    }
}
