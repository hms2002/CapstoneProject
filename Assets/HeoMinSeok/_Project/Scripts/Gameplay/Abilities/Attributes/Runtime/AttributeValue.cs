using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityGAS
{
    [Serializable]
    public class AttributeValue : IReadOnlyAttributeValue
    {
        public AttributeDefinition Definition { get; }
        public float BaseValue { get; private set; }
        public float CurrentValue { get; private set; }

        // ✅ 동적 Max (예: Health의 max를 MaxHealth.CurrentValue로)
        public Func<float> MaxValueGetter { get; private set; }

        private readonly List<AttributeModifier> modifiers = new List<AttributeModifier>();
        private float lastDamageTime;

        public Action<float, float> OnValueChanged;
        private bool dirty;

        public AttributeValue(AttributeDefinition definition)
        {
            Definition = definition;
            BaseValue = definition.defaultBaseValue;
            dirty = true;
            RecalculateValue(); // 초기 CurrentValue 세팅
        }

        public void SetMaxValueGetter(Func<float> getter)
        {
            MaxValueGetter = getter;
            dirty = true;
        }

        public void MarkDirty()
        {
            dirty = true;
        }

        // ✅ 즉시 clamp가 필요한 경우(예: MaxHealth가 내려간 순간)
        public void ForceRecalculate()
        {
            dirty = false;
            RecalculateValue();
        }

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

            float max = GetMaxForClamp();

            // ✅ regen도 동적 max 기준
            if (Definition.hasRegeneration && CurrentValue < max)
            {
                if (Time.time - lastDamageTime >= Definition.regenerationDelay)
                {
                    AddBaseValue(Definition.regenerationRate * deltaTime); // dirty=true
                }
            }

            if (dirty)
            {
                dirty = false;
                RecalculateValue();
            }
        }

        private float GetMaxForClamp()
        {
            float max = Definition.maxValue;
            if (MaxValueGetter != null)
            {
                try { max = MaxValueGetter(); }
                catch { max = Definition.maxValue; }
            }

            if (max < Definition.minValue) max = Definition.minValue;
            return max;
        }

        private void RecalculateValue()
        {
            float oldValue = CurrentValue;
            float finalValue = BaseValue;

            var flatModifiers = modifiers.Where(m => m.Type == ModifierType.Flat).Sum(m => m.Value);
            var percentModifiers = modifiers.Where(m => m.Type == ModifierType.Percent).Sum(m => m.Value);

            finalValue += flatModifiers;
            finalValue *= (1f + percentModifiers);

            float max = GetMaxForClamp();
            CurrentValue = Mathf.Clamp(finalValue, Definition.minValue, max);

            if (Math.Abs(oldValue - CurrentValue) > 0.001f)
            {
                OnValueChanged?.Invoke(oldValue, CurrentValue);
                if (CurrentValue < oldValue)
                    lastDamageTime = Time.time;
            }
        }
    }
}
