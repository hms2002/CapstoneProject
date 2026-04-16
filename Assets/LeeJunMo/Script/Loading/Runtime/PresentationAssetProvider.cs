using System.Collections.Generic;
using CapstonePresentation;
using CapstoneRuntime;
using UnityEngine;

[DefaultExecutionOrder(-870)]
[DisallowMultipleComponent]
public sealed class PresentationAssetProvider : MonoBehaviour
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

    public static PresentationAssetProvider Instance { get; private set; }

    private static bool s_isQuitting;

    private readonly Dictionary<int, int> manifestRefCounts = new();
    private readonly Dictionary<int, int> routeManifestRefCounts = new();
    private readonly Dictionary<int, int> assetRefCounts = new();
    private readonly Dictionary<int, int> prewarmRefCounts = new();
    private readonly Dictionary<int, Object> trackedAssets = new();
    private readonly Dictionary<int, LoadManifestSO> trackedManifests = new();
    private readonly Dictionary<int, RouteSetLoadManifestSO> trackedRouteManifests = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoBootstrap()
    {
        if (s_isQuitting || Instance != null)
            return;

        EnsureInstance();
    }

    public static PresentationAssetProvider EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        PresentationAssetProvider existing = RuntimeServiceOwnership.FindExistingService<PresentationAssetProvider>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject host = RuntimeServiceOwnership.CreateServiceHost(nameof(PresentationAssetProvider));
        return host.AddComponent<PresentationAssetProvider>();
    }

    public static void PreloadManifest(LoadManifestSO manifest)
    {
        PresentationAssetProvider service = EnsureInstance();
        service?.AcquireManifest(manifest);
    }

    public static void ReleaseManifest(LoadManifestSO manifest)
    {
        if (manifest == null)
            return;

        PresentationAssetProvider service = EnsureInstance();
        service?.ReleaseManifestInternal(manifest);
    }

    public static void PreloadRouteSetManifest(RouteSetLoadManifestSO manifest)
    {
        PresentationAssetProvider service = EnsureInstance();
        service?.AcquireRouteSetManifest(manifest);
    }

    public static void ReleaseRouteSetManifest(RouteSetLoadManifestSO manifest)
    {
        if (manifest == null)
            return;

        PresentationAssetProvider service = EnsureInstance();
        service?.ReleaseRouteSetManifestInternal(manifest);
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

        PresentationAssetProvider service = EnsureInstance();
        service?.TrackResolvedAsset(asset);
        return asset;
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
        PresentationAssetProvider service = EnsureInstance();
        return service != null ? service.assetRefCounts.Count : 0;
    }

    public static int GetLoadedManifestCount()
    {
        PresentationAssetProvider service = EnsureInstance();
        return service != null ? service.manifestRefCounts.Count : 0;
    }

    public static int GetLoadedRouteManifestCount()
    {
        PresentationAssetProvider service = EnsureInstance();
        return service != null ? service.routeManifestRefCounts.Count : 0;
    }

    public static int GetPrewarmedPrefabCount()
    {
        PresentationAssetProvider service = EnsureInstance();
        return service != null ? service.prewarmRefCounts.Count : 0;
    }

    public static DebugCountEntry[] GetManifestSnapshot()
    {
        PresentationAssetProvider service = EnsureInstance();
        return service != null ? service.BuildManifestSnapshot() : System.Array.Empty<DebugCountEntry>();
    }

    public static DebugCountEntry[] GetRouteManifestSnapshot()
    {
        PresentationAssetProvider service = EnsureInstance();
        return service != null ? service.BuildRouteManifestSnapshot() : System.Array.Empty<DebugCountEntry>();
    }

    public static DebugCountEntry[] GetAssetSnapshot(int maxCount = 24)
    {
        PresentationAssetProvider service = EnsureInstance();
        return service != null ? service.BuildAssetSnapshot(maxCount) : System.Array.Empty<DebugCountEntry>();
    }

    public static DebugCountEntry[] GetPrewarmSnapshot(int maxCount = 24)
    {
        PresentationAssetProvider service = EnsureInstance();
        return service != null ? service.BuildPrewarmSnapshot(maxCount) : System.Array.Empty<DebugCountEntry>();
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

    private void AcquireManifest(LoadManifestSO manifest)
    {
        if (manifest == null)
            return;

        int manifestId = manifest.GetInstanceID();
        manifestRefCounts.TryGetValue(manifestId, out int currentCount);
        manifestRefCounts[manifestId] = currentCount + 1;
        trackedManifests[manifestId] = manifest;
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

        if (currentCount > 1)
        {
            manifestRefCounts[manifestId] = currentCount - 1;
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
        routeManifestRefCounts[manifestId] = currentCount + 1;
        trackedRouteManifests[manifestId] = manifest;
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

        if (currentCount > 1)
        {
            routeManifestRefCounts[manifestId] = currentCount - 1;
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
        prewarmRefCounts[prefabId] = currentCount + entry.EffectiveCount;
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
        if (currentCount > releaseCount)
            prewarmRefCounts[prefabId] = currentCount - releaseCount;
        else
            prewarmRefCounts.Remove(prefabId);

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
}
