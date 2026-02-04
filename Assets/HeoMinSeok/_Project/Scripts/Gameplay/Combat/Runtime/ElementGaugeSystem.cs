using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// Element gauge build-up system.
    /// In this project, "element damage" is interpreted as "element gauge fill".
    /// When a gauge reaches its threshold, a corresponding debuff/effect is applied.
    /// </summary>
    [DisallowMultipleComponent]
    public class ElementGaugeSystem : MonoBehaviour
    {
        [Serializable]
        public class GaugeEntry
        {
            [Tooltip("Element type tag. e.g. Element.Fire / Element.Bleed / Element.Poison")]
            public GameplayTag elementType;

            [Min(1f)]
            public float threshold = 100f;

            [Tooltip("Applied when gauge reaches threshold.")]
            public GameplayEffect debuffEffect;

            [Header("Trigger Damage (Optional)")]
            [Tooltip("If debuffEffect is a GE_Damage_Spec, this will inject a SetByCaller damage value when the gauge triggers.")]
            public bool injectDamageToSpec = false;

            [Tooltip("Override SetByCaller key. If empty, uses GE_Damage_Spec.damageKey.")]
            public GameplayTag setByCallerKeyOverride;

            [Range(0f, 1f)]
            [Tooltip("Damage = target current health * percentOfCurrentHealth (evaluated per trigger). E.g. 0.1 = 10%.")]
            public float percentOfCurrentHealth = 0f;

            [Tooltip("Optional: add damage based on instigator Attribute. Damage += instigatorStat * instigatorStatMultiplier")]
            public AttributeDefinition instigatorStatAttribute;

            [Tooltip("Multiplier for instigatorStatAttribute.")]
            public float instigatorStatMultiplier = 0f;

            [Tooltip("Optional: add damage based on build-up amount from the hit that triggered this gauge. Damage += buildUpAmount * buildUpAmountMultiplier")]
            public float buildUpAmountMultiplier = 0f;

            [Tooltip("Optional: flat bonus damage added on trigger.")]
            public float flatDamageBonus = 0f;

            [Tooltip("If true, overflow carries over (value -= threshold). If false, value is reset to 0.")]
            public bool allowOverflow = true;

            [Tooltip("Optional: build-up resistance attribute on target. (0.2 = 20% reduction)")]
            public AttributeDefinition resistancePercentAttribute;

            [NonSerialized] public float value;
        }

        [Header("Gauges")]
        public List<GaugeEntry> gauges = new();

        [Header("Debug")]
        [Tooltip("When true, logs whenever a gauge reaches its threshold.")]
        public bool logWhenTriggered = false;

        public event Action<GameplayTag, float, float> OnGaugeChanged; // (element, old, new)
        public event Action<GameplayTag> OnGaugeTriggered;            // (element)

        private GameplayEffectRunner _runner;
        private AttributeSet _attr;

        private void Awake()
        {
            _runner = GetComponent<GameplayEffectRunner>();
            _attr = GetComponent<AttributeSet>();
        }

        public float GetValue(GameplayTag elementType)
        {
            var g = FindGauge(elementType);
            return g != null ? g.value : 0f;
        }

        public float GetThreshold(GameplayTag elementType)
        {
            var g = FindGauge(elementType);
            return g != null ? g.threshold : 0f;
        }

        public void ClearAll()
        {
            for (int i = 0; i < gauges.Count; i++)
            {
                if (gauges[i] == null) continue;
                gauges[i].value = 0f;
            }
        }

        /// <summary>
        /// Add build-up amount to a specific element gauge.
        /// </summary>
        public void AddBuildUp(GameplayTag elementType, float amount, GameObject instigator, GameObject causer)
        {
            if (elementType == null || amount <= 0f) return;

            // "Raw" build-up as provided by the attacker. (Currently no resistance system in this project.)
            float rawBuildUp = amount;

            var g = FindGauge(elementType);
            if (g == null)
            {
#if UNITY_EDITOR
                Debug.LogWarning($"[ElementGaugeSystem] Missing gauge entry for element '{elementType.CachedPath}' on '{name}'. Ignoring build-up.");
#endif
                return;
            }

            // Resistance (percent reduction)
            if (g.resistancePercentAttribute != null && _attr != null)
            {
                float r = Mathf.Clamp01(_attr.GetAttributeValue(g.resistancePercentAttribute));
                amount *= (1f - r);
                if (amount <= 0f) return;
            }

            float old = g.value;
            g.value += amount;
            OnGaugeChanged?.Invoke(elementType, old, g.value);

            if (g.threshold <= 0f) return;

            // Support overflow triggering multiple times in a single hit if desired.
            // (e.g., one big hit can fill the gauge more than once.)
            int triggerCount = 0;
            while (g.value >= g.threshold)
            {
                triggerCount++;

                if (g.allowOverflow)
                    g.value -= g.threshold;
                else
                {
                    g.value = 0f;
                    break; // if not allowing overflow, only trigger once
                }
            }

            if (triggerCount <= 0) return;

            // Notify (once per trigger)
            for (int i = 0; i < triggerCount; i++)
                OnGaugeTriggered?.Invoke(elementType);

            if (logWhenTriggered)
            {
                string srcName = (instigator != null ? instigator.name : (causer != null ? causer.name : "null"));
                Debug.Log($"[ElementGaugeSystem] TRIGGER {elementType.CachedPath} x{triggerCount} on '{name}' (source='{srcName}')");
            }

            // Apply debuff/effect (once per trigger)
            if (g.debuffEffect != null && _runner != null)
            {
                var src = instigator != null ? instigator : causer;
                // If configured, inject computed damage into GE_Damage_Spec using SetByCaller.
                // This supports effects like "Bleed gauge max -> deal 10% of current HP".
                if (g.injectDamageToSpec && g.debuffEffect is GE_Damage_Spec dmgSpec)
                {
                    var key = g.setByCallerKeyOverride != null ? g.setByCallerKeyOverride : dmgSpec.damageKey;
                    var healthAttr = dmgSpec.healthAttribute;

                    for (int i = 0; i < triggerCount; i++)
                    {
                        float damage = 0f;

                        // (1) Percent of current health
                        if (g.percentOfCurrentHealth > 0f && healthAttr != null && _attr != null)
                        {
                            float curHp = _attr.GetAttributeValue(healthAttr);
                            if (curHp > 0f)
                                damage += curHp * g.percentOfCurrentHealth;
                        }

                        // (2) Instigator stat contribution (future-proof)
                        if (g.instigatorStatAttribute != null && g.instigatorStatMultiplier != 0f && src != null)
                        {
                            var instAttr = src.GetComponent<AttributeSet>();
                            if (instAttr != null)
                                damage += instAttr.GetAttributeValue(g.instigatorStatAttribute) * g.instigatorStatMultiplier;
                        }

                        // (3) Build-up amount contribution (future-proof)
                        if (g.buildUpAmountMultiplier != 0f)
                            damage += rawBuildUp * g.buildUpAmountMultiplier;

                        // (4) Flat bonus
                        if (g.flatDamageBonus != 0f)
                            damage += g.flatDamageBonus;

                        if (damage <= 0f || key == null)
                        {
                            // If nothing to inject, just apply the effect normally.
                            _runner.ApplyEffect(g.debuffEffect, gameObject, src);
                            continue;
                        }

                        var ctx = new GameplayEffectContext(instigator != null ? instigator : src, causer != null ? causer : src);
                        ctx.SourceObject = g.debuffEffect;
                        var spec = new GameplayEffectSpec(g.debuffEffect, ctx);
                        spec.SetSetByCallerMagnitude(key, damage);

                        _runner.ApplyEffectSpec(spec, gameObject);
                    }
                }
                else
                {
                    for (int i = 0; i < triggerCount; i++)
                        _runner.ApplyEffect(g.debuffEffect, gameObject, src);
                }
            }
        }

        private GaugeEntry FindGauge(GameplayTag elementType)
        {
            if (gauges == null) return null;
            for (int i = 0; i < gauges.Count; i++)
            {
                var g = gauges[i];
                if (g != null && g.elementType == elementType)
                    return g;
            }
            return null;
        }
    }
}

