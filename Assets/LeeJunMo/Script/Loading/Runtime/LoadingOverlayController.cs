using System;
using System.Collections.Generic;
using CapstoneRuntime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-858)]
[DisallowMultipleComponent]
public sealed class LoadingOverlayController : MonoBehaviour
{
    public static LoadingOverlayController Instance { get; private set; }
    public bool IsActiveLoadingPresentation { get; private set; }
    public bool IsPresentationVisible =>
        overlayRoot != null &&
        canvasGroup != null &&
        overlayRoot.gameObject.activeSelf &&
        canvasGroup.alpha > 0.001f;

    private static bool s_isQuitting;
    private const int DebugPreviewBatchId = -7001;
    private const float FallbackTrackWidth = 560f;
    private const float TravelRange = 172f;
    private bool ownsRuntimeInstance;

    [Header("Policy")]
    [SerializeField] private bool showOnlyForCorridorEntry = true;

    [Header("View")]
    [SerializeField] private LoadingOverlayView overlayView;

    [Header("Timing")]
    [SerializeField, Min(0.01f)] private float fadeInSeconds = 0.1f;
    [SerializeField, Min(0.01f)] private float fadeOutSeconds = 0.1f;
    [SerializeField, Min(0f)] private float minimumVisibleSeconds = 0.12f;
    [SerializeField, Min(0f)] private float delayedRevealSeconds = 1.5f;
    [SerializeField, Min(0.05f)] private float loadingDotStepSeconds = 0.35f;
    [SerializeField, Min(0.01f)] private float activeProgressFollowSpeed = 9f;
    [SerializeField, Min(0.01f)] private float completionProgressFollowSpeed = 22f;
    [SerializeField, Min(1f)] private float tipCycleSeconds = 5.5f;

    [Header("Managed Presentation")]
    [SerializeField, Min(0f)] private float managedBatchAppearanceGraceSeconds = 0.75f;

    [Header("Stall Recovery")]
    [SerializeField, Min(0f)] private float stalledBatchTimeoutSeconds = 2.5f;
    [SerializeField, Range(0f, 1f)] private float stalledBatchMinimumProgress01 = 0.9f;

    [Header("Travel Visual")]
    [SerializeField] private GameObject customTravelVisualPrefab;
    [SerializeField, Range(0f, 1f)] private float trackFillProgressStart01 = 0.12f;
    [SerializeField, Range(0f, 1f)] private float trackFillProgressEnd01 = 0.88f;

    [Header("Debug Preview")]
    [SerializeField] private KeyCode debugPreviewToggleKey = KeyCode.F7;
    [SerializeField, Min(0.25f)] private float debugPreviewCycleSeconds = 2.4f;
    [SerializeField] private bool startWithDebugPreview;

    [Header("Tips")]
    [SerializeField] private List<string> defaultCorridorTips = new()
    {
        "TMI: Corridor entry preloads the next boss presentation set before the fight starts.",
        "TMI: If corridor loading grows, trim route manifests before adding more prewarm targets.",
        "TMI: Replace the default travel visual by assigning a custom travel prefab on this controller.",
        "TMI: The progress bar now reflects real Addressables provider progress."
    };

    private LoadingOverlayView boundOverlayView;
    private RectTransform overlayRoot;
    private CanvasGroup canvasGroup;
    private Image progressFillImage;
    private RectTransform progressGlowRect;
    private TMP_Text titleText;
    private TMP_Text statusText;
    private TMP_Text detailText;
    private TMP_Text percentText;
    private TMP_Text loadingText;
    private TMP_Text tipLabelText;
    private TMP_Text tipText;
    private RectTransform travelHost;
    private RectTransform defaultTravelRoot;
    private RectTransform travelTrackBoundsRect;
    private Image travelTrackFillImage;
    private RectTransform travelWalkerRect;
    private Vector2 baseTravelWalkerAnchoredPosition;
    private bool hasBaseTravelWalkerAnchoredPosition;
    private LoadingOverlayView runtimeFallbackView;
    private GameObject runtimeFallbackCanvas;

