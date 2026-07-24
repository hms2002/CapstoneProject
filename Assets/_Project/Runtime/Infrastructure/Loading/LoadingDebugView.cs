using System.Collections.Generic;
using CapstonePresentation;
using CapstoneRuntime;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-830)]
[DisallowMultipleComponent]
// 이 클래스의 책임:
// 로딩/프리로드/Addressables 상태를 개발 중에 관찰하고, 필요한 진단 리포트를 수동으로 생성한다.
public sealed class LoadingDebugView : MonoBehaviour
{
    private readonly struct SceneEventEntry
    {
        public SceneEventEntry(float realtimeSeconds, string message)
        {
            RealtimeSeconds = realtimeSeconds;
            Message = message;
        }

        public float RealtimeSeconds { get; }
        public string Message { get; }
    }

    public static LoadingDebugView Instance { get; private set; }

    private static bool s_isQuitting;
    private static bool s_visible;
    private const int MaxSceneHistoryEntries = 32;

    [SerializeField] private KeyCode toggleKey = KeyCode.F8;
    [SerializeField] private KeyCode dumpRetainedAssetsKey = KeyCode.F9;
    [SerializeField] private bool startVisible;
    [SerializeField, Min(4)] private int maxListedEntries = 12;

    private readonly List<SceneEventEntry> sceneHistory = new();
    private Rect windowRect = new(16f, 16f, 520f, 640f);
    private Vector2 scrollPosition;
    private GUIStyle windowStyle;
    private GUIStyle headerStyle;
    private GUIStyle bodyStyle;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoBootstrap()
    {
        if (s_isQuitting || Instance != null)
            return;

        EnsureInstance();
    }
#endif

    public static LoadingDebugView EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        LoadingDebugView existing = RuntimeServiceOwnership.FindExistingService<LoadingDebugView>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject host = RuntimeServiceOwnership.CreateServiceHost(nameof(LoadingDebugView));
        return host.AddComponent<LoadingDebugView>();
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
        if (!s_visible)
            s_visible = startVisible;

        RecordSceneEvent($"Scene watcher ready. active={SafeSceneName(SceneManager.GetActiveScene())}");
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        SceneManager.sceneUnloaded += HandleSceneUnloaded;
        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
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

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            s_visible = !s_visible;

