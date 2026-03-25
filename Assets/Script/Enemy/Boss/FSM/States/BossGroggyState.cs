public class BossGroggyState : BossState
{
    public BossGroggyState(BossControllerBase boss) : base(boss) { }

    public override void OnEnter()
    {
        boss.AbortCurrentPattern();
    }

    public override void OnUpdate()
    {
        if (boss.HasGroggyTag())
            return;

        boss.ChangeState(boss.GetCombatIdleState());
    }
}