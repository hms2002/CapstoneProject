using System.Collections.Generic;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 공용 피해 계산 결과를 하나의 스냅샷으로 보관한다.
    /// - 최종 피해량과 함께 적중 결과(예: 치명타 여부)를 호출부에 전달한다.
    /// </summary>
    public readonly struct CombatDamageSnapshot
    {
        public readonly float FinalHpDamage;
        public readonly float FinalStaggerBuildUp;
        public readonly float FinalKnockbackImpulse;
        public readonly List<ElementDamageResult> ElementBuildUps;
        public readonly bool IsCriticalHit;

        public CombatDamageSnapshot(
            float finalHpDamage,
            float finalStaggerBuildUp,
            float finalKnockbackImpulse,
            List<ElementDamageResult> elementBuildUps,
            bool isCriticalHit)
        {
            FinalHpDamage = finalHpDamage;
            FinalStaggerBuildUp = finalStaggerBuildUp;
            FinalKnockbackImpulse = finalKnockbackImpulse;
            ElementBuildUps = elementBuildUps;
            IsCriticalHit = isCriticalHit;
        }
    }
}
