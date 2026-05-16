namespace UnityGAS
{
    /// <summary>
    /// Shared damage calculation result passed through the hit pipeline.
    /// Element build-up is resolved at application time from the attacker's ElementOffenseSource.
    /// </summary>
    public readonly struct CombatDamageSnapshot
    {
        public readonly float FinalHpDamage;
        public readonly float FinalStaggerBuildUp;
        public readonly float FinalKnockbackImpulse;
        public readonly bool IsCriticalHit;

        public CombatDamageSnapshot(
            float finalHpDamage,
            float finalStaggerBuildUp,
            float finalKnockbackImpulse,
            bool isCriticalHit)
        {
            FinalHpDamage = finalHpDamage;
            FinalStaggerBuildUp = finalStaggerBuildUp;
            FinalKnockbackImpulse = finalKnockbackImpulse;
            IsCriticalHit = isCriticalHit;
        }
    }
}
