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

    [Header("Dual Boss Health")]
    [Tooltip("2체 보스 체력바 루트입니다. 비어 있으면 기존 슬라이더를 복제해 fallback UI를 만듭니다.")]
    [SerializeField] private RectTransform dualHealthRoot;

    [Tooltip("왼쪽 보스의 즉시 체력 슬라이더입니다.")]
    [SerializeField] private Slider leftImmediateHealthSlider;

    [Tooltip("왼쪽 보스의 지연 체력 슬라이더입니다.")]
    [SerializeField] private Slider leftDelayedHealthSlider;

    [Tooltip("오른쪽 보스의 즉시 체력 슬라이더입니다.")]
    [SerializeField] private Slider rightImmediateHealthSlider;

    [Tooltip("오른쪽 보스의 지연 체력 슬라이더입니다.")]
    [SerializeField] private Slider rightDelayedHealthSlider;

    [Tooltip("좌우 체력바 사이 간격입니다.")]
    [SerializeField, Min(0f)] private float dualHealthGap = 10f;

    [Tooltip("전용 체력바 참조가 없을 때 기존 슬라이더를 복제해 fallback UI를 생성합니다.")]
    [SerializeField] private bool createFallbackDualHealthPresentation = true;

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
    private float leftTargetHealthRatio = 1f;
    private float rightTargetHealthRatio = 1f;
    private float leftPreviousHealthRatio = 1f;
    private float rightPreviousHealthRatio = 1f;
    private float leftLastDamageTime = float.NegativeInfinity;
    private float rightLastDamageTime = float.NegativeInfinity;
    private float leftDelayedVelocity;
    private float rightDelayedVelocity;
    private bool isDualHealthMode;
    private bool initializedDualHealth;
    private RectTransform fallbackLeftHealthRoot;
    private RectTransform fallbackRightHealthRoot;

    private void Awake()
    {
        ApplyImmediate(1f);
        ApplyDelayedImmediate(1f);
        ApplyDualHealthVisible(false);
    }

    private void Update()
    {
        if (isDualHealthMode)
        {
            UpdateDualHealthBars();
            return;
        }

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
        if (isDualHealthMode)
            SetDualHealthRatios(false, 0f, 0f);

        float nextRatio = Mathf.Clamp01(ratio);
        if (!initialized)
        {
            ResetToRatio(nextRatio);
            return;
        }

        targetHealthRatio = nextRatio;
    }

    /// <summary>2체 보스 체력바 표시 여부와 좌우 체력 비율을 갱신합니다.</summary>
    public void SetDualHealthRatios(bool visible, float leftRatio, float rightRatio)
    {
        if (!visible)
        {
            isDualHealthMode = false;
            initializedDualHealth = false;
            ApplySingleHealthVisible(true);
            ApplyDualHealthVisible(false);
            return;
        }

        if (!EnsureDualHealthPresentation())
            return;

        float nextLeftRatio = Mathf.Clamp01(leftRatio);
        float nextRightRatio = Mathf.Clamp01(rightRatio);

        isDualHealthMode = true;
        ApplySplitPresentationVisible(false);
        ApplySingleHealthVisible(false);
        ApplyDualHealthVisible(true);

        if (!initializedDualHealth)
        {
            ResetDualHealthRatios(nextLeftRatio, nextRightRatio);
            return;
        }

        if (nextLeftRatio < leftPreviousHealthRatio)
            leftLastDamageTime = Time.unscaledTime;

        if (nextRightRatio < rightPreviousHealthRatio)
            rightLastDamageTime = Time.unscaledTime;

        leftPreviousHealthRatio = nextLeftRatio;
        rightPreviousHealthRatio = nextRightRatio;
        leftTargetHealthRatio = nextLeftRatio;
        rightTargetHealthRatio = nextRightRatio;

        ApplySliderImmediate(leftImmediateHealthSlider, nextLeftRatio);
        ApplySliderImmediate(rightImmediateHealthSlider, nextRightRatio);
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
        ApplySliderImmediate(immediateHealthSlider, ratio);
    }

    private void ApplyDelayedImmediate(float ratio)
    {
        ApplySliderImmediate(delayedHealthSlider, ratio);
        delayedVelocity = 0f;
    }

    /// <summary>지정한 슬라이더 값을 즉시 체력 비율로 맞춥니다.</summary>
    private void ApplySliderImmediate(Slider slider, float ratio)
    {
        if (slider == null)
            return;

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = ratio;
    }

    /// <summary>2체 보스 체력바의 지연 체력 연출을 갱신합니다.</summary>
    private void UpdateDualHealthBars()
    {
        UpdateDelayedSlider(leftDelayedHealthSlider, leftTargetHealthRatio, leftLastDamageTime, ref leftDelayedVelocity);
        UpdateDelayedSlider(rightDelayedHealthSlider, rightTargetHealthRatio, rightLastDamageTime, ref rightDelayedVelocity);
    }

    /// <summary>지정한 지연 슬라이더를 목표 체력 비율로 부드럽게 이동시킵니다.</summary>
    private void UpdateDelayedSlider(Slider slider, float targetRatio, float damageTime, ref float velocity)
    {
        if (slider == null)
            return;

        if (targetRatio >= slider.value)
        {
            ApplySliderImmediate(slider, targetRatio);
            velocity = 0f;
            return;
        }

        if (Time.unscaledTime - damageTime < delayedStartDelay)
            return;

        slider.value = Mathf.SmoothDamp(
            slider.value,
            targetRatio,
            ref velocity,
            Mathf.Max(0.01f, delayedSmoothTime),
            Mathf.Infinity,
            Time.unscaledDeltaTime);
    }

    /// <summary>2체 보스 체력바를 지정한 비율로 즉시 초기화합니다.</summary>
    private void ResetDualHealthRatios(float leftRatio, float rightRatio)
    {
        initializedDualHealth = true;
        leftTargetHealthRatio = leftRatio;
        rightTargetHealthRatio = rightRatio;
        leftPreviousHealthRatio = leftRatio;
        rightPreviousHealthRatio = rightRatio;
        leftLastDamageTime = float.NegativeInfinity;
        rightLastDamageTime = float.NegativeInfinity;
        leftDelayedVelocity = 0f;
        rightDelayedVelocity = 0f;

        ApplySliderImmediate(leftImmediateHealthSlider, leftRatio);
        ApplySliderImmediate(leftDelayedHealthSlider, leftRatio);
        ApplySliderImmediate(rightImmediateHealthSlider, rightRatio);
        ApplySliderImmediate(rightDelayedHealthSlider, rightRatio);
    }

    /// <summary>2체 보스 체력바 참조가 준비되어 있는지 확인하고 없으면 fallback을 생성합니다.</summary>
    private bool EnsureDualHealthPresentation()
    {
        if (HasDualHealthSliders())
            return true;

        if (!createFallbackDualHealthPresentation)
            return false;

        if (immediateHealthSlider == null || delayedHealthSlider == null)
            return false;

        CreateFallbackDualHealthPresentation();
        return HasDualHealthSliders();
    }

    /// <summary>2체 보스 체력바에 필요한 슬라이더 참조가 모두 있는지 확인합니다.</summary>
    private bool HasDualHealthSliders()
    {
        return leftImmediateHealthSlider != null
            && leftDelayedHealthSlider != null
            && rightImmediateHealthSlider != null
            && rightDelayedHealthSlider != null;
    }

    /// <summary>기존 체력 슬라이더를 복제해 좌우 반분 체력바를 만듭니다.</summary>
    private void CreateFallbackDualHealthPresentation()
    {
        RuntimePresentationFallbackAudit.Record(
            this,
            "Boss dual health fallback",
            "authored dual-health BossHealthBarUI references");

        if (dualHealthRoot == null)
        {
            GameObject rootObject = new GameObject("DualBossHealthRoot", typeof(RectTransform));
            dualHealthRoot = rootObject.GetComponent<RectTransform>();
            dualHealthRoot.SetParent(transform, false);
            StretchToParent(dualHealthRoot);
        }

        if (fallbackLeftHealthRoot == null)
            fallbackLeftHealthRoot = CreateFallbackHalfRoot("LeftBossHealth", true);

        if (fallbackRightHealthRoot == null)
            fallbackRightHealthRoot = CreateFallbackHalfRoot("RightBossHealth", false);

        if (leftDelayedHealthSlider == null)
            leftDelayedHealthSlider = CloneHealthSlider(delayedHealthSlider, fallbackLeftHealthRoot, "LeftDelayedHealthSlider");

        if (leftImmediateHealthSlider == null)
            leftImmediateHealthSlider = CloneHealthSlider(immediateHealthSlider, fallbackLeftHealthRoot, "LeftImmediateHealthSlider");

        if (rightDelayedHealthSlider == null)
            rightDelayedHealthSlider = CloneHealthSlider(delayedHealthSlider, fallbackRightHealthRoot, "RightDelayedHealthSlider");

        if (rightImmediateHealthSlider == null)
            rightImmediateHealthSlider = CloneHealthSlider(immediateHealthSlider, fallbackRightHealthRoot, "RightImmediateHealthSlider");
    }

    /// <summary>좌우 중 한쪽 체력바 루트를 생성합니다.</summary>
    private RectTransform CreateFallbackHalfRoot(string objectName, bool isLeftSide)
    {
        GameObject halfRootObject = new GameObject(objectName, typeof(RectTransform));
        RectTransform halfRoot = halfRootObject.GetComponent<RectTransform>();
        halfRoot.SetParent(dualHealthRoot, false);

        float halfGap = dualHealthGap * 0.5f;
        if (isLeftSide)
        {
            halfRoot.anchorMin = new Vector2(0f, 0f);
            halfRoot.anchorMax = new Vector2(0.5f, 1f);
            halfRoot.offsetMin = Vector2.zero;
            halfRoot.offsetMax = new Vector2(-halfGap, 0f);
        }
        else
        {
            halfRoot.anchorMin = new Vector2(0.5f, 0f);
            halfRoot.anchorMax = new Vector2(1f, 1f);
            halfRoot.offsetMin = new Vector2(halfGap, 0f);
            halfRoot.offsetMax = Vector2.zero;
        }

        halfRoot.pivot = new Vector2(0.5f, 0.5f);
        return halfRoot;
    }

    /// <summary>체력 슬라이더를 복제하고 부모 영역 전체에 맞춥니다.</summary>
    private Slider CloneHealthSlider(Slider sourceSlider, RectTransform parentRoot, string objectName)
    {
        if (sourceSlider == null || parentRoot == null)
            return null;

        Slider clonedSlider = Instantiate(sourceSlider, parentRoot);
        clonedSlider.name = objectName;
        StretchToParent(clonedSlider.GetComponent<RectTransform>());
        clonedSlider.value = 1f;
        clonedSlider.gameObject.SetActive(false);
        return clonedSlider;
    }

    /// <summary>RectTransform을 부모 영역 전체에 맞춥니다.</summary>
    private void StretchToParent(RectTransform targetTransform)
    {
        if (targetTransform == null)
            return;

        targetTransform.anchorMin = Vector2.zero;
        targetTransform.anchorMax = Vector2.one;
        targetTransform.offsetMin = Vector2.zero;
        targetTransform.offsetMax = Vector2.zero;
        targetTransform.pivot = new Vector2(0.5f, 0.5f);
    }

    /// <summary>단일 보스 체력바 표시 여부를 바꿉니다.</summary>
    private void ApplySingleHealthVisible(bool visible)
    {
        if (immediateHealthSlider != null && immediateHealthSlider.gameObject.activeSelf != visible)
            immediateHealthSlider.gameObject.SetActive(visible);

        if (delayedHealthSlider != null && delayedHealthSlider.gameObject.activeSelf != visible)
            delayedHealthSlider.gameObject.SetActive(visible);
    }

    /// <summary>2체 보스 체력바 표시 여부를 바꿉니다.</summary>
    private void ApplyDualHealthVisible(bool visible)
    {
        if (dualHealthRoot != null && dualHealthRoot.gameObject.activeSelf != visible)
            dualHealthRoot.gameObject.SetActive(visible);

        if (leftImmediateHealthSlider != null && leftImmediateHealthSlider.gameObject.activeSelf != visible)
            leftImmediateHealthSlider.gameObject.SetActive(visible);

        if (leftDelayedHealthSlider != null && leftDelayedHealthSlider.gameObject.activeSelf != visible)
            leftDelayedHealthSlider.gameObject.SetActive(visible);

        if (rightImmediateHealthSlider != null && rightImmediateHealthSlider.gameObject.activeSelf != visible)
            rightImmediateHealthSlider.gameObject.SetActive(visible);

        if (rightDelayedHealthSlider != null && rightDelayedHealthSlider.gameObject.activeSelf != visible)
            rightDelayedHealthSlider.gameObject.SetActive(visible);
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

        RuntimePresentationFallbackAudit.Record(
            this,
            "Boss split health fallback",
            "authored split-health BossHealthBarUI references");

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
