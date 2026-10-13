using System.Collections;
using TMPro;
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
    private TMP_Text labelText;
    private readonly Vector3[] swordCorners = new Vector3[4];
    private const float SwordTextGap = 12f;
    private Bounds cachedLabelBounds;
    private bool labelBoundsDirty = true;
    private TMP_Text boundsLabel;
    private TMP_FontAsset boundsFont;
    private Material boundsMaterial;
    private string boundsText;
    private Vector2 boundsRectSize;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public int DiagnosticLabelMeshRefreshCount { get; private set; }
#endif

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
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(HandleLabelTextChanged);
        UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocaleChanged += HandleLocaleChanged;
        labelBoundsDirty = true;
        ResolveReferences();
        CaptureSwordVisiblePosition(force: false);
        Snap(false);
    }

    private void OnDisable()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(HandleLabelTextChanged);
        UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocaleChanged -= HandleLocaleChanged;
        labelBoundsDirty = true;
        StopMotion();
        pointerInside = false;
        selected = false;
    }

    private void OnValidate()
    {
        labelBoundsDirty = true;
        ResolveReferences();
        CaptureSwordVisiblePosition(force: true);
    }

    private void HandleLabelTextChanged(Object changed)
    {
        if (changed == labelText) labelBoundsDirty = true;
    }

    private void HandleLocaleChanged(UnityEngine.Localization.Locale locale) => labelBoundsDirty = true;

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
        if (visible) RefreshSwordVisiblePosition();
        RefreshState();

        if (visible && motionRoutine == null)
        {
            if (swordRoot != null) swordRoot.anchoredPosition = swordVisiblePosition;
            ApplyBackgroundAlpha(ResolveActiveBackgroundAlpha());
        }
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
        RefreshSwordVisiblePosition();

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
        Vector2 fromSwordOffset = swordRoot != null ? swordRoot.anchoredPosition - swordVisiblePosition : Vector2.zero;
        Vector2 toSwordOffset = show ? Vector2.zero : swordHiddenOffset;
        float fromSwordAlpha = swordGraphic != null ? swordGraphic.color.a : 0f;
        float toSwordAlpha = show ? swordVisibleAlpha : 0f;
        float duration = show ? showDuration : hideDuration;

        if (duration <= 0f)
        {
            ApplyPose(toBackgroundAlpha, swordVisiblePosition + toSwordOffset, toSwordAlpha);
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
                swordVisiblePosition + Vector2.LerpUnclamped(fromSwordOffset, toSwordOffset, eased),
                Mathf.LerpUnclamped(fromSwordAlpha, toSwordAlpha, eased));
            yield return null;
        }

        ApplyPose(toBackgroundAlpha, swordVisiblePosition + toSwordOffset, toSwordAlpha);
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
        RefreshSwordVisiblePosition();
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

        if (labelText == null)
            labelText = GetComponentInChildren<TMP_Text>(true);

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

    private void RefreshSwordVisiblePosition()
    {
        if (labelText == null || swordRoot == null || !(swordRoot.parent is RectTransform parent)) return;
        // CanvasScaler can leave the authored canvas at zero scale until its first layout pass.
        if (Mathf.Abs(parent.lossyScale.x) < 0.0001f || Mathf.Abs(parent.lossyScale.y) < 0.0001f) return;
        RefreshLabelBoundsIfNeeded();
        if (labelText.textInfo.characterCount == 0) return;
        Bounds bounds = cachedLabelBounds;
        Vector3 textLeft = parent.InverseTransformPoint(labelText.transform.TransformPoint(new Vector3(bounds.min.x, bounds.center.y, 0f)));
        swordRoot.GetWorldCorners(swordCorners);
        float right = float.NegativeInfinity, bottom = float.PositiveInfinity, top = float.NegativeInfinity;
        foreach (Vector3 corner in swordCorners)
        {
            Vector3 point = parent.InverseTransformPoint(corner);
            right = Mathf.Max(right, point.x);
            bottom = Mathf.Min(bottom, point.y);
            top = Mathf.Max(top, point.y);
        }
        // Convert the rendered text edge into the icon parent's space, retaining anchors/pivot.
        Vector2 shift = new Vector2(textLeft.x - SwordTextGap - right, textLeft.y - (bottom + top) * 0.5f);
        swordVisiblePosition = swordRoot.anchoredPosition + shift;
    }

    private void RefreshLabelBoundsIfNeeded()
    {
        if (!labelBoundsDirty && boundsLabel == labelText && boundsFont == labelText.font &&
            boundsMaterial == labelText.fontSharedMaterial && boundsText == labelText.text &&
            boundsRectSize.Equals(labelText.rectTransform.rect.size) && !labelText.havePropertiesChanged)
            return;

        labelText.ForceMeshUpdate(ignoreActiveState: true);
        cachedLabelBounds = labelText.textBounds;
        boundsLabel = labelText;
        boundsFont = labelText.font;
        boundsMaterial = labelText.fontSharedMaterial;
        boundsText = labelText.text;
        boundsRectSize = labelText.rectTransform.rect.size;
        // The forced refresh raises TMP's text event; the new bounds already
        // incorporate that generation, including font and layout changes.
        labelBoundsDirty = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DiagnosticLabelMeshRefreshCount++;
#endif
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

// Responsibility: keeps existing menu/card Buttons in one navigation group and restores its focus after suspension.
// Plain presentation state only; callers decide when their screen may receive input.
internal sealed class ButtonNavigationGroup
{
    private readonly UnityEngine.UI.Button[] buttons;
    private readonly bool horizontal;
    private UnityEngine.UI.Button lastSelected;
    private bool spaceNeedsRelease = true;

    public ButtonNavigationGroup(bool horizontal, params UnityEngine.UI.Button[] buttons)
    {
        this.horizontal = horizontal;
        this.buttons = buttons;
    }

    public void Refresh(bool inputAllowed)
    {
        ConfigureNavigation(inputAllowed);
        if (!inputAllowed)
        {
            Suspend();
            return;
        }

        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return;

        UnityEngine.UI.Button selected = FindSelected(eventSystem);
        if (IsAvailable(selected))
        {
            lastSelected = selected;
            SubmitSpaceIfPressed(eventSystem, selected);
            return;
        }

        UnityEngine.UI.Button target = IsAvailable(lastSelected) ? lastSelected : FindAvailable(0, 1);
        if (target == null)
        {
            spaceNeedsRelease = true;
            return;
        }

        eventSystem.SetSelectedGameObject(target.gameObject);
        lastSelected = target;
        SubmitSpaceIfPressed(eventSystem, target);
    }

    private void SubmitSpaceIfPressed(EventSystem eventSystem, UnityEngine.UI.Button selected)
    {
        if (spaceNeedsRelease)
        {
            if (!InputKeyCompatibility.IsPressed(KeyCode.Space))
                spaceNeedsRelease = false;
            return;
        }

        if (!InputKeyCompatibility.WasPressedThisFrame(KeyCode.Space))
            return;

        spaceNeedsRelease = true;
#if ENABLE_INPUT_SYSTEM
        // Native Submit (for example Enter) may already have run during EventSystem.Update.
        if (eventSystem.currentInputModule is UnityEngine.InputSystem.UI.InputSystemUIInputModule inputModule &&
            inputModule.submit?.action != null && inputModule.submit.action.WasPerformedThisDynamicUpdate())
        {
            return;
        }
#endif
        ExecuteEvents.Execute(selected.gameObject, new BaseEventData(eventSystem), ExecuteEvents.submitHandler);
    }

    public void Suspend()
    {
        spaceNeedsRelease = true;
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return;

        UnityEngine.UI.Button selected = FindSelected(eventSystem);
        if (selected == null)
            return;

        lastSelected = selected;
        eventSystem.SetSelectedGameObject(null);
    }

    public void Reset()
    {
        Suspend();
        lastSelected = null;
        ConfigureNavigation(false);
    }

    private void ConfigureNavigation(bool inputAllowed)
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            UnityEngine.UI.Button button = buttons[i];
            if (button == null)
                continue;

            UnityEngine.UI.Navigation navigation = new UnityEngine.UI.Navigation
            {
                mode = inputAllowed && IsAvailable(button)
                    ? UnityEngine.UI.Navigation.Mode.Explicit
                    : UnityEngine.UI.Navigation.Mode.None
            };
            if (navigation.mode == UnityEngine.UI.Navigation.Mode.Explicit)
            {
                UnityEngine.UI.Button previous = FindAvailable(i - 1, -1);
                UnityEngine.UI.Button next = FindAvailable(i + 1, 1);
                if (horizontal)
                {
                    navigation.selectOnLeft = previous;
                    navigation.selectOnRight = next;
                }
                else
                {
                    navigation.selectOnUp = previous;
                    navigation.selectOnDown = next;
                }
            }

            button.navigation = navigation;
        }
    }

    private UnityEngine.UI.Button FindAvailable(int start, int direction)
    {
        for (int i = start; i >= 0 && i < buttons.Length; i += direction)
        {
            if (IsAvailable(buttons[i]))
                return buttons[i];
        }

        return null;
    }

    private UnityEngine.UI.Button FindSelected(EventSystem eventSystem)
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null && eventSystem.currentSelectedGameObject == buttons[i].gameObject)
                return buttons[i];
        }

        return null;
    }

    private static bool IsAvailable(UnityEngine.UI.Button button)
    {
        return button != null && button.IsActive() && button.IsInteractable();
    }
}
