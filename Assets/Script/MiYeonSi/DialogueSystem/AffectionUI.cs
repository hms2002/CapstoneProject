using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System;

public class AffectionUI : MonoBehaviour
{
    [Header("UI 컴포넌트")]
    [SerializeField] private Slider affectionSlider;      // 게이지 (Fill 0 -> 1)
    [SerializeField] private TextMeshProUGUI affectionText; // 호감도 숫자 표시
    [SerializeField] private CanvasGroup uiCanvasGroup;   // UI 전체 투명도 조절용

    [Header("연출 설정")]
    [SerializeField] private float fillDuration = 0.5f;   // 게이지 차오르는 시간
    [SerializeField] private float resetDuration = 0.2f;  // 게이지 초기화 시간

    private void Awake()
    {
        // 시작 시 슬라이더 초기화
        if (affectionSlider != null) affectionSlider.value = 0f;
        if (uiCanvasGroup != null) uiCanvasGroup.alpha = 1f;
    }

    // 초기값 세팅 (대화 시작 시 호출 가능)
    public void Setup(int currentAffection)
    {
        if (affectionText != null)
            affectionText.text = currentAffection.ToString();

        if (affectionSlider != null)
            affectionSlider.value = 0f;
    }

    // 호감도 획득 연출 실행
    public void PlayGainAnimation(int prevAffection, int newAffection, Action onComplete)
    {
        // 1. 대화 조작 차단
        if (DialogueManager.GetInstance() != null)
            DialogueManager.GetInstance().IsEventRunning = true;

        Sequence seq = DOTween.Sequence();

        // [연출 단계 1] Fill이 0에서 1까지 차오름
        seq.Append(affectionSlider.DOValue(1f, fillDuration).SetEase(Ease.OutQuad));

        // [연출 단계 2] 호감도 숫자 변경 (연출 효과를 위해 살짝 펀치 효과 추가)
        seq.AppendCallback(() => {
            if (affectionText != null)
            {
                affectionText.text = newAffection.ToString();
                affectionText.transform.DOPunchScale(Vector3.one * 0.2f, 0.2f); // 살짝 커졌다 작아짐
            }
        });

        // 숫자 변경된 거 보여줄 딜레이 약간
        seq.AppendInterval(0.3f);

        // [연출 단계 3] Fill을 다시 0으로 만듦
        seq.Append(affectionSlider.DOValue(0f, resetDuration).SetEase(Ease.InQuad));

        // [종료] 대화 조작 해제 및 콜백 실행
        seq.OnComplete(() => {
            if (DialogueManager.GetInstance() != null)
                DialogueManager.GetInstance().IsEventRunning = false;

            onComplete?.Invoke();
        });
    }
}