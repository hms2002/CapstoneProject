using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

public class SpeechBubble : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TextMeshProUGUI bubbleText;

    private Transform target;
    private Vector3 offset;
    private Tween typingTween;
    private Tween hideDelayTween;
    private Vector3 originalScale;
    private Action<SpeechBubble> releaseAction;

    private void Awake()
    {
        originalScale = transform.localScale;
    }

    public void SetupAndShow(
        Transform target,
        Vector3 offset,
        string text,
        float duration,
        bool useTyping,
        float typingSpeed,
        Action<SpeechBubble> onRelease)
    {
        StopActiveTweens();

        this.target = target;
        this.offset = offset;
        releaseAction = onRelease;

        transform.position = target.position + offset;
        gameObject.SetActive(true);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.DOKill();
        }

        transform.localScale = Vector3.zero;
        transform.DOKill();
        transform.DOScale(originalScale, 0.3f).SetEase(Ease.OutBack);

        if (bubbleText != null)
        {
            bubbleText.text = string.Empty;

            if (useTyping)
                typingTween = bubbleText.DOText(text, text.Length * typingSpeed).SetEase(Ease.Linear);
            else
                bubbleText.text = text;
        }

        if (duration > 0f)
            hideDelayTween = DOVirtual.DelayedCall(duration, Hide);
    }

    public void Hide()
    {
        StopActiveTweens();

        if (canvasGroup != null)
        {
            canvasGroup.DOFade(0f, 0.3f).OnComplete(() =>
            {
                ReleaseToPool();
            });
        }
        else
        {
            ReleaseToPool();
        }

        transform.DOScale(Vector3.zero, 0.3f).SetEase(Ease.InBack);
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        transform.position = target.position + offset;
    }

    private void OnDisable()
    {
        StopActiveTweens();
        target = null;
        releaseAction = null;
    }

    private void StopActiveTweens()
    {
        typingTween?.Kill();
        typingTween = null;

        hideDelayTween?.Kill();
        hideDelayTween = null;

        if (canvasGroup != null)
            canvasGroup.DOKill();

        transform.DOKill();
    }

    private void ReleaseToPool()
    {
        target = null;

        Action<SpeechBubble> callback = releaseAction;
        releaseAction = null;
        callback?.Invoke(this);
    }
}
