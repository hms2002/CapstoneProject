using UnityEngine;

/// <summary>
/// 책임 : Gameplay 코드가 Infrastructure route manager 구현 없이 현재 런 route 상태를 읽고 포탈 route 해석을 요청하게 하는 계약이다.
/// </summary>
public interface IRunRouteBackend
{
    bool HasActivePlan { get; }
    int CurrentStageIndex { get; }
    int TotalStageCount { get; }
    RunRouteCatalogSO ActiveRouteCatalog { get; }
    CorridorBossRouteSetSO CurrentStageSet { get; }

    bool EnsurePendingPlan(ScenePortal portal);
    bool CanResolveRoute(ScenePortal portal);

#if UNITY_EDITOR
    string GetDebugResolveStatus(ScenePortal portal);
#endif
}

/// <summary>
/// 책임 : Gameplay route 사용자들이 구체 Infrastructure route manager 타입 없이 현재 런 route backend에 접근하게 하는 정적 관문이다.
/// </summary>
public static class RunRoutePlayback
{
    private static IRunRouteBackend backend;

    public static IRunRouteBackend Backend => backend;
    public static bool HasBackend => backend != null;
    public static bool HasActivePlan => backend != null && backend.HasActivePlan;
    public static int TotalStageCount => backend != null ? backend.TotalStageCount : 0;
    public static RunRouteCatalogSO ActiveRouteCatalog => backend != null ? backend.ActiveRouteCatalog : null;
    public static CorridorBossRouteSetSO CurrentStageSet => backend != null ? backend.CurrentStageSet : null;

    public static int CurrentStageIndexOrDefault
    {
        get
        {
            if (backend == null || !backend.HasActivePlan)
                return 0;

            return Mathf.Max(0, backend.CurrentStageIndex);
        }
    }

    public static int CurrentStageIndexOrInvalid => backend != null ? backend.CurrentStageIndex : -1;

    public static void RegisterBackend(IRunRouteBackend routeBackend)
    {
        backend = routeBackend;
    }

    public static void UnregisterBackend(IRunRouteBackend routeBackend)
    {
        if (ReferenceEquals(backend, routeBackend))
            backend = null;
    }

    public static bool EnsurePendingPlan(ScenePortal portal)
    {
        return backend != null && backend.EnsurePendingPlan(portal);
    }

    public static bool CanResolveRoute(ScenePortal portal)
    {
        return backend != null && backend.CanResolveRoute(portal);
    }

    public static bool TryResolveCurrentLocationName(string sceneName, out string locationName)
    {
        locationName = null;
        CorridorBossRouteSetSO currentStageSet = CurrentStageSet;
        return currentStageSet != null && currentStageSet.TryResolveLocationName(sceneName, out locationName);
    }

#if UNITY_EDITOR
    public static string GetDebugResolveStatus(ScenePortal portal)
    {
        return backend != null ? backend.GetDebugResolveStatus(portal) : "manager=null";
    }
#endif
}
