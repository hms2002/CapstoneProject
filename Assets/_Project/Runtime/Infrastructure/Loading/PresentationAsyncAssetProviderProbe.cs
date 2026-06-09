using System;
using System.Collections;
using CapstonePresentation;
using UnityEngine;

[DefaultExecutionOrder(-869)]
[DisallowMultipleComponent]
public sealed class PresentationAsyncAssetProviderProbe : MonoBehaviour, IAssetProvider
{
    [SerializeField, Min(0)] private int lifecycleDelayFrames = 1;
    [SerializeField, Min(0)] private int resolveDelayFrames = 1;
    [SerializeField] private bool verboseLogging;

    private void OnEnable()
    {
        PresentationAssetProvider.EnsureInstance();
        PresentationAssetProvider.SetProviderOverride(this);

        if (verboseLogging)
            Debug.Log("[PresentationAsyncAssetProviderProbe] Installed provider override.", this);
    }

    private void OnDisable()
    {
        PresentationAssetProvider.ClearProviderOverride(this);

        if (verboseLogging)
            Debug.Log("[PresentationAsyncAssetProviderProbe] Cleared provider override.", this);
    }

    public void PreloadManifest(LoadManifestSO manifest)
    {
        InvokeBaseProvider(provider => provider.PreloadManifest(manifest));
    }

    public void ReleaseManifest(LoadManifestSO manifest)
    {
        InvokeBaseProvider(provider => provider.ReleaseManifest(manifest));
    }

    public void PreloadRouteSetManifest(RouteSetLoadManifestSO manifest)
    {
        InvokeBaseProvider(provider => provider.PreloadRouteSetManifest(manifest));
    }

    public void ReleaseRouteSetManifest(RouteSetLoadManifestSO manifest)
    {
        InvokeBaseProvider(provider => provider.ReleaseRouteSetManifest(manifest));
    }

    public AssetProviderOperation PreloadManifestAsync(LoadManifestSO manifest)
    {
        return StartLifecycleOperation(
            BuildOperationLabel("PreloadManifest", manifest),
            lifecycleDelayFrames,
            () => InvokeBaseProvider(provider => provider.PreloadManifest(manifest)));
    }

    public AssetProviderOperation ReleaseManifestAsync(LoadManifestSO manifest)
    {
        return StartLifecycleOperation(
            BuildOperationLabel("ReleaseManifest", manifest),
            lifecycleDelayFrames,
            () => InvokeBaseProvider(provider => provider.ReleaseManifest(manifest)));
    }

    public AssetProviderOperation PreloadRouteSetManifestAsync(RouteSetLoadManifestSO manifest)
    {
        return StartLifecycleOperation(
            BuildOperationLabel("PreloadRouteSetManifest", manifest),
            lifecycleDelayFrames,
            () => InvokeBaseProvider(provider => provider.PreloadRouteSetManifest(manifest)));
    }

    public AssetProviderOperation ReleaseRouteSetManifestAsync(RouteSetLoadManifestSO manifest)
    {
        return StartLifecycleOperation(
            BuildOperationLabel("ReleaseRouteSetManifest", manifest),
            lifecycleDelayFrames,
            () => InvokeBaseProvider(provider => provider.ReleaseRouteSetManifest(manifest)));
    }

    public GameObject ResolvePrefab(GameObject prefab)
    {
        IAssetProvider baseProvider = ResolveBaseProvider();
        return baseProvider != null ? baseProvider.ResolvePrefab(prefab) : prefab;
    }

    public PresentationCueSO ResolveCue(PresentationCueSO cue)
    {
        IAssetProvider baseProvider = ResolveBaseProvider();
        return baseProvider != null ? baseProvider.ResolveCue(cue) : cue;
    }

    public T ResolveAsset<T>(T asset) where T : UnityEngine.Object
    {
        IAssetProvider baseProvider = ResolveBaseProvider();
        return baseProvider != null ? baseProvider.ResolveAsset(asset) : asset;
    }

    public AssetResolveOperation<GameObject> ResolvePrefabAsync(GameObject prefab)
    {
        return StartResolveOperation(
            BuildOperationLabel("ResolvePrefab", prefab),
            resolveDelayFrames,
            () => ResolvePrefab(prefab));
    }

    public AssetResolveOperation<PresentationCueSO> ResolveCueAsync(PresentationCueSO cue)
    {
        return StartResolveOperation(
            BuildOperationLabel("ResolveCue", cue),
            resolveDelayFrames,
            () => ResolveCue(cue));
    }

    public AssetResolveOperation<T> ResolveAssetAsync<T>(T asset) where T : UnityEngine.Object
    {
        return StartResolveOperation(
            BuildOperationLabel("ResolveAsset", asset),
            resolveDelayFrames,
            () => ResolveAsset(asset));
    }

    private IAssetProvider ResolveBaseProvider()
    {
        return PresentationAssetProvider.EnsureInstance();
    }

    private void InvokeBaseProvider(Action<IAssetProvider> action)
    {
        IAssetProvider baseProvider = ResolveBaseProvider();
        if (baseProvider == null)
            return;

        action?.Invoke(baseProvider);
    }

    private AssetProviderOperation StartLifecycleOperation(string label, int delayFrames, Action action)
    {
        if (delayFrames <= 0 || !isActiveAndEnabled)
        {
            action?.Invoke();
            return AssetProviderOperation.Completed(label);
        }

        var operation = new AssetProviderOperation(label);
        StartCoroutine(CompleteLifecycleOperation(operation, delayFrames, action));
        return operation;
    }

    private AssetResolveOperation<T> StartResolveOperation<T>(string label, int delayFrames, Func<T> resolver) where T : UnityEngine.Object
    {
        if (delayFrames <= 0 || !isActiveAndEnabled)
            return AssetResolveOperation<T>.Completed(resolver != null ? resolver() : null, label);

        var operation = new AssetResolveOperation<T>(label);
        StartCoroutine(CompleteResolveOperation(operation, delayFrames, resolver));
        return operation;
    }

    private IEnumerator CompleteLifecycleOperation(AssetProviderOperation operation, int delayFrames, Action action)
    {
        for (int i = 0; i < delayFrames; i++)
        {
            operation.ReportProgress((i + 1f) / (delayFrames + 1f));
            yield return null;
        }

        try
        {
            action?.Invoke();
            operation.Complete();
        }
        catch (Exception ex)
        {
            operation.Complete(ex.Message);
            if (verboseLogging)
                Debug.LogException(ex, this);
        }
    }

    private IEnumerator CompleteResolveOperation<T>(
        AssetResolveOperation<T> operation,
        int delayFrames,
        Func<T> resolver) where T : UnityEngine.Object
    {
        for (int i = 0; i < delayFrames; i++)
        {
            operation.ReportProgress((i + 1f) / (delayFrames + 1f));
            yield return null;
        }

        try
        {
            T resolved = resolver != null ? resolver() : null;
            operation.Complete(resolved);
        }
        catch (Exception ex)
        {
            operation.Complete(null, ex.Message);
            if (verboseLogging)
                Debug.LogException(ex, this);
        }
    }

    private static string BuildOperationLabel(string action, UnityEngine.Object target)
    {
        string targetName = target != null ? target.name : "<null>";
        return $"{action} {targetName}";
    }
}
