using System.Collections.Generic;
using UnityEngine;

namespace Thunderbolt
{
    /// <summary>
    /// Shared registry for part-mounted and KK/static lightning rods.
    /// </summary>
    public static class ThunderboltRodRegistry
    {
        private static readonly List<IThunderboltRod> Rods = new List<IThunderboltRod>(32);

        public static void Register(IThunderboltRod rod)
        {
            if (rod == null || Rods.Contains(rod))
            {
                return;
            }

            Rods.Add(rod);
        }

        public static void Unregister(IThunderboltRod rod)
        {
            if (rod == null)
            {
                return;
            }

            Rods.Remove(rod);
        }

        /// <summary>
        /// Picks a nearby rod that successfully diverts the strike away from the vessel.
        /// Higher priority / closer / taller rods are preferred. Not absolute immunity.
        /// </summary>
        public static bool TryDivert(Vessel vessel, out IThunderboltRod rod)
        {
            rod = null;
            if (vessel == null || Rods.Count == 0)
            {
                return false;
            }

            Vector3 vesselPos = vessel.GetWorldPos3D();
            Vector3 bodyPos = vessel.mainBody != null ? vessel.mainBody.position : Vector3.zero;
            Vector3 up = bodyPos != Vector3.zero ? (vesselPos - bodyPos).normalized : Vector3.up;

            IThunderboltRod best = null;
            float bestScore = float.NegativeInfinity;

            for (int i = Rods.Count - 1; i >= 0; i--)
            {
                IThunderboltRod candidate = Rods[i];
                if (candidate == null || !candidate.IsRodActive)
                {
                    if (candidate == null)
                    {
                        Rods.RemoveAt(i);
                    }

                    continue;
                }

                Vector3 rodPos = candidate.WorldPosition;
                float range = Mathf.Max(1f, candidate.AttractRadius);
                float dist = Vector3.Distance(vesselPos, rodPos);
                if (dist > range)
                {
                    continue;
                }

                float proximity = 1f - (dist / range);
                float heightBonus = Vector3.Dot(rodPos - vesselPos, up);
                float score = (candidate.AttractPriority * 10f) + (proximity * 6f) + (heightBonus * 0.002f);

                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            if (best == null)
            {
                return false;
            }

            float chance = Mathf.Clamp01(best.DivertChance);
            if (chance < 1f && Random.value > chance)
            {
                ThunderboltSettings.Log($"Rod divert failed for {best.DisplayName} (p={chance:F2}).");
                return false;
            }

            rod = best;
            return true;
        }
    }

    public interface IThunderboltRod
    {
        bool IsRodActive { get; }
        float AttractRadius { get; }
        float DivertChance { get; }
        float AttractPriority { get; }
        Vector3 WorldPosition { get; }
        Vector3 StrikePoint { get; }
        string DisplayName { get; }
        Part HostPart { get; }

        /// <summary>
        /// Optional damage when the rod absorbs a strike. Returns true if destroyed.
        /// </summary>
        bool TryAbsorbStrike(bool applyDamage);
    }
}
