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
    private ISceneEntryReadiness failedSceneEntry;
    private bool retrySceneEntryRequested;
    private Vector2 sceneFailureScroll;
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

    private IEnumerator WaitForSceneContentReady(Scene scene)
    {
        // Start-time generators must run before checking their result, even with zero settle frames.
        yield return null;
        var producers = new System.Collections.Generic.List<MonoBehaviour>();
        foreach (var root in scene.GetRootGameObjects())
            producers.AddRange(root.GetComponentsInChildren<MonoBehaviour>(true));
        try
        {
            foreach (var producer in producers)
            {
                if (producer is not ISceneEntryReadiness readiness) continue;
                while (producer != null && producer.isActiveAndEnabled && !readiness.IsSceneEntryReady)
                {
                    failedSceneEntry = readiness;
                    if (retrySceneEntryRequested)
                    {
                        retrySceneEntryRequested = false;
                        readiness.RetrySceneEntry();
                    }
                    yield return null;
                }
            }
        }
        finally
        {
            failedSceneEntry = null;
            retrySceneEntryRequested = false;
        }
    }

    private void OnGUI()
    {
        if (failedSceneEntry == null || string.IsNullOrEmpty(failedSceneEntry.SceneEntryFailure)) return;
        int oldDepth = GUI.depth;
        GUI.depth = -10000;
        float width = Mathf.Min(620f, Screen.width - 24f);
        GUI.ModalWindow(GetInstanceID(), new Rect((Screen.width - width) / 2f,
            Mathf.Max(12f, (Screen.height - 240f) / 2f), width, 240f), _ =>
        {
            GUILayout.Label("맵을 준비하지 못했습니다. 빈 맵으로 진입하지 않도록 이동을 보류했습니다.");
            sceneFailureScroll = GUILayout.BeginScrollView(sceneFailureScroll, GUILayout.Height(130f));
            GUILayout.Label(failedSceneEntry.SceneEntryFailure, new GUIStyle(GUI.skin.label) { wordWrap = true });
            GUILayout.EndScrollView();
            if (GUILayout.Button("다시 시도", GUILayout.Height(36f))) retrySceneEntryRequested = true;
        }, "맵 생성 실패");
        GUI.depth = oldDepth;
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
        return TryLoadScene(
            targetSceneName,
            SceneTransitionVisualMode.AlphaFade,
            fadeOutDurationOverride: null,
            fadeInDurationOverride: null);
    }

    public bool TryLoadScene(string targetSceneName, float fadeOutDurationOverride)
    {
        return TryLoadScene(
            targetSceneName,
            SceneTransitionVisualMode.AlphaFade,
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
            SceneTransitionVisualMode.AlphaFade,
            (float?)Mathf.Max(0f, fadeOutDurationOverride),
            (float?)Mathf.Max(0f, fadeInDurationOverride));
    }

    /// <summary>
    /// 책임 : 데이터 기반 이동 연결이 선택한 화면 전환 방식과 양쪽 재생 시간을 적용해 씬 로드를 시작한다.
    /// </summary>
    public bool TryLoadScene(
        string targetSceneName,
        SceneTransitionVisualMode visualMode,
        float coverDuration,
        float revealDuration)
    {
        return TryLoadScene(
            targetSceneName,
            visualMode,
            (float?)Mathf.Max(0f, coverDuration),
            (float?)Mathf.Max(0f, revealDuration));
    }

    private bool TryLoadScene(
        string targetSceneName,
        SceneTransitionVisualMode visualMode,
        float? fadeOutDurationOverride,
        float? fadeInDurationOverride)
    {
        if (string.IsNullOrWhiteSpace(targetSceneName) || transitionRoutine != null)
            return false;

        transitionRoutine = StartCoroutine(CoTransition(
            targetSceneName,
            visualMode,
            fadeOutDurationOverride,
            fadeInDurationOverride));
        return true;
    }

    private IEnumerator CoTransition(
        string targetSceneName,
        SceneTransitionVisualMode visualMode,
        float? fadeOutDurationOverride,
        float? fadeInDurationOverride)
    {
        SceneFadeTransitionService fadeService = SceneFadeTransitionService.EnsureInstance(allowRuntimeFallback: true);
        if (fadeService == null)
        {
            CapstoneDiagnostics.EditorOnlyLog.LogWarning(
                $"[SceneTransitionCoordinator] No fade service was available. Loading scene '{targetSceneName}' without transition fade.",
                this);
            transitionRoutine = null;
            LoadSceneImmediately(targetSceneName);
            yield break;
        }

        if (!fadeService.TryBeginTransitionSession())
        {
            CapstoneDiagnostics.EditorOnlyLog.LogWarning(
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

        yield return CoverAsync(fadeService, visualMode, fadeOutDurationOverride);

        // Accept the title preload overlay only after the scene fade fully covers it.
        LoadingOverlayController.Instance?.ForceHidePresentation();

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
            Debug.LogError(
                $"[SceneTransitionCoordinator] LoadSceneAsync returned null for scene '{targetSceneName}'. Check scene name and Build Settings registration.",
                this);

            if (loadingOverlay != null)
            {
                fadeService.ShowBlackImmediately();
                loadingOverlay.ForceHidePresentation();
            }

            yield return RevealAsync(fadeService, visualMode, fadeInDurationOverride);
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
        yield return WaitForSceneContentReady(SceneManager.GetActiveScene());
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
            yield return RevealAsync(fadeService, visualMode, fadeInDurationOverride);
            LogLoadingHandoffDiagnostics("after managed fade-in before player unlock");
        }
        else
        {
            yield return RevealAsync(fadeService, visualMode, fadeInDurationOverride);
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
            CapstoneDiagnostics.EditorOnlyLog.LogWarning(
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
                CapstoneDiagnostics.EditorOnlyLog.LogWarning(
                    $"[SceneTransitionCoordinator] Fade service was destroyed while loading scene '{targetSceneName}'. A replacement was found, but it could not begin a recovered fade session.",
                    logContext);
            }
        }

        return recovered;
    }

    private static IEnumerator CoverAsync(
        SceneFadeTransitionService fadeService,
        SceneTransitionVisualMode visualMode,
        float? durationOverride)
    {
        if (visualMode == SceneTransitionVisualMode.HorizontalWipeRightToLeft)
        {
            yield return fadeService.WipeCoverRightToLeftAsync(durationOverride ?? 0.2f);
            yield break;
        }

        if (durationOverride.HasValue)
            yield return fadeService.FadeOutAsync(durationOverride.Value);
        else
            yield return fadeService.FadeOutAsync();
    }

    private static IEnumerator RevealAsync(
        SceneFadeTransitionService fadeService,
        SceneTransitionVisualMode visualMode,
        float? durationOverride)
    {
        if (visualMode == SceneTransitionVisualMode.HorizontalWipeRightToLeft)
        {
            yield return fadeService.WipeRevealRightToLeftAsync(durationOverride ?? 0.2f);
            yield break;
        }

        if (durationOverride.HasValue)
            yield return fadeService.FadeInAsync(durationOverride.Value);
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
                CapstoneDiagnostics.EditorOnlyLog.LogWarning(
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

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
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

        CapstoneDiagnostics.EditorOnlyLog.Log(
            $"[LoadingHandoffDiagnostics] {phase}: batch={currentBatchId}, currentPending={currentBatchPending}, totalPending={totalPending}, progress={progress * 100f:0.0}%, {providerStatus}",
            this);
#endif
    }
}
