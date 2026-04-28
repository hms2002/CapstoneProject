using System.Collections;
using CapstonePresentation;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// First-open chest UI reveal for the authored Top/Middle/Down chest frame.
/// The grid defines the middle size; top/down follow its width and keep their height.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class ChestFirstOpenRevealPresentation : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform chestPanel;
    [SerializeField] private RectTransform inventoryPanel;
    [SerializeField] private RectTransform motionBounds;
    [SerializeField] private RectTransform topAnimRoot;
    [SerializeField] private RectTransform middleRevealSlot;
    [SerializeField] private RectTransform middleViewport;
    [SerializeField] private RectTransform middleContent;
    [SerializeField] private CanvasGroup interactionCanvasGroup;

    [Header("Post Reveal Presentation")]
    [SerializeField] private RectTransform postRevealSlideFadeTarget;
    [SerializeField] private UISlideFadePresentation postRevealSlideFadePresentation;
    [SerializeField] private bool playPostRevealSlideFade = true;

    [Header("Layout References")]
    [SerializeField] private RectTransform topSlot;
    [SerializeField] private RectTransform topFrame;
    [SerializeField] private RectTransform middleFrame;
    [SerializeField] private RectTransform gridRoot;
    [SerializeField] private RectTransform downFrame;

    [Header("Motion")]
    [SerializeField, Min(0f)] private float revealDuration = 0.34f;
    [SerializeField, Min(0f)] private float topLiftDistance = 86f;
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private bool blockInteractionDuringReveal = true;

    [Header("Entry Motion")]
    [SerializeField, Min(0f)] private float sideApproachDuration = 0.24f;
    [SerializeField, Range(0f, 0.6f)] private float resistanceTravelFraction = 0.18f;
    [SerializeField, Min(0f)] private float resistancePulseAmplitude = 18f;
    [SerializeField, Min(1)] private int resistancePulseCount = 2;
    [SerializeField, Min(0f)] private float impactRushDuration = 0.1f;
    [SerializeField, Min(0f)] private float impactPauseDuration = 0.04f;
    [SerializeField, Min(0f)] private float settleDuration = 0.34f;
    [SerializeField, Min(0f)] private float offscreenPadding = 96f;

    [Header("Impact Feedback")]
    [SerializeField] private bool playImpactCameraShake = true;
    [SerializeField] private WorldPresentationHook impactPresentation;
    [SerializeField, Min(0f)] private float impactCameraShakeAmplitude = 0.08f;
    [SerializeField, Min(0f)] private float impactCameraShakeDuration = 0.12f;
    [SerializeField] private RectTransform impactPresentationAnchor;
    [SerializeField] private ParticleSystem[] impactParticleSystems;
    [SerializeField] private RectTransform impactShakeRoot;

    [Header("Impact UI Particles")]
    [SerializeField] private bool playImpactUiParticles = true;
    [SerializeField] private UIParticleEmitter impactUiParticleEmitter;
    [SerializeField] private Vector2 impactUiParticleOffset;

    [Header("Impact UI Shake")]
    [SerializeField, Min(0f)] private float uiImpactShakeDuration = 0.16f;
    [SerializeField, Min(0f)] private float uiImpactShakeAmplitude = 16f;
    [SerializeField, Min(1)] private int uiImpactShakeFrequency = 5;

    [Header("Layout")]
    [SerializeField] private bool applyLayoutInEditMode = true;
    [SerializeField] private bool disableOuterLayoutDrivers = true;
    [SerializeField, Min(1)] private int previewSlotCount = 6;
    [SerializeField, Min(0f)] private float fallbackTopHeight = 84.55f;
    [SerializeField, Min(0f)] private float fallbackDownHeight = 84.55f;

    [Header("Fallback Search Names")]
    [SerializeField] private string chestPanelName = "ChestPanel";
    [SerializeField] private string inventoryPanelName = "InventoryElementPannel";
    [SerializeField] private string topSlotName = "TopSlot";
    [SerializeField] private string topAnimRootName = "TopAnimRoot";
    [SerializeField] private string topFrameName = "TopChestFrame";
    [SerializeField] private string middleRevealSlotName = "MiddleRevealSlot";
    [SerializeField] private string middleViewportName = "MiddleViewport";
    [SerializeField] private string middleContentName = "MiddleContent";
    [SerializeField] private string middleFrameName = "MiddleFrame";
    [SerializeField] private string gridRootName = "ChestGridRoot";
    [SerializeField] private string downFrameName = "DownChestFrame";
    [SerializeField] private string postRevealSlideFadeTargetName = "PlayerStatUI";
    [SerializeField] private string impactPresentationAnchorName = "ImpactPresentationAnchor";
    [SerializeField] private string impactShakeRootName = "ImpactShakeRoot";
    [SerializeField] private string impactUiParticleEmitterName = "ImpactUiParticleEmitter";

    private Coroutine activeRoutine;
    private Coroutine impactShakeRoutine;
    private Vector2 chestPanelOpenPosition;
    private Vector2 inventoryPanelOpenPosition;
    private Vector2 impactShakeRootOpenPosition;
    private bool hasCapturedPanelOpenPositions;
    private bool hasCapturedImpactShakeRootPosition;

    private readonly struct LayoutMetrics
    {
        public readonly float Width;
        public readonly float TopHeight;
        public readonly float MiddleHeight;
        public readonly float DownHeight;
        public readonly Vector2 GridSize;

        public LayoutMetrics(float width, float topHeight, float middleHeight, float downHeight, Vector2 gridSize)
        {
            Width = width;
            TopHeight = topHeight;
            MiddleHeight = middleHeight;
            DownHeight = downHeight;
            GridSize = gridSize;
        }
    }

    private void Reset()
    {
        ResolveReferences();
        ApplyOpenedPose();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (!Application.isPlaying && applyLayoutInEditMode)
            ApplyOpenedPose();
    }

    private void OnValidate()
    {
        ResolveReferences();

        if (!Application.isPlaying && applyLayoutInEditMode)
            ApplyOpenedPose();
    }

