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
    internal bool KeyboardNavigationActive { get; set; }
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
        bool shouldShow = isActiveAndEnabled && button != null && button.IsInteractable() &&
                          (selected || (pointerInside && !KeyboardNavigationActive));
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

// Screen owners call Refresh after EventSystem.Update so the first move can acquire focus without skipping an item.
internal sealed class ButtonNavigationGroup
{
    private readonly Selectable[] controls;
    private readonly Navigation.Mode layout;
    private readonly Color[] pointerColors;
    private readonly Sprite[] pointerSprites;
    private Selectable lastSelected;
    private bool keyboardActive;
    private bool spaceNeedsRelease = true;
    private int suspendedFrame = -1;

    public ButtonNavigationGroup(bool horizontal, params UnityEngine.UI.Button[] buttons)
        : this(horizontal ? Navigation.Mode.Horizontal : Navigation.Mode.Vertical, buttons)
    {
    }

    public ButtonNavigationGroup(Navigation.Mode layout, params Selectable[] controls)
    {
        this.layout = layout;
        this.controls = controls;
        pointerColors = new Color[controls.Length];
        pointerSprites = new Sprite[controls.Length];
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

        Selectable selected = FindSelected(eventSystem);
        if (HasPointerInput(eventSystem))
        {
            SetKeyboardActive(false);
            // A pointer down also selects a uGUI control. Let hover alone represent the mouse.
            if (selected != null)
                eventSystem.SetSelectedGameObject(null);
            return;
        }

        if (!IsAvailable(selected))
        {
            if (!keyboardActive && (Time.frameCount == suspendedFrame || !HasNavigationPress(eventSystem)))
                return;
            Selectable target = IsAvailable(lastSelected) ? lastSelected : FindAvailable(0, 1);
            if (target == null)
            {
                Suspend();
                return;
            }

            SetKeyboardActive(true);
            eventSystem.SetSelectedGameObject(target.gameObject);
            lastSelected = target;
            spaceNeedsRelease = InputKeyCompatibility.IsPressed(KeyCode.Space);
            return;
        }

        SetKeyboardActive(true);
        lastSelected = selected;
        if (selected is Button button)
            SubmitSpaceIfPressed(eventSystem, button);
    }

    private static bool HasPointerInput(EventSystem eventSystem)
    {
#if ENABLE_INPUT_SYSTEM
        if (eventSystem.currentInputModule is UnityEngine.InputSystem.UI.InputSystemUIInputModule module)
            return module.point?.action?.WasPerformedThisDynamicUpdate() == true ||
                   module.leftClick?.action?.WasPerformedThisDynamicUpdate() == true ||
                   module.scrollWheel?.action?.WasPerformedThisDynamicUpdate() == true;
#endif
        return Input.mousePositionDelta.sqrMagnitude > 0f || Input.GetMouseButtonDown(0) ||
               Input.mouseScrollDelta.sqrMagnitude > 0f;
    }

    private bool HasNavigationPress(EventSystem eventSystem)
    {
        if (!eventSystem.sendNavigationEvents)
            return false;
#if ENABLE_INPUT_SYSTEM
        if (eventSystem.currentInputModule is UnityEngine.InputSystem.UI.InputSystemUIInputModule module)
        {
            var move = module.move?.action;
            if (move == null || !move.WasPerformedThisDynamicUpdate())
                return false;
            Vector2 direction = move.ReadValue<Vector2>();
            return (layout != Navigation.Mode.Vertical && direction.x != 0f) ||
                   (layout != Navigation.Mode.Horizontal && direction.y != 0f);
        }
#endif
        return (layout != Navigation.Mode.Horizontal &&
                (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow) ||
                 Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.S))) ||
               (layout != Navigation.Mode.Vertical &&
                (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow) ||
                 Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.D)));
    }

    // uGUI keeps a stationary pointer inside its control even when navigation moves elsewhere.
    private void SetKeyboardActive(bool active)
    {
        if (keyboardActive == active)
            return;
        keyboardActive = active;
        for (int i = 0; i < controls.Length; i++)
        {
            Selectable control = controls[i];
            if (control == null)
                continue;
            if (control.TryGetComponent(out MenuButtonHighlightPresentation highlight))
                highlight.KeyboardNavigationActive = active;
            if (control.transition == Selectable.Transition.ColorTint)
            {
                var colors = control.colors;
                if (active) pointerColors[i] = colors.highlightedColor;
                colors.highlightedColor = active ? colors.normalColor : pointerColors[i];
                control.colors = colors;
            }
            else if (control.transition == Selectable.Transition.SpriteSwap)
            {
                var sprites = control.spriteState;
                if (active) pointerSprites[i] = sprites.highlightedSprite;
                sprites.highlightedSprite = active ? null : pointerSprites[i];
                control.spriteState = sprites;
            }
        }
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
        SetKeyboardActive(false);
        spaceNeedsRelease = true;
        suspendedFrame = Time.frameCount;
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return;

        Selectable selected = FindSelected(eventSystem);
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

    public void SetEntryControl(Selectable control) => lastSelected = control;

    private void ConfigureNavigation(bool inputAllowed)
    {
        // Settings retains its authored two-dimensional navigation and native slider adjustment.
        if (layout == Navigation.Mode.Automatic)
            return;
        for (int i = 0; i < controls.Length; i++)
        {
            Selectable button = controls[i];
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
                Selectable previous = FindAvailable(i - 1, -1);
                Selectable next = FindAvailable(i + 1, 1);
                if (layout == Navigation.Mode.Horizontal)
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

    private Selectable FindAvailable(int start, int direction)
    {
        for (int i = start; i >= 0 && i < controls.Length; i += direction)
        {
            if (IsAvailable(controls[i]))
                return controls[i];
        }

        return null;
    }

    private Selectable FindSelected(EventSystem eventSystem)
    {
        for (int i = 0; i < controls.Length; i++)
        {
            if (controls[i] != null && eventSystem.currentSelectedGameObject == controls[i].gameObject)
                return controls[i];
        }

        return null;
    }

    private static bool IsAvailable(Selectable button)
    {
        return button != null && button.IsActive() && button.IsInteractable();
    }
}
