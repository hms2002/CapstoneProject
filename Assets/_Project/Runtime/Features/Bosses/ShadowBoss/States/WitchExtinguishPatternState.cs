using UnityEngine;
using UnityGAS.Sample;

public class WitchExtinguishPatternState : BossState
{
    // 이 클래스의 책임:
    // 마녀의 촛불 끄기 패턴 실행 구간을 관리하고, 패턴 시작/완료/취소를 브리지 계약을 통해 조율한다.

    private const float WarningTime = 1.2f;

    private readonly IWitchPatternStateBridge patternBridge;
    private bool activationRequested;
    private float explodeTime;
    private bool isWaiting;

    public WitchExtinguishPatternState(BossControllerBase boss, IWitchPatternStateBridge patternBridge) : base(boss)
    {
        this.patternBridge = patternBridge;
    }

    public override void OnEnter()
    {
        activationRequested = false;

        BossPatternEntry reservedPattern = boss.PatternRuntime.ReservedPattern;
        if (reservedPattern == null)
        {
            LogState("촛불 끄기 패턴 예약 정보가 없습니다.");
            boss.ChangeState(boss.GetCombatIdleState());
            return;
        }

        activationRequested = boss.TryStartPattern(reservedPattern);
        if (!activationRequested)
        {
            string reservedPatternName = reservedPattern.Ability != null ? reservedPattern.Ability.name : "None";
            LogState($"촛불 끄기 패턴 '{reservedPatternName}' 실행에 실패했습니다.");
            boss.PatternRuntime.ClearReservedPattern();
            boss.ChangeState(boss.GetCombatIdleState());
            return;
        }

        AbilityLogic_WitchExtinguishCandle logic = reservedPattern.Ability != null
            ? reservedPattern.Ability.logic as AbilityLogic_WitchExtinguishCandle
            : null;
        if (logic == null)
        {
            LogState("촛불 끄기 패턴 logic을 찾지 못했습니다.");
            boss.AbortCurrentPattern();
            boss.ChangeState(boss.GetCombatIdleState());
            return;
        }

        if (patternBridge == null || !patternBridge.TryBeginExtinguishPattern(logic, WarningTime, out float resolvedDuration))
        {
            LogState($"촛불 끄기 패턴을 시작하지 못했습니다. sealedCandles={((Witch)boss).GetSealedCandleCount()}");
            boss.AbortCurrentPattern();
            boss.ChangeState(boss.GetCombatIdleState());
            return;
        }

        isWaiting = true;
        explodeTime = Time.time + resolvedDuration;
        LogState("가장 가까운 촛대에 경고를 표시했습니다.");
    }

    public override void OnUpdate()
    {
        if (!activationRequested) return;

        if (!isWaiting) return;

        if (Time.time < explodeTime) return;

        patternBridge?.CompleteExtinguishPattern();
        boss.FinishCurrentPattern();
        isWaiting = false;
        LogState("촛불 끄기 패턴이 끝났습니다.");
        boss.ChangeState(boss.GetCombatIdleState());
    }

    public override void OnExit()
    {
        patternBridge?.CancelExtinguishPattern();
        boss.PatternRuntime.ClearReservedPattern();
        activationRequested = false;
        isWaiting = false;
    }
}
