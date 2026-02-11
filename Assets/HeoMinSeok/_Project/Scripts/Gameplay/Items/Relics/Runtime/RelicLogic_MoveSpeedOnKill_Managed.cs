using UnityEngine;
using UnityGAS;

/// <summary>
/// [적 처치 시] 이동속도(%)를 일정 시간 부여하는 유물 로직.
/// - AbilitySystem이 KillConfirmedTag로 GameplayEvent를 발행해야 동작합니다.
/// - RelicProcManager를 통해 AbilitySystem.OnGameplayEvent를 수신합니다.
/// </summary>
[CreateAssetMenu(menuName = "Game/Relic Logic/Move Speed On Kill (Managed)")]
public class RelicLogic_MoveSpeedOnKill_Managed : RelicLogic
{
    [Header("Trigger")]
    [Tooltip("킬 확정 이벤트 태그. 보통 AbilitySystem.killConfirmedTag에 설정한 태그(Event.KillConfirmed).")]
    public GameplayTag triggerTag;

    [Header("Buff")]
    [Tooltip("이동속도(%)를 담당하는 AttributeDefinition. (예: MoveSpeedMultiplier 혹은 MoveSpeed)")]
    public AttributeDefinition moveSpeedAttribute;

    [Tooltip("증가량(Percent modifier). 0.18 = +18%")]
    public float percentBonus = 0.18f;

    [Tooltip("버프 지속시간(초)")]
    public float durationSeconds = 4f;

    [Tooltip("true면 처치 시 기존 버프를 제거하고 남은시간을 갱신합니다. false면 중첩됩니다(여러 개가 곱해짐).")]
    public bool refreshDuration = true;

    public override void OnEquipped(RelicContext ctx)
    {
        if (ctx.owner == null || ctx.token == null) return;
        if (moveSpeedAttribute == null) return;

        var mgr = ctx.owner.GetComponent<RelicProcManager>();
        if (mgr == null) mgr = ctx.owner.AddComponent<RelicProcManager>();

        var proc = new MoveSpeedOnKillProc(
            ctx,
            triggerTag,
            moveSpeedAttribute,
            percentBonus,
            durationSeconds,
            refreshDuration
        );

        mgr.Register(proc);
    }

    public override void OnUnequipped(RelicContext ctx)
    {
        if (ctx.owner == null || ctx.token == null) return;
        var mgr = ctx.owner.GetComponent<RelicProcManager>();
        if (mgr == null) return;

        mgr.UnregisterAll(ctx.token);
    }
}
