using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class MenuButtonHighlightPresentation : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [Header("References")]
    [SerializeField] private Button button;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private RectTransform swordRoot;
    [SerializeField] private Graphic swordGraphic;

    [Header("Background Alpha")]
    [SerializeField, Range(0f, 1f)] private float hiddenBackgroundAlpha = 0f;
    [SerializeField, Range(0f, 1f)] private float activeBackgroundMinAlpha = 0.18f;
    [SerializeField, Range(0f, 1f)] private float activeBackgroundMaxAlpha = 0.34f;
    [SerializeField, Min(0f)] private float backgroundPulseDuration = 0.8f;

    [Header("Sword")]
    [SerializeField] private Vector2 swordHiddenOffset = new Vector2(-28f, 0f);
    [SerializeField, Range(0f, 1f)] private float swordVisibleAlpha = 1f;

    [Header("Motion")]
    [SerializeField, Min(0f)] private float showDuration = 0.14f;
    [SerializeField, Min(0f)] private float hideDuration = 0.10f;

    private Coroutine motionRoutine;
    private Vector2 swordVisiblePosition;
    private bool pointerInside;
    private bool selected;
    private bool visible;
    private bool swordPositionCaptured;

    private void Reset()
    {
        ResolveReferences();
        CaptureSwordVisiblePosition(force: true);
    }

    private void Awake()
    {
        ResolveReferences();
        CaptureSwordVisiblePosition(force: true);
        Snap(false);
    }

    private void OnEnable()
    {
        ResolveReferences();
        CaptureSwordVisiblePosition(force: false);
        Snap(false);
    }

    private void OnDisable()
    {
        StopMotion();
        pointerInside = false;
        selected = false;
    }

    private void OnValidate()
    {
        ResolveReferences();
        CaptureSwordVisiblePosition(force: true);
    }

    [ContextMenu("Preview Highlight On")]
    private void PreviewHighlightOn()
    {
        ResolveReferences();
        CaptureSwordVisiblePosition(force: true);
        Snap(true);
    }

    [ContextMenu("Preview Highlight Off")]
    private void PreviewHighlightOff()
    {
        Snap(false);
    }

    private void LateUpdate()
    {
        RefreshState();

        if (visible && motionRoutine == null)
            ApplyBackgroundAlpha(ResolveActiveBackgroundAlpha());
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;
        RefreshState();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        RefreshState();
    }

    public void OnSelect(BaseEventData eventData)
    {
        selected = true;
        RefreshState();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        selected = false;
        RefreshState();
    }

    private void RefreshState()
    {
        bool shouldShow = isActiveAndEnabled && button != null && button.IsInteractable() && (pointerInside || selected);
        if (visible == shouldShow)
            return;

        SetVisible(shouldShow);
    }

    private void SetVisible(bool show)
    {
        ResolveReferences();

        if (visible == show && motionRoutine == null)
            return;

        visible = show;
        StopMotion();
        motionRoutine = StartCoroutine(Animate(show));
    }

    private IEnumerator Animate(bool show)
    {
        if (backgroundImage == null && (swordRoot == null || swordGraphic == null))
        {
            motionRoutine = null;
            yield break;
        }

        if (show && swordRoot != null)
            swordRoot.gameObject.SetActive(true);

        float fromBackgroundAlpha = backgroundImage != null ? backgroundImage.color.a : hiddenBackgroundAlpha;
        float toBackgroundAlpha = show ? activeBackgroundMinAlpha : hiddenBackgroundAlpha;
        Vector2 fromSwordPosition = swordRoot != null ? swordRoot.anchoredPosition : swordVisiblePosition;
        Vector2 toSwordPosition = show ? swordVisiblePosition : swordVisiblePosition + swordHiddenOffset;
        float fromSwordAlpha = swordGraphic != null ? swordGraphic.color.a : 0f;
        float toSwordAlpha = show ? swordVisibleAlpha : 0f;
        float duration = show ? showDuration : hideDuration;

        if (duration <= 0f)
        {
            ApplyPose(toBackgroundAlpha, toSwordPosition, toSwordAlpha);
            FinishAnimation(show);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = show ? EaseOutCubic(t) : EaseInCubic(t);
            ApplyPose(
                Mathf.LerpUnclamped(fromBackgroundAlpha, toBackgroundAlpha, eased),
                Vector2.LerpUnclamped(fromSwordPosition, toSwordPosition, eased),
                Mathf.LerpUnclamped(fromSwordAlpha, toSwordAlpha, eased));
            yield return null;
        }

        ApplyPose(toBackgroundAlpha, toSwordPosition, toSwordAlpha);
        FinishAnimation(show);
    }

    private void FinishAnimation(bool show)
    {
        motionRoutine = null;

        if (!show && swordRoot != null)
            swordRoot.gameObject.SetActive(false);
    }

    private void Snap(bool show)
    {
        ResolveReferences();
        visible = show;

        if (show && swordRoot != null)
            swordRoot.gameObject.SetActive(true);

        ApplyPose(
            show ? activeBackgroundMinAlpha : hiddenBackgroundAlpha,
            show ? swordVisiblePosition : swordVisiblePosition + swordHiddenOffset,
            show ? swordVisibleAlpha : 0f);

        if (!show && swordRoot != null)
            swordRoot.gameObject.SetActive(false);
    }

    private void ApplyPose(float backgroundAlpha, Vector2 swordPosition, float swordAlpha)
    {
        ApplyBackgroundAlpha(backgroundAlpha);

        if (swordRoot != null)
            swordRoot.anchoredPosition = swordPosition;

        ApplySwordAlpha(swordAlpha);
    }

    private void ApplyBackgroundAlpha(float alpha)
    {
        if (backgroundImage == null)
            return;

        Color color = backgroundImage.color;
        color.a = Mathf.Clamp01(alpha);
        backgroundImage.color = color;
    }

    private void ApplySwordAlpha(float alpha)
    {
        if (swordGraphic == null)
            return;

        Color color = swordGraphic.color;
        color.a = Mathf.Clamp01(alpha);
        swordGraphic.color = color;
    }

    private float ResolveActiveBackgroundAlpha()
    {
        float min = Mathf.Min(activeBackgroundMinAlpha, activeBackgroundMaxAlpha);
        float max = Mathf.Max(activeBackgroundMinAlpha, activeBackgroundMaxAlpha);

        if (backgroundPulseDuration <= 0f || Mathf.Approximately(min, max))
            return max;

        float phase = Mathf.PingPong(Time.unscaledTime / backgroundPulseDuration, 1f);
        return Mathf.Lerp(min, max, EaseInOutSine(phase));
    }

    private void ResolveReferences()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();

        if (swordRoot == null)
            swordRoot = ResolveVisualRoot("SelectionSwordIcon");

        if (swordGraphic == null && swordRoot != null)
            swordGraphic = swordRoot.GetComponent<Graphic>();

        if (swordGraphic != null)
            swordGraphic.raycastTarget = false;
    }

    private RectTransform ResolveVisualRoot(string visualName)
    {
        Transform existing = transform.Find(visualName);
        if (existing != null)
            return existing as RectTransform;

        if (transform.parent == null)
            return null;

        existing = transform.parent.Find(visualName);
        return existing as RectTransform;
    }

    private void CaptureSwordVisiblePosition(bool force)
    {
        if (swordRoot == null || (!force && swordPositionCaptured))
            return;

        swordVisiblePosition = swordRoot.anchoredPosition;
        swordPositionCaptured = true;
    }

    private void StopMotion()
    {
        if (motionRoutine == null)
            return;

        StopCoroutine(motionRoutine);
        motionRoutine = null;
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

    private static float EaseInOutSine(float t)
    {
        return 0.5f - Mathf.Cos(Mathf.Clamp01(t) * Mathf.PI) * 0.5f;
    }
}
