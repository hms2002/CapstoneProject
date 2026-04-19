using System.Collections.Generic;
using CapstonePresentation;
using CapstoneRuntime;
using UnityEngine;

[DefaultExecutionOrder(-870)]
[DisallowMultipleComponent]
public sealed class PresentationAssetProvider : MonoBehaviour, IAssetProvider, IAssetProviderDebugInfo
{
    public readonly struct DebugCountEntry
    {
        public DebugCountEntry(string name, int count)
        {
            Name = name;
            Count = count;
        }

        public string Name { get; }
        public int Count { get; }
    }

    public readonly struct DebugEventEntry
    {
        public DebugEventEntry(float realtimeSeconds, string message)
        {
            RealtimeSeconds = realtimeSeconds;
            Message = message;
        }

        public float RealtimeSeconds { get; }
        public string Message { get; }
    }

    public static PresentationAssetProvider Instance { get; private set; }

    private static bool s_isQuitting;
    private static IAssetProvider s_providerOverride;
    private const int MaxDebugHistoryEntries = 96;

    private readonly Dictionary<int, int> manifestRefCounts = new();
    private readonly Dictionary<int, int> routeManifestRefCounts = new();
    private readonly Dictionary<int, int> assetRefCounts = new();
    private readonly Dictionary<int, int> prewarmRefCounts = new();
    private readonly List<DebugEventEntry> debugHistory = new();
    private readonly Dictionary<int, Object> trackedAssets = new();
    private readonly Dictionary<int, LoadManifestSO> trackedManifests = new();
    private readonly Dictionary<int, RouteSetLoadManifestSO> trackedRouteManifests = new();

    int IAssetProviderDebugInfo.LoadedManifestCount => manifestRefCounts.Count;
    int IAssetProviderDebugInfo.LoadedRouteManifestCount => routeManifestRefCounts.Count;
    int IAssetProviderDebugInfo.RetainedAssetCount => assetRefCounts.Count;
    int IAssetProviderDebugInfo.PrewarmedPrefabCount => prewarmRefCounts.Count;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoBootstrap()
    {
        if (s_isQuitting || Instance != null || s_providerOverride != null)
            return;

        EnsureInstance();
    }

    public static IAssetProvider CurrentProvider => GetCurrentProviderWithoutCreating() ?? EnsureInstance();

    public static IAssetProvider GetCurrentProviderWithoutCreating()
    {
        if (s_providerOverride is Object overrideObject && overrideObject == null)
            s_providerOverride = null;

        if (s_providerOverride != null)
            return s_providerOverride;

        if (Instance != null)
            return Instance;

        PresentationAssetProvider existing = RuntimeServiceOwnership.FindExistingService<PresentationAssetProvider>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        return null;
    }

    public static PresentationAssetProvider EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        if (s_isQuitting)
            return null;

