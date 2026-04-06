public class BossGroggyState : BossState
{
    public BossGroggyState(BossControllerBase boss) : base(boss) { }

    public override void OnEnter()
    {
        LogState("그로기 상태에 들어갑니다.");
        boss.AbortCurrentPattern();
    }

    public override void OnUpdate()
    {
        if (boss.HasGroggyTag())
            return;

        LogState("그로기가 끝나 다시 전투를 고릅니다.");
        boss.ChangeState(boss.GetCombatIdleState());
    }
}
