using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using KSP.Localization;
using UnityEngine;

namespace Thunderbolt
{
    /// <summary>
    /// Page 1 col 1 — EVE / volumetric-cloud strike knobs.
    /// </summary>
    public class ThunderboltStrikeParameters : GameParameters.CustomParameterNode
    {
        public override string Title => Localizer.Format("#TB_ParamTitleStrikeEve");
        public override GameParameters.GameMode GameMode => GameParameters.GameMode.ANY;
        public override string Section => Localizer.Format("#TB_ParamSection1");
        public override string DisplaySection => Localizer.Format("#TB_ParamSection1");
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
            "#TB_ParamMinCoverage",
            toolTip = "#TB_ParamMinCoverage_tip",
            minValue = 0f,
            maxValue = 1f,
            stepCount = 50,
            displayFormat = "F2")]
        public float minCoverage = 0.40f;

        [GameParameters.CustomFloatParameterUI(
            "#TB_ParamMinLightningFrequency",
            toolTip = "#TB_ParamMinLightningFrequency_tip",
            minValue = 0f,
            maxValue = 1f,
            stepCount = 50,
            displayFormat = "F2")]
        public float minLightningFrequency = 0.25f;

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
                    minCoverage = 0.50f;
                    minLightningFrequency = 0.35f;
                    insideCloudChanceMultiplier = 3f;
                    break;
                case GameParameters.Preset.Moderate:
                    baseChancePerCheck = 0.028f;
                    minCoverage = 0.32f;
                    minLightningFrequency = 0.18f;
                    insideCloudChanceMultiplier = 7f;
                    break;
                case GameParameters.Preset.Hard:
                    baseChancePerCheck = 0.045f;
                    minCoverage = 0.25f;
                    minLightningFrequency = 0.12f;
                    insideCloudChanceMultiplier = 10f;
                    break;
                case GameParameters.Preset.Normal:
                default:
                    baseChancePerCheck = 0.018f;
                    minCoverage = 0.40f;
                    minLightningFrequency = 0.25f;
                    insideCloudChanceMultiplier = 5f;
                    break;
            }
        }
    }

    /// <summary>
    /// Page 1 col 3 — timing / eligibility shared by EVE and NonEVE.
    /// </summary>
    public class ThunderboltSharedParameters : GameParameters.CustomParameterNode
    {
        public override string Title => Localizer.Format("#TB_ParamTitleShared");
        public override GameParameters.GameMode GameMode => GameParameters.GameMode.ANY;
        public override string Section => Localizer.Format("#TB_ParamSection1");
        public override string DisplaySection => Localizer.Format("#TB_ParamSection1");
        public override int SectionOrder => 2;
        public override bool HasPresets => true;

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
            "#TB_ParamMaxTimeWarp",
            toolTip = "#TB_ParamMaxTimeWarp_tip",
            minValue = 1f,
            maxValue = 100f,
            stepCount = 99,
            displayFormat = "F0")]
        public float maxTimeWarp = 4f;

        [GameParameters.CustomParameterUI("#TB_ParamOnlyActive", toolTip = "#TB_ParamOnlyActive_tip")]
        public bool onlyActiveVessel = true;

        public static ThunderboltSharedParameters Instance =>
            HighLogic.CurrentGame?.Parameters.CustomParams<ThunderboltSharedParameters>();

        public override void SetDifficultyPreset(GameParameters.Preset preset)
        {
            switch (preset)
            {
                case GameParameters.Preset.Easy:
                    checkInterval = 5f;
                    vesselCooldown = 60f;
                    maxTimeWarp = 4f;
                    onlyActiveVessel = true;
                    break;
                case GameParameters.Preset.Moderate:
                    checkInterval = 2.5f;
                    vesselCooldown = 30f;
                    maxTimeWarp = 4f;
                    onlyActiveVessel = true;
                    break;
                case GameParameters.Preset.Hard:
                    checkInterval = 2f;
                    vesselCooldown = 20f;
                    maxTimeWarp = 4f;
                    onlyActiveVessel = true;
                    break;
                case GameParameters.Preset.Normal:
                default:
                    checkInterval = 3.5f;
                    vesselCooldown = 45f;
                    maxTimeWarp = 4f;
                    onlyActiveVessel = true;
                    break;
            }
        }
    }

    /// <summary>
    /// Atmospheric (no-EVE) strike knobs — page 1 with EVE.
    /// </summary>
    public class ThunderboltNonEveParameters : GameParameters.CustomParameterNode
    {
        public override string Title => Localizer.Format("#TB_ParamTitleStrikeNonEve");
        public override GameParameters.GameMode GameMode => GameParameters.GameMode.ANY;
        public override string Section => Localizer.Format("#TB_ParamSection1");
        public override string DisplaySection => Localizer.Format("#TB_ParamSection1");
        public override int SectionOrder => 1;
        public override bool HasPresets => true;

        [GameParameters.CustomFloatParameterUI(
            "#TB_ParamNonEveBaseChance",
            toolTip = "#TB_ParamNonEveBaseChance_tip",
            minValue = 0f,
            maxValue = 0.25f,
            stepCount = 50,
            displayFormat = "F3")]
        public float baseChancePerCheck = 0.018f;

        [GameParameters.CustomFloatParameterUI(
            "#TB_ParamNonEveStormCellChance",
            toolTip = "#TB_ParamNonEveStormCellChance_tip",
            minValue = 0.05f,
            maxValue = 0.90f,
            stepCount = 85,
            displayFormat = "F2")]
        public float stormCellChance = 0.38f;

        [GameParameters.CustomFloatParameterUI(
            "#TB_ParamNonEveDensityPower",
            toolTip = "#TB_ParamNonEveDensityPower_tip",
            minValue = 0.20f,
            maxValue = 2.00f,
            stepCount = 36,
            displayFormat = "F2")]
        public float densityPower = 0.85f;

        [GameParameters.CustomFloatParameterUI(
            "#TB_ParamNonEveStrengthScale",
            toolTip = "#TB_ParamNonEveStrengthScale_tip",
            minValue = 0.10f,
            maxValue = 1.50f,
            stepCount = 28,
            displayFormat = "F2")]
        public float stormStrengthScale = 0.45f;

        [GameParameters.CustomFloatParameterUI(
            "#TB_ParamNonEveMaxAltFraction",
            toolTip = "#TB_ParamNonEveMaxAltFraction_tip",
            minValue = 0.15f,
            maxValue = 0.80f,
            stepCount = 65,
            displayFormat = "F2")]
        public float maxAltitudeFraction = 0.42f;

        [GameParameters.CustomFloatParameterUI(
            "#TB_ParamNonEveMaxAltCap",
            toolTip = "#TB_ParamNonEveMaxAltCap_tip",
            minValue = 3000f,
            maxValue = 30000f,
            stepCount = 54,
            displayFormat = "F0")]
        public float maxAltitudeCap = 14000f;

        [GameParameters.CustomFloatParameterUI(
            "#TB_ParamNonEveInsideMultiplier",
            toolTip = "#TB_ParamNonEveInsideMultiplier_tip",
            minValue = 1f,
            maxValue = 25f,
            stepCount = 48,
            displayFormat = "F1")]
        public float insideStormChanceMultiplier = 5f;

        public static ThunderboltNonEveParameters Instance =>
            HighLogic.CurrentGame?.Parameters.CustomParams<ThunderboltNonEveParameters>();

        public override void SetDifficultyPreset(GameParameters.Preset preset)
        {
            switch (preset)
            {
                case GameParameters.Preset.Easy:
                    baseChancePerCheck = 0.01f;
                    stormCellChance = 0.28f;
                    densityPower = 0.70f;
                    stormStrengthScale = 0.35f;
                    maxAltitudeFraction = 0.35f;
                    maxAltitudeCap = 12000f;
                    insideStormChanceMultiplier = 3f;
                    break;
                case GameParameters.Preset.Moderate:
                    baseChancePerCheck = 0.028f;
                    stormCellChance = 0.48f;
                    densityPower = 0.95f;
                    stormStrengthScale = 0.55f;
                    maxAltitudeFraction = 0.48f;
                    maxAltitudeCap = 16000f;
                    insideStormChanceMultiplier = 7f;
                    break;
                case GameParameters.Preset.Hard:
                    baseChancePerCheck = 0.045f;
                    stormCellChance = 0.58f;
                    densityPower = 1.10f;
                    stormStrengthScale = 0.70f;
                    maxAltitudeFraction = 0.55f;
                    maxAltitudeCap = 18000f;
                    insideStormChanceMultiplier = 10f;
                    break;
                case GameParameters.Preset.Normal:
                default:
                    baseChancePerCheck = 0.018f;
                    stormCellChance = 0.38f;
                    densityPower = 0.85f;
                    stormStrengthScale = 0.45f;
                    maxAltitudeFraction = 0.42f;
                    maxAltitudeCap = 14000f;
                    insideStormChanceMultiplier = 5f;
                    break;
            }
        }
    }

    /// <summary>
    /// Damage rolls — page 2 with Debug.
    /// </summary>
    public class ThunderboltDamageParameters : GameParameters.CustomParameterNode
    {
        public override string Title => Localizer.Format("#TB_ParamTitleDamage");
        public override GameParameters.GameMode GameMode => GameParameters.GameMode.ANY;
        public override string Section => Localizer.Format("#TB_ParamSection2");
        public override string DisplaySection => Localizer.Format("#TB_ParamSection2");
        public override int SectionOrder => 0;
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
    /// Debug — page 2 with Damage.
    /// </summary>
    public class ThunderboltVisualParameters : GameParameters.CustomParameterNode
    {
        public override string Title => Localizer.Format("#TB_ParamTitleVisual");
        public override GameParameters.GameMode GameMode => GameParameters.GameMode.ANY;
        public override string Section => Localizer.Format("#TB_ParamSection2");
        public override string DisplaySection => Localizer.Format("#TB_ParamSection2");
        public override int SectionOrder => 1;
        public override bool HasPresets => false;

        [GameParameters.CustomParameterUI("#TB_ParamDebugLogging", toolTip = "#TB_ParamDebugLogging_tip")]
        public bool debugLogging = false;

        [GameParameters.CustomParameterUI("#TB_ParamDebugMode", toolTip = "#TB_ParamDebugMode_tip")]
        public bool debugMode = false;

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
