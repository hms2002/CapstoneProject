using UnityEngine;

public class WitchExtinguishState : BossState
{
    private const float WarningTime = 1.2f;

    private readonly Witch witch;
    private float explodeTime;
    private bool isWaiting;

    public WitchExtinguishState(Witch witch) : base(witch)
    {
        this.witch = witch;
    }

    public override void OnEnter()
    {
        if (!witch.StartExtinguish(WarningTime))
        {
            LogState("촛불 끄기 패턴을 시작하지 못했습니다.");
            boss.ChangeState(boss.GetCombatIdleState());
            return;
        }

        isWaiting = true;
        explodeTime = Time.time + WarningTime;
        LogState("가장 가까운 촛대에 경고를 표시했습니다.");
    }

    public override void OnUpdate()
    {
        if (!isWaiting) return;

        if (Time.time < explodeTime) return;

        witch.FinishExtinguish();
        isWaiting = false;
        LogState("촛불 끄기 패턴이 끝났습니다.");
        boss.ChangeState(boss.GetCombatIdleState());
    }

    public override void OnExit()
    {
        witch.HideExtinguishWarning();
        isWaiting = false;
    }
}
