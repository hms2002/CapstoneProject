using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 책임 :
/// - 오케스트레이션 계층이 전달하는 보스 체력 비율을 UI 슬라이더 2개로 표시한다.
/// - 즉시 체력바와 지연 체력바를 분리해, 감소 잔상 연출이 일정 시간 뒤 부드럽게 따라오도록 관리한다.
/// - 체력 감소 중 추가 피격이 오면 잔상 시작 타이머를 리셋해, 큰 피해가 누적되는 체감을 유지한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BossHealthBarUI : MonoBehaviour
{
    [Header("Sliders")]
    [Tooltip("실제 보스 체력을 즉시 표시하는 앞쪽 슬라이더입니다.")]
    [SerializeField] private Slider immediateHealthSlider;

    [Tooltip("보스 체력 감소 잔상을 표시하는 뒤쪽 슬라이더입니다.")]
    [SerializeField] private Slider delayedHealthSlider;

    [Header("Timing")]
    [Tooltip("체력 감소 후 잔상 슬라이더가 내려오기 전 대기 시간입니다.")]
    [SerializeField] private float delayedStartDelay = 0.5f;

    [Tooltip("잔상 슬라이더가 실제 체력 슬라이더를 따라오는 부드러움 시간입니다.")]
    [SerializeField] private float delayedSmoothTime = 0.2f;

    [Header("Split Health Presentation")]
    [Tooltip("분리형 보스 체력 표시 루트입니다. 비워두면 런타임에 중앙 분리선만 자동 생성합니다.")]
    [SerializeField] private RectTransform splitHealthRoot;

    [Tooltip("분리형 보스 체력바의 중앙 구분선 이미지입니다.")]
    [SerializeField] private Image splitDividerImage;

    [Tooltip("분리형 보스 체력바 왼쪽 라벨입니다. 선택 사항입니다.")]
    [SerializeField] private TMP_Text splitLeftLabelText;

    [Tooltip("분리형 보스 체력바 오른쪽 라벨입니다. 선택 사항입니다.")]
    [SerializeField] private TMP_Text splitRightLabelText;

    [Tooltip("분리형 보스 표시용 오브젝트가 없을 때 중앙 분리선을 자동 생성합니다.")]
    [SerializeField] private bool createFallbackSplitHealthPresentation = true;

    [Tooltip("자동 생성 중앙 분리선 색상입니다.")]
    [SerializeField] private Color fallbackSplitDividerColor = new Color(1f, 1f, 1f, 0.9f);

    [Tooltip("자동 생성 중앙 분리선의 너비입니다.")]
    [SerializeField, Min(1f)] private float fallbackSplitDividerWidth = 4f;

    [Tooltip("자동 생성 중앙 분리선의 높이입니다.")]
    [SerializeField, Min(1f)] private float fallbackSplitDividerHeight = 36f;

    private float targetHealthRatio = 1f;
    private float previousHealthRatio = 1f;
    private float lastDamageTime = float.NegativeInfinity;
    private float delayedVelocity;
    private bool initialized;

    private void Awake()
    {
        ApplyImmediate(1f);
        ApplyDelayedImmediate(1f);
    }

    private void Update()
    {
        if (!initialized)
            return;

        float currentRatio = targetHealthRatio;
        float immediateValue = immediateHealthSlider != null ? immediateHealthSlider.value : currentRatio;

        if (currentRatio > immediateValue)
        {
            ApplyImmediate(currentRatio);
            ApplyDelayedImmediate(currentRatio);
            previousHealthRatio = currentRatio;
            return;
        }

        if (currentRatio < previousHealthRatio)
            lastDamageTime = Time.unscaledTime;

        ApplyImmediate(currentRatio);

        if (delayedHealthSlider != null)
        {
            if (currentRatio >= delayedHealthSlider.value)
            {
                ApplyDelayedImmediate(currentRatio);
            }
            else if (Time.unscaledTime - lastDamageTime >= delayedStartDelay)
            {
                float next = Mathf.SmoothDamp(
                    delayedHealthSlider.value,
                    currentRatio,
                    ref delayedVelocity,
                    Mathf.Max(0.01f, delayedSmoothTime),
                    Mathf.Infinity,
                    Time.unscaledDeltaTime);

                delayedHealthSlider.value = next;
            }
        }

        previousHealthRatio = currentRatio;
    }

    /// <summary>
    /// 책임 :
    /// - 보스 HUD가 처음 활성화되거나 새 보스에 바인딩될 때 두 슬라이더를 같은 값으로 동기화한다.
    /// - 초기 프레임에 체력 잔상이 남지 않게 시작 상태를 안정적으로 맞춘다.
    /// </summary>
    public void ResetToRatio(float ratio)
    {
        float initialRatio = Mathf.Clamp01(ratio);
        targetHealthRatio = initialRatio;
        ApplyImmediate(initialRatio);
        ApplyDelayedImmediate(initialRatio);
        previousHealthRatio = initialRatio;
        lastDamageTime = float.NegativeInfinity;
        delayedVelocity = 0f;
        initialized = true;
    }

    /// <summary>
    /// 책임 :
    /// - 외부 HUD 컨트롤러가 계산한 최신 보스 체력 비율을 받아 다음 프레임 UI 갱신 목표로 저장한다.
    /// - 실제 체력 감소 판정과 잔상 타이머 관리는 뷰 내부 규칙으로 일관되게 유지한다.
    /// </summary>
    public void SetHealthRatio(float ratio)
    {
        float nextRatio = Mathf.Clamp01(ratio);
        if (!initialized)
        {
            ResetToRatio(nextRatio);
            return;
        }

        targetHealthRatio = nextRatio;
    }

    /// <summary>분리형 보스 체력 표시 상태와 라벨을 갱신합니다.</summary>
    public void SetSplitHealthPresentation(bool visible, string leftLabel, string rightLabel)
    {
        if (visible && splitHealthRoot == null && splitDividerImage == null && createFallbackSplitHealthPresentation)
            CreateFallbackSplitHealthPresentation();

        ApplySplitPresentationVisible(visible);
        ApplySplitPresentationLabels(leftLabel, rightLabel);
    }

    private void ApplyImmediate(float ratio)
    {
        if (immediateHealthSlider == null)
            return;

        immediateHealthSlider.minValue = 0f;
        immediateHealthSlider.maxValue = 1f;
        immediateHealthSlider.value = ratio;
    }

    private void ApplyDelayedImmediate(float ratio)
    {
        if (delayedHealthSlider == null)
            return;

        delayedHealthSlider.minValue = 0f;
        delayedHealthSlider.maxValue = 1f;
        delayedHealthSlider.value = ratio;
        delayedVelocity = 0f;
    }

    /// <summary>분리형 보스 체력 표시 오브젝트 활성 상태를 반영합니다.</summary>
    private void ApplySplitPresentationVisible(bool visible)
    {
        if (splitHealthRoot != null && splitHealthRoot.gameObject.activeSelf != visible)
            splitHealthRoot.gameObject.SetActive(visible);

        if (splitHealthRoot == null && splitDividerImage != null && splitDividerImage.gameObject.activeSelf != visible)
            splitDividerImage.gameObject.SetActive(visible);
    }

    /// <summary>분리형 보스 체력 표시 라벨을 반영합니다.</summary>
    private void ApplySplitPresentationLabels(string leftLabel, string rightLabel)
    {
        if (splitLeftLabelText != null)
            splitLeftLabelText.text = string.IsNullOrWhiteSpace(leftLabel) ? string.Empty : leftLabel;

        if (splitRightLabelText != null)
            splitRightLabelText.text = string.IsNullOrWhiteSpace(rightLabel) ? string.Empty : rightLabel;
    }

    /// <summary>인스펙터 참조가 없을 때 최소 중앙 분리선을 생성합니다.</summary>
    private void CreateFallbackSplitHealthPresentation()
    {
        RectTransform parentRect = transform as RectTransform;
        if (parentRect == null)
            return;

        GameObject rootObject = new GameObject("SplitHealthPresentation", typeof(RectTransform));
        splitHealthRoot = rootObject.GetComponent<RectTransform>();
        splitHealthRoot.SetParent(parentRect, false);
        splitHealthRoot.anchorMin = Vector2.zero;
        splitHealthRoot.anchorMax = Vector2.one;
        splitHealthRoot.anchoredPosition = Vector2.zero;
        splitHealthRoot.sizeDelta = Vector2.zero;
        splitHealthRoot.pivot = new Vector2(0.5f, 0.5f);

        GameObject dividerObject = new GameObject("CenterDivider", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform dividerRect = dividerObject.GetComponent<RectTransform>();
        dividerRect.SetParent(splitHealthRoot, false);
        dividerRect.anchorMin = new Vector2(0.5f, 0.5f);
        dividerRect.anchorMax = new Vector2(0.5f, 0.5f);
        dividerRect.anchoredPosition = Vector2.zero;
        dividerRect.sizeDelta = new Vector2(fallbackSplitDividerWidth, fallbackSplitDividerHeight);
        dividerRect.pivot = new Vector2(0.5f, 0.5f);

        splitDividerImage = dividerObject.GetComponent<Image>();
        splitDividerImage.color = fallbackSplitDividerColor;
        splitDividerImage.raycastTarget = false;
    }
}
