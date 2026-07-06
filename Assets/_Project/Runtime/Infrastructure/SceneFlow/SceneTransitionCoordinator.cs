using System.Collections;
using CapstoneRuntime;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-857)]
[DisallowMultipleComponent]
/// <summary>
/// 책임 : 씬 전환 요청을 단일 코루틴으로 직렬화하고 페이드, 로딩 표시, Addressables 프리로드 handoff를 조율한다.
/// </summary>
public sealed class SceneTransitionCoordinator : MonoBehaviour, ISceneTransitionHandle
{
    public static SceneTransitionCoordinator Instance { get; private set; }

    private static bool s_isQuitting;
    private static readonly ISceneTransitionBackend PlaybackBackend = new SceneTransitionBackend();

    [Header("Loading Handoff")]
    [SerializeField, Min(0f)] private float loadingCompletionTimeoutSeconds = 15f;
    [SerializeField] private bool completePresentationPreloadBeforeSceneLoad = true;
    [SerializeField] private bool logLoadingHandoffDiagnostics;

    private Coroutine transitionRoutine;
    public bool IsTransitionActive => transitionRoutine != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoBootstrap()
    {
        SceneTransitionPlayback.RegisterBackend(PlaybackBackend);

        if (s_isQuitting || Instance != null)
            return;

        EnsureInstance();
    }

    public static SceneTransitionCoordinator EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        SceneTransitionCoordinator existing = RuntimeServiceOwnership.FindExistingService<SceneTransitionCoordinator>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        if (s_isQuitting)
            return null;

        GameObject host = RuntimeServiceOwnership.CreateServiceHost(nameof(SceneTransitionCoordinator));
        return host.AddComponent<SceneTransitionCoordinator>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        SceneTransitionPlayback.RegisterBackend(PlaybackBackend);
        RuntimeServiceOwnership.Adopt(this);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// 책임 : Core의 씬 전환 playback 요청을 현재 런타임 SceneTransitionCoordinator static 진입점으로 연결한다.
    /// </summary>
    private sealed class SceneTransitionBackend : ISceneTransitionBackend
    {
        public ISceneTransitionHandle Instance => SceneTransitionCoordinator.Instance;

        public ISceneTransitionHandle EnsureInstance()
        {
            return SceneTransitionCoordinator.EnsureInstance();
        }
    }

    private void OnApplicationQuit()
    {
        s_isQuitting = true;
    }

    public bool TryLoadScene(string targetSceneName)
    {
        return TryLoadScene(targetSceneName, fadeOutDurationOverride: null, fadeInDurationOverride: null);
    }

    public bool TryLoadScene(string targetSceneName, float fadeOutDurationOverride)
    {
        return TryLoadScene(
            targetSceneName,
            (float?)Mathf.Max(0f, fadeOutDurationOverride),
            fadeInDurationOverride: null);
    }

    public bool TryLoadScene(
        string targetSceneName,
        float fadeOutDurationOverride,
        float fadeInDurationOverride)
    {
        return TryLoadScene(
            targetSceneName,
            (float?)Mathf.Max(0f, fadeOutDurationOverride),
            (float?)Mathf.Max(0f, fadeInDurationOverride));
    }

    private bool TryLoadScene(
        string targetSceneName,
        float? fadeOutDurationOverride,
        float? fadeInDurationOverride)
    {
        if (string.IsNullOrWhiteSpace(targetSceneName) || transitionRoutine != null)
            return false;

        transitionRoutine = StartCoroutine(CoTransition(
            targetSceneName,
            fadeOutDurationOverride,
            fadeInDurationOverride));
        return true;
    }

