using UnityEngine;
using UnityGAS;

internal readonly struct ScenePortalTravelExecutionRequest
{
    public readonly ScenePortalTravelRequest TravelRequest;
    public readonly PortalRouteDecision Route;
    public readonly RunTransitionDirective RunDirective;
    public readonly GamePlayDataManager Gameplay;
    public readonly SceneTransitionCoordinator TransitionCoordinator;

    public ScenePortalTravelExecutionRequest(
        ScenePortalTravelRequest travelRequest,
        PortalRouteDecision route,
        RunTransitionDirective runDirective,
        GamePlayDataManager gameplay,
        SceneTransitionCoordinator transitionCoordinator)
    {
        TravelRequest = travelRequest;
        Route = route;
        RunDirective = runDirective;
        Gameplay = gameplay;
        TransitionCoordinator = transitionCoordinator;
    }
}

internal readonly struct ScenePortalTravelExecutionResult
{
    public readonly bool Succeeded;
    public readonly ScenePortalTravelPlan Plan;

    public ScenePortalTravelExecutionResult(bool succeeded, ScenePortalTravelPlan plan)
    {
        Succeeded = succeeded;
        Plan = plan;
    }
}

internal static class ScenePortalTravelExecutor
{
    public static ScenePortalTravelExecutionResult Execute(ScenePortalTravelExecutionRequest request)
    {
        if (request.Gameplay == null || request.TransitionCoordinator == null)
            return new ScenePortalTravelExecutionResult(false, default);

        if (ScenePortalTravelPlanner.ShouldCapturePlayerRuntimeState(request.RunDirective))
        {
            ScenePortalPlayerRuntimeCaptureService.CaptureAndStore(
                new ScenePortalPlayerRuntimeCaptureRequest(
                    request.TravelRequest.Portal,
                    request.Gameplay));
        }

        if (request.RunDirective.ShouldStartRun)
            request.Gameplay.StartRun();

        if (request.RunDirective.ShouldEndRun)
            request.Gameplay.EndRun(request.RunDirective.endReason);

        ScenePortalTravelPlan plan = ScenePortalTravelPlanner.CreatePlan(
            request.TravelRequest,
            request.Route,
            request.RunDirective);

        Debug.Log(
            $"[ScenePortalTravelService] PrepareTransition from={plan.Context.fromScene} to={plan.Context.toScene}, entry={plan.Context.entryPointId}, exit={plan.Context.exitPointId}, type={plan.Context.transitionType}, run={plan.RunDirective}, policy={plan.TransitionPolicy}",
            request.TravelRequest.Portal);

        request.Gameplay.PrepareTransition(plan.Context);

        PortalRouteManager notifyRouteManager = PortalRouteManager.EnsureInstance();
        if (notifyRouteManager != null)
            notifyRouteManager.NotifyTransitionConsumed(plan.Route.TransitionType);

        bool loaded = request.TransitionCoordinator.TryLoadScene(plan.Route.TargetSceneName);
        return new ScenePortalTravelExecutionResult(loaded, plan);
    }
}

internal readonly struct ScenePortalPlayerRuntimeCaptureRequest
{
    public readonly ScenePortal Portal;
    public readonly GamePlayDataManager Gameplay;

    public ScenePortalPlayerRuntimeCaptureRequest(ScenePortal portal, GamePlayDataManager gameplay)
    {
        Portal = portal;
        Gameplay = gameplay;
    }
}

internal static class ScenePortalPlayerRuntimeCaptureService
{
    public static void CaptureAndStore(ScenePortalPlayerRuntimeCaptureRequest request)
    {
        if (request.Portal == null || request.Gameplay == null)
            return;

        var playerTransform = PlayerRuntimeRegistry.GetPlayerTransform();
        var playerGo = playerTransform != null
            ? playerTransform.gameObject
            : GameObject.FindGameObjectWithTag("Player");

        if (playerGo == null)
        {
            Debug.LogWarning($"[ScenePortalTravelService] Failed to find Player for portal {request.Portal.name}", request.Portal);
            return;
        }

        CleanupBeforeCapture(request.Portal, playerGo);

        var captureBridge = playerGo.GetComponent<PlayerRuntimeCaptureBridge>();
        if (captureBridge == null)
        {
            Debug.LogWarning($"[ScenePortalTravelService] PlayerRuntimeCaptureBridge missing. portal={request.Portal.name}", playerGo);
            return;
        }

        var state = captureBridge.CaptureRuntimeState();
        request.Gameplay.PreparePlayerState(state);
    }

    private static void CleanupBeforeCapture(ScenePortal portal, GameObject playerGo)
    {
        if (playerGo == null)
            return;

        var abilitySystem = playerGo.GetComponent<AbilitySystem>();
        if (abilitySystem != null)
            abilitySystem.CancelAllForSceneTransition(portal.SceneTravelCleanupTagSets);
    }
}
