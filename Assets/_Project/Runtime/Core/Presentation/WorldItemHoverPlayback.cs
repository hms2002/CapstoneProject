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
    private static IWorldItemHoverBackend backend;

    public static void RegisterBackend(IWorldItemHoverBackend hoverBackend)
    {
        backend = hoverBackend;
    }

    public static void Show(Transform worldAnchor, ScriptableObject itemDefinition, int relicLevelOverride = 0)
    {
        backend?.ShowWorldItemDetail(worldAnchor, itemDefinition, relicLevelOverride);
    }

    public static void Hide(Transform worldAnchor = null)
    {
        backend?.HideWorldItemDetail(worldAnchor);
    }
}
