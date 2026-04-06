using UnityEngine;
using UnityEngine.SceneManagement;
using UnityGAS;

public static class ScenePortalTravelService
{
    public static bool TryTravel(ScenePortal portal)
    {
        if (portal == null)
            return false;

        var route = ResolveRoute(portal);
        if (!route.IsValid)
        {
            Debug.LogError($"[ScenePortalTravelService] Invalid route for portal {portal.name}", portal);
            return false;
        }

        var gameplay = GamePlayDataManager.Instance;
        if (gameplay == null)
        {
            Debug.LogError($"[ScenePortalTravelService] GamePlayDataManager is null. portal={portal.name}", portal);
            return false;
        }

        var runDirective = ResolveRunTransition(route);
        if (!runDirective.ShouldEndRun)
            CaptureAndStorePlayerRuntimeState(portal, gameplay);

        if (runDirective.ShouldStartRun)
            gameplay.StartRun();

        if (runDirective.ShouldEndRun)
            gameplay.EndRun(runDirective.endReason);

        var policy = ResolveTransitionPolicy(route);
        var context = CreateTransitionContext(portal, route, policy);

        Debug.Log(
            $"[ScenePortalTravelService] PrepareTransition from={context.fromScene} to={context.toScene}, entry={context.entryPointId}, exit={context.exitPointId}, type={context.transitionType}, run={runDirective}, policy={policy}",
            portal);

        gameplay.PrepareTransition(context);

        if (PortalRouteManager.Instance != null)
            PortalRouteManager.Instance.NotifyTransitionConsumed(route.TransitionType);

        SceneManager.LoadScene(route.TargetSceneName);
        return true;
    }

    private static PortalRouteDecision ResolveRoute(ScenePortal portal)
    {
        if (PortalRouteManager.Instance == null)
            return default;

        return PortalRouteManager.Instance.TryResolveRoute(portal, out var route)
            ? route
            : default;
    }

    private static RunTransitionDirective ResolveRunTransition(PortalRouteDecision route)
    {
        if (RunTransitionResolver.Instance != null)
        {
            var resolved = RunTransitionResolver.Instance.Resolve(route);
            if (resolved.action != RunTransitionAction.None)
                return resolved;
        }

        return ResolveDefaultRunTransition(route);
    }

    private static SceneTransitionPolicy ResolveTransitionPolicy(PortalRouteDecision route)
    {
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
            transitionType = route.TransitionType
        };

        policy.ApplyTo(context);
        return context;
    }

    private static void CaptureAndStorePlayerRuntimeState(ScenePortal portal, GamePlayDataManager gameplay)
    {
        var playerTransform = PlayerRuntimeRegistry.GetPlayerTransform();
        var playerGo = playerTransform != null 
            ? playerTransform.gameObject 
            : GameObject.FindGameObjectWithTag("Player");
            
        if (playerGo == null)
        {
            Debug.LogWarning($"[ScenePortalTravelService] Failed to find Player for portal {portal.name}", portal);
            return;
        }

        CleanupBeforeCapture(portal, playerGo);

        var captureBridge = playerGo.GetComponent<PlayerRuntimeCaptureBridge>();
        if (captureBridge == null)
        {
            Debug.LogWarning($"[ScenePortalTravelService] PlayerRuntimeCaptureBridge missing. portal={portal.name}", playerGo);
            return;
        }

        var state = captureBridge.CaptureRuntimeState();
        gameplay.PreparePlayerState(state);
    }

    private static void CleanupBeforeCapture(ScenePortal portal, GameObject playerGo)
    {
        if (playerGo == null)
            return;

        var abilitySystem = playerGo.GetComponent<AbilitySystem>();
        if (abilitySystem != null)
            abilitySystem.CancelAllForSceneTransition(portal.SceneTravelCleanupTagSets);
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
}