    private IEnumerator CoTransition(
        string targetSceneName,
        float? fadeOutDurationOverride,
        float? fadeInDurationOverride)
    {
        SceneFadeTransitionService fadeService = SceneFadeTransitionService.EnsureInstance(allowRuntimeFallback: true);
        if (fadeService == null)
        {
            Debug.LogWarning(
                $"[SceneTransitionCoordinator] No fade service was available. Loading scene '{targetSceneName}' without transition fade.",
                this);
            transitionRoutine = null;
            LoadSceneImmediately(targetSceneName);
            yield break;
        }

        if (!fadeService.TryBeginTransitionSession())
        {
            Debug.LogWarning(
                $"[SceneTransitionCoordinator] Could not begin a fade transition session. Loading scene '{targetSceneName}' without transition fade.",
                this);
            transitionRoutine = null;
            LoadSceneImmediately(targetSceneName);
            yield break;
        }

        PortalRouteManager routeManager = PortalRouteManager.EnsureInstance();
        bool useLoadingPresentation =
            routeManager != null &&
            PortalRouteManager.IsCorridorEntryTransition(routeManager.LastLoadPresentationTransitionType);

        if (fadeOutDurationOverride.HasValue)
            yield return fadeService.FadeOutAsync(fadeOutDurationOverride.Value);
        else
            yield return fadeService.FadeOutAsync();

        LoadingOverlayController loadingOverlay = null;
        float loadingPhaseStartedRealtime = 0f;
        bool loadingPresentationRevealed = false;
        if (useLoadingPresentation)
        {
            PresentationPreloadService preloadService = PresentationPreloadService.EnsureInstance();
            loadingOverlay = LoadingOverlayController.EnsureInstance();
            loadingOverlay?.BeginManagedPresentation(showImmediately: false);
            preloadService?.RefreshActiveLoadWindow("Managed transition loading window");
            LogLoadingHandoffDiagnostics("after RefreshActiveLoadWindow");
            loadingPhaseStartedRealtime = Time.realtimeSinceStartup;

            if (completePresentationPreloadBeforeSceneLoad && loadingOverlay != null)
            {
                LogLoadingHandoffDiagnostics("before pre-scene WaitForManagedLoadingReady");
                yield return WaitForManagedLoadingReady(
                    loadingOverlay,
                    fadeService,
                    loadingPhaseStartedRealtime,
                    () => loadingPresentationRevealed,
                    value => loadingPresentationRevealed = value);
                LogLoadingHandoffDiagnostics("after pre-scene WaitForManagedLoadingReady");
            }
        }

        AsyncOperation loadOperation = null;
        try
        {
            loadOperation = SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Single);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[SceneTransitionCoordinator] Failed to load scene '{targetSceneName}': {ex.Message}", this);
        }

        if (loadOperation == null)
        {
            if (loadingOverlay != null)
            {
                fadeService.ShowBlackImmediately();
                loadingOverlay.ForceHidePresentation();
            }

            yield return FadeInAsync(fadeService, fadeInDurationOverride);
            fadeService.EndTransitionSession();
            transitionRoutine = null;
            yield break;
        }

        while (!loadOperation.isDone)
        {
            TryRevealDelayedLoadingPresentation(
                loadingOverlay,
                fadeService,
                loadingPhaseStartedRealtime,
                ref loadingPresentationRevealed);
            yield return null;
        }
        LogLoadingHandoffDiagnostics("after scene LoadSceneAsync completed");

        fadeService = ResolvePostLoadFadeService(fadeService, targetSceneName, this);
        if (fadeService == null)
        {
            transitionRoutine = null;
            yield break;
        }

        yield return fadeService.WaitForPostLoadSettleAsync();
        LogLoadingHandoffDiagnostics("after post-load settle");

        if (loadingOverlay != null)
        {
            if (!completePresentationPreloadBeforeSceneLoad)
            {
                LogLoadingHandoffDiagnostics("before post-scene WaitForManagedLoadingReady");
                yield return WaitForManagedLoadingReady(
                    loadingOverlay,
                    fadeService,
                    loadingPhaseStartedRealtime,
                    () => loadingPresentationRevealed,
                    value => loadingPresentationRevealed = value);
                LogLoadingHandoffDiagnostics("after post-scene WaitForManagedLoadingReady");
            }

            routeManager?.CompleteLoadPresentationContext("Managed loading presentation completed.");
            fadeService.ShowBlackImmediately();
            loadingOverlay.ForceHidePresentation();
            yield return FadeInAsync(fadeService, fadeInDurationOverride);
            LogLoadingHandoffDiagnostics("after managed fade-in before player unlock");
        }
        else
        {
            yield return FadeInAsync(fadeService, fadeInDurationOverride);
            LogLoadingHandoffDiagnostics("after fade-in before player unlock");
        }

