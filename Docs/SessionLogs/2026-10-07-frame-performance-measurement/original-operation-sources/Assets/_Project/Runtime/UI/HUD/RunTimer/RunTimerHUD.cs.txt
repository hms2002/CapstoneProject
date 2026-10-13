using TMPro;
using UnityEngine;

/// <summary>
/// 책임 :
/// - RunTimeLimitSystem의 남은 시간을 사람이 읽기 쉬운 텍스트로 출력한다.
/// - 시간이 적을 때와 외부 pause 중일 때의 색상 변화, 표시/숨김만 담당하고 타이머 로직은 소유하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class RunTimerHUD : MonoBehaviour
{
    public static RunTimerHUD Instance { get; private set; }

    [Header("Binding")]
    [SerializeField] private RunTimeLimitSystem timeLimitSystem;
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private GameObject visibleRoot;

    [Header("Visual")]
    [SerializeField] private string timeFormat = "{0:00}:{1:00}";
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color lowTimeColor = new(1f, 0.35f, 0.25f, 1f);
    [SerializeField] private Color pausedColor = new(0.6f, 0.85f, 1f, 1f);
    [SerializeField] private Color outlineColor = Color.black;
    [SerializeField, Range(0f, 1f)] private float outlineWidth = 0.2f;
    [SerializeField] private bool hideWhenTimerInactive = true;
    [SerializeField] private bool pulseWhilePaused = true;
    [SerializeField] private float pausedPulseSpeed = 2.5f;
    [SerializeField, Range(0f, 1f)] private float pausedPulseMinAlpha = 0.55f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (visibleRoot == null)
            visibleRoot = gameObject;

        ApplyTextOutline();
    }

    private void OnEnable()
    {
        RunTimeLimitSystem.InstanceChanged += HandleTimeLimitSystemChanged;
        ApplyTextOutline();
        ResolveTimeLimitBinding();
    }

    private void OnValidate()
    {
        outlineWidth = Mathf.Clamp01(outlineWidth);
    }

    private void OnDisable()
    {
        RunTimeLimitSystem.InstanceChanged -= HandleTimeLimitSystemChanged;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (timeLimitSystem == null)
        {
            SetVisible(false);
            return;
        }

        if (!timeLimitSystem.IsRunning)
        {
            if (timeLimitSystem.IsExpired)
            {
                SetVisible(true);
                UpdateTimeText(0f, true, false);
                return;
            }

            SetVisible(!hideWhenTimerInactive);

            if (!hideWhenTimerInactive)
            {
                UpdateTimeText(
                    timeLimitSystem.InactivePreviewSeconds,
                    false,
                    true);
            }

            return;
        }

        SetVisible(true);
        UpdateTimeText(
            timeLimitSystem.RemainingSeconds,
            timeLimitSystem.IsLowTime,
            timeLimitSystem.IsVisuallyPaused);
    }

    /// <summary>
    /// 책임 :
    /// - 남은 시간을 mm:ss 형식으로 표시하고, 상태에 따라 우선순위 있는 색상 규칙을 적용한다.
    /// - 외부 pause와 스테이지 정책 정지를 같은 "멈춤 상태"로 표현해, 시간이 흐르지 않는 씬에서도 즉시 읽히게 한다.
    /// </summary>
    private void UpdateTimeText(float remainingSeconds, bool isLowTime, bool isPaused)
    {
        if (timeText == null)
            return;

        float safeSeconds = Mathf.Max(0f, remainingSeconds);
        int totalSeconds = Mathf.CeilToInt(safeSeconds);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        timeText.text = string.Format(timeFormat, minutes, seconds);
        Color targetColor = isPaused
            ? pausedColor
            : (isLowTime ? lowTimeColor : normalColor);

        if (isPaused && pulseWhilePaused)
        {
            float pulse = Mathf.Lerp(
                pausedPulseMinAlpha,
                1f,
                Mathf.PingPong(Time.unscaledTime * pausedPulseSpeed, 1f));

            targetColor.a *= pulse;
        }

        timeText.color = targetColor;
    }

    private void ApplyTextOutline()
    {
        if (timeText == null)
            return;

        timeText.outlineColor = outlineColor;
        timeText.outlineWidth = outlineWidth;
    }

    private void SetVisible(bool isVisible)
    {
        if (visibleRoot != null && visibleRoot.activeSelf != isVisible)
            visibleRoot.SetActive(isVisible);
    }

    public void BindTimeLimitSystem(RunTimeLimitSystem system)
    {
        timeLimitSystem = system;
    }

    public void UnbindTimeLimitSystem(RunTimeLimitSystem system)
    {
        if (system == null || timeLimitSystem != system)
            return;

        timeLimitSystem = null;
    }

    private void HandleTimeLimitSystemChanged(RunTimeLimitSystem system)
    {
        if (system == null)
        {
            UnbindTimeLimitSystem(timeLimitSystem);
            return;
        }

        BindTimeLimitSystem(system);
    }

    private void ResolveTimeLimitBinding()
    {
        if (timeLimitSystem != null)
            return;

        if (RunTimeLimitSystem.Instance != null)
        {
            BindTimeLimitSystem(RunTimeLimitSystem.Instance);
            return;
        }

        if (ShouldUseAutoFindFallback())
            timeLimitSystem = FindAnyObjectByType<RunTimeLimitSystem>();
    }

    private static bool ShouldUseAutoFindFallback()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        return true;
#else
        return false;
#endif
    }
}
