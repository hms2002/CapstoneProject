using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System;

public class AffectionUI : MonoBehaviour
{
    [Header("UI 컴포넌트")]
    [SerializeField] private Slider affectionSlider;        // 호감도 게이지
    [SerializeField] private TextMeshProUGUI affectionText; // 호감도 숫자 텍스트
    [SerializeField] private CanvasGroup uiCanvasGroup;

    [Header("연출 설정")]
    [SerializeField] private float fillDuration = 0.5f;
    [SerializeField] private float resetDuration = 0.2f;

    private void Awake()
    {
        if (affectionSlider != null) affectionSlider.value = 0f;
        if (uiCanvasGroup != null) uiCanvasGroup.alpha = 1f;
    }

    // 초기 상태 설정 (대화 시작 시 호출됨)
    public void Setup(int currentAffection)
    {
        if (affectionText != null)
            affectionText.text = currentAffection.ToString();

        if (affectionSlider != null)
            affectionSlider.value = 0f;
    }

    // 호감도 상승 연출
    public void PlayGainAnimation(int prevAffection, int newAffection, Action onComplete)
    {
        // 안전 장치: 시작 전 텍스트 갱신
        if (affectionText != null) affectionText.text = prevAffection.ToString();

        Sequence seq = DOTween.Sequence();

        // 1. 게이지 차오름
        seq.Append(affectionSlider.DOValue(1f, fillDuration).SetEase(Ease.OutQuad));

        // 2. 숫자 변경 및 펀치 효과 (게이지가 다 찼을 때)
        seq.AppendCallback(() => {
            if (affectionText != null)
            {
                affectionText.text = newAffection.ToString();
                affectionText.transform.DOPunchScale(Vector3.one * 0.3f, 0.3f, 5, 1f);
            }
        });

        // 3. 유저가 변화를 인지할 시간 (0.4초 대기)
        seq.AppendInterval(0.4f);

        // 4. 게이지 초기화 (다음 상승을 위해 비움)
        seq.Append(affectionSlider.DOValue(0f, resetDuration).SetEase(Ease.InQuad));

        // 5. 완료 콜백 실행 (매니저에게 알림)
        seq.OnComplete(() => {
            onComplete?.Invoke();
        });
    }
}