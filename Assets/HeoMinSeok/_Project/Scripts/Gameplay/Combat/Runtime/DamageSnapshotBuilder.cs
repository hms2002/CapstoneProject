namespace UnityGAS
{
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// 책임 :
    /// - 공격 생성 시점의 능력치 기반 피해 계산을 CombatDamageSnapshot으로 봉인한다.
    /// - HP / 스태거 / 넉백뿐 아니라 자동 원소 누적도 같은 시점에 캡처해 장기 공격체가 이후 스탯 변화를 다시 참조하지 않게 한다.
    /// </summary>
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
            ScaledStatFormula knockbackFormula,
            GameObject elementSource = null)
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
                baseKnockback: baseKnockback,
                elementSource: elementSource
            );
        }

        public static CombatDamageSnapshot BuildFromBaseValues(
            IStatProvider statProvider,
            DamagePayloadConfig config,
            float baseHp,
            float baseStagger,
            float baseKnockback,
            GameObject elementSource = null)
        {
            WarnLegacyElementFormulasIgnored(config);

            var result = DamageFormulaUtil.PostProcess(
                statProvider,
                baseHpDamage: baseHp,
                baseStaggerDamage: baseStagger
            );

            ElementDamageResult[] elementBuildUps = CaptureElementBuildUps(elementSource, config);
            bool hasResolvedElementBuildUps = elementSource != null;

            return new CombatDamageSnapshot(
                finalHpDamage: result.hpDamage,
                finalStaggerBuildUp: result.staggerDamage,
                finalKnockbackImpulse: baseKnockback,
                isCriticalHit: result.isCrit,
                finalElementBuildUps: elementBuildUps,
                hasResolvedElementBuildUps: hasResolvedElementBuildUps
            );
        }

        /// <summary>
        /// 책임 :
        /// - 공격자 현재 ElementOffenseSource와 능력치를 생성 시점 값으로 평가해 배열로 복사한다.
        /// - 원소 누적을 사용하지 않는 설정은 빈 snapshot으로 고정해 적용 시점 재조회가 일어나지 않게 한다.
        /// </summary>
        private static ElementDamageResult[] CaptureElementBuildUps(GameObject elementSource, DamagePayloadConfig config)
        {
            if (elementSource == null || (config != null && !config.includeElementBuildUp))
                return null;

            List<ElementDamageResult> buffer = ElementBuildUpResolver.Evaluate(elementSource, target: null);
            if (buffer == null || buffer.Count == 0)
                return System.Array.Empty<ElementDamageResult>();

            return buffer.ToArray();
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
