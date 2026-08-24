using System.Collections.Generic;
using KSP.Localization;
using UnityEngine;

namespace Thunderbolt
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class ThunderboltAddon : MonoBehaviour
    {
        private readonly Dictionary<uint, double> vesselCooldowns = new Dictionary<uint, double>();
        private float checkTimer;
        private bool loggedUnavailable;

        private void Start()
        {
            ThunderboltSettings.Load();

            if (!StormSampler.HasEveProvider && !loggedUnavailable)
            {
                loggedUnavailable = true;
                ThunderboltSettings.Log(
                    "No EVE cloud bridge — using atmospheric ASL-density lightning (Kerbin baseline). " +
                    "Install ThunderboltEVE + EVE/TVC for cloud-synced strikes.");
            }

            if (ThunderboltSettings.DebugMode)
            {
                ScreenMessages.PostScreenMessage(
                    $"[Thunderbolt Debug] ON — press {ThunderboltSettings.DebugStrikeKey} to strike.",
                    5f,
                    ScreenMessageStyle.UPPER_CENTER);
            }
        }

        private void Update()
        {
            if (HighLogic.LoadedScene != GameScenes.FLIGHT || FlightGlobals.ActiveVessel == null)
            {
                return;
            }

            if (ThunderboltSettings.DebugMode
                && !IsUiBlockingInput()
                && Input.GetKeyDown(ThunderboltSettings.DebugStrikeKey))
            {
                ForceStrikeActiveVessel();
            }

            float warp = TimeWarp.CurrentRate * Time.timeScale;
            if (warp > ThunderboltSettings.MaxTimeWarp)
            {
                return;
            }

            checkTimer += Time.unscaledDeltaTime;
            if (checkTimer < ThunderboltSettings.CheckInterval)
            {
                return;
            }

            checkTimer = 0f;
            TryStrikeLoadedVessels();
        }

        private void ForceStrikeActiveVessel()
        {
            Vessel vessel = FlightGlobals.ActiveVessel;
            if (vessel == null)
            {
                ThunderboltSettings.Log("Forced strike skipped: no active vessel.");
                return;
            }

            StormSample sample;
            if (StormSampler.TrySampleStormAboveVessel(vessel, out StormSample stormSample))
            {
                sample = stormSample;
                ThunderboltSettings.Log(
                    $"Forced strike ({StormSampler.ActiveModeLabel} cov={sample.Coverage:F2} freq={sample.LightningFrequency:F2} storm={sample.StormStrength:F2}).");
            }
            else
            {
                sample = BuildSyntheticSample(vessel);
                ThunderboltSettings.Log("Forced strike (synthetic cloud point).");
            }

            bool applyDamage = ThunderboltSettings.EnableDamage && ThunderboltSettings.DebugApplyDamage;
            StrikeVessel(vessel, sample, forced: true, applyDamage: applyDamage);

            if (ThunderboltSettings.ScreenMessages)
            {
                ScreenMessages.PostScreenMessage(
                    applyDamage ? "Debug lightning strike (with damage)!" : "Debug lightning strike!",
                    2.5f,
                    ScreenMessageStyle.UPPER_CENTER);
            }
        }

        private static StormSample BuildSyntheticSample(Vessel vessel)
        {
            Vector3 bodyPos = vessel.mainBody.position;
            Vector3 vesselPos = vessel.GetWorldPos3D();
            Vector3 up = (vesselPos - bodyPos).normalized;
            float radius = (vesselPos - bodyPos).magnitude + 2500f;
            return new StormSample(1f, 1f, 1f, bodyPos + up * radius, isInsideStormCloud: false);
        }

        private static bool IsUiBlockingInput()
        {
            return InputLockManager.IsLocked(ControlTypes.All)
                || (PauseMenu.exists && PauseMenu.isOpen)
                || GameSettings.MODIFIER_KEY.GetKey();
        }

        private void TryStrikeLoadedVessels()
        {
            double ut = Planetarium.GetUniversalTime();
            List<Vessel> vessels = FlightGlobals.VesselsLoaded;
            for (int i = 0; i < vessels.Count; i++)
            {
                Vessel vessel = vessels[i];
                if (!IsEligible(vessel))
                {
                    continue;
                }

                if (ThunderboltSettings.OnlyActiveVessel && vessel != FlightGlobals.ActiveVessel)
                {
                    continue;
                }

                if (vesselCooldowns.TryGetValue(vessel.persistentId, out double readyAt) && ut < readyAt)
                {
                    continue;
                }

                if (!StormSampler.TrySampleStormAboveVessel(vessel, out StormSample sample))
                {
                    continue;
                }

                // StormStrength: EVE (coverage × lightning × precip) or atmospheric (Kerbin-relative ASL density).
                float chance = ThunderboltSettings.BaseChancePerCheck * sample.StormStrength;
                if (vessel == FlightGlobals.ActiveVessel)
                {
                    chance *= 1.35f;
                }

                // Flying through the storm cloud itself is far more dangerous.
                if (sample.IsInsideStormCloud)
                {
                    chance *= ThunderboltSettings.InsideCloudChanceMultiplier;
                }

                chance = Mathf.Clamp01(chance);

                ThunderboltSettings.Log(
                    $"Candidate {vessel.vesselName}: cov={sample.Coverage:F2} freq={sample.LightningFrequency:F2} " +
                    $"storm={sample.StormStrength:F2} inside={sample.IsInsideStormCloud} chance={chance:F3}");

                if (Random.value > chance)
                {
                    continue;
                }

                StrikeVessel(vessel, sample, forced: false, applyDamage: ThunderboltSettings.EnableDamage);
            }
        }

        private static bool IsEligible(Vessel vessel)
        {
            if (vessel == null || !vessel.loaded || vessel.packed)
            {
                return false;
            }

            // Flying, pre-launch, landed, splashed, and EVA are all eligible
            // as long as they are under a storm cloud layer.

            if (vessel.mainBody == null || !vessel.mainBody.atmosphere)
            {
                return false;
            }

            if (vessel.altitude > vessel.mainBody.atmosphereDepth)
            {
                return false;
            }

            // Ignore tiny spectator debris, but never skip EVA kerbals for mass.
            if (!vessel.isEVA && vessel != FlightGlobals.ActiveVessel && vessel.GetTotalMass() < 0.2f)
            {
                return false;
            }

            return true;
        }

        private void StrikeVessel(Vessel vessel, StormSample sample, bool forced, bool applyDamage)
        {
            // External / pad rods divert random strikes (not debug force-strikes).
            if (!forced && ThunderboltRodRegistry.TryDivert(vessel, out IThunderboltRod rod) && rod != null)
            {
                StrikeRod(vessel, sample, rod, applyDamage);
                return;
            }

            Part target = PickStrikePart(vessel, forced);
            if (target == null)
            {
                return;
            }

            Vector3 strikePoint = target.partTransform != null ? target.partTransform.position : vessel.GetWorldPos3D();
            ModuleThunderboltRod mountedRod = target.FindModuleImplementing<ModuleThunderboltRod>();
            if (mountedRod != null)
            {
                strikePoint = mountedRod.StrikePoint;
            }

            // Procedural bolt always. Flying pierces nose→tail→ground; landed/etc. stop at nose.
            bool exitThroughTail = !vessel.isEVA && vessel.situation == Vessel.Situations.FLYING;
            if (!vessel.isEVA
                && ThunderboltPiercingPath.TryBuild(
                    vessel,
                    sample.CloudSampleWorldPosition,
                    exitThroughTail,
                    out List<Vector3> path,
                    out int hiddenBodySegment))
            {
                ThunderboltFx.SpawnPath(path, hiddenBodySegment);
                ThunderboltSettings.Log(
                    $"Bolt path points={path.Count} pierce={exitThroughTail} on {vessel.vesselName}");
            }
            else if (ThunderboltPiercingPath.TryBuildToPoint(
                vessel,
                sample.CloudSampleWorldPosition,
                strikePoint,
                out List<Vector3> hitPath))
            {
                ThunderboltFx.SpawnPath(hitPath);
            }
            else
            {
                ThunderboltFx.Spawn(sample.CloudSampleWorldPosition, strikePoint);
            }

            bool willDestroy = applyDamage && RollDestroyStruckPart(vessel, target, forced);

            // BlastFX opaque fireball wraps the part, then quietly deletes it mid-burst.
            ThunderboltBlastBridge.SpawnAtPoint(strikePoint, target, willDestroy);

            if (!forced)
            {
                vesselCooldowns[vessel.persistentId] = Planetarium.GetUniversalTime() + ThunderboltSettings.VesselCooldown;
            }

            string targetTitle = GetStrikeDisplayName(vessel, target);
            string msg = forced
                ? $"Forced lightning hit {vessel.vesselName} / {targetTitle} destroyed={willDestroy}"
                : $"Lightning struck {vessel.vesselName} / {targetTitle} destroyed={willDestroy}";

            ThunderboltSettings.Log(
                msg + $" cov={sample.Coverage:F2} freq={sample.LightningFrequency:F2} storm={sample.StormStrength:F2} damage={applyDamage} eva={vessel.isEVA}");

            // Flight Results left-column event log (skip pure visual debug spam).
            if (!forced || willDestroy || applyDamage)
            {
                LogFlightResult(targetTitle, willDestroy, vessel.isEVA);
            }

            if (ThunderboltSettings.ScreenMessages)
            {
                string screen;
                if (willDestroy)
                {
                    screen = vessel.isEVA
                        ? Localizer.Format("#TB_evaKilledScreen", targetTitle)
                        : Localizer.Format("#TB_strikeDestroyedScreen", targetTitle);
                }
                else
                {
                    screen = forced
                        ? Localizer.Format("#TB_forceStrikeMessage", targetTitle)
                        : Localizer.Format("#TB_strikeMessage", targetTitle);
                }

                if (!forced || willDestroy)
                {
                    ScreenMessages.PostScreenMessage(screen, 4f, ScreenMessageStyle.UPPER_CENTER);
                }
            }
        }

        private static string GetStrikeDisplayName(Vessel vessel, Part target)
        {
            if (vessel != null && vessel.isEVA)
            {
                List<ProtoCrewMember> crew = vessel.GetVesselCrew();
                if (crew != null && crew.Count > 0 && !string.IsNullOrEmpty(crew[0].name))
                {
                    return crew[0].name;
                }

                if (!string.IsNullOrEmpty(vessel.vesselName))
                {
                    return vessel.vesselName;
                }
            }

            return target?.partInfo?.title ?? target?.name ?? vessel?.vesselName ?? "unknown";
        }

        private void StrikeRod(Vessel vessel, StormSample sample, IThunderboltRod rod, bool applyDamage)
        {
            Vector3 strikePoint = rod.StrikePoint;
            if (ThunderboltPiercingPath.TryBuildToPoint(
                vessel,
                sample.CloudSampleWorldPosition,
                strikePoint,
                out List<Vector3> path))
            {
                ThunderboltFx.SpawnPath(path);
            }
            else
            {
                ThunderboltFx.Spawn(sample.CloudSampleWorldPosition, strikePoint);
            }

            bool willDestroy = rod.TryAbsorbStrike(applyDamage);
            Part rodPart = (rod as ModuleThunderboltRod)?.part;
            ThunderboltBlastBridge.SpawnAtPoint(strikePoint, rodPart, willDestroy);

            vesselCooldowns[vessel.persistentId] = Planetarium.GetUniversalTime() + ThunderboltSettings.VesselCooldown;

            string rodTitle = rod.DisplayName;
            ThunderboltSettings.Log(
                $"Strike diverted to rod '{rodTitle}' protecting {vessel.vesselName} " +
                $"destroyed={willDestroy} cov={sample.Coverage:F2} freq={sample.LightningFrequency:F2}");

            LogFlightResultDiverted(rodTitle, vessel.vesselName);

            if (ThunderboltSettings.ScreenMessages)
            {
                string screen = Localizer.Format("#TB_divertMessage", rodTitle);
                if (string.IsNullOrEmpty(screen) || screen.StartsWith("#TB_", System.StringComparison.Ordinal))
                {
                    screen = $"Lightning diverted to {rodTitle}!";
                }

                ScreenMessages.PostScreenMessage(screen, 4f, ScreenMessageStyle.UPPER_CENTER);
            }
        }

        private static void LogFlightResultDiverted(string rodTitle, string vesselName)
        {
            if (FlightLogger.fetch == null)
            {
                return;
            }

            string eventMsg = Localizer.Format("#TB_FlightLogDiverted", rodTitle, vesselName);
            if (string.IsNullOrEmpty(eventMsg) || eventMsg.StartsWith("#TB_", System.StringComparison.Ordinal))
            {
                eventMsg = $"Lightning was diverted to {rodTitle}, sparing {vesselName}.";
            }

            FlightLogger.fetch.LogEvent(eventMsg);
        }

        private static void LogFlightResult(string partTitle, bool destroyed, bool isEva)
        {
            if (FlightLogger.fetch == null)
            {
                return;
            }

            string eventMsg;
            if (isEva)
            {
                eventMsg = destroyed
                    ? Localizer.Format("#TB_FlightLogEvaKilled", partTitle)
                    : Localizer.Format("#TB_FlightLogEvaStruck", partTitle);
            }
            else
            {
                eventMsg = destroyed
                    ? Localizer.Format("#TB_FlightLogDestroyed", partTitle)
                    : Localizer.Format("#TB_FlightLogStruck", partTitle);
            }

            if (string.IsNullOrEmpty(eventMsg) || eventMsg.StartsWith("#TB_", System.StringComparison.Ordinal))
            {
                if (isEva)
                {
                    eventMsg = destroyed
                        ? $"{partTitle} was killed by lightning."
                        : $"{partTitle} was struck by lightning on EVA.";
                }
                else
                {
                    eventMsg = destroyed
                        ? $"{partTitle} was destroyed by lightning."
                        : $"{partTitle} was struck by lightning.";
                }
            }

            FlightLogger.fetch.LogEvent(eventMsg);
        }

        /// <summary>
        /// Weighted random pick among vessel parts.
        /// All parts are eligible; antennas/solar are only slightly more likely.
        /// When root protection is on, the root is skipped if other parts exist.
        /// </summary>
        private static Part PickStrikePart(Vessel vessel, bool forced)
        {
            if (vessel == null || vessel.parts == null || vessel.parts.Count == 0)
            {
                return null;
            }

            Vector3 bodyPos = vessel.mainBody.position;
            float minRadius = float.MaxValue;
            float maxRadius = float.MinValue;
            int aliveCount = 0;

            for (int i = 0; i < vessel.parts.Count; i++)
            {
                Part part = vessel.parts[i];
                if (part == null || part.State == PartStates.DEAD || part.partTransform == null)
                {
                    continue;
                }

                aliveCount++;
                float radius = (part.partTransform.position - bodyPos).magnitude;
                if (radius < minRadius) minRadius = radius;
                if (radius > maxRadius) maxRadius = radius;
            }

            if (aliveCount == 0)
            {
                return vessel.rootPart;
            }

            bool skipProtectedRoot = !forced
                && ThunderboltSettings.ProtectRootPart
                && aliveCount > 1;

            float totalWeight = 0f;
            // Reuse a small local list via two parallel arrays sized to part count.
            Part[] candidates = new Part[vessel.parts.Count];
            float[] weights = new float[vessel.parts.Count];
            int count = 0;
            float radiusSpan = Mathf.Max(1f, maxRadius - minRadius);

            for (int i = 0; i < vessel.parts.Count; i++)
            {
                Part part = vessel.parts[i];
                if (part == null || part.State == PartStates.DEAD || part.partTransform == null)
                {
                    continue;
                }

                if (skipProtectedRoot && part == vessel.rootPart)
                {
                    continue;
                }

                float radius = (part.partTransform.position - bodyPos).magnitude;
                float heightBias = 0.85f + 0.30f * ((radius - minRadius) / radiusSpan); // top parts a bit likelier
                float weight = (1f + GetVulnerabilityBonus(part)) * heightBias;

                candidates[count] = part;
                weights[count] = weight;
                totalWeight += weight;
                count++;
            }

            if (count == 0)
            {
                return vessel.rootPart;
            }

            float roll = Random.Range(0f, totalWeight);
            float cumulative = 0f;
            for (int i = 0; i < count; i++)
            {
                cumulative += weights[i];
                if (roll <= cumulative)
                {
                    return candidates[i];
                }
            }

            return candidates[count - 1];
        }

        /// <summary>
        /// Extra pick weight only — does not restrict which parts can be struck.
        /// </summary>
        private static float GetVulnerabilityBonus(Part part)
        {
            if (part.Modules.Contains("ModuleDeployableSolarPanel") || part.Modules.Contains("ModuleDataTransmitter"))
            {
                return 1.5f;
            }

            string name = part.name ?? string.Empty;
            string title = part.partInfo != null ? part.partInfo.title ?? string.Empty : string.Empty;
            string haystack = (name + " " + title).ToLowerInvariant();

            if (haystack.Contains("antenna") || haystack.Contains("commun") || haystack.Contains("relay"))
            {
                return 2f;
            }

            if (haystack.Contains("solar") || haystack.Contains("panel") || haystack.Contains("photovoltaic"))
            {
                return 1.5f;
            }

            // Command/probe: no pick monopoly — they already use a lower destroy chance.
            return 0f;
        }

        /// <summary>
        /// Rolls whether the struck part should be destroyed. Does not explode yet —
        /// BlastFX deletes it once the opaque fireball covers the mesh.
        /// Forced debug strikes ignore root-part protection so single-part test craft can be destroyed.
        /// </summary>
        private static bool RollDestroyStruckPart(Vessel vessel, Part target, bool forced)
        {
            if (target == null || target.State == PartStates.DEAD)
            {
                return false;
            }

            string title = GetStrikeDisplayName(vessel, target);

            // EVA uses its own low kill chance; craft root protection does not apply.
            if (vessel != null && vessel.isEVA)
            {
                float evaChance = forced ? 1f : Mathf.Clamp01(ThunderboltSettings.EvaKillChance);
                if (evaChance <= 0f || Random.value > evaChance)
                {
                    ThunderboltSettings.Log($"EVA kill roll failed for {title} (p={evaChance:F2}).");
                    return false;
                }

                ThunderboltSettings.Log($"Scheduling EVA kill {title} behind fireball (p={evaChance:F2}, forced={forced}).");
                return true;
            }

            if (!forced && ThunderboltSettings.ProtectRootPart && vessel != null && target == vessel.rootPart)
            {
                ThunderboltSettings.Log($"Root part protected — skip destroy ({title}).");
                return false;
            }

            float chance = GetDestroyChance(target);
            // Debug force-strike with damage: always destroy so testing is unambiguous.
            if (forced)
            {
                chance = 1f;
            }

            if (chance <= 0f || Random.value > chance)
            {
                ThunderboltSettings.Log($"Destroy roll failed for {title} (p={chance:F2}, command={IsCommandPart(target)}).");
                return false;
            }

            ThunderboltSettings.Log($"Scheduling part destroy {title} behind fireball (p={chance:F2}, forced={forced}).");
            return true;
        }

        private static float GetDestroyChance(Part part)
        {
            if (IsCommandPart(part))
            {
                return Mathf.Clamp01(ThunderboltSettings.CommandDestroyChance);
            }

            if (IsVulnerablePart(part))
            {
                return Mathf.Clamp01(ThunderboltSettings.VulnerableDestroyChance);
            }

            return Mathf.Clamp01(ThunderboltSettings.PartDestroyChance);
        }

        private static bool IsVulnerablePart(Part part)
        {
            if (part.Modules.Contains("ModuleDeployableSolarPanel") || part.Modules.Contains("ModuleDataTransmitter"))
            {
                return true;
            }

            string name = part.name ?? string.Empty;
            string title = part.partInfo != null ? part.partInfo.title ?? string.Empty : string.Empty;
            string haystack = (name + " " + title).ToLowerInvariant();
            return haystack.Contains("antenna")
                || haystack.Contains("commun")
                || haystack.Contains("relay")
                || haystack.Contains("solar")
                || haystack.Contains("panel")
                || haystack.Contains("photovoltaic");
        }

        private static bool IsCommandPart(Part part)
        {
            if (part == null)
            {
                return false;
            }

            if (part.isControlSource != Vessel.ControlLevel.NONE || part.Modules.Contains("ModuleCommand"))
            {
                return true;
            }

            string name = part.name ?? string.Empty;
            string title = part.partInfo != null ? part.partInfo.title ?? string.Empty : string.Empty;
            string haystack = (name + " " + title).ToLowerInvariant();
            return haystack.Contains("command") || haystack.Contains("probe") || haystack.Contains("cockpit") || haystack.Contains("avionics");
        }
    }
}
