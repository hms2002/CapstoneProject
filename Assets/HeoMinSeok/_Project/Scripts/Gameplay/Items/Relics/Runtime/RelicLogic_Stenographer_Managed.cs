using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 속기사 유물이 장착된 동안 플레이어에게 전용 상태 태그를 부여한다.
/// - 잔영의 날개 Skill1 Rush가 이 태그를 읽어 런타임 stack 증가량을 변형할 수 있게 만든다.
/// </summary>
[CreateAssetMenu(menuName = "Game/Relic Logic/Stenographer (Managed)")]
public class RelicLogic_Stenographer_Managed : RelicLogic
{
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
}
