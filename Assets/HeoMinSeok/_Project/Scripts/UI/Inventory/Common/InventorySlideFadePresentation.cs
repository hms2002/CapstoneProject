using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Reusable slide-up/fade presentation for inventory-style UI roots.
/// </summary>
[DisallowMultipleComponent]
public sealed class InventorySlideFadePresentation : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform targetRoot;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Motion")]
    [SerializeField] private Vector2 openOffset = new Vector2(0f, -48f);
    [SerializeField, Min(0f)] private float openDuration = 0.18f;
    [SerializeField, Min(0f)] private float closeDuration = 0.14f;
    [SerializeField, Range(0f, 1f)] private float closedAlpha = 0f;
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private bool disableInteractionDuringMotion = true;
    [SerializeField] private bool deactivateAfterClose = true;

    private Vector2 openAnchoredPosition;
    private bool hasOpenAnchoredPosition;
    private Coroutine activeRoutine;

    private void Reset()
    {
        targetRoot = transform as RectTransform;
        canvasGroup = GetComponent<CanvasGroup>();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnDisable()
    {
        StopActiveRoutine();
    }

    public void PlayOpen(bool animate = true)
    {
        ResolveReferences();

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        StopActiveRoutine();
        CaptureOpenPosition();

        if (!animate || openDuration <= 0f)
        {
            ApplyPose(openAnchoredPosition, 1f);
            SetInteractionEnabled(true);
            return;
        }

        ApplyPose(openAnchoredPosition + openOffset, closedAlpha);
        SetInteractionEnabled(false);
        activeRoutine = StartCoroutine(Animate(
            openAnchoredPosition + openOffset,
            openAnchoredPosition,
            closedAlpha,
            1f,
            openDuration,
            EaseOutCubic,
            () => SetInteractionEnabled(true)));
    }

    public void PlayClose(Action onComplete = null, bool animate = true)
    {
        ResolveReferences();

        if (!gameObject.activeSelf)
        {
            onComplete?.Invoke();
            return;
        }

        StopActiveRoutine();
        CaptureOpenPosition();
        SetInteractionEnabled(false);

        Vector2 startPosition = targetRoot != null ? targetRoot.anchoredPosition : openAnchoredPosition;
        float startAlpha = canvasGroup != null ? canvasGroup.alpha : 1f;
        Vector2 closedPosition = openAnchoredPosition + openOffset;

        if (!animate || closeDuration <= 0f)
        {
            ApplyPose(closedPosition, closedAlpha);
            FinishClose(onComplete);
            return;
        }

        activeRoutine = StartCoroutine(Animate(
            startPosition,
            closedPosition,
            startAlpha,
            closedAlpha,
            closeDuration,
            EaseInCubic,
            () => FinishClose(onComplete)));
    }

    public void SnapOpen()
    {
        ResolveReferences();

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        StopActiveRoutine();
        CaptureOpenPosition();
        ApplyPose(openAnchoredPosition, 1f);
        SetInteractionEnabled(true);
    }

    private void ResolveReferences()
    {
        if (targetRoot == null)
            targetRoot = transform as RectTransform;

        if (canvasGroup != null)
            return;

        if (targetRoot != null)
            canvasGroup = targetRoot.GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    private void CaptureOpenPosition()
    {
        if (hasOpenAnchoredPosition || targetRoot == null)
            return;

        openAnchoredPosition = targetRoot.anchoredPosition;
        hasOpenAnchoredPosition = true;
    }

    private IEnumerator Animate(
        Vector2 fromPosition,
        Vector2 toPosition,
        float fromAlpha,
        float toAlpha,
        float duration,
        Func<float, float> ease,
        Action onComplete)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = ease != null ? ease(t) : t;
            ApplyPose(Vector2.LerpUnclamped(fromPosition, toPosition, eased), Mathf.LerpUnclamped(fromAlpha, toAlpha, eased));
            yield return null;
        }

        ApplyPose(toPosition, toAlpha);
        activeRoutine = null;
        onComplete?.Invoke();
    }

    private void ApplyPose(Vector2 anchoredPosition, float alpha)
    {
        if (targetRoot != null)
            targetRoot.anchoredPosition = anchoredPosition;

        if (canvasGroup != null)
            canvasGroup.alpha = alpha;
    }

    private void SetInteractionEnabled(bool enabled)
    {
        if (!disableInteractionDuringMotion || canvasGroup == null)
            return;

        canvasGroup.interactable = enabled;
        canvasGroup.blocksRaycasts = enabled;
    }

    private void FinishClose(Action onComplete)
    {
        if (deactivateAfterClose)
            gameObject.SetActive(false);

        onComplete?.Invoke();
    }

    private void StopActiveRoutine()
    {
        if (activeRoutine == null)
            return;

        StopCoroutine(activeRoutine);
        activeRoutine = null;
    }

    private static float EaseOutCubic(float t)
    {
        t = 1f - Mathf.Clamp01(t);
        return 1f - t * t * t;
    }

    private static float EaseInCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * t;
    }
}