    private GameObject activeCustomTravelVisualInstance;
    private GameObject boundCustomTravelVisualPrefab;
    private int observedBatchId;
    private bool targetVisible;
    private float visibleSinceRealtime;
    private float displayedProgress;
    private float shimmerPhase;
    private bool debugPreviewActive;
    private float debugPreviewStartedRealtime;
    private int trackedRealBatchId;
    private float lastObservedRealBatchProgress;
    private float lastObservedRealBatchRealtime;
    private bool managedPresentationActive;
    private bool managedPresentationRevealed;
    private bool managedPresentationObservedRealBatch;
    private float managedPresentationStartedRealtime;

    public float DelayedRevealSeconds => Mathf.Max(0f, delayedRevealSeconds);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoBootstrap()
    {
        if (s_isQuitting || Instance != null)
            return;

        EnsureInstance();
    }

    public static LoadingOverlayController EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        LoadingOverlayController existing = RuntimeServiceOwnership.FindExistingService<LoadingOverlayController>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        if (s_isQuitting)
            return null;

        GameObject host = RuntimeServiceOwnership.CreateServiceHost(nameof(LoadingOverlayController));
        LoadingOverlayController created = host.AddComponent<LoadingOverlayController>();
        created.ownsRuntimeInstance = true;
        return created;
    }

    public void ForceHidePresentation()
    {
        debugPreviewActive = false;
        targetVisible = false;
        displayedProgress = 1f;
        observedBatchId = 0;
        visibleSinceRealtime = 0f;
        IsActiveLoadingPresentation = false;
        trackedRealBatchId = 0;
        lastObservedRealBatchProgress = 0f;
        lastObservedRealBatchRealtime = 0f;
        managedPresentationActive = false;
        managedPresentationRevealed = false;
        managedPresentationObservedRealBatch = false;
        managedPresentationStartedRealtime = 0f;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        if (overlayRoot != null)
            overlayRoot.gameObject.SetActive(false);
    }

    public void BeginManagedPresentation(bool showImmediately = false)
    {
        ResolveViewIfNeeded(allowRuntimeFallback: true);
        BindTravelVisual();
        debugPreviewActive = false;
        managedPresentationActive = true;
        managedPresentationRevealed = false;
        managedPresentationObservedRealBatch = false;
        managedPresentationStartedRealtime = Time.realtimeSinceStartup;
        targetVisible = false;
        visibleSinceRealtime = managedPresentationStartedRealtime;
        displayedProgress = 0f;
        shimmerPhase = 0f;

        if (!showImmediately)
            return;

        RevealManagedPresentation(immediate: true);
    }

    public void RevealManagedPresentation(bool immediate = false)
    {
        if (!managedPresentationActive && !debugPreviewActive)
            return;

        ResolveViewIfNeeded(allowRuntimeFallback: true);
        BindTravelVisual();
        managedPresentationRevealed = true;
        targetVisible = true;
        visibleSinceRealtime = Time.realtimeSinceStartup;

        if (overlayRoot == null || canvasGroup == null)
            return;

        overlayRoot.gameObject.SetActive(true);
        if (immediate)
            canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = false;
        ApplyCompactViewVisibility();
        UpdateCompactLoadingText(Time.realtimeSinceStartup);
    }

    public bool IsManagedPresentationReadyToComplete()
    {
        if (!managedPresentationActive)
            return true;

        if (PresentationPreloadService.GetCurrentBatchPendingProviderOperationCount() > 0)
            return false;

        if (managedPresentationObservedRealBatch)
            return true;

        return Time.realtimeSinceStartup - managedPresentationStartedRealtime >=
               Mathf.Max(0f, managedBatchAppearanceGraceSeconds);
    }

    public void EndManagedPresentation()
    {
        managedPresentationActive = false;
        managedPresentationRevealed = false;
        targetVisible = false;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            if (Instance.ownsRuntimeInstance && !ownsRuntimeInstance)
            {
                LoadingOverlayController previousInstance = Instance;
                Instance = this;
                Destroy(previousInstance.gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        Instance = this;
        RuntimeServiceOwnership.Adopt(this);
        ResolveViewIfNeeded(allowRuntimeFallback: false);
        ForceHidePresentation();
        BindTravelVisual();
        debugPreviewActive = startWithDebugPreview;
        debugPreviewStartedRealtime = Time.realtimeSinceStartup;
    }

    private void OnDestroy()
    {
        if (activeCustomTravelVisualInstance != null)
            Destroy(activeCustomTravelVisualInstance);

        if (runtimeFallbackCanvas != null)
            Destroy(runtimeFallbackCanvas);

        IsActiveLoadingPresentation = false;
        if (Instance == this)
            Instance = null;
    }

    private void OnApplicationQuit()
    {
        s_isQuitting = true;
    }

    private void Update()
    {
        UpdateDebugPreviewToggle();

        PortalRouteManager routeManager = PortalRouteManager.Instance;
        int batchId = PresentationPreloadService.GetCurrentProviderBatchId();
        int pendingCount = PresentationPreloadService.GetCurrentBatchPendingProviderOperationCount();
        float providerProgress = PresentationPreloadService.GetCurrentProviderProgress01();
        bool realBatchActive = batchId > 0 && pendingCount > 0;
        bool allowedRealBatch =
            realBatchActive &&
            managedPresentationActive &&
            ShouldShowRealBatch(routeManager);
        bool previewBatch = debugPreviewActive;
        bool batchActive = previewBatch || allowedRealBatch;
        int effectiveBatchId = previewBatch
            ? DebugPreviewBatchId
            : allowedRealBatch
                ? batchId
                : 0;
        int effectivePendingCount = previewBatch ? 1 : allowedRealBatch ? pendingCount : 0;
        float effectiveProgress = previewBatch
            ? EvaluateDebugPreviewProgress()
            : allowedRealBatch
                ? providerProgress
                : 1f;

        if (ForceCompleteStalledRealBatch(previewBatch, allowedRealBatch, effectiveBatchId, effectivePendingCount, effectiveProgress))
        {
            batchActive = false;
            effectiveBatchId = 0;
            effectivePendingCount = 0;
            effectiveProgress = 1f;
        }

        bool wantsPresentationView =
            debugPreviewActive ||
            batchActive ||
            managedPresentationRevealed ||
            targetVisible ||
            canvasGroup != null && canvasGroup.alpha > 0.001f;
        ResolveViewIfNeeded(allowRuntimeFallback: wantsPresentationView);
        BindTravelVisual();

        if (allowedRealBatch)
            managedPresentationObservedRealBatch = true;

        if (batchActive && effectiveBatchId != observedBatchId)
        {
            BeginBatch(effectiveBatchId);
            if (!previewBatch && !managedPresentationRevealed)
                targetVisible = false;
        }

        if (previewBatch || managedPresentationRevealed)
            targetVisible = true;

        float targetProgress = batchActive
            ? effectiveProgress
            : managedPresentationActive && managedPresentationObservedRealBatch
                ? 1f
                : managedPresentationActive
                    ? displayedProgress
                    : 1f;
        if (effectiveBatchId != 0 && observedBatchId == effectiveBatchId)
            targetProgress = Mathf.Max(displayedProgress, targetProgress);

        if (previewBatch && targetProgress >= 0.985f)
            displayedProgress = 0f;

        float followSpeed = batchActive ? activeProgressFollowSpeed : completionProgressFollowSpeed;
        displayedProgress = SmoothTowards(displayedProgress, targetProgress, followSpeed);
        displayedProgress = Mathf.Clamp01(displayedProgress);

        if (!batchActive && targetVisible && !managedPresentationActive)
        {
            bool visibleLongEnough = Time.realtimeSinceStartup - visibleSinceRealtime >= minimumVisibleSeconds;
            if (visibleLongEnough && displayedProgress >= 0.999f)
                targetVisible = false;
        }

        UpdateCopy(routeManager, effectivePendingCount, batchActive, previewBatch);
        UpdateVisualState();
        bool corridorLoadingContext =
            routeManager != null &&
            PortalRouteManager.IsCorridorEntryTransition(routeManager.LastLoadPresentationTransitionType);
        IsActiveLoadingPresentation =
            !previewBatch &&
            overlayRoot != null &&
            canvasGroup != null &&
            (managedPresentationRevealed || corridorLoadingContext) &&
            (targetVisible || canvasGroup.alpha > 0.001f);
    }

    private bool ShouldShowRealBatch(PortalRouteManager routeManager)
    {
        if (!showOnlyForCorridorEntry)
            return true;

        return routeManager != null &&
               PortalRouteManager.IsCorridorEntryTransition(routeManager.LastLoadPresentationTransitionType);
    }

    private void BeginBatch(int batchId)
    {
        observedBatchId = batchId;
        targetVisible = true;
        visibleSinceRealtime = Time.realtimeSinceStartup;
        displayedProgress = 0f;
        shimmerPhase = 0f;
    }

    private void ResolveViewIfNeeded(bool allowRuntimeFallback)
    {
        LoadingOverlayView desiredView = ResolveDesiredView(allowRuntimeFallback);

        if (desiredView == null)
        {
            ClearResolvedView();
            return;
        }

        if (boundOverlayView == desiredView && overlayRoot != null && canvasGroup != null)
            return;

        bool firstBind = boundOverlayView == null;

        boundOverlayView = desiredView;
        overlayRoot = desiredView.Root;
        canvasGroup = desiredView.CanvasGroup;
        titleText = desiredView.TitleText;
        statusText = desiredView.StatusText;
        detailText = desiredView.DetailText;
        percentText = desiredView.PercentText;
        loadingText = desiredView.LoadingText;
        tipLabelText = desiredView.TipLabelText;
        tipText = desiredView.TipText;
        progressFillImage = desiredView.ProgressFillImage;
        progressGlowRect = desiredView.ProgressGlowRect;
        travelHost = desiredView.TravelHost;
        defaultTravelRoot = desiredView.DefaultTravelRoot;
        travelTrackBoundsRect = desiredView.TravelTrackBoundsRect;
        travelTrackFillImage = desiredView.TravelTrackFillImage;
        travelWalkerRect = desiredView.TravelWalkerRect;
        hasBaseTravelWalkerAnchoredPosition = travelWalkerRect != null;
        if (hasBaseTravelWalkerAnchoredPosition)
            baseTravelWalkerAnchoredPosition = travelWalkerRect.anchoredPosition;

        if (overlayRoot != null && !overlayRoot.gameObject.activeSelf)
            overlayRoot.gameObject.SetActive(false);

        if (firstBind && overlayRoot != null && canvasGroup != null && !targetVisible)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            overlayRoot.gameObject.SetActive(false);
        }

        ApplyCompactViewVisibility();
    }

    private LoadingOverlayView ResolveDesiredView(bool allowRuntimeFallback)
    {
        LoadingOverlayView preferredCanvasView = null;
        Canvas loadingCanvas = GlobalUIRoot.GetCanvas(GlobalCanvasLayer.Loading);
        if (loadingCanvas != null)
        {
            preferredCanvasView = loadingCanvas.GetComponentInChildren<LoadingOverlayView>(includeInactive: true);
        }

        bool keepCurrentView = boundOverlayView != null && (targetVisible || (canvasGroup != null && canvasGroup.alpha > 0.001f));
        if (keepCurrentView)
            return boundOverlayView;

        if (overlayView != null)
            return overlayView;

        if (preferredCanvasView != null)
            return preferredCanvasView;

        if (!allowRuntimeFallback)
            return GetComponentInChildren<LoadingOverlayView>(includeInactive: true);

        CreateRuntimeFallbackViewIfNeeded();
        if (runtimeFallbackView != null)
            return runtimeFallbackView;

        return GetComponentInChildren<LoadingOverlayView>(includeInactive: true);
    }

    private void ClearResolvedView()
    {
        if (activeCustomTravelVisualInstance != null)
        {
            Destroy(activeCustomTravelVisualInstance);
            activeCustomTravelVisualInstance = null;
        }

        boundCustomTravelVisualPrefab = null;
        boundOverlayView = null;
        overlayRoot = null;
        canvasGroup = null;
        titleText = null;
        statusText = null;
        detailText = null;
        percentText = null;
        loadingText = null;
        tipLabelText = null;
        tipText = null;
        progressFillImage = null;
        progressGlowRect = null;
        travelHost = null;
        defaultTravelRoot = null;
        travelTrackBoundsRect = null;
        travelTrackFillImage = null;
        travelWalkerRect = null;
        hasBaseTravelWalkerAnchoredPosition = false;
    }

    private void UpdateCopy(
        PortalRouteManager routeManager,
        int pendingCount,
        bool batchActive,
        bool previewActive)
    {
        ApplyCompactViewVisibility();
        UpdateCompactLoadingText(previewActive ? debugPreviewStartedRealtime : visibleSinceRealtime);
    }

    private void ApplyCompactViewVisibility()
    {
        SetTextActive(titleText, titleText == loadingText);
        SetTextActive(statusText, statusText == loadingText);
        SetTextActive(detailText, detailText == loadingText);
        SetTextActive(percentText, percentText == loadingText);
        SetTextActive(tipLabelText, tipLabelText == loadingText);
        SetTextActive(tipText, tipText == loadingText);

        if (loadingText != null)
            loadingText.gameObject.SetActive(true);

        if (progressFillImage != null)
            progressFillImage.gameObject.SetActive(false);

        if (progressGlowRect != null)
            progressGlowRect.gameObject.SetActive(false);

        if (travelTrackFillImage != null)
            travelTrackFillImage.gameObject.SetActive(false);

        if (travelTrackBoundsRect != null)
            travelTrackBoundsRect.gameObject.SetActive(false);

        if (travelWalkerRect != null)
            travelWalkerRect.gameObject.SetActive(true);
    }

    private static void SetTextActive(TMP_Text text, bool active)
    {
        if (text != null)
            text.gameObject.SetActive(active);
    }

    private void UpdateCompactLoadingText(float startedRealtime)
    {
        if (loadingText == null)
            return;

        float elapsed = Mathf.Max(0f, Time.realtimeSinceStartup - startedRealtime);
        int dotCount = Mathf.FloorToInt(elapsed / Mathf.Max(0.05f, loadingDotStepSeconds)) % 3 + 1;
        loadingText.text = "Loading" + new string('.', dotCount);
    }

    private void UpdateLegacyCopy(
        PortalRouteManager routeManager,
        int pendingCount,
        bool batchActive,
        bool previewActive)
    {
        string title = previewActive ? "ENTERING CORRIDOR [PREVIEW]" : BuildTitle(routeManager);
        string status = BuildStatus(routeManager, batchActive, previewActive);
        string detail = BuildDetail(routeManager, pendingCount, batchActive, previewActive);
        string percent = $"{Mathf.RoundToInt(displayedProgress * 100f):0}%";
        string tipLabel = previewActive ? "LAYOUT CHECK" : "TMI";
        string tip = BuildTip(previewActive);

        if (titleText != null)
            titleText.text = title;

        if (statusText != null)
            statusText.text = status;

        if (detailText != null)
            detailText.text = detail;

        if (percentText != null)
            percentText.text = percent;

        if (tipLabelText != null)
            tipLabelText.text = tipLabel;

        if (tipText != null)
            tipText.text = tip;
    }

    private string BuildTitle(PortalRouteManager routeManager)
    {
        if (routeManager == null)
            return "STREAMING CONTENT";

        return routeManager.LastLoadPresentationTransitionType switch
        {
            TransitionType.HubToRunStart => "STARTING RUN",
            TransitionType.BossToCorridor => "ENTERING NEXT CORRIDOR",
            _ => "STREAMING CONTENT"
        };
    }

    private string BuildStatus(PortalRouteManager routeManager, bool batchActive, bool previewActive)
    {
        if (previewActive)
            return "Corridor Loading Layout Preview";

        if (routeManager != null && !string.IsNullOrWhiteSpace(routeManager.LastLoadPresentationTargetSceneName))
            return $"Preparing {FormatDisplayName(routeManager.LastLoadPresentationTargetSceneName)}";

        if (routeManager != null && routeManager.CurrentStageSet != null)
            return $"Preparing {FormatDisplayName(routeManager.CurrentStageSet.name)}";

        return batchActive ? "Preparing corridor presentation" : "Standing by";
    }

    private string BuildDetail(
        PortalRouteManager routeManager,
        int pendingCount,
        bool batchActive,
        bool previewActive)
    {
        if (previewActive)
            return $"Press {debugPreviewToggleKey} to hide preview";

        string stageText = routeManager != null && routeManager.HasActivePlan
            ? $"Stage {routeManager.CurrentStageIndex + 1}/{Mathf.Max(1, routeManager.TotalStageCount)}"
            : "Standalone";
        string transitionText = routeManager != null
            ? FormatTransitionLabel(routeManager.LastLoadPresentationTransitionType)
            : "Route transition";

        if (batchActive)
        {
            string workText = pendingCount > 0
                ? $"{pendingCount} async ops remaining"
                : "Finalizing loaded assets";
            return $"{stageText} | {transitionText} | {workText}";
        }

        if (routeManager != null && !string.IsNullOrWhiteSpace(routeManager.LastTransitionEvent))
            return $"{stageText} | {routeManager.LastTransitionEvent}";

        return $"{stageText} | Standing by";
    }

    private bool ForceCompleteStalledRealBatch(
        bool previewBatch,
        bool allowedRealBatch,
        int effectiveBatchId,
        int effectivePendingCount,
        float effectiveProgress)
    {
        if (previewBatch || !allowedRealBatch || effectiveBatchId <= 0 || stalledBatchTimeoutSeconds <= 0f)
        {
            trackedRealBatchId = 0;
            lastObservedRealBatchProgress = 0f;
            lastObservedRealBatchRealtime = 0f;
            return false;
        }

        if (trackedRealBatchId != effectiveBatchId)
        {
            trackedRealBatchId = effectiveBatchId;
            lastObservedRealBatchProgress = effectiveProgress;
            lastObservedRealBatchRealtime = Time.realtimeSinceStartup;
            return false;
        }

        bool progressed = effectiveProgress > lastObservedRealBatchProgress + 0.001f;
        bool drainedPendingOps = effectivePendingCount <= 0;
        if (progressed || drainedPendingOps)
        {
            lastObservedRealBatchProgress = effectiveProgress;
            lastObservedRealBatchRealtime = Time.realtimeSinceStartup;
            return false;
        }

        if (effectiveProgress < stalledBatchMinimumProgress01)
            return false;

        float stalledSeconds = Time.realtimeSinceStartup - lastObservedRealBatchRealtime;
        if (stalledSeconds < stalledBatchTimeoutSeconds)
            return false;

        Debug.LogWarning(
            $"[LoadingOverlayController] Provider batch {effectiveBatchId} stalled at {effectiveProgress * 100f:0}% for {stalledSeconds:0.0}s. Hiding loading overlay.",
            this);

        trackedRealBatchId = 0;
        lastObservedRealBatchProgress = 0f;
        lastObservedRealBatchRealtime = 0f;
        return true;
    }

    private string BuildTip(bool previewActive)
    {
        if (previewActive)
            return "Use this preview to check layout. Later you can assign a custom travel prefab here.";

        if (defaultCorridorTips == null || defaultCorridorTips.Count == 0)
            return "Assign corridor TMI copy on LoadingOverlayController when the final copy is ready.";

        float elapsed = Mathf.Max(0f, Time.realtimeSinceStartup - visibleSinceRealtime);
        int tipIndex = Mathf.FloorToInt(elapsed / Mathf.Max(1f, tipCycleSeconds)) % defaultCorridorTips.Count;
        return defaultCorridorTips[tipIndex];
    }

    private void CreateRuntimeFallbackViewIfNeeded()
    {
        if (runtimeFallbackView != null)
            return;

        RuntimePresentationFallbackAudit.Record(
            this,
            "Loading overlay fallback",
            "a scene-authored LoadingOverlayView or GlobalUIRoot loading overlay prefab");

        runtimeFallbackCanvas = new GameObject(
            "RuntimeLoadingCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        runtimeFallbackCanvas.transform.SetParent(transform, false);

        Canvas canvas = runtimeFallbackCanvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue - 2;

        CanvasScaler scaler = runtimeFallbackCanvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform rootRect = CreateRect("RuntimeLoadingRoot", runtimeFallbackCanvas.transform);
        Stretch(rootRect);
        Image background = rootRect.gameObject.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.82f);
        CanvasGroup rootGroup = rootRect.gameObject.AddComponent<CanvasGroup>();
        rootGroup.alpha = 0f;
        rootGroup.blocksRaycasts = false;
        rootGroup.interactable = false;

        TextMeshProUGUI loading = CreateText(
            rootRect,
            "LoadingText",
            "Loading...",
            34f,
            FontStyles.Bold,
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(-72f, 32f),
            new Vector2(220f, 44f));
        loading.alignment = TextAlignmentOptions.Right;

        runtimeFallbackView = rootRect.gameObject.AddComponent<LoadingOverlayView>();
        runtimeFallbackView.AssignRuntimeReferences(rootRect, rootGroup, null, null, null, null, loading, null);
        runtimeFallbackCanvas.SetActive(true);
        rootRect.gameObject.SetActive(false);
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private static void Stretch(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private static TextMeshProUGUI CreateText(
        Transform parent,
        string name,
        string initialText,
        float fontSize,
        FontStyles fontStyle,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        RectTransform rect = CreateRect(name, parent);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = initialText;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private void UpdateVisualState()
    {
        if (overlayRoot == null || canvasGroup == null)
            return;

        bool shouldRemainActive = targetVisible || canvasGroup.alpha > 0.001f;
        if (overlayRoot.gameObject.activeSelf != shouldRemainActive)
            overlayRoot.gameObject.SetActive(shouldRemainActive);

        if (!shouldRemainActive)
            return;

        float fadeDuration = targetVisible ? fadeInSeconds : fadeOutSeconds;
        float alphaStep = fadeDuration > 0f ? Time.unscaledDeltaTime / fadeDuration : 1f;
        canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetVisible ? 1f : 0f, alphaStep);
        canvasGroup.blocksRaycasts = canvasGroup.alpha > 0.01f;
        canvasGroup.interactable = false;

        ApplyCompactViewVisibility();
        UpdateCompactLoadingText(debugPreviewActive ? debugPreviewStartedRealtime : visibleSinceRealtime);
    }

    private void BindTravelVisual()
    {
        if (travelHost == null)
            return;

        if (boundCustomTravelVisualPrefab == customTravelVisualPrefab)
        {
            if (defaultTravelRoot != null)
                defaultTravelRoot.gameObject.SetActive(boundCustomTravelVisualPrefab == null);
            return;
        }

        if (activeCustomTravelVisualInstance != null)
            Destroy(activeCustomTravelVisualInstance);

        boundCustomTravelVisualPrefab = customTravelVisualPrefab;
        activeCustomTravelVisualInstance = null;

        if (boundCustomTravelVisualPrefab != null)
        {
            activeCustomTravelVisualInstance = Instantiate(boundCustomTravelVisualPrefab, travelHost, false);
            if (activeCustomTravelVisualInstance.transform is RectTransform visualRect)
            {
                visualRect.anchorMin = Vector2.zero;
                visualRect.anchorMax = Vector2.one;
                visualRect.offsetMin = Vector2.zero;
                visualRect.offsetMax = Vector2.zero;
                visualRect.localScale = Vector3.one;
            }
        }

        if (defaultTravelRoot != null)
            defaultTravelRoot.gameObject.SetActive(boundCustomTravelVisualPrefab == null);
    }

    private void UpdateDefaultTravelVisual()
    {
        if (defaultTravelRoot == null || !defaultTravelRoot.gameObject.activeSelf || travelWalkerRect == null)
            return;

        float walkerProgress01 = Mathf.Clamp01(displayedProgress);
        float trackFillProgress01 = EvaluateTrackFillProgress01(displayedProgress);
        if (travelTrackFillImage != null)
            travelTrackFillImage.fillAmount = trackFillProgress01;

        Vector2 anchoredPosition = hasBaseTravelWalkerAnchoredPosition
            ? baseTravelWalkerAnchoredPosition
            : travelWalkerRect.anchoredPosition;
        anchoredPosition.x = ResolveTravelWalkerAnchoredX(walkerProgress01, anchoredPosition.x);
        travelWalkerRect.anchoredPosition = anchoredPosition;
    }

    private float EvaluateTrackFillProgress01(float progress01)
    {
        float start = Mathf.Clamp01(trackFillProgressStart01);
        float end = Mathf.Clamp01(trackFillProgressEnd01);
        if (end < start)
        {
            float swap = start;
            start = end;
            end = swap;
        }

        if (Mathf.Approximately(start, end))
            return progress01 >= end ? 1f : 0f;

        return Mathf.InverseLerp(start, end, Mathf.Clamp01(progress01));
    }

    private float ResolveTravelWalkerAnchoredX(float travel01, float fallbackX)
    {
        RectTransform walkerParent = travelWalkerRect.parent as RectTransform;
        RectTransform trackRect = ResolveTravelTrackRect();
        if (walkerParent == null || trackRect == null)
            return Mathf.Lerp(-TravelRange, TravelRange, travel01);

        Vector3[] corners = new Vector3[4];
        trackRect.GetWorldCorners(corners);
        Vector3 leftWorld = (corners[0] + corners[1]) * 0.5f;
        Vector3 rightWorld = (corners[2] + corners[3]) * 0.5f;
        float halfWalkerWidth = ResolveHalfWalkerWidthOnTrack(walkerParent, leftWorld, rightWorld);
        Vector3 trackDirection = rightWorld - leftWorld;
        float trackLength = trackDirection.magnitude;
        if (trackLength > 0.0001f)
        {
            Vector3 direction = trackDirection / trackLength;
            float safeOffset = Mathf.Min(halfWalkerWidth, trackLength * 0.5f);
            leftWorld += direction * safeOffset;
            rightWorld -= direction * safeOffset;
        }

        Vector3 worldPoint = Vector3.Lerp(leftWorld, rightWorld, travel01);
        Vector3 localPoint = walkerParent.InverseTransformPoint(worldPoint);
        return !float.IsNaN(localPoint.x) && !float.IsInfinity(localPoint.x) ? localPoint.x : fallbackX;
    }

    private RectTransform ResolveTravelTrackRect()
    {
        if (travelTrackBoundsRect != null)
            return travelTrackBoundsRect;

        if (travelTrackFillImage != null)
        {
            RectTransform fillRect = travelTrackFillImage.rectTransform;
            if (fillRect.parent is RectTransform trackParentRect)
                return trackParentRect;

            return fillRect;
        }

        return defaultTravelRoot;
    }

    private float ResolveHalfWalkerWidthOnTrack(RectTransform walkerParent, Vector3 leftWorld, Vector3 rightWorld)
    {
        if (travelWalkerRect == null)
            return 0f;

        Vector3[] walkerCorners = new Vector3[4];
        travelWalkerRect.GetWorldCorners(walkerCorners);
        Vector3 walkerLeftWorld = (walkerCorners[0] + walkerCorners[1]) * 0.5f;
        Vector3 walkerRightWorld = (walkerCorners[2] + walkerCorners[3]) * 0.5f;

        Vector3 localLeft = walkerParent.InverseTransformPoint(walkerLeftWorld);
        Vector3 localRight = walkerParent.InverseTransformPoint(walkerRightWorld);
        float localWidth = Mathf.Abs(localRight.x - localLeft.x);
        if (localWidth <= 0.0001f)
            return 0f;

        Vector3 localTrackLeft = walkerParent.InverseTransformPoint(leftWorld);
        Vector3 localTrackRight = walkerParent.InverseTransformPoint(rightWorld);
        float localTrackWidth = Mathf.Abs(localTrackRight.x - localTrackLeft.x);
        if (localTrackWidth <= 0.0001f)
            return 0f;

        float worldTrackWidth = Vector3.Distance(leftWorld, rightWorld);
        if (worldTrackWidth <= 0.0001f)
            return 0f;

        return (localWidth / localTrackWidth) * worldTrackWidth * 0.5f;
    }

    private static float SmoothTowards(float current, float target, float speed)
    {
        if (Mathf.Approximately(current, target))
            return target;

        float blend = 1f - Mathf.Exp(-Mathf.Max(0.01f, speed) * Time.unscaledDeltaTime);
        return Mathf.Lerp(current, target, blend);
    }

    private static string FormatDisplayName(string rawName)
    {
        return string.IsNullOrWhiteSpace(rawName) ? "<unknown>" : rawName.Replace('_', ' ');
    }

    private static string FormatTransitionLabel(TransitionType transitionType)
    {
        return transitionType switch
        {
            TransitionType.HubToRunStart => "Hub -> Corridor",
            TransitionType.BossToCorridor => "Boss -> Corridor",
            TransitionType.CorridorToBoss => "Corridor -> Boss",
            TransitionType.ReturnToHubAfterRun => "Run -> Hub",
            _ => "Route transition"
        };
    }

    private void UpdateDebugPreviewToggle()
    {
        if (!Input.GetKeyDown(debugPreviewToggleKey))
            return;

        debugPreviewActive = !debugPreviewActive;
        debugPreviewStartedRealtime = Time.realtimeSinceStartup;
        if (debugPreviewActive)
            BeginBatch(DebugPreviewBatchId);
    }

    private float EvaluateDebugPreviewProgress()
    {
        float elapsed = Mathf.Max(0f, Time.realtimeSinceStartup - debugPreviewStartedRealtime);
        float cycleSeconds = Mathf.Max(0.25f, debugPreviewCycleSeconds);
        float cycle01 = (elapsed % cycleSeconds) / cycleSeconds;
        return Mathf.Lerp(0.08f, 0.98f, cycle01);
    }
}
