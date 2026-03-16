using System.Collections.Generic;

namespace UnityGAS
{
    public static class DamageSnapshotBuilder
    {
        public static CombatDamageSnapshot Build(
            AttributeSet attributeSet,
            IStatProvider statProvider,
            DamagePayloadConfig config,
            ScaledStatFormula damageFormula,
            ScaledStatFormula knockbackFormula)
        {
            if (attributeSet == null)
                return new CombatDamageSnapshot(0f, 0f, 0f, null);

            float baseHp = 0f;
            if (damageFormula != null)
                baseHp = damageFormula.Evaluate(attributeSet, statProvider, defaultIfEmpty: 0f);

            float baseKnockback = 0f;
            if (knockbackFormula != null)
                baseKnockback = knockbackFormula.Evaluate(attributeSet, statProvider, defaultIfEmpty: 0f);

            float baseStagger = 0f;
            if (config != null && config.includeStaggerBuildUp && config.staggerFormula != null)
                baseStagger = config.staggerFormula.Evaluate(attributeSet, statProvider, defaultIfEmpty: 0f);

            List<ElementDamageInput> elementInputs = null;
            if (config != null && config.includeElementBuildUp && config.HasElementFormulas)
            {
                elementInputs = new List<ElementDamageInput>(config.elementFormulas.Length);

                for (int i = 0; i < config.elementFormulas.Length; i++)
                {
                    var e = config.elementFormulas[i];
                    if (e == null || e.elementType == null || e.formula == null)
                        continue;

                    float v = e.formula.Evaluate(attributeSet, statProvider, defaultIfEmpty: 0f);
                    if (v <= 0f)
                        continue;

                    elementInputs.Add(new ElementDamageInput
                    {
                        elementType = e.elementType,
                        baseDamage = v
                    });
                }
            }

            return BuildFromBaseValues(
                statProvider: statProvider,
                config: config,
                baseHp: baseHp,
                baseStagger: baseStagger,
                baseKnockback: baseKnockback,
                elementInputs: elementInputs
            );
        }

        public static CombatDamageSnapshot BuildFromBaseValues(
            IStatProvider statProvider,
            DamagePayloadConfig config,
            float baseHp,
            float baseStagger,
            float baseKnockback,
            List<ElementDamageInput> elementInputs)
        {
            List<ElementDamageResult> elementResults = null;
            if (elementInputs != null && elementInputs.Count > 0)
                elementResults = new List<ElementDamageResult>(elementInputs.Count);

            var r = DamageFormulaUtil.PostProcess(
                statProvider,
                baseHpDamage: baseHp,
                baseStaggerDamage: baseStagger,
                elementInputs: elementInputs,
                outElementResults: elementResults,
                critAffectsElement: (config == null ? true : config.critAffectsElement)
            );

            return new CombatDamageSnapshot(
                finalHpDamage: r.hpDamage,
                finalStaggerBuildUp: r.staggerDamage,
                finalKnockbackImpulse: baseKnockback,
                elementBuildUps: elementResults
            );
        }
    }
}