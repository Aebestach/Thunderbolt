using System.Collections.Generic;
using UnityEngine;

namespace Thunderbolt
{
    /// <summary>
    /// Procedural bolt paths anchored to vessel geometry.
    /// Flying: jagged sky → nose, (hidden) through stack, jagged tail → ground.
    /// Other situations: jagged sky → nose/hit only (no engine exit).
    /// </summary>
    internal static class ThunderboltPiercingPath
    {
        private const float MinBodySpan = 2f;
        private const float MinEntryLength = 180f;
        private const float MaxEntryLength = 8000f;
        private const float MaxGroundSegment = 120000f;

        internal static bool TryBuild(
            Vessel vessel,
            Vector3 cloudSample,
            bool exitThroughTail,
            out List<Vector3> points,
            out int hiddenBodySegment)
        {
            points = null;
            hiddenBodySegment = -1;
            if (vessel == null || vessel.isEVA || vessel.parts == null || vessel.parts.Count == 0)
            {
                return false;
            }

            if (!TryGetNoseAndTailParts(vessel, out Vector3 axis, out Part nosePart, out Part tailPart))
            {
                return false;
            }

            Vector3 nose = PartTip(nosePart, axis, towardPositiveAxis: true);
            Vector3 entry = BuildAxialEntryPoint(cloudSample, nose, axis);

            int seed = unchecked((int)vessel.persistentId)
                ^ (int)(Planetarium.GetUniversalTime() * 10.0);

            points = new List<Vector3>(20);
            AppendJaggedSpan(points, entry, nose, piecesPerSpan: 4, seed, spanIndex: 0, ampScale: 1.25f);

            bool pierce = exitThroughTail;
            Vector3 tail = default;
            if (pierce)
            {
                tail = PartTip(tailPart, axis, towardPositiveAxis: false);
                if (TryGetEngineExit(vessel, axis, out Vector3 engineExit))
                {
                    tail = engineExit;
                }

                if (Vector3.Distance(nose, tail) < MinBodySpan)
                {
                    pierce = false;
                }
            }

            if (pierce)
            {
                // Logical nose→tail edge is not drawn (stays inside the hull).
                hiddenBodySegment = points.Count - 1;
                AppendPoint(points, tail);
                Vector3 ground = BuildExitPoint(vessel, tail, -axis);
                AppendJaggedSpan(points, tail, ground, piecesPerSpan: 4, seed, spanIndex: 1, ampScale: 1.25f);

                ThunderboltSettings.Log(
                    $"Pierce anchors nose='{nosePart.partInfo?.title ?? nosePart.name}' " +
                    $"tail='{tailPart.partInfo?.title ?? tailPart.name}' points={points.Count}");
            }
            else
            {
                ThunderboltSettings.Log(
                    $"Strike anchor nose='{nosePart.partInfo?.title ?? nosePart.name}' points={points.Count}");
            }

            return points.Count >= 2;
        }

        /// <summary>
        /// Jagged bolt from cloud toward an explicit hit point (rods, EVA, fallbacks).
        /// </summary>
        internal static bool TryBuildToPoint(Vessel vessel, Vector3 cloudSample, Vector3 hitPoint, out List<Vector3> points)
        {
            points = null;
            if (hitPoint == Vector3.zero && (vessel == null || vessel.parts == null))
            {
                return false;
            }

            Vector3 axis = Vector3.up;
            if (vessel != null)
            {
                if (TryGetNoseAndTailParts(vessel, out Vector3 vesselAxis, out _, out _))
                {
                    axis = vesselAxis;
                }
                else if (vessel.ReferenceTransform != null)
                {
                    axis = vessel.ReferenceTransform.up.normalized;
                }
                else if (vessel.mainBody != null)
                {
                    axis = (hitPoint - vessel.mainBody.position).normalized;
                }
            }
            else if (cloudSample != hitPoint)
            {
                axis = (cloudSample - hitPoint).normalized;
            }

            Vector3 entry = BuildAxialEntryPoint(cloudSample, hitPoint, axis);
            int seed = vessel != null
                ? unchecked((int)vessel.persistentId) ^ (int)(Planetarium.GetUniversalTime() * 10.0)
                : (int)(Planetarium.GetUniversalTime() * 10.0);

            points = new List<Vector3>(12);
            AppendJaggedSpan(points, entry, hitPoint, piecesPerSpan: 4, seed, spanIndex: 0, ampScale: 1.25f);
            return points.Count >= 2;
        }

