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
            boss.ChangeState(boss.GetCombatIdleState());
            return;
        }

        activationRequested = boss.TryStartPattern(reservedPattern);

        if (!activationRequested)
        {
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

        boss.FinishCurrentPattern();
        boss.ChangeState(boss.GetCombatIdleState());
    }

    public override void OnExit()
    {
        boss.Blackboard.ClearReservedPattern();
    }
}