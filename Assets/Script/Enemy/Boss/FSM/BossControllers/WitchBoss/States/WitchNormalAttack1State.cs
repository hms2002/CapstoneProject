using UnityEngine;

public class WitchNormalAttack1State : BossState
{
    private readonly Witch witch;
    private bool activationRequested;
    private float endTime;
    private bool isWaiting;

    public WitchNormalAttack1State(Witch witch) : base(witch)
    {
        this.witch = witch;
    }

    public override void OnEnter()
    {
        activationRequested = false;

        BossPatternEntry reservedPattern = boss.PatternRuntime.ReservedPattern;
        if (reservedPattern == null)
        {
            LogState("평타1 예약 정보가 없습니다.");
            boss.ChangeState(boss.GetCombatIdleState());
            return;
        }

        activationRequested = boss.TryStartPattern(reservedPattern);
        if (!activationRequested)
        {
            string reservedPatternName = reservedPattern.Ability != null ? reservedPattern.Ability.name : "None";
            LogState($"평타1 패턴 '{reservedPatternName}' 실행에 실패했습니다.");
            boss.PatternRuntime.ClearReservedPattern();
            boss.ChangeState(boss.GetCombatIdleState());
            return;
        }

        if (!witch.StartNormal1())
        {
            LogState("평타1 패턴을 시작하지 못했습니다.");
            boss.AbortCurrentPattern();
            boss.ChangeState(boss.GetCombatIdleState());
            return;
        }

        isWaiting = true;
        endTime = Time.time + witch.GetNormal1Time();
        LogState("평타1 장판 공격을 시작했습니다.");
    }

    public override void OnUpdate()
    {
        if (!activationRequested) return;
        if (!isWaiting) return;
        if (Time.time < endTime) return;

        boss.FinishCurrentPattern();
        isWaiting = false;
        LogState("평타1 패턴이 끝났습니다.");
        boss.ChangeState(boss.GetCombatIdleState());
    }

    public override void OnExit()
    {
        boss.PatternRuntime.ClearReservedPattern();
        activationRequested = false;
        isWaiting = false;
    }
}
