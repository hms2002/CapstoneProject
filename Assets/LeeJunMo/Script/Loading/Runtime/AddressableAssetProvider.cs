using System;
using System.Collections;
using System.Collections.Generic;
using CapstonePresentation;
using CapstoneRuntime;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DefaultExecutionOrder(-871)]
[DisallowMultipleComponent]
public sealed class AddressableAssetProvider : MonoBehaviour, IAssetProvider, IAssetProviderDebugInfo
{
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

    [SerializeField] private LoadingAddressableRegistrySO registry;

    public int LoadedManifestCount => manifestRefCounts.Count;
    public int LoadedRouteManifestCount => routeManifestRefCounts.Count;
    public int RetainedAssetCount => assetRefCounts.Count;
    public int PrewarmedPrefabCount => prewarmRefCounts.Count;

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
                    return;

                foreach (PrewarmPrefabEntry prewarmEntry in manifest.EnumeratePrewarmEntries())
                    RetainPrewarm(prewarmEntry);
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
            RecordDebugEvent($"Address key missing for {sourceAsset.name}; using direct reference.");
            return AssetProviderOperation.Completed(BuildOperationLabel("ResolveDirectAsset", sourceAsset));
        }

        AsyncOperationHandle<UnityEngine.Object> handle;
        try
        {
            handle = Addressables.LoadAssetAsync<UnityEngine.Object>(addressKey);
        }
        catch (Exception ex)
        {
            RecordDebugEvent($"Addressables start failed for {sourceAsset.name} [{addressKey}]: {ex.Message}. Using fallback.");
            loadStates[sourceId] = new LoadState
            {
                SourceId = sourceId,
                SourceAsset = sourceAsset,
                AddressKey = addressKey,
                LoadedAsset = sourceAsset
            };
            return AssetProviderOperation.Completed(BuildOperationLabel("ResolveDirectAsset", sourceAsset));
        }

        var state = new LoadState
        {
            SourceId = sourceId,
            SourceAsset = sourceAsset,
            AddressKey = addressKey,
            Handle = handle,
            HasHandle = true
        };
        loadStates[sourceId] = state;

        var operation = new AssetProviderOperation(BuildOperationLabel("LoadAddressableAsset", sourceAsset));
        state.ActiveLoadOperation = operation;
        operation.ReportProgress(handle.PercentComplete);
        StartCoroutine(CompleteLoadOperation(state, operation));
        return operation;
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
            RecordDebugEvent($"Addressables load failed for {state.SourceAsset.name}: {errorMessage}. Using fallback.");
        else
            RecordDebugEvent($"Addressables load completed for {state.SourceAsset.name} [{state.AddressKey}]");

        operation.Complete(errorMessage);

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

    private void RetainPrewarm(PrewarmPrefabEntry entry)
    {
        if (!entry.IsValid)
            return;

        GameObject resolvedPrefab = ResolveLoadedAsset(entry.prefab);
        if (resolvedPrefab == null)
            return;

        int prefabId = resolvedPrefab.GetInstanceID();
        prewarmRefCounts.TryGetValue(prefabId, out int currentCount);
        int nextCount = currentCount + entry.EffectiveCount;
        prewarmRefCounts[prefabId] = nextCount;
        trackedAssets[prefabId] = resolvedPrefab;
        RecordDebugEvent($"Prewarm + {resolvedPrefab.name} ({currentCount}->{nextCount})");
        PresentationSpawnService.PrewarmPrefab(resolvedPrefab, entry.EffectiveCount);
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
        Action onCompleted = null)
    {
        if (operations == null || operations.Count == 0)
        {
            try
            {
                onCompleted?.Invoke();
                return AssetProviderOperation.Completed(label);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, this);
                return AssetProviderOperation.Failed(ex.Message, label);
            }
        }

        var combinedOperation = new AssetProviderOperation(label);
        combinedOperation.SetProgressUnits(CalculateOperationUnits(operations));
        StartCoroutine(CompleteCombinedOperation(combinedOperation, operations, onCompleted));
        return combinedOperation;
    }

    private IEnumerator CompleteCombinedOperation(
        AssetProviderOperation combinedOperation,
        List<AssetProviderOperation> operations,
        Action onCompleted)
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

        string errorMessage = null;
        for (int i = 0; i < operations.Count; i++)
        {
            AssetProviderOperation operation = operations[i];
            if (operation == null || operation.Succeeded)
                continue;

            errorMessage = operation.ErrorMessage;
            break;
        }

        try
        {
            onCompleted?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogException(ex, this);
            if (string.IsNullOrEmpty(errorMessage))
                errorMessage = ex.Message;
        }

        combinedOperation.ReportProgress(1f);
        combinedOperation.Complete(errorMessage);
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
