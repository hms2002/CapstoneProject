using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 책임 :
/// - 오케스트레이션 계층이 전달하는 보스 그로기 비율을 단일 슬라이더에 표시한다.
/// - 그로기 활성 중에는 fill 색상이 흰색 ↔ 원래 색을 오가며 점멸한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BossGroggyBarUI : MonoBehaviour
{
    [SerializeField] private Slider groggySlider;
    [SerializeField] private Image fillImage;

    [Header("Blink")]
    [SerializeField] private float blinkSpeed = 4f;

    private Color _originalColor;
    private bool _isGroggyMode;

    private void Awake()
    {
        if (fillImage == null && groggySlider != null)
            fillImage = groggySlider.fillRect.GetComponent<Image>();

        if (fillImage != null)
            _originalColor = fillImage.color;
    }

    private void Update()
    {
        if (!_isGroggyMode || fillImage == null) return;

        float t = Mathf.PingPong(Time.time * blinkSpeed, 1f);
        fillImage.color = Color.Lerp(_originalColor, Color.white, t);
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }

    public void SetGroggyRatio(float ratio)
    {
        if (groggySlider == null) return;

        groggySlider.minValue = 0f;
        groggySlider.maxValue = 1f;
        groggySlider.value = Mathf.Clamp01(ratio);
    }

    public void SetGroggyMode(bool isGroggy)
    {
        if (_isGroggyMode == isGroggy) return;

        _isGroggyMode = isGroggy;

        // 그로기 모드 종료 시 색상 원복
        if (!isGroggy && fillImage != null)
            fillImage.color = _originalColor;
    }
}