        if (Input.GetKeyDown(dumpRetainedAssetsKey))
            DumpRetainedAssets();
    }

    private void OnGUI()
    {
        if (!ShouldDraw() || Event.current == null || GUI.skin == null)
            return;

        EnsureStyles();
        windowRect = GUI.Window(GetInstanceID(), windowRect, DrawWindow, "Loading Debug", windowStyle);
    }

    private bool ShouldDraw()
    {
        if (!s_visible)
            return false;

#if UNITY_EDITOR
        return true;
#else
        return Debug.isDebugBuild;
#endif
    }

    private void DrawWindow(int windowId)
    {
        PresentationPreloadService preload = PresentationPreloadService.Instance ?? PresentationPreloadService.EnsureInstance();
        PortalRouteManager routeManager = preload != null && preload.BoundRouteManager != null
            ? preload.BoundRouteManager
            : PortalRouteManager.Instance;

        using (var scroll = new GUILayout.ScrollViewScope(scrollPosition))
        {
            scrollPosition = scroll.scrollPosition;

            DrawHeaderLine($"Toggle: {toggleKey}");
            DrawHeaderLine($"Dump Retained Assets: {dumpRetainedAssetsKey}");
            DrawHeaderLine($"Route Catalog: {SafeName(routeManager != null ? routeManager.ActiveRouteCatalog : null)}");
            DrawHeaderLine($"Current Stage: {BuildStageLabel(routeManager)}");
            DrawHeaderLine($"Current RouteSet: {SafeName(routeManager != null ? routeManager.CurrentStageSet : null)}");
            DrawHeaderLine($"Next RouteSet: {SafeName(routeManager != null ? routeManager.NextStageSet : null)}");
            DrawHeaderLine($"Last Transition: {(routeManager != null ? routeManager.LastTransitionEvent : "<none>")}");

            GUILayout.Space(6f);
            DrawSection("Active Preload Window");
            DrawBodyLine($"Boot: {SafeName(preload != null ? preload.ActiveBootManifest : null)}");
            DrawBodyLine($"FirstRunIntro: {SafeName(preload != null ? preload.ActiveFirstRunIntroManifest : null)}");
            DrawBodyLine($"RunCommon: {SafeName(preload != null ? preload.ActiveRunCommonManifest : null)}");
            DrawBodyLine($"Current: {SafeName(preload != null ? preload.ActiveCurrentStageManifest : null)}");
            DrawBodyLine($"Next: {SafeName(preload != null ? preload.ActiveNextStageManifest : null)}");

            GUILayout.Space(6f);
            DrawSection("Load Window History");
            DrawWindowHistoryEntries(PresentationPreloadService.GetWindowHistorySnapshot(maxListedEntries));

            GUILayout.Space(6f);
            DrawSection("Async Provider Ops");
            DrawProviderOperationEntries(PresentationPreloadService.GetProviderOperationSnapshot(maxListedEntries));

            GUILayout.Space(6f);
            DrawSection("Transition History");
            DrawTransitionEntries(routeManager != null
                ? routeManager.GetTransitionHistorySnapshot(maxListedEntries)
                : System.Array.Empty<PortalRouteManager.DebugTransitionEntry>());

            GUILayout.Space(6f);
            DrawSection("Loaded Scenes");
            DrawTextEntries(BuildLoadedSceneSnapshot());

            GUILayout.Space(6f);
            DrawSection("Scene History");
            DrawSceneEntries(GetSceneHistorySnapshot(maxListedEntries));

            GUILayout.Space(6f);
            DrawSection("Provider Counts");
            DrawBodyLine($"Provider: {PresentationAssetProvider.GetCurrentProviderName()}");
            DrawBodyLine($"Provider Override: {(PresentationAssetProvider.IsProviderOverrideActive ? "yes" : "no")}");
            DrawBodyLine($"Loaded Manifests: {PresentationAssetProvider.GetLoadedManifestCount()}");
            DrawBodyLine($"Loaded Route Manifests: {PresentationAssetProvider.GetLoadedRouteManifestCount()}");
            DrawBodyLine($"Retained Assets: {PresentationAssetProvider.GetRetainedAssetCount()}");
            DrawBodyLine($"Prewarmed Prefabs: {PresentationAssetProvider.GetPrewarmedPrefabCount()}");
            DrawBodyLine($"Pending Async Ops: {PresentationPreloadService.GetPendingProviderOperationCount()}");
            DrawBodyLine($"Current Batch Pending: {PresentationPreloadService.GetCurrentBatchPendingProviderOperationCount()}");
            DrawBodyLine($"Provider Progress: {PresentationPreloadService.GetCurrentProviderProgress01() * 100f:0}%");
            DrawBodyLine($"Pool Types: {PresentationSpawnService.GetPooledPrefabTypeCount()}");
            DrawBodyLine($"Pooled Instances: {PresentationSpawnService.GetTotalPooledInstanceCount()}");

            GUILayout.Space(6f);
            DrawSection("Provider Ref History");
            DrawProviderHistoryEntries(PresentationAssetProvider.GetDebugHistorySnapshot(maxListedEntries));

            GUILayout.Space(6f);
            DrawSection("Manifest Refs");
            DrawCountEntries(PresentationAssetProvider.GetManifestSnapshot());

            GUILayout.Space(6f);
            DrawSection("Route Manifest Refs");
            DrawCountEntries(PresentationAssetProvider.GetRouteManifestSnapshot());

            GUILayout.Space(6f);
            DrawSection("Top Retained Assets");
            DrawCountEntries(PresentationAssetProvider.GetAssetSnapshot(maxListedEntries));

            GUILayout.Space(6f);
            DrawSection("Top Prewarm Refs");
            DrawCountEntries(PresentationAssetProvider.GetPrewarmSnapshot(maxListedEntries));

            GUILayout.Space(6f);
            DrawSection("Pool Snapshot");
            DrawPoolEntries(PresentationSpawnService.GetPoolSnapshot(maxListedEntries));
        }

        GUI.DragWindow(new Rect(0f, 0f, windowRect.width, 24f));
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RecordSceneEvent($"Loaded {SafeSceneName(scene)} ({mode})");
    }

    private void DumpRetainedAssets()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (AddressableAssetProvider.Instance == null)
        {
            Debug.LogWarning("[LoadingDebugView] AddressableAssetProvider is not active; retained asset report was not written.", this);
            return;
        }

        AddressableAssetProvider.Instance.DumpRetainedAssetsToTextFile();
