using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace Thunderbolt
{
    /// <summary>
    /// Soft Kerbal Konstructs bridge: reads STATIC MODULE configs and ensures
    /// <see cref="ModuleThunderboltRodStatic"/> is configured on spawned buildings.
    /// Works without a hard KK assembly reference.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class ThunderboltKkRodSupport : MonoBehaviour
    {
        private const string RodClassName = "ModuleThunderboltRodStatic";

        private static bool kkMissingLogged;
        private float refreshTimer;

        private void Start()
        {
            StartCoroutine(RefreshSoon());
        }

        private IEnumerator RefreshSoon()
        {
            yield return new WaitForSecondsRealtime(2f);
            TrySyncAll();
            yield return new WaitForSecondsRealtime(5f);
            TrySyncAll();
        }

        private void Update()
        {
            refreshTimer += Time.unscaledDeltaTime;
            if (refreshTimer < 8f)
            {
                return;
            }

            refreshTimer = 0f;
            TrySyncAll();
        }

        private void TrySyncAll()
        {
            Assembly kkAsm = FindKkAssembly();
            if (kkAsm == null)
            {
                if (!kkMissingLogged && ThunderboltSettings.DebugLogging)
                {
                    kkMissingLogged = true;
                    Debug.Log("[Thunderbolt] KerbalKonstructs not loaded — static rod bridge idle.");
                }

                return;
            }

            try
            {
                Type dbType = kkAsm.GetType("KerbalKonstructs.Core.StaticDatabase");
                if (dbType == null)
                {
                    return;
                }

                MethodInfo getAll = dbType.GetMethod("GetAllStatics", BindingFlags.Public | BindingFlags.Static);
                if (getAll == null)
                {
                    return;
                }

                object instancesObj = getAll.Invoke(null, null);
                if (!(instancesObj is Array instances))
                {
                    return;
                }

                int synced = 0;
                for (int i = 0; i < instances.Length; i++)
                {
                    object instance = instances.GetValue(i);
                    if (instance == null)
                    {
                        continue;
                    }

                    if (TrySyncInstance(instance))
                    {
                        synced++;
                    }
                }

                if (ThunderboltSettings.DebugLogging && synced > 0)
                {
                    Debug.Log($"[Thunderbolt] KK rod sync: configured {synced} static rod(s).");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Thunderbolt] KK rod sync failed: {ex.Message}");
            }
        }

        private static Assembly FindKkAssembly()
        {
            foreach (AssemblyLoader.LoadedAssembly loaded in AssemblyLoader.loadedAssemblies)
            {
                Assembly asm = loaded?.assembly;
                if (asm == null)
                {
                    continue;
                }

                string name = asm.GetName().Name;
                if (string.Equals(name, "KerbalKonstructs", StringComparison.OrdinalIgnoreCase))
                {
                    return asm;
                }
            }

            return null;
        }

        private static bool TrySyncInstance(object staticInstance)
        {
            Type instType = staticInstance.GetType();
            bool isActive = GetBool(staticInstance, instType, "isActive", true);
            bool isSpawned = GetBool(staticInstance, instType, "isSpawned", true);
            if (!isActive && !isSpawned)
            {
                return false;
            }

            object model = GetMemberValue(staticInstance, instType, "model");
            if (model == null)
            {
                return false;
            }

            object modulesObj = GetMemberValue(model, model.GetType(), "modules");
            if (!(modulesObj is IEnumerable modules))
            {
                return false;
            }

            GameObject host = GetMemberValue(staticInstance, instType, "gameObject") as GameObject;
            GameObject mesh = GetMemberValue(staticInstance, instType, "mesh") as GameObject;
            GameObject attachRoot = mesh != null ? mesh : host;
            if (attachRoot == null)
            {
                return false;
            }

            string modelTitle = GetMemberValue(model, model.GetType(), "title") as string
                ?? GetMemberValue(model, model.GetType(), "name") as string
                ?? attachRoot.name;

            bool any = false;
            foreach (object module in modules)
            {
                if (module == null)
                {
                    continue;
                }

                Type modType = module.GetType();
                string className = GetMemberValue(module, modType, "moduleClassname") as string ?? string.Empty;

                bool isRod =
                    string.Equals(className, RodClassName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(className, "ModuleThunderboltRod", StringComparison.OrdinalIgnoreCase);

                if (!isRod)
                {
                    continue;
                }

                Dictionary<string, string> fields = GetModuleFields(module, modType);
                ModuleThunderboltRodStatic rod = attachRoot.GetComponent<ModuleThunderboltRodStatic>();
                if (rod == null)
                {
                    rod = attachRoot.AddComponent<ModuleThunderboltRodStatic>();
                }

                ApplyFields(rod, fields, modelTitle);
                if (!rod.enabled)
                {
                    rod.enabled = true;
                }

                any = true;
            }

            return any;
        }

        private static Dictionary<string, string> GetModuleFields(object module, Type modType)
        {
            object fieldsObj = GetMemberValue(module, modType, "moduleFields");
            if (fieldsObj is Dictionary<string, string> dict)
            {
                return dict;
            }

            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (fieldsObj is IDictionary idict)
            {
                foreach (DictionaryEntry entry in idict)
                {
                    if (entry.Key != null && entry.Value != null)
                    {
                        result[entry.Key.ToString()] = entry.Value.ToString();
                    }
                }
            }

            return result;
        }

        private static void ApplyFields(ModuleThunderboltRodStatic rod, Dictionary<string, string> fields, string fallbackName)
        {
            float radius = ParseFloat(fields, "attractRadius", rod.attractRadius);
            float chance = ParseFloat(fields, "divertChance", rod.divertChance);
            float priority = ParseFloat(fields, "attractPriority", rod.attractPriority);
            string tip = ParseString(fields, "tipTransform", rod.tipTransform);
            Vector3 offset = ParseVector3(fields, "tipOffset", rod.tipOffset);
            string name = ParseString(fields, "displayName", fallbackName);
            rod.ApplyConfig(radius, chance, priority, tip, offset, name);
        }

        private static string ParseString(Dictionary<string, string> fields, string key, string fallback)
        {
            if (fields != null && fields.TryGetValue(key, out string value) && !string.IsNullOrEmpty(value))
            {
                return value;
            }

            return fallback;
        }

        private static float ParseFloat(Dictionary<string, string> fields, string key, float fallback)
        {
            if (fields != null
                && fields.TryGetValue(key, out string value)
                && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
            {
                return parsed;
            }

            return fallback;
        }

        private static Vector3 ParseVector3(Dictionary<string, string> fields, string key, Vector3 fallback)
        {
            if (fields == null || !fields.TryGetValue(key, out string value) || string.IsNullOrEmpty(value))
            {
                return fallback;
            }

            string[] parts = value.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
            {
                return fallback;
            }

            if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
                && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)
                && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
            {
                return new Vector3(x, y, z);
            }

            return fallback;
        }

        private static bool GetBool(object obj, Type type, string name, bool fallback)
        {
            object value = GetMemberValue(obj, type, name);
            if (value is bool b)
            {
                return b;
            }

            return fallback;
        }

        private static object GetMemberValue(object obj, Type type, string name)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            FieldInfo field = type.GetField(name, flags);
            if (field != null)
            {
                return field.GetValue(obj);
            }

            PropertyInfo prop = type.GetProperty(name, flags);
            if (prop != null && prop.CanRead)
            {
                return prop.GetValue(obj, null);
            }

            return null;
        }
    }
}
