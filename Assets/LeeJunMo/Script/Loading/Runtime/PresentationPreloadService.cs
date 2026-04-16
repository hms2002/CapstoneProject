using CapstoneRuntime;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DefaultExecutionOrder(-865)]
[DisallowMultipleComponent]
public sealed class PresentationPreloadService : MonoBehaviour
{
    public static PresentationPreloadService Instance { get; private set; }

    private static bool s_isQuitting;

    [SerializeField] private bool verboseLogging;

    private PortalRouteManager boundRouteManager;
    private LoadManifestSO activeBootManifest;
    private LoadManifestSO activeRunCommonManifest;
    private RouteSetLoadManifestSO activeCurrentStageManifest;
    private RouteSetLoadManifestSO activeNextStageManifest;

    public LoadManifestSO ActiveBootManifest => activeBootManifest;
    public LoadManifestSO ActiveRunCommonManifest => activeRunCommonManifest;
    public RouteSetLoadManifestSO ActiveCurrentStageManifest => activeCurrentStageManifest;
    public RouteSetLoadManifestSO ActiveNextStageManifest => activeNextStageManifest;
    public PortalRouteManager BoundRouteManager => boundRouteManager;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoBootstrap()
    {
        if (s_isQuitting || Instance != null)
            return;

        EnsureInstance();
    }

    public static PresentationPreloadService EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        PresentationPreloadService existing = RuntimeServiceOwnership.FindExistingService<PresentationPreloadService>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject host = RuntimeServiceOwnership.CreateServiceHost(nameof(PresentationPreloadService));
        return host.AddComponent<PresentationPreloadService>();
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
        BindRouteManager(PortalRouteManager.Instance);
        RefreshBootManifest();
        RefreshLoadWindow();
    }

    private void OnEnable()
    {
        PortalRouteManager.InstanceChanged += HandleRouteManagerInstanceChanged;
    }

    private void OnDisable()
    {
        PortalRouteManager.InstanceChanged -= HandleRouteManagerInstanceChanged;
        BindRouteManager(null);
    }

    private void OnDestroy()
    {
        ReleaseAllActiveManifests();

        if (Instance == this)
            Instance = null;
    }

    private void OnApplicationQuit()
    {
        s_isQuitting = true;
    }

    private void HandleRouteManagerInstanceChanged(PortalRouteManager manager)
    {
        BindRouteManager(manager);
        RefreshLoadWindow();
    }

    private void HandleLoadWindowChanged(PortalRouteManager manager)
    {
        if (manager != boundRouteManager)
            return;

        RefreshLoadWindow();
    }

    private void BindRouteManager(PortalRouteManager manager)
    {
        if (boundRouteManager == manager)
            return;

        if (boundRouteManager != null)
            boundRouteManager.LoadWindowChanged -= HandleLoadWindowChanged;

        boundRouteManager = manager;

        if (boundRouteManager != null)
            boundRouteManager.LoadWindowChanged += HandleLoadWindowChanged;
    }

    private void RefreshLoadWindow()
    {
        LoadManifestSO desiredRunCommon = null;
        RouteSetLoadManifestSO desiredCurrentStage = null;
        RouteSetLoadManifestSO desiredNextStage = null;

        if (boundRouteManager != null)
            boundRouteManager.TryGetActiveLoadWindow(out desiredRunCommon, out desiredCurrentStage, out desiredNextStage);

        ApplyManifest(ref activeRunCommonManifest, desiredRunCommon);
        ApplyRouteSetManifest(ref activeCurrentStageManifest, desiredCurrentStage);
        ApplyRouteSetManifest(ref activeNextStageManifest, desiredNextStage);

        if (verboseLogging)
        {
            string bootName = activeBootManifest != null ? activeBootManifest.name : "<none>";
            string runCommonName = activeRunCommonManifest != null ? activeRunCommonManifest.name : "<none>";
            string currentName = activeCurrentStageManifest != null ? activeCurrentStageManifest.name : "<none>";
            string nextName = activeNextStageManifest != null ? activeNextStageManifest.name : "<none>";
            Debug.Log(
                $"[PresentationPreloadService] Active load window updated. boot={bootName}, runCommon={runCommonName}, current={currentName}, next={nextName}",
                this);
        }
    }

    private void ReleaseAllActiveManifests()
    {
        ApplyManifest(ref activeBootManifest, null);
        ApplyManifest(ref activeRunCommonManifest, null);
        ApplyRouteSetManifest(ref activeCurrentStageManifest, null);
        ApplyRouteSetManifest(ref activeNextStageManifest, null);
    }

    private void RefreshBootManifest()
    {
        LoadingBootstrapConfigSO config = LoadBootstrapConfig();
        LoadManifestSO desiredBootManifest = config != null ? config.BootManifest : null;
        ApplyManifest(ref activeBootManifest, desiredBootManifest);
    }

    private static LoadingBootstrapConfigSO LoadBootstrapConfig()
    {
        LoadingBootstrapConfigSO config = FindPreloadedBootstrapConfig();
#if UNITY_EDITOR
        if (config == null)
            config = UnityEditor.AssetDatabase.LoadAssetAtPath<LoadingBootstrapConfigSO>(LoadingBootstrapConfigSO.SourceAssetPath);
#endif
        return config;
    }

    private static LoadingBootstrapConfigSO FindPreloadedBootstrapConfig()
    {
        LoadingBootstrapConfigSO[] loadedConfigs = Resources.FindObjectsOfTypeAll<LoadingBootstrapConfigSO>();
        if (loadedConfigs == null || loadedConfigs.Length == 0)
            return null;

#if UNITY_EDITOR
        for (int i = 0; i < loadedConfigs.Length; i++)
        {
            LoadingBootstrapConfigSO candidate = loadedConfigs[i];
            if (candidate == null)
                continue;

            string assetPath = AssetDatabase.GetAssetPath(candidate);
            if (string.Equals(assetPath, LoadingBootstrapConfigSO.SourceAssetPath, System.StringComparison.OrdinalIgnoreCase))
                return candidate;
        }
#endif

        return loadedConfigs[0];
    }

    private static void ApplyManifest(ref LoadManifestSO currentManifest, LoadManifestSO desiredManifest)
    {
        if (currentManifest == desiredManifest)
            return;

        if (currentManifest != null)
            PresentationAssetProvider.ReleaseManifest(currentManifest);

        currentManifest = desiredManifest;

        if (currentManifest != null)
            PresentationAssetProvider.PreloadManifest(currentManifest);
    }

    private static void ApplyRouteSetManifest(
        ref RouteSetLoadManifestSO currentManifest,
        RouteSetLoadManifestSO desiredManifest)
    {
        if (currentManifest == desiredManifest)
            return;

        if (currentManifest != null)
            PresentationAssetProvider.ReleaseRouteSetManifest(currentManifest);

        currentManifest = desiredManifest;

        if (currentManifest != null)
            PresentationAssetProvider.PreloadRouteSetManifest(currentManifest);
    }
}
