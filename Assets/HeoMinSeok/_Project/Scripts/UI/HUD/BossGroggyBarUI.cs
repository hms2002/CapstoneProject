using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 책임 :
/// - 오케스트레이션 계층이 전달하는 보스 그로기 비율을 단일 또는 2채널 슬라이더로 표시한다.
/// - 그로기 활성 중에는 각 fill 색상이 흰색 ↔ 원래 색을 오가며 점멸한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BossGroggyBarUI : MonoBehaviour
{
    [SerializeField] private Slider groggySlider;
    [SerializeField] private Image fillImage;

    [Header("Dual Boss Groggy")]
    [Tooltip("2체 보스 그로기바 루트입니다. 비어 있으면 기존 슬라이더를 복제해 fallback UI를 만듭니다.")]
    [SerializeField] private RectTransform dualGroggyRoot;

    [Tooltip("왼쪽 보스의 그로기 슬라이더입니다.")]
    [SerializeField] private Slider leftGroggySlider;

    [Tooltip("왼쪽 보스의 그로기 fill 이미지입니다. 비어 있으면 슬라이더 fillRect에서 찾습니다.")]
    [SerializeField] private Image leftFillImage;

    [Tooltip("오른쪽 보스의 그로기 슬라이더입니다.")]
    [SerializeField] private Slider rightGroggySlider;

    [Tooltip("오른쪽 보스의 그로기 fill 이미지입니다. 비어 있으면 슬라이더 fillRect에서 찾습니다.")]
    [SerializeField] private Image rightFillImage;

    [Tooltip("좌우 그로기바 사이 간격입니다.")]
    [SerializeField, Min(0f)] private float dualGroggyGap = 10f;

    [Tooltip("전용 그로기바 참조가 없을 때 기존 슬라이더를 복제해 fallback UI를 생성합니다.")]
    [SerializeField] private bool createFallbackDualGroggyPresentation = true;

    [Header("Blink")]
    [SerializeField] private float blinkSpeed = 4f;

    private Color originalColor;
    private Color leftOriginalColor;
    private Color rightOriginalColor;
    private bool isGroggyMode;
    private bool isDualGroggyMode;
    private bool leftIsGroggyMode;
    private bool rightIsGroggyMode;
    private RectTransform fallbackLeftGroggyRoot;
    private RectTransform fallbackRightGroggyRoot;

    private void Awake()
    {
        ResolveFillReferences();
        CacheOriginalColors();
        ApplyDualGroggyVisible(false);
    }

    private void Update()
    {
        if (isDualGroggyMode)
        {
            UpdateBlink(leftFillImage, leftOriginalColor, leftIsGroggyMode);
            UpdateBlink(rightFillImage, rightOriginalColor, rightIsGroggyMode);
            return;
        }

        UpdateBlink(fillImage, originalColor, isGroggyMode);
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }

    public void SetGroggyRatio(float ratio)
    {
        if (isDualGroggyMode)
            SetDualGroggyRatios(false, 0f, false, 0f, false);

        ApplySliderValue(groggySlider, ratio);
    }

    public void SetGroggyMode(bool isGroggy)
    {
        if (isDualGroggyMode)
            SetDualGroggyRatios(false, 0f, false, 0f, false);

        if (isGroggyMode == isGroggy)
            return;

        isGroggyMode = isGroggy;
        if (!isGroggy)
            RestoreColor(fillImage, originalColor);
    }

    /// <summary>2체 보스 그로기바 표시 여부와 좌우 그로기 값을 갱신합니다.</summary>
    public void SetDualGroggyRatios(
        bool visible,
        float leftRatio,
        bool leftIsGroggy,
        float rightRatio,
        bool rightIsGroggy)
    {
        if (!visible)
        {
            isDualGroggyMode = false;
            leftIsGroggyMode = false;
            rightIsGroggyMode = false;
            RestoreColor(leftFillImage, leftOriginalColor);
            RestoreColor(rightFillImage, rightOriginalColor);
            ApplySingleGroggyVisible(true);
            ApplyDualGroggyVisible(false);
            return;
        }

        if (!EnsureDualGroggyPresentation())
            return;

        isDualGroggyMode = true;
        isGroggyMode = false;
        leftIsGroggyMode = leftIsGroggy;
        rightIsGroggyMode = rightIsGroggy;
        RestoreColor(fillImage, originalColor);
        if (!leftIsGroggy)
            RestoreColor(leftFillImage, leftOriginalColor);
        if (!rightIsGroggy)
            RestoreColor(rightFillImage, rightOriginalColor);

        ApplySingleGroggyVisible(false);
        ApplyDualGroggyVisible(true);
        ApplySliderValue(leftGroggySlider, leftRatio);
        ApplySliderValue(rightGroggySlider, rightRatio);
    }

    private void ResolveFillReferences()
    {
        if (fillImage == null)
            fillImage = ResolveFillImage(groggySlider);

        if (leftFillImage == null)
            leftFillImage = ResolveFillImage(leftGroggySlider);

        if (rightFillImage == null)
            rightFillImage = ResolveFillImage(rightGroggySlider);
    }

    private void CacheOriginalColors()
    {
        if (fillImage != null)
            originalColor = fillImage.color;

        if (leftFillImage != null)
            leftOriginalColor = leftFillImage.color;

        if (rightFillImage != null)
            rightOriginalColor = rightFillImage.color;
    }

    private void UpdateBlink(Image targetImage, Color baseColor, bool isBlinking)
    {
        if (targetImage == null)
            return;

        if (!isBlinking)
        {
            if (targetImage.color != baseColor)
                targetImage.color = baseColor;

            return;
        }

        float t = Mathf.PingPong(Time.time * blinkSpeed, 1f);
        targetImage.color = Color.Lerp(baseColor, Color.white, t);
    }

    private void RestoreColor(Image targetImage, Color color)
    {
        if (targetImage != null)
            targetImage.color = color;
    }

    private void ApplySliderValue(Slider slider, float ratio)
    {
        if (slider == null)
            return;

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = Mathf.Clamp01(ratio);
    }

    private bool EnsureDualGroggyPresentation()
    {
        if (HasDualGroggySliders())
            return true;

        if (!createFallbackDualGroggyPresentation || groggySlider == null)
            return false;

        CreateFallbackDualGroggyPresentation();
        ResolveFillReferences();
        CacheOriginalColors();
        return HasDualGroggySliders();
    }

    private bool HasDualGroggySliders()
    {
        return leftGroggySlider != null && rightGroggySlider != null;
    }

    private void CreateFallbackDualGroggyPresentation()
    {
        RuntimePresentationFallbackAudit.Record(
            this,
            "Boss dual groggy fallback",
            "authored dual-groggy BossGroggyBarUI references");

        if (dualGroggyRoot == null)
        {
            GameObject rootObject = new GameObject("DualBossGroggyRoot", typeof(RectTransform));
            dualGroggyRoot = rootObject.GetComponent<RectTransform>();
            dualGroggyRoot.SetParent(transform, false);
            StretchToParent(dualGroggyRoot);
        }

        if (fallbackLeftGroggyRoot == null)
            fallbackLeftGroggyRoot = CreateFallbackHalfRoot("LeftBossGroggy", true);

        if (fallbackRightGroggyRoot == null)
            fallbackRightGroggyRoot = CreateFallbackHalfRoot("RightBossGroggy", false);

        if (leftGroggySlider == null)
            leftGroggySlider = CloneGroggySlider(groggySlider, fallbackLeftGroggyRoot, "LeftGroggySlider");

        if (rightGroggySlider == null)
            rightGroggySlider = CloneGroggySlider(groggySlider, fallbackRightGroggyRoot, "RightGroggySlider");
    }

    private RectTransform CreateFallbackHalfRoot(string objectName, bool isLeftSide)
    {
        GameObject halfRootObject = new GameObject(objectName, typeof(RectTransform));
        RectTransform halfRoot = halfRootObject.GetComponent<RectTransform>();
        halfRoot.SetParent(dualGroggyRoot, false);

        float halfGap = dualGroggyGap * 0.5f;
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

    private Slider CloneGroggySlider(Slider sourceSlider, RectTransform parentRoot, string objectName)
    {
        if (sourceSlider == null || parentRoot == null)
            return null;

        Slider clonedSlider = Instantiate(sourceSlider, parentRoot);
        clonedSlider.name = objectName;
        StretchToParent(clonedSlider.GetComponent<RectTransform>());
        clonedSlider.value = 1f;
        clonedSlider.gameObject.SetActive(false);

        BossGroggyBarUI clonedController = clonedSlider.GetComponent<BossGroggyBarUI>();
        if (clonedController != null)
            Destroy(clonedController);

        return clonedSlider;
    }

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

    private void ApplySingleGroggyVisible(bool visible)
    {
        if (groggySlider != null && groggySlider.gameObject != gameObject &&
            groggySlider.gameObject.activeSelf != visible)
        {
            groggySlider.gameObject.SetActive(visible);
        }
    }

    private void ApplyDualGroggyVisible(bool visible)
    {
        if (dualGroggyRoot != null && dualGroggyRoot.gameObject.activeSelf != visible)
            dualGroggyRoot.gameObject.SetActive(visible);

        if (leftGroggySlider != null && leftGroggySlider.gameObject.activeSelf != visible)
            leftGroggySlider.gameObject.SetActive(visible);

        if (rightGroggySlider != null && rightGroggySlider.gameObject.activeSelf != visible)
            rightGroggySlider.gameObject.SetActive(visible);
    }

    private static Image ResolveFillImage(Slider slider)
    {
        return slider != null && slider.fillRect != null
            ? slider.fillRect.GetComponent<Image>()
            : null;
    }
}
