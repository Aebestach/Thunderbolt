using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Thunderbolt
{
    /// <summary>
    /// Soft reflection into EVE LightningConfig when Atmosphere.dll is present.
    /// Core Thunderbolt never hard-references Atmosphere types.
    /// </summary>
    internal sealed class EveLightningHints
    {
        public float LifeTime = 0.5f;
        public float LightIntensity = 4.5f;
        public float LightRange = 9000f;
        public Color BoltColor = new Color(1.2f, 1.2f, 1.2f, 1f);
        public Color LightColor = new Color(0.55f, 0.6f, 1f, 1f);
        public float SoundMaxDistance = 15000f;
        public float SoundMinDistance = 2000f;
        public float SoundFarThreshold = 5000f;
        public float RealisticAudioDelayMultiplier;
        public readonly List<string> NearSoundNames = new List<string>();
        public readonly List<string> FarSoundNames = new List<string>();

        private static bool resolved;
        private static MethodInfo getObjectList;
        private static PropertyInfo nameProp;
        private static PropertyInfo lifeTimeProp;
        private static PropertyInfo lightIntensityProp;
        private static PropertyInfo lightRangeProp;
        private static PropertyInfo boltColorProp;
        private static PropertyInfo lightColorProp;
        private static PropertyInfo soundMaxProp;
        private static PropertyInfo soundMinProp;
        private static PropertyInfo soundFarProp;
        private static PropertyInfo delayProp;
        private static PropertyInfo nearSoundsProp;
        private static PropertyInfo farSoundsProp;
        private static PropertyInfo soundNameProp;

        public static EveLightningHints TryGet()
        {
            EnsureResolved();
            if (getObjectList == null)
            {
                return null;
            }

            try
            {
                object listObj = getObjectList.Invoke(null, null);
                if (!(listObj is IList list) || list.Count == 0)
                {
                    return null;
                }

                string bodyName = FlightGlobals.currentMainBody != null
                    ? FlightGlobals.currentMainBody.bodyName
                    : null;

                object chosen = null;
                if (!string.IsNullOrEmpty(bodyName) && nameProp != null)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        object item = list[i];
                        string name = nameProp.GetValue(item, null) as string;
                        if (name != null && name.IndexOf(bodyName, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            chosen = item;
                            break;
                        }
                    }
                }

                if (chosen == null && nameProp != null)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        object item = list[i];
                        string name = nameProp.GetValue(item, null) as string;
                        if (name != null && name.IndexOf("Kerbin", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            chosen = item;
                            break;
                        }
                    }
                }

                if (chosen == null)
                {
                    chosen = list[0];
                }

                return chosen == null ? null : FromConfig(chosen);
            }
            catch
            {
                return null;
            }
        }

        private static EveLightningHints FromConfig(object cfg)
        {
            EveLightningHints hints = new EveLightningHints();
            hints.LifeTime = ReadFloat(lifeTimeProp, cfg, hints.LifeTime);
            hints.LightIntensity = ReadFloat(lightIntensityProp, cfg, hints.LightIntensity);
            hints.LightRange = ReadFloat(lightRangeProp, cfg, hints.LightRange);
            hints.BoltColor = ReadColor(boltColorProp, cfg, hints.BoltColor);
            hints.LightColor = ReadColor(lightColorProp, cfg, hints.LightColor);
            hints.SoundMaxDistance = ReadFloat(soundMaxProp, cfg, hints.SoundMaxDistance);
            hints.SoundMinDistance = ReadFloat(soundMinProp, cfg, hints.SoundMinDistance);
            hints.SoundFarThreshold = ReadFloat(soundFarProp, cfg, hints.SoundFarThreshold);
            hints.RealisticAudioDelayMultiplier = ReadFloat(delayProp, cfg, hints.RealisticAudioDelayMultiplier);
            FillSoundNames(nearSoundsProp, cfg, hints.NearSoundNames);
            FillSoundNames(farSoundsProp, cfg, hints.FarSoundNames);
            return hints;
        }

        private static void EnsureResolved()
        {
            if (resolved)
            {
                return;
            }

            resolved = true;
            Type managerType = Type.GetType("Atmosphere.LightningManager, Atmosphere");
            Type configType = Type.GetType("Atmosphere.LightningConfig, Atmosphere");
            Type soundType = Type.GetType("Atmosphere.LightningSoundConfig, Atmosphere");
            if (managerType == null || configType == null)
            {
                return;
            }

            getObjectList = managerType.GetMethod(
                "GetObjectList",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            nameProp = configType.GetProperty("Name");
            lifeTimeProp = configType.GetProperty("LifeTime");
            lightIntensityProp = configType.GetProperty("LightIntensity");
            lightRangeProp = configType.GetProperty("LightRange");
            boltColorProp = configType.GetProperty("BoltColor");
            lightColorProp = configType.GetProperty("LightColor");
            soundMaxProp = configType.GetProperty("SoundMaxDistance");
            soundMinProp = configType.GetProperty("SoundMinDistance");
            soundFarProp = configType.GetProperty("SoundFarThreshold");
            delayProp = configType.GetProperty("RealisticAudioDelayMultiplier");
            nearSoundsProp = configType.GetProperty("NearSoundNames");
            farSoundsProp = configType.GetProperty("FarSoundNames");
            soundNameProp = soundType?.GetProperty("SoundName");
        }

        private static float ReadFloat(PropertyInfo prop, object target, float fallback)
        {
            if (prop == null)
            {
                return fallback;
            }

            try
            {
                object value = prop.GetValue(target, null);
                return value == null ? fallback : Convert.ToSingle(value);
            }
            catch
            {
                return fallback;
            }
        }

        private static Color ReadColor(PropertyInfo prop, object target, Color fallback)
        {
            if (prop == null)
            {
                return fallback;
            }

            try
            {
                object value = prop.GetValue(target, null);
                return value is Color color ? color : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private static void FillSoundNames(PropertyInfo prop, object target, List<string> into)
        {
            into.Clear();
            if (prop == null || soundNameProp == null)
            {
                return;
            }

            try
            {
                if (!(prop.GetValue(target, null) is IList list))
                {
                    return;
                }

                for (int i = 0; i < list.Count; i++)
                {
                    object item = list[i];
                    string path = soundNameProp.GetValue(item, null) as string;
                    if (!string.IsNullOrEmpty(path))
                    {
                        into.Add(path);
                    }
                }
            }
            catch
            {
                // ignore soft failures
            }
        }
    }
}
