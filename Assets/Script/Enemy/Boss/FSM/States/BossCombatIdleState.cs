public class BossCombatIdleState : BossState
{
    private float thinkDelay;

    public BossCombatIdleState(BossControllerBase boss) : base(boss) { }

    public override void OnEnter()
    {
        thinkDelay = boss.GetCurrentPhaseThinkDelay();
        LogState($"전투를 고릅니다. {thinkDelay:F2}초 대기합니다.");
    }

    public override void OnUpdate()
    {
        if (boss.Blackboard.StateElapsedTime < thinkDelay)
            return;

        LogState("다음 패턴을 고릅니다.");
        boss.ChangeState(boss.GetPatternSelectState());
    }
}
