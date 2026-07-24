public class BossDeadState : BossState
{
    public BossDeadState(BossControllerBase boss) : base(boss) { }

    public override void OnEnter()
    {
        LogState("사망 상태에 들어갑니다.");
        boss.AbortCurrentPattern();
    }
}
