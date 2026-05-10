using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

namespace UnityGAS.Sample
{
    public static class WeaponDamagePayloadUtility
    {
        public static CombatHitPayload BuildPayload(
            AbilitySystem system,
            AbilitySpec spec,
            UnityGAS.DamagePayloadConfig config,
            GameplayEffect damageEffect,
            GE_Knockback_Spec knockbackEffect,
            ScaledStatFormula damageFormula,
            ScaledStatFormula knockbackFormula,
            float legacyDamage,
            float legacyStaggerDamage,
            float damageScale,
            GameplayTag hitConfirmedTag,
            WeaponComboElementDamageGroup fallbackElementDamages = null)
        {
            if (system == null || system.AttributeSet == null || damageEffect == null)
                return null;

            IStatProvider statProvider = AbilityStatProviderFactory.Create(system);
            float safeScale = Mathf.Max(0f, damageScale);

            float baseHp = damageFormula != null
                ? damageFormula.Evaluate(system.AttributeSet, statProvider, defaultIfEmpty: legacyDamage)
                : legacyDamage;
            baseHp *= safeScale;

            float baseKnockback = knockbackFormula != null
                ? knockbackFormula.Evaluate(system.AttributeSet, statProvider, defaultIfEmpty: 0f)
                : 0f;
            baseKnockback *= safeScale;

            float baseStagger = config != null && config.includeStaggerBuildUp && config.staggerFormula != null
                ? config.staggerFormula.Evaluate(system.AttributeSet, statProvider, defaultIfEmpty: 0f)
                : legacyStaggerDamage;
            baseStagger *= safeScale;

            List<ElementDamageInput> elementInputs = BuildElementInputs(
                system,
                statProvider,
                config,
                safeScale,
                fallbackElementDamages);

            CombatDamageSnapshot snapshot = DamageSnapshotBuilder.BuildFromBaseValues(
                statProvider: statProvider,
                config: config,
                baseHp: baseHp,
                baseStagger: config != null && config.includeStaggerBuildUp ? baseStagger : 0f,
                baseKnockback: baseKnockback,
                elementInputs: elementInputs);

            if (snapshot.FinalHpDamage <= 0f)
                return null;

            return CombatHitPayload.FromSnapshot(
                sourceSystem: system,
                sourceSpec: spec,
                damageEffect: damageEffect,
                knockbackEffect: knockbackEffect,
                snapshot: snapshot,
                hitConfirmedTag: hitConfirmedTag,
                causer: system.gameObject);
        }

        private static List<ElementDamageInput> BuildElementInputs(
            AbilitySystem system,
            IStatProvider statProvider,
            UnityGAS.DamagePayloadConfig config,
            float scale,
            WeaponComboElementDamageGroup fallbackElementDamages)
        {
            if (system == null || statProvider == null || config == null || !config.includeElementBuildUp)
                return null;

            if (config.HasElementFormulas)
            {
                List<ElementDamageInput> elementInputs = new(config.elementFormulas.Length);
                for (int i = 0; i < config.elementFormulas.Length; i++)
                {
                    ElementFormulaEntry entry = config.elementFormulas[i];
                    if (entry == null || entry.elementType == null || entry.formula == null)
                        continue;

                    float value = entry.formula.Evaluate(system.AttributeSet, statProvider, defaultIfEmpty: 0f) * scale;
                    if (value <= 0f)
                        continue;

                    elementInputs.Add(new ElementDamageInput
                    {
                        elementType = entry.elementType,
                        baseDamage = value
                    });
                }

                return elementInputs.Count > 0 ? elementInputs : null;
            }

            if (fallbackElementDamages?.elements == null || fallbackElementDamages.elements.Count == 0)
                return null;

            List<ElementDamageInput> fallbackInputs = new(fallbackElementDamages.elements.Count);
            for (int i = 0; i < fallbackElementDamages.elements.Count; i++)
            {
                ElementDamageInput input = fallbackElementDamages.elements[i];
                if (input.elementType == null || input.baseDamage <= 0f)
                    continue;

                input.baseDamage *= scale;
                fallbackInputs.Add(input);
            }

            return fallbackInputs.Count > 0 ? fallbackInputs : null;
        }
    }
}
