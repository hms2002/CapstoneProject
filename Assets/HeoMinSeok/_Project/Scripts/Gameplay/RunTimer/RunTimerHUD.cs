using TMPro;
using UnityEngine;

/// <summary>
/// 책임 :
/// - RunTimeLimitSystem의 남은 시간을 사람이 읽기 쉬운 텍스트로 출력한다.
/// - 시간이 적을 때의 색상 변화와 표시/숨김만 담당하고, 타이머 로직은 소유하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class RunTimerHUD : MonoBehaviour
{
    [Header("Binding")]
    [SerializeField] private RunTimeLimitSystem timeLimitSystem;
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private GameObject visibleRoot;

    [Header("Visual")]
    [SerializeField] private string timeFormat = "{0:00}:{1:00}";
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color lowTimeColor = new(1f, 0.35f, 0.25f, 1f);

    private void Awake()
    {
        if (timeLimitSystem == null)
            timeLimitSystem = FindAnyObjectByType<RunTimeLimitSystem>();

        if (visibleRoot == null)
            visibleRoot = gameObject;
    }

    private void Update()
    {
        if (timeLimitSystem == null || !timeLimitSystem.IsRunning)
        {
            SetVisible(false);
            return;
        }

        SetVisible(true);
        UpdateTimeText(timeLimitSystem.RemainingSeconds, timeLimitSystem.IsLowTime);
    }

    private void UpdateTimeText(float remainingSeconds, bool isLowTime)
    {
        if (timeText == null)
            return;

        float safeSeconds = Mathf.Max(0f, remainingSeconds);
        int totalSeconds = Mathf.CeilToInt(safeSeconds);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        timeText.text = string.Format(timeFormat, minutes, seconds);
        timeText.color = isLowTime ? lowTimeColor : normalColor;
    }

    private void SetVisible(bool isVisible)
    {
        if (visibleRoot != null && visibleRoot.activeSelf != isVisible)
            visibleRoot.SetActive(isVisible);
    }
}
