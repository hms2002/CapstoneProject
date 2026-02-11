using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// Reads stats from an <see cref="AttributeSet"/> using <see cref="StatTypeBindings"/>.
    ///
    /// Supports composite final stats:
    ///   Final = (Base + Add) * Mul
    /// where Mul is stored as x1 factor (default 1.0 if missing).
    /// </summary>
    public sealed class AttributeStatProvider : IStatProvider
    {
        private readonly AttributeSet _set;
        private readonly StatTypeBindings _bindings;

        public AttributeStatProvider(AttributeSet set, StatTypeBindings bindings)
        {
            _set = set;
            _bindings = bindings;
        }

        public float Get(StatId id)
        {
            if (_set == null || _bindings == null) return 0f;
            if (id == StatId.None) return 0f;

            // Composite final
            if (_bindings.TryGetComposite(id, out var c) && c != null)
            {
                float b = GetBoundOrDefault(c.baseId, defaultValue: 0f);
                float a = GetBoundOrDefault(c.addId, defaultValue: 0f);
                float m = GetBoundOrDefault(c.mulId, defaultValue: 1f, treatAsMultiplier: true);
                return (b + a) * m;
            }

            // Raw bound
            return GetBoundOrDefault(id, defaultValue: 0f);
        }

        private float GetBoundOrDefault(StatId id, float defaultValue, bool treatAsMultiplier = false)
        {
            if (id == StatId.None) return defaultValue;

            if (_bindings.TryGetBinding(id, out var b) && b != null)
            {
                if (b.attribute == null) return defaultValue;
                float v = _set.GetAttributeValue(b.attribute);

                if (b.isMultiplier || treatAsMultiplier)
                    return v != 0f ? Mathf.Max(0f, v) : 1f;

                return v;
            }

            return defaultValue;
        }
    }
}
