public class BossDeadState : BossState
{
    public BossDeadState(BossControllerBase boss) : base(boss) { }

    public override void OnEnter()
    {
        boss.AbortCurrentPattern();
    }
}