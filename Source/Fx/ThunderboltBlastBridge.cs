using System;
using System.Reflection;
using UnityEngine;

namespace Thunderbolt
{
    /// <summary>
    /// Soft dependency on BlastFX via reflection. Thunderbolt still runs without it;
    /// fireball cover-destroy requires BlastFX + BlastFX.bundle.
    /// </summary>
    internal static class ThunderboltBlastBridge
    {
        private static bool resolved;
        private static MethodInfo spawnAtPoint;

        public static bool Available
        {
            get
            {
                Ensure();
                return spawnAtPoint != null;
            }
        }

        /// <summary>
        /// Spawn BlastFX fireball at the strike point; optionally destroy the part mid-burst.
        /// </summary>
        public static void SpawnAtPoint(Vector3 worldPos, Part part, bool destroy, Color? plasma = null)
        {
            Ensure();
            if (spawnAtPoint != null)
            {
                try
                {
                    ParameterInfo[] ps = spawnAtPoint.GetParameters();
                    object[] args = new object[ps.Length];
                    args[0] = worldPos;
                    args[1] = part;
                    args[2] = destroy;
                    for (int i = 3; i < ps.Length; i++)
                    {
                        if (ps[i].ParameterType == typeof(float))
                        {
                            args[i] = -1f;
                        }
                        else if (ps[i].ParameterType == typeof(Color?))
                        {
                            args[i] = plasma;
                        }
                        else
                        {
                            args[i] = ps[i].HasDefaultValue ? ps[i].DefaultValue : null;
                        }
                    }

                    spawnAtPoint.Invoke(null, args);
                    return;
                }
                catch (Exception ex)
                {
                    ThunderboltSettings.LogWarning("BlastFX.SpawnAtPoint failed: " + ex.Message);
                }
            }

            Fallback(worldPos, part, destroy);
        }

        private static void Ensure()
        {
            if (resolved)
            {
                return;
            }

            resolved = true;
            try
            {
                AssemblyLoader.LoadedAssembly found = null;
                for (int i = 0; i < AssemblyLoader.loadedAssemblies.Count; i++)
                {
                    AssemblyLoader.LoadedAssembly la = AssemblyLoader.loadedAssemblies[i];
                    if (la?.assembly == null)
                    {
                        continue;
                    }

                    if (string.Equals(la.assembly.GetName().Name, "BlastFX", StringComparison.OrdinalIgnoreCase))
                    {
                        found = la;
                        break;
                    }
                }

                if (found == null)
                {
                    ThunderboltSettings.Log(
                        "BlastFX not installed — lightning still works; fireball destroy FX disabled.");
                    return;
                }

                Type blastType = found.assembly.GetType("BlastFX.Blast");
                if (blastType == null)
                {
                    ThunderboltSettings.LogWarning("BlastFX.dll loaded but BlastFX.Blast type missing.");
                    return;
                }

                MethodInfo[] methods = blastType.GetMethods(BindingFlags.Public | BindingFlags.Static);
                for (int m = 0; m < methods.Length; m++)
                {
                    if (methods[m].Name != "SpawnAtPoint")
                    {
                        continue;
                    }

                    ParameterInfo[] ps = methods[m].GetParameters();
                    if (ps.Length >= 3
                        && ps[0].ParameterType == typeof(Vector3)
                        && ps[1].ParameterType == typeof(Part)
                        && ps[2].ParameterType == typeof(bool))
                    {
                        spawnAtPoint = methods[m];
                        break;
                    }
                }

                if (spawnAtPoint == null)
                {
                    ThunderboltSettings.LogWarning("BlastFX.Blast.SpawnAtPoint signature not found.");
                }
                else
                {
                    ThunderboltSettings.Log("BlastFX bridge ready.");
                }
            }
            catch (Exception ex)
            {
                ThunderboltSettings.LogWarning("BlastFX bridge resolve failed: " + ex.Message);
            }
        }

        private static void Fallback(Vector3 worldPos, Part part, bool destroy)
        {
            ThunderboltSettings.LogWarning(
                "BlastFX unavailable at " + worldPos + " — using silent stock destroy fallback.");

            if (!destroy || part == null || part.State == PartStates.DEAD)
            {
                return;
            }

            float saved = part.explosionPotential;
            part.explosionPotential = 0f;
            try
            {
                part.explode();
            }
            finally
            {
                try
                {
                    if (part != null)
                    {
                        part.explosionPotential = saved;
                    }
                }
                catch
                {
                    // ignored
                }
            }
        }
    }
}
