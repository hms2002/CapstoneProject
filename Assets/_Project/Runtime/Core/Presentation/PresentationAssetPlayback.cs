using UnityEngine;

/// <summary>
/// 책임 : 구체 presentation asset provider 없이 Gameplay/Presentation 코드가 prefab resolve를 요청하게 하는 backend 계약이다.
/// </summary>
public interface IPresentationAssetBackend
{
    GameObject ResolvePrefab(GameObject prefab);
}

/// <summary>
/// 책임 : Gameplay 코드가 Infrastructure asset provider 타입 없이 presentation prefab을 resolve하게 한다.
/// </summary>
public static class PresentationAssetPlayback
{
    private static IPresentationAssetBackend backend;

    public static bool IsAvailable => backend != null;

    public static void RegisterBackend(IPresentationAssetBackend assetBackend)
    {
        backend = assetBackend;
    }

    public static void UnregisterBackend(IPresentationAssetBackend assetBackend)
    {
        if (ReferenceEquals(backend, assetBackend))
            backend = null;
    }

    public static GameObject ResolvePrefab(GameObject prefab)
    {
        if (prefab == null)
            return null;

        GameObject resolved = backend != null ? backend.ResolvePrefab(prefab) : null;
        return resolved != null ? resolved : prefab;
    }
}
