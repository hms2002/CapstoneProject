namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 공격체 생성 시점에 확정된 HP / 스태거 / 넉백 / 원소 누적 결과를 보관한다.
    /// - 장기 생존 히트박스나 투사체가 적중 시점의 공격자 스탯을 다시 읽지 않도록 피해 파이프라인의 snapshot 경계를 제공한다.
    /// </summary>
    public readonly struct CombatDamageSnapshot
    {
        public readonly float FinalHpDamage;
        public readonly float FinalStaggerBuildUp;
        public readonly float FinalKnockbackImpulse;
        public readonly bool IsCriticalHit;
        public readonly ElementDamageResult[] FinalElementBuildUps;
        public readonly bool HasResolvedElementBuildUps;

        public CombatDamageSnapshot(
            float finalHpDamage,
            float finalStaggerBuildUp,
            float finalKnockbackImpulse,
            bool isCriticalHit)
            : this(
                finalHpDamage,
                finalStaggerBuildUp,
                finalKnockbackImpulse,
                isCriticalHit,
                finalElementBuildUps: null,
                hasResolvedElementBuildUps: false)
        {
        }

        public CombatDamageSnapshot(
            float finalHpDamage,
            float finalStaggerBuildUp,
            float finalKnockbackImpulse,
            bool isCriticalHit,
            ElementDamageResult[] finalElementBuildUps,
            bool hasResolvedElementBuildUps)
        {
            FinalHpDamage = finalHpDamage;
            FinalStaggerBuildUp = finalStaggerBuildUp;
            FinalKnockbackImpulse = finalKnockbackImpulse;
            IsCriticalHit = isCriticalHit;
            FinalElementBuildUps = finalElementBuildUps;
            HasResolvedElementBuildUps = hasResolvedElementBuildUps;
        }
    }
}
