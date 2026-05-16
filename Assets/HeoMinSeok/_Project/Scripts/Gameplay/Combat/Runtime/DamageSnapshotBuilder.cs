namespace UnityGAS
{
    public static class DamageSnapshotBuilder
    {
#if UNITY_EDITOR
        private static bool s_warnedLegacyElementFormulasIgnored;
#endif

        public static CombatDamageSnapshot Build(
            AttributeSet attributeSet,
            IStatProvider statProvider,
            DamagePayloadConfig config,
            ScaledStatFormula damageFormula,
            ScaledStatFormula knockbackFormula)
        {
            if (attributeSet == null)
                return new CombatDamageSnapshot(0f, 0f, 0f, false);

            float baseHp = 0f;
            if (damageFormula != null)
                baseHp = damageFormula.Evaluate(attributeSet, statProvider, defaultIfEmpty: 0f);

            float baseKnockback = 0f;
            if (knockbackFormula != null)
                baseKnockback = knockbackFormula.Evaluate(attributeSet, statProvider, defaultIfEmpty: 0f);

            float baseStagger = 0f;
            if (config != null && config.includeStaggerBuildUp && config.staggerFormula != null)
                baseStagger = config.staggerFormula.Evaluate(attributeSet, statProvider, defaultIfEmpty: 0f);

            return BuildFromBaseValues(
                statProvider: statProvider,
                config: config,
                baseHp: baseHp,
                baseStagger: baseStagger,
                baseKnockback: baseKnockback
            );
        }

        public static CombatDamageSnapshot BuildFromBaseValues(
            IStatProvider statProvider,
            DamagePayloadConfig config,
            float baseHp,
            float baseStagger,
            float baseKnockback)
        {
            WarnLegacyElementFormulasIgnored(config);

            var result = DamageFormulaUtil.PostProcess(
                statProvider,
                baseHpDamage: baseHp,
                baseStaggerDamage: baseStagger
            );

            return new CombatDamageSnapshot(
                finalHpDamage: result.hpDamage,
                finalStaggerBuildUp: result.staggerDamage,
                finalKnockbackImpulse: baseKnockback,
                isCriticalHit: result.isCrit
            );
        }

        private static void WarnLegacyElementFormulasIgnored(DamagePayloadConfig config)
        {
#if UNITY_EDITOR
            if (s_warnedLegacyElementFormulasIgnored)
                return;

            if (config == null || !config.includeElementBuildUp || !config.HasElementFormulas)
                return;

            s_warnedLegacyElementFormulasIgnored = true;
            UnityEngine.Debug.LogWarning(
                "[DamageSnapshotBuilder] DamagePayloadConfig.elementFormulas is legacy and no longer " +
                "produces applied element build-up. Element build-up is resolved from the attacker's " +
                "ElementOffenseSource at CombatDamageAction application time.");
#endif
        }
    }
}
