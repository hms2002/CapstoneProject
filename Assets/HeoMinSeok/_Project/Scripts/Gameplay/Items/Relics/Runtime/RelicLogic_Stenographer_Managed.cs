using UnityEngine;
using UnityGAS;
using System.Collections.Generic;

/// <summary>
/// 책임 :
/// - 속기사 유물이 장착된 동안 플레이어에게 전용 상태 태그를 부여한다.
/// - 잔영의 날개 Skill1 Rush가 이 태그를 읽어 런타임 stack 증가량을 변형할 수 있게 만든다.
/// </summary>
[CreateAssetMenu(menuName = "Game/Relic Logic/Stenographer (Managed)")]
public class RelicLogic_Stenographer_Managed : RelicLogic
{
    protected override string DefaultEffectTemplate => "● [[잔영의 날개]] 전용 유물\n● [[스킬 1]] 변동\n● 사용 시 [[이동속도]] {stage_0_bonus}\n● 3초 후 추가 [[이동속도]] {stage_1_bonus} (총 {stage_1_total})\n● 6초 후 추가 [[이동속도]] {stage_2_bonus} (총 {stage_2_total})";

    [Tooltip("속기사 장착 상태를 나타내는 태그.")]
    public GameplayTag grantedTag;

    public override void OnEquipped(RelicContext ctx)
    {
        ApplyGrantedTag(ctx, add: true);
    }

    public override void OnUnequipped(RelicContext ctx)
    {
        ApplyGrantedTag(ctx, add: false);
    }

    public override void OnRestoreAttached(RelicContext ctx)
    {
        ApplyGrantedTag(ctx, add: true);
    }

    public override void OnRestoreDetached(RelicContext ctx)
    {
        ApplyGrantedTag(ctx, add: false);
    }

    /// <summary>
    /// 책임 :
    /// - 유물 장착/해제/복원 생명주기에 맞춰 전용 태그를 증감한다.
    /// - 스킬 로직은 이 태그만 조회하고, 유물 자체는 이동속도 수치 계산 책임을 갖지 않는다.
    /// </summary>
    private void ApplyGrantedTag(RelicContext ctx, bool add)
    {
        if (ctx.tagSystem == null || grantedTag == null)
            return;

        if (add)
            ctx.tagSystem.AddTag(grantedTag);
        else
            ctx.tagSystem.RemoveTag(grantedTag);
    }

    public override RelicTooltipData BuildTooltip(RelicDefinition definition, int previewLevel, ItemDetailContext ctx)
    {
        return BuildTemplatedTooltip(
            "● [[잔영의 날개]] 전용 유물\n● [[스킬 1]] 변동\n● 사용 시 [[이동속도]] {stage_0_bonus}\n● 3초 후 추가 [[이동속도]] {stage_1_bonus} (총 {stage_1_total})\n● 6초 후 추가 [[이동속도]] {stage_2_bonus} (총 {stage_2_total})",
            new Dictionary<string, string>
            {
                ["stage_0_bonus"] = "{pos:[+150%]}",
                ["stage_1_bonus"] = "{pos:[+150%]}",
                ["stage_1_total"] = "{pos:[+300%]}",
                ["stage_2_bonus"] = "{pos:[+200%]}",
                ["stage_2_total"] = "{pos:[+500%]}",
            });
    }
}
