using System.Collections;
using System.Collections.Generic;
using CapstoneAudio;
using CapstonePresentation;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// First-open chest UI reveal for the authored Top/Middle/Down chest frame.
/// The grid defines the middle size; top/down follow its width and keep their height.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
// 책임: 첫 상자 오픈 UI의 프레임, 보상 슬롯, 충돌/개방 연출을 재생하고 정리한다.
public sealed class ChestFirstOpenRevealPresentation : MonoBehaviour
{
    private static readonly SoundRef UiCollisionSound = SoundRef.FromKey("sound_ui_CollisionEachUI");
    private static readonly SoundRef ChestUnlockSound = SoundRef.FromKey("sound_ui_ChestUnlock");
    private static readonly SoundRef[] FindUniqueItemSounds =
    {
        SoundRef.FromKey("sound_ui_FindUniqueItem1"),
        SoundRef.FromKey("sound_ui_FindUniqueItem2"),
        SoundRef.FromKey("sound_ui_FindUniqueItem3")
    };

    private enum EditModePreviewPose
    {
        Closed,
        Opened,
        Custom
    }

    [Header("References")]
    [SerializeField] private RectTransform chestPanel;
    [SerializeField] private RectTransform inventoryPanel;
    [SerializeField] private RectTransform motionBounds;
    [SerializeField] private RectTransform chestCollisionBounds;
    [SerializeField] private RectTransform inventoryCollisionBounds;
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
    [SerializeField] private float topLiftDistance = 86f;
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
    [SerializeField, Min(0f)] private float impactCameraShakePositionScale = 2f;
    [SerializeField] private RectTransform impactPresentationAnchor;
    [SerializeField] private ParticleSystem[] impactParticleSystems;
    [SerializeField] private RectTransform impactShakeRoot;

    [Header("Impact Chest Shake")]
    [SerializeField] private bool playImpactChestShake = true;
    [SerializeField, Min(0f)] private float impactChestShakeDuration = 0.18f;
    [SerializeField] private Vector2 impactChestShakeStrength = new(16f, 6f);
    [SerializeField, Min(1)] private int impactChestShakeVibrato = 18;
    [SerializeField, Range(0f, 180f)] private float impactChestShakeRandomness = 60f;

    [Header("Impact UI Particles")]
    [SerializeField] private bool playImpactUiParticles = true;
    [SerializeField] private UIParticleEmitter impactUiParticleEmitter;
    [SerializeField] private Vector2 impactUiParticleOffset;

    [Header("Open UI Particles")]
    [SerializeField] private bool playOpenUiParticles = true;
    [SerializeField] private UIParticleEmitter openUiParticleEmitter;
    [SerializeField] private RectTransform openUiParticleAnchor;
    [SerializeField] private RectTransform openUiParticleRenderRoot;
    [SerializeField] private Vector2 openUiParticleOffset;
    [FormerlySerializedAs("forceOpenUiParticleCanvasOnTop")]
    [SerializeField] private bool forceOpenUiParticleRenderRootOnTop = true;

    [Header("Slot Reveal UI Particles")]
    [SerializeField] private bool playSlotRevealUiParticles = true;
    [SerializeField] private UIParticleEmitter slotRevealUiParticleEmitterPrefab;
    [SerializeField] private RectTransform slotRevealUiParticlePoolRoot;
    [SerializeField, Min(0)] private int slotRevealUiParticlePrewarmCount = 3;
    [SerializeField, Min(1)] private int slotRevealUiParticleMaxPoolSize = 8;
    [SerializeField] private Vector2 slotRevealUiParticleOffset;
    [SerializeField, Min(0f)] private float slotRevealVisibilityPadding = 2f;

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
    [FormerlySerializedAs("manualChestWidth")]
    [SerializeField, Min(1f)] private float chestWidth = 431f;
    [InspectorName("Override Top Frame Height")]
    [SerializeField] private bool overrideTopFrameSize;
    [FormerlySerializedAs("topFrameSize")]
    [SerializeField, Min(1f)] private float topFrameHeight = 84.55f;
    [InspectorName("Override Down Frame Height")]
    [SerializeField] private bool overrideDownFrameSize;
    [FormerlySerializedAs("downFrameSize")]
    [SerializeField, Min(1f)] private float downFrameHeight = 84.55f;

    [Header("Layering")]
    [SerializeField] private bool forceTopSlotAsLastSibling;

    [Header("Editor Preview")]
    [SerializeField] private EditModePreviewPose editModePreviewPose = EditModePreviewPose.Opened;
    [SerializeField, Range(0f, 1f)] private float customPreviewRevealProgress = 1f;
    [SerializeField] private Vector2 closedTopFrameOffset;

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
    private Tween impactChestShakeTween;
    private Vector2 impactChestShakeRestorePosition;
    private bool hasImpactChestShakeRestorePosition;
    private Vector2 chestPanelOpenPosition;
    private Vector2 chestPanelOpenPivot = new(0.5f, 0.5f);
    private Vector2 inventoryPanelOpenPosition;
    private bool hasCapturedPanelOpenPositions;
    private bool hasPlayedOpenUiParticles;
    private bool hasPlayedUniqueItemHighlightSound;
    private GameFlowInputBlocker inputBlocker;
    private ObjectPool<UIParticleEmitter> slotRevealParticlePool;
    private readonly List<ItemSlotUI> itemRevealSlots = new();
    private readonly HashSet<ItemSlotUI> playedSlotRevealParticleSlots = new();
    private readonly HashSet<UIParticleEmitter> activeSlotRevealParticleEmitters = new();
    private readonly List<UIParticleEmitter> slotRevealParticleBuffer = new();
    private readonly ChestRevealPanelMotion panelMotion = new();

    public bool IsOpenPresentationPlaying =>
        activeRoutine != null || (inputBlocker != null && inputBlocker.IsBlocking);

    // 책임: 첫 상자 개봉 reveal 레이아웃 계산에 필요한 프레임 크기와 grid 크기를 보관한다.
    private readonly struct LayoutMetrics
    {
        public readonly float Width;
        public readonly float TopWidth;
        public readonly float MiddleWidth;
        public readonly float DownWidth;
        public readonly float TopHeight;
        public readonly float MiddleHeight;
        public readonly float DownHeight;
        public readonly Vector2 GridSize;

        public LayoutMetrics(
            float width,
            float topWidth,
            float middleWidth,
            float downWidth,
            float topHeight,
            float middleHeight,
            float downHeight,
            Vector2 gridSize)
        {
            Width = width;
            TopWidth = topWidth;
            MiddleWidth = middleWidth;
            DownWidth = downWidth;
            TopHeight = topHeight;
            MiddleHeight = middleHeight;
            DownHeight = downHeight;
            GridSize = gridSize;
        }
    }

