using UnityEngine.SceneManagement;

internal readonly struct ScenePortalTravelRequest
{
    public readonly ScenePortal Portal;
    public readonly PortalRouteManager RouteManager;

    public ScenePortalTravelRequest(
        ScenePortal portal,
        PortalRouteManager routeManager)
    {
        Portal = portal;
        RouteManager = routeManager;
    }

    public static ScenePortalTravelRequest Create(ScenePortal portal)
    {
        return new ScenePortalTravelRequest(
            portal,
            PortalRouteManager.EnsureInstance());
    }
}

internal readonly struct ScenePortalTravelPlan
{
    public readonly ScenePortal Portal;
    public readonly PortalRouteDecision Route;
    public readonly RunTransitionDirective RunDirective;
    public readonly SceneTransitionPolicy TransitionPolicy;
    public readonly SceneTransitionContext Context;

    public bool IsValid => Portal != null && Route.IsValid && Context != null;
    public ScenePortalTravelPlan(
        ScenePortal portal,
        PortalRouteDecision route,
        RunTransitionDirective runDirective,
        SceneTransitionPolicy transitionPolicy,
        SceneTransitionContext context)
    {
        Portal = portal;
        Route = route;
        RunDirective = runDirective;
        TransitionPolicy = transitionPolicy;
        Context = context;
    }
}

internal static class ScenePortalTravelPlanner
{
    public static PortalRouteDecision ResolveRoute(ScenePortalTravelRequest request)
    {
        if (request.Portal != null &&
            request.Portal.TryGetOneShotDestinationOverride(out string overrideSceneName))
        {
            return new PortalRouteDecision(overrideSceneName, "Default", TransitionType.CorridorToBoss);
        }

        if (request.RouteManager == null)
            return default;

        return request.RouteManager.TryResolveRoute(request.Portal, out PortalRouteDecision route)
            ? route
            : default;
    }

    public static RunTransitionDirective ResolveRunTransition(PortalRouteDecision route)
    {
        if (route.TransitionType == TransitionType.HubToRunStart &&
            RunSessionStore.IsRunActive)
        {
            return default;
        }

        if (RunTransitionResolver.Instance != null)
        {
            RunTransitionDirective resolved = RunTransitionResolver.Instance.Resolve(route);
            if (resolved.action != RunTransitionAction.None)
                return resolved;
        }

        return ResolveDefaultRunTransition(route);
    }

    public static bool ShouldCapturePlayerRuntimeState(RunTransitionDirective runDirective)
    {
        return !runDirective.ShouldEndRun;
    }

    public static ScenePortalTravelPlan CreatePlan(
        ScenePortalTravelRequest request,
        PortalRouteDecision route,
        RunTransitionDirective runDirective)
    {
        if (request.Portal == null || !route.IsValid)
            return default;

        SceneTransitionPolicy policy = ResolveTransitionPolicy(route);
        SceneTransitionContext context = CreateTransitionContext(request.Portal, route, policy);
        return new ScenePortalTravelPlan(request.Portal, route, runDirective, policy, context);
    }

    private static SceneTransitionPolicy ResolveTransitionPolicy(PortalRouteDecision route)
    {
        if (route.TransitionType == TransitionType.HubToRunStart &&
            RunSessionStore.IsRunActive)
        {
            return default;
        }

        if (SceneTransitionPolicyResolver.Instance == null)
            return default;

        return SceneTransitionPolicyResolver.Instance.Resolve(route);
    }

    private static SceneTransitionContext CreateTransitionContext(
        ScenePortal portal,
        PortalRouteDecision route,
        SceneTransitionPolicy policy)
    {
        var context = new SceneTransitionContext
        {
            fromScene = portal.gameObject.scene.IsValid()
                ? portal.gameObject.scene.name
                : SceneManager.GetActiveScene().name,
            toScene = route.TargetSceneName,
            exitPointId = portal.PortalId,
            entryPointId = route.EntryPointId,
            destinationEndpointId = IsDynamicEntryPoint(route.EntryPointId)
                ? route.EntryPointId
                : null,
            transitionType = route.TransitionType
        };

        policy.ApplyTo(context);
        return context;
    }

    private static RunTransitionDirective ResolveDefaultRunTransition(PortalRouteDecision route)
    {
        if (!route.IsValid)
            return default;

        switch (route.TransitionType)
        {
            case TransitionType.HubToRunStart:
                return new RunTransitionDirective(
                    RunTransitionAction.StartRun,
                    RunEndReason.None);

            case TransitionType.ReturnToHubAfterRun:
                return new RunTransitionDirective(
                    RunTransitionAction.EndRun,
                    RunEndReason.Victory);

            default:
                return default;
        }
    }

    private static bool IsDynamicEntryPoint(string entryPointId)
    {
        return !string.IsNullOrWhiteSpace(entryPointId) &&
               !string.Equals(entryPointId, "Default", System.StringComparison.Ordinal);
    }
}
