using UnityEngine;

/// <summary>
/// 책임 : 현재 씬에서 데미지 팝업이 생성될 UI 루트와 월드 카메라를 선언하고,
/// 활성화/비활성화 시점에 DamagePopupService에 자신을 등록/해제한다.
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

    private void OnEnable()
    {
        if (DamagePopupService.Instance != null)
            DamagePopupService.Instance.RegisterAnchor(this);
    }

    private void Start()
    {
        // 서비스가 자기보다 늦게 생성되는 경우를 한 번 더 보정
        if (DamagePopupService.Instance != null)
            DamagePopupService.Instance.RegisterAnchor(this);
    }

    private void OnDisable()
    {
        if (DamagePopupService.Instance != null)
            DamagePopupService.Instance.UnregisterAnchor(this);
    }

    /// <summary>
    /// 책임 : 팝업 생성 컨텍스트로 사용 가능한 최소 조건을 검사한다.
    /// </summary>
    public bool IsValid()
    {
        return popupRoot != null && WorldCamera != null;
    }
}