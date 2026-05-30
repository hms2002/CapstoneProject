using UnityEngine;

public static class ScenePortalTravelService
{
    public static bool TryTravel(ScenePortal portal)
    {
        return ScenePortalTravelCoordinator.TryTravel(portal);
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

        return result.Succeeded;
    }
}
