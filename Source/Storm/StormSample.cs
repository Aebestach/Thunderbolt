using UnityEngine;

namespace Thunderbolt
{
    /// <summary>
    /// Shared storm sample used by EVE volumes and the atmospheric fallback.
    /// </summary>
    public readonly struct StormSample
    {
        public readonly float Coverage;
        public readonly float LightningFrequency;
        /// <summary>
        /// Combined storm score used for chance rolls.
        /// EVE: coverage × lightning freq × precip corroboration.
        /// Atmospheric: Kerbin-relative ASL density × weather cell noise.
        /// </summary>
        public readonly float StormStrength;
        public readonly Vector3 CloudSampleWorldPosition;
        /// <summary>True when the vessel itself is inside a lightning-capable cloud volume.</summary>
        public readonly bool IsInsideStormCloud;

        public StormSample(
            float coverage,
            float lightningFrequency,
            float stormStrength,
            Vector3 cloudSampleWorldPosition,
            bool isInsideStormCloud)
        {
            Coverage = coverage;
            LightningFrequency = lightningFrequency;
            StormStrength = stormStrength;
            CloudSampleWorldPosition = cloudSampleWorldPosition;
            IsInsideStormCloud = isInsideStormCloud;
        }
    }
}
