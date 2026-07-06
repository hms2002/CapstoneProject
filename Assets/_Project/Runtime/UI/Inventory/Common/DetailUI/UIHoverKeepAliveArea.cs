using UnityEngine;

/// <summary>
/// 책임:
/// - 오래된 인벤토리 툴팁 keep-alive 영역 컴포넌트의 직렬화 참조를 보존한다.
/// - 현재 DetailUI 구조에서는 중앙 HoverManager가 없으므로 영역 RectTransform만 노출하는 호환 셸로 동작한다.
/// </summary>
public sealed class UIHoverKeepAliveArea : MonoBehaviour
{
    [SerializeField] private RectTransform rect;

    public RectTransform RectTransform => rect != null ? rect : transform as RectTransform;

    private void Reset()
    {
        if (rect == null)
            rect = transform as RectTransform;
    }

    public bool IsValid()
    {
        return RectTransform != null;
    }
}
