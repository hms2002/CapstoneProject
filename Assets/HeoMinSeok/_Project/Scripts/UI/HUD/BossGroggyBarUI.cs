using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 책임 :
/// - 오케스트레이션 계층이 전달하는 보스 그로기 비율을 단일 슬라이더로 표시한다.
/// - 그로기 활성 중에는 fill 색상이 흰색 ↔ 원래 색을 오가며 점멸한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BossGroggyBarUI : MonoBehaviour
{
    [SerializeField] private Slider groggySlider;
    [SerializeField] private Image fillImage;

    [Header("Blink")]
    [SerializeField] private float blinkSpeed = 4f;
    [SerializeField] private Color blinkTargetColor = new Color(1f, 0.25f, 0.05f, 1f);

    private Color originalColor;
    private bool isGroggyMode;

    private void Awake()
    {
        ResolveFillReferences();
        CacheOriginalColors();
    }

    private void Update()
    {
        UpdateBlink(fillImage, originalColor, isGroggyMode);
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }

    public void SetGroggyRatio(float ratio)
    {
        ApplySliderValue(groggySlider, ratio);
    }

    public void SetGroggyMode(bool isGroggy)
    {
        if (isGroggyMode == isGroggy)
            return;

        isGroggyMode = isGroggy;
        if (!isGroggy)
            RestoreColor(fillImage, originalColor);
    }

    private void ResolveFillReferences()
    {
        if (fillImage == null)
            fillImage = ResolveFillImage(groggySlider);
    }

    private void CacheOriginalColors()
    {
        if (fillImage != null)
            originalColor = fillImage.color;
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
        targetImage.color = Color.Lerp(baseColor, blinkTargetColor, t);
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

    private static Image ResolveFillImage(Slider slider)
    {
        return slider != null && slider.fillRect != null
            ? slider.fillRect.GetComponent<Image>()
            : null;
    }
}