        fadeService.EndTransitionSession();
        LogLoadingHandoffDiagnostics("after EndTransitionSession");
        transitionRoutine = null;
    }

    private static SceneFadeTransitionService ResolvePostLoadFadeService(
        SceneFadeTransitionService current,
        string targetSceneName,
        Object logContext)
    {
        if (current != null)
            return current;

        SceneFadeTransitionService recovered =
            SceneFadeTransitionService.EnsureInstance(allowRuntimeFallback: true);
        if (recovered == null)
        {
            Debug.LogWarning(
                $"[SceneTransitionCoordinator] Fade service was destroyed while loading scene '{targetSceneName}', and no replacement was available for fade-in.",
                logContext);
            return null;
        }

        if (!recovered.IsTransitionActive)
        {
            if (recovered.TryBeginTransitionSession())
            {
                recovered.ShowBlackImmediately();
            }
            else
            {
                Debug.LogWarning(
                    $"[SceneTransitionCoordinator] Fade service was destroyed while loading scene '{targetSceneName}'. A replacement was found, but it could not begin a recovered fade session.",
                    logContext);
            }
        }

        return recovered;
    }

    private static IEnumerator FadeInAsync(
        SceneFadeTransitionService fadeService,
        float? fadeInDurationOverride)
    {
        if (fadeInDurationOverride.HasValue)
            yield return fadeService.FadeInAsync(fadeInDurationOverride.Value);
        else
            yield return fadeService.FadeInAsync();
    }

    private IEnumerator WaitForManagedLoadingReady(
        LoadingOverlayController loadingOverlay,
        SceneFadeTransitionService fadeService,
        float loadingPhaseStartedRealtime,
        System.Func<bool> getLoadingPresentationRevealed,
        System.Action<bool> setLoadingPresentationRevealed)
    {
        float timeoutSeconds = Mathf.Max(0f, loadingCompletionTimeoutSeconds);
        float elapsed = 0f;

        while (loadingOverlay != null && !loadingOverlay.IsManagedPresentationReadyToComplete())
        {
            bool revealed = getLoadingPresentationRevealed != null && getLoadingPresentationRevealed();
            if (TryRevealDelayedLoadingPresentation(
                    loadingOverlay,
                    fadeService,
                    loadingPhaseStartedRealtime,
                    ref revealed))
            {
                setLoadingPresentationRevealed?.Invoke(revealed);
            }

            if (timeoutSeconds > 0f && elapsed >= timeoutSeconds)
            {
                Debug.LogWarning(
                    "[SceneTransitionCoordinator] Timed out waiting for managed loading presentation to complete.",
                    this);
                break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private static bool TryRevealDelayedLoadingPresentation(
        LoadingOverlayController loadingOverlay,
        SceneFadeTransitionService fadeService,
        float loadingPhaseStartedRealtime,
        ref bool loadingPresentationRevealed)
    {
        if (loadingPresentationRevealed || loadingOverlay == null || loadingPhaseStartedRealtime <= 0f)
            return false;

        float elapsed = Time.realtimeSinceStartup - loadingPhaseStartedRealtime;
        if (elapsed < loadingOverlay.DelayedRevealSeconds)
            return false;

        loadingOverlay.RevealManagedPresentation(immediate: true);
        fadeService?.HideOverlayImmediately();
        loadingPresentationRevealed = true;
        return true;
    }

    private void LoadSceneImmediately(string targetSceneName)
    {
        try
        {
            SceneManager.LoadScene(targetSceneName, LoadSceneMode.Single);
        }
        catch (System.Exception ex)
        {
            Debug.LogError(
                $"[SceneTransitionCoordinator] Failed to load scene '{targetSceneName}' without transition fade: {ex.Message}",
            this);
        }
    }

    private void LogLoadingHandoffDiagnostics(string phase)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!logLoadingHandoffDiagnostics)
            return;

        int currentBatchId = PresentationPreloadService.GetCurrentProviderBatchId();
        int currentBatchPending = PresentationPreloadService.GetCurrentBatchPendingProviderOperationCount();
        int totalPending = PresentationPreloadService.GetPendingProviderOperationCount();
        float progress = PresentationPreloadService.GetCurrentProviderProgress01();
        string providerStatus = AddressableAssetProvider.Instance != null
            ? AddressableAssetProvider.Instance.BuildRuntimeQueueDiagnosticSummary()
            : "AddressableAssetProvider=null";

        Debug.Log(
            $"[LoadingHandoffDiagnostics] {phase}: batch={currentBatchId}, currentPending={currentBatchPending}, totalPending={totalPending}, progress={progress * 100f:0.0}%, {providerStatus}",
            this);
#endif
    }
}
