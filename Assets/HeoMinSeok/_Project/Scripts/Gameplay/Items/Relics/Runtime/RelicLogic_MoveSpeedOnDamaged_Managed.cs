using UnityEngine;
using UnityGAS;

/// <summary>
/// [피해를 받으면] 일정 시간 이동속도(%) 버프를 부여하는 유물 로직.
/// - 기본은 PlayerTokenHealth.OnTokenDamaged(토큰 체력) 이벤트를 구독합니다.
/// - 토큰 체력이 없는 프로젝트라면, (옵션) healthAttributeFallback을 지정하고 AttributeSet에서 체력 감소를 감지할 수도 있습니다.
/// - RelicProcManager를 통해 생명주기(Dispose)를 관리합니다.
/// </summary>
[CreateAssetMenu(menuName = "Game/Relic Logic/Move Speed On Damaged (Managed)")]
public class RelicLogic_MoveSpeedOnDamaged_Managed : RelicLogic
{
    [Header("Trigger (Optional)")]
    [Tooltip("토큰 체력이 없는 경우, 이 Attribute가 감소할 때 트리거로 사용합니다(예: Health). 비워도 됩니다.")]
    public AttributeDefinition healthAttributeFallback;

    [Header("Buff")]
    [Tooltip("이동속도(%)를 담당하는 AttributeDefinition. (예: MoveSpeedMultiplier 혹은 MoveSpeed)")]
    public AttributeDefinition moveSpeedAttribute;

    [Tooltip("증가량(Percent modifier). 0.50 = +50%")]
    public float percentBonus = 0.50f;

    [Tooltip("버프 지속시간(초)")]
    public float durationSeconds = 15f;

    [Tooltip("true면 피격 시 기존 버프를 제거하고 남은시간을 갱신합니다. false면 중첩됩니다(여러 개가 곱해짐).")]
    public bool refreshDuration = true;

    public override void OnEquipped(RelicContext ctx)
    {
        if (ctx.owner == null || ctx.token == null) return;
        if (moveSpeedAttribute == null) return;

        var mgr = ctx.owner.GetComponent<RelicProcManager>();
        if (mgr == null) mgr = ctx.owner.AddComponent<RelicProcManager>();

        var proc = new MoveSpeedOnDamagedProc(
            ctx,
            moveSpeedAttribute,
            percentBonus,
            durationSeconds,
            refreshDuration,
            healthAttributeFallback
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
