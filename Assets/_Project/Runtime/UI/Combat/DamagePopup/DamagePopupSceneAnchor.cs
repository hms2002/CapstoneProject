using UnityEngine;

/// <summary>
/// 책임:
/// - 오래된 씬에 남아 있는 데미지 팝업 UI 앵커 직렬화 참조를 보존한다.
/// - 현재 DamagePopupService는 월드 팝업을 직접 생성하므로 런타임 등록 동작은 수행하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class DamagePopupSceneAnchor : MonoBehaviour
{
    [Header("Popup Root")]
    [SerializeField] private RectTransform popupRoot;

    [Header("World Camera")]
    [SerializeField] private Camera worldCamera;

    public RectTransform PopupRoot => popupRoot;
    public Camera WorldCamera => worldCamera != null ? worldCamera : Camera.main;

    private void Reset()
    {
        if (popupRoot == null)
            popupRoot = transform as RectTransform;
    }

    public bool IsValid()
    {
        return popupRoot != null && WorldCamera != null;
    }
}