        private static Vector3 PartTip(Part part, Vector3 axis, bool towardPositiveAxis)
        {
            Vector3 pos = part.partTransform.position;
            float nudge = 0.6f;

            List<Renderer> renderers = part.FindModelComponents<Renderer>();
            if (renderers != null && renderers.Count > 0)
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Count; i++)
                {
                    if (renderers[i] != null)
                    {
                        b.Encapsulate(renderers[i].bounds);
                    }
                }

                // Centre of the extreme face along +/- axis (not a lateral corner).
                Vector3 e = b.extents;
                float projectedExtent =
                    Mathf.Abs(axis.x) * e.x
                    + Mathf.Abs(axis.y) * e.y
                    + Mathf.Abs(axis.z) * e.z;
                float sign = towardPositiveAxis ? 1f : -1f;
                return b.center + axis * (projectedExtent * sign);
            }

            return pos + axis * (towardPositiveAxis ? nudge : -nudge);
        }

        private static void AppendPoint(List<Vector3> points, Vector3 p)
        {
            if (points.Count == 0 || (points[points.Count - 1] - p).sqrMagnitude > 0.0001f)
            {
                points.Add(p);
            }
        }

        private static void AppendJaggedSpan(
            List<Vector3> points,
            Vector3 a,
            Vector3 b,
            int piecesPerSpan,
            int seed,
            int spanIndex,
            float ampScale)
        {
            piecesPerSpan = Mathf.Clamp(piecesPerSpan, 3, 5);
            AppendPoint(points, a);

            Vector3 delta = b - a;
            float len = delta.magnitude;
            if (len < 1f)
            {
                AppendPoint(points, b);
                return;
            }

            Vector3 dir = delta / len;
            Vector3 side = Vector3.Cross(dir, Vector3.up);
            if (side.sqrMagnitude < 1e-4f)
            {
                side = Vector3.Cross(dir, Vector3.right);
            }

            side.Normalize();
            Vector3 bil = Vector3.Cross(side, dir).normalized;
            float amp = Mathf.Clamp(len * 0.11f, 18f, 90f) * ampScale;

            for (int s = 1; s < piecesPerSpan; s++)
            {
                float t = s / (float)piecesPerSpan;
                Vector3 p = Vector3.Lerp(a, b, t);
                // Squared sine → strong mid bend, near-zero at endpoints so we hit the part.
                float envelope = Mathf.Sin(t * Mathf.PI);
                envelope *= envelope;
                float h1 = Hash01(seed, spanIndex, s, 1) * 2f - 1f;
                float h2 = Hash01(seed, spanIndex, s, 2) * 2f - 1f;
                p += side * (h1 * amp * envelope);
                p += bil * (h2 * amp * 0.55f * envelope);
                points.Add(p);
            }

            AppendPoint(points, b);
        }

        private static float Hash01(int seed, int a, int b, int c)
        {
            unchecked
            {
                int h = seed;
                h = (h * 397) ^ a;
                h = (h * 397) ^ b;
                h = (h * 397) ^ c;
                h ^= h << 13;
                h ^= h >> 17;
                h ^= h << 5;
                return (h & 0x7FFFFFFF) / (float)int.MaxValue;
            }
        }

        private static Vector3 BuildAxialEntryPoint(Vector3 cloudSample, Vector3 nose, Vector3 axis)
        {
            float along = Vector3.Dot(cloudSample - nose, axis);
            float sampleDist = Vector3.Distance(cloudSample, nose);
            float entryLen = along > 50f ? along : sampleDist;
            entryLen = Mathf.Clamp(entryLen, MinEntryLength, MaxEntryLength);
            return nose + axis * entryLen;
        }

        private static Vector3 BuildExitPoint(Vessel vessel, Vector3 tail, Vector3 outAxis)
        {
            outAxis.Normalize();
            if (TryGetGroundAlong(vessel, tail, outAxis, out Vector3 ground))
            {
                return ground;
            }

            float fallback = Mathf.Clamp(Vector3.Distance(vessel.CoM, tail) + 40f, 25f, 120f);
            return tail + outAxis * fallback;
        }

        private static bool TryGetNoseAndTailParts(
            Vessel vessel,
            out Vector3 axis,
            out Part nosePart,
            out Part tailPart)
        {
            axis = default;
            nosePart = null;
            tailPart = null;

            List<Part> parts = new List<Part>(vessel.parts.Count);
            for (int i = 0; i < vessel.parts.Count; i++)
            {
                Part part = vessel.parts[i];
                if (part != null && part.partTransform != null)
                {
                    parts.Add(part);
                }
            }

            if (parts.Count == 0)
            {
                return false;
            }

            float bestDistSq = -1f;
            Vector3 extremeA = parts[0].partTransform.position;
            Vector3 extremeB = extremeA;
            for (int i = 0; i < parts.Count; i++)
            {
                Vector3 pi = parts[i].partTransform.position;
                for (int j = i + 1; j < parts.Count; j++)
                {
                    Vector3 pj = parts[j].partTransform.position;
                    float d = (pi - pj).sqrMagnitude;
                    if (d > bestDistSq)
                    {
                        bestDistSq = d;
                        extremeA = pi;
                        extremeB = pj;
                    }
                }
            }

            if (bestDistSq < 0.25f && vessel.ReferenceTransform != null)
            {
                axis = vessel.ReferenceTransform.up.normalized;
            }
            else
            {
                axis = (extremeA - extremeB).normalized;
            }

            Vector3 origin = vessel.CoM;
            float maxDot = float.NegativeInfinity;
            float minDot = float.PositiveInfinity;
            for (int i = 0; i < parts.Count; i++)
            {
                float d = Vector3.Dot(parts[i].partTransform.position - origin, axis);
                if (d > maxDot)
                {
                    maxDot = d;
                    nosePart = parts[i];
                }

                if (d < minDot)
                {
                    minDot = d;
                    tailPart = parts[i];
                }
            }

            if (nosePart != null && EnginesCloserTo(vessel, nosePart.partTransform.position, tailPart.partTransform.position))
            {
                Part swap = nosePart;
                nosePart = tailPart;
                tailPart = swap;
                axis = -axis;
            }
            else if (vessel.mainBody != null && nosePart != null && tailPart != null)
            {
                Vector3 body = vessel.mainBody.position;
                if ((tailPart.partTransform.position - body).sqrMagnitude
                    > (nosePart.partTransform.position - body).sqrMagnitude)
                {
                    Part swap = nosePart;
                    nosePart = tailPart;
                    tailPart = swap;
                    axis = -axis;
                }
            }

            float forwardTipDot = float.NegativeInfinity;
            for (int i = 0; i < parts.Count; i++)
            {
                forwardTipDot = Mathf.Max(
                    forwardTipDot,
                    Vector3.Dot(parts[i].partTransform.position - origin, axis));
            }

            Part fairing = FindForwardFairing(vessel, axis, origin, forwardTipDot);
            if (fairing != null)
            {
                nosePart = fairing;
            }

            return nosePart != null && tailPart != null;
        }

        private static Part FindForwardFairing(Vessel vessel, Vector3 axis, Vector3 origin, float tipDot)
        {
            Part best = null;
            float bestDot = float.NegativeInfinity;
            for (int i = 0; i < vessel.parts.Count; i++)
            {
                Part part = vessel.parts[i];
                if (part?.partTransform == null || !IsFairingOrNoseCone(part))
                {
                    continue;
                }

                float d = Vector3.Dot(part.partTransform.position - origin, axis);
                if (d > tipDot - 5f && d > bestDot)
                {
                    bestDot = d;
                    best = part;
                }
            }

            return best;
        }

        private static bool IsFairingOrNoseCone(Part part)
        {
            if (part.FindModuleImplementing<ModuleProceduralFairing>() != null)
            {
                return true;
            }

            string name = part.name ?? string.Empty;
            string title = part.partInfo != null ? part.partInfo.title ?? string.Empty : string.Empty;
            return Contains(name, "fairing") || Contains(title, "fairing")
                || Contains(name, "nose") || Contains(title, "nose")
                || Contains(name, "整流罩") || Contains(title, "整流罩")
                || Contains(name, "锥") || Contains(title, "锥");
        }

        private static bool Contains(string s, string token)
        {
            return s.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool EnginesCloserTo(Vessel vessel, Vector3 a, Vector3 b)
        {
            float bestA = float.MaxValue;
            float bestB = float.MaxValue;
            bool any = false;

            for (int i = 0; i < vessel.parts.Count; i++)
            {
                Part part = vessel.parts[i];
                List<ModuleEngines> engines = part?.FindModulesImplementing<ModuleEngines>();
                if (engines == null || engines.Count == 0)
                {
                    continue;
                }

                for (int e = 0; e < engines.Count; e++)
                {
                    ModuleEngines engine = engines[e];
                    if (engine?.thrustTransforms == null)
                    {
                        continue;
                    }

                    for (int t = 0; t < engine.thrustTransforms.Count; t++)
                    {
                        if (engine.thrustTransforms[t] == null)
                        {
                            continue;
                        }

                        Vector3 p = engine.thrustTransforms[t].position;
                        bestA = Mathf.Min(bestA, (p - a).sqrMagnitude);
                        bestB = Mathf.Min(bestB, (p - b).sqrMagnitude);
                        any = true;
                    }
                }
            }

            return any && bestA < bestB;
        }

        private static bool TryGetEngineExit(Vessel vessel, Vector3 axis, out Vector3 exit)
        {
            exit = default;
            Vector3 origin = vessel.CoM;
            List<Transform> exits = new List<Transform>();

            for (int i = 0; i < vessel.parts.Count; i++)
            {
                Part part = vessel.parts[i];
                List<ModuleEngines> engines = part?.FindModulesImplementing<ModuleEngines>();
                if (engines == null)
                {
                    continue;
                }

                for (int e = 0; e < engines.Count; e++)
                {
                    ModuleEngines engine = engines[e];
                    if (engine?.thrustTransforms == null)
                    {
                        continue;
                    }

                    for (int t = 0; t < engine.thrustTransforms.Count; t++)
                    {
                        Transform tt = engine.thrustTransforms[t];
                        if (tt == null)
                        {
                            continue;
                        }

                        exits.Add(tt);
                    }
                }
            }

            if (exits.Count == 0)
            {
                return false;
            }

            float aftMost = float.PositiveInfinity;
            for (int i = 0; i < exits.Count; i++)
            {
                aftMost = Mathf.Min(aftMost, Vector3.Dot(exits[i].position - origin, axis));
            }

            const float aftTolerance = 5f;
            float bestRadialSq = float.PositiveInfinity;
            float bestAxial = float.PositiveInfinity;
            bool found = false;
            for (int i = 0; i < exits.Count; i++)
            {
                Vector3 p = exits[i].position;
                float axial = Vector3.Dot(p - origin, axis);
                if (axial > aftMost + aftTolerance)
                {
                    continue;
                }

                Vector3 onAxis = origin + axis * axial;
                float radialSq = (p - onAxis).sqrMagnitude;
                if (!found
                    || radialSq < bestRadialSq - 0.01f
                    || (Mathf.Abs(radialSq - bestRadialSq) <= 0.01f && axial < bestAxial))
                {
                    bestRadialSq = radialSq;
                    bestAxial = axial;
                    exit = p;
                    found = true;
                }
            }

            return found;
        }

        private static bool TryGetGroundAlong(Vessel vessel, Vector3 from, Vector3 dir, out Vector3 ground)
        {
            ground = default;
            CelestialBody body = vessel.mainBody;
            if (body == null)
            {
                return false;
            }

            try
            {
                dir.Normalize();
                float guess = 500f;
                if (vessel.radarAltitude > 1.0 && vessel.radarAltitude < MaxGroundSegment)
                {
                    guess = (float)vessel.radarAltitude + 50f;
                }
                else if (vessel.altitude > 1.0)
                {
                    guess = Mathf.Clamp((float)vessel.altitude, 100f, MaxGroundSegment);
                }

                Vector3 probe = from + dir * guess;
                double lat = body.GetLatitude(probe);
                double lon = body.GetLongitude(probe);
                ground = body.GetWorldSurfacePosition(lat, lon, body.TerrainAltitude(lat, lon));

                Vector3d bodyPos = body.position;
                if (((Vector3d)ground - bodyPos).magnitude >= ((Vector3d)from - bodyPos).magnitude - 1.0)
                {
                    Vector3d up = ((Vector3d)from - bodyPos).normalized;
                    ground = bodyPos + up * (body.Radius + body.TerrainAltitude(
                        body.GetLatitude(from),
                        body.GetLongitude(from)));
                    if (((Vector3d)ground - bodyPos).magnitude >= ((Vector3d)from - bodyPos).magnitude - 1.0)
                    {
                        return false;
                    }
                }

                return true;
            }
            catch (System.Exception ex)
            {
                ThunderboltSettings.LogWarning("Piercing ground sample failed: " + ex.Message);
                return false;
            }
        }
    }
}