#if UNITY_EDITOR
    private void LateUpdate()
    {
        if (Application.isPlaying || !applyLayoutInEditMode)
            return;

        ResolveReferences();
        ApplyOpenedPose();
    }
#endif

    private void OnDisable()
    {
        StopActiveRoutine();
    }

    public void PlayOpen()
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        StopActiveRoutine();
        ResolveReferences();
        CapturePanelOpenPositions(force: false);
        CaptureImpactShakeRootPosition(force: false);
        PreparePostRevealSlideFade();
        ApplyRevealPose(0f);

        if (!CanPlaySideEntry())
        {
            PlayRevealOnly();
            return;
        }

        SetInteractionEnabled(false);
        activeRoutine = StartCoroutine(PlaySideEntryRevealRoutine());
    }

    public void SnapOpen()
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        StopActiveRoutine();
        ResolveReferences();
        CapturePanelOpenPositions(force: false);
        CaptureImpactShakeRootPosition(force: false);
        ApplyPanelPositions(chestPanelOpenPosition, inventoryPanelOpenPosition);
        ApplyOpenedPose();
        SnapPostRevealSlideFadeOpen();
    }

    public void ConfigurePanels(
        RectTransform chestPanelOverride,
        RectTransform inventoryPanelOverride,
        RectTransform postRevealTargetOverride = null)
    {
        if (chestPanelOverride != null)
            chestPanel = chestPanelOverride;

        if (inventoryPanelOverride != null)
            inventoryPanel = inventoryPanelOverride;

        if (postRevealTargetOverride != null)
        {
            postRevealSlideFadeTarget = postRevealTargetOverride;
            postRevealSlideFadePresentation = null;
        }

        hasCapturedPanelOpenPositions = false;
    }

    private IEnumerator PlaySideEntryRevealRoutine()
    {
        SideEntryPose pose = ResolveSideEntryPose();
        ApplyPanelPositions(pose.ChestStart, pose.InventoryStart);
        ApplyRevealPose(0f);

        yield return AnimateResistanceEntry(pose);

        yield return AnimatePanels(
            pose.ChestResistance,
            pose.ChestCollision,
            pose.InventoryResistance,
            pose.InventoryCollision,
            impactRushDuration,
            EaseInCubic);

        PlayImpactFeedback();

        if (impactPauseDuration > 0f)
        {
            if (useUnscaledTime)
                yield return new WaitForSecondsRealtime(impactPauseDuration);
            else
                yield return new WaitForSeconds(impactPauseDuration);
        }

        float duration = Mathf.Max(settleDuration, revealDuration);
        if (duration <= 0f)
        {
            ApplyPanelPositions(pose.ChestFinal, pose.InventoryFinal);
            ApplyOpenedPose();
            PlayPostRevealSlideFadeOpen();
            activeRoutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float settleT = settleDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / settleDuration);
            float revealT = revealDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / revealDuration);
            float easedSettle = EaseOutCubic(settleT);

            ApplyPanelPositions(
                Vector2.LerpUnclamped(pose.ChestCollision, pose.ChestFinal, easedSettle),
                Vector2.LerpUnclamped(pose.InventoryCollision, pose.InventoryFinal, easedSettle));
            ApplyRevealPose(SmoothStep(revealT));
            yield return null;
        }

        ApplyPanelPositions(pose.ChestFinal, pose.InventoryFinal);
        ApplyOpenedPose();
        PlayPostRevealSlideFadeOpen();
        activeRoutine = null;
    }

    private IEnumerator PlayRevealRoutine()
    {
        float elapsed = 0f;

        while (elapsed < revealDuration)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / revealDuration);
            ApplyRevealPose(SmoothStep(t));
            yield return null;
        }

        ApplyOpenedPose();
        PlayPostRevealSlideFadeOpen();
        activeRoutine = null;
    }

    private IEnumerator AnimatePanels(
        Vector2 chestFrom,
        Vector2 chestTo,
        Vector2 inventoryFrom,
        Vector2 inventoryTo,
        float duration,
        System.Func<float, float> ease)
    {
        if (duration <= 0f)
        {
            ApplyPanelPositions(chestTo, inventoryTo);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = ease != null ? ease(t) : t;
            ApplyPanelPositions(
                Vector2.LerpUnclamped(chestFrom, chestTo, eased),
                Vector2.LerpUnclamped(inventoryFrom, inventoryTo, eased));
            ApplyRevealPose(0f);
            yield return null;
        }

        ApplyPanelPositions(chestTo, inventoryTo);
    }

    private void ResolveReferences()
    {
        if (interactionCanvasGroup == null)
            interactionCanvasGroup = GetComponent<CanvasGroup>();

        if (chestPanel == null)
            chestPanel = FindChildRect(chestPanelName, transform);
        if (inventoryPanel == null)
            inventoryPanel = FindChildRect(inventoryPanelName, transform);
        if (motionBounds == null)
            motionBounds = transform as RectTransform;
        if (topSlot == null)
            topSlot = FindChildRect(topSlotName, transform);
        if (topAnimRoot == null)
            topAnimRoot = FindChildRect(topAnimRootName, transform);
        if (topFrame == null)
            topFrame = FindChildRect(topFrameName, transform);
        if (middleRevealSlot == null)
            middleRevealSlot = FindChildRect(middleRevealSlotName, transform);
        if (middleViewport == null)
            middleViewport = FindChildRect(middleViewportName, transform);
        if (middleContent == null)
            middleContent = FindChildRect(middleContentName, transform);
        if (middleFrame == null)
            middleFrame = FindChildRect(middleFrameName, transform);
        if (gridRoot == null)
            gridRoot = FindChildRect(gridRootName, transform);
        if (downFrame == null)
            downFrame = FindChildRect(downFrameName, transform);
        if (postRevealSlideFadeTarget == null)
            postRevealSlideFadeTarget = FindChildRect(postRevealSlideFadeTargetName, transform);
        if (impactPresentationAnchor == null)
            impactPresentationAnchor = FindChildRect(impactPresentationAnchorName, transform);
        if (impactShakeRoot == null)
            impactShakeRoot = ResolveDefaultImpactShakeRoot();
        if (impactUiParticleEmitter == null)
            impactUiParticleEmitter = ResolveImpactUiParticleEmitter(createIfMissing: false);

        ResolvePostRevealSlideFadePresentation(createIfMissing: false);
        ConfigureOuterLayoutDrivers();
    }

    private void ConfigureOuterLayoutDrivers()
    {
        if (!disableOuterLayoutDrivers)
            return;

        DisableComponent<ContentSizeFitter>(chestPanel);
        DisableComponent<LayoutGroup>(chestPanel);
        DisableComponent<LayoutElement>(topSlot);
        DisableComponent<LayoutElement>(middleRevealSlot);
        DisableComponent<LayoutGroup>(middleRevealSlot);
        DisableComponent<LayoutGroup>(middleViewport);
        DisableComponent<LayoutGroup>(middleContent);
        DisableComponent<LayoutGroup>(topFrame);
        DisableComponent<LayoutGroup>(middleFrame);
        DisableComponent<LayoutGroup>(downFrame);
        DisableComponent<LayoutElement>(downFrame);
    }

    private void ApplyOpenedPose()
    {
        ApplyRevealPose(1f);
        SetInteractionEnabled(true);
    }

    private void PlayRevealOnly()
    {
        if (revealDuration <= 0f)
        {
            ApplyOpenedPose();
            PlayPostRevealSlideFadeOpen();
            return;
        }

        SetInteractionEnabled(false);
        activeRoutine = StartCoroutine(PlayRevealRoutine());
    }

    private void ApplyRevealPose(float t)
    {
        t = Mathf.Clamp01(t);
        LayoutMetrics metrics = ResolveLayoutMetrics();
        float revealedMiddleHeight = metrics.MiddleHeight * t;
        float totalHeight = metrics.TopHeight + revealedMiddleHeight + metrics.DownHeight;

        SetSize(chestPanel, metrics.Width, totalHeight);

        if (chestPanel != null)
        {
            SetStackChild(middleRevealSlot, metrics.Width, revealedMiddleHeight, totalHeight, metrics.TopHeight);
            SetStackChild(downFrame, metrics.Width, metrics.DownHeight, totalHeight, metrics.TopHeight + revealedMiddleHeight);
            SetStackChild(topSlot, metrics.Width, metrics.TopHeight, totalHeight, 0f);
        }

        SetStretch(topAnimRoot);
        SetStretch(middleViewport);
        SetTopStretch(middleContent, metrics.MiddleHeight);
        SetStretch(topFrame);
        SetStretch(middleFrame);

        SetSize(topFrame, metrics.Width, metrics.TopHeight);
        SetSize(middleContent, metrics.Width, metrics.MiddleHeight);
        SetSize(middleFrame, metrics.Width, metrics.MiddleHeight);
        SetSize(downFrame, metrics.Width, metrics.DownHeight);
        SetSize(gridRoot, metrics.GridSize.x, metrics.GridSize.y);
        ArrangeThreePartFrame(topFrame, metrics.Width, metrics.TopHeight, null, Vector2.zero);
        ArrangeThreePartFrame(middleFrame, metrics.Width, metrics.MiddleHeight, gridRoot, metrics.GridSize);
        ArrangeThreePartFrame(downFrame, metrics.Width, metrics.DownHeight, null, Vector2.zero);

        if (topAnimRoot != null)
            topAnimRoot.anchoredPosition = new Vector2(0f, topLiftDistance * t);

        if (topSlot != null)
            topSlot.SetAsLastSibling();

        ForceRebuild(gridRoot);
        ForceRebuild(middleFrame);
        ForceRebuild(topFrame);
        ForceRebuild(downFrame);
        ForceRebuild(chestPanel);
    }

    private LayoutMetrics ResolveLayoutMetrics()
    {
        Vector2 gridSize = ResolveGridSize();
        float middleWidth = ResolveHorizontalFrameWidth(middleFrame, gridRoot, gridSize.x);
        float middleHeight = ResolveHorizontalFrameHeight(middleFrame, gridRoot, gridSize.y);
        float topHeight = ResolveFrameHeight(topSlot, topFrame, fallbackTopHeight);
        float downHeight = ResolveFrameHeight(downFrame, null, fallbackDownHeight);
        float topMinimumWidth = ResolveHorizontalFrameWidth(topFrame, null, 0f);
        float downMinimumWidth = ResolveHorizontalFrameWidth(downFrame, null, 0f);
        float width = Mathf.Max(middleWidth, topMinimumWidth, downMinimumWidth, 1f);

        return new LayoutMetrics(width, topHeight, middleHeight, downHeight, gridSize);
    }

    private bool CanPlaySideEntry()
    {
        return chestPanel != null && inventoryPanel != null && motionBounds != null;
    }

    private void CapturePanelOpenPositions(bool force)
    {
        if (hasCapturedPanelOpenPositions && !force)
            return;

        if (chestPanel != null)
            chestPanelOpenPosition = chestPanel.anchoredPosition;

        if (inventoryPanel != null)
            inventoryPanelOpenPosition = inventoryPanel.anchoredPosition;

        hasCapturedPanelOpenPositions = chestPanel != null || inventoryPanel != null;
    }

    private void CaptureImpactShakeRootPosition(bool force)
    {
        if (hasCapturedImpactShakeRootPosition && !force)
            return;

        if (impactShakeRoot == null)
            impactShakeRoot = ResolveDefaultImpactShakeRoot();

        if (impactShakeRoot == null)
            return;

        impactShakeRootOpenPosition = impactShakeRoot.anchoredPosition;
        hasCapturedImpactShakeRootPosition = true;
    }

    private void ApplyPanelPositions(Vector2 chestPosition, Vector2 inventoryPosition)
    {
        if (chestPanel != null)
            chestPanel.anchoredPosition = chestPosition;

        if (inventoryPanel != null)
            inventoryPanel.anchoredPosition = inventoryPosition;
    }

    private SideEntryPose ResolveSideEntryPose()
    {
        ForceRebuild(transform as RectTransform);
        Canvas.ForceUpdateCanvases();

        float chestWidth = ResolveElementWidth(chestPanel);
        float inventoryWidth = ResolveElementWidth(inventoryPanel);
        RectTransform chestParent = chestPanel != null ? chestPanel.parent as RectTransform : null;
        RectTransform inventoryParent = inventoryPanel != null ? inventoryPanel.parent as RectTransform : null;

        float chestStartRightEdge = ResolveBoundsX(motionBounds, chestParent, motionBounds.rect.xMin) - offscreenPadding;
        float chestCollisionRightEdge = ResolveBoundsX(motionBounds, chestParent, motionBounds.rect.center.x);
        float inventoryStartLeftEdge = ResolveBoundsX(motionBounds, inventoryParent, motionBounds.rect.xMax) + offscreenPadding;
        float inventoryCollisionLeftEdge = ResolveBoundsX(motionBounds, inventoryParent, motionBounds.rect.center.x);

        Vector2 chestStart = new Vector2(
            AnchoredXForRightEdge(chestPanel, chestStartRightEdge, chestWidth),
            chestPanelOpenPosition.y);
        Vector2 chestCollision = new Vector2(
            AnchoredXForRightEdge(chestPanel, chestCollisionRightEdge, chestWidth),
            chestPanelOpenPosition.y);
        Vector2 inventoryStart = new Vector2(
            AnchoredXForLeftEdge(inventoryPanel, inventoryStartLeftEdge, inventoryWidth),
            inventoryPanelOpenPosition.y);
        Vector2 inventoryCollision = new Vector2(
            AnchoredXForLeftEdge(inventoryPanel, inventoryCollisionLeftEdge, inventoryWidth),
            inventoryPanelOpenPosition.y);
        Vector2 chestResistance = Vector2.LerpUnclamped(chestStart, chestCollision, resistanceTravelFraction);
        Vector2 inventoryResistance = Vector2.LerpUnclamped(inventoryStart, inventoryCollision, resistanceTravelFraction);

        return new SideEntryPose(
            chestStart,
            chestResistance,
            chestCollision,
            chestPanelOpenPosition,
            inventoryStart,
            inventoryResistance,
            inventoryCollision,
            inventoryPanelOpenPosition);
    }

    private IEnumerator AnimateResistanceEntry(SideEntryPose pose)
    {
        if (sideApproachDuration <= 0f)
        {
            ApplyPanelPositions(pose.ChestResistance, pose.InventoryResistance);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < sideApproachDuration)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / sideApproachDuration);
            float eased = SmoothStep(t);

            Vector2 chestBase = Vector2.LerpUnclamped(pose.ChestStart, pose.ChestResistance, eased);
            Vector2 inventoryBase = Vector2.LerpUnclamped(pose.InventoryStart, pose.InventoryResistance, eased);

            ApplyPanelPositions(
                ApplyResistancePullback(chestBase, pose.ChestStart, pose.ChestCollision, t),
                ApplyResistancePullback(inventoryBase, pose.InventoryStart, pose.InventoryCollision, t));
            ApplyRevealPose(0f);
            yield return null;
        }

        ApplyPanelPositions(pose.ChestResistance, pose.InventoryResistance);
    }

    private Vector2 ApplyResistancePullback(Vector2 basePosition, Vector2 start, Vector2 collision, float t)
    {
        if (resistancePulseAmplitude <= 0f || resistancePulseCount <= 0)
            return basePosition;

        Vector2 inwardDirection = collision - start;
        if (inwardDirection.sqrMagnitude <= 0.0001f)
            return basePosition;

        inwardDirection.Normalize();
        float pulse = Mathf.Abs(Mathf.Sin(t * Mathf.PI * resistancePulseCount));
        float envelope = Mathf.Sin(t * Mathf.PI);
        float pullback = resistancePulseAmplitude * pulse * envelope;
        return basePosition - inwardDirection * pullback;
    }

    private Vector2 ResolveGridSize()
    {
        if (gridRoot == null)
            return Vector2.zero;

        GridLayoutGroup grid = gridRoot.GetComponent<GridLayoutGroup>();
        if (grid == null)
            return new Vector2(ResolveElementWidth(gridRoot), ResolveElementHeight(gridRoot));

        int count = CountActiveChildren(gridRoot);
        if (count <= 0)
            count = previewSlotCount;

        int columns;
        int rows;
        int constraintCount = Mathf.Max(1, grid.constraintCount);

        switch (grid.constraint)
        {
            case GridLayoutGroup.Constraint.FixedColumnCount:
                columns = constraintCount;
                rows = Mathf.CeilToInt(count / (float)columns);
                break;
            case GridLayoutGroup.Constraint.FixedRowCount:
                rows = constraintCount;
                columns = Mathf.CeilToInt(count / (float)rows);
                break;
            default:
                columns = Mathf.CeilToInt(Mathf.Sqrt(count));
                rows = Mathf.CeilToInt(count / (float)columns);
                break;
        }

        columns = Mathf.Max(1, columns);
        rows = Mathf.Max(1, rows);

        RectOffset padding = grid.padding;
        float width = padding.left + padding.right
            + columns * grid.cellSize.x
            + Mathf.Max(0, columns - 1) * grid.spacing.x;
        float height = padding.top + padding.bottom
            + rows * grid.cellSize.y
            + Mathf.Max(0, rows - 1) * grid.spacing.y;

        return new Vector2(Mathf.Max(1f, width), Mathf.Max(1f, height));
    }

    private static int CountActiveChildren(RectTransform root)
    {
        int count = 0;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child != null && child.gameObject.activeSelf)
                count++;
        }

        return count;
    }

    private static float ResolveHorizontalFrameWidth(RectTransform frame, RectTransform measuredChild, float measuredChildWidth)
    {
        if (frame == null)
            return 0f;

        HorizontalOrVerticalLayoutGroup layout = frame.GetComponent<HorizontalOrVerticalLayoutGroup>();
        RectOffset padding = layout != null ? layout.padding : null;
        float width = padding != null ? padding.left + padding.right : 0f;
        int activeChildCount = 0;

        for (int i = 0; i < frame.childCount; i++)
        {
            RectTransform child = frame.GetChild(i) as RectTransform;
            if (child == null || !child.gameObject.activeSelf)
                continue;

            activeChildCount++;
            width += child == measuredChild ? measuredChildWidth : ResolveElementWidth(child);
        }

        if (layout != null && activeChildCount > 1)
            width += layout.spacing * (activeChildCount - 1);

        return width;
    }

    private static float ResolveHorizontalFrameHeight(RectTransform frame, RectTransform measuredChild, float measuredChildHeight)
    {
        if (frame == null)
            return 0f;

        HorizontalOrVerticalLayoutGroup layout = frame.GetComponent<HorizontalOrVerticalLayoutGroup>();
        RectOffset padding = layout != null ? layout.padding : null;
        float height = padding != null ? padding.top + padding.bottom : 0f;
        float childHeight = 0f;

        for (int i = 0; i < frame.childCount; i++)
        {
            RectTransform child = frame.GetChild(i) as RectTransform;
            if (child == null || !child.gameObject.activeSelf)
                continue;

            float resolvedHeight = child == measuredChild ? measuredChildHeight : ResolveElementHeight(child);
            childHeight = Mathf.Max(childHeight, resolvedHeight);
        }

        return Mathf.Max(1f, height + childHeight);
    }

    private static float ResolveFrameHeight(RectTransform slot, RectTransform frame, float fallback)
    {
        float slotHeight = ResolveElementHeight(slot);
        if (slotHeight > 0f)
            return slotHeight;

        float frameHeight = ResolveHorizontalFrameHeight(frame, null, 0f);
        if (frameHeight > 0f)
            return frameHeight;

        frameHeight = ResolveElementHeight(frame);
        if (frameHeight > 0f)
            return frameHeight;

        return Mathf.Max(1f, fallback);
    }

    private static void ArrangeThreePartFrame(RectTransform frame, float width, float height, RectTransform fixedCenter, Vector2 fixedCenterSize)
    {
        if (!TryGetThreePartChildren(frame, out RectTransform left, out RectTransform center, out RectTransform right))
            return;

        float leftWidth = ResolveElementWidth(left);
        float rightWidth = ResolveElementWidth(right);
        float centerWidth = center == fixedCenter
            ? fixedCenterSize.x
            : Mathf.Max(0f, width - leftWidth - rightWidth);
        float centerHeight = center == fixedCenter
            ? fixedCenterSize.y
            : height;

        SetLeftAnchored(left, 0f, leftWidth, height);
        SetLeftAnchored(center, leftWidth, centerWidth, centerHeight);
        SetLeftAnchored(right, Mathf.Max(0f, width - rightWidth), rightWidth, height);
    }

    private static bool TryGetThreePartChildren(
        RectTransform frame,
        out RectTransform left,
        out RectTransform center,
        out RectTransform right)
    {
        left = null;
        center = null;
        right = null;

        if (frame == null)
            return false;

        for (int i = 0; i < frame.childCount; i++)
        {
            RectTransform child = frame.GetChild(i) as RectTransform;
            if (child == null || !child.gameObject.activeSelf)
                continue;

            if (left == null)
                left = child;
            else if (center == null)
                center = child;
            else
            {
                right = child;
                return true;
            }
        }

        return false;
    }

    private static float ResolveElementWidth(RectTransform rect)
    {
        if (rect == null)
            return 0f;

        float preferredWidth = LayoutUtility.GetPreferredWidth(rect);
        if (preferredWidth > 0f)
            return preferredWidth;

        if (rect.rect.width > 0f)
            return rect.rect.width;

        return rect.sizeDelta.x > 0f ? rect.sizeDelta.x : 0f;
    }

    private static float ResolveBoundsX(RectTransform source, RectTransform targetParent, float sourceLocalX)
    {
        if (source == null || targetParent == null)
            return sourceLocalX;

        Vector3 worldPoint = source.TransformPoint(new Vector3(sourceLocalX, source.rect.center.y, 0f));
        return targetParent.InverseTransformPoint(worldPoint).x;
    }

    private static float AnchoredXForRightEdge(RectTransform rect, float rightEdgeX, float width)
    {
        float pivotX = rect != null ? rect.pivot.x : 0.5f;
        return rightEdgeX - (1f - pivotX) * width;
    }

    private static float AnchoredXForLeftEdge(RectTransform rect, float leftEdgeX, float width)
    {
        float pivotX = rect != null ? rect.pivot.x : 0.5f;
        return leftEdgeX + pivotX * width;
    }

    private static float ResolveElementHeight(RectTransform rect)
    {
        if (rect == null)
            return 0f;

        float preferredHeight = LayoutUtility.GetPreferredHeight(rect);
        if (preferredHeight > 0f)
            return preferredHeight;

        if (rect.rect.height > 0f)
            return rect.rect.height;

        return rect.sizeDelta.y > 0f ? rect.sizeDelta.y : 0f;
    }

    private static void SetStackChild(RectTransform rect, float width, float height, float totalHeight, float yFromTop)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, totalHeight * 0.5f - yFromTop);
        SetSize(rect, width, height);
    }

    private static void SetStretch(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private static void SetTopStretch(RectTransform rect, float height)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0f, height);
    }

    private static void SetLeftAnchored(RectTransform rect, float x, float width, float height)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(x, 0f);
        SetSize(rect, width, height);
    }

    private static void SetSize(RectTransform rect, float width, float height)
    {
        if (rect == null)
            return;

        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(0f, width));
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(0f, height));
    }

    private void SetInteractionEnabled(bool enabled)
    {
        if (!Application.isPlaying || !blockInteractionDuringReveal || interactionCanvasGroup == null)
            return;

        interactionCanvasGroup.interactable = enabled;
        interactionCanvasGroup.blocksRaycasts = enabled;
    }

    private void PlayImpactFeedback()
    {
        PlayImpactPresentationHook();
        PlayImpactCameraShakeIfNeeded();
        PlayImpactParticleSystems();
        PlayImpactUiParticles();
        PlayUiImpactShake();
    }

    private void PlayImpactPresentationHook()
    {
        WorldPresentationHook presentation = impactPresentation;
        presentation.cameraShake = default;

        if (!presentation.HasAnyContent)
            return;

        WorldPresentationContext context = WorldPresentationContext.AtWorld(
            instigator: gameObject,
            position: ResolveImpactWorldPosition(),
            fallbackDirection: Vector3.up,
            target: gameObject,
            sourceObject: this,
            rotation: ResolveImpactWorldRotation(),
            causer: gameObject);

        WorldPresentationRuntime.Play(presentation, context);
    }

    private void PlayImpactCameraShakeIfNeeded()
    {
        if (!playImpactCameraShake || impactCameraShakeAmplitude <= 0f || impactCameraShakeDuration <= 0f)
            return;

        CameraShakeService.Play(new CameraShakeRequest(
            impactCameraShakeAmplitude,
            Vector3.up,
            gameObject,
            minIntervalSeconds: 0f,
            debugReason: nameof(ChestFirstOpenRevealPresentation),
            ignoreScreenShakeSetting: false,
            hasManualShakeSettingsOverride: true,
            manualShakeSettingsOverride: CameraManualShakeSettings.Create(impactCameraShakeDuration)));
    }

    private Vector3 ResolveImpactWorldPosition()
    {
        if (impactPresentationAnchor != null)
            return impactPresentationAnchor.position;

        if (motionBounds != null)
            return motionBounds.TransformPoint(motionBounds.rect.center);

        return transform.position;
    }

    private Quaternion ResolveImpactWorldRotation()
    {
        if (impactPresentationAnchor != null)
            return impactPresentationAnchor.rotation;

        return Quaternion.identity;
    }

    private void PlayImpactParticleSystems()
    {
        if (impactParticleSystems == null || impactParticleSystems.Length == 0)
            return;

        for (int i = 0; i < impactParticleSystems.Length; i++)
        {
            ParticleSystem particleSystem = impactParticleSystems[i];
            if (particleSystem == null)
                continue;

            particleSystem.gameObject.SetActive(true);

            ParticleSystem.MainModule main = particleSystem.main;
            main.useUnscaledTime = useUnscaledTime;

            particleSystem.Stop(withChildren: true, stopBehavior: ParticleSystemStopBehavior.StopEmittingAndClear);
            particleSystem.Clear(withChildren: true);
            particleSystem.Play(withChildren: true);
        }
    }

    private void PlayImpactUiParticles()
    {
        if (!Application.isPlaying || !playImpactUiParticles)
            return;

        UIParticleEmitter emitter = ResolveImpactUiParticleEmitter(createIfMissing: true);
        if (emitter == null)
            return;

        RectTransform emitterRect = emitter.transform as RectTransform;
        if (emitterRect == null)
        {
            emitter.PlayAtWorldPosition(ResolveImpactWorldPosition());
            return;
        }

        Vector2 localPosition = emitterRect.InverseTransformPoint(ResolveImpactWorldPosition());
        emitter.PlayAt(localPosition + impactUiParticleOffset);
    }

    private void PlayUiImpactShake()
    {
        if (uiImpactShakeDuration <= 0f || uiImpactShakeAmplitude <= 0f)
            return;

        if (impactShakeRoot == null)
            impactShakeRoot = ResolveDefaultImpactShakeRoot();
        if (impactShakeRoot == null)
            return;

        StopImpactShake(resetPosition: true);
        CaptureImpactShakeRootPosition(force: false);
        impactShakeRoutine = StartCoroutine(PlayUiImpactShakeRoutine());
    }

    private IEnumerator PlayUiImpactShakeRoutine()
    {
        float elapsed = 0f;
        while (elapsed < uiImpactShakeDuration)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / uiImpactShakeDuration);
            float fade = 1f - SmoothStep(t);
            float cycle = t * uiImpactShakeFrequency * Mathf.PI * 2f;
            Vector2 offset = new Vector2(
                Mathf.Sin(cycle) * uiImpactShakeAmplitude,
                Mathf.Cos(cycle * 1.37f) * uiImpactShakeAmplitude * 0.35f) * fade;

            if (impactShakeRoot != null)
                impactShakeRoot.anchoredPosition = impactShakeRootOpenPosition + offset;

            yield return null;
        }

        if (impactShakeRoot != null)
            impactShakeRoot.anchoredPosition = impactShakeRootOpenPosition;

        impactShakeRoutine = null;
    }

    private void PreparePostRevealSlideFade()
    {
        if (!playPostRevealSlideFade)
            return;

        UISlideFadePresentation presentation = ResolvePostRevealSlideFadePresentation(createIfMissing: true);
        if (presentation == null)
            return;

        presentation.SnapClosed(deactivate: false);
    }

    private void PlayPostRevealSlideFadeOpen()
    {
        if (!playPostRevealSlideFade)
            return;

        UISlideFadePresentation presentation = ResolvePostRevealSlideFadePresentation(createIfMissing: true);
        if (presentation != null)
            presentation.PlayOpen();
    }

    private void SnapPostRevealSlideFadeOpen()
    {
        if (!playPostRevealSlideFade)
            return;

        UISlideFadePresentation presentation = ResolvePostRevealSlideFadePresentation(createIfMissing: true);
        if (presentation != null)
        {
            presentation.SnapOpen();
            return;
        }

        if (postRevealSlideFadeTarget != null)
            postRevealSlideFadeTarget.gameObject.SetActive(true);
    }

    private UISlideFadePresentation ResolvePostRevealSlideFadePresentation(bool createIfMissing)
    {
        if (postRevealSlideFadePresentation != null)
            return postRevealSlideFadePresentation;

        if (postRevealSlideFadeTarget == null)
            postRevealSlideFadeTarget = FindChildRect(postRevealSlideFadeTargetName, transform);

        if (postRevealSlideFadeTarget == null)
            return null;

        postRevealSlideFadePresentation = postRevealSlideFadeTarget.GetComponent<UISlideFadePresentation>();
        if (postRevealSlideFadePresentation == null && createIfMissing && Application.isPlaying)
            postRevealSlideFadePresentation = postRevealSlideFadeTarget.gameObject.AddComponent<UISlideFadePresentation>();

        return postRevealSlideFadePresentation;
    }

    private UIParticleEmitter ResolveImpactUiParticleEmitter(bool createIfMissing)
    {
        if (impactUiParticleEmitter != null)
            return impactUiParticleEmitter;

        RectTransform namedRoot = FindChildRect(impactUiParticleEmitterName, transform);
        if (namedRoot != null)
            impactUiParticleEmitter = namedRoot.GetComponent<UIParticleEmitter>();

        if (impactUiParticleEmitter == null)
            impactUiParticleEmitter = GetComponentInChildren<UIParticleEmitter>(true);

        if (impactUiParticleEmitter == null && createIfMissing && Application.isPlaying)
        {
            RectTransform parent = transform as RectTransform;
            if (parent == null)
                return null;

            GameObject emitterObject = new GameObject(impactUiParticleEmitterName, typeof(RectTransform), typeof(LayoutElement), typeof(UIParticleEmitter));
            RectTransform emitterRect = emitterObject.GetComponent<RectTransform>();
            emitterRect.SetParent(parent, worldPositionStays: false);
            emitterRect.anchorMin = Vector2.zero;
            emitterRect.anchorMax = Vector2.one;
            emitterRect.pivot = new Vector2(0.5f, 0.5f);
            emitterRect.offsetMin = Vector2.zero;
            emitterRect.offsetMax = Vector2.zero;
            emitterRect.localScale = Vector3.one;
            emitterRect.localRotation = Quaternion.identity;
            emitterRect.SetAsLastSibling();

            LayoutElement layoutElement = emitterObject.GetComponent<LayoutElement>();
            layoutElement.ignoreLayout = true;

            impactUiParticleEmitter = emitterObject.GetComponent<UIParticleEmitter>();
        }

        return impactUiParticleEmitter;
    }

    private static void DisableComponent<T>(RectTransform rect) where T : Behaviour
    {
        if (rect == null)
            return;

        T component = rect.GetComponent<T>();
        if (component != null)
            component.enabled = false;
    }

    private static void ForceRebuild(RectTransform rect)
    {
        if (rect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
    }

    private static RectTransform FindChildRect(string childName, Transform root)
    {
        if (string.IsNullOrWhiteSpace(childName) || root == null)
            return null;

        RectTransform[] children = root.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            RectTransform child = children[i];
            if (child != null && string.Equals(child.name, childName, System.StringComparison.Ordinal))
                return child;
        }

        return null;
    }

    private RectTransform ResolveDefaultImpactShakeRoot()
    {
        RectTransform namedRoot = FindChildRect(impactShakeRootName, transform);
        if (namedRoot != null)
            return namedRoot;

        return transform as RectTransform;
    }

    private void StopActiveRoutine()
    {
        if (activeRoutine == null)
        {
            impactUiParticleEmitter?.Stop(clear: true);
            StopImpactShake(resetPosition: true);
            return;
        }

        StopCoroutine(activeRoutine);
        activeRoutine = null;
        impactUiParticleEmitter?.Stop(clear: true);
        StopImpactShake(resetPosition: true);
    }

    private void StopImpactShake(bool resetPosition)
    {
        if (impactShakeRoutine != null)
        {
            StopCoroutine(impactShakeRoutine);
            impactShakeRoutine = null;
        }

        if (resetPosition && impactShakeRoot != null && hasCapturedImpactShakeRootPosition)
            impactShakeRoot.anchoredPosition = impactShakeRootOpenPosition;
    }

    private static float SmoothStep(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
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

    private readonly struct SideEntryPose
    {
        public readonly Vector2 ChestStart;
        public readonly Vector2 ChestResistance;
        public readonly Vector2 ChestCollision;
        public readonly Vector2 ChestFinal;
        public readonly Vector2 InventoryStart;
        public readonly Vector2 InventoryResistance;
        public readonly Vector2 InventoryCollision;
        public readonly Vector2 InventoryFinal;

        public SideEntryPose(
            Vector2 chestStart,
            Vector2 chestResistance,
            Vector2 chestCollision,
            Vector2 chestFinal,
            Vector2 inventoryStart,
            Vector2 inventoryResistance,
            Vector2 inventoryCollision,
            Vector2 inventoryFinal)
        {
            ChestStart = chestStart;
            ChestResistance = chestResistance;
            ChestCollision = chestCollision;
            ChestFinal = chestFinal;
            InventoryStart = inventoryStart;
            InventoryResistance = inventoryResistance;
            InventoryCollision = inventoryCollision;
            InventoryFinal = inventoryFinal;
        }
    }
}
