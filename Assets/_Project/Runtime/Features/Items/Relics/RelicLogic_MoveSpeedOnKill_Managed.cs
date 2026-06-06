using UnityEngine;
using UnityGAS;
using System.Collections.Generic;

/// <summary>
/// 책임 : 적 처치 이벤트를 감지해 일정 시간 이동속도 버프를 부여하는 유물 로직이다.
/// 일반 장착에서는 proc를 등록하고, 복원 장착에서는 앞으로의 이벤트를 받을 runtime hook만 다시 연결한다.
/// </summary>
[CreateAssetMenu(menuName = "Game/Relic Logic/Move Speed On Kill (Managed)")]
public class RelicLogic_MoveSpeedOnKill_Managed : RelicLogic
{
    protected override string DefaultEffectTemplate => "● 적 처치 시 {duration} 동안 [[이동속도]] {move_speed_bonus}";

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

    [Header("Status HUD")]
    [Tooltip("이 유물 버프를 상태 HUD로 보여줄 때 사용할 표시 정의.")]
    public StatusHudDefinition statusDefinition;

    public override void OnEquipped(RelicContext ctx)
    {
        RegisterProc(ctx);
    }

    public override void OnUnequipped(RelicContext ctx)
    {
        if (ctx.owner == null || ctx.token == null) return;
        var mgr = ctx.owner.GetComponent<RelicProcManager>();
        if (mgr == null) return;

        mgr.UnregisterAll(ctx.token);
    }

    public override void OnRestoreAttached(RelicContext ctx)
    {
        RegisterProc(ctx);
    }

    private void RegisterProc(RelicContext ctx)
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
            refreshDuration,
            statusDefinition
        );

        mgr.Register(proc);
    }

    public override RelicTooltipData BuildTooltip(RelicDefinition definition, int previewLevel, ItemDetailContext ctx)
    {
        return BuildTemplatedTooltip(
            "● 적 처치 시 {duration} 동안 [[이동속도]] {move_speed_bonus}",
            new Dictionary<string, string>
            {
                ["duration"] = RelicTooltipFormatter.FormatSeconds(durationSeconds),
                ["move_speed_bonus"] = RelicTooltipFormatter.FormatSignedValueToken(percentBonus, true),
            });
    }
}
