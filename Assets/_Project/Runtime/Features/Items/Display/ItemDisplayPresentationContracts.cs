using UnityEngine;

/// <summary>
/// 책임 : 아이템 드롭/상점 gameplay가 concrete 아이템 표시 구현 없이 월드 표시 상태를 갱신하게 하는 계약이다.
/// </summary>
public interface IItemDisplayVisualPresenter
{
    SpriteRenderer FallbackRenderer { get; }

    void Apply(ScriptableObject item);
    void SetOutline(bool enabled);
    void ClearVisual();
    bool TryResolveVisualBoundsWorld(out Bounds bounds);
}
