using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using KSP.Localization;
using UnityEngine;

namespace Thunderbolt
{
    /// <summary>
    /// Column 1 — strike chance / timing / cloud thresholds.
    /// </summary>
    public class ThunderboltStrikeParameters : GameParameters.CustomParameterNode
    {
        public override string Title => Localizer.Format("#TB_ParamTitleStrike");
        public override GameParameters.GameMode GameMode => GameParameters.GameMode.ANY;
        public override string Section => "Thunderbolt";
        public override string DisplaySection => Localizer.Format("#TB_ParamSection");
        public override int SectionOrder => 0;
        public override bool HasPresets => true;

        [GameParameters.CustomFloatParameterUI(
            "#TB_ParamBaseChance",
            toolTip = "#TB_ParamBaseChance_tip",
            minValue = 0f,
            maxValue = 0.25f,
            stepCount = 50,
            displayFormat = "F3")]
        public float baseChancePerCheck = 0.018f;

        [GameParameters.CustomFloatParameterUI(
            "#TB_ParamCheckInterval",
            toolTip = "#TB_ParamCheckInterval_tip",
            minValue = 0.5f,
            maxValue = 30f,
            stepCount = 59,
            displayFormat = "F1")]
        public float checkInterval = 3.5f;

        [GameParameters.CustomFloatParameterUI(
            "#TB_ParamVesselCooldown",
            toolTip = "#TB_ParamVesselCooldown_tip",
            minValue = 0f,
            maxValue = 300f,
            stepCount = 60,
            displayFormat = "F0")]
        public float vesselCooldown = 45f;

        [GameParameters.CustomFloatParameterUI(
            "#TB_ParamMinCoverage",
            toolTip = "#TB_ParamMinCoverage_tip",
            minValue = 0f,
            maxValue = 1f,
            stepCount = 50,
            displayFormat = "F2")]
        public float minCoverage = 0.35f;

        [GameParameters.CustomFloatParameterUI(
            "#TB_ParamMinLightningFrequency",
            toolTip = "#TB_ParamMinLightningFrequency_tip",
            minValue = 0f,
            maxValue = 1f,
            stepCount = 50,
            displayFormat = "F2")]
        public float minLightningFrequency = 0.15f;

        [GameParameters.CustomFloatParameterUI(
            "#TB_ParamMaxTimeWarp",
            toolTip = "#TB_ParamMaxTimeWarp_tip",
            minValue = 1f,
            maxValue = 100f,
            stepCount = 99,
            displayFormat = "F0")]
        public float maxTimeWarp = 4f;

        [GameParameters.CustomParameterUI("#TB_ParamOnlyActive", toolTip = "#TB_ParamOnlyActive_tip")]
        public bool onlyActiveVessel = true;

        [GameParameters.CustomFloatParameterUI(
            "#TB_ParamInsideCloudMultiplier",
            toolTip = "#TB_ParamInsideCloudMultiplier_tip",
            minValue = 1f,
            maxValue = 25f,
            stepCount = 48,
            displayFormat = "F1")]
        public float insideCloudChanceMultiplier = 5f;

        public static ThunderboltStrikeParameters Instance =>
            HighLogic.CurrentGame?.Parameters.CustomParams<ThunderboltStrikeParameters>();

        public override void SetDifficultyPreset(GameParameters.Preset preset)
        {
            switch (preset)
            {
                case GameParameters.Preset.Easy:
                    baseChancePerCheck = 0.01f;
                    checkInterval = 5f;
                    vesselCooldown = 60f;
                    minCoverage = 0.45f;
                    minLightningFrequency = 0.25f;
                    maxTimeWarp = 4f;
                    onlyActiveVessel = true;
                    insideCloudChanceMultiplier = 3f;
                    break;
                case GameParameters.Preset.Moderate:
                    baseChancePerCheck = 0.028f;
                    checkInterval = 2.5f;
                    vesselCooldown = 30f;
                    minCoverage = 0.25f;
                    minLightningFrequency = 0.1f;
                    maxTimeWarp = 4f;
                    onlyActiveVessel = true;
                    insideCloudChanceMultiplier = 7f;
                    break;
                case GameParameters.Preset.Hard:
                    baseChancePerCheck = 0.045f;
                    checkInterval = 2f;
                    vesselCooldown = 20f;
                    minCoverage = 0.18f;
                    minLightningFrequency = 0.08f;
                    maxTimeWarp = 4f;
                    onlyActiveVessel = true;
                    insideCloudChanceMultiplier = 10f;
                    break;
                case GameParameters.Preset.Normal:
                default:
                    baseChancePerCheck = 0.018f;
                    checkInterval = 3.5f;
                    vesselCooldown = 45f;
                    minCoverage = 0.35f;
                    minLightningFrequency = 0.15f;
                    maxTimeWarp = 4f;
                    onlyActiveVessel = true;
                    insideCloudChanceMultiplier = 5f;
                    break;
            }
        }
    }

    /// <summary>
    /// Column 2 — damage rolls.
    /// </summary>
    public class ThunderboltDamageParameters : GameParameters.CustomParameterNode
    {
        public override string Title => Localizer.Format("#TB_ParamTitleDamage");
        public override GameParameters.GameMode GameMode => GameParameters.GameMode.ANY;
        public override string Section => "Thunderbolt";
        public override string DisplaySection => Localizer.Format("#TB_ParamSection");
        public override int SectionOrder => 1;
        public override bool HasPresets => true;

