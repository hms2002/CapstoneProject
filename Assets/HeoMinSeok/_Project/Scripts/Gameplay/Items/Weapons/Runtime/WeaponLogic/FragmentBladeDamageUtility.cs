using UnityEngine;

namespace UnityGAS.Sample
{
    /// <summary>
    /// 책임 :
    /// - 파편검 ability logic들이 공통으로 사용하는 CombatHitPayload 생성 절차를 모은다.
    /// - 피해/스태거/원소 공식을 같은 방식으로 평가해 기본 공격과 회수 피해의 계산 규칙을 맞춘다.
    /// </summary>
    public static class FragmentBladeDamageUtility
    {
        public static CombatHitPayload BuildPayload(
            AbilitySystem system,
            AbilitySpec spec,
            DamagePayloadConfig config,
            GameplayEffect damageEffect,
            GE_Knockback_Spec knockbackEffect,
            ScaledStatFormula damageFormula,
            ScaledStatFormula knockbackFormula,
            float legacyDamage,
            float legacyStaggerDamage,
            float damageScale,
            GameplayTag hitConfirmedTag = null)
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

            float baseStagger = (config != null && config.includeStaggerBuildUp && config.staggerFormula != null)
                ? config.staggerFormula.Evaluate(system.AttributeSet, statProvider, defaultIfEmpty: 0f)
                : legacyStaggerDamage;
            baseStagger *= safeScale;

            var snapshot = DamageSnapshotBuilder.BuildFromBaseValues(
                statProvider: statProvider,
                config: config,
                baseHp: baseHp,
                baseStagger: config != null && config.includeStaggerBuildUp ? baseStagger : 0f,
                baseKnockback: baseKnockback,
                elementSource: system.gameObject);

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

    }
}
