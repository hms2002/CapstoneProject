public class BossPatternExecuteState : BossState
{
    // 이 클래스의 책임:
    // 예약된 보스 패턴을 실제로 실행하고, 능력 브리지를 통해 실행 완료 시점을 감지해 다음 상태로 넘긴다.

    private bool activationRequested;

    public BossPatternExecuteState(BossControllerBase boss) : base(boss) { }

    public override void OnEnter()
    {
        activationRequested = false;

        BossPatternEntry reservedPattern = boss.PatternRuntime.ReservedPattern;
        if (reservedPattern == null)
        {
            LogState("예약된 패턴이 없어 다시 대기합니다.");
            boss.ChangeState(boss.GetCombatIdleState());
            return;
        }

        string reservedPatternName = reservedPattern.Ability != null ? reservedPattern.Ability.name : "None";
        LogState($"패턴 '{reservedPatternName}'을 실행합니다.");
        activationRequested = boss.TryStartPattern(reservedPattern);

        if (!activationRequested)
        {
            LogState($"패턴 '{reservedPatternName}' 실행에 실패했습니다.");
            boss.PatternRuntime.ClearReservedPattern();
            boss.ChangeState(boss.GetCombatIdleState());
        }
    }

    public override void OnUpdate()
    {
        if (!activationRequested)
            return;

        if (abilityBridge != null && abilityBridge.IsAbilityExecutionBusy)
            return;

        string currentPatternName = boss.PatternRuntime.CurrentPattern != null && boss.PatternRuntime.CurrentPattern.Ability != null
            ? boss.PatternRuntime.CurrentPattern.Ability.name
            : "None";
        LogState($"패턴 '{currentPatternName}'을 마칩니다.");
        boss.FinishCurrentPattern();
        boss.ChangeState(boss.GetCombatIdleState());
    }

    public override void OnExit()
    {
        boss.PatternRuntime.ClearReservedPattern();
    }
}
