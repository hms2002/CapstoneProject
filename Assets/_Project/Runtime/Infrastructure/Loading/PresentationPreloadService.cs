using System.Collections.Generic;
using CapstoneRuntime;
using UnityEngine;

/// <summary>
/// 책임 : 씬/런 진행 상황에 맞춰 프레젠테이션 preload manifest window와 provider 작업 상태를 관리하는 Infrastructure 서비스이다.
/// </summary>
[DefaultExecutionOrder(-865)]
[DisallowMultipleComponent]
public sealed class PresentationPreloadService : MonoBehaviour, IPresentationPreloadBackend
{
    private sealed class TrackedProviderOperation
    {
        public TrackedProviderOperation(
            int batchId,
            string slotName,
            string actionName,
            string targetName,
            AssetProviderOperation operation)
        {
            BatchId = batchId;
            SlotName = slotName;
            ActionName = actionName;
            TargetName = targetName;
            Operation = operation;
        }

        public int BatchId { get; }
        public string SlotName { get; }
        public string ActionName { get; }
        public string TargetName { get; }
        public AssetProviderOperation Operation { get; }
    }

    // 책임: Presentation preload window 갱신 사유와 관련 manifest 이름을 디버그 기록으로 보관한다.
    public readonly struct DebugWindowEvent
    {
        public DebugWindowEvent(
            float realtimeSeconds,
            string reason,
            string bootManifestName,
            string firstRunIntroManifestName,
            string runCommonManifestName,
            string currentManifestName,
            string nextManifestName)
        {
            RealtimeSeconds = realtimeSeconds;
            Reason = reason;
            BootManifestName = bootManifestName;
            FirstRunIntroManifestName = firstRunIntroManifestName;
            RunCommonManifestName = runCommonManifestName;
            CurrentManifestName = currentManifestName;
            NextManifestName = nextManifestName;
        }

        public float RealtimeSeconds { get; }
        public string Reason { get; }
        public string BootManifestName { get; }
        public string FirstRunIntroManifestName { get; }
        public string RunCommonManifestName { get; }
        public string CurrentManifestName { get; }
        public string NextManifestName { get; }
    }

    // 책임: provider operation 디버그 스냅샷에 필요한 slot/action/asset/count 상태를 보관한다.
    public readonly struct DebugProviderOperationEntry
    {
        public DebugProviderOperationEntry(
            float startedRealtimeSeconds,
            string slotName,
            string actionName,
            string targetName,
            bool isDone,
            bool succeeded,
            float progress01,
            float elapsedSeconds,
            string errorMessage)
        {
            StartedRealtimeSeconds = startedRealtimeSeconds;
            SlotName = slotName;
            ActionName = actionName;
            TargetName = targetName;
            IsDone = isDone;
            Succeeded = succeeded;
            Progress01 = progress01;
            ElapsedSeconds = elapsedSeconds;
            ErrorMessage = errorMessage;
        }

        public float StartedRealtimeSeconds { get; }
        public string SlotName { get; }
        public string ActionName { get; }
        public string TargetName { get; }
        public bool IsDone { get; }
        public bool Succeeded { get; }
        public float Progress01 { get; }
        public float ElapsedSeconds { get; }
        public string ErrorMessage { get; }
    }

    public static PresentationPreloadService Instance { get; private set; }

    private static bool s_isQuitting;
    private const int MaxWindowHistoryEntries = 32;
    private const int MaxProviderOperationEntries = 48;

    [SerializeField] private bool verboseLogging;

    private readonly List<DebugWindowEvent> windowHistory = new();
    private readonly List<TrackedProviderOperation> providerOperationHistory = new();
    private PortalRouteManager boundRouteManager;
    private GameDataManager boundGameDataManager;
    private LoadManifestSO activeBootManifest;
    private LoadManifestSO activeFirstRunIntroManifest;
    private LoadManifestSO activeRunCommonManifest;
    private RouteSetLoadManifestSO activeCurrentStageManifest;
    private RouteSetLoadManifestSO activeNextStageManifest;
    private int nextProviderBatchId = 1;
    private int currentProviderBatchId;
    private bool currentProviderBatchHasOperations;

    public LoadManifestSO ActiveBootManifest => activeBootManifest;
    public LoadManifestSO ActiveFirstRunIntroManifest => activeFirstRunIntroManifest;
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