#endif
    }

    private void HandleSceneUnloaded(Scene scene)
    {
        RecordSceneEvent($"Unloaded {SafeSceneName(scene)}");
    }

    private void HandleActiveSceneChanged(Scene previousScene, Scene nextScene)
    {
        RecordSceneEvent(
            $"Active scene {SafeSceneName(previousScene)} -> {SafeSceneName(nextScene)}");
    }

    private void DrawCountEntries(PresentationAssetProvider.DebugCountEntry[] entries)
    {
        if (entries == null || entries.Length == 0)
        {
            DrawBodyLine("<none>");
            return;
        }

        for (int i = 0; i < entries.Length; i++)
            DrawBodyLine($"{entries[i].Name} x{entries[i].Count}");
    }

    private void DrawPoolEntries(PresentationSpawnService.PoolDebugEntry[] entries)
    {
        if (entries == null || entries.Length == 0)
        {
            DrawBodyLine("<none>");
            return;
        }

        for (int i = 0; i < entries.Length; i++)
            DrawBodyLine($"{entries[i].Name} x{entries[i].PooledCount}");
    }

    private void DrawWindowHistoryEntries(PresentationPreloadService.DebugWindowEvent[] entries)
    {
        if (entries == null || entries.Length == 0)
        {
            DrawBodyLine("<none>");
            return;
        }

        for (int i = 0; i < entries.Length; i++)
        {
            PresentationPreloadService.DebugWindowEvent entry = entries[i];
            DrawBodyLine(
                $"[{entry.RealtimeSeconds:0.0}] {entry.Reason} | boot={entry.BootManifestName}, firstRun={entry.FirstRunIntroManifestName}, run={entry.RunCommonManifestName}, current={entry.CurrentManifestName}, next={entry.NextManifestName}");
        }
    }

    private void DrawTransitionEntries(PortalRouteManager.DebugTransitionEntry[] entries)
    {
        if (entries == null || entries.Length == 0)
        {
            DrawBodyLine("<none>");
            return;
        }

        for (int i = 0; i < entries.Length; i++)
        {
            PortalRouteManager.DebugTransitionEntry entry = entries[i];
            DrawBodyLine($"[{entry.RealtimeSeconds:0.0}] {entry.Message}");
        }
    }

    private void DrawProviderOperationEntries(PresentationPreloadService.DebugProviderOperationEntry[] entries)
    {
        if (entries == null || entries.Length == 0)
        {
            DrawBodyLine("<none>");
            return;
        }

        for (int i = 0; i < entries.Length; i++)
        {
            PresentationPreloadService.DebugProviderOperationEntry entry = entries[i];
            string status = entry.IsDone
                ? (entry.Succeeded ? "done" : "failed")
                : "pending";
            string errorSuffix = !entry.Succeeded && !string.IsNullOrEmpty(entry.ErrorMessage)
                ? $" | {entry.ErrorMessage}"
                : string.Empty;
            DrawBodyLine(
                $"[{entry.StartedRealtimeSeconds:0.0}] {entry.SlotName} {entry.ActionName} {entry.TargetName} | {status} {entry.Progress01 * 100f:0}% {entry.ElapsedSeconds:0.0}s{errorSuffix}");
        }
    }

    private void DrawProviderHistoryEntries(PresentationAssetProvider.DebugEventEntry[] entries)
    {
        if (entries == null || entries.Length == 0)
        {
            DrawBodyLine("<none>");
            return;
        }

        for (int i = 0; i < entries.Length; i++)
        {
            PresentationAssetProvider.DebugEventEntry entry = entries[i];
            DrawBodyLine($"[{entry.RealtimeSeconds:0.0}] {entry.Message}");
        }
    }

    private void DrawTextEntries(string[] entries)
    {
        if (entries == null || entries.Length == 0)
        {
            DrawBodyLine("<none>");
            return;
        }

        for (int i = 0; i < entries.Length; i++)
            DrawBodyLine(entries[i]);
    }

    private void DrawSceneEntries(SceneEventEntry[] entries)
    {
        if (entries == null || entries.Length == 0)
        {
            DrawBodyLine("<none>");
            return;
        }

        for (int i = 0; i < entries.Length; i++)
            DrawBodyLine($"[{entries[i].RealtimeSeconds:0.0}] {entries[i].Message}");
    }

    private void DrawSection(string title)
    {
        GUILayout.Label(title, headerStyle);
    }

    private void DrawHeaderLine(string line)
    {
        GUILayout.Label(line, headerStyle);
    }

    private void DrawBodyLine(string line)
    {
        GUILayout.Label(line, bodyStyle);
    }

    private void EnsureStyles()
    {
        if (windowStyle == null)
        {
            windowStyle = new GUIStyle(GUI.skin.window)
            {
                padding = new RectOffset(10, 10, 24, 10)
            };
        }

        if (headerStyle == null)
        {
            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };
        }

        if (bodyStyle == null)
        {
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                wordWrap = true
            };
        }
    }

    private static string SafeName(Object target)
    {
        return target != null ? target.name : "<none>";
    }

    private static string BuildStageLabel(PortalRouteManager routeManager)
    {
        if (routeManager == null || !routeManager.HasActivePlan)
            return "<none>";

        int current = routeManager.CurrentStageIndex + 1;
        int total = routeManager.TotalStageCount;
        return $"{current}/{total}";
    }

    private SceneEventEntry[] GetSceneHistorySnapshot(int maxCount)
    {
        int safeMaxCount = Mathf.Max(1, maxCount);
        int resultCount = Mathf.Min(safeMaxCount, sceneHistory.Count);
        var results = new SceneEventEntry[resultCount];
        for (int i = 0; i < resultCount; i++)
        {
            int sourceIndex = sceneHistory.Count - 1 - i;
            results[i] = sceneHistory[sourceIndex];
        }

        return results;
    }

    private void RecordSceneEvent(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        sceneHistory.Add(new SceneEventEntry(Time.realtimeSinceStartup, message));
        if (sceneHistory.Count > MaxSceneHistoryEntries)
            sceneHistory.RemoveRange(0, sceneHistory.Count - MaxSceneHistoryEntries);
    }

    private static string[] BuildLoadedSceneSnapshot()
    {
        int loadedSceneCount = SceneManager.sceneCount;
        if (loadedSceneCount <= 0)
            return System.Array.Empty<string>();

        Scene activeScene = SceneManager.GetActiveScene();
        var results = new List<string>(loadedSceneCount);
        for (int i = 0; i < loadedSceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.IsValid() || !scene.isLoaded)
                continue;

            bool isActive = activeScene.IsValid() && scene.handle == activeScene.handle;
            results.Add($"{(isActive ? "* " : "- ")}{scene.name}");
        }

        return results.Count > 0 ? results.ToArray() : System.Array.Empty<string>();
    }

    private static string SafeSceneName(Scene scene)
    {
        return scene.IsValid() && !string.IsNullOrEmpty(scene.name) ? scene.name : "<none>";
    }
}