    private void Reset()
    {
        ResolveReferences();
        ApplyEditModePreviewPose();
    }

    private void Awake()
    {
        ResolveReferences();

        if (Application.isPlaying)
            EnsureSlotRevealParticlePool();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (!Application.isPlaying && applyLayoutInEditMode)
            ApplyEditModePreviewPose();
    }

    private void OnValidate()
    {
        customPreviewRevealProgress = Mathf.Clamp01(customPreviewRevealProgress);
        topFrameHeight = Mathf.Max(1f, topFrameHeight);
        downFrameHeight = Mathf.Max(1f, downFrameHeight);
        ResolveReferences();

        if (!Application.isPlaying && applyLayoutInEditMode)
            ApplyEditModePreviewPose();
    }

#if UNITY_EDITOR
    private void LateUpdate()
    {
        if (Application.isPlaying || !applyLayoutInEditMode)
            return;

        ResolveReferences();
        ApplyEditModePreviewPose();
    }
#endif

    private void OnDisable()
    {
        StopActiveRoutine();
    }

    private void OnDestroy()
    {
        ClearSlotRevealParticlePool();
    }

    public void PlayOpen()
    {
        if (Application.isPlaying && activeRoutine != null)
            return;

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        StopActiveRoutine();
        ResolveReferences();
        CapturePanelOpenPositions(force: false);
        PreparePostRevealSlideFade();
        ApplyRevealPose(0f);
        ResetRevealParticleState();
        AcquireExternalUiInputBlockIfNeeded();
        SetInteractionEnabled(false);

        if (!CanPlaySideEntry())
        {
            PlayRevealOnly();
            return;
        }

        activeRoutine = StartCoroutine(PlaySideEntryRevealRoutine());
    }