    public static DebugWindowEvent[] GetWindowHistorySnapshot(int maxCount = 16)
    {
        PresentationPreloadService service = EnsureInstance();
        return service != null ? service.BuildWindowHistorySnapshot(maxCount) : System.Array.Empty<DebugWindowEvent>();
    }

    public static DebugProviderOperationEntry[] GetProviderOperationSnapshot(int maxCount = 16)
    {
        PresentationPreloadService service = EnsureInstance();
        return service != null
            ? service.BuildProviderOperationSnapshot(maxCount)
            : System.Array.Empty<DebugProviderOperationEntry>();
    }

    public static int GetPendingProviderOperationCount()
    {
        PresentationPreloadService service = EnsureInstance();
        return service != null ? service.CountPendingProviderOperations() : 0;
    }

    public static int GetCurrentBatchPendingProviderOperationCount()
    {
        PresentationPreloadService service = EnsureInstance();
        return service != null ? service.CountPendingProviderOperationsInBatch(service.currentProviderBatchId) : 0;
    }

    public static float GetCurrentProviderProgress01()
    {
        PresentationPreloadService service = EnsureInstance();
        return service != null ? service.ComputeCurrentProviderProgress01() : 1f;
    }

    public static int GetCurrentProviderBatchId()
    {
        PresentationPreloadService service = EnsureInstance();
        return service != null ? service.currentProviderBatchId : 0;
    }

    public static void RefreshFirstRunIntroWindow(string reason = null)
    {
        PresentationPreloadService service = EnsureInstance();
        service?.RefreshFirstRunIntroManifest(
            string.IsNullOrWhiteSpace(reason) ? "Explicit first-run intro refresh" : reason);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        PresentationPreloadPlayback.RegisterBackend(this);
        RuntimeServiceOwnership.Adopt(this);
        BindRouteManager(PortalRouteManager.Instance);
        BindGameDataManager(GameDataManager.Instance);
        RefreshBootManifest("Initial boot manifest");
        RefreshFirstRunIntroManifest("Initial first-run intro manifest");
        RefreshLoadWindow("Initial load window");
    }

    private void OnEnable()
    {
        PortalRouteManager.InstanceChanged += HandleRouteManagerInstanceChanged;
        BindGameDataManager(GameDataManager.Instance);
    }

    private void OnDisable()
    {
        PortalRouteManager.InstanceChanged -= HandleRouteManagerInstanceChanged;
        BindRouteManager(null);
        BindGameDataManager(null);
    }

    private void OnDestroy()
    {
        PresentationPreloadPlayback.UnregisterBackend(this);
        ReleaseAllActiveManifests(PresentationAssetProvider.GetCurrentProviderWithoutCreating());

        if (Instance == this)
            Instance = null;
    }

    private void OnApplicationQuit()
    {
        s_isQuitting = true;
    }

    void IPresentationPreloadBackend.RefreshFirstRunIntroWindow(string reason)
    {
        RefreshFirstRunIntroManifest(
            string.IsNullOrWhiteSpace(reason) ? "Explicit first-run intro refresh" : reason);
    }

    private void HandleRouteManagerInstanceChanged(PortalRouteManager manager)
    {
        BindRouteManager(manager);
        RefreshLoadWindow(manager != null ? "Route manager rebound" : "Route manager cleared");
    }

    private void HandleLoadWindowChanged(PortalRouteManager manager)
    {
        if (manager != boundRouteManager)
            return;

        RefreshLoadWindow("Load window changed");
    }

    private void HandleGameDataLoaded(GameData data, int slotIndex)
    {
        RefreshFirstRunIntroManifest($"Profile slot {slotIndex + 1} loaded");
    }

