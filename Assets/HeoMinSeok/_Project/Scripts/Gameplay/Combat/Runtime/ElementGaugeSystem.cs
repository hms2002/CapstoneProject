using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    [DisallowMultipleComponent]
    public class ElementGaugeSystem : MonoBehaviour
    {
        [Serializable]
        public class GaugeEntry
        {
            [Tooltip("Element type tag. e.g. Element.Fire / Element.Bleed / Element.Poison")]
            public GameplayTag elementType;

            [Header("Gauge Attributes")]
            public AttributeDefinition currentGaugeAttribute;      // 예: FireGauge
            public AttributeDefinition maxGaugeAttribute;          // 예: MaxFireGauge
            public AttributeDefinition resistancePercentAttribute; // 예: FireResistance

            [Header("Trigger")]
            public GameplayEffect debuffEffect;
            public bool allowOverflow = true;

            [Header("Trigger Damage (Optional)")]
            public bool injectDamageToSpec = false;
            public GameplayTag setByCallerKeyOverride;
            [Range(0f, 1f)] public float percentOfCurrentHealth = 0f;
            public AttributeDefinition instigatorStatAttribute;
            public float instigatorStatMultiplier = 0f;
            public float buildUpAmountMultiplier = 0f;
            public float flatDamageBonus = 0f;
        }

        [Header("Gauges")]
        public List<GaugeEntry> gauges = new();

        [Header("Debug")]
        public bool logWhenTriggered = false;

        public event Action<GameplayTag, float, float> OnGaugeChanged; // element, old, new
        public event Action<GameplayTag> OnGaugeTriggered;

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
            return g != null ? GetGaugeValue(g) : 0f;
        }

        public float GetThreshold(GameplayTag elementType)
        {
            var g = FindGauge(elementType);
            return g != null ? Mathf.Max(0f, GetAttrValue(g.maxGaugeAttribute)) : 0f;
        }

        public void ClearAll()
        {
            if (gauges == null) return;

            for (int i = 0; i < gauges.Count; i++)
            {
                var g = gauges[i];
                if (g == null) continue;

                float old = GetGaugeValue(g);
                SetGaugeValue(g, 0f);
                OnGaugeChanged?.Invoke(g.elementType, old, 0f);
            }
        }

        public void AddBuildUp(GameplayTag elementType, float amount, GameObject instigator, GameObject causer)
        {
            if (_attr == null) return;
            if (elementType == null || amount <= 0f) return;

            float rawBuildUp = amount;

            var g = FindGauge(elementType);
            if (g == null)
            {
#if UNITY_EDITOR
                Debug.LogWarning($"[ElementGaugeSystem] Missing gauge entry for element '{elementType.CachedPath}' on '{name}'. Ignoring build-up.");
#endif
                return;
            }

            if (g.resistancePercentAttribute != null)
            {
                float resist = Mathf.Clamp01(GetAttrValue(g.resistancePercentAttribute));
                amount *= (1f - resist);
                if (amount <= 0f) return;
            }

            float old = GetGaugeValue(g);
            float max = Mathf.Max(0f, GetAttrValue(g.maxGaugeAttribute));
            float next = old + amount;

            if (max <= 0f)
            {
                SetGaugeValue(g, next);
                OnGaugeChanged?.Invoke(elementType, old, next);
                return;
            }

            int triggerCount = 0;
            while (next >= max)
            {
                triggerCount++;

                if (g.allowOverflow)
                    next -= max;
                else
                {
                    next = 0f;
                    break;
                }
            }

            SetGaugeValue(g, next);
            OnGaugeChanged?.Invoke(elementType, old, next);

            if (triggerCount <= 0)
                return;

            for (int i = 0; i < triggerCount; i++)
                OnGaugeTriggered?.Invoke(elementType);

            if (logWhenTriggered)
            {
                string srcName = instigator != null ? instigator.name : (causer != null ? causer.name : "null");
                Debug.Log($"[ElementGaugeSystem] TRIGGER {elementType.CachedPath} x{triggerCount} on '{name}' (source='{srcName}')");
            }

            if (g.debuffEffect == null || _runner == null)
                return;

            var src = instigator != null ? instigator : causer;

            if (g.injectDamageToSpec && g.debuffEffect is GE_Damage_Spec dmgSpec)
            {
                var key = g.setByCallerKeyOverride != null ? g.setByCallerKeyOverride : dmgSpec.damageKey;
                var healthAttr = dmgSpec.healthAttribute;

                for (int i = 0; i < triggerCount; i++)
                {
                    float damage = 0f;

                    if (g.percentOfCurrentHealth > 0f && healthAttr != null)
                    {
                        float curHp = GetAttrValue(healthAttr);
                        if (curHp > 0f)
                            damage += curHp * g.percentOfCurrentHealth;
                    }

                    if (g.instigatorStatAttribute != null && g.instigatorStatMultiplier != 0f && src != null)
                    {
                        var instAttr = src.GetComponent<AttributeSet>();
                        if (instAttr != null)
                            damage += instAttr.GetAttributeValue(g.instigatorStatAttribute) * g.instigatorStatMultiplier;
                    }

                    if (g.buildUpAmountMultiplier != 0f)
                        damage += rawBuildUp * g.buildUpAmountMultiplier;

                    damage += g.flatDamageBonus;
                    if (damage < 0f) damage = 0f;

                    if (key == null)
                    {
                        _runner.ApplyEffect(g.debuffEffect, gameObject, src);
                        continue;
                    }

                    var ctx = new GameplayEffectContext(src, src);
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

        private float GetGaugeValue(GaugeEntry g)
        {
            if (_attr == null || g == null || g.currentGaugeAttribute == null)
                return 0f;

            return _attr.GetAttributeValue(g.currentGaugeAttribute);
        }

        private void SetGaugeValue(GaugeEntry g, float value)
        {
            if (_attr == null || g == null || g.currentGaugeAttribute == null)
                return;

            _attr.TrySetBaseValue(g.currentGaugeAttribute, Mathf.Max(0f, value), this);
        }

        private float GetAttrValue(AttributeDefinition def)
        {
            return (_attr != null && def != null) ? _attr.GetAttributeValue(def) : 0f;
        }
    }
}