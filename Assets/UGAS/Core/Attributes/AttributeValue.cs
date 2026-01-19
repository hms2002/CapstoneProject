using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityGAS
{
    [Serializable]
    public class AttributeValue
    {
        public AttributeDefinition Definition { get; }
        public float BaseValue { get; private set; }
        public float CurrentValue { get; private set; }

        private readonly List<AttributeModifier> modifiers = new List<AttributeModifier>();
        private float lastDamageTime;

        public Action<float, float> OnValueChanged;
        private bool dirty;

        public void SetBaseValue(float value)
        {
            if (Math.Abs(BaseValue - value) < 0.0001f) return;
            BaseValue = value;
            dirty = true;
        }

        public void AddBaseValue(float delta)
        {
            if (Math.Abs(delta) < 0.0001f) return;
            BaseValue += delta;
            dirty = true;
        }

        public AttributeValue(AttributeDefinition definition)
        {
            Definition = definition;
            BaseValue = definition.defaultBaseValue;
            dirty = true;
            RecalculateValue(); // 초기 CurrentValue 세팅은 즉시 필요하니 한 번 호출하는 게 안전
        }


        public void AddModifier(AttributeModifier modifier)
        {
            modifiers.Add(modifier);
            dirty = true;
        }

        public void RemoveModifier(AttributeModifier modifier)
        {
            modifiers.Remove(modifier);
            dirty = true;
        }

        public void RemoveModifiersFromSource(UnityEngine.Object source)
        {
            if (modifiers.RemoveAll(mod => mod.Source == source) > 0)
                dirty = true;
        }


        public void Update(float deltaTime)
        {
            for (int i = modifiers.Count - 1; i >= 0; i--)
            {
                modifiers[i].Update(deltaTime);
                if (!modifiers[i].IsPermanent && modifiers[i].TimeRemaining <= 0)
                {
                    modifiers.RemoveAt(i);
                    dirty = true;
                }
            }

            if (Definition.hasRegeneration && CurrentValue < Definition.maxValue)
            {
                if (Time.time - lastDamageTime >= Definition.regenerationDelay)
                {
                    AddBaseValue(Definition.regenerationRate * deltaTime); // dirty = true 처리됨
                }
            }

            if (dirty)
            {
                dirty = false;
                RecalculateValue();
            }
        }


        private void RecalculateValue()
        {
            float oldValue = CurrentValue;
            float finalValue = BaseValue;

            var flatModifiers = modifiers.Where(m => m.Type == ModifierType.Flat).Sum(m => m.Value);
            var percentModifiers = modifiers.Where(m => m.Type == ModifierType.Percent).Sum(m => m.Value);

            finalValue += flatModifiers;
            finalValue *= (1f + percentModifiers);

            CurrentValue = Mathf.Clamp(finalValue, Definition.minValue, Definition.maxValue);

            if (Math.Abs(oldValue - CurrentValue) > 0.001f)
            {
                OnValueChanged?.Invoke(oldValue, CurrentValue);
                if (CurrentValue < oldValue)
                {
                    lastDamageTime = Time.time;
                }
            }
        }
    }
}