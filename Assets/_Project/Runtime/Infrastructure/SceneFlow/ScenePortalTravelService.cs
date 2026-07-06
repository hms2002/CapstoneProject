using UnityEngine;

/// <summary>
/// 책임 : ScenePortal 이동 요청을 씬 전환 coordinator, run/session 상태, route 계획에 연결하는 Infrastructure 진입점이다.
/// </summary>
public static class ScenePortalTravelService
{
    private static readonly IScenePortalTravelBackend PlaybackBackend = new ScenePortalTravelBackend();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterPlaybackBackend()
    {
        ScenePortalTravelPlayback.RegisterBackend(PlaybackBackend);
    }

    public static bool TryTravel(ScenePortal portal)
    {
        return ScenePortalTravelCoordinator.TryTravel(portal);
    }

    /// <summary>
    /// 책임 : Gameplay의 ScenePortalTravelPlayback 요청을 기존 Infrastructure 이동 실행기로 전달한다.
    /// </summary>
    private sealed class ScenePortalTravelBackend : IScenePortalTravelBackend
    {
        public bool TryTravel(ScenePortal portal)
        {
            return ScenePortalTravelCoordinator.TryTravel(portal);
        }
    }
}

internal static class ScenePortalTravelCoordinator
{
    public static bool TryTravel(ScenePortal portal)
    {
        if (portal == null)
            return false;

        var transitionCoordinator = SceneTransitionCoordinator.EnsureInstance();
        if (transitionCoordinator == null)
        {
            Debug.LogError(
                "[ScenePortalTravelService] SceneTransitionCoordinator could not be created for scene travel.",
                portal);
            return false;
        }

        if (transitionCoordinator.IsTransitionActive)
        {
            Debug.LogWarning(
                $"[ScenePortalTravelService] Travel rejected because a scene transition is already active. portal={portal.name}",
                portal);
            return false;
        }

        var request = ScenePortalTravelRequest.Create(portal);
        var route = ScenePortalTravelPlanner.ResolveRoute(request);
        if (!route.IsValid)
        {
            Debug.LogError($"[ScenePortalTravelService] Invalid route for portal {portal.name}", portal);
            return false;
        }

        var gameplay = GamePlayDataManager.EnsureInstance();
        if (gameplay == null)
        {
            Debug.LogError($"[ScenePortalTravelService] GamePlayDataManager is null. portal={portal.name}", portal);
            return false;
        }

        RunTransitionDirective runDirective = ScenePortalTravelPlanner.ResolveRunTransition(route);
        ScenePortalTravelExecutionResult result = ScenePortalTravelExecutor.Execute(
            new ScenePortalTravelExecutionRequest(
                request,
                route,
                runDirective,
                gameplay,
                transitionCoordinator));

        if (result.Succeeded)
            portal.ClearOneShotDestinationOverride(null);

        return result.Succeeded;
    }
}