    public void RefreshActiveLoadWindow(string reason = null)
    {
        RefreshLoadWindow(string.IsNullOrWhiteSpace(reason) ? "Explicit load window refresh" : reason);
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

    private void BindGameDataManager(GameDataManager manager)
    {
        if (boundGameDataManager == manager)
            return;

        if (boundGameDataManager != null)
            boundGameDataManager.OnDataLoaded -= HandleGameDataLoaded;

        boundGameDataManager = manager;

        if (boundGameDataManager != null)
            boundGameDataManager.OnDataLoaded += HandleGameDataLoaded;
    }

    private void RefreshLoadWindow(string reason = null)
    {
        int batchId = BeginProviderBatch();
        LoadManifestSO desiredRunCommon = null;
        RouteSetLoadManifestSO desiredCurrentStage = null;
        RouteSetLoadManifestSO desiredNextStage = null;

        if (boundRouteManager != null)
            boundRouteManager.TryGetActiveLoadWindow(out desiredRunCommon, out desiredCurrentStage, out desiredNextStage);

        bool changed = false;
        changed |= ApplyManifest("RunCommon", ref activeRunCommonManifest, desiredRunCommon, batchId);
        changed |= ApplyRouteSetManifest("Current", ref activeCurrentStageManifest, desiredCurrentStage, batchId);
        changed |= ApplyRouteSetManifest("Next", ref activeNextStageManifest, desiredNextStage, batchId);

        if (changed || !string.IsNullOrEmpty(reason))
            RecordWindowEvent(string.IsNullOrEmpty(reason) ? "Load window refresh" : reason);

        if (verboseLogging)
        {
            string bootName = activeBootManifest != null ? activeBootManifest.name : "<none>";
            string firstRunIntroName = activeFirstRunIntroManifest != null ? activeFirstRunIntroManifest.name : "<none>";
            string runCommonName = activeRunCommonManifest != null ? activeRunCommonManifest.name : "<none>";
            string currentName = activeCurrentStageManifest != null ? activeCurrentStageManifest.name : "<none>";
            string nextName = activeNextStageManifest != null ? activeNextStageManifest.name : "<none>";
            Debug.Log(
                $"[PresentationPreloadService] Active load window updated. boot={bootName}, firstRunIntro={firstRunIntroName}, runCommon={runCommonName}, current={currentName}, next={nextName}",
                this);
        }
    }

    private void ReleaseAllActiveManifests(IAssetProvider assetProvider = null)
    {
        const bool allowProviderCreation = false;
        ApplyManifest(
            "Boot",
            ref activeBootManifest,
            null,
            assetProvider: assetProvider,
            allowProviderCreation: allowProviderCreation);
        ApplyManifest(
            "FirstRunIntro",
            ref activeFirstRunIntroManifest,
            null,
            assetProvider: assetProvider,
            allowProviderCreation: allowProviderCreation);
        ApplyManifest(
            "RunCommon",
            ref activeRunCommonManifest,
            null,
            assetProvider: assetProvider,
            allowProviderCreation: allowProviderCreation);
        ApplyRouteSetManifest(
            "Current",
            ref activeCurrentStageManifest,
            null,
            assetProvider: assetProvider,
            allowProviderCreation: allowProviderCreation);
        ApplyRouteSetManifest(
            "Next",
            ref activeNextStageManifest,
            null,
            assetProvider: assetProvider,
            allowProviderCreation: allowProviderCreation);
    }

    private void RefreshBootManifest(string reason = null)
    {
        int batchId = BeginProviderBatch();
        LoadingBootstrapConfigSO config = LoadBootstrapConfig();
        LoadManifestSO desiredBootManifest = config != null ? config.BootManifest : null;
        bool changed = ApplyManifest("Boot", ref activeBootManifest, desiredBootManifest, batchId);
        if (changed || !string.IsNullOrEmpty(reason))
            RecordWindowEvent(string.IsNullOrEmpty(reason) ? "Boot manifest refresh" : reason);
    }

    private void RefreshFirstRunIntroManifest(string reason = null)
    {
        int batchId = BeginProviderBatch();
        LoadManifestSO desiredFirstRunIntroManifest = ResolveFirstRunIntroManifest();
        bool changed = ApplyManifest("FirstRunIntro", ref activeFirstRunIntroManifest, desiredFirstRunIntroManifest, batchId);
        if (changed || !string.IsNullOrEmpty(reason))
            RecordWindowEvent(string.IsNullOrEmpty(reason) ? "First-run intro manifest refresh" : reason);
    }

    private static LoadManifestSO ResolveFirstRunIntroManifest()
    {
        LoadingBootstrapConfigSO config = LoadBootstrapConfig();
        if (config == null || config.FirstRunIntroManifest == null)
            return null;

        return ShouldKeepFirstRunIntroManifestLoaded(config)
            ? config.FirstRunIntroManifest
            : null;
    }

    private static bool ShouldKeepFirstRunIntroManifestLoaded(LoadingBootstrapConfigSO config)
    {
        if (config == null)
            return false;

        GameDataManager manager = GameDataManager.Instance;
        GameData data = manager != null ? manager.Data : null;
        if (data == null)
            return false;

        TutorialSaveData tutorialData = data.tutorialData;
        if (tutorialData == null)
            return true;

        tutorialData.Normalize();
        return !tutorialData.IsCompleted(config.FirstRunIntroCompletionTutorialId);
    }

    private bool ApplyManifest(
        string slotName,
        ref LoadManifestSO currentManifest,
        LoadManifestSO desiredManifest,
        int batchId = 0,
        IAssetProvider assetProvider = null,
        bool allowProviderCreation = true)
    {
        if (currentManifest == desiredManifest)
            return false;

        if (assetProvider == null && allowProviderCreation)
            assetProvider = PresentationAssetProvider.CurrentProvider;

        if (currentManifest != null)
        {
            AssetProviderOperation releaseOperation = assetProvider != null
                ? assetProvider.ReleaseManifestAsync(currentManifest)
                : AssetProviderOperation.Completed(BuildProviderOperationLabel(slotName, "ReleaseManifest", currentManifest));
            RecordProviderOperation(batchId, slotName, "ReleaseManifest", currentManifest, releaseOperation);
        }

        currentManifest = desiredManifest;

        if (currentManifest != null)
        {
            AssetProviderOperation preloadOperation = assetProvider != null
                ? assetProvider.PreloadManifestAsync(currentManifest)
                : AssetProviderOperation.Completed(BuildProviderOperationLabel(slotName, "PreloadManifest", currentManifest));
            RecordProviderOperation(batchId, slotName, "PreloadManifest", currentManifest, preloadOperation);
        }

        return true;
    }

    private bool ApplyRouteSetManifest(
        string slotName,
        ref RouteSetLoadManifestSO currentManifest,
        RouteSetLoadManifestSO desiredManifest,
        int batchId = 0,
        IAssetProvider assetProvider = null,
        bool allowProviderCreation = true)
    {
        if (currentManifest == desiredManifest)
            return false;

        if (assetProvider == null && allowProviderCreation)
            assetProvider = PresentationAssetProvider.CurrentProvider;

        if (currentManifest != null)
        {
            AssetProviderOperation releaseOperation = assetProvider != null
                ? assetProvider.ReleaseRouteSetManifestAsync(currentManifest)
                : AssetProviderOperation.Completed(BuildProviderOperationLabel(slotName, "ReleaseRouteSetManifest", currentManifest));
            RecordProviderOperation(batchId, slotName, "ReleaseRouteSetManifest", currentManifest, releaseOperation);
        }

        currentManifest = desiredManifest;

        if (currentManifest != null)
        {
            AssetProviderOperation preloadOperation = assetProvider != null
                ? assetProvider.PreloadRouteSetManifestAsync(currentManifest)
                : AssetProviderOperation.Completed(BuildProviderOperationLabel(slotName, "PreloadRouteSetManifest", currentManifest));
            RecordProviderOperation(batchId, slotName, "PreloadRouteSetManifest", currentManifest, preloadOperation);
        }

        return true;
    }

    private DebugWindowEvent[] BuildWindowHistorySnapshot(int maxCount)
    {
        int safeMaxCount = Mathf.Max(1, maxCount);
        int resultCount = Mathf.Min(safeMaxCount, windowHistory.Count);
        var results = new DebugWindowEvent[resultCount];
        for (int i = 0; i < resultCount; i++)
        {
            int sourceIndex = windowHistory.Count - 1 - i;
            results[i] = windowHistory[sourceIndex];
        }

        return results;
    }

    private DebugProviderOperationEntry[] BuildProviderOperationSnapshot(int maxCount)
    {
        int safeMaxCount = Mathf.Max(1, maxCount);
        int resultCount = Mathf.Min(safeMaxCount, providerOperationHistory.Count);
        var results = new DebugProviderOperationEntry[resultCount];
        for (int i = 0; i < resultCount; i++)
        {
            TrackedProviderOperation trackedOperation = providerOperationHistory[providerOperationHistory.Count - 1 - i];
            AssetProviderOperation operation = trackedOperation.Operation;
            results[i] = new DebugProviderOperationEntry(
                operation != null ? operation.StartedRealtimeSeconds : 0f,
                trackedOperation.SlotName,
                trackedOperation.ActionName,
                trackedOperation.TargetName,
                operation == null || operation.IsDone,
                operation == null || operation.Succeeded,
                operation != null ? operation.Progress01 : 1f,
                operation != null ? operation.ElapsedSeconds : 0f,
                operation != null ? operation.ErrorMessage : null);
        }

        return results;
    }

    private int CountPendingProviderOperations()
    {
        int pendingCount = 0;
        for (int i = 0; i < providerOperationHistory.Count; i++)
        {
            AssetProviderOperation operation = providerOperationHistory[i].Operation;
            if (operation != null && !operation.IsDone)
                pendingCount++;
        }

        return pendingCount;
    }

    private int CountPendingProviderOperationsInBatch(int batchId)
    {
        if (batchId <= 0 || !currentProviderBatchHasOperations)
            return 0;

        int pendingCount = 0;
        for (int i = 0; i < providerOperationHistory.Count; i++)
        {
            TrackedProviderOperation trackedOperation = providerOperationHistory[i];
            if (trackedOperation.BatchId != batchId)
                continue;

            AssetProviderOperation operation = trackedOperation.Operation;
            if (operation != null && !operation.IsDone)
                pendingCount++;
        }

        return pendingCount;
    }

    private void RecordWindowEvent(string reason)
    {
        windowHistory.Add(new DebugWindowEvent(
            Time.realtimeSinceStartup,
            string.IsNullOrEmpty(reason) ? "Load window refresh" : reason,
            SafeName(activeBootManifest),
            SafeName(activeFirstRunIntroManifest),
            SafeName(activeRunCommonManifest),
            SafeName(activeCurrentStageManifest),
            SafeName(activeNextStageManifest)));

        if (windowHistory.Count > MaxWindowHistoryEntries)
            windowHistory.RemoveRange(0, windowHistory.Count - MaxWindowHistoryEntries);
    }

    private void RecordProviderOperation(
        int batchId,
        string slotName,
        string actionName,
        Object target,
        AssetProviderOperation operation)
    {
        providerOperationHistory.Add(new TrackedProviderOperation(
            batchId,
            slotName,
            actionName,
            SafeName(target),
            operation ?? AssetProviderOperation.Completed(BuildProviderOperationLabel(slotName, actionName, target))));

        if (batchId > 0 && batchId == currentProviderBatchId)
            currentProviderBatchHasOperations = true;

        if (providerOperationHistory.Count > MaxProviderOperationEntries)
            providerOperationHistory.RemoveRange(0, providerOperationHistory.Count - MaxProviderOperationEntries);
    }

    private static string SafeName(Object target)
    {
        return target != null ? target.name : "<none>";
    }

    private static string BuildProviderOperationLabel(string slotName, string actionName, Object target)
    {
        return $"{slotName} {actionName} {SafeName(target)}";
    }

    private int BeginProviderBatch()
    {
        currentProviderBatchId = nextProviderBatchId++;
        currentProviderBatchHasOperations = false;
        return currentProviderBatchId;
    }

    private float ComputeCurrentProviderProgress01()
    {
        if (currentProviderBatchId <= 0 || !currentProviderBatchHasOperations)
            return 1f;

        float totalUnits = 0f;
        float totalProgress = 0f;
        for (int i = 0; i < providerOperationHistory.Count; i++)
        {
            TrackedProviderOperation trackedOperation = providerOperationHistory[i];
            if (trackedOperation.BatchId != currentProviderBatchId)
                continue;

            AssetProviderOperation operation = trackedOperation.Operation;
            float units = operation != null ? operation.ProgressUnits : 1f;
            float progress = operation != null ? operation.Progress01 : 1f;
            totalUnits += units;
            totalProgress += units * progress;
        }

        return totalUnits > 0f ? Mathf.Clamp01(totalProgress / totalUnits) : 1f;
    }

    private static LoadingBootstrapConfigSO LoadBootstrapConfig()
    {
        LoadingBootstrapConfigSO[] loadedConfigs = Resources.FindObjectsOfTypeAll<LoadingBootstrapConfigSO>();
        if (loadedConfigs != null)
        {
#if UNITY_EDITOR
            for (int i = 0; i < loadedConfigs.Length; i++)
            {
                LoadingBootstrapConfigSO candidate = loadedConfigs[i];
                if (candidate == null)
                    continue;

                string assetPath = EditorAuthoringPlayback.GetAssetPath(candidate);
                if (string.Equals(assetPath, LoadingBootstrapConfigSO.SourceAssetPath, System.StringComparison.OrdinalIgnoreCase))
                    return candidate;
            }
#endif

            if (loadedConfigs.Length > 0)
                return loadedConfigs[0];
        }

#if UNITY_EDITOR
        return EditorAuthoringPlayback.LoadAssetAtPath<LoadingBootstrapConfigSO>(LoadingBootstrapConfigSO.SourceAssetPath);
#else
        return null;
#endif
    }
}
