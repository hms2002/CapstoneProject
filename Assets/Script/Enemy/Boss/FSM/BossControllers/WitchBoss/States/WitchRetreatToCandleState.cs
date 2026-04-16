using UnityGAS.Sample;

public class WitchRetreatToCandleState : BossState
{
    // 이 클래스의 책임:
    // 마녀의 촛대로 피난 패턴을 시작하고, 발동 실패/성공에 따른 상태 전환을 브리지 계약으로 정리한다.

    private readonly IWitchPatternStateBridge patternBridge;

    public WitchRetreatToCandleState(BossControllerBase boss, IWitchPatternStateBridge patternBridge) : base(boss)
    {
        this.patternBridge = patternBridge;
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

        AbilityLogic_WitchRetreatToCandle logic = reservedPattern.Ability != null
            ? reservedPattern.Ability.logic as AbilityLogic_WitchRetreatToCandle
            : null;

        if (patternBridge == null || logic == null || !patternBridge.TryBeginRetreatPattern(logic))
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
