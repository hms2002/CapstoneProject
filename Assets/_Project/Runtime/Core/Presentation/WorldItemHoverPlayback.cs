using UnityEngine;

/// <summary>
/// 책임 : Gameplay 계층이 구체 hover UI 구현 없이 월드 아이템 상세 표시/숨김을 요청하게 하는 backend 계약이다.
/// </summary>
public interface IWorldItemHoverBackend
{
    void ShowWorldItemDetail(Transform worldAnchor, ScriptableObject itemDefinition, int relicLevelOverride);
    void HideWorldItemDetail(Transform worldAnchor);
}

/// <summary>
/// 책임 : 월드 드롭/상점 슬롯이 UI 구현 타입을 참조하지 않고 아이템 상세 hover 요청을 전달하게 한다.
/// </summary>
public static class WorldItemHoverPlayback
{
    private static readonly object SkillInputBlockOwner = new();
    private static IWorldItemHoverBackend backend;
    private static Transform activeWorldAnchor;

    public static bool IsShowing => activeWorldAnchor != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        backend = null;
        activeWorldAnchor = null;
        SetSkillPressBlocked(false);
    }

    public static void RegisterBackend(IWorldItemHoverBackend hoverBackend)
    {
        backend = hoverBackend;
        if (backend == null)
            ClearActiveState();
    }

    public static void Show(Transform worldAnchor, ScriptableObject itemDefinition, int relicLevelOverride = 0)
    {
        if (worldAnchor == null || itemDefinition == null)
        {
            Hide(worldAnchor);
            return;
        }

        activeWorldAnchor = worldAnchor;
        SetSkillPressBlocked(true);
        backend?.ShowWorldItemDetail(worldAnchor, itemDefinition, relicLevelOverride);
    }

    public static void Hide(Transform worldAnchor = null)
    {
        if (worldAnchor != null && activeWorldAnchor != worldAnchor)
            return;

        ClearActiveState();
        backend?.HideWorldItemDetail(worldAnchor);
    }

    private static void ClearActiveState()
    {
        activeWorldAnchor = null;
        SetSkillPressBlocked(false);
    }

    private static void SetSkillPressBlocked(bool blocked)
    {
        InputActionQuery.SetPressBlocked(InputActionId.Skill1, SkillInputBlockOwner, blocked);
        InputActionQuery.SetPressBlocked(InputActionId.Skill2, SkillInputBlockOwner, blocked);
    }
}
