using System;
using System.Collections.Generic;
using Atmosphere;
using UnityEngine;

namespace Thunderbolt
{
    internal readonly struct StormSample
    {
        public readonly float Coverage;
        public readonly float LightningFrequency;
        public readonly Vector3 CloudSampleWorldPosition;
        /// <summary>True when the vessel itself is inside a lightning-capable cloud volume.</summary>
        public readonly bool IsInsideStormCloud;

        public StormSample(
            float coverage,
            float lightningFrequency,
            Vector3 cloudSampleWorldPosition,
            bool isInsideStormCloud)
        {
            Coverage = coverage;
            LightningFrequency = lightningFrequency;
            CloudSampleWorldPosition = cloudSampleWorldPosition;
            IsInsideStormCloud = isInsideStormCloud;
        }
    }

    /// <summary>
    /// Direct bridge into EVE raymarched cloud volumes.
    /// Samples EVE for "when/where"; Thunderbolt draws its own bolt FX (not EVE Lightning.Spawn).
    /// </summary>
    internal static class EveCloudBridge
    {
        internal static bool IsAvailable { get; private set; }

        private static readonly Dictionary<string, List<CloudsRaymarchedVolume>> VolumesByBody =
            new Dictionary<string, List<CloudsRaymarchedVolume>>(StringComparer.OrdinalIgnoreCase);

        private static double lastVolumeCacheUt = double.MinValue;

        internal static void Initialize()
        {
            if (IsAvailable)
            {
                return;
            }

            try
            {
                _ = CloudsManager.GetObjectList();
                IsAvailable = true;
                ThunderboltSettings.Log("EVE Atmosphere.dll linked — cloud bridge ready.");
            }
            catch (Exception ex)
            {
                IsAvailable = false;
                ThunderboltSettings.LogError("Failed to initialize EVE bridge: " + ex);
            }
        }

        internal static bool TrySampleStormAboveVessel(Vessel vessel, out StormSample sample)
        {
            sample = default;
            if (!IsAvailable || vessel == null || vessel.mainBody == null)
            {
                return false;
            }

            CelestialBody body = vessel.mainBody;
            RefreshVolumeCacheIfNeeded();

            if (!VolumesByBody.TryGetValue(body.bodyName, out List<CloudsRaymarchedVolume> volumes) || volumes.Count == 0)
            {
                return false;
            }

            Vector3 bodyPos = body.position;
            Vector3 vesselPos = vessel.GetWorldPos3D();
            Vector3 up = (vesselPos - bodyPos).normalized;
            float vesselRadius = (vesselPos - bodyPos).magnitude;

            float bestScore = 0f;
            bool found = false;
            StormSample best = default;

            for (int i = 0; i < volumes.Count; i++)
            {
                CloudsRaymarchedVolume volume = volumes[i];
                if (volume == null)
                {
                    continue;
                }

                float inner = volume.InnerSphereRadius;
                float outer = volume.OuterSphereRadius;
                if (outer <= inner || vesselRadius > outer + 500f)
                {
                    continue;
                }

                bool vesselInCloudHeight = vesselRadius >= inner && vesselRadius <= outer;

                // Prefer sampling at the vessel when it is inside the cloud slab.
                Vector3 samplePos;
                float sampleRadius;
                if (vesselInCloudHeight)
                {
                    sampleRadius = vesselRadius;
                    samplePos = vesselPos;
                }
                else
                {
                    sampleRadius = Mathf.Clamp(Mathf.Lerp(inner, outer, 0.65f), inner + 1f, outer - 1f);
                    if (vesselRadius > sampleRadius)
                    {
                        sampleRadius = Mathf.Min(outer - 1f, vesselRadius + 200f);
                    }

                    samplePos = bodyPos + up * sampleRadius;
                }

                float coverage = volume.SampleCoverage(samplePos, out float cloudType, false);
                // Below the cloud slab (runway / clear-looking days), demand denser coverage
                // so thin high samples with default lightningFrequency=1 do not count as storms.
                float coverageThreshold = vesselInCloudHeight
                    ? ThunderboltSettings.MinCoverage
                    : Mathf.Max(ThunderboltSettings.MinCoverage, ThunderboltSettings.BelowCloudCoverageFloor);
                if (coverage < coverageThreshold)
                {
                    continue;
                }

                float frequency = volume.GetInterpolatedCloudTypeLightningFrequency(cloudType);
                if (frequency < ThunderboltSettings.MinLightningFrequency)
                {
                    continue;
                }

                bool insideStorm = false;
                if (vesselInCloudHeight)
                {
                    // Confirm local coverage at the vessel itself (not only a point above).
                    float vesselCoverage = volume.SampleCoverage(vesselPos, out float vesselCloudType, false);
                    float vesselFrequency = volume.GetInterpolatedCloudTypeLightningFrequency(vesselCloudType);
                    insideStorm = vesselCoverage >= ThunderboltSettings.MinCoverage
                        && vesselFrequency >= ThunderboltSettings.MinLightningFrequency;
                    if (insideStorm)
                    {
                        coverage = Mathf.Max(coverage, vesselCoverage);
                        frequency = Mathf.Max(frequency, vesselFrequency);
                    }
                }

                // Bolt origin: a bit above the vessel while inside cloud, else cloud sample point.
                Vector3 boltOrigin = insideStorm
                    ? bodyPos + up * Mathf.Min(outer - 1f, vesselRadius + 400f)
                    : samplePos;

                float score = coverage * frequency + (insideStorm ? 10f : 0f);
                if (!found || score > bestScore)
                {
                    found = true;
                    bestScore = score;
                    best = new StormSample(coverage, frequency, boltOrigin, insideStorm);
                }
            }

            if (!found)
            {
                return false;
            }

            sample = best;
            return true;
        }

        private static void RefreshVolumeCacheIfNeeded()
        {
            double now = Planetarium.GetUniversalTime();
            if (now - lastVolumeCacheUt < 5.0 && VolumesByBody.Count > 0)
            {
                return;
            }

            lastVolumeCacheUt = now;
            VolumesByBody.Clear();

            List<CloudsObject> cloudObjects = CloudsManager.GetObjectList();
            if (cloudObjects == null)
            {
                return;
            }

            for (int i = 0; i < cloudObjects.Count; i++)
            {
                CloudsObject cloudsObject = cloudObjects[i];
                if (cloudsObject == null || string.IsNullOrEmpty(cloudsObject.Body))
                {
                    continue;
                }

                CloudsRaymarchedVolume volume = cloudsObject.LayerRaymarchedVolume;
                if (volume == null)
                {
                    continue;
                }

                if (!VolumesByBody.TryGetValue(cloudsObject.Body, out List<CloudsRaymarchedVolume> list))
                {
                    list = new List<CloudsRaymarchedVolume>();
                    VolumesByBody[cloudsObject.Body] = list;
                }

                list.Add(volume);
            }
        }
    }
}
