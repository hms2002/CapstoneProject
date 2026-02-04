using System;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// Stagger gauge build-up system ("현기증/무력화 게이지").
    /// When reaches threshold, a debuff/effect is applied.
    /// </summary>
    [DisallowMultipleComponent]
    public class StaggerGaugeSystem : MonoBehaviour
    {
        [Min(1f)] public float threshold = 100f;

        [Tooltip("Applied when stagger gauge reaches threshold.")]
        public GameplayEffect staggeredEffect;

        [Tooltip("If true, overflow carries over (value -= threshold). If false, value is reset to 0.")]
        public bool allowOverflow = true;

        [Tooltip("Optional: build-up resistance attribute on target. (0.2 = 20% reduction)")]
        public AttributeDefinition resistancePercentAttribute;

        [NonSerialized] public float value;

        public event Action<float, float> OnGaugeChanged; // old,new
        public event Action OnTriggered;

        private GameplayEffectRunner _runner;
        private AttributeSet _attr;

        private void Awake()
        {
            _runner = GetComponent<GameplayEffectRunner>();
            _attr = GetComponent<AttributeSet>();
        }

        public void Clear() => value = 0f;

        public void AddBuildUp(float amount, GameObject instigator, GameObject causer)
        {
            if (amount <= 0f) return;

            if (resistancePercentAttribute != null && _attr != null)
            {
                float r = Mathf.Clamp01(_attr.GetAttributeValue(resistancePercentAttribute));
                amount *= (1f - r);
                if (amount <= 0f) return;
            }

            float old = value;
            value += amount;
            OnGaugeChanged?.Invoke(old, value);

            if (value >= threshold)
            {
                if (allowOverflow) value -= threshold;
                else value = 0f;

                OnTriggered?.Invoke();

                if (staggeredEffect != null && _runner != null)
                {
                    _runner.ApplyEffect(staggeredEffect, gameObject, instigator != null ? instigator : causer);
                }
            }
        }
    }
}
