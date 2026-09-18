using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Scene-authored guidance for one tutorial chest; never changes shared chest prefab defaults.</summary>
public sealed class PrototypeChestNavigation : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private TreasureChest targetChest;
    [SerializeField] private PrototypeTutorialUpgrade tutorial;
    [SerializeField] private RectTransform inputShield;
    [SerializeField] private RectTransform[] shadePanels = new RectTransform[4];
    [SerializeField] private Image rightClickGlyph;
    [SerializeField] private RectTransform instruction;

    private ChestScreen screen;
    private EventSystem navigationSystem;
    private GameObject previousSelection;
    private bool previousNavigation;
    private bool locked;
    private bool learnedSelection;
    private bool completed;
    private bool pulsing;
    private float pulseStarted;
    private Button confirm;
    private readonly Vector3[] corners = new Vector3[4];
    // Match ItemDetailPanel's authored unscaled fade/slide presentation.
    private const float OpenDuration = .12f, CloseDuration = .1f, HiddenOffset = -24f;
    private Graphic[] presentationGraphics;
    private Image shieldImage;
    private Vector2 glyphPosition, instructionPosition;
    private bool showing;
    private float visibility, presentationFrom, presentationElapsed;

    private void OnEnable()
    {
        inputShield.gameObject.SetActive(false);
        if (targetChest != null) targetChest.OpenedUi += OnChestOpened;
    }

    private void OnDisable()
    {
        if (targetChest != null) targetChest.OpenedUi -= OnChestOpened;
        Cleanup();
    }

    private void OnChestOpened(TreasureChest chest)
    {
        if (completed || chest != targetChest || tutorial == null || tutorial.Stage != 4) return;
        Cleanup();
        foreach (ChestScreen candidate in FindObjectsByType<ChestScreen>(FindObjectsInactive.Include))
            if (candidate.BoundInventory == chest.GetInventory()) { screen = candidate; break; }
        if (screen == null) return;
        screen.SelectionCommitted += OnCommitted;
        confirm = screen.ConfirmButton;
        pulseStarted = Time.unscaledTime;
        if (learnedSelection) return;
        if (!screen.AcquireGuidedSelection(this)) { Cleanup(); return; }
        locked = true;
        UiStackPlayback.SetExternalUiInputBlocked(this, true);
        ItemDragContext.CancelActiveDragSession();
        UIManager.Instance?.HideHoverImmediate();
        navigationSystem = EventSystem.current;
        if (navigationSystem != null)
        {
            previousNavigation = navigationSystem.sendNavigationEvents;
            previousSelection = navigationSystem.currentSelectedGameObject;
            navigationSystem.SetSelectedGameObject(null);
            navigationSystem.sendNavigationEvents = false;
        }
        // Intercept input during the existing reveal, but show the spotlight only after it settles.
        foreach (RectTransform panel in shadePanels) panel.gameObject.SetActive(false);
        rightClickGlyph.gameObject.SetActive(false);
        instruction.gameObject.SetActive(false);
        shieldImage = inputShield.GetComponent<Image>();
        shieldImage.raycastTarget = true;
        inputShield.gameObject.SetActive(true);
    }

    private void LateUpdate()
    {
        TickPresentation(Time.unscaledDeltaTime);
        if (screen == null) return;
        if (!screen.isActiveAndEnabled || targetChest == null || screen.BoundInventory != targetChest.GetInventory())
        { ReleaseScreen(); HideGuidance(); return; }
        if (locked)
        {
            if (!screen.IsSelectionReady) return;
            if (screen.FirstVisibleSlot == null)
            {
                Debug.LogWarning("Tutorial chest has no visible item; releasing guidance input lock.", this);
                Cleanup();
                return;
            }
            LayoutSpotlight(screen.FirstVisibleSlot.SlotRect);
            if (!showing)
            {
                showing = true;
                presentationFrom = visibility;
                presentationElapsed = 0f;
            }
            ApplyPresentation();
        }
        else if (confirm != null && confirm.targetGraphic != null)
        {
            if (!confirm.IsInteractable()) { RestoreButton(); return; }
            ColorBlock colors = confirm.colors;
            bool pressed = Mathf.FloorToInt((Time.unscaledTime - pulseStarted) / .55f) % 2 != 0;
            confirm.targetGraphic.CrossFadeColor((pressed ? colors.pressedColor : colors.normalColor) * colors.colorMultiplier,
                0f, true, true);
            pulsing = true;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!locked || screen == null || !screen.IsSelectionReady ||
            eventData.button != PointerEventData.InputButton.Right) return;
        ItemSlotUI slot = screen.FirstVisibleSlot;
        if (slot == null || !RectTransformUtility.RectangleContainsScreenPoint(slot.SlotRect,
                eventData.position, SlotCamera(slot.SlotRect))) return;
        if (!screen.TrySelectGuidedFirstSlot(this)) return;
        learnedSelection = true;
        ReleaseInput();
        HideGuidance();
        pulseStarted = Time.unscaledTime;
    }

    private static Camera SlotCamera(RectTransform slot)
    {
        Canvas canvas = slot.GetComponentInParent<Canvas>()?.rootCanvas;
        return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
    }

    private void LayoutSpotlight(RectTransform slot)
    {
        slot.GetWorldCorners(corners);
        Vector2 min = new(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new(float.NegativeInfinity, float.NegativeInfinity);
        Camera camera = SlotCamera(slot);
        foreach (Vector3 corner in corners)
        {
            Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corner);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(inputShield, point, null, out Vector2 local);
            min = Vector2.Min(min, local);
            max = Vector2.Max(max, local);
        }
        Rect bounds = inputShield.rect;
        min = Vector2.Max(min, bounds.min);
        max = Vector2.Min(max, bounds.max);
        SetRect(shadePanels[0], bounds.xMin, max.y, bounds.width, bounds.yMax - max.y);
        SetRect(shadePanels[1], bounds.xMin, bounds.yMin, bounds.width, min.y - bounds.yMin);
        SetRect(shadePanels[2], bounds.xMin, min.y, min.x - bounds.xMin, max.y - min.y);
        SetRect(shadePanels[3], max.x, min.y, bounds.xMax - max.x, max.y - min.y);
        rightClickGlyph.sprite = InputGlyphDatabase.Resolve(KeyCode.Mouse1).Icon;
        rightClickGlyph.gameObject.SetActive(rightClickGlyph.sprite != null);
        if (rightClickGlyph.sprite != null)
            SetRect(rightClickGlyph.rectTransform, (min.x + max.x) / 2f - 24f, max.y + 10f, 48f, 48f);
        SetRect(instruction, (min.x + max.x) / 2f - 200f, max.y + 62f, 400f, 40f);
        glyphPosition = rightClickGlyph.rectTransform.anchoredPosition;
        instructionPosition = instruction.anchoredPosition;
    }

    private void TickPresentation(float deltaTime)
    {
        if (!inputShield.gameObject.activeSelf) return;
        presentationElapsed += deltaTime;
        float t = Mathf.Clamp01(presentationElapsed / (showing ? OpenDuration : CloseDuration));
        float eased = 1f - Mathf.Pow(1f - t, 3f);
        visibility = Mathf.Lerp(presentationFrom, showing ? 1f : 0f, eased);
        ApplyPresentation();
        // During the reward reveal the invisible shield must still intercept input.
        if (!showing && !locked && t >= 1f) inputShield.gameObject.SetActive(false);
    }

    private void ApplyPresentation()
    {
        if (presentationGraphics == null)
        {
            presentationGraphics = new Graphic[shadePanels.Length + 2];
            for (int i = 0; i < shadePanels.Length; i++)
                presentationGraphics[i] = shadePanels[i].GetComponent<Graphic>();
            presentationGraphics[shadePanels.Length] = rightClickGlyph;
            presentationGraphics[shadePanels.Length + 1] = instruction.GetComponent<Graphic>();
        }
        foreach (Graphic graphic in presentationGraphics)
            if (graphic != null) graphic.canvasRenderer.SetAlpha(visibility);
        Vector2 offset = Vector2.up * Mathf.Lerp(HiddenOffset, 0f, visibility);
        rightClickGlyph.rectTransform.anchoredPosition = glyphPosition + offset;
        instruction.anchoredPosition = instructionPosition + offset;
    }

    private void HideGuidance()
    {
        showing = false;
        presentationFrom = visibility;
        presentationElapsed = 0f;
        if (visibility <= 0f) inputShield.gameObject.SetActive(false);
    }

    private void SetRect(RectTransform rect, float x, float y, float width, float height)
    {
        rect.gameObject.SetActive(true);
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(x - inputShield.rect.xMin, y - inputShield.rect.yMin);
        rect.sizeDelta = new Vector2(Mathf.Max(0f, width), Mathf.Max(0f, height));
    }

    private void OnCommitted()
    {
        completed = true;
        ReleaseScreen();
        HideGuidance();
        tutorial.CompleteChestTutorial();
    }

    private void ReleaseInput()
    {
        if (shieldImage != null) shieldImage.raycastTarget = false;
        if (!locked) return;
        locked = false;
        if (screen != null) screen.ReleaseGuidedSelection(this);
        UiStackPlayback.SetExternalUiInputBlocked(this, false);
        if (navigationSystem != null)
        {
            navigationSystem.sendNavigationEvents = previousNavigation;
            if (previousSelection != null && previousSelection.activeInHierarchy)
                navigationSystem.SetSelectedGameObject(previousSelection);
        }
        navigationSystem = null;
        previousSelection = null;
    }

    private void RestoreButton()
    {
        if (!pulsing || confirm == null || confirm.targetGraphic == null) return;
        ColorBlock colors = confirm.colors;
        confirm.targetGraphic.CrossFadeColor((confirm.IsInteractable() ? colors.normalColor : colors.disabledColor) * colors.colorMultiplier,
            0f, true, true);
        pulsing = false;
    }

    private void Cleanup()
    {
        ReleaseScreen();
        showing = false;
        visibility = presentationFrom = presentationElapsed = 0f;
        inputShield.gameObject.SetActive(false);
    }

    private void ReleaseScreen()
    {
        ReleaseInput();
        RestoreButton();
        if (screen != null) screen.SelectionCommitted -= OnCommitted;
        screen = null;
        confirm = null;
    }
}
