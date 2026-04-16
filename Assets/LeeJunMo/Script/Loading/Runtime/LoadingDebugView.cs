using System.Text;
using CapstonePresentation;
using CapstoneRuntime;
using UnityEngine;

[DefaultExecutionOrder(-830)]
[DisallowMultipleComponent]
public sealed class LoadingDebugView : MonoBehaviour
{
    public static LoadingDebugView Instance { get; private set; }

    private static bool s_isQuitting;
    private static bool s_visible;

    [SerializeField] private KeyCode toggleKey = KeyCode.F8;
    [SerializeField] private bool startVisible;
    [SerializeField, Min(4)] private int maxListedEntries = 12;

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
    }

    private void OnGUI()
    {
        if (!ShouldDraw())
            return;

        EnsureStyles();
        windowRect = GUI.Window(GetInstanceID(), windowRect, DrawWindow, "Loading Debug");
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
            DrawHeaderLine($"Route Catalog: {SafeName(routeManager != null ? routeManager.ActiveRouteCatalog : null)}");
            DrawHeaderLine($"Current Stage: {BuildStageLabel(routeManager)}");
            DrawHeaderLine($"Current RouteSet: {SafeName(routeManager != null ? routeManager.CurrentStageSet : null)}");
            DrawHeaderLine($"Next RouteSet: {SafeName(routeManager != null ? routeManager.NextStageSet : null)}");

            GUILayout.Space(6f);
            DrawSection("Active Preload Window");
            DrawBodyLine($"Boot: {SafeName(preload != null ? preload.ActiveBootManifest : null)}");
            DrawBodyLine($"RunCommon: {SafeName(preload != null ? preload.ActiveRunCommonManifest : null)}");
            DrawBodyLine($"Current: {SafeName(preload != null ? preload.ActiveCurrentStageManifest : null)}");
            DrawBodyLine($"Next: {SafeName(preload != null ? preload.ActiveNextStageManifest : null)}");

            GUILayout.Space(6f);
            DrawSection("Provider Counts");
            DrawBodyLine($"Loaded Manifests: {PresentationAssetProvider.GetLoadedManifestCount()}");
            DrawBodyLine($"Loaded Route Manifests: {PresentationAssetProvider.GetLoadedRouteManifestCount()}");
            DrawBodyLine($"Retained Assets: {PresentationAssetProvider.GetRetainedAssetCount()}");
            DrawBodyLine($"Prewarmed Prefabs: {PresentationAssetProvider.GetPrewarmedPrefabCount()}");
            DrawBodyLine($"Pool Types: {PresentationSpawnService.GetPooledPrefabTypeCount()}");
            DrawBodyLine($"Pooled Instances: {PresentationSpawnService.GetTotalPooledInstanceCount()}");

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

        GUI.skin.window = windowStyle;
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
}
