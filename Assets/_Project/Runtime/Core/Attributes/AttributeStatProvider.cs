using UnityEngine;

namespace UnityGAS
{
    //  AttributeStatProvider = (AttributeSet + StatTypeBindings)를 묶어서 StatId로 값을 읽어오는 어댑터((Base + Add) * Mul 형태의 final stat 계산을 지원한다)
    //  IStatProvider는 공식/데이터가 “어떤 AttributeDef를 쓰는지”와 분리되도록 해주는 인터페이스
    /// <summary>
    /// 책임: AttributeSet과 StatTypeBindings를 조합해 StatId 기반 최종 스탯 값을 계산하는 Core stat provider이다.
    ///
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
        private readonly System.Func<AttributeDefinition, float> _readAttribute;

        public AttributeStatProvider(AttributeSet set, StatTypeBindings bindings)
        {
            _set = set;
            _bindings = bindings;
            _readAttribute = set != null ? set.GetAttributeValue : (System.Func<AttributeDefinition, float>)null;
        }

        public float Get(StatId id)
            => _set != null ? Get(id, _readAttribute) : 0f;

        public float Get(StatId id, System.Func<AttributeDefinition, float> readAttribute)
        {
            if (readAttribute == null || _bindings == null) return 0f;
            if (id == StatId.None) return 0f;

            // Composite final
            if (_bindings.TryGetComposite(id, out var c) && c != null)
            {
                float b = GetBoundOrDefault(c.baseId, readAttribute, defaultValue: 0f);
                float a = GetBoundOrDefault(c.addId, readAttribute, defaultValue: 0f);
                float m = GetBoundOrDefault(c.mulId, readAttribute, defaultValue: 1f, treatAsMultiplier: true);
                return (b + a) * m;
            }

            // Raw bound
            return GetBoundOrDefault(id, readAttribute, defaultValue: 0f);
        }

        private float GetBoundOrDefault(StatId id, System.Func<AttributeDefinition, float> readAttribute, float defaultValue, bool treatAsMultiplier = false)
        {
            if (id == StatId.None) return defaultValue;

            if (_bindings.TryGetBinding(id, out var b) && b != null)
            {
                if (b.attribute == null) return defaultValue;
                float v = readAttribute(b.attribute);

                if (b.isMultiplier || treatAsMultiplier)
                    return v != 0f ? Mathf.Max(0f, v) : 1f;

                return v;
            }

            return defaultValue;
        }
    }
}
