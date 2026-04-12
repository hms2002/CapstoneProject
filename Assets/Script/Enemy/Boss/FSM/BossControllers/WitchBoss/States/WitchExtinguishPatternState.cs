using UnityEngine;

public class WitchExtinguishPatternState : BossState
{
    private const float WarningTime = 1.2f;

    private readonly Witch witch;
    private bool activationRequested;
    private float explodeTime;
    private bool isWaiting;

    public WitchExtinguishPatternState(Witch witch) : base(witch)
    {
        this.witch = witch;
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

        if (!witch.StartExtinguish(WarningTime))
        {
            LogState("촛불 끄기 패턴을 시작하지 못했습니다.");
            boss.AbortCurrentPattern();
            boss.ChangeState(boss.GetCombatIdleState());
            return;
        }

        isWaiting = true;
        explodeTime = Time.time + WarningTime;
        LogState("가장 가까운 촛대에 경고를 표시했습니다.");
    }

    public override void OnUpdate()
    {
        if (!activationRequested) return;

        if (!isWaiting) return;

        if (Time.time < explodeTime) return;

        witch.FinishExtinguish();
        boss.FinishCurrentPattern();
        isWaiting = false;
        LogState("촛불 끄기 패턴이 끝났습니다.");
        boss.ChangeState(boss.GetCombatIdleState());
    }

    public override void OnExit()
    {
        witch.HideExtinguishWarning();
        boss.PatternRuntime.ClearReservedPattern();
        activationRequested = false;
        isWaiting = false;
    }
}
