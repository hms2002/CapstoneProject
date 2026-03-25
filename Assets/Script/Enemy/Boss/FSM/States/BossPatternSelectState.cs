public class BossPatternSelectState : BossState
{
    public BossPatternSelectState(BossControllerBase boss) : base(boss) { }

    public override void OnEnter()
    {
        BossPatternEntry nextPattern = boss.SelectNextPattern();

        if (nextPattern == null)
        {
            boss.ChangeState(boss.GetCombatIdleState());
            return;
        }

        boss.Blackboard.ReservePattern(nextPattern);
        boss.ChangeState(boss.GetPatternExecuteState());
    }
}