using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
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
        /// Ensures this AttributeSet has created its runtime AttributeValue instances and MaxLinks.
        /// This allows safe Get/Read calls even if invoked before Unity's Awake order.
        /// </summary>
        private void EnsureInitialized()
        {
            if (_initialized) return;
            // 1) 생성
            foreach (var attributeDef in initialAttributes)
            {
                if (attributeDef == null) continue;

                var av = new AttributeValue(attributeDef);
                attributes[attributeDef] = av;

                // 이벤트 연결
                var capturedDef = attributeDef;
                av.OnValueChanged += (oldVal, newVal) => OnAttributeChanged?.Invoke(capturedDef, oldVal, newVal);
                _initialized = true;
            }


            // 2) value-max 링크 구성 (Health <- MaxHealth)
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

                // value의 max를 maxAttr.CurrentValue로
                valueAttr.SetMaxValueGetter(() => maxAttr.CurrentValue);

                // max가 바뀌면 value를 즉시 clamp
                var capturedValue = valueAttr;
                maxAttr.OnValueChanged += (_, __) =>
                {
                    capturedValue.MarkDirty();
                    capturedValue.ForceRecalculate(); // 그 프레임에 즉시 clamp되게
                };

                // 초기에도 한 번 clamp
                valueAttr.MarkDirty();
                valueAttr.ForceRecalculate();
            }
        }

        public AttributeValue GetAttribute(AttributeDefinition definition)
        {
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

        public void ModifyAttributeValue(AttributeDefinition definition, float amount, UnityEngine.Object source)
        {
            // Damage/heal often needs to be visible immediately (same frame)
            // for kill checks, UI, hit reactions, etc.
            // AttributeValue uses a 'dirty' flag and normally recalculates in Update(),
            // so we force a recalc here.
            EnsureInitialized();
            var attr = GetAttribute(definition);
            if (attr != null)
            {
                attr.AddBaseValue(amount);
                attr.ForceRecalculate();
            }
        }

        public void AddModifier(AttributeDefinition definition, AttributeModifier modifier)
        {
            EnsureInitialized();
            var attr = GetAttribute(definition);
            if (attr != null)
            {
                attr.AddModifier(modifier);
                // Modifiers are also frequently queried immediately after application.
                attr.ForceRecalculate();
            }
        }

        public void RemoveModifiersFromSource(UnityEngine.Object source)
        {
            foreach (var attribute in attributes.Values)
                attribute.RemoveModifiersFromSource(source);
        }
    }
}
