public class WitchRetreatToCandleState : BossState
{
    private readonly Witch witch;

    public WitchRetreatToCandleState(Witch witch) : base(witch)
    {
        this.witch = witch;
    }

    public override void OnEnter()
    {
        BossPatternEntry reservedPattern = boss.PatternRuntime.ReservedPattern;
        if (reservedPattern == null)
        {
            LogState("촛대로의 피난 패턴 예약 정보가 없습니다.");
            boss.ChangeState(boss.GetCombatIdleState());
            return;
        }

        bool activationRequested = boss.TryStartPattern(reservedPattern);
        if (!activationRequested)
        {
            string reservedPatternName = reservedPattern.Ability != null ? reservedPattern.Ability.name : "None";
            LogState($"촛대로의 피난 패턴 '{reservedPatternName}' 실행에 실패했습니다.");
            boss.PatternRuntime.ClearReservedPattern();
            boss.ChangeState(boss.GetCombatIdleState());
            return;
        }

        if (!witch.StartRetreat())
        {
            LogState("촛대로의 피난 패턴을 시작하지 못했습니다.");
            boss.AbortCurrentPattern();
            boss.ChangeState(boss.GetCombatIdleState());
            return;
        }

        boss.FinishCurrentPattern();
        LogState("촛대로의 피난 패턴이 발동됐습니다.");
        boss.ChangeState(boss.GetCombatIdleState());
    }

    public override void OnExit()
    {
        boss.PatternRuntime.ClearReservedPattern();
    }
}
