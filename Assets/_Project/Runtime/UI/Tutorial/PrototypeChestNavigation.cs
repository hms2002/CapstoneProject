using TMPro;
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
    [SerializeField] private ChestInteractable worldChest;
    [SerializeField] private Transform worldArrow;
    [Header("Authored potion lesson")]
    [SerializeField] private ConsumableDefinition tutorialPotion;
    [SerializeField] private Image potionRangeEndGlyph;
    [SerializeField] private TMP_Text potionRangeSeparator;
    [SerializeField] private Image potionUseGlyph;
    [SerializeField] private TMP_Text potionUseInstruction;
    private PlayerConsumableInventory potionInventory;
    private PlayerConsumableHUD2D potionHud;
    private bool potionGuidance;
    private RectTransform potionHudRect;
    private Vector3 potionHudOriginalPosition, potionHudOriginalScale, potionHudTargetPosition;
    private float potionMotionElapsed;
    private bool potionReturning;
    private InventoryScreen lessonInventoryScreen;
    private InventoryOpenHudButton inventoryButton;
    private InventoryUIOpenRequestHandler inventoryHandler;
    private PlayerCombatInput2D inventoryCombatInput;
    private bool inventoryGuidance;
    private bool inventoryInputArmed;
    private enum InventoryLessonPhase { Intro, AwaitOpen, Viewing, Returning }
    private InventoryLessonPhase inventoryPhase;
    private const float InventoryMotionDuration = .8f;
    private float inventoryMotionElapsed;
    private RectTransform inventoryButtonRect;
    private Vector3 inventoryOriginalPosition, inventoryOriginalScale;
    private Canvas guidanceCanvas;
    private int guidanceSortingOrder, guidanceSortingLayer;
    private TMP_Text inventoryLabel;
    private float inventoryLabelFontSize;
    private TextWrappingModes inventoryLabelWrapping;
    private TextAlignmentOptions inventoryLabelAlignment;
    private static readonly InputActionId[] InventoryBlockedActions =
    {
        InputActionId.Dash, InputActionId.SwapWeapon, InputActionId.Interact,
        InputActionId.ConsumableSlot1, InputActionId.ConsumableSlot2,
        InputActionId.ConsumableSlot3, InputActionId.ConsumableSlot4
    };
    private bool worldHighlight;
    private float worldHighlightStarted;

    private ChestScreen screen;
    private EventSystem navigationSystem;
    private GameObject previousSelection;
    private bool previousNavigation;
    private bool locked;
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
        SetPotionExtrasVisible(false);
        if (worldArrow != null) worldArrow.gameObject.SetActive(false);
        if (targetChest != null) targetChest.OpenedUi += OnChestOpened;
    }

    private void OnDisable()
    {
        if (targetChest != null) targetChest.OpenedUi -= OnChestOpened;
        SetWorldGuidance(false);
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
        if (tutorialPotion == null || !screen.RequireSelection(this, tutorialPotion)) { Cleanup(); return; }
        confirm = screen.ConfirmButton;
        pulseStarted = Time.unscaledTime;
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
        SetWorldGuidance(tutorial != null && tutorial.isActiveAndEnabled && tutorial.Stage == 4 &&
            !completed && (screen == null || !screen.isActiveAndEnabled));
        TickPresentation(Time.unscaledDeltaTime);
        if (TickPotionGuidance()) return;
        if (TickInventoryGuidance()) return;
        if (screen == null) return;
        if (!screen.isActiveAndEnabled || targetChest == null || screen.BoundInventory != targetChest.GetInventory())
        { ReleaseScreen(); HideGuidance(); return; }
        if (locked)
        {
            if (!screen.IsSelectionReady) return;
            ItemSlotUI potionSlot = screen.FindVisibleSlot(tutorialPotion);
            if (potionSlot == null)
            {
                Debug.LogError("Tutorial chest requires its authored healing potion; no matching reward slot was found.", this);
                Cleanup();
                return;
            }
            LayoutSpotlight(potionSlot.SlotRect);
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

    private bool TickInventoryGuidance()
    {
        if (tutorial == null || !tutorial.isActiveAndEnabled || tutorial.Stage != 5)
        {
            if (inventoryGuidance) { ReleaseInventoryGuidance(); HideGuidance(); }
            return false;
        }
        // The same InventoryScreen hosts the chest. Wait for its close animation to finish.
        if (!inventoryGuidance)
        {
            if (InventoryUIManager.Instance == null || InventoryUIManager.Instance.IsOpen ||
                PlayerRuntimeRegistry.CurrentPlayer == null) return true;
            if (inventoryButton == null) inventoryButton = ResolveInventoryHudButton();
            if (inventoryHandler == null) inventoryHandler = FindFirstObjectByType<InventoryUIOpenRequestHandler>(FindObjectsInactive.Include);
            if (inventoryButton == null || inventoryHandler == null || !inventoryHandler.CanOpenInventory) return true;
            inventoryButtonRect = inventoryButton.GetPresentationRoot().transform as RectTransform;
            if (inventoryButtonRect == null) return true;
            ReleaseScreen();
            inventoryGuidance = true;
            inventoryPhase = InventoryLessonPhase.Intro;
            inventoryMotionElapsed = 0f;
            inventoryInputArmed = false;
            inventoryOriginalPosition = inventoryButtonRect.anchoredPosition3D;
            inventoryOriginalScale = inventoryButtonRect.localScale;
            guidanceCanvas = inputShield.GetComponentInParent<Canvas>();
            if (guidanceCanvas != null)
            {
                guidanceSortingOrder = guidanceCanvas.sortingOrder;
                guidanceSortingLayer = guidanceCanvas.sortingLayerID;
            }
            inventoryLabel = instruction.GetComponent<TMP_Text>();
            if (inventoryLabel != null)
            {
                inventoryLabelFontSize = inventoryLabel.fontSize;
                inventoryLabelWrapping = inventoryLabel.textWrappingMode;
                inventoryLabelAlignment = inventoryLabel.alignment;
            }
            inventoryCombatInput = PlayerRuntimeRegistry.CurrentPlayer.GetComponent<PlayerCombatInput2D>();
            inventoryCombatInput?.SetWeaponInputBlocked(this, true);
            foreach (InputActionId action in InventoryBlockedActions) InputActionQuery.SetPressBlocked(action, this, true);
            InputActionQuery.SetPressBlocked(InputActionId.InventoryToggle, this, true);
            TimeScalePausePlayback.SetOwnedTimeScale(this, 0f);
            shieldImage = inputShield.GetComponent<Image>();
            shieldImage.raycastTarget = true;
            inputShield.gameObject.SetActive(true);
            showing = true;
            presentationFrom = visibility;
            presentationElapsed = 0f;
            LayoutSpotlight(inventoryButtonRect, inventory: true);
            SetInventoryInstructionVisible(false);
            ApplyPresentation();
            return true;
        }
        if (PlayerRuntimeRegistry.CurrentPlayer == null || inventoryHandler == null || inventoryButton == null || inventoryButtonRect == null)
        {
            ReleaseInventoryGuidance();
            HideGuidance();
            return true;
        }
        TickInventoryPresentation(Time.unscaledDeltaTime);
        return true;
    }

    private void TickInventoryPresentation(float deltaTime)
    {
        if (inventoryPhase == InventoryLessonPhase.Viewing)
        {
            // IsInventoryOpen remains true throughout the screen's closing animation.
            if (inventoryHandler.IsInventoryOpen) return;
            inventoryPhase = InventoryLessonPhase.Returning;
            inventoryMotionElapsed = 0f;
            RestoreGuidanceCanvas();
            InputActionQuery.SetPressBlocked(InputActionId.InventoryToggle, this, true);
            shieldImage.raycastTarget = true;
        }

        if (inventoryPhase == InventoryLessonPhase.Intro || inventoryPhase == InventoryLessonPhase.Returning)
        {
            inventoryMotionElapsed += deltaTime;
            float t = Mathf.Clamp01(inventoryMotionElapsed / InventoryMotionDuration);
            float progress = Mathf.SmoothStep(0f, 1f, t);
            bool returning = inventoryPhase == InventoryLessonPhase.Returning;
            PoseInventoryButton(returning ? 1f - progress : progress);
            if (returning) visibility = 1f;
            if (t >= 1f)
            {
                if (returning)
                {
                    ReleaseInventoryGuidance();
                    showing = false;
                    visibility = 0f;
                    inputShield.gameObject.SetActive(false);
                    tutorial.CompleteInventoryTutorial();
                    TickPotionGuidance();
                    return;
                }
                inventoryPhase = InventoryLessonPhase.AwaitOpen;
                InputActionQuery.SetPressBlocked(InputActionId.InventoryToggle, this, false);
                inventoryInputArmed = !InputActionQuery.IsPressed(InputActionId.InventoryToggle);
            }
        }
        else if (inventoryPhase == InventoryLessonPhase.AwaitOpen)
        {
            if (!InputActionQuery.IsPressed(InputActionId.InventoryToggle)) inventoryInputArmed = true;
            // The ordinary inventory handler owns opening. A rejected request never advances the lesson.
            if (inventoryInputArmed && InputActionQuery.WasPressedThisFrame(InputActionId.InventoryToggle) &&
                inventoryHandler.IsInventoryOpen)
            {
                inventoryPhase = InventoryLessonPhase.Viewing;
                shieldImage.raycastTarget = false;
                SetInventoryInstructionVisible(false);
                InventoryScreen openScreen = FindFirstObjectByType<InventoryScreen>();
                lessonInventoryScreen = openScreen;
                lessonInventoryScreen?.AcquireInspectionOnlyMode(this);
                Canvas inventoryCanvas = openScreen != null ? openScreen.GetComponentInParent<Canvas>() : null;
                if (guidanceCanvas != null && inventoryCanvas != null && guidanceCanvas != inventoryCanvas)
                {
                    guidanceCanvas.sortingLayerID = inventoryCanvas.sortingLayerID;
                    guidanceCanvas.sortingOrder = inventoryCanvas.sortingOrder - 1;
                }
                return;
            }
            PoseInventoryButton(1f);
        }

        LayoutSpotlight(inventoryButtonRect, inventory: true);
        SetInventoryInstructionVisible(inventoryPhase == InventoryLessonPhase.AwaitOpen);
        ApplyPresentation();
    }

    private void PoseInventoryButton(float progress)
    {
        // Overlay and camera-space canvases do not share world units (e.g. letterboxing).
        // Measure the displacement in pixels, then project both endpoints onto the HUD plane.
        inputShield.GetWorldCorners(corners);
        Camera shieldCamera = SlotCamera(inputShield);
        Vector2 screenOffset = (RectTransformUtility.WorldToScreenPoint(shieldCamera, corners[2]) -
                                RectTransformUtility.WorldToScreenPoint(shieldCamera, corners[0])) / 3f;
        RectTransform parent = inventoryButtonRect.parent as RectTransform;
        if (parent == null) return;
        Camera hudCamera = SlotCamera(inventoryButtonRect);
        Vector2 origin = RectTransformUtility.WorldToScreenPoint(hudCamera, parent.position);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, origin, hudCamera, out Vector2 localOrigin) ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, origin + screenOffset, hudCamera, out Vector2 localTarget))
            return;
        Vector3 offset = localTarget - localOrigin;
        inventoryButtonRect.anchoredPosition3D = inventoryOriginalPosition + offset * progress;
        inventoryButtonRect.localScale = inventoryOriginalScale * Mathf.Lerp(1f, 2.5f, progress);
    }

    private void SetInventoryInstructionVisible(bool visible)
    {
        instruction.gameObject.SetActive(visible);
        rightClickGlyph.gameObject.SetActive(visible && rightClickGlyph.sprite != null);
    }

    private void RestoreGuidanceCanvas()
    {
        if (guidanceCanvas == null) return;
        guidanceCanvas.sortingLayerID = guidanceSortingLayer;
        guidanceCanvas.sortingOrder = guidanceSortingOrder;
    }

    private static InventoryOpenHudButton ResolveInventoryHudButton()
    {
        InventoryOpenHudButton inactiveCandidate = null;
        foreach (InventoryOpenHudButton button in FindObjectsByType<InventoryOpenHudButton>(
                     FindObjectsInactive.Include, FindObjectsSortMode.InstanceID))
        {
            // The result screen has its own button; only the gameplay HUD is a lesson target.
            if (button.GetComponentInParent<GameOverPresentationController>(true) != null)
                continue;

            if (button.isActiveAndEnabled)
                return button;

            inactiveCandidate ??= button;
        }

        return inactiveCandidate;
    }

    private void ReleaseInventoryGuidance()
    {
        lessonInventoryScreen?.ReleaseInspectionOnlyMode(this);
        lessonInventoryScreen = null;
        if (inventoryButtonRect != null)
        {
            inventoryButtonRect.anchoredPosition3D = inventoryOriginalPosition;
            inventoryButtonRect.localScale = inventoryOriginalScale;
            inventoryButtonRect = null;
        }
        RestoreGuidanceCanvas();
        guidanceCanvas = null;
        if (inventoryLabel != null)
        {
            inventoryLabel.fontSize = inventoryLabelFontSize;
            inventoryLabel.textWrappingMode = inventoryLabelWrapping;
            inventoryLabel.alignment = inventoryLabelAlignment;
            inventoryLabel = null;
        }
        inventoryGuidance = false;
        inventoryInputArmed = false;
        inventoryCombatInput?.SetWeaponInputBlocked(this, false);
        inventoryCombatInput = null;
        foreach (InputActionId action in InventoryBlockedActions) InputActionQuery.SetPressBlocked(action, this, false);
        InputActionQuery.SetPressBlocked(InputActionId.InventoryToggle, this, false);
        if (TimeScalePausePlayback.IsHeldBy(this)) TimeScalePausePlayback.Release(this);
        if (shieldImage != null) shieldImage.raycastTarget = false;
    }

    private bool TickPotionGuidance()
    {
        if (tutorial == null || !tutorial.isActiveAndEnabled || tutorial.Stage != 6)
        {
            if (potionGuidance) { ReleasePotionGuidance(); ReleaseInventoryGuidance(); HideGuidance(); }
            return false;
        }
        if (PlayerRuntimeRegistry.CurrentPlayer == null)
        {
            ReleasePotionGuidance(); ReleaseInventoryGuidance(); HideGuidance();
            return true;
        }
        if (!potionGuidance)
        {
            potionInventory = PlayerRuntimeRegistry.CurrentPlayer.GetComponent<PlayerConsumableInventory>();
            potionHud = FindFirstObjectByType<PlayerConsumableHUD2D>();
            if (potionInventory == null || potionHud == null || tutorialPotion == null ||
                potionRangeEndGlyph == null || potionRangeSeparator == null || potionUseGlyph == null || potionUseInstruction == null)
                return true;
            int potionIndex = -1;
            for (int i = 0; i < potionInventory.SlotCount; i++)
                if (potionInventory.GetConsumableInSlot(i) == tutorialPotion) { potionIndex = i; break; }
            if (potionIndex < 0) return true;
            for (int i = 0; i < 4; i++) if (potionHud.GetSlotRect(i) == null) return true;
            potionInventory.TrySwapConsumableSlots(0, potionIndex);
            potionInventory.ConsumableUsed += OnTutorialPotionUsed;
            potionGuidance = true;
            inventoryLabel = instruction.GetComponent<TMP_Text>();
            inventoryLabelFontSize = inventoryLabel.fontSize;
            inventoryLabelWrapping = inventoryLabel.textWrappingMode;
            inventoryLabelAlignment = inventoryLabel.alignment;
            SetInventoryInstructionVisible(false);
            SetPotionExtrasVisible(false);
            potionHudRect = potionHud.transform as RectTransform;
            potionHudOriginalPosition = potionHudRect.anchoredPosition3D;
            potionHudOriginalScale = potionHudRect.localScale;
            potionMotionElapsed = 0f;
            potionReturning = false;
            PreparePotionMotion();
            inventoryCombatInput = PlayerRuntimeRegistry.CurrentPlayer.GetComponent<PlayerCombatInput2D>();
            inventoryCombatInput?.SetWeaponInputBlocked(this, true);
            foreach (InputActionId action in InventoryBlockedActions)
                InputActionQuery.SetPressBlocked(action, this, true);
            InputActionQuery.SetPressBlocked(InputActionId.InventoryToggle, this, true);
            TimeScalePausePlayback.SetOwnedTimeScale(this, 0f);
            shieldImage = inputShield.GetComponent<Image>();
            shieldImage.raycastTarget = true;
            inputShield.gameObject.SetActive(true);
            showing = true;
            visibility = presentationFrom = 1f;
            presentationElapsed = 0f;
        }
        if (potionInventory == null || potionHud == null)
        {
            ReleasePotionGuidance(); ReleaseInventoryGuidance(); HideGuidance();
            return true;
        }
        TickPotionPresentation(Time.unscaledDeltaTime);
        return true;
    }

    private void OnTutorialPotionUsed(ConsumableDefinition used)
    {
        if (!potionGuidance || used != tutorialPotion || tutorial == null || tutorial.Stage != 6) return;
        if (potionReturning || potionMotionElapsed < InventoryMotionDuration) return;
        potionReturning = true;
        potionMotionElapsed = 0f;
        InputActionQuery.SetPressBlocked(InputActionId.ConsumableSlot1, this, true);
        SetInventoryInstructionVisible(false);
        SetPotionExtrasVisible(false);
    }

    private void PreparePotionMotion()
    {
        GetPotionBounds(out Vector2 startMin, out Vector2 startMax);
        Vector2 targetCenter = (startMin + startMax) * .5f +
            new Vector2(inputShield.rect.width, -inputShield.rect.height) / 3f;
        potionHudRect.localScale = potionHudOriginalScale * 2.5f;
        GetPotionBounds(out Vector2 min, out Vector2 max);
        Vector2 half = (max - min) * .5f;
        Rect bounds = inputShield.rect;
        targetCenter.x = Mathf.Clamp(targetCenter.x, bounds.xMin + half.x + 12f, bounds.xMax - half.x - 12f);
        targetCenter.y = Mathf.Clamp(targetCenter.y, bounds.yMin + half.y + 88f, bounds.yMax - half.y - 88f);
        // Project the scaled slot center onto the HUD plane, including camera-space canvases.
        RectTransform parent = potionHudRect.parent as RectTransform;
        Camera hudCamera = SlotCamera(potionHudRect);
        Camera shieldCamera = SlotCamera(inputShield);
        Vector2 from = RectTransformUtility.WorldToScreenPoint(shieldCamera, inputShield.TransformPoint((min + max) * .5f));
        Vector2 to = RectTransformUtility.WorldToScreenPoint(shieldCamera, inputShield.TransformPoint(targetCenter));
        potionHudTargetPosition = potionHudOriginalPosition;
        if (parent != null &&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, from, hudCamera, out Vector2 localFrom) &&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, to, hudCamera, out Vector2 localTo))
            potionHudTargetPosition += (Vector3)(localTo - localFrom);
        potionHudRect.localScale = potionHudOriginalScale;
    }

    private void TickPotionPresentation(float deltaTime)
    {
        potionMotionElapsed += deltaTime;
        float t = Mathf.Clamp01(potionMotionElapsed / InventoryMotionDuration);
        float progress = Mathf.SmoothStep(0f, 1f, t);
        if (potionReturning) progress = 1f - progress;
        potionHudRect.anchoredPosition3D = Vector3.Lerp(potionHudOriginalPosition, potionHudTargetPosition, progress);
        potionHudRect.localScale = potionHudOriginalScale * Mathf.Lerp(1f, 2.5f, progress);
        GetPotionBounds(out Vector2 min, out Vector2 max);
        LayoutShades(min, max);
        if (potionReturning && t >= 1f)
        {
            ReleasePotionGuidance();
            ReleaseInventoryGuidance();
            HideGuidance();
            tutorial.CompletePotionTutorial();
            return;
        }
        if (!potionReturning && t >= 1f)
        {
            InputActionQuery.SetPressBlocked(InputActionId.ConsumableSlot1, this, false);
            LayoutPotionGuidance();
        }
        ApplyPresentation();
    }

    private void ReleasePotionGuidance()
    {
        if (potionInventory != null) potionInventory.ConsumableUsed -= OnTutorialPotionUsed;
        potionInventory = null;
        potionGuidance = false;
        if (potionHudRect != null)
        {
            potionHudRect.anchoredPosition3D = potionHudOriginalPosition;
            potionHudRect.localScale = potionHudOriginalScale;
        }
        potionHudRect = null;
        potionHud = null;
        SetPotionExtrasVisible(false);
    }

    private void SetPotionExtrasVisible(bool visible)
    {
        if (potionRangeEndGlyph != null) potionRangeEndGlyph.gameObject.SetActive(visible);
        if (potionRangeSeparator != null) potionRangeSeparator.gameObject.SetActive(visible);
        if (potionUseGlyph != null) potionUseGlyph.gameObject.SetActive(visible);
        if (potionUseInstruction != null) potionUseInstruction.gameObject.SetActive(visible);
    }

    private void GetPotionBounds(out Vector2 min, out Vector2 max)
    {
        min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (int i = 0; i < 4; i++)
        {
            GetSpotlightBounds(potionHud.GetSlotRect(i), out Vector2 slotMin, out Vector2 slotMax);
            min = Vector2.Min(min, slotMin);
            max = Vector2.Max(max, slotMax);
        }
    }

    private void LayoutPotionGuidance()
    {
        GetPotionBounds(out Vector2 min, out Vector2 max);
        LayoutShades(min, max);
        InputBindingService bindings = InputBindingService.EnsureInstance();
        InputGlyphPresentation first = bindings.GetBindingGlyph(InputActionId.ConsumableSlot1);
        InputGlyphPresentation last = bindings.GetBindingGlyph(InputActionId.ConsumableSlot4);
        if (first.Key == KeyCode.None) first = bindings.GetBindingGlyph(InputActionId.ConsumableSlot1, true);
        if (last.Key == KeyCode.None) last = bindings.GetBindingGlyph(InputActionId.ConsumableSlot4, true);
        rightClickGlyph.sprite = potionUseGlyph.sprite = first.Icon;
        potionRangeEndGlyph.sprite = last.Icon;
        TMP_Text label = instruction.GetComponent<TMP_Text>();
        label.text = "를 눌러서 포션을 사용할 수 있습니다.";
        potionRangeSeparator.text = (first.HasIcon ? "" : first.DisplayLabel) + "~" + (last.HasIcon ? "" : last.DisplayLabel);
        potionUseInstruction.text = (first.HasIcon ? "" : first.DisplayLabel) + "을 눌러 포션 사용.";
        float captionWidth = PreparePotionText(label);
        float rangeWidth = PreparePotionText(potionRangeSeparator);
        float useWidth = PreparePotionText(potionUseInstruction);
        float firstWidth = first.HasIcon ? 56f : 0f, lastWidth = last.HasIcon ? 56f : 0f;
        float rowWidth = firstWidth + rangeWidth + lastWidth + captionWidth;
        float fit = Mathf.Min(1f, (inputShield.rect.width - 24f) / rowWidth);
        float left = Mathf.Clamp((min.x + max.x - rowWidth * fit) * .5f,
            inputShield.rect.xMin + 12f, inputShield.rect.xMax - rowWidth * fit - 12f);
        float top = max.y + 12f;
        PlacePotionGlyph(rightClickGlyph, first.HasIcon, left, top, fit);
        left += firstWidth * fit;
        potionRangeSeparator.fontSize *= fit;
        SetRect(potionRangeSeparator.rectTransform, left, top, rangeWidth * fit, 64f);
        left += rangeWidth * fit;
        PlacePotionGlyph(potionRangeEndGlyph, last.HasIcon, left, top, fit);
        left += lastWidth * fit;
        label.fontSize *= fit;
        SetRect(instruction, left, top, captionWidth * fit, 64f);
        float useRowWidth = firstWidth + useWidth;
        float useFit = Mathf.Min(1f, (inputShield.rect.width - 24f) / useRowWidth);
        left = Mathf.Clamp((min.x + max.x - useRowWidth * useFit) * .5f,
            inputShield.rect.xMin + 12f, inputShield.rect.xMax - useRowWidth * useFit - 12f);
        PlacePotionGlyph(potionUseGlyph, first.HasIcon, left, min.y - 76f, useFit);
        potionUseInstruction.fontSize *= useFit;
        SetRect(potionUseInstruction.rectTransform, left + firstWidth * useFit, min.y - 76f, useWidth * useFit, 64f);
        glyphPosition = rightClickGlyph.rectTransform.anchoredPosition;
        instructionPosition = instruction.anchoredPosition;
    }

    private static float PreparePotionText(TMP_Text label)
    {
        label.fontSize = 28f;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        return label.GetPreferredValues(label.text, Mathf.Infinity, Mathf.Infinity).x + 6f;
    }

    private void PlacePotionGlyph(Image image, bool visible, float x, float y, float fit)
    {
        image.gameObject.SetActive(visible);
        if (visible) SetRect(image.rectTransform, x, y + (64f - 48f * fit) * .5f, 48f * fit, 48f * fit);
    }

    private void SetWorldGuidance(bool visible)
    {
        if (worldHighlight != visible)
        {
            worldHighlight = visible;
            worldHighlightStarted = Time.unscaledTime;
            worldChest?.SetGuidanceHighlight(this, visible);
        }
        if (worldArrow == null) return;
        worldArrow.gameObject.SetActive(visible);
        if (!visible || worldChest == null) return;
        // Match HubWeaponDepartureGuide's authored arrow offset and unscaled bounce.
        float bounce = Mathf.Abs(Mathf.Sin((Time.unscaledTime - worldHighlightStarted) / .6f * Mathf.PI)) * .2f;
        Transform anchor = worldChest.GetPromptAnchor();
        worldArrow.position = (anchor != null ? anchor.position : worldChest.transform.position) + Vector3.up * (1.2f + bounce);
        worldArrow.rotation = Quaternion.identity;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!locked || screen == null || !screen.IsSelectionReady ||
            (eventData.button != PointerEventData.InputButton.Left &&
             eventData.button != PointerEventData.InputButton.Right)) return;
        ItemSlotUI slot = screen.FindVisibleSlot(tutorialPotion);
        if (slot == null || !RectTransformUtility.RectangleContainsScreenPoint(slot.SlotRect,
                eventData.position, SlotCamera(slot.SlotRect))) return;
        if (!screen.TrySelectGuidedSlot(this, slot)) return;
        ReleaseInput();
        HideGuidance();
        pulseStarted = Time.unscaledTime;
    }

    private static Camera SlotCamera(RectTransform slot)
    {
        Canvas canvas = slot.GetComponentInParent<Canvas>()?.rootCanvas;
        return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
    }

    private void GetSpotlightBounds(RectTransform slot, out Vector2 min, out Vector2 max)
    {
        slot.GetWorldCorners(corners);
        min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        Camera camera = SlotCamera(slot);
        foreach (Vector3 corner in corners)
        {
            Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corner);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(inputShield, point, SlotCamera(inputShield), out Vector2 local);
            min = Vector2.Min(min, local);
            max = Vector2.Max(max, local);
        }
    }

    private void LayoutShades(Vector2 min, Vector2 max)
    {
        Rect bounds = inputShield.rect;
        min = Vector2.Max(min, bounds.min);
        max = Vector2.Min(max, bounds.max);
        SetRect(shadePanels[0], bounds.xMin, max.y, bounds.width, bounds.yMax - max.y);
        SetRect(shadePanels[1], bounds.xMin, bounds.yMin, bounds.width, min.y - bounds.yMin);
        SetRect(shadePanels[2], bounds.xMin, min.y, min.x - bounds.xMin, max.y - min.y);
        SetRect(shadePanels[3], max.x, min.y, bounds.xMax - max.x, max.y - min.y);
    }

    private void LayoutSpotlight(RectTransform slot, bool inventory = false)
    {
        GetSpotlightBounds(slot, out Vector2 min, out Vector2 max);
        LayoutShades(min, max);
        Rect bounds = inputShield.rect;
        InputGlyphPresentation glyph;
        if (inventory)
        {
            InputBindingService bindings = InputBindingService.EnsureInstance();
            glyph = bindings.GetBindingGlyph(InputActionId.InventoryToggle);
            if (glyph.Key == KeyCode.None) glyph = bindings.GetBindingGlyph(InputActionId.InventoryToggle, true);
        }
        else glyph = InputGlyphDatabase.Resolve(KeyCode.Mouse0);
        rightClickGlyph.sprite = glyph.Icon;
        TMP_Text label = instruction.GetComponent<TMP_Text>();
        if (label != null) label.text = inventory
            ? (glyph.HasIcon ? "를 눌러 인벤토리를 열 수 있습니다." : glyph.DisplayLabel + "를 눌러 인벤토리를 열 수 있습니다.")
            : "포션을 좌클릭하여 선택하세요";
        if (inventory && label != null)
        {
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.fontSize = inventoryLabelFontSize;
            float textWidth = label.GetPreferredValues(label.text, Mathf.Infinity, Mathf.Infinity).x + 4f;
            float glyphWidth = glyph.HasIcon ? 56f : 0f;
            float fit = Mathf.Min(1f, (bounds.width - 24f) / (textWidth + glyphWidth));
            label.fontSize = inventoryLabelFontSize * fit;
            float rowWidth = (textWidth + glyphWidth) * fit;
            float left = Mathf.Clamp((min.x + max.x - rowWidth) * .5f, bounds.xMin + 12f, bounds.xMax - rowWidth - 12f);
            float bottom = Mathf.Min(max.y + 24f, bounds.yMax - 70f);
            SetRect(instruction, left + glyphWidth * fit, bottom, textWidth * fit, 70f);
            if (glyph.HasIcon) SetRect(rightClickGlyph.rectTransform, left, bottom + 11f, 48f * fit, 48f * fit);
            glyphPosition = rightClickGlyph.rectTransform.anchoredPosition;
            instructionPosition = instruction.anchoredPosition;
            return;
        }
        float instructionWidth = inventory ? 560f : 400f;
        float centerX = Mathf.Clamp((min.x + max.x) * .5f,
            bounds.xMin + instructionWidth * .5f + 12f, bounds.xMax - instructionWidth * .5f - 12f);
        rightClickGlyph.gameObject.SetActive(rightClickGlyph.sprite != null);
        if (rightClickGlyph.sprite != null)
            SetRect(rightClickGlyph.rectTransform, centerX - 24f, max.y + 10f, 48f, 48f);
        SetRect(instruction, centerX - instructionWidth * .5f, max.y + 62f, instructionWidth, 70f);
        glyphPosition = rightClickGlyph.rectTransform.anchoredPosition;
        instructionPosition = instruction.anchoredPosition;
    }

    private void TickPresentation(float deltaTime)
    {
        if (!inputShield.gameObject.activeSelf) return;
        if (inventoryGuidance && inventoryPhase == InventoryLessonPhase.Returning) return;
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
        ReleasePotionGuidance();
        ReleaseInventoryGuidance();
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
        if (screen != null) screen.ReleaseSelectionRequirement(this);
        screen = null;
        confirm = null;
    }
}
