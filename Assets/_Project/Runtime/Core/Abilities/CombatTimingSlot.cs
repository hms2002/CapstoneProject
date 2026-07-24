namespace UnityGAS
{
    /// <summary>
    /// 책임:
    /// - 전투 코루틴의 대기 시간이 어떤 의도의 시간인지 구분한다.
    /// - CombatTimingService가 공격속도 보정 가능 시간과 연출 전용 시간을 안전하게 분리하게 돕는다.
    /// </summary>
    public enum CombatTimingSlot
    {
        AttackWarning,
        AttackRecovery,
        AttackInterval,
        AbilityCast,
        AbilityRecovery,
        AbilityCooldown,
        PresentationOnly
    }
}
