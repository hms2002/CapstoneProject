using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 한방울의 신속 유물이 장착된 동안 플레이어에게 전용 상태 태그를 부여한다.
/// - 잔영의 날개 Skill1 Rush가 이 태그를 읽어 Skill2 킬 확인 취소 유예 규칙을 적용하게 만든다.
/// </summary>
[CreateAssetMenu(menuName = "Game/Relic Logic/One Drop Of Swiftness (Managed)")]
public class RelicLogic_OneDropOfSwiftness_Managed : RelicLogic
{
    [Tooltip("한방울의 신속 장착 상태를 나타내는 태그.")]
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
    /// - 유물 생명주기에 맞춰 전용 태그를 증감한다.
    /// - 실제 Skill1 취소 보류 규칙은 Rush 로직이 맡고, 유물은 상태 표식만 제공한다.
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
}
