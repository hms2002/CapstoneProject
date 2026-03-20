using UnityEngine;
using TMPro;
using DG.Tweening;
using System;

public class SpeechBubble : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TextMeshProUGUI bubbleText;

    private Transform target;
    private Vector3 offset;
    private Tween typingTween;

    // 1. (수정됨) 아까 우리가 고쳤던 스케일 기억 변수!
    private Vector3 originalScale;
    private Action<SpeechBubble> releaseAction;

    private void Awake()
    {
        // 시작할 때 자신의 원래 스케일(0.01 등)을 기억해둡니다.
        originalScale = transform.localScale;
    }

    public void SetupAndShow(Transform target, Vector3 offset, string text, float duration, bool useTyping, float typingSpeed, Action<SpeechBubble> onRelease)
    {
        this.target = target;
        this.offset = offset;
        this.releaseAction = onRelease;

        // 2. (수정됨 핵심!) 켜지기 전에 무조건 타겟의 머리 위로 위치를 강제 이동시킵니다.
        // 이 한 줄 덕분에 0,0,0에서 날아오는 버그가 완벽히 사라집니다.
        transform.position = target.position + offset;

        gameObject.SetActive(true);

        // 연출 초기화 및 시작
        canvasGroup.DOKill();
        canvasGroup.alpha = 1f;
        transform.localScale = Vector3.zero;

        // 원래 스케일(originalScale)로 커지도록 설정
        transform.DOScale(originalScale, 0.3f).SetEase(Ease.OutBack);

        typingTween?.Kill();
        bubbleText.text = "";

        if (useTyping)
        {
            typingTween = bubbleText.DOText(text, text.Length * typingSpeed).SetEase(Ease.Linear);
        }
        else
        {
            bubbleText.text = text;
        }

        if (duration > 0)
        {
            DOVirtual.DelayedCall(duration, Hide);
        }
    }

    public void Hide()
    {
        canvasGroup.DOFade(0f, 0.3f).OnComplete(() =>
        {
            target = null;
            releaseAction?.Invoke(this);
        });

        transform.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InBack);
    }
        
    private void LateUpdate()
    {
        if (target == null) return;

        // 3. (수정됨) SmoothDamp를 지우고 딱딱하게 바로 따라가도록 변경!
        transform.position = target.position + offset;
    }
}