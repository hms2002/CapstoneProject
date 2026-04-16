using System.Collections.Generic;
using CapstonePresentation;
using CapstoneRuntime;
using UnityEngine;

[DefaultExecutionOrder(-870)]
[DisallowMultipleComponent]
public sealed class PresentationAssetProvider : MonoBehaviour
{
    public static PresentationAssetProvider Instance { get; private set; }

    private static bool s_isQuitting;

    private readonly Dictionary<int, int> manifestRefCounts = new();
    private readonly Dictionary<int, int> routeManifestRefCounts = new();
    private readonly Dictionary<int, int> assetRefCounts = new();
    private readonly Dictionary<int, int> prewarmRefCounts = new();
    private readonly Dictionary<int, Object> trackedAssets = new();

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
}
