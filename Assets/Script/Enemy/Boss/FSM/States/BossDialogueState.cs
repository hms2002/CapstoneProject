public class BossEncounterIntroState : BossState
{
    private bool started;

    public BossEncounterIntroState(BossControllerBase boss) : base(boss) { }

    public override void OnEnter()
    {
        started = boss.TryStartEncounterIntro();

        if (started)
        {
            LogState("인트로 시퀀스를 시작합니다.");
            return;
        }

        LogState("인트로를 시작하지 못해 바로 전투로 넘어갑니다.");
        boss.FinishEncounterIntro();
        boss.ChangeState(boss.GetCombatIdleState());
    }

    public override void OnUpdate()
    {
        if (!started) return;

        if (boss.IsEncounterIntroActive()) return;

        LogState("인트로가 끝나 전투를 시작합니다.");
        boss.FinishEncounterIntro();
        boss.ChangeState(boss.GetCombatIdleState());
    }
}
