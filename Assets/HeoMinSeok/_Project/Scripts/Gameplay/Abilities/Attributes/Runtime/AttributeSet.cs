using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    // “한 게임 오브젝트의 AttributeValue들을 생성/보관하고,
    // (델타/모디파이어/클램프/틱/이벤트) 규칙을 한 곳에서 일관되게 적용하는 ‘스탯 관문’이다.
    // 외부 코드는 Attribute를 변경하려면 이 클래스를 통해야 한다.”
    public class AttributeSet : MonoBehaviour
    {
        [Serializable]
        public struct MaxLink
        {
            public AttributeDefinition value; // 예: Health
            public AttributeDefinition max;   // 예: MaxHealth
        }

        [SerializeField] private List<AttributeDefinition> initialAttributes = new List<AttributeDefinition>();

        [Header("Optional: Dynamic Max Links (value is clamped by max.CurrentValue)")]
        [SerializeField] private List<MaxLink> maxLinks = new List<MaxLink>();

        private readonly Dictionary<AttributeDefinition, AttributeValue> attributes = new Dictionary<AttributeDefinition, AttributeValue>();
        private bool _initialized;

        public delegate void AttributeChangedDelegate(AttributeDefinition attribute, float oldValue, float newValue);
        public event AttributeChangedDelegate OnAttributeChanged;

        private void Awake()
        {
            EnsureInitialized();
        }

        /// <summary>
        /// 런타임 AttributeValue 생성 및 MaxLink 연결 보장.
        /// initialAttributes가 비어 있어도 초기화는 완료된 것으로 본다.
        /// </summary>
        private void EnsureInitialized()
        {
            if (_initialized) return;

            // 1) 생성
            foreach (var attributeDef in initialAttributes)
            {
                if (attributeDef == null) continue;

                if (attributes.ContainsKey(attributeDef))
                    continue;

                var av = new AttributeValue(attributeDef);
                attributes[attributeDef] = av;

                var capturedDef = attributeDef;
                av.OnValueChanged += (oldVal, newVal) => OnAttributeChanged?.Invoke(capturedDef, oldVal, newVal);
            }

            // initialAttributes가 비어 있어도 초기화 완료로 본다.
            _initialized = true;

            // 2) value-max 링크 구성
            SetupMaxLinks();
        }

        private void Update()
        {
            if (!_initialized) return;
            if (attributes.Count == 0) return;

            float dt = Time.deltaTime;
            foreach (var attributeValue in attributes.Values)
                attributeValue.Update(dt);
        }

        private void SetupMaxLinks()
        {
            if (maxLinks == null || maxLinks.Count == 0) return;

            for (int i = 0; i < maxLinks.Count; i++)
            {
                var link = maxLinks[i];
                if (link.value == null || link.max == null) continue;

                var valueAttr = GetAttribute(link.value);
                var maxAttr = GetAttribute(link.max);

                if (valueAttr == null || maxAttr == null) continue;

                valueAttr.SetMaxValueGetter(() => maxAttr.CurrentValue);

                var capturedValue = valueAttr;
                maxAttr.OnValueChanged += (_, __) =>
                {
                    capturedValue.MarkDirty();
                    capturedValue.ForceRecalculate();
                };

                valueAttr.MarkDirty();
                valueAttr.ForceRecalculate();
            }
        }

        public AttributeValue GetAttribute(AttributeDefinition definition)
        {
            EnsureInitialized();
            if (definition == null) return null;
            return attributes.TryGetValue(definition, out var v) ? v : null;
        }

        public IReadOnlyAttributeValue GetReadOnly(AttributeDefinition definition)
        {
            EnsureInitialized();
            return GetAttribute(definition);
        }

        public bool TryGetReadOnly(AttributeDefinition definition, out IReadOnlyAttributeValue value)
        {
            EnsureInitialized();
            value = null;
            if (definition == null) return false;

            if (attributes.TryGetValue(definition, out var v))
            {
                value = v;
                return true;
            }
            return false;
        }

        public float GetAttributeValue(AttributeDefinition definition)
        {
            EnsureInitialized();
            return GetAttribute(definition)?.CurrentValue ?? 0f;
        }

        public bool TrySetBaseValue(AttributeDefinition definition, float newValue, UnityEngine.Object source)
        {
            EnsureInitialized();

            if (definition == null)
                return false;

            var attr = GetAttribute(definition);
            if (attr == null)
                return false;

            attr.SetBaseValue(newValue);
            attr.ForceRecalculate();
            return true;
        }

        public bool TryModifyAttributeValue(AttributeDefinition definition, float amount, UnityEngine.Object source)
        {
            EnsureInitialized();

            if (definition == null)
                return false;

            var attr = GetAttribute(definition);
            if (attr == null)
                return false;

            attr.AddBaseValue(amount);
            attr.ForceRecalculate();
            return true;
        }
        


        public void RemoveModifiersFromSource(UnityEngine.Object source)
        {
            EnsureInitialized();

            foreach (var attribute in attributes.Values)
            {
                attribute.RemoveModifiersFromSource(source);
                attribute.ForceRecalculate();
            }
        }
        public bool TryAddModifier(AttributeDefinition definition, AttributeModifier modifier)
        {
            EnsureInitialized();

            if (definition == null || modifier == null)
                return false;

            if (definition.IsBaseOnly())
            {
                Debug.LogWarning($"[AttributeSet] '{definition.attributeName}' 은(는) BaseOnly 속성이므로 Modifier를 적용할 수 없습니다.");
                return false;
            }

            var attr = GetAttribute(definition);
            if (attr == null)
                return false;

            attr.AddModifier(modifier);
            attr.ForceRecalculate();
            return true;
        }

        public bool TryRemoveModifier(AttributeDefinition definition, AttributeModifier modifier)
        {
            EnsureInitialized();

            if (definition == null || modifier == null)
                return false;

            var attr = GetAttribute(definition);
            if (attr == null)
                return false;

            attr.RemoveModifier(modifier);
            attr.ForceRecalculate();
            return true;
        }
    }
}