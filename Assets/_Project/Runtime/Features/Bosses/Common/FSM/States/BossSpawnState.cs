public class BossSpawnState : BossState
{
    public BossSpawnState(BossControllerBase boss) : base(boss) { }

    public override void OnEnter()
    {
        LogState("스폰을 시작합니다.");
        boss.NotifySpawnFinished();
    }
}
