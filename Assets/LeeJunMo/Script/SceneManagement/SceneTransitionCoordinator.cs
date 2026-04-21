using System.Collections;
using CapstoneRuntime;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-857)]
[DisallowMultipleComponent]
public sealed class SceneTransitionCoordinator : MonoBehaviour
{
    public static SceneTransitionCoordinator Instance { get; private set; }

    private static bool s_isQuitting;

    [Header("Loading Handoff")]
    [SerializeField, Min(0f)] private float loadingCompletionTimeoutSeconds = 15f;
    [SerializeField] private bool completePresentationPreloadBeforeSceneLoad = true;

    private Coroutine transitionRoutine;
    public bool IsTransitionActive => transitionRoutine != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoBootstrap()
    {
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
        RuntimeServiceOwnership.Adopt(this);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnApplicationQuit()
    {
        s_isQuitting = true;
    }

    public bool TryLoadScene(string targetSceneName)
    {
        if (string.IsNullOrWhiteSpace(targetSceneName) || transitionRoutine != null)
            return false;

        transitionRoutine = StartCoroutine(CoTransition(targetSceneName));
        return true;
    }

    private IEnumerator CoTransition(string targetSceneName)
    {
        SceneFadeTransitionService fadeService = SceneFadeTransitionService.EnsureInstance(allowRuntimeFallback: true);
        if (fadeService == null || !fadeService.TryBeginTransitionSession())
        {
            transitionRoutine = null;
            yield break;
        }

        PortalRouteManager routeManager = PortalRouteManager.EnsureInstance();
        bool useLoadingPresentation =
            routeManager != null &&
            PortalRouteManager.IsCorridorEntryTransition(routeManager.LastLoadPresentationTransitionType);

        yield return fadeService.FadeOutAsync();

        LoadingOverlayController loadingOverlay = null;
        if (useLoadingPresentation)
        {
            PresentationPreloadService preloadService = PresentationPreloadService.EnsureInstance();
            loadingOverlay = LoadingOverlayController.EnsureInstance();
            loadingOverlay?.BeginManagedPresentation(showImmediately: true);
            preloadService?.RefreshActiveLoadWindow("Managed transition loading window");
            fadeService.HideOverlayImmediately();

            if (completePresentationPreloadBeforeSceneLoad && loadingOverlay != null)
                yield return WaitForManagedLoadingReady(loadingOverlay);
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

            yield return fadeService.FadeInAsync();
            fadeService.EndTransitionSession();
            transitionRoutine = null;
            yield break;
        }

        while (!loadOperation.isDone)
            yield return null;

        yield return fadeService.WaitForPostLoadSettleAsync();

        if (loadingOverlay != null)
        {
            if (!completePresentationPreloadBeforeSceneLoad)
                yield return WaitForManagedLoadingReady(loadingOverlay);

            routeManager?.CompleteLoadPresentationContext("Managed loading presentation completed.");
            fadeService.ShowBlackImmediately();
            loadingOverlay.ForceHidePresentation();
            yield return fadeService.FadeInAsync();
        }
        else
        {
            yield return fadeService.FadeInAsync();
        }

        fadeService.EndTransitionSession();
        transitionRoutine = null;
    }

    private IEnumerator WaitForManagedLoadingReady(LoadingOverlayController loadingOverlay)
    {
        float timeoutSeconds = Mathf.Max(0f, loadingCompletionTimeoutSeconds);
        float elapsed = 0f;

        while (loadingOverlay != null && !loadingOverlay.IsManagedPresentationReadyToComplete())
        {
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
}
