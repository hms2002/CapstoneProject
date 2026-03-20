using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System;

public class AffectionUI : MonoBehaviour
{
    [Header("UI 컴포넌트")]
    [SerializeField] private Slider affectionSlider;
    [SerializeField] private TextMeshProUGUI affectionText;
    [SerializeField] private CanvasGroup uiCanvasGroup;

    [Header("연출 설정")]
    [SerializeField] private float fillDuration = 0.5f;
    [SerializeField] private float resetDuration = 0.2f;

    private void Awake()
    {
        if (affectionSlider != null) affectionSlider.value = 0f;
        if (uiCanvasGroup != null) uiCanvasGroup.alpha = 1f;
    }

    private void OnEnable()
    {
        // UI가 활성화될 때마다 매니저에게 "나 여기 있어!" 하고 연결을 갱신합니다.
        if (AffectionManager.Instance != null)
        {
            AffectionManager.Instance.SetLinkedUI(this);
        }
    }

    public void Setup(int currentAffection)
    {
        if (affectionText != null)
            affectionText.text = currentAffection.ToString();

        if (affectionSlider != null)
            affectionSlider.value = 0f;
    }

    public void PlayGainAnimation(int prevAffection, int newAffection, Action onComplete)
    {
        // [수정] 이전에 진행 중이던 애니메이션이 있다면 강제 종료 (꼬임 방지)
        if (affectionSlider != null) affectionSlider.DOKill();
        if (affectionText != null) affectionText.transform.DOKill();

        if (affectionText != null) affectionText.text = prevAffection.ToString();

        Sequence seq = DOTween.Sequence();

        seq.Append(affectionSlider.DOValue(1f, fillDuration).SetEase(Ease.OutQuad));

        seq.AppendCallback(() => {
            if (affectionText != null)
            {
                affectionText.text = newAffection.ToString();
                affectionText.transform.DOPunchScale(Vector3.one * 0.3f, 0.3f, 5, 1f);
            }
        });

        seq.AppendInterval(0.4f);

        seq.Append(affectionSlider.DOValue(0f, resetDuration).SetEase(Ease.InQuad));

        seq.OnComplete(() => {
            onComplete?.Invoke();
        });
    }
}