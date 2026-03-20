using System;
using UnityEngine;

namespace UnityGAS
{
    [DisallowMultipleComponent]
    public class StaggerGaugeSystem : MonoBehaviour
    {
        [Header("Gauge Attributes")]
        public AttributeDefinition currentGaugeAttribute;      // 예: StaggerGauge
        public AttributeDefinition maxGaugeAttribute;          // 예: MaxStaggerGauge
        public AttributeDefinition resistancePercentAttribute; // 예: StaggerResistance (0.2 = 20%)

        [Header("Trigger")]
        public GameplayEffect staggeredEffect;
        public bool allowOverflow = true;

        public event Action<float, float> OnGaugeChanged; // old,new
        public event Action OnTriggered;

        private GameplayEffectRunner _runner;
        private AttributeSet _attr;

        private void Awake()
        {
            _runner = GetComponent<GameplayEffectRunner>();
            _attr = GetComponent<AttributeSet>();
        }

        public void Clear()
        {
            if (_attr == null || currentGaugeAttribute == null) return;

            float old = GetCurrentGauge();
            SetCurrentGauge(0f);
            OnGaugeChanged?.Invoke(old, 0f);
        }

        public void AddBuildUp(float amount, GameObject instigator, GameObject causer)
        {
            if (_attr == null) return;
            if (currentGaugeAttribute == null || maxGaugeAttribute == null) return;
            if (amount <= 0f) return;

            if (resistancePercentAttribute != null)
            {
                float resist = Mathf.Clamp01(_attr.GetAttributeValue(resistancePercentAttribute));
                amount *= (1f - resist);
                if (amount <= 0f) return;
            }

            float old = GetCurrentGauge();
            float max = Mathf.Max(0f, _attr.GetAttributeValue(maxGaugeAttribute));
            float next = old + amount;

            if (max <= 0f)
            {
                SetCurrentGauge(next);
                OnGaugeChanged?.Invoke(old, next);
                return;
            }

            int triggerCount = 0;
            while (next >= max)
            {
                triggerCount++;

                if (allowOverflow)
                    next -= max;
                else
                {
                    next = 0f;
                    break;
                }
            }

            SetCurrentGauge(next);
            OnGaugeChanged?.Invoke(old, next);

            if (triggerCount <= 0)
                return;

            for (int i = 0; i < triggerCount; i++)
                OnTriggered?.Invoke();

            if (staggeredEffect != null && _runner != null)
            {
                var src = instigator != null ? instigator : causer;
                for (int i = 0; i < triggerCount; i++)
                    _runner.ApplyEffect(staggeredEffect, gameObject, src);
            }
        }

        private float GetCurrentGauge()
        {
            return _attr != null && currentGaugeAttribute != null
                ? _attr.GetAttributeValue(currentGaugeAttribute)
                : 0f;
        }

        private void SetCurrentGauge(float value)
        {
            if (_attr == null || currentGaugeAttribute == null)
                return;

            _attr.TrySetBaseValue(currentGaugeAttribute, Mathf.Max(0f, value), this);
        }
    }
}