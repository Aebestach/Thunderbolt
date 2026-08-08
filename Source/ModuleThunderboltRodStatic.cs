using UnityEngine;

namespace Thunderbolt
{
    /// <summary>
    /// Lightning rod for Kerbal Konstructs statics (and any other GameObject host).
    /// KK STATIC example:
    /// <code>
    /// MODULE
    /// {
    ///     namespace = Thunderbolt
    ///     name = ModuleThunderboltRodStatic
    ///     attractRadius = 800
    ///     divertChance = 0.95
    ///     attractPriority = 3
    /// }
    /// </code>
    /// Does not require a compile-time KK reference: KK AddComponent-s this MonoBehaviour;
    /// <see cref="ThunderboltKkRodSupport"/> copies CFG fields when KK's StaticModule cast fails.
    /// </summary>
    public class ModuleThunderboltRodStatic : MonoBehaviour, IThunderboltRod
    {
        public float attractRadius = 800f;
        public float divertChance = 0.95f;
        public float attractPriority = 2f;
        public string tipTransform = string.Empty;
        public Vector3 tipOffset = Vector3.zero;
        public string displayName = "Lightning Tower";

        private Transform tip;
        private bool registered;

        public bool IsRodActive => enabled && gameObject != null && gameObject.activeInHierarchy;
        public float AttractRadius => attractRadius;
        public float DivertChance => divertChance;
        public float AttractPriority => attractPriority;
        public Part HostPart => null;
        public Vector3 WorldPosition => transform != null ? transform.position : Vector3.zero;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? gameObject.name : displayName;

        public Vector3 StrikePoint
        {
            get
            {
                Transform t = tip != null ? tip : transform;
                if (t == null)
                {
                    return Vector3.zero;
                }

                if (tipOffset != Vector3.zero)
                {
                    return t.position + t.TransformDirection(tipOffset);
                }

                // Default: tip near the top of renderers, else a modest upward bias.
                Renderer[] renderers = GetComponentsInChildren<Renderer>();
                if (renderers != null && renderers.Length > 0)
                {
                    Bounds b = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++)
                    {
                        if (renderers[i] != null)
                        {
                            b.Encapsulate(renderers[i].bounds);
                        }
                    }

                    Vector3 up = FlightGlobals.currentMainBody != null
                        ? (t.position - FlightGlobals.currentMainBody.position).normalized
                        : Vector3.up;
                    return b.center + up * (b.extents.magnitude * 0.5f);
                }

                Vector3 fallbackUp = FlightGlobals.currentMainBody != null
                    ? (t.position - FlightGlobals.currentMainBody.position).normalized
                    : Vector3.up;
                return t.position + fallbackUp * 15f;
            }
        }

        private void Awake()
        {
            ResolveTip();
        }

        private void OnEnable()
        {
            ResolveTip();
            if (!registered)
            {
                ThunderboltRodRegistry.Register(this);
                registered = true;
            }
        }

        private void OnDisable()
        {
            if (registered)
            {
                ThunderboltRodRegistry.Unregister(this);
                registered = false;
            }
        }

        private void OnDestroy()
        {
            if (registered)
            {
                ThunderboltRodRegistry.Unregister(this);
                registered = false;
            }
        }

        public void ApplyConfig(
            float radius,
            float chance,
            float priority,
            string tipName,
            Vector3 offset,
            string name)
        {
            attractRadius = radius;
            divertChance = chance;
            attractPriority = priority;
            tipTransform = tipName ?? string.Empty;
            tipOffset = offset;
            if (!string.IsNullOrEmpty(name))
            {
                displayName = name;
            }

            ResolveTip();
        }

        private void ResolveTip()
        {
            tip = null;
            if (string.IsNullOrEmpty(tipTransform))
            {
                return;
            }

            tip = FindChildTransform(transform, tipTransform);
            if (tip == null)
            {
                Debug.LogWarning($"[Thunderbolt] ModuleThunderboltRodStatic: tipTransform '{tipTransform}' not found on {gameObject.name}.");
            }
        }

        private static Transform FindChildTransform(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            if (root.name == name)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChildTransform(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        public bool TryAbsorbStrike(bool applyDamage)
        {
            // Static towers are not destroyed by default (no vessel part to explode).
            return false;
        }
    }
}