        PresentationAssetProvider existing = RuntimeServiceOwnership.FindExistingService<PresentationAssetProvider>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject host = RuntimeServiceOwnership.CreateServiceHost(nameof(PresentationAssetProvider));
        return host.AddComponent<PresentationAssetProvider>();
    }

    public static bool IsProviderOverrideActive => s_providerOverride != null;

    public static string GetCurrentProviderName()
    {
        IAssetProvider provider = GetCurrentProviderWithoutCreating() ?? EnsureInstance();
        return provider != null ? provider.GetType().Name : "<none>";
    }

    public static void SetProviderOverride(IAssetProvider provider)
    {
        s_providerOverride = provider;
    }

    public static void ClearProviderOverride(IAssetProvider provider = null)
    {
        if (provider == null || ReferenceEquals(s_providerOverride, provider))
            s_providerOverride = null;
    }

    public static void PreloadManifest(LoadManifestSO manifest)
    {
        CurrentProvider?.PreloadManifest(manifest);
    }

    public static void ReleaseManifest(LoadManifestSO manifest)
    {
        if (manifest == null)
            return;

        CurrentProvider?.ReleaseManifest(manifest);
    }

    public static void PreloadRouteSetManifest(RouteSetLoadManifestSO manifest)
    {
        CurrentProvider?.PreloadRouteSetManifest(manifest);
    }

    public static void ReleaseRouteSetManifest(RouteSetLoadManifestSO manifest)
    {
        if (manifest == null)
            return;

        CurrentProvider?.ReleaseRouteSetManifest(manifest);
    }

    public static AssetProviderOperation PreloadManifestAsync(LoadManifestSO manifest)
    {
        if (manifest == null)
            return AssetProviderOperation.Completed("PreloadManifest <null>");

        IAssetProvider provider = CurrentProvider;
        return provider != null
            ? provider.PreloadManifestAsync(manifest)
            : AssetProviderOperation.Completed(BuildOperationLabel("PreloadManifest", manifest));
    }

    public static AssetProviderOperation ReleaseManifestAsync(LoadManifestSO manifest)
    {
        if (manifest == null)
            return AssetProviderOperation.Completed("ReleaseManifest <null>");

        IAssetProvider provider = CurrentProvider;
        return provider != null
            ? provider.ReleaseManifestAsync(manifest)
            : AssetProviderOperation.Completed(BuildOperationLabel("ReleaseManifest", manifest));
    }

    public static AssetProviderOperation PreloadRouteSetManifestAsync(RouteSetLoadManifestSO manifest)
    {
        if (manifest == null)
            return AssetProviderOperation.Completed("PreloadRouteSetManifest <null>");

        IAssetProvider provider = CurrentProvider;
        return provider != null
            ? provider.PreloadRouteSetManifestAsync(manifest)
            : AssetProviderOperation.Completed(BuildOperationLabel("PreloadRouteSetManifest", manifest));
    }

    public static AssetProviderOperation ReleaseRouteSetManifestAsync(RouteSetLoadManifestSO manifest)
    {
        if (manifest == null)
            return AssetProviderOperation.Completed("ReleaseRouteSetManifest <null>");

        IAssetProvider provider = CurrentProvider;
        return provider != null
            ? provider.ReleaseRouteSetManifestAsync(manifest)
            : AssetProviderOperation.Completed(BuildOperationLabel("ReleaseRouteSetManifest", manifest));
    }

    public static GameObject ResolvePrefab(GameObject prefab)
    {
        return ResolveAsset(prefab);
    }

    public static PresentationCueSO ResolveCue(PresentationCueSO cue)
    {
        return ResolveAsset(cue);
    }

    public static T ResolveAsset<T>(T asset) where T : Object
    {
        if (asset == null)
            return null;

        return CurrentProvider != null ? CurrentProvider.ResolveAsset(asset) : asset;
    }

    public static AssetResolveOperation<GameObject> ResolvePrefabAsync(GameObject prefab)
    {
        return ResolveAssetAsync(prefab);
    }

    public static AssetResolveOperation<PresentationCueSO> ResolveCueAsync(PresentationCueSO cue)
    {
        return ResolveAssetAsync(cue);
    }

    public static AssetResolveOperation<T> ResolveAssetAsync<T>(T asset) where T : Object
    {
        if (asset == null)
            return AssetResolveOperation<T>.Completed(null, "ResolveAsset <null>");

        IAssetProvider provider = CurrentProvider;
        return provider != null
            ? provider.ResolveAssetAsync(asset)
            : AssetResolveOperation<T>.Completed(asset, BuildOperationLabel("ResolveAsset", asset));
    }

    public static bool IsManifestLoaded(LoadManifestSO manifest)
    {
        if (manifest == null)
            return false;

        PresentationAssetProvider service = EnsureInstance();
        return service != null && service.manifestRefCounts.TryGetValue(manifest.GetInstanceID(), out int count) && count > 0;
    }

    public static bool IsRouteSetManifestLoaded(RouteSetLoadManifestSO manifest)
    {
        if (manifest == null)
            return false;

        PresentationAssetProvider service = EnsureInstance();
        return service != null &&
               service.routeManifestRefCounts.TryGetValue(manifest.GetInstanceID(), out int count) &&
               count > 0;
    }

    public static bool IsAssetLoaded(Object asset)
    {
        if (asset == null)
            return false;

        PresentationAssetProvider service = EnsureInstance();
        return service != null && service.assetRefCounts.TryGetValue(asset.GetInstanceID(), out int count) && count > 0;
    }

    public static int GetRetainedAssetCount()
    {
        IAssetProviderDebugInfo debugProvider = ResolveDebugProvider();
        return debugProvider != null ? debugProvider.RetainedAssetCount : 0;
    }

    public static int GetLoadedManifestCount()
    {
        IAssetProviderDebugInfo debugProvider = ResolveDebugProvider();
        return debugProvider != null ? debugProvider.LoadedManifestCount : 0;
    }

    public static int GetLoadedRouteManifestCount()
    {
        IAssetProviderDebugInfo debugProvider = ResolveDebugProvider();
        return debugProvider != null ? debugProvider.LoadedRouteManifestCount : 0;
    }

    public static int GetPrewarmedPrefabCount()
    {
        IAssetProviderDebugInfo debugProvider = ResolveDebugProvider();
        return debugProvider != null ? debugProvider.PrewarmedPrefabCount : 0;
    }

    public static DebugCountEntry[] GetManifestSnapshot()
    {
        IAssetProviderDebugInfo debugProvider = ResolveDebugProvider();
        return debugProvider != null ? debugProvider.GetManifestSnapshot() : System.Array.Empty<DebugCountEntry>();
    }

    public static DebugCountEntry[] GetRouteManifestSnapshot()
    {
        IAssetProviderDebugInfo debugProvider = ResolveDebugProvider();
        return debugProvider != null ? debugProvider.GetRouteManifestSnapshot() : System.Array.Empty<DebugCountEntry>();
    }

    public static DebugCountEntry[] GetAssetSnapshot(int maxCount = 24)
    {
        IAssetProviderDebugInfo debugProvider = ResolveDebugProvider();
        return debugProvider != null ? debugProvider.GetAssetSnapshot(maxCount) : System.Array.Empty<DebugCountEntry>();
    }

    public static DebugCountEntry[] GetPrewarmSnapshot(int maxCount = 24)
    {
        IAssetProviderDebugInfo debugProvider = ResolveDebugProvider();
        return debugProvider != null ? debugProvider.GetPrewarmSnapshot(maxCount) : System.Array.Empty<DebugCountEntry>();
    }

    public static DebugEventEntry[] GetDebugHistorySnapshot(int maxCount = 24)
    {
        IAssetProviderDebugInfo debugProvider = ResolveDebugProvider();
        return debugProvider != null ? debugProvider.GetDebugHistorySnapshot(maxCount) : System.Array.Empty<DebugEventEntry>();
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

    void IAssetProvider.PreloadManifest(LoadManifestSO manifest)
    {
        AcquireManifest(manifest);
    }

    void IAssetProvider.ReleaseManifest(LoadManifestSO manifest)
    {
        ReleaseManifestInternal(manifest);
    }

    AssetProviderOperation IAssetProvider.PreloadManifestAsync(LoadManifestSO manifest)
    {
        AcquireManifest(manifest);
        return AssetProviderOperation.Completed(BuildOperationLabel("PreloadManifest", manifest));
    }

    AssetProviderOperation IAssetProvider.ReleaseManifestAsync(LoadManifestSO manifest)
    {
        ReleaseManifestInternal(manifest);
        return AssetProviderOperation.Completed(BuildOperationLabel("ReleaseManifest", manifest));
    }

    void IAssetProvider.PreloadRouteSetManifest(RouteSetLoadManifestSO manifest)
    {
        AcquireRouteSetManifest(manifest);
    }

    AssetProviderOperation IAssetProvider.PreloadRouteSetManifestAsync(RouteSetLoadManifestSO manifest)
    {
        AcquireRouteSetManifest(manifest);
        return AssetProviderOperation.Completed(BuildOperationLabel("PreloadRouteSetManifest", manifest));
    }

    void IAssetProvider.ReleaseRouteSetManifest(RouteSetLoadManifestSO manifest)
    {
        ReleaseRouteSetManifestInternal(manifest);
    }

    AssetProviderOperation IAssetProvider.ReleaseRouteSetManifestAsync(RouteSetLoadManifestSO manifest)
    {
        ReleaseRouteSetManifestInternal(manifest);
        return AssetProviderOperation.Completed(BuildOperationLabel("ReleaseRouteSetManifest", manifest));
    }

    GameObject IAssetProvider.ResolvePrefab(GameObject prefab)
    {
        return ResolveAssetInternal(prefab);
    }

    AssetResolveOperation<GameObject> IAssetProvider.ResolvePrefabAsync(GameObject prefab)
    {
        GameObject resolved = ResolveAssetInternal(prefab);
        return AssetResolveOperation<GameObject>.Completed(resolved, BuildOperationLabel("ResolvePrefab", prefab));
    }

    PresentationCueSO IAssetProvider.ResolveCue(PresentationCueSO cue)
    {
        return ResolveAssetInternal(cue);
    }

    AssetResolveOperation<PresentationCueSO> IAssetProvider.ResolveCueAsync(PresentationCueSO cue)
    {
        PresentationCueSO resolved = ResolveAssetInternal(cue);
        return AssetResolveOperation<PresentationCueSO>.Completed(resolved, BuildOperationLabel("ResolveCue", cue));
    }

    T IAssetProvider.ResolveAsset<T>(T asset)
    {
        return ResolveAssetInternal(asset);
    }

    AssetResolveOperation<T> IAssetProvider.ResolveAssetAsync<T>(T asset)
    {
        T resolved = ResolveAssetInternal(asset);
        return AssetResolveOperation<T>.Completed(resolved, BuildOperationLabel("ResolveAsset", asset));
    }

    DebugCountEntry[] IAssetProviderDebugInfo.GetManifestSnapshot()
    {
        return BuildManifestSnapshot();
    }

    DebugCountEntry[] IAssetProviderDebugInfo.GetRouteManifestSnapshot()
    {
        return BuildRouteManifestSnapshot();
    }

    DebugCountEntry[] IAssetProviderDebugInfo.GetAssetSnapshot(int maxCount)
    {
        return BuildAssetSnapshot(maxCount);
    }

    DebugCountEntry[] IAssetProviderDebugInfo.GetPrewarmSnapshot(int maxCount)
    {
        return BuildPrewarmSnapshot(maxCount);
    }

    DebugEventEntry[] IAssetProviderDebugInfo.GetDebugHistorySnapshot(int maxCount)
    {
        return BuildDebugHistorySnapshot(maxCount);
    }

    private void AcquireManifest(LoadManifestSO manifest)
    {
        if (manifest == null)
            return;

        int manifestId = manifest.GetInstanceID();
        manifestRefCounts.TryGetValue(manifestId, out int currentCount);
        int nextCount = currentCount + 1;
        manifestRefCounts[manifestId] = nextCount;
        trackedManifests[manifestId] = manifest;
        RecordDebugEvent(
            $"Manifest + {manifest.name} ({currentCount}->{nextCount}, assets={CountReferencedAssets(manifest)}, prewarm={CountPrewarmEntries(manifest)})");
        if (currentCount > 0)
            return;

        foreach (Object asset in manifest.EnumerateReferencedAssets())
        {
            RetainAsset(asset);
        }

        foreach (PrewarmPrefabEntry prewarmEntry in manifest.EnumeratePrewarmEntries())
        {
            RetainPrewarm(prewarmEntry);
        }
    }

    private void ReleaseManifestInternal(LoadManifestSO manifest)
    {
        if (manifest == null)
            return;

        int manifestId = manifest.GetInstanceID();
        if (!manifestRefCounts.TryGetValue(manifestId, out int currentCount))
            return;

        int nextCount = Mathf.Max(0, currentCount - 1);
        RecordDebugEvent(
            $"Manifest - {manifest.name} ({currentCount}->{nextCount}, assets={CountReferencedAssets(manifest)}, prewarm={CountPrewarmEntries(manifest)})");
        if (currentCount > 1)
        {
            manifestRefCounts[manifestId] = nextCount;
            return;
        }

        manifestRefCounts.Remove(manifestId);
        trackedManifests.Remove(manifestId);

        foreach (Object asset in manifest.EnumerateReferencedAssets())
        {
            ReleaseAsset(asset);
        }

        foreach (PrewarmPrefabEntry prewarmEntry in manifest.EnumeratePrewarmEntries())
        {
            ReleasePrewarm(prewarmEntry);
        }
    }

    private void AcquireRouteSetManifest(RouteSetLoadManifestSO manifest)
    {
        if (manifest == null)
            return;

        int manifestId = manifest.GetInstanceID();
        routeManifestRefCounts.TryGetValue(manifestId, out int currentCount);
        int nextCount = currentCount + 1;
        routeManifestRefCounts[manifestId] = nextCount;
        trackedRouteManifests[manifestId] = manifest;
        RecordDebugEvent(
            $"Route manifest + {manifest.name} ({currentCount}->{nextCount}, children={CountChildManifests(manifest)})");
        if (currentCount > 0)
            return;

        foreach (LoadManifestSO childManifest in manifest.EnumerateManifests())
        {
            AcquireManifest(childManifest);
        }
    }

    private void ReleaseRouteSetManifestInternal(RouteSetLoadManifestSO manifest)
    {
        if (manifest == null)
            return;

        int manifestId = manifest.GetInstanceID();
        if (!routeManifestRefCounts.TryGetValue(manifestId, out int currentCount))
            return;

        int nextCount = Mathf.Max(0, currentCount - 1);
        RecordDebugEvent(
            $"Route manifest - {manifest.name} ({currentCount}->{nextCount}, children={CountChildManifests(manifest)})");
        if (currentCount > 1)
        {
            routeManifestRefCounts[manifestId] = nextCount;
            return;
        }

        routeManifestRefCounts.Remove(manifestId);
        trackedRouteManifests.Remove(manifestId);

        foreach (LoadManifestSO childManifest in manifest.EnumerateManifests())
        {
            ReleaseManifestInternal(childManifest);
        }
    }

    private void TrackResolvedAsset(Object asset)
    {
        if (asset == null)
            return;

        int assetId = asset.GetInstanceID();
        if (!trackedAssets.ContainsKey(assetId))
            trackedAssets.Add(assetId, asset);
    }

    private T ResolveAssetInternal<T>(T asset) where T : Object
    {
        if (asset == null)
            return null;

        TrackResolvedAsset(asset);
        return asset;
    }

    private void RetainAsset(Object asset)
    {
        if (asset == null)
            return;

        int assetId = asset.GetInstanceID();
        assetRefCounts.TryGetValue(assetId, out int currentCount);
        assetRefCounts[assetId] = currentCount + 1;
        trackedAssets[assetId] = asset;
    }

    private void ReleaseAsset(Object asset)
    {
        if (asset == null)
            return;

        int assetId = asset.GetInstanceID();
        if (!assetRefCounts.TryGetValue(assetId, out int currentCount))
            return;

        if (currentCount > 1)
        {
            assetRefCounts[assetId] = currentCount - 1;
            return;
        }

        assetRefCounts.Remove(assetId);
        trackedAssets.Remove(assetId);
    }

    private void RetainPrewarm(PrewarmPrefabEntry entry)
    {
        if (!entry.IsValid)
            return;

        int prefabId = entry.prefab.GetInstanceID();
        prewarmRefCounts.TryGetValue(prefabId, out int currentCount);
        int nextCount = currentCount + entry.EffectiveCount;
        prewarmRefCounts[prefabId] = nextCount;
        RecordDebugEvent(
            $"Prewarm + {entry.prefab.name} ({currentCount}->{nextCount})");
        PresentationSpawnService.PrewarmPrefab(entry.prefab, entry.EffectiveCount);
    }

    private void ReleasePrewarm(PrewarmPrefabEntry entry)
    {
        if (!entry.IsValid)
            return;

        int prefabId = entry.prefab.GetInstanceID();
        if (!prewarmRefCounts.TryGetValue(prefabId, out int currentCount))
            return;

        int releaseCount = Mathf.Min(entry.EffectiveCount, currentCount);
        int nextCount = Mathf.Max(0, currentCount - releaseCount);
        if (currentCount > releaseCount)
            prewarmRefCounts[prefabId] = nextCount;
        else
            prewarmRefCounts.Remove(prefabId);

        RecordDebugEvent(
            $"Prewarm - {entry.prefab.name} ({currentCount}->{nextCount})");
        PresentationSpawnService.TrimPrewarmedPrefab(entry.prefab, releaseCount);
    }

    private DebugCountEntry[] BuildManifestSnapshot()
    {
        var results = new List<DebugCountEntry>(manifestRefCounts.Count);
        foreach (KeyValuePair<int, int> pair in manifestRefCounts)
        {
            string name = trackedManifests.TryGetValue(pair.Key, out LoadManifestSO manifest) && manifest != null
                ? manifest.name
                : pair.Key.ToString();
            results.Add(new DebugCountEntry(name, pair.Value));
        }

        results.Sort((left, right) => right.Count.CompareTo(left.Count));
        return results.ToArray();
    }

    private DebugCountEntry[] BuildRouteManifestSnapshot()
    {
        var results = new List<DebugCountEntry>(routeManifestRefCounts.Count);
        foreach (KeyValuePair<int, int> pair in routeManifestRefCounts)
        {
            string name = trackedRouteManifests.TryGetValue(pair.Key, out RouteSetLoadManifestSO manifest) && manifest != null
                ? manifest.name
                : pair.Key.ToString();
            results.Add(new DebugCountEntry(name, pair.Value));
        }

        results.Sort((left, right) => right.Count.CompareTo(left.Count));
        return results.ToArray();
    }

    private DebugCountEntry[] BuildAssetSnapshot(int maxCount)
    {
        return BuildObjectSnapshot(assetRefCounts, maxCount);
    }

    private DebugCountEntry[] BuildPrewarmSnapshot(int maxCount)
    {
        return BuildObjectSnapshot(prewarmRefCounts, maxCount);
    }

    private DebugCountEntry[] BuildObjectSnapshot(Dictionary<int, int> source, int maxCount)
    {
        int safeMaxCount = Mathf.Max(1, maxCount);
        var results = new List<DebugCountEntry>(source.Count);
        foreach (KeyValuePair<int, int> pair in source)
        {
            string name = trackedAssets.TryGetValue(pair.Key, out Object asset) && asset != null
                ? asset.name
                : pair.Key.ToString();
            results.Add(new DebugCountEntry(name, pair.Value));
        }

        results.Sort((left, right) => right.Count.CompareTo(left.Count));
        if (results.Count > safeMaxCount)
            results.RemoveRange(safeMaxCount, results.Count - safeMaxCount);

        return results.ToArray();
    }

    private DebugEventEntry[] BuildDebugHistorySnapshot(int maxCount)
    {
        int safeMaxCount = Mathf.Max(1, maxCount);
        int resultCount = Mathf.Min(safeMaxCount, debugHistory.Count);
        var results = new DebugEventEntry[resultCount];
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

        debugHistory.Add(new DebugEventEntry(Time.realtimeSinceStartup, message));
        if (debugHistory.Count > MaxDebugHistoryEntries)
            debugHistory.RemoveRange(0, debugHistory.Count - MaxDebugHistoryEntries);
    }

    private static int CountReferencedAssets(LoadManifestSO manifest)
    {
        if (manifest == null)
            return 0;

        int count = 0;
        foreach (Object asset in manifest.EnumerateReferencedAssets())
        {
            if (asset != null)
                count++;
        }

        return count;
    }

    private static int CountPrewarmEntries(LoadManifestSO manifest)
    {
        if (manifest == null)
            return 0;

        int count = 0;
        foreach (PrewarmPrefabEntry entry in manifest.EnumeratePrewarmEntries())
        {
            if (entry.IsValid)
                count++;
        }

        return count;
    }

    private static int CountChildManifests(RouteSetLoadManifestSO manifest)
    {
        if (manifest == null)
            return 0;

        int count = 0;
        foreach (LoadManifestSO childManifest in manifest.EnumerateManifests())
        {
            if (childManifest != null)
                count++;
        }

        return count;
    }

    private static string BuildOperationLabel(string action, Object target)
    {
        string targetName = target != null ? target.name : "<null>";
        return $"{action} {targetName}";
    }

    private static IAssetProviderDebugInfo ResolveDebugProvider()
    {
        IAssetProvider provider = GetCurrentProviderWithoutCreating() ?? CurrentProvider;
        if (provider is IAssetProviderDebugInfo debugProvider)
            return debugProvider;

        return EnsureInstance();
    }
}
