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
        /// <summary>
        /// Combined EVE-style storm score: coverage × lightning freq × precip corroboration.
        /// Used for chance rolls so fair-weather dense clouds score lower than wet storms.
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

    /// <summary>
    /// Direct bridge into EVE raymarched cloud volumes.
    /// Samples EVE for "when/where"; bolts are drawn only by Thunderbolt/ProceduralBolt.
    /// Storm gating mirrors EVE's weather stack: coverage, per-type lightningFrequency,
    /// and precip/wetness densities — not coverage alone.
    /// </summary>
    internal static class EveCloudBridge
    {
        internal static bool IsAvailable { get; private set; }

        private static readonly Dictionary<string, List<CloudsRaymarchedVolume>> VolumesByBody =
            new Dictionary<string, List<CloudsRaymarchedVolume>>(StringComparer.OrdinalIgnoreCase);

        private static double lastVolumeCacheUt = double.MinValue;

        // Column sample heights (fraction through the cloud slab) when the vessel is below clouds.
        private static readonly float[] BelowCloudSampleFractions = { 0.35f, 0.55f, 0.75f, 0.90f };

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

                float timeFade = volume.CurrentTimeFadeCoverage * volume.CurrentTimeFadeDensity;
                if (timeFade < ThunderboltSettings.MinTimeFade)
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

                if (!TryEvaluateVolume(
                    volume,
                    bodyPos,
                    up,
                    vesselPos,
                    vesselRadius,
                    inner,
                    outer,
                    vesselInCloudHeight,
                    out float coverage,
                    out float frequency,
                    out float precip,
                    out float relativeFreq,
                    out Vector3 boltOrigin,
                    out bool insideStorm))
                {
                    continue;
                }

                if (!PassesStormGates(coverage, frequency, precip, relativeFreq, vesselInCloudHeight))
                {
                    continue;
                }

                float stormStrength = ComputeStormStrength(coverage, frequency, precip, relativeFreq);
                // Prefer inside-cloud + wetter / more lightning-capable samples.
                float score = stormStrength + (insideStorm ? 10f : 0f) + precip * 0.5f;
                if (!found || score > bestScore)
                {
                    found = true;
                    bestScore = score;
                    best = new StormSample(coverage, frequency, stormStrength, boltOrigin, insideStorm);
                }
            }

            if (!found)
            {
                return false;
            }

            sample = best;
            return true;
        }

        private static bool TryEvaluateVolume(
            CloudsRaymarchedVolume volume,
            Vector3 bodyPos,
            Vector3 up,
            Vector3 vesselPos,
            float vesselRadius,
            float inner,
            float outer,
            bool vesselInCloudHeight,
            out float coverage,
            out float frequency,
            out float precip,
            out float relativeFreq,
            out Vector3 boltOrigin,
            out bool insideStorm)
        {
            coverage = 0f;
            frequency = 0f;
            precip = 0f;
            relativeFreq = 0f;
            boltOrigin = vesselPos;
            insideStorm = false;

            Vector3 samplePos;

            if (vesselInCloudHeight)
            {
                samplePos = vesselPos;
                coverage = volume.SampleCoverage(samplePos, out float cloudType, false);
                frequency = volume.GetInterpolatedCloudTypeLightningFrequency(cloudType);
                precip = SamplePrecipSignal(volume, cloudType);
            }
            else
            {
                // Below the slab: average several heights so a single thin layer cannot
                // look like a full thunderstorm (common clear-day false positive).
                float covSum = 0f;
                float freqWeighted = 0f;
                float precipWeighted = 0f;
                float bestLocalCov = 0f;
                float bestLocalFreq = 0f;
                float bestLocalPrecip = 0f;
                Vector3 bestLocalPos = bodyPos + up * Mathf.Lerp(inner, outer, 0.65f);
                int hits = 0;

                for (int s = 0; s < BelowCloudSampleFractions.Length; s++)
                {
                    float sampleRadius = Mathf.Lerp(inner, outer, BelowCloudSampleFractions[s]);
                    sampleRadius = Mathf.Clamp(sampleRadius, inner + 1f, outer - 1f);
                    Vector3 pos = bodyPos + up * sampleRadius;
                    float localCov = volume.SampleCoverage(pos, out float localType, false);
                    if (localCov <= 0.001f)
                    {
                        continue;
                    }

                    float localFreq = volume.GetInterpolatedCloudTypeLightningFrequency(localType);
                    float localPrecip = SamplePrecipSignal(volume, localType);

                    covSum += localCov;
                    freqWeighted += localFreq * localCov;
                    precipWeighted += localPrecip * localCov;
                    hits++;

                    float localScore = localCov * localFreq * (0.35f + 0.65f * Mathf.Clamp01(localPrecip));
                    float bestScore = bestLocalCov * bestLocalFreq * (0.35f + 0.65f * Mathf.Clamp01(bestLocalPrecip));
                    if (localScore >= bestScore)
                    {
                        bestLocalCov = localCov;
                        bestLocalFreq = localFreq;
                        bestLocalPrecip = localPrecip;
                        bestLocalPos = pos;
                    }
                }

                if (hits == 0 || covSum <= 0.001f)
                {
                    return false;
                }

                // Average over the full column (empty heights count as 0) so a single thin
                // layer cannot inflate coverage up to the storm threshold.
                float columnAvgCov = covSum / BelowCloudSampleFractions.Length;
                float avgFreq = freqWeighted / covSum;
                float avgPrecip = precipWeighted / covSum;
                coverage = Mathf.Lerp(columnAvgCov, bestLocalCov, 0.40f);
                frequency = Mathf.Lerp(avgFreq, bestLocalFreq, 0.40f);
                precip = Mathf.Lerp(avgPrecip, bestLocalPrecip, 0.40f);
                samplePos = bestLocalPos;
            }

            relativeFreq = GetRelativeLightningFrequency(volume, frequency);

            if (vesselInCloudHeight)
            {
                float vesselCoverage = volume.SampleCoverage(vesselPos, out float vesselCloudType, false);
                float vesselFrequency = volume.GetInterpolatedCloudTypeLightningFrequency(vesselCloudType);
                float vesselPrecip = SamplePrecipSignal(volume, vesselCloudType);
                float vesselRelative = GetRelativeLightningFrequency(volume, vesselFrequency);
                insideStorm = vesselCoverage >= ThunderboltSettings.MinCoverage
                    && vesselFrequency >= ThunderboltSettings.MinLightningFrequency
                    && PassesStormGates(vesselCoverage, vesselFrequency, vesselPrecip, vesselRelative, vesselInCloudHeight: true);
                if (insideStorm)
                {
                    coverage = Mathf.Max(coverage, vesselCoverage);
                    frequency = Mathf.Max(frequency, vesselFrequency);
                    precip = Mathf.Max(precip, vesselPrecip);
                    relativeFreq = Mathf.Max(relativeFreq, vesselRelative);
                }
            }

            boltOrigin = insideStorm
                ? bodyPos + up * Mathf.Min(outer - 1f, vesselRadius + 2000f)
                : samplePos;

            return true;
        }

        private static bool PassesStormGates(
            float coverage,
            float frequency,
            float precip,
            float relativeFreq,
            bool vesselInCloudHeight)
        {
            float coverageThreshold = vesselInCloudHeight
                ? ThunderboltSettings.MinCoverage
                : Mathf.Max(ThunderboltSettings.MinCoverage, ThunderboltSettings.BelowCloudCoverageFloor);

            if (coverage < coverageThreshold)
            {
                return false;
            }

            if (frequency < ThunderboltSettings.MinLightningFrequency)
            {
                return false;
            }

            // EVE packs often leave fair-weather types near the default lightningFrequency=1.
            // Demand that this type is among the stormier types in the volume when authors
            // differentiated them; if all types share the same freq, relative stays ~1.
            if (relativeFreq < ThunderboltSettings.MinRelativeLightningFrequency)
            {
                return false;
            }

            // Product floor kills thin cirrus with default freq=1 and thick calm fog with freq≈0.
            float productFloor = vesselInCloudHeight
                ? ThunderboltSettings.StormProductFloorInside
                : ThunderboltSettings.StormProductFloorBelow;
            if (coverage * frequency < productFloor)
            {
                return false;
            }

            // Below the cloud slab (runway / "sunny" days): require either a clearly strong
            // lightning-capable column, or precip/wetness corroboration like EVE's rain stack.
            if (!vesselInCloudHeight)
            {
                bool strongLightningColumn =
                    coverage * frequency >= ThunderboltSettings.StrongStormProduct
                    && frequency >= ThunderboltSettings.MinLightningFrequency * 1.25f
                    && relativeFreq >= 0.55f;

                bool wetStorm =
                    precip >= ThunderboltSettings.MinPrecipSignal
                    && coverage * frequency * Mathf.Clamp01(precip) >= productFloor * 0.85f;

                if (!strongLightningColumn && !wetStorm)
                {
                    return false;
                }
            }

            return true;
        }

        private static float ComputeStormStrength(
            float coverage,
            float frequency,
            float precip,
            float relativeFreq)
        {
            // Mirror EVE ParticleField/WetSurfaces: coverage × type density, with lightning weight.
            float precipFactor = Mathf.Lerp(0.40f, 1f, Mathf.Clamp01(precip));
            float relativeFactor = Mathf.Lerp(0.70f, 1f, Mathf.Clamp01(relativeFreq));
            return Mathf.Clamp01(coverage * frequency * precipFactor * relativeFactor);
        }

        private static float SamplePrecipSignal(CloudsRaymarchedVolume volume, float cloudType)
        {
            float particle = volume.GetInterpolatedCloudTypeParticleFieldDensity(cloudType);
            float wet = volume.GetInterpolatedCloudTypeWetSurfacesDensity(cloudType);
            float droplets = volume.GetInterpolatedCloudTypeDropletsDensity(cloudType);
            return Mathf.Max(particle, Mathf.Max(wet, droplets));
        }

        private static float GetRelativeLightningFrequency(CloudsRaymarchedVolume volume, float frequency)
        {
            List<CloudType> types = volume.CloudTypes;
            if (types == null || types.Count == 0)
            {
                return 1f;
            }

            float maxFreq = 0f;
            for (int i = 0; i < types.Count; i++)
            {
                if (types[i] != null)
                {
                    maxFreq = Mathf.Max(maxFreq, types[i].LightningFrequency);
                }
            }

            if (maxFreq <= 1e-4f)
            {
                return 0f;
            }

            return Mathf.Clamp01(frequency / maxFreq);
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