        [GameParameters.CustomParameterUI("#TB_ParamEnableDamage", toolTip = "#TB_ParamEnableDamage_tip")]
        public bool enableDamage = true;

        [GameParameters.CustomFloatParameterUI(
            "#TB_ParamPartDestroyChance",
            toolTip = "#TB_ParamPartDestroyChance_tip",
            minValue = 0f,
            maxValue = 1f,
            stepCount = 20,
            displayFormat = "F2")]
        public float partDestroyChance = 0.45f;

        [GameParameters.CustomFloatParameterUI(
            "#TB_ParamVulnerableDestroyChance",
            toolTip = "#TB_ParamVulnerableDestroyChance_tip",
            minValue = 0f,
            maxValue = 1f,
            stepCount = 20,
            displayFormat = "F2")]
        public float vulnerableDestroyChance = 0.80f;

        [GameParameters.CustomFloatParameterUI(
            "#TB_ParamCommandDestroyChance",
            toolTip = "#TB_ParamCommandDestroyChance_tip",
            minValue = 0f,
            maxValue = 1f,
            stepCount = 20,
            displayFormat = "F2")]
        public float commandDestroyChance = 0.12f;

        [GameParameters.CustomFloatParameterUI(
            "#TB_ParamEvaKillChance",
            toolTip = "#TB_ParamEvaKillChance_tip",
            minValue = 0f,
            maxValue = 1f,
            stepCount = 20,
            displayFormat = "F2")]
        public float evaKillChance = 0.08f;

        [GameParameters.CustomParameterUI("#TB_ParamProtectRoot", toolTip = "#TB_ParamProtectRoot_tip")]
        public bool protectRootPart = false;

        [GameParameters.CustomParameterUI("#TB_ParamScreenMessages", toolTip = "#TB_ParamScreenMessages_tip")]
        public bool screenMessages = true;

        public static ThunderboltDamageParameters Instance =>
            HighLogic.CurrentGame?.Parameters.CustomParams<ThunderboltDamageParameters>();

        public override void SetDifficultyPreset(GameParameters.Preset preset)
        {
            switch (preset)
            {
                case GameParameters.Preset.Easy:
                    enableDamage = true;
                    partDestroyChance = 0.20f;
                    vulnerableDestroyChance = 0.45f;
                    commandDestroyChance = 0.05f;
                    evaKillChance = 0.03f;
                    protectRootPart = false;
                    screenMessages = true;
                    break;
                case GameParameters.Preset.Moderate:
                    enableDamage = true;
                    partDestroyChance = 0.60f;
                    vulnerableDestroyChance = 0.90f;
                    commandDestroyChance = 0.20f;
                    evaKillChance = 0.12f;
                    protectRootPart = false;
                    screenMessages = true;
                    break;
                case GameParameters.Preset.Hard:
                    enableDamage = true;
                    partDestroyChance = 0.75f;
                    vulnerableDestroyChance = 1.0f;
                    commandDestroyChance = 0.35f;
                    evaKillChance = 0.20f;
                    protectRootPart = false;
                    screenMessages = true;
                    break;
                case GameParameters.Preset.Normal:
                default:
                    enableDamage = true;
                    partDestroyChance = 0.45f;
                    vulnerableDestroyChance = 0.80f;
                    commandDestroyChance = 0.12f;
                    evaKillChance = 0.08f;
                    protectRootPart = false;
                    screenMessages = true;
                    break;
            }
        }
    }

    /// <summary>
    /// Column 3 — debug only (bolt drawn by Thunderbolt/ProceduralBolt; light/sound still from EVE).
    /// </summary>
    public class ThunderboltVisualParameters : GameParameters.CustomParameterNode
    {
        public override string Title => Localizer.Format("#TB_ParamTitleVisual");
        public override GameParameters.GameMode GameMode => GameParameters.GameMode.ANY;
        public override string Section => "Thunderbolt";
        public override string DisplaySection => Localizer.Format("#TB_ParamSection");
        public override int SectionOrder => 2;
        public override bool HasPresets => false;

        [GameParameters.CustomParameterUI("#TB_ParamDebugLogging", toolTip = "#TB_ParamDebugLogging_tip")]
        public bool debugLogging = false;

        [GameParameters.CustomParameterUI("#TB_ParamDebugMode", toolTip = "#TB_ParamDebugMode_tip")]
        public bool debugMode = false;

        [GameParameters.CustomParameterUI("#TB_ParamDebugApplyDamage", toolTip = "#TB_ParamDebugApplyDamage_tip")]
        public bool debugApplyDamage = false;

        [GameParameters.CustomStringParameterUI("#TB_ParamDebugStrikeKey", toolTip = "#TB_ParamDebugStrikeKey_tip")]
        public string debugStrikeKey = "L";

        public static ThunderboltVisualParameters Instance =>
            HighLogic.CurrentGame?.Parameters.CustomParams<ThunderboltVisualParameters>();

        public KeyCode ResolvedStrikeKey
        {
            get
            {
                return Enum.TryParse(debugStrikeKey, true, out KeyCode key) ? key : KeyCode.L;
            }
        }

        public override IList ValidValues(MemberInfo member)
        {
            if (member.Name != "debugStrikeKey")
            {
                return null;
            }

            return new List<string>
            {
                "L", "K", "J", "H",
                "F6", "F7", "F8", "F9", "F10",
                "Keypad0", "Keypad1", "Keypad2"
            };
        }
    }
}
