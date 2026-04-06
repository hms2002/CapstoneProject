public class BossPatternExecuteState : BossState
{
    private bool activationRequested;

    public BossPatternExecuteState(BossControllerBase boss) : base(boss) { }

    public override void OnEnter()
    {
        activationRequested = false;

        BossPatternEntry reservedPattern = boss.Blackboard.ReservedPattern;
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
            boss.Blackboard.ClearReservedPattern();
            boss.ChangeState(boss.GetCombatIdleState());
        }
    }

    public override void OnUpdate()
    {
        if (!activationRequested)
            return;

        if (boss.AbilitySystem != null && boss.AbilitySystem.IsBusy)
            return;

        string currentPatternName = boss.Blackboard.CurrentPattern != null && boss.Blackboard.CurrentPattern.Ability != null
            ? boss.Blackboard.CurrentPattern.Ability.name
            : "None";
        LogState($"패턴 '{currentPatternName}'을 마칩니다.");
        boss.FinishCurrentPattern();
        boss.ChangeState(boss.GetCombatIdleState());
    }

    public override void OnExit()
    {
        boss.Blackboard.ClearReservedPattern();
    }
}
