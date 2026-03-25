public class BossCombatIdleState : BossState
{
    private float thinkDelay;

    public BossCombatIdleState(BossControllerBase boss) : base(boss) { }

    public override void OnEnter()
    {
        thinkDelay = boss.GetCurrentPhaseThinkDelay();
    }

    public override void OnUpdate()
    {
        if (boss.Blackboard.StateElapsedTime < thinkDelay)
            return;

        boss.ChangeState(boss.GetPatternSelectState());
    }
}