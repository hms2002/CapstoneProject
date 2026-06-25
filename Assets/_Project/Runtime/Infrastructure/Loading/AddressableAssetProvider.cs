using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CapstonePresentation;
using CapstoneRuntime;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Loading manifest assets을 Addressables로 유지하되, 누락된 주소는 직접 참조로 안전하게 대체하는 provider입니다.
/// </summary>
[DefaultExecutionOrder(-871)]
[DisallowMultipleComponent]
// 이 클래스의 책임:
// Loading manifest가 요구한 presentation 에셋을 Addressables로 보유/해제하고, 런타임 진단용 로드 상태를 제공한다.
public sealed class AddressableAssetProvider : MonoBehaviour, IAssetProvider, IAssetProviderDebugInfo
{
    // 이 클래스의 책임:
    // 직접 참조 에셋 하나가 Addressables 로드 큐/활성 핸들/직접 참조 fallback 중 어디에 있는지 추적한다.
    private sealed class LoadState
    {
        public int SourceId;
        public UnityEngine.Object SourceAsset;
        public string AddressKey;
        public UnityEngine.Object LoadedAsset;
        public AsyncOperationHandle<UnityEngine.Object> Handle;
        public bool HasHandle;
        public AssetProviderOperation ActiveLoadOperation;
        public bool ReleaseWhenLoaded;
        public bool IsQueued;
    }

    public static AddressableAssetProvider Instance { get; private set; }

    private static bool s_isQuitting;
    private const int MaxDebugHistoryEntries = 96;

    private readonly Dictionary<int, int> manifestRefCounts = new();
    private readonly Dictionary<int, int> routeManifestRefCounts = new();
    private readonly Dictionary<int, int> assetRefCounts = new();
    private readonly Dictionary<int, int> prewarmRefCounts = new();
    private readonly Dictionary<int, LoadManifestSO> trackedManifests = new();
    private readonly Dictionary<int, RouteSetLoadManifestSO> trackedRouteManifests = new();
    private readonly Dictionary<int, UnityEngine.Object> trackedAssets = new();
    private readonly Dictionary<int, LoadState> loadStates = new();
    private readonly List<PresentationAssetProvider.DebugEventEntry> debugHistory = new();
    private readonly Queue<LoadState> pendingLoadQueue = new();

    [SerializeField] private LoadingAddressableRegistrySO registry;
    [Header("Load Smoothing")]
    [SerializeField, Min(1)] private int maxConcurrentAddressableLoads = 4;
    [SerializeField, Min(1)] private int maxAddressableLoadStartsPerFrame = 2;
    [Header("Diagnostics")]
    [SerializeField] private bool logFallbackWarnings;
    [SerializeField] private bool logRetainedAssetDumpPath = true;

    private Coroutine loadQueueRoutine;
    private int activeAddressableLoadCount;

    public int LoadedManifestCount => manifestRefCounts.Count;
    public int LoadedRouteManifestCount => routeManifestRefCounts.Count;
    public int RetainedAssetCount => assetRefCounts.Count;
    public int PrewarmedPrefabCount => prewarmRefCounts.Count;
    public int PendingQueuedLoadCount => pendingLoadQueue.Count;
    public int ActiveAddressableLoadCount => activeAddressableLoadCount;
    public int TrackedLoadStateCount => loadStates.Count;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoBootstrap()
    {
        if (s_isQuitting)
            return;

        LoadingBootstrapConfigSO config = LoadBootstrapConfig();
        if (config == null ||
            config.AssetProviderMode != LoadingAssetProviderMode.Addressables ||
            config.AddressableRegistry == null)
        {
            return;
        }

        AddressableAssetProvider provider = EnsureInstance(config.AddressableRegistry);
        PresentationAssetProvider.SetProviderOverride(provider);
        provider.RecordDebugEvent("Installed AddressableAssetProvider override.");
    }

    public static AddressableAssetProvider EnsureInstance(LoadingAddressableRegistrySO configuredRegistry = null)
    {
        if (Instance != null)
        {
            Instance.Configure(configuredRegistry);
            return Instance;
        }

        AddressableAssetProvider existing = RuntimeServiceOwnership.FindExistingService<AddressableAssetProvider>();
        if (existing != null)
        {
            Instance = existing;
            existing.Configure(configuredRegistry);
            return existing;
        }

        GameObject host = RuntimeServiceOwnership.CreateServiceHost(nameof(AddressableAssetProvider));
        AddressableAssetProvider created = host.AddComponent<AddressableAssetProvider>();
        created.Configure(configuredRegistry);
        return created;
    }

    public void Configure(LoadingAddressableRegistrySO configuredRegistry)
    {
        if (configuredRegistry != null)
            registry = configuredRegistry;
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

        LoadingBootstrapConfigSO config = LoadBootstrapConfig();
        if (registry == null && config != null)
            registry = config.AddressableRegistry;
    }

    private void OnDestroy()
    {
        PresentationAssetProvider.ClearProviderOverride(this);

        if (Instance == this)
            Instance = null;
    }

    private void OnApplicationQuit()
    {
        s_isQuitting = true;
    }

    void IAssetProvider.PreloadManifest(LoadManifestSO manifest)
    {
        ((IAssetProvider)this).PreloadManifestAsync(manifest);
    }

    void IAssetProvider.ReleaseManifest(LoadManifestSO manifest)
    {
        ((IAssetProvider)this).ReleaseManifestAsync(manifest);
    }

    void IAssetProvider.PreloadRouteSetManifest(RouteSetLoadManifestSO manifest)
    {
        ((IAssetProvider)this).PreloadRouteSetManifestAsync(manifest);
    }

    void IAssetProvider.ReleaseRouteSetManifest(RouteSetLoadManifestSO manifest)
    {
        ((IAssetProvider)this).ReleaseRouteSetManifestAsync(manifest);
    }

    AssetProviderOperation IAssetProvider.PreloadManifestAsync(LoadManifestSO manifest)
    {
        if (manifest == null)
            return AssetProviderOperation.Completed("Addressables PreloadManifest <null>");

        int manifestId = manifest.GetInstanceID();
        manifestRefCounts.TryGetValue(manifestId, out int currentCount);
        int nextCount = currentCount + 1;
        manifestRefCounts[manifestId] = nextCount;
        trackedManifests[manifestId] = manifest;
        RecordDebugEvent($"Manifest + {manifest.name} ({currentCount}->{nextCount})");
        if (currentCount > 0)
            return AssetProviderOperation.Completed(BuildOperationLabel("PreloadManifest", manifest));

        var operations = new List<AssetProviderOperation>();
        foreach (UnityEngine.Object asset in manifest.EnumerateReferencedAssets())
            operations.Add(RetainAssetForManifest(asset));

        return StartCombinedOperation(
            BuildOperationLabel("PreloadManifest", manifest),
            operations,
            () =>
            {
                if (!IsManifestRetained(manifest))
                    return null;

                var prewarmOperations = new List<AssetProviderOperation>();
                foreach (PrewarmPrefabEntry prewarmEntry in manifest.EnumeratePrewarmEntries())
                    prewarmOperations.Add(RetainPrewarm(prewarmEntry));

                return prewarmOperations;
            });
    }

    AssetProviderOperation IAssetProvider.ReleaseManifestAsync(LoadManifestSO manifest)
    {
        if (manifest == null)
            return AssetProviderOperation.Completed("Addressables ReleaseManifest <null>");

        int manifestId = manifest.GetInstanceID();
        if (!manifestRefCounts.TryGetValue(manifestId, out int currentCount))
            return AssetProviderOperation.Completed(BuildOperationLabel("ReleaseManifest", manifest));

        int nextCount = Mathf.Max(0, currentCount - 1);
        RecordDebugEvent($"Manifest - {manifest.name} ({currentCount}->{nextCount})");
        if (currentCount > 1)
        {
            manifestRefCounts[manifestId] = nextCount;
            return AssetProviderOperation.Completed(BuildOperationLabel("ReleaseManifest", manifest));
        }

        manifestRefCounts.Remove(manifestId);
        trackedManifests.Remove(manifestId);

        foreach (PrewarmPrefabEntry prewarmEntry in manifest.EnumeratePrewarmEntries())
            ReleasePrewarm(prewarmEntry);

        foreach (UnityEngine.Object asset in manifest.EnumerateReferencedAssets())
            ReleaseAssetReference(asset);

        return AssetProviderOperation.Completed(BuildOperationLabel("ReleaseManifest", manifest));
    }

    AssetProviderOperation IAssetProvider.PreloadRouteSetManifestAsync(RouteSetLoadManifestSO manifest)
    {
        if (manifest == null)
            return AssetProviderOperation.Completed("Addressables PreloadRouteSetManifest <null>");

        int manifestId = manifest.GetInstanceID();
        routeManifestRefCounts.TryGetValue(manifestId, out int currentCount);
        int nextCount = currentCount + 1;
        routeManifestRefCounts[manifestId] = nextCount;
        trackedRouteManifests[manifestId] = manifest;
        RecordDebugEvent($"Route manifest + {manifest.name} ({currentCount}->{nextCount})");
        if (currentCount > 0)
            return AssetProviderOperation.Completed(BuildOperationLabel("PreloadRouteSetManifest", manifest));

        var operations = new List<AssetProviderOperation>();
        foreach (LoadManifestSO childManifest in manifest.EnumerateManifests())
            operations.Add(((IAssetProvider)this).PreloadManifestAsync(childManifest));

        return StartCombinedOperation(BuildOperationLabel("PreloadRouteSetManifest", manifest), operations);
    }

    AssetProviderOperation IAssetProvider.ReleaseRouteSetManifestAsync(RouteSetLoadManifestSO manifest)
    {
        if (manifest == null)
            return AssetProviderOperation.Completed("Addressables ReleaseRouteSetManifest <null>");

        int manifestId = manifest.GetInstanceID();
        if (!routeManifestRefCounts.TryGetValue(manifestId, out int currentCount))
            return AssetProviderOperation.Completed(BuildOperationLabel("ReleaseRouteSetManifest", manifest));

        int nextCount = Mathf.Max(0, currentCount - 1);
        RecordDebugEvent($"Route manifest - {manifest.name} ({currentCount}->{nextCount})");
        if (currentCount > 1)
        {
            routeManifestRefCounts[manifestId] = nextCount;
            return AssetProviderOperation.Completed(BuildOperationLabel("ReleaseRouteSetManifest", manifest));
        }

        routeManifestRefCounts.Remove(manifestId);
        trackedRouteManifests.Remove(manifestId);

        foreach (LoadManifestSO childManifest in manifest.EnumerateManifests())
            ((IAssetProvider)this).ReleaseManifestAsync(childManifest);

        return AssetProviderOperation.Completed(BuildOperationLabel("ReleaseRouteSetManifest", manifest));
    }

    GameObject IAssetProvider.ResolvePrefab(GameObject prefab)
    {
        return ResolveLoadedAsset(prefab);
    }

    PresentationCueSO IAssetProvider.ResolveCue(PresentationCueSO cue)
    {
        return ResolveLoadedAsset(cue);
    }

    T IAssetProvider.ResolveAsset<T>(T asset)
    {
        return ResolveLoadedAsset(asset);
    }

    AssetResolveOperation<GameObject> IAssetProvider.ResolvePrefabAsync(GameObject prefab)
    {
        return ResolveAssetAsyncInternal(prefab);
    }

    AssetResolveOperation<PresentationCueSO> IAssetProvider.ResolveCueAsync(PresentationCueSO cue)
    {
        return ResolveAssetAsyncInternal(cue);
    }

    AssetResolveOperation<T> IAssetProvider.ResolveAssetAsync<T>(T asset)
    {
        return ResolveAssetAsyncInternal(asset);
    }

    PresentationAssetProvider.DebugCountEntry[] IAssetProviderDebugInfo.GetManifestSnapshot()
    {
        return BuildManifestSnapshot();
    }

    PresentationAssetProvider.DebugCountEntry[] IAssetProviderDebugInfo.GetRouteManifestSnapshot()
    {
        return BuildRouteManifestSnapshot();
    }

    PresentationAssetProvider.DebugCountEntry[] IAssetProviderDebugInfo.GetAssetSnapshot(int maxCount)
    {
        return BuildAssetSnapshot(maxCount);
    }

    PresentationAssetProvider.DebugCountEntry[] IAssetProviderDebugInfo.GetPrewarmSnapshot(int maxCount)
    {
        return BuildPrewarmSnapshot(maxCount);
    }

    PresentationAssetProvider.DebugEventEntry[] IAssetProviderDebugInfo.GetDebugHistorySnapshot(int maxCount)
    {
        return BuildDebugHistorySnapshot(maxCount);
    }

    private AssetProviderOperation RetainAssetForManifest(UnityEngine.Object sourceAsset)
    {
        if (sourceAsset == null)
            return AssetProviderOperation.Completed("RetainAsset <null>");

        IncrementAssetRefCount(sourceAsset);
        return StartOrReuseLoadOperation(sourceAsset);
    }

    private void ReleaseAssetReference(UnityEngine.Object sourceAsset)
    {
        if (sourceAsset == null)
            return;

        int sourceId = sourceAsset.GetInstanceID();
        if (!assetRefCounts.TryGetValue(sourceId, out int currentCount))
            return;

        if (currentCount > 1)
        {
            assetRefCounts[sourceId] = currentCount - 1;
            return;
        }

        assetRefCounts.Remove(sourceId);
        trackedAssets.Remove(sourceId);

        if (!loadStates.TryGetValue(sourceId, out LoadState state))
            return;

        if (state.ActiveLoadOperation != null && !state.ActiveLoadOperation.IsDone)
        {
            state.ReleaseWhenLoaded = true;
            return;
        }

        ReleaseLoadedState(state);
    }

    private void IncrementAssetRefCount(UnityEngine.Object sourceAsset)
    {
        int sourceId = sourceAsset.GetInstanceID();
        assetRefCounts.TryGetValue(sourceId, out int currentCount);
        assetRefCounts[sourceId] = currentCount + 1;
        trackedAssets[sourceId] = sourceAsset;
    }

    private AssetProviderOperation StartOrReuseLoadOperation(UnityEngine.Object sourceAsset)
    {
        if (sourceAsset == null)
            return AssetProviderOperation.Completed("LoadAsset <null>");

        int sourceId = sourceAsset.GetInstanceID();
        trackedAssets[sourceId] = sourceAsset;

        if (loadStates.TryGetValue(sourceId, out LoadState existingState))
        {
            if (existingState.LoadedAsset != null)
                return AssetProviderOperation.Completed(BuildOperationLabel("ResolveLoadedAsset", sourceAsset));

            return existingState.ActiveLoadOperation ?? AssetProviderOperation.Completed(BuildOperationLabel("ResolveLoadedAsset", sourceAsset));
        }

        if (!TryGetAddressKey(sourceAsset, out string addressKey))
        {
            loadStates[sourceId] = new LoadState
            {
                SourceId = sourceId,
                SourceAsset = sourceAsset,
                LoadedAsset = sourceAsset
            };
            RecordFallbackEvent($"Address missing for {sourceAsset.name}; fallback used with direct reference.");
            return AssetProviderOperation.Completed(BuildOperationLabel("ResolveDirectAsset", sourceAsset));
        }

        var operation = new AssetProviderOperation(BuildOperationLabel("LoadAddressableAsset", sourceAsset));
        var state = new LoadState
        {
            SourceId = sourceId,
            SourceAsset = sourceAsset,
            AddressKey = addressKey,
            ActiveLoadOperation = operation,
            IsQueued = true
        };
        loadStates[sourceId] = state;
        EnqueueLoadState(state);

        return operation;
    }

    private void EnqueueLoadState(LoadState state)
    {
        if (state == null)
            return;

        pendingLoadQueue.Enqueue(state);
        if (loadQueueRoutine == null)
            loadQueueRoutine = StartCoroutine(ProcessLoadQueue());
    }

    private IEnumerator ProcessLoadQueue()
    {
        while (pendingLoadQueue.Count > 0)
        {
            int startedThisFrame = 0;
            while (pendingLoadQueue.Count > 0 &&
                   activeAddressableLoadCount < Mathf.Max(1, maxConcurrentAddressableLoads) &&
                   startedThisFrame < Mathf.Max(1, maxAddressableLoadStartsPerFrame))
            {
                LoadState state = pendingLoadQueue.Dequeue();
                if (!CanStartQueuedLoad(state))
                    continue;

                StartQueuedLoad(state);
                startedThisFrame++;
            }

            yield return null;
        }

        loadQueueRoutine = null;
    }

    private bool CanStartQueuedLoad(LoadState state)
    {
        if (state == null || !state.IsQueued)
            return false;

        if (!loadStates.TryGetValue(state.SourceId, out LoadState currentState) || currentState != state)
            return false;

        if (state.ReleaseWhenLoaded && !assetRefCounts.ContainsKey(state.SourceId))
        {
            CompleteQueuedLoadWithoutStarting(state);
            return false;
        }

        return true;
    }

    private void CompleteQueuedLoadWithoutStarting(LoadState state)
    {
        state.IsQueued = false;
        state.LoadedAsset = state.SourceAsset;
        state.ActiveLoadOperation?.Complete();
        state.ActiveLoadOperation = null;
        loadStates.Remove(state.SourceId);
    }

    private void StartQueuedLoad(LoadState state)
    {
        if (state == null)
            return;

        state.IsQueued = false;
        AssetProviderOperation operation = state.ActiveLoadOperation;

        if (!HasAddressableLocation(state.AddressKey))
        {
            state.LoadedAsset = state.SourceAsset;
            state.ActiveLoadOperation = null;
            trackedAssets[state.SourceId] = state.SourceAsset;
            RecordFallbackEvent($"Address not found for {state.SourceAsset.name} [{state.AddressKey}]; fallback used with direct reference.");
            operation?.Complete();
            return;
        }

        try
        {
            state.Handle = Addressables.LoadAssetAsync<UnityEngine.Object>(state.AddressKey);
            state.HasHandle = true;
        }
        catch (Exception ex)
        {
            state.LoadedAsset = state.SourceAsset;
            state.ActiveLoadOperation = null;
            trackedAssets[state.SourceId] = state.SourceAsset;
            RecordFallbackEvent($"Addressables start failed for {state.SourceAsset.name} [{state.AddressKey}]: {ex.Message}. Fallback used with direct reference.");
            operation?.Complete();
            return;
        }

        activeAddressableLoadCount++;
        state.ActiveLoadOperation = operation;
        operation.ReportProgress(state.Handle.PercentComplete);
        StartCoroutine(CompleteLoadOperation(state, operation));
    }

    private static bool HasAddressableLocation(string addressKey)
    {
        if (string.IsNullOrWhiteSpace(addressKey))
            return false;

        foreach (IResourceLocator locator in Addressables.ResourceLocators)
        {
            if (locator == null)
                continue;

            if (locator.Locate(addressKey, typeof(UnityEngine.Object), out IList<IResourceLocation> locations) &&
                locations != null &&
                locations.Count > 0)
                return true;
        }

        return false;
    }

    private IEnumerator CompleteLoadOperation(LoadState state, AssetProviderOperation operation)
    {
        while (state != null && state.HasHandle && !state.Handle.IsDone)
        {
            operation.ReportProgress(state.Handle.PercentComplete);
            yield return null;
        }

        if (state == null)
        {
            operation.Complete("Load state missing.");
            yield break;
        }

        string errorMessage = null;
        UnityEngine.Object loadedAsset = null;
        if (state.HasHandle)
        {
            operation.ReportProgress(state.Handle.PercentComplete);
            if (state.Handle.Status == AsyncOperationStatus.Failed)
                errorMessage = state.Handle.OperationException != null
                    ? state.Handle.OperationException.Message
                    : "Addressables operation failed.";
            else if (state.Handle.Status == AsyncOperationStatus.Succeeded)
                loadedAsset = state.Handle.Result;
        }

        if (loadedAsset == null)
            loadedAsset = state.SourceAsset;

        state.LoadedAsset = loadedAsset;
        state.ActiveLoadOperation = null;
        trackedAssets[state.SourceId] = loadedAsset != null ? loadedAsset : state.SourceAsset;

        if (!string.IsNullOrWhiteSpace(errorMessage))
            RecordFallbackEvent($"Addressables load failed for {state.SourceAsset.name}: {errorMessage}. Fallback used with direct reference.");
        else
            RecordDebugEvent($"Addressables load completed for {state.SourceAsset.name} [{state.AddressKey}]");

        operation.Complete();
        activeAddressableLoadCount = Mathf.Max(0, activeAddressableLoadCount - 1);

        if (state.ReleaseWhenLoaded && !assetRefCounts.ContainsKey(state.SourceId))
            ReleaseLoadedState(state);
    }

    private void ReleaseLoadedState(LoadState state)
    {
        if (state == null)
            return;

        if (state.HasHandle && state.Handle.IsValid())
            Addressables.Release(state.Handle);

        loadStates.Remove(state.SourceId);
    }

    private T ResolveLoadedAsset<T>(T sourceAsset) where T : UnityEngine.Object
    {
        if (sourceAsset == null)
            return null;

        int sourceId = sourceAsset.GetInstanceID();
        if (loadStates.TryGetValue(sourceId, out LoadState state) && state.LoadedAsset is T loadedAsset)
            return loadedAsset;

        return sourceAsset;
    }

    private AssetResolveOperation<T> ResolveAssetAsyncInternal<T>(T sourceAsset) where T : UnityEngine.Object
    {
        if (sourceAsset == null)
            return AssetResolveOperation<T>.Completed(null, "ResolveAsset <null>");

        int sourceId = sourceAsset.GetInstanceID();
        if (loadStates.TryGetValue(sourceId, out LoadState loadedState) && loadedState.LoadedAsset is T loadedAsset)
            return AssetResolveOperation<T>.Completed(loadedAsset, BuildOperationLabel("ResolveAsset", sourceAsset));

        if (!assetRefCounts.ContainsKey(sourceId))
            IncrementAssetRefCount(sourceAsset);

        AssetProviderOperation baseOperation = StartOrReuseLoadOperation(sourceAsset);
        if (loadStates.TryGetValue(sourceId, out LoadState stateAfterStart) && stateAfterStart.LoadedAsset is T resolvedAfterStart)
            return AssetResolveOperation<T>.Completed(resolvedAfterStart, BuildOperationLabel("ResolveAsset", sourceAsset));

        var resolveOperation = new AssetResolveOperation<T>(BuildOperationLabel("ResolveAsset", sourceAsset));
        if (baseOperation != null)
            resolveOperation.ReportProgress(baseOperation.Progress01);
        StartCoroutine(CompleteResolveOperation(sourceAsset, baseOperation, resolveOperation));
        return resolveOperation;
    }

    private IEnumerator CompleteResolveOperation<T>(
        T sourceAsset,
        AssetProviderOperation baseOperation,
        AssetResolveOperation<T> resolveOperation) where T : UnityEngine.Object
    {
        while (baseOperation != null && !baseOperation.IsDone)
        {
            resolveOperation.ReportProgress(baseOperation.Progress01);
            yield return null;
        }

        if (baseOperation != null)
            resolveOperation.ReportProgress(baseOperation.Progress01);

        T resolvedAsset = ResolveLoadedAsset(sourceAsset);
        resolveOperation.Complete(resolvedAsset, baseOperation != null ? baseOperation.ErrorMessage : null);
    }

    private AssetProviderOperation RetainPrewarm(PrewarmPrefabEntry entry)
    {
        if (!entry.IsValid)
            return AssetProviderOperation.Completed("Prewarm <invalid>");

        GameObject resolvedPrefab = ResolveLoadedAsset(entry.prefab);
        if (resolvedPrefab == null)
            return AssetProviderOperation.Completed("Prewarm <missing prefab>");

        int prefabId = resolvedPrefab.GetInstanceID();
        prewarmRefCounts.TryGetValue(prefabId, out int currentCount);
        int nextCount = currentCount + entry.EffectiveCount;
        prewarmRefCounts[prefabId] = nextCount;
        trackedAssets[prefabId] = resolvedPrefab;
        RecordDebugEvent($"Prewarm + {resolvedPrefab.name} ({currentCount}->{nextCount})");
        return PresentationSpawnService.PrewarmPrefabAsync(resolvedPrefab, entry.EffectiveCount);
    }

    private void ReleasePrewarm(PrewarmPrefabEntry entry)
    {
        if (!entry.IsValid)
            return;

        GameObject resolvedPrefab = ResolveLoadedAsset(entry.prefab);
        if (resolvedPrefab == null)
            return;

        int prefabId = resolvedPrefab.GetInstanceID();
        if (!prewarmRefCounts.TryGetValue(prefabId, out int currentCount))
            return;

        int releaseCount = Mathf.Min(entry.EffectiveCount, currentCount);
        int nextCount = Mathf.Max(0, currentCount - releaseCount);
        if (currentCount > releaseCount)
            prewarmRefCounts[prefabId] = nextCount;
        else
            prewarmRefCounts.Remove(prefabId);

        RecordDebugEvent($"Prewarm - {resolvedPrefab.name} ({currentCount}->{nextCount})");
        PresentationSpawnService.TrimPrewarmedPrefab(resolvedPrefab, releaseCount);
    }

    private bool IsManifestRetained(LoadManifestSO manifest)
    {
        return manifest != null &&
               manifestRefCounts.TryGetValue(manifest.GetInstanceID(), out int count) &&
               count > 0;
    }

    private bool TryGetAddressKey(UnityEngine.Object sourceAsset, out string addressKey)
    {
        if (registry != null && registry.TryGetAddressKey(sourceAsset, out addressKey))
            return true;

        addressKey = null;
        return false;
    }

    private AssetProviderOperation StartCombinedOperation(
        string label,
        List<AssetProviderOperation> operations,
        Func<List<AssetProviderOperation>> onCompleted = null)
    {
        operations ??= new List<AssetProviderOperation>();
        if (operations.Count == 0 && onCompleted == null)
        {
            return AssetProviderOperation.Completed(label);
        }

        var combinedOperation = new AssetProviderOperation(label);
        combinedOperation.SetProgressUnits(CalculateOperationUnits(operations));
        StartCoroutine(CompleteCombinedOperation(combinedOperation, operations, onCompleted));
        return combinedOperation;
    }

    private IEnumerator CompleteCombinedOperation(
        AssetProviderOperation combinedOperation,
        List<AssetProviderOperation> operations,
        Func<List<AssetProviderOperation>> onCompleted)
    {
        yield return WaitForOperations(combinedOperation, operations);

        string errorMessage = FindFirstOperationError(operations);

        List<AssetProviderOperation> followUpOperations = null;
        try
        {
            followUpOperations = onCompleted?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogException(ex, this);
            if (string.IsNullOrEmpty(errorMessage))
                errorMessage = ex.Message;
        }

        if (followUpOperations != null && followUpOperations.Count > 0)
        {
            operations.AddRange(followUpOperations);
            combinedOperation.SetProgressUnits(CalculateOperationUnits(operations));
            yield return WaitForOperations(combinedOperation, operations);

            if (string.IsNullOrEmpty(errorMessage))
                errorMessage = FindFirstOperationError(operations);
        }

        combinedOperation.ReportProgress(1f);
        combinedOperation.Complete(errorMessage);
    }

    private static IEnumerator WaitForOperations(
        AssetProviderOperation combinedOperation,
        List<AssetProviderOperation> operations)
    {
        while (true)
        {
            bool hasPendingOperation = false;
            combinedOperation.ReportProgress(CalculateCombinedProgress(operations));

            for (int i = 0; i < operations.Count; i++)
            {
                AssetProviderOperation operation = operations[i];
                if (operation != null && !operation.IsDone)
                {
                    hasPendingOperation = true;
                    break;
                }
            }

            if (!hasPendingOperation)
                break;

            yield return null;
        }
    }

    private static string FindFirstOperationError(List<AssetProviderOperation> operations)
    {
        if (operations == null)
            return null;

        for (int i = 0; i < operations.Count; i++)
        {
            AssetProviderOperation operation = operations[i];
            if (operation == null || operation.Succeeded)
                continue;

            return operation.ErrorMessage;
        }

        return null;
    }

    private static float CalculateCombinedProgress(List<AssetProviderOperation> operations)
    {
        if (operations == null || operations.Count == 0)
            return 1f;

        float totalUnits = 0f;
        float totalProgress = 0f;
        for (int i = 0; i < operations.Count; i++)
        {
            AssetProviderOperation operation = operations[i];
            float units = operation != null ? operation.ProgressUnits : 1f;
            float progress = operation != null ? operation.Progress01 : 1f;
            totalUnits += units;
            totalProgress += units * progress;
        }

        return totalUnits > 0f ? Mathf.Clamp01(totalProgress / totalUnits) : 1f;
    }

    private static float CalculateOperationUnits(List<AssetProviderOperation> operations)
    {
        if (operations == null || operations.Count == 0)
            return 1f;

        float totalUnits = 0f;
        for (int i = 0; i < operations.Count; i++)
        {
            AssetProviderOperation operation = operations[i];
            totalUnits += operation != null ? operation.ProgressUnits : 1f;
        }

        return totalUnits > 0f ? totalUnits : 1f;
    }

    private PresentationAssetProvider.DebugCountEntry[] BuildManifestSnapshot()
    {
        var results = new List<PresentationAssetProvider.DebugCountEntry>(manifestRefCounts.Count);
        foreach (KeyValuePair<int, int> pair in manifestRefCounts)
        {
            string name = trackedManifests.TryGetValue(pair.Key, out LoadManifestSO manifest) && manifest != null
                ? manifest.name
                : pair.Key.ToString();
            results.Add(new PresentationAssetProvider.DebugCountEntry(name, pair.Value));
        }

        results.Sort((left, right) => right.Count.CompareTo(left.Count));
        return results.ToArray();
    }

    private PresentationAssetProvider.DebugCountEntry[] BuildRouteManifestSnapshot()
    {
        var results = new List<PresentationAssetProvider.DebugCountEntry>(routeManifestRefCounts.Count);
        foreach (KeyValuePair<int, int> pair in routeManifestRefCounts)
        {
            string name = trackedRouteManifests.TryGetValue(pair.Key, out RouteSetLoadManifestSO manifest) && manifest != null
                ? manifest.name
                : pair.Key.ToString();
            results.Add(new PresentationAssetProvider.DebugCountEntry(name, pair.Value));
        }

        results.Sort((left, right) => right.Count.CompareTo(left.Count));
        return results.ToArray();
    }

    private PresentationAssetProvider.DebugCountEntry[] BuildAssetSnapshot(int maxCount)
    {
        return BuildObjectSnapshot(assetRefCounts, maxCount);
    }

    private PresentationAssetProvider.DebugCountEntry[] BuildPrewarmSnapshot(int maxCount)
    {
        return BuildObjectSnapshot(prewarmRefCounts, maxCount);
    }

    private PresentationAssetProvider.DebugCountEntry[] BuildObjectSnapshot(Dictionary<int, int> source, int maxCount)
    {
        int safeMaxCount = Mathf.Max(1, maxCount);
        var results = new List<PresentationAssetProvider.DebugCountEntry>(source.Count);
        foreach (KeyValuePair<int, int> pair in source)
        {
            string name = trackedAssets.TryGetValue(pair.Key, out UnityEngine.Object asset) && asset != null
                ? asset.name
                : pair.Key.ToString();
            results.Add(new PresentationAssetProvider.DebugCountEntry(name, pair.Value));
        }

        results.Sort((left, right) => right.Count.CompareTo(left.Count));
        if (results.Count > safeMaxCount)
            results.RemoveRange(safeMaxCount, results.Count - safeMaxCount);

        return results.ToArray();
    }

    private PresentationAssetProvider.DebugEventEntry[] BuildDebugHistorySnapshot(int maxCount)
    {
        int safeMaxCount = Mathf.Max(1, maxCount);
        int resultCount = Mathf.Min(safeMaxCount, debugHistory.Count);
        var results = new PresentationAssetProvider.DebugEventEntry[resultCount];
        for (int i = 0; i < resultCount; i++)
        {
            int sourceIndex = debugHistory.Count - 1 - i;
            results[i] = debugHistory[sourceIndex];
        }

        return results;
    }

    private void RecordDebugEvent(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        debugHistory.Add(new PresentationAssetProvider.DebugEventEntry(Time.realtimeSinceStartup, message));
        if (debugHistory.Count > MaxDebugHistoryEntries)
            debugHistory.RemoveRange(0, debugHistory.Count - MaxDebugHistoryEntries);
    }

    private void RecordFallbackEvent(string message)
    {
        RecordDebugEvent(message);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (logFallbackWarnings)
            Debug.LogWarning($"[AddressableAssetProvider] {message}", this);
#endif
    }

    public string BuildRuntimeQueueDiagnosticSummary()
    {
        return BuildQueueDiagnosticMessage("Runtime status");
    }

    public string DumpRetainedAssetsToTextFile()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string reportDirectory = Path.Combine(projectRoot, "Logs", "Addressables");
        Directory.CreateDirectory(reportDirectory);

        string fileName = $"RuntimeRetainedAssets_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
        string reportPath = Path.Combine(reportDirectory, fileName);
        File.WriteAllText(reportPath, BuildRetainedAssetsReport(), Encoding.UTF8);

        if (logRetainedAssetDumpPath)
            Debug.Log($"[AddressableAssetProvider] Retained asset report written: {reportPath}", this);
        RecordDebugEvent($"Retained asset report written: {reportPath}");
        return reportPath;
#else
        Debug.LogWarning("[AddressableAssetProvider] Retained asset dump is only available in editor/development builds.", this);
        return null;
#endif
    }

    private string BuildRetainedAssetsReport()
    {
        var builder = new StringBuilder(256 * 1024);
        builder.AppendLine("# Runtime Retained Addressable Assets");
        builder.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine(BuildQueueDiagnosticMessage("Provider"));
        builder.AppendLine($"LoadedManifestCount: {LoadedManifestCount}");
        builder.AppendLine($"LoadedRouteManifestCount: {LoadedRouteManifestCount}");
        builder.AppendLine($"RetainedAssetCount: {RetainedAssetCount}");
        builder.AppendLine($"PrewarmedPrefabCount: {PrewarmedPrefabCount}");
        builder.AppendLine();

        builder.AppendLine("## Route Manifests");
        AppendCountSnapshot(builder, BuildRouteManifestSnapshot());
        builder.AppendLine();

        builder.AppendLine("## Manifests");
        AppendCountSnapshot(builder, BuildManifestSnapshot());
        builder.AppendLine();

        builder.AppendLine("## Retained Assets");
        builder.AppendLine("refCount\tqueued\tactive\taddress\tassetName\tloadedName\tpath");
        foreach (RetainedAssetReportEntry entry in BuildRetainedAssetReportEntries())
        {
            builder.Append(entry.RefCount);
            builder.Append('\t');
            builder.Append(entry.IsQueued ? "yes" : "no");
            builder.Append('\t');
            builder.Append(entry.IsActive ? "yes" : "no");
            builder.Append('\t');
            builder.Append(entry.AddressKey);
            builder.Append('\t');
            builder.Append(entry.AssetName);
            builder.Append('\t');
            builder.Append(entry.LoadedName);
            builder.Append('\t');
            builder.AppendLine(entry.AssetPath);
        }

        builder.AppendLine();
        builder.AppendLine("## Prewarm Refs");
        AppendCountSnapshot(builder, BuildPrewarmSnapshot(int.MaxValue));
        return builder.ToString();
    }

    private static void AppendCountSnapshot(StringBuilder builder, PresentationAssetProvider.DebugCountEntry[] entries)
    {
        if (entries == null || entries.Length == 0)
        {
            builder.AppendLine("<none>");
            return;
        }

        for (int i = 0; i < entries.Length; i++)
            builder.AppendLine($"{entries[i].Count}\t{entries[i].Name}");
    }

    private List<RetainedAssetReportEntry> BuildRetainedAssetReportEntries()
    {
        var entries = new List<RetainedAssetReportEntry>(assetRefCounts.Count);
        foreach (KeyValuePair<int, int> pair in assetRefCounts)
        {
            UnityEngine.Object sourceAsset = null;
            trackedAssets.TryGetValue(pair.Key, out UnityEngine.Object trackedAsset);

            if (loadStates.TryGetValue(pair.Key, out LoadState state))
                sourceAsset = state.SourceAsset;

            sourceAsset ??= trackedAsset;
            string assetName = sourceAsset != null ? sourceAsset.name : pair.Key.ToString();
            string loadedName = trackedAsset != null ? trackedAsset.name : "<none>";
            string assetPath = ResolveDebugAssetPath(sourceAsset);
            string addressKey = state != null && !string.IsNullOrWhiteSpace(state.AddressKey)
                ? state.AddressKey
                : "<direct/fallback>";
            bool isActive = state != null && state.HasHandle && !state.Handle.IsDone;

            entries.Add(new RetainedAssetReportEntry(
                pair.Value,
                state != null && state.IsQueued,
                isActive,
                addressKey,
                assetName,
                loadedName,
                assetPath));
        }

        entries.Sort((left, right) =>
        {
            int pathCompare = string.Compare(left.AssetPath, right.AssetPath, StringComparison.OrdinalIgnoreCase);
            if (pathCompare != 0)
                return pathCompare;

            return string.Compare(left.AssetName, right.AssetName, StringComparison.OrdinalIgnoreCase);
        });
        return entries;
    }

    private static string ResolveDebugAssetPath(UnityEngine.Object asset)
    {
        if (asset == null)
            return "<missing>";

#if UNITY_EDITOR
        string path = AssetDatabase.GetAssetPath(asset);
        return string.IsNullOrWhiteSpace(path) ? "<runtime>" : path;
#else
        return "<editor-only-path>";
#endif
    }

    private readonly struct RetainedAssetReportEntry
    {
        public RetainedAssetReportEntry(
            int refCount,
            bool isQueued,
            bool isActive,
            string addressKey,
            string assetName,
            string loadedName,
            string assetPath)
        {
            RefCount = refCount;
            IsQueued = isQueued;
            IsActive = isActive;
            AddressKey = string.IsNullOrWhiteSpace(addressKey) ? "<none>" : addressKey;
            AssetName = string.IsNullOrWhiteSpace(assetName) ? "<unnamed>" : assetName;
            LoadedName = string.IsNullOrWhiteSpace(loadedName) ? "<none>" : loadedName;
            AssetPath = string.IsNullOrWhiteSpace(assetPath) ? "<unknown>" : assetPath;
        }

        public int RefCount { get; }
        public bool IsQueued { get; }
        public bool IsActive { get; }
        public string AddressKey { get; }
        public string AssetName { get; }
        public string LoadedName { get; }
        public string AssetPath { get; }
    }

    private string BuildQueueDiagnosticMessage(string prefix)
    {
        int queuedCount = pendingLoadQueue.Count;
        int activeCount = activeAddressableLoadCount;
        int retainedCount = assetRefCounts.Count;
        int stateCount = loadStates.Count;
        int prewarmCount = prewarmRefCounts.Count;
        return $"{prefix}: queued={queuedCount}, active={activeCount}, states={stateCount}, retained={retainedCount}, prewarm={prewarmCount}";
    }

    private static string BuildOperationLabel(string action, UnityEngine.Object target)
    {
        string targetName = target != null ? target.name : "<null>";
        return $"{action} {targetName}";
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

                string assetPath = AssetDatabase.GetAssetPath(candidate);
                if (string.Equals(assetPath, LoadingBootstrapConfigSO.SourceAssetPath, StringComparison.OrdinalIgnoreCase))
                    return candidate;
            }
#endif

            if (loadedConfigs.Length > 0)
                return loadedConfigs[0];
        }

#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<LoadingBootstrapConfigSO>(LoadingBootstrapConfigSO.SourceAssetPath);
#else
        return null;
#endif
    }
}
