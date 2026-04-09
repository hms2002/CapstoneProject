using UnityEngine;

/// <summary>
/// 책임 :
/// - 경고 팝업 표시 요청을 현재 연결된 WarningPopupUI에 즉시 전달한다.
/// - 최신 경고 요청이 이전 표시를 자연스럽게 덮어쓰도록 공통 진입점을 제공한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class WarningPopupService : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private WarningPopupUI currentView;

    private void Awake()
    {
        if (currentView != null)
        {
            currentView.BindService(this);
            currentView.HideImmediate();
        }
    }

    /// <summary>
    /// 책임 :
     /// - 외부 호출이 사용할 공통 경고 메시지 표시 API다.
    /// - 이미 표시 중인 경고가 있더라도 최신 요청으로 즉시 덮어써서 짧은 경고 피드백을 최신 상태로 유지한다.
    /// </summary>
    public void ShowWarning(string message, float duration = WarningPopupUI.DefaultDuration)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        if (currentView == null)
        {
            Debug.LogWarning("[WarningPopupService] WarningPopupUI view is not bound yet.", this);
            return;
        }

        currentView.ShowWarning(message, duration);
    }
}
