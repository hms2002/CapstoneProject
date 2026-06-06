using CapstonePresentation;
using UnityEngine;

public interface IAssetProvider
{
    void PreloadManifest(LoadManifestSO manifest);
    void ReleaseManifest(LoadManifestSO manifest);
    void PreloadRouteSetManifest(RouteSetLoadManifestSO manifest);
    void ReleaseRouteSetManifest(RouteSetLoadManifestSO manifest);
    AssetProviderOperation PreloadManifestAsync(LoadManifestSO manifest);
    AssetProviderOperation ReleaseManifestAsync(LoadManifestSO manifest);
    AssetProviderOperation PreloadRouteSetManifestAsync(RouteSetLoadManifestSO manifest);
    AssetProviderOperation ReleaseRouteSetManifestAsync(RouteSetLoadManifestSO manifest);
    GameObject ResolvePrefab(GameObject prefab);
    PresentationCueSO ResolveCue(PresentationCueSO cue);
    T ResolveAsset<T>(T asset) where T : Object;
    AssetResolveOperation<GameObject> ResolvePrefabAsync(GameObject prefab);
    AssetResolveOperation<PresentationCueSO> ResolveCueAsync(PresentationCueSO cue);
    AssetResolveOperation<T> ResolveAssetAsync<T>(T asset) where T : Object;
}
