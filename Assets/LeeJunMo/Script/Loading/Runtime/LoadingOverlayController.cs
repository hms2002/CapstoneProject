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

    private static bool s_isQuitting;
    private const int DebugPreviewBatchId = -7001;
    private const float FallbackTrackWidth = 560f;
    private const float TravelRange = 172f;

    [Header("Policy")]
    [SerializeField] private bool showOnlyForCorridorEntry = true;

    [Header("View")]
    [SerializeField] private LoadingOverlayView overlayView;

    [Header("Timing")]
    [SerializeField, Min(0.01f)] private float fadeInSeconds = 0.12f;
    [SerializeField, Min(0.01f)] private float fadeOutSeconds = 0.18f;
    [SerializeField, Min(0f)] private float minimumVisibleSeconds = 0.35f;
    [SerializeField, Min(0.01f)] private float activeProgressFollowSpeed = 9f;
    [SerializeField, Min(0.01f)] private float completionProgressFollowSpeed = 15f;
    [SerializeField, Min(1f)] private float tipCycleSeconds = 5.5f;

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
    private TMP_Text tipLabelText;
    private TMP_Text tipText;
    private RectTransform travelHost;
    private RectTransform defaultTravelRoot;
    private RectTransform travelTrackBoundsRect;
    private Image travelTrackFillImage;
    private RectTransform travelWalkerRect;
    private Vector2 baseTravelWalkerAnchoredPosition;
    private bool hasBaseTravelWalkerAnchoredPosition;

    private GameObject activeCustomTravelVisualInstance;
    private GameObject boundCustomTravelVisualPrefab;
    private int observedBatchId;
    private bool targetVisible;
    private float visibleSinceRealtime;
    private float displayedProgress;
    private float shimmerPhase;
    private bool debugPreviewActive;
    private float debugPreviewStartedRealtime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoBootstrap()
    {
        if (s_isQuitting || Instance != null)
            return;

        LoadingOverlayController existing = RuntimeServiceOwnership.FindExistingService<LoadingOverlayController>();
        if (existing != null)
            Instance = existing;
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

        return null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        RuntimeServiceOwnership.Adopt(this);
        ResolveViewIfNeeded();
        BindTravelVisual();
        debugPreviewActive = startWithDebugPreview;
        debugPreviewStartedRealtime = Time.realtimeSinceStartup;
    }

    private void OnDestroy()
    {
        if (activeCustomTravelVisualInstance != null)
            Destroy(activeCustomTravelVisualInstance);

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
        ResolveViewIfNeeded();
        BindTravelVisual();
        UpdateDebugPreviewToggle();

        PortalRouteManager routeManager = PortalRouteManager.Instance;
        int batchId = PresentationPreloadService.GetCurrentProviderBatchId();
        int pendingCount = PresentationPreloadService.GetCurrentBatchPendingProviderOperationCount();
        float providerProgress = PresentationPreloadService.GetCurrentProviderProgress01();
        bool realBatchActive = batchId > 0 && (pendingCount > 0 || providerProgress < 0.999f);
        bool allowedRealBatch = realBatchActive && ShouldShowRealBatch(routeManager);
        bool previewBatch = debugPreviewActive;
        bool batchActive = previewBatch || allowedRealBatch;
        int effectiveBatchId = previewBatch ? DebugPreviewBatchId : allowedRealBatch ? batchId : 0;
        int effectivePendingCount = previewBatch ? 1 : allowedRealBatch ? pendingCount : 0;
        float effectiveProgress = previewBatch ? EvaluateDebugPreviewProgress() : allowedRealBatch ? providerProgress : 1f;

        if (batchActive && effectiveBatchId != observedBatchId)
            BeginBatch(effectiveBatchId);

        if (batchActive)
            targetVisible = true;

        float targetProgress = batchActive ? effectiveProgress : 1f;
        if (effectiveBatchId != 0 && observedBatchId == effectiveBatchId)
            targetProgress = Mathf.Max(displayedProgress, targetProgress);

        if (previewBatch && targetProgress >= 0.985f)
            displayedProgress = 0f;

        float followSpeed = batchActive ? activeProgressFollowSpeed : completionProgressFollowSpeed;
        displayedProgress = SmoothTowards(displayedProgress, targetProgress, followSpeed);
        displayedProgress = Mathf.Clamp01(displayedProgress);

        if (!batchActive && targetVisible)
        {
            bool visibleLongEnough = Time.realtimeSinceStartup - visibleSinceRealtime >= minimumVisibleSeconds;
            if (visibleLongEnough && displayedProgress >= 0.999f)
                targetVisible = false;
        }

        UpdateCopy(routeManager, effectivePendingCount, batchActive, previewBatch);
        UpdateVisualState();
        IsActiveLoadingPresentation =
            !previewBatch &&
            overlayRoot != null &&
            canvasGroup != null &&
            routeManager != null &&
            PortalRouteManager.IsCorridorEntryTransition(routeManager.LastLoadPresentationTransitionType) &&
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

    private void ResolveViewIfNeeded()
    {
        LoadingOverlayView desiredView = ResolveDesiredView();

        if (desiredView == null)
        {
            ClearResolvedView();
            return;
        }

        if (boundOverlayView == desiredView && overlayRoot != null && canvasGroup != null)
            return;

        boundOverlayView = desiredView;
        overlayRoot = desiredView.Root;
        canvasGroup = desiredView.CanvasGroup;
        titleText = desiredView.TitleText;
        statusText = desiredView.StatusText;
        detailText = desiredView.DetailText;
        percentText = desiredView.PercentText;
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
    }

    private LoadingOverlayView ResolveDesiredView()
    {
        if (overlayView != null)
            return overlayView;

        Canvas loadingCanvas = GlobalUIRoot.GetCanvas(GlobalCanvasLayer.Loading);
        if (loadingCanvas != null)
        {
            LoadingOverlayView canvasView = loadingCanvas.GetComponentInChildren<LoadingOverlayView>(includeInactive: true);
            if (canvasView != null)
                return canvasView;
        }

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

        if (progressFillImage != null)
            progressFillImage.fillAmount = displayedProgress;

        if (progressGlowRect != null)
        {
            RectTransform glowParent = progressGlowRect.parent as RectTransform;
            float trackWidth = glowParent != null ? Mathf.Max(48f, glowParent.rect.width) : FallbackTrackWidth;
            shimmerPhase = (shimmerPhase + Time.unscaledDeltaTime * 1.8f) % 1f;
            float width = Mathf.Max(48f, progressGlowRect.sizeDelta.x);
            float visibleWidth = Mathf.Max(48f, trackWidth * displayedProgress);
            float x = Mathf.Lerp(-width, Mathf.Max(-width, visibleWidth - width), shimmerPhase);
            progressGlowRect.anchoredPosition = new Vector2(x, progressGlowRect.anchoredPosition.y);
        }

        UpdateDefaultTravelVisual();
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
