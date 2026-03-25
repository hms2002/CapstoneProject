public class BossSpawnState : BossState
{
    public BossSpawnState(BossControllerBase boss) : base(boss) { }

    public override void OnEnter()
    {
        boss.NotifySpawnFinished();
    }
}