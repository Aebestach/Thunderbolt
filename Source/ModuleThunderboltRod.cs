using UnityEngine;

namespace Thunderbolt
{
    /// <summary>
    /// Part-module lightning rod. Add to any PART cfg:
    /// <code>
    /// MODULE
    /// {
    ///     name = ModuleThunderboltRod
    ///     attractRadius = 400
    ///     divertChance = 0.92
    ///     attractPriority = 2
    /// }
    /// </code>
    /// </summary>
    public class ModuleThunderboltRod : PartModule, IThunderboltRod
    {
        [KSPField]
        public float attractRadius = 400f;

        /// <summary>Chance to divert a nearby strike onto this rod (not absolute).</summary>
        [KSPField]
        public float divertChance = 0.92f;

        /// <summary>Higher values win when multiple rods compete.</summary>
        [KSPField]
        public float attractPriority = 1f;

        /// <summary>Optional child transform name used as the bolt tip.</summary>
        [KSPField]
        public string tipTransform = string.Empty;

        /// <summary>World-space offset from part origin / tip transform (x,y,z).</summary>
        [KSPField]
        public Vector3 tipOffset = Vector3.zero;

        [KSPField]
        public bool canBeDestroyed = false;

        [KSPField]
        public float destroyChance = 0.05f;

        private Transform tip;

        public bool IsRodActive =>
            enabled
            && isEnabled
            && part != null
            && part.State != PartStates.DEAD
            && vessel != null
            && vessel.loaded
            && !vessel.packed;

        public float AttractRadius => attractRadius;
        public float DivertChance => divertChance;
        public float AttractPriority => attractPriority;
        public Part HostPart => part;

        public Vector3 WorldPosition =>
            part != null && part.partTransform != null ? part.partTransform.position : Vector3.zero;

        public string DisplayName
        {
            get
            {
                if (part?.partInfo != null && !string.IsNullOrEmpty(part.partInfo.title))
                {
                    return part.partInfo.title;
                }

                return part != null ? part.name : "Thunderbolt Rod";
            }
        }

        public Vector3 StrikePoint
        {
            get
            {
                Transform t = tip != null ? tip : part != null ? part.partTransform : null;
                if (t == null)
                {
                    return Vector3.zero;
                }

                Vector3 point = t.position;
                if (tipOffset != Vector3.zero)
                {
                    point += t.TransformDirection(tipOffset);
                }
                else if (tip == null && part != null && vessel != null && vessel.mainBody != null)
                {
                    // Bias toward the top of the part when no tip is configured.
                    Vector3 up = (t.position - vessel.mainBody.position).normalized;
                    var renderers = part.FindModelComponents<Renderer>();
                    if (renderers != null && renderers.Count > 0)
                    {
                        Bounds bounds = renderers[0].bounds;
                        for (int i = 1; i < renderers.Count; i++)
                        {
                            if (renderers[i] != null)
                            {
                                bounds.Encapsulate(renderers[i].bounds);
                            }
                        }

                        point = bounds.center + up * Mathf.Max(bounds.extents.y, 0.5f);
                    }
                    else
                    {
                        point = t.position + up * 2f;
                    }
                }

                return point;
            }
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            ResolveTip();
            ThunderboltRodRegistry.Register(this);
        }

        public override void OnStartFinished(StartState state)
        {
            base.OnStartFinished(state);
            ResolveTip();
        }

        private void OnDestroy()
        {
            ThunderboltRodRegistry.Unregister(this);
        }

        private void ResolveTip()
        {
            tip = null;
            if (string.IsNullOrEmpty(tipTransform) || part == null || part.partTransform == null)
            {
                return;
            }

            tip = part.FindModelTransform(tipTransform);
            if (tip == null)
            {
                Debug.LogWarning($"[Thunderbolt] ModuleThunderboltRod: tipTransform '{tipTransform}' not found on {part.name}.");
            }
        }

        public bool TryAbsorbStrike(bool applyDamage)
        {
            if (!applyDamage || !canBeDestroyed || part == null || part.State == PartStates.DEAD)
            {
                return false;
            }

            float chance = Mathf.Clamp01(destroyChance);
            if (chance <= 0f || Random.value > chance)
            {
                return false;
            }

            Debug.Log($"[Thunderbolt] Lightning rod absorbed strike and was destroyed: {DisplayName}");
            part.explode();
            return true;
        }
    }
}
