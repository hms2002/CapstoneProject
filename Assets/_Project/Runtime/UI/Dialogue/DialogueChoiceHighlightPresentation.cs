using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class DialogueChoiceHighlightPresentation : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform iconRoot;
    [SerializeField] private CanvasGroup iconCanvasGroup;
    [SerializeField] private Graphic iconGraphic;

    [Header("Motion")]
    [SerializeField] private Vector2 openOffset = new Vector2(0f, -18f);
    [SerializeField, Min(0f)] private float openDuration = 0.14f;
    [SerializeField, Min(0f)] private float closeDuration = 0.10f;
    [SerializeField, Min(0f)] private float hoverAmplitude = 5f;
    [SerializeField, Min(0.01f)] private float hoverCycleDuration = 0.72f;
    [SerializeField] private bool keepIconVisibleWhenDeselected;
    [SerializeField, Range(0f, 1f)] private float deselectedAlpha = 1f;

    private Vector2 openPosition;
    private bool hasOpenPosition;
    private bool selected;
    private Coroutine motionRoutine;
    private Coroutine hoverRoutine;

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();
        Snap(false);
    }

    private void OnDisable()
    {
        StopMotion();
        StopHover();
        selected = false;
    }

    public void SetSelected(bool isSelected, bool immediate = false)
    {
        ResolveReferences();
        CaptureOpenPosition();

        if (selected == isSelected && motionRoutine == null)
            return;

        selected = isSelected;
        StopMotion();
        StopHover();

        if (immediate)
        {
            Snap(isSelected);
            if (isSelected)
                StartHover();
            return;
        }

        motionRoutine = StartCoroutine(isSelected ? PlayOpen() : PlayClose());
    }

    private IEnumerator PlayOpen()
    {
        if (iconRoot == null || iconCanvasGroup == null)
        {
            motionRoutine = null;
            yield break;
        }

        iconRoot.gameObject.SetActive(true);
        Vector2 fromPosition = keepIconVisibleWhenDeselected ? iconRoot.anchoredPosition : openPosition + openOffset;
        Vector2 toPosition = openPosition;
        float fromAlpha = keepIconVisibleWhenDeselected ? iconCanvasGroup.alpha : 0f;
        float toAlpha = 1f;

        yield return Animate(fromPosition, toPosition, fromAlpha, toAlpha, openDuration, EaseOutCubic);
        motionRoutine = null;
        StartHover();
    }

    private IEnumerator PlayClose()
    {
        if (iconRoot == null || iconCanvasGroup == null)
        {
            motionRoutine = null;
            yield break;
        }

        Vector2 fromPosition = iconRoot.anchoredPosition;
        Vector2 toPosition = keepIconVisibleWhenDeselected ? openPosition : openPosition + openOffset;
        float fromAlpha = iconCanvasGroup.alpha;
        float toAlpha = keepIconVisibleWhenDeselected ? deselectedAlpha : 0f;

        yield return Animate(fromPosition, toPosition, fromAlpha, toAlpha, closeDuration, EaseInCubic);
        iconRoot.gameObject.SetActive(keepIconVisibleWhenDeselected);
        motionRoutine = null;
    }

    private IEnumerator Animate(
        Vector2 fromPosition,
        Vector2 toPosition,
        float fromAlpha,
        float toAlpha,
        float duration,
        System.Func<float, float> ease)
    {
        if (duration <= 0f)
        {
            ApplyPose(toPosition, toAlpha);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = ease != null ? ease(t) : t;
            ApplyPose(
                Vector2.LerpUnclamped(fromPosition, toPosition, eased),
                Mathf.LerpUnclamped(fromAlpha, toAlpha, eased));
            yield return null;
        }

        ApplyPose(toPosition, toAlpha);
    }

    private void StartHover()
    {
        if (iconRoot == null || hoverAmplitude <= 0f)
            return;

        StopHover();
        hoverRoutine = StartCoroutine(Hover());
    }

    private IEnumerator Hover()
    {
        float elapsed = 0f;
        while (selected)
        {
            elapsed += Time.unscaledDeltaTime;
            float phase = elapsed / hoverCycleDuration * Mathf.PI * 2f;
            iconRoot.anchoredPosition = openPosition + new Vector2(0f, Mathf.Sin(phase) * hoverAmplitude);
            yield return null;
        }
    }

    private void Snap(bool isSelected)
    {
        CaptureOpenPosition();

        if (iconRoot != null)
        {
            iconRoot.gameObject.SetActive(isSelected || keepIconVisibleWhenDeselected);
            iconRoot.anchoredPosition = isSelected || keepIconVisibleWhenDeselected
                ? openPosition
                : openPosition + openOffset;
        }

        if (iconCanvasGroup != null)
            iconCanvasGroup.alpha = isSelected ? 1f : keepIconVisibleWhenDeselected ? deselectedAlpha : 0f;
    }

    private void ApplyPose(Vector2 anchoredPosition, float alpha)
    {
        if (iconRoot != null)
            iconRoot.anchoredPosition = anchoredPosition;

        if (iconCanvasGroup != null)
            iconCanvasGroup.alpha = alpha;
    }

    private void ResolveReferences()
    {
        if (iconRoot == null)
            iconRoot = ResolveIconRoot();

        if (iconRoot == null)
            return;

        if (iconGraphic == null)
            iconGraphic = iconRoot.GetComponent<Graphic>();

        if (iconCanvasGroup == null)
            iconCanvasGroup = iconRoot.GetComponent<CanvasGroup>();

        if (iconCanvasGroup == null)
            iconCanvasGroup = iconRoot.gameObject.AddComponent<CanvasGroup>();

        if (iconGraphic != null)
            iconGraphic.raycastTarget = false;
    }

    private RectTransform ResolveIconRoot()
    {
        Transform glyph = transform.Find("KeyGlyph");
        if (glyph != null && glyph != transform)
            return glyph as RectTransform;

        Transform named = transform.Find("Image");
        if (named != null && named != transform)
            return named as RectTransform;

        Image[] images = GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image != null && image.transform != transform)
                return image.transform as RectTransform;
        }

        return null;
    }

    private void CaptureOpenPosition()
    {
        if (hasOpenPosition || iconRoot == null)
            return;

        openPosition = iconRoot.anchoredPosition;
        hasOpenPosition = true;
    }

    private void StopMotion()
    {
        if (motionRoutine == null)
            return;

        StopCoroutine(motionRoutine);
        motionRoutine = null;
    }

    private void StopHover()
    {
        if (hoverRoutine == null)
            return;

        StopCoroutine(hoverRoutine);
        hoverRoutine = null;
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