    public void SnapOpen()
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        StopActiveRoutine();
        ResolveReferences();
        CapturePanelOpenPositions(force: false);
        ApplyPanelPositions(chestPanelOpenPosition, inventoryPanelOpenPosition);
        ApplyOpenedPose();
        SnapPostRevealSlideFadeOpen();
    }

    public void ApplyManualRevealProgress(
        float progress,
        bool enableInteraction,
        bool stopPresentationEffects = true,
        float? resizePivotY = null)
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        StopActiveRoutine(stopPresentationEffects);
        ResolveReferences();
        CapturePanelOpenPositions(force: false);
        ApplyPanelPositions(chestPanelOpenPosition, inventoryPanelOpenPosition);
        ApplyRevealPose(progress, resizePivotY);
        SetInteractionEnabled(enableInteraction);
    }

    public void PlayManualOpenRevealVfx(bool playSlotRevealParticles = true)
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        ResolveReferences();
        ResetRevealParticleState();
        PlayOpenUiParticles();
        if (playSlotRevealParticles)
            PlayVisibleSlotRevealParticles(forceVisible: true);
    }

    public void PlayManualSlotRevealVfx()
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        ResolveReferences();
        PlayVisibleSlotRevealParticles(forceVisible: true);
    }

    public void ConfigurePanels(
        RectTransform chestPanelOverride,
        RectTransform inventoryPanelOverride,
        RectTransform postRevealTargetOverride = null,
        RectTransform inventoryCollisionBoundsOverride = null)
    {
        if (chestPanelOverride != null)
            chestPanel = chestPanelOverride;

        if (inventoryPanelOverride != null)
            inventoryPanel = inventoryPanelOverride;

        if (inventoryCollisionBoundsOverride != null)
            inventoryCollisionBounds = inventoryCollisionBoundsOverride;
        else if (inventoryPanelOverride != null && !IsUsableCollisionBounds(inventoryPanelOverride, inventoryCollisionBounds))
            inventoryCollisionBounds = null;

        if (postRevealTargetOverride != null)
        {
            postRevealSlideFadeTarget = postRevealTargetOverride;
            postRevealSlideFadePresentation = null;
        }

        hasCapturedPanelOpenPositions = false;
    }

    public void ConfigureItemRevealSlots(IList<ItemSlotUI> slots)
    {
        itemRevealSlots.Clear();
        playedSlotRevealParticleSlots.Clear();

        if (slots == null)
            return;

        for (int i = 0; i < slots.Count; i++)
        {
            ItemSlotUI slot = slots[i];
            if (slot != null)
                itemRevealSlots.Add(slot);
        }
    }

    private IEnumerator PlaySideEntryRevealRoutine()
    {
        SideEntryPose pose = ResolveSideEntryPose();
        bool completedSideEntryReveal = false;
        BeginPanelMotionOwnership();
        try
        {
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

            PanelPairPosition collisionPose = ApplyImpactCollisionPose(pose);
            PlayImpactFeedback();
            yield return HoldImpactCollisionPose(collisionPose);
            yield return SettlePanelsFromImpact(collisionPose, pose);

            yield return PlaySeparateImpactChestShakeIfNeeded(pose.ChestFinal);
            PlayOpenUiParticles();

            if (revealDuration <= 0f)
            {
                ApplyOpenedPose(enableInteraction: false);
                PlayVisibleSlotRevealParticles(forceVisible: true);
                yield return PlayPostRevealSlideFadeOpenAndWait();
                CompleteOpenPresentation();
                completedSideEntryReveal = true;
                activeRoutine = null;
                yield break;
            }

            float revealElapsed = 0f;
            while (revealElapsed < revealDuration)
            {
                revealElapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float revealT = Mathf.Clamp01(revealElapsed / revealDuration);
                ApplyRevealPose(SmoothStep(revealT));
                yield return null;
            }

            ApplyOpenedPose(enableInteraction: false);
            PlayVisibleSlotRevealParticles(forceVisible: true);
            yield return PlayPostRevealSlideFadeOpenAndWait();
            CompleteOpenPresentation();
            completedSideEntryReveal = true;
            activeRoutine = null;
        }
        finally
        {
            EndPanelMotionOwnership(completedSideEntryReveal ? new PanelPairPosition(pose.ChestFinal, pose.InventoryFinal) : null);
        }
    }

    private IEnumerator PlayRevealRoutine()
    {
        float elapsed = 0f;
        PlayOpenUiParticles();

        while (elapsed < revealDuration)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / revealDuration);
            ApplyRevealPose(SmoothStep(t));
            yield return null;
        }

        ApplyOpenedPose(enableInteraction: false);
        PlayVisibleSlotRevealParticles(forceVisible: true);
        yield return PlayPostRevealSlideFadeOpenAndWait();
        CompleteOpenPresentation();
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
            ApplyRevealPose(0f);
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
        ApplyRevealPose(0f);
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
        ConfigureOpenUiParticleRenderRoot();
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
        DisableChildLayoutGroups(topFrame);
        DisableComponent<LayoutGroup>(middleFrame);
        DisableComponent<LayoutGroup>(downFrame);
        DisableComponent<LayoutElement>(downFrame);
    }

    private void ApplyOpenedPose(bool enableInteraction = true)
    {
        ApplyRevealPose(1f);
        if (enableInteraction)
            SetInteractionEnabled(true);
    }

    private void ApplyEditModePreviewPose()
    {
        ApplyRevealPose(ResolveEditModePreviewProgress());
        SetInteractionEnabled(true);
    }

    private float ResolveEditModePreviewProgress()
    {
        return editModePreviewPose switch
        {
            EditModePreviewPose.Closed => 0f,
            EditModePreviewPose.Opened => 1f,
            EditModePreviewPose.Custom => customPreviewRevealProgress,
            _ => 1f
        };
    }

    private void PlayRevealOnly()
    {
        if (revealDuration <= 0f)
        {
            PlayOpenUiParticles();
            ApplyOpenedPose(enableInteraction: false);
            PlayVisibleSlotRevealParticles(forceVisible: true);
            activeRoutine = StartCoroutine(CompleteOpenPresentationAfterPostRevealRoutine());
            return;
        }

        SetInteractionEnabled(false);
        activeRoutine = StartCoroutine(PlayRevealRoutine());
    }

    private void ApplyRevealPose(float t, float? resizePivotY = null)
    {
        t = Mathf.Clamp01(t);
        LayoutMetrics metrics = ResolveLayoutMetrics();
        float revealedMiddleHeight = metrics.MiddleHeight * t;
        float totalHeight = metrics.TopHeight + revealedMiddleHeight + metrics.DownHeight;

        SetChestPanelSize(metrics.Width, totalHeight, metrics.TopHeight + metrics.MiddleHeight + metrics.DownHeight, resizePivotY);

        if (chestPanel != null)
        {
            SetStackChild(middleRevealSlot, metrics.MiddleWidth, revealedMiddleHeight, totalHeight, metrics.TopHeight);
            SetStackChild(downFrame, metrics.DownWidth, metrics.DownHeight, totalHeight, metrics.TopHeight + revealedMiddleHeight);
            SetStackChild(topSlot, metrics.TopWidth, metrics.TopHeight, totalHeight, 0f);
        }

        SetStretch(topAnimRoot);
        SetStretch(middleViewport);
        SetTopStretch(middleContent, metrics.MiddleHeight);
        SetStretch(topFrame);
        SetStretch(middleFrame);

        SetSize(topFrame, metrics.TopWidth, metrics.TopHeight);
        SetSize(middleContent, metrics.MiddleWidth, metrics.MiddleHeight);
        SetSize(middleFrame, metrics.MiddleWidth, metrics.MiddleHeight);
        SetSize(downFrame, metrics.DownWidth, metrics.DownHeight);
        SetSize(gridRoot, metrics.GridSize.x, metrics.GridSize.y);
        ArrangeTopFrame(topFrame, metrics.TopWidth, metrics.TopHeight);
        ArrangeThreePartFrame(middleFrame, metrics.MiddleWidth, metrics.MiddleHeight, gridRoot, metrics.GridSize);
        ArrangeThreePartFrame(downFrame, metrics.DownWidth, metrics.DownHeight, null, Vector2.zero);

        if (topAnimRoot != null)
        {
            Vector2 openedTopFrameOffset = new Vector2(0f, topLiftDistance);
            topAnimRoot.anchoredPosition = Vector2.LerpUnclamped(
                closedTopFrameOffset,
                openedTopFrameOffset,
                t);
        }

        if (forceTopSlotAsLastSibling && topSlot != null)
            topSlot.SetAsLastSibling();
        if (forceOpenUiParticleRenderRootOnTop && openUiParticleRenderRoot != null)
            openUiParticleRenderRoot.SetAsLastSibling();

        ForceRebuild(gridRoot);
        ForceRebuild(middleFrame);
        ForceRebuild(topFrame);
        ForceRebuild(downFrame);
        ForceRebuild(chestPanel);
    }

    private LayoutMetrics ResolveLayoutMetrics()
    {
        Vector2 gridSize = ResolveGridSize();
        float naturalMiddleWidth = ResolveHorizontalFrameWidth(middleFrame, gridRoot, gridSize.x);
        float middleHeight = ResolveHorizontalFrameHeight(middleFrame, gridRoot, gridSize.y);
        float middleWidth = Mathf.Max(1f, chestWidth, naturalMiddleWidth);
        float naturalTopHeight = ResolveFrameHeight(topSlot, topFrame, fallbackTopHeight);
        float naturalDownHeight = ResolveFrameHeight(downFrame, null, fallbackDownHeight);
        Vector2 resolvedTopSize = ResolveFrameSize(
            overrideTopFrameSize,
            topFrameHeight,
            middleWidth,
            naturalTopHeight);
        Vector2 resolvedDownSize = ResolveFrameSize(
            overrideDownFrameSize,
            downFrameHeight,
            middleWidth,
            naturalDownHeight);
        float width = middleWidth;

        return new LayoutMetrics(
            width,
            resolvedTopSize.x,
            middleWidth,
            resolvedDownSize.x,
            resolvedTopSize.y,
            middleHeight,
            resolvedDownSize.y,
            gridSize);
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
        {
            chestPanelOpenPosition = chestPanel.anchoredPosition;
            chestPanelOpenPivot = chestPanel.pivot;
        }

        if (inventoryPanel != null)
            inventoryPanelOpenPosition = inventoryPanel.anchoredPosition;

        hasCapturedPanelOpenPositions = chestPanel != null || inventoryPanel != null;
    }

    private void ApplyPanelPositions(Vector2 chestPosition, Vector2 inventoryPosition)
    {
        panelMotion.Configure(chestPanel, inventoryPanel);
        panelMotion.ApplyPositions(chestPosition, inventoryPosition);
    }

    private void BeginPanelMotionOwnership()
    {
        panelMotion.Configure(chestPanel, inventoryPanel);
        panelMotion.BeginOwnership();
    }

    private void EndPanelMotionOwnership(PanelPairPosition? finalPose = null)
    {
        panelMotion.EndOwnership();

        if (finalPose.HasValue)
        {
            PanelPairPosition pose = finalPose.Value;
            panelMotion.ApplyPositions(pose.Chest, pose.Inventory);
        }
    }

    private PanelPairPosition ApplyImpactCollisionPose(SideEntryPose pose)
    {
        return ApplyImpactCollisionPose(new PanelPairPosition(pose.ChestCollision, pose.InventoryCollision));
    }

    private PanelPairPosition ApplyImpactCollisionPose(PanelPairPosition pose)
    {
        Vector2 chestCollision = pose.Chest;
        Vector2 inventoryCollision = pose.Inventory;

        ApplyPanelPositions(chestCollision, inventoryCollision);
        AlignImpactCollisionEdges(ref chestCollision, ref inventoryCollision);
        return new PanelPairPosition(chestCollision, inventoryCollision);
    }

    private IEnumerator HoldImpactCollisionPose(PanelPairPosition collisionPose)
    {
        float impactHoldDuration = ResolveImpactHoldDuration();
        float elapsed = 0f;
        while (elapsed < impactHoldDuration)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            PanelPairPosition shakenPose = ApplyImpactShakeOffset(collisionPose, ResolveImpactShakeOffset(elapsed));
            ApplyPanelPositions(shakenPose.Chest, shakenPose.Inventory);
            ApplyRevealPose(0f);
            yield return null;
        }

        ApplyPanelPositions(collisionPose.Chest, collisionPose.Inventory);
        ApplyRevealPose(0f);
    }

    private IEnumerator SettlePanelsFromImpact(PanelPairPosition collisionPose, SideEntryPose pose)
    {
        if (settleDuration <= 0f)
        {
            ApplyPanelPositions(pose.ChestFinal, pose.InventoryFinal);
            yield break;
        }

        float settleElapsed = 0f;
        while (settleElapsed < settleDuration)
        {
            settleElapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float settleT = Mathf.Clamp01(settleElapsed / settleDuration);
            float easedSettle = EaseOutCubic(settleT);

            ApplyPanelPositions(
                Vector2.LerpUnclamped(collisionPose.Chest, pose.ChestFinal, easedSettle),
                Vector2.LerpUnclamped(collisionPose.Inventory, pose.InventoryFinal, easedSettle));
            ApplyRevealPose(0f);
            yield return null;
        }

        ApplyPanelPositions(pose.ChestFinal, pose.InventoryFinal);
    }

    private void AlignImpactCollisionEdges(ref Vector2 chestCollision, ref Vector2 inventoryCollision)
    {
        if (motionBounds == null || chestPanel == null || inventoryPanel == null)
            return;

        RectTransform chestEdgeSource = ResolveCollisionBounds(chestPanel, chestCollisionBounds);
        RectTransform inventoryEdgeSource = ResolveCollisionBounds(inventoryPanel, inventoryCollisionBounds);
        Canvas.ForceUpdateCanvases();

        if (!TryResolveTargetWorldX(motionBounds, motionBounds.rect.center.x, out float targetWorldX))
            return;

        bool alignedChest = TranslatePanelEdgeToWorldX(chestPanel, chestEdgeSource, useRightEdge: true, targetWorldX);
        bool alignedInventory = TranslatePanelEdgeToWorldX(inventoryPanel, inventoryEdgeSource, useRightEdge: false, targetWorldX);

        if (alignedChest)
            chestCollision = chestPanel.anchoredPosition;
        if (alignedInventory)
            inventoryCollision = inventoryPanel.anchoredPosition;
    }

    private SideEntryPose ResolveSideEntryPose()
    {
        ForceRebuild(transform as RectTransform);
        ForceRebuild(chestPanel);
        ForceRebuild(inventoryPanel);
        Canvas.ForceUpdateCanvases();

        RectTransform chestEdgeSource = ResolveCollisionBounds(chestPanel, chestCollisionBounds);
        RectTransform inventoryEdgeSource = ResolveCollisionBounds(inventoryPanel, inventoryCollisionBounds);
        ForceRebuild(chestEdgeSource);
        ForceRebuild(inventoryEdgeSource);
        Canvas.ForceUpdateCanvases();

        float chestPanelWidth = ResolveElementWidth(chestPanel);
        float inventoryWidth = ResolveElementWidth(inventoryPanel);
        float chestStartRightEdge = motionBounds.rect.xMin - offscreenPadding;
        float chestCollisionRightEdge = motionBounds.rect.center.x;
        float inventoryStartLeftEdge = motionBounds.rect.xMax + offscreenPadding;
        float inventoryCollisionLeftEdge = motionBounds.rect.center.x;

        Vector2 chestStart = new Vector2(
            AnchoredXForRightEdge(chestPanel, chestEdgeSource, motionBounds, chestStartRightEdge, chestPanelWidth),
            chestPanelOpenPosition.y);
        Vector2 chestCollision = new Vector2(
            AnchoredXForRightEdge(chestPanel, chestEdgeSource, motionBounds, chestCollisionRightEdge, chestPanelWidth),
            chestPanelOpenPosition.y);
        Vector2 inventoryStart = new Vector2(
            AnchoredXForLeftEdge(inventoryPanel, inventoryEdgeSource, motionBounds, inventoryStartLeftEdge, inventoryWidth),
            inventoryPanelOpenPosition.y);
        Vector2 inventoryCollision = new Vector2(
            AnchoredXForLeftEdge(inventoryPanel, inventoryEdgeSource, motionBounds, inventoryCollisionLeftEdge, inventoryWidth),
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
            ApplyRevealPose(0f);
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
        ApplyRevealPose(0f);
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
            RectTransform child = root.GetChild(i) as RectTransform;
            if (child != null && child.gameObject.activeSelf && !ShouldIgnoreLayoutChild(child))
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
            if (child == null || !child.gameObject.activeSelf || ShouldIgnoreLayoutChild(child))
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
            if (child == null || !child.gameObject.activeSelf || ShouldIgnoreLayoutChild(child))
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

    private static Vector2 ResolveFrameSize(bool overrideHeight, float heightOverride, float width, float fallbackHeight)
    {
        float height = overrideHeight && heightOverride > 0f ? heightOverride : fallbackHeight;
        return new Vector2(Mathf.Max(1f, width), Mathf.Max(1f, height));
    }

    private static void ArrangeTopFrame(RectTransform frame, float width, float height)
    {
        if (!TryGetThreePartChildren(frame, out RectTransform left, out RectTransform topMiddle, out RectTransform right))
            return;

        float leftWidth = ResolveElementWidth(left);
        float rightWidth = ResolveElementWidth(right);
        float middleWidth = Mathf.Max(0f, width - leftWidth - rightWidth);

        SetLeftAnchored(left, 0f, leftWidth, height);
        SetLeftAnchored(topMiddle, leftWidth, middleWidth, height);
        SetLeftAnchored(right, Mathf.Max(0f, width - rightWidth), rightWidth, height);
        ArrangeEqualWidthChildren(topMiddle, middleWidth, height);
    }

    private static void ArrangeEqualWidthChildren(RectTransform frame, float width, float height)
    {
        if (frame == null)
            return;

        int count = CountLayoutChildren(frame);
        if (count <= 0)
            return;

        float childWidth = Mathf.Max(0f, width / count);
        int arrangedIndex = 0;
        for (int i = 0; i < frame.childCount; i++)
        {
            RectTransform child = frame.GetChild(i) as RectTransform;
            if (child == null || !child.gameObject.activeSelf || ShouldIgnoreLayoutChild(child))
                continue;

            SetLeftAnchored(child, childWidth * arrangedIndex, childWidth, height);
            arrangedIndex++;
        }
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
        float centerX = ResolveFrameCenterX(width, leftWidth, centerWidth, rightWidth, center == fixedCenter);

        SetLeftAnchored(left, 0f, leftWidth, height);
        SetLeftAnchored(center, centerX, centerWidth, centerHeight);
        SetLeftAnchored(right, Mathf.Max(0f, width - rightWidth), rightWidth, height);
    }

    private static float ResolveFrameCenterX(
        float width,
        float leftWidth,
        float centerWidth,
        float rightWidth,
        bool fixedCenter)
    {
        if (!fixedCenter)
            return leftWidth;

        float naturalWidth = leftWidth + centerWidth + rightWidth;
        if (width <= naturalWidth)
            return leftWidth;

        float maxX = width - rightWidth - centerWidth;
        if (maxX <= leftWidth)
            return leftWidth;

        return Mathf.Clamp((width - centerWidth) * 0.5f, leftWidth, maxX);
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
            if (child == null || !child.gameObject.activeSelf || ShouldIgnoreLayoutChild(child))
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

    private static bool ShouldIgnoreLayoutChild(RectTransform child)
    {
        if (child == null)
            return false;

        LayoutElement layoutElement = child.GetComponent<LayoutElement>();
        if (layoutElement != null && layoutElement.ignoreLayout)
            return true;

        return child.GetComponent<UIParticleEmitter>() != null;
    }

    private static int CountLayoutChildren(RectTransform root)
    {
        if (root == null)
            return 0;

        int count = 0;
        for (int i = 0; i < root.childCount; i++)
        {
            RectTransform child = root.GetChild(i) as RectTransform;
            if (child != null && child.gameObject.activeSelf && !ShouldIgnoreLayoutChild(child))
                count++;
        }

        return count;
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

    private static float AnchoredXForRightEdge(
        RectTransform movingRect,
        RectTransform edgeSource,
        RectTransform targetBounds,
        float targetBoundsLocalX,
        float fallbackWidth)
    {
        if (movingRect == null)
            return 0f;

        if (TryResolveEdgeWorldX(edgeSource, useRightEdge: true, out float currentRightEdgeWorldX)
            && TryResolveTargetWorldX(targetBounds, targetBoundsLocalX, out float targetWorldX))
        {
            return ResolveAnchoredXAfterWorldDelta(movingRect, targetWorldX - currentRightEdgeWorldX);
        }

        RectTransform targetParent = movingRect.parent as RectTransform;
        float rightEdgeX = ResolveBoundsX(targetBounds, targetParent, targetBoundsLocalX);

        float pivotX = movingRect.pivot.x;
        return rightEdgeX - (1f - pivotX) * fallbackWidth;
    }

    private static float AnchoredXForLeftEdge(
        RectTransform movingRect,
        RectTransform edgeSource,
        RectTransform targetBounds,
        float targetBoundsLocalX,
        float fallbackWidth)
    {
        if (movingRect == null)
            return 0f;

        if (TryResolveEdgeWorldX(edgeSource, useRightEdge: false, out float currentLeftEdgeWorldX)
            && TryResolveTargetWorldX(targetBounds, targetBoundsLocalX, out float targetWorldX))
        {
            return ResolveAnchoredXAfterWorldDelta(movingRect, targetWorldX - currentLeftEdgeWorldX);
        }

        RectTransform targetParent = movingRect.parent as RectTransform;
        float leftEdgeX = ResolveBoundsX(targetBounds, targetParent, targetBoundsLocalX);

        float pivotX = movingRect.pivot.x;
        return leftEdgeX + pivotX * fallbackWidth;
    }

    private static bool TryResolveEdgeWorldX(RectTransform rect, bool useRightEdge, out float edgeX)
    {
        edgeX = 0f;

        if (rect == null)
            return false;

        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);

        float resolvedEdge = useRightEdge ? float.NegativeInfinity : float.PositiveInfinity;
        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        for (int i = 0; i < corners.Length; i++)
        {
            float worldX = corners[i].x;
            minX = Mathf.Min(minX, worldX);
            maxX = Mathf.Max(maxX, worldX);
            resolvedEdge = useRightEdge
                ? Mathf.Max(resolvedEdge, worldX)
                : Mathf.Min(resolvedEdge, worldX);
        }

        if (!float.IsInfinity(resolvedEdge) && Mathf.Abs(maxX - minX) > 0.01f)
        {
            edgeX = resolvedEdge;
            return true;
        }

        Bounds childBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(rect);
        if (childBounds.size.x > 0.01f)
        {
            Vector3 localEdge = new Vector3(useRightEdge ? childBounds.max.x : childBounds.min.x, childBounds.center.y, childBounds.center.z);
            edgeX = rect.TransformPoint(localEdge).x;
            return true;
        }

        return false;
    }

    private static bool TryResolveTargetWorldX(RectTransform targetBounds, float targetBoundsLocalX, out float worldX)
    {
        worldX = 0f;

        if (targetBounds == null)
            return false;

        Vector3 worldPoint = targetBounds.TransformPoint(new Vector3(
            targetBoundsLocalX,
            targetBounds.rect.center.y,
            0f));
        worldX = worldPoint.x;
        return true;
    }

    private static float ResolveAnchoredXAfterWorldDelta(RectTransform movingRect, float deltaWorldX)
    {
        Vector2 previousAnchoredPosition = movingRect.anchoredPosition;
        Vector3 previousWorldPosition = movingRect.position;

        movingRect.position = new Vector3(
            previousWorldPosition.x + deltaWorldX,
            previousWorldPosition.y,
            previousWorldPosition.z);
        float resolvedX = movingRect.anchoredPosition.x;
        movingRect.anchoredPosition = previousAnchoredPosition;
        return resolvedX;
    }

    private static bool TranslatePanelEdgeToWorldX(
        RectTransform movingRect,
        RectTransform edgeSource,
        bool useRightEdge,
        float targetWorldX)
    {
        if (movingRect == null || edgeSource == null)
            return false;

        if (!TryResolveEdgeWorldX(edgeSource, useRightEdge, out float currentWorldX))
            return false;

        TranslateRectWorldX(movingRect, targetWorldX - currentWorldX);
        return true;
    }

    private static void TranslateRectWorldX(RectTransform rect, float deltaWorldX)
    {
        if (rect == null || Mathf.Abs(deltaWorldX) <= 0.0001f)
            return;

        Vector3 position = rect.position;
        rect.position = new Vector3(position.x + deltaWorldX, position.y, position.z);
    }

    private static RectTransform ResolveCollisionBounds(RectTransform movingRect, RectTransform preferredBounds)
    {
        if (IsUsableCollisionBounds(movingRect, preferredBounds) && HasUsableHorizontalBounds(preferredBounds))
            return preferredBounds;

        return movingRect;
    }

    private static bool IsUsableCollisionBounds(RectTransform movingRect, RectTransform preferredBounds)
    {
        if (preferredBounds == null || movingRect == null)
            return false;

        return preferredBounds == movingRect || preferredBounds.IsChildOf(movingRect);
    }

    private static bool HasUsableHorizontalBounds(RectTransform rect)
    {
        if (rect == null)
            return false;

        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;

        for (int i = 0; i < corners.Length; i++)
        {
            minX = Mathf.Min(minX, corners[i].x);
            maxX = Mathf.Max(maxX, corners[i].x);
        }

        if (Mathf.Abs(maxX - minX) > 0.01f)
            return true;

        Bounds childBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(rect);
        return childBounds.size.x > 0.01f;
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

    private void SetChestPanelSize(float width, float height, float openHeight, float? resizePivotY)
    {
        if (chestPanel == null)
            return;

        SetSize(chestPanel, width, height);

        if (!resizePivotY.HasValue)
            return;

        float virtualPivotY = Mathf.Clamp01(resizePivotY.Value);
        float clampedOpenHeight = Mathf.Max(0f, openHeight);
        float clampedHeight = Mathf.Max(0f, height);
        float referenceY = chestPanelOpenPosition.y + (virtualPivotY - chestPanelOpenPivot.y) * clampedOpenHeight;
        Vector2 anchoredPosition = chestPanel.anchoredPosition;
        anchoredPosition.y = referenceY - (virtualPivotY - chestPanel.pivot.y) * clampedHeight;
        chestPanel.anchoredPosition = anchoredPosition;
    }

    private void SetInteractionEnabled(bool enabled)
    {
        if (!Application.isPlaying || !blockInteractionDuringReveal || interactionCanvasGroup == null)
            return;

        interactionCanvasGroup.interactable = enabled;
        interactionCanvasGroup.blocksRaycasts = enabled;
    }

    private void AcquireExternalUiInputBlockIfNeeded()
    {
        if (!Application.isPlaying || (inputBlocker != null && inputBlocker.IsBlocking))
            return;

        inputBlocker = GameFlowInputBlocker.GetOrAdd(this);
        inputBlocker?.Acquire();
    }

    private void ReleaseExternalUiInputBlockIfNeeded()
    {
        inputBlocker?.Release();
    }

    private void PlayImpactFeedback()
    {
        PlayImpactPresentationHook();
        PlayImpactCameraShakeIfNeeded();
        PlayImpactParticleSystems();
        PlayImpactUiParticles();
    }

    private float ResolveImpactHoldDuration()
    {
        float holdDuration = impactPauseDuration;

        if (uiImpactShakeDuration > 0f && uiImpactShakeAmplitude > 0f)
            holdDuration = Mathf.Max(holdDuration, uiImpactShakeDuration);

        return holdDuration;
    }

    private Vector2 ResolveImpactShakeOffset(float elapsed)
    {
        if (uiImpactShakeDuration <= 0f || uiImpactShakeAmplitude <= 0f || elapsed > uiImpactShakeDuration)
            return Vector2.zero;

        float t = Mathf.Clamp01(elapsed / uiImpactShakeDuration);
        float fade = 1f - SmoothStep(t);
        float cycle = t * uiImpactShakeFrequency * Mathf.PI * 2f;
        return new Vector2(
            Mathf.Sin(cycle) * uiImpactShakeAmplitude,
            Mathf.Cos(cycle * 1.37f) * uiImpactShakeAmplitude * 0.35f) * fade;
    }

    private PanelPairPosition ApplyImpactShakeOffset(PanelPairPosition pose, Vector2 localOffset)
    {
        if (localOffset == Vector2.zero)
            return pose;

        Vector3 worldOffset = ResolveImpactShakeWorldOffset(localOffset);
        return new PanelPairPosition(
            OffsetAnchoredPositionByWorldOffset(chestPanel, pose.Chest, worldOffset),
            OffsetAnchoredPositionByWorldOffset(inventoryPanel, pose.Inventory, worldOffset));
    }

    private Vector3 ResolveImpactShakeWorldOffset(Vector2 localOffset)
    {
        if (impactShakeRoot == null)
            impactShakeRoot = ResolveDefaultImpactShakeRoot();

        RectTransform reference = impactShakeRoot != null ? impactShakeRoot : transform as RectTransform;
        if (reference == null)
            return new Vector3(localOffset.x, localOffset.y, 0f);

        return reference.TransformVector(new Vector3(localOffset.x, localOffset.y, 0f));
    }

    private static Vector2 OffsetAnchoredPositionByWorldOffset(RectTransform rect, Vector2 anchoredPosition, Vector3 worldOffset)
    {
        if (rect == null || rect.parent == null)
            return anchoredPosition + new Vector2(worldOffset.x, worldOffset.y);

        Vector3 parentLocalOffset = rect.parent.InverseTransformVector(worldOffset);
        return anchoredPosition + new Vector2(parentLocalOffset.x, parentLocalOffset.y);
    }

    private IEnumerator PlaySeparateImpactChestShakeIfNeeded(Vector2 restorePosition)
    {
        if (uiImpactShakeDuration > 0f && uiImpactShakeAmplitude > 0f)
            yield break;

        yield return PlayImpactChestShake(restorePosition);
    }

    private IEnumerator PlayImpactChestShake(Vector2 restorePosition)
    {
        if (!playImpactChestShake
            || chestPanel == null
            || impactChestShakeDuration <= 0f
            || impactChestShakeStrength.sqrMagnitude <= 0f)
            yield break;

        StopImpactChestShake(resetPosition: false);
        impactChestShakeRestorePosition = restorePosition;
        hasImpactChestShakeRestorePosition = true;
        chestPanel.anchoredPosition = restorePosition;

        impactChestShakeTween = chestPanel
            .DOShakeAnchorPos(
                impactChestShakeDuration,
                impactChestShakeStrength,
                impactChestShakeVibrato,
                impactChestShakeRandomness,
                snapping: false,
                fadeOut: true)
            .SetUpdate(useUnscaledTime)
            .OnComplete(() =>
            {
                if (chestPanel != null)
                    chestPanel.anchoredPosition = restorePosition;
                impactChestShakeTween = null;
                hasImpactChestShakeRestorePosition = false;
            });

        yield return impactChestShakeTween.WaitForCompletion();

        if (chestPanel != null)
            chestPanel.anchoredPosition = restorePosition;
        impactChestShakeTween = null;
        hasImpactChestShakeRestorePosition = false;
    }

    private void PlayImpactPresentationHook()
    {
        SoundPlaybackUtility.Play(UiCollisionSound, position: ResolveImpactWorldPosition(), sourceObject: this);

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

        CameraShakePlayback.Play(new CameraShakeRequest(
            impactCameraShakeAmplitude,
            Vector3.up,
            gameObject,
            minIntervalSeconds: 0f,
            debugReason: nameof(ChestFirstOpenRevealPresentation),
            ignoreScreenShakeSetting: false,
            hasManualShakeSettingsOverride: true,
            manualShakeSettingsOverride: CameraManualShakeSettings.Create(
                impactCameraShakeDuration,
                positionAmplitudeScale: impactCameraShakePositionScale)));
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

        PlayUiParticleAtWorldPosition(emitter, ResolveImpactWorldPosition(), impactUiParticleOffset, clearExisting: true);
    }

    private void PlayOpenUiParticles()
    {
        if (!Application.isPlaying || !playOpenUiParticles || hasPlayedOpenUiParticles)
            return;

        hasPlayedOpenUiParticles = true;
        SoundPlaybackUtility.Play(ChestUnlockSound, position: ResolveOpenUiParticleWorldPosition(), sourceObject: this);

        if (openUiParticleEmitter == null)
            return;

        ConfigureOpenUiParticleRenderRoot();
        PlayUiParticleAtWorldPosition(openUiParticleEmitter, ResolveOpenUiParticleWorldPosition(), openUiParticleOffset, clearExisting: true);
    }

    private void ConfigureOpenUiParticleRenderRoot()
    {
        if (openUiParticleEmitter == null)
            return;

        if (openUiParticleRenderRoot != null)
        {
            openUiParticleEmitter.SetParticleRoot(openUiParticleRenderRoot, clearExisting: false);
            SetStretch(openUiParticleRenderRoot);

            LayoutElement layoutElement = openUiParticleRenderRoot.GetComponent<LayoutElement>();
            if (layoutElement != null)
                layoutElement.ignoreLayout = true;

            if (forceOpenUiParticleRenderRootOnTop)
                openUiParticleRenderRoot.SetAsLastSibling();
        }
    }

    private void PlayVisibleSlotRevealParticles(bool forceVisible = false)
    {
        if (!Application.isPlaying || !playSlotRevealUiParticles || itemRevealSlots.Count == 0)
            return;

        EnsureSlotRevealParticlePool();
        if (slotRevealParticlePool == null)
            return;

        for (int i = 0; i < itemRevealSlots.Count; i++)
        {
            ItemSlotUI slot = itemRevealSlots[i];
            if (slot == null || playedSlotRevealParticleSlots.Contains(slot))
                continue;

            if (!slot.HasEpicItem)
                continue;

            if (!forceVisible && !IsSlotVisibleInReveal(slot))
                continue;

            UIParticleEmitter emitter = GetSlotRevealUiParticleEmitter();
            if (emitter == null)
                return;

            PlayUiParticleAtWorldPosition(
                emitter,
                ResolveSlotWorldCenter(slot),
                slotRevealUiParticleOffset,
                clearExisting: true);
            StartCoroutine(ReleaseSlotRevealParticleWhenFinished(emitter));
            playedSlotRevealParticleSlots.Add(slot);
            PlayUniqueItemHighlightSoundOnce(ResolveSlotWorldCenter(slot));
        }
    }

    /// <summary>상자 최초 공개 중 귀한 아이템 하이라이트가 보일 때 랜덤 강조 사운드를 한 번만 재생합니다.</summary>
    private void PlayUniqueItemHighlightSoundOnce(Vector3 position)
    {
        if (hasPlayedUniqueItemHighlightSound || FindUniqueItemSounds.Length == 0)
            return;

        hasPlayedUniqueItemHighlightSound = true;
        SoundRef sound = FindUniqueItemSounds[Random.Range(0, FindUniqueItemSounds.Length)];
        SoundPlaybackUtility.Play(sound, position: position, sourceObject: this);
    }

    private void ResetRevealParticleState()
    {
        hasPlayedOpenUiParticles = false;
        hasPlayedUniqueItemHighlightSound = false;
        playedSlotRevealParticleSlots.Clear();
    }

    private void EnsureSlotRevealParticlePool()
    {
        if (slotRevealParticlePool != null || slotRevealUiParticleEmitterPrefab == null)
            return;

        int maxSize = Mathf.Max(1, slotRevealUiParticleMaxPoolSize);
        int defaultCapacity = Mathf.Clamp(slotRevealUiParticlePrewarmCount, 0, maxSize);
        slotRevealParticlePool = new ObjectPool<UIParticleEmitter>(
            createFunc: CreateSlotRevealParticleEmitter,
            actionOnGet: OnGetSlotRevealParticleEmitter,
            actionOnRelease: OnReleaseSlotRevealParticleEmitter,
            actionOnDestroy: DestroySlotRevealParticleEmitter,
            collectionCheck: true,
            defaultCapacity: defaultCapacity,
            maxSize: maxSize);

        if (defaultCapacity <= 0)
            return;

        slotRevealParticleBuffer.Clear();
        for (int i = 0; i < defaultCapacity; i++)
            slotRevealParticleBuffer.Add(slotRevealParticlePool.Get());

        for (int i = 0; i < slotRevealParticleBuffer.Count; i++)
            slotRevealParticlePool.Release(slotRevealParticleBuffer[i]);

        slotRevealParticleBuffer.Clear();
    }

    private UIParticleEmitter GetSlotRevealUiParticleEmitter()
    {
        if (slotRevealParticlePool == null)
            return null;

        int maxSize = Mathf.Max(1, slotRevealUiParticleMaxPoolSize);
        if (slotRevealParticlePool.CountInactive == 0 && activeSlotRevealParticleEmitters.Count >= maxSize)
            return null;

        return slotRevealParticlePool.Get();
    }

    private UIParticleEmitter CreateSlotRevealParticleEmitter()
    {
        Transform parent = slotRevealUiParticlePoolRoot != null ? slotRevealUiParticlePoolRoot : transform;
        UIParticleEmitter emitter = Instantiate(slotRevealUiParticleEmitterPrefab, parent, false);
        emitter.name = $"{slotRevealUiParticleEmitterPrefab.name}_Pooled";
        return emitter;
    }

    private void OnGetSlotRevealParticleEmitter(UIParticleEmitter emitter)
    {
        if (emitter == null)
            return;

        activeSlotRevealParticleEmitters.Add(emitter);
        emitter.gameObject.SetActive(true);
    }

    private void OnReleaseSlotRevealParticleEmitter(UIParticleEmitter emitter)
    {
        if (emitter == null)
            return;

        emitter.Stop(clear: true);
        activeSlotRevealParticleEmitters.Remove(emitter);
        emitter.gameObject.SetActive(false);
    }

    private static void DestroySlotRevealParticleEmitter(UIParticleEmitter emitter)
    {
        if (emitter == null)
            return;

        if (Application.isPlaying)
            Destroy(emitter.gameObject);
        else
            DestroyImmediate(emitter.gameObject);
    }

    private IEnumerator ReleaseSlotRevealParticleWhenFinished(UIParticleEmitter emitter)
    {
        if (emitter == null)
            yield break;

        yield return null;
        while (emitter != null && emitter.IsPlaying)
            yield return null;

        ReleaseSlotRevealParticleEmitter(emitter);
    }

    private void ReleaseSlotRevealParticleEmitter(UIParticleEmitter emitter)
    {
        if (slotRevealParticlePool == null || emitter == null || !activeSlotRevealParticleEmitters.Contains(emitter))
            return;

        slotRevealParticlePool.Release(emitter);
    }

    private Vector3 ResolveOpenUiParticleWorldPosition()
    {
        if (openUiParticleAnchor != null)
            return openUiParticleAnchor.TransformPoint(openUiParticleAnchor.rect.center);

        RectTransform fallback = topFrame != null ? topFrame : topAnimRoot != null ? topAnimRoot : topSlot;
        if (fallback != null)
            return fallback.TransformPoint(fallback.rect.center);

        return ResolveImpactWorldPosition();
    }

    private static Vector3 ResolveSlotWorldCenter(ItemSlotUI slot)
    {
        RectTransform slotRect = slot != null ? slot.SlotRect : null;
        return slotRect != null ? slotRect.TransformPoint(slotRect.rect.center) : Vector3.zero;
    }

    private bool IsSlotVisibleInReveal(ItemSlotUI slot)
    {
        RectTransform slotRect = slot != null ? slot.SlotRect : null;
        if (slotRect == null)
            return false;

        if (middleRevealSlot == null)
            return true;

        Vector2 localCenter = middleRevealSlot.InverseTransformPoint(ResolveSlotWorldCenter(slot));
        Rect visibleRect = middleRevealSlot.rect;
        visibleRect.xMin -= slotRevealVisibilityPadding;
        visibleRect.xMax += slotRevealVisibilityPadding;
        visibleRect.yMin -= slotRevealVisibilityPadding;
        visibleRect.yMax += slotRevealVisibilityPadding;
        return visibleRect.Contains(localCenter);
    }

    private static void PlayUiParticleAtWorldPosition(
        UIParticleEmitter emitter,
        Vector3 worldPosition,
        Vector2 localOffset,
        bool clearExisting)
    {
        if (emitter == null)
            return;

        if (localOffset != Vector2.zero && emitter.transform is RectTransform emitterRect)
        {
            Vector2 localPosition = emitterRect.InverseTransformPoint(worldPosition);
            worldPosition = emitterRect.TransformPoint(localPosition + localOffset);
        }

        emitter.PlayAtWorldPosition(worldPosition, clearExisting);
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

    private IEnumerator PlayPostRevealSlideFadeOpenAndWait()
    {
        if (!playPostRevealSlideFade)
            yield break;

        UISlideFadePresentation presentation = ResolvePostRevealSlideFadePresentation(createIfMissing: true);
        if (presentation == null)
            yield break;

        bool completed = false;
        presentation.PlayOpen(() => completed = true);
        while (!completed && presentation != null && presentation.IsAnimating)
            yield return null;
    }

    private IEnumerator CompleteOpenPresentationAfterPostRevealRoutine()
    {
        yield return PlayPostRevealSlideFadeOpenAndWait();
        CompleteOpenPresentation();
        activeRoutine = null;
    }

    private void CompleteOpenPresentation()
    {
        SetInteractionEnabled(true);
        ReleaseExternalUiInputBlockIfNeeded();
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

    private static void DisableChildLayoutGroups(RectTransform root)
    {
        if (root == null)
            return;

        for (int i = 0; i < root.childCount; i++)
        {
            RectTransform child = root.GetChild(i) as RectTransform;
            if (child == null)
                continue;

            DisableComponent<LayoutGroup>(child);
            DisableChildLayoutGroups(child);
        }
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

    private void StopActiveRoutine(bool stopPresentationEffects = true)
    {
        EndPanelMotionOwnership();
        ReleaseExternalUiInputBlockIfNeeded();

        if (activeRoutine == null)
        {
            if (stopPresentationEffects)
                StopPresentationParticles();
            StopImpactChestShake(resetPosition: true);
            return;
        }

        StopCoroutine(activeRoutine);
        activeRoutine = null;
        if (stopPresentationEffects)
            StopPresentationParticles();
        StopImpactChestShake(resetPosition: true);
    }

    private void StopPresentationParticles()
    {
        impactUiParticleEmitter?.Stop(clear: true);
        openUiParticleEmitter?.Stop(clear: true);
        StopSlotRevealUiParticles();
        ResetRevealParticleState();
    }

    private void StopSlotRevealUiParticles()
    {
        if (activeSlotRevealParticleEmitters.Count == 0)
            return;

        slotRevealParticleBuffer.Clear();
        foreach (UIParticleEmitter emitter in activeSlotRevealParticleEmitters)
            slotRevealParticleBuffer.Add(emitter);

        for (int i = 0; i < slotRevealParticleBuffer.Count; i++)
            ReleaseSlotRevealParticleEmitter(slotRevealParticleBuffer[i]);

        slotRevealParticleBuffer.Clear();
    }

    private void ClearSlotRevealParticlePool()
    {
        StopSlotRevealUiParticles();
        slotRevealParticlePool?.Clear();
        slotRevealParticlePool = null;
    }

    private void StopImpactChestShake(bool resetPosition)
    {
        if (impactChestShakeTween != null)
        {
            impactChestShakeTween.Kill(complete: false);
            impactChestShakeTween = null;
        }

        if (resetPosition && chestPanel != null && hasImpactChestShakeRestorePosition)
            chestPanel.anchoredPosition = impactChestShakeRestorePosition;

        hasImpactChestShakeRestorePosition = false;
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

    // 책임: 상자/인벤토리 패널의 한 쌍 위치를 충돌/정착 연출 단계에 전달한다.
    private readonly struct PanelPairPosition
    {
        public readonly Vector2 Chest;
        public readonly Vector2 Inventory;

        public PanelPairPosition(Vector2 chest, Vector2 inventory)
        {
            Chest = chest;
            Inventory = inventory;
        }
    }

    // 책임: 상자/인벤토리 패널의 측면 진입 시작, 저항, 충돌, 최종 위치를 보관한다.
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
