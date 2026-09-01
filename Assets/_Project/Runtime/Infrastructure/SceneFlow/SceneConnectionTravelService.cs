using System.Collections;
using CapstoneAudio;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityGAS;

/// <summary>
/// 책임 : Gameplay의 데이터 기반 endpoint 이동 요청을 gate 검사, 런 상태, 플레이어 캡처, 연출, 씬 전환 순서로 실행한다.
/// </summary>
public static class SceneConnectionTravelService
{
    private static readonly ISceneTravelBackend PlaybackBackend = new SceneConnectionTravelBackend();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterPlaybackBackend()
    {
        SceneTravelPlayback.RegisterBackend(PlaybackBackend);
    }

    /// <summary>
    /// 책임 : Gameplay playback 계약을 Infrastructure 이동 실행기로 연결한다.
    /// </summary>
    private sealed class SceneConnectionTravelBackend : ISceneTravelBackend
    {
        public bool TryTravel(
            SceneTravelEndpoint endpoint,
            IPlayerInteractor player,
            SceneTravelActivationKind activationKind)
        {
            return SceneConnectionTravelCoordinator.TryTravel(endpoint, player, activationKind);
        }
    }
}

/// <summary>
/// 책임 : 연결 해석과 선행 조건 검사를 완료한 요청만 씬 전환 coroutine에 넘긴다.
/// </summary>
internal static class SceneConnectionTravelCoordinator
{
    public static bool TryTravel(
        SceneTravelEndpoint endpoint,
        IPlayerInteractor player,
        SceneTravelActivationKind activationKind)
    {
        if (endpoint == null || player?.Transform == null)
            return false;

        SceneTransitionCoordinator transitionCoordinator = SceneTransitionCoordinator.EnsureInstance();
        if (transitionCoordinator == null || transitionCoordinator.IsTransitionActive)
            return false;

        if (!endpoint.TryResolveDirection(out ResolvedSceneTravelDirection resolved))
        {
            WarningPopupPlayback.ShowMessage("이동 경로를 사용할 수 없습니다.");
            Debug.LogError(
                $"[SceneConnectionTravelService] Endpoint '{endpoint.EndpointId}' has no valid direction for scene '{endpoint.gameObject.scene.name}'.",
                endpoint);
            return false;
        }

        if (!SceneTravelGateEvaluator.CanTravel(
                resolved.Direction,
                out WarningPopupCode warning,
                out SceneTravelGateKind failedGateKind))
        {
            if (warning != WarningPopupCode.None)
                WarningPopupPlayback.Show(warning);
            else if (failedGateKind == SceneTravelGateKind.BossNotDefeatedThisRun)
                WarningPopupPlayback.Show(WarningPopupCode.BossAlreadyDefeatedThisRun);
            else
                WarningPopupPlayback.ShowMessage("아직 이용할 수 없습니다.");
            return false;
        }

        if (!endpoint.TryReserveTravel())
            return false;

        player.SetInteractState(InteractState.None);
        transitionCoordinator.StartCoroutine(SceneConnectionTravelExecutor.Execute(
            endpoint,
            player,
            activationKind,
            resolved,
            transitionCoordinator));
        return true;
    }
}

/// <summary>
/// 책임 : 방향에 선언된 모든 gate를 현재 런 진행 상태와 비교하고 첫 실패 경고를 반환한다.
/// </summary>
internal static class SceneTravelGateEvaluator
{
    public static bool CanTravel(
        SceneTravelDirectionData direction,
        out WarningPopupCode failureWarning,
        out SceneTravelGateKind failedGateKind)
    {
        failureWarning = WarningPopupCode.None;
        failedGateKind = SceneTravelGateKind.None;
        if (direction.Gates == null)
            return true;

        for (int i = 0; i < direction.Gates.Count; i++)
        {
            SceneTravelGateData gate = direction.Gates[i];
            if (!gate.IsConfigured)
                continue;

            bool defeated = RunProgressPlayback.IsBossDefeatedThisRun(gate.SubjectId) ||
                            RunSessionStateService.HasDefeatedBoss(RunSessionStore.Data, gate.SubjectId);
            bool passed = gate.Kind switch
            {
                SceneTravelGateKind.BossNotDefeatedThisRun => !defeated,
                SceneTravelGateKind.BossDefeatedThisRun => defeated,
                _ => true
            };

            if (passed)
                continue;

            failureWarning = gate.FailureWarning;
            failedGateKind = gate.Kind;
            return false;
        }

        return true;
    }
}

/// <summary>
/// 책임 : 승인된 이동의 출발 연출, 플레이어 상태 캡처, 런 수명 변경, transition context 준비와 씬 로드를 순서대로 수행한다.
/// </summary>
internal static class SceneConnectionTravelExecutor
{
    public static IEnumerator Execute(
        SceneTravelEndpoint endpoint,
        IPlayerInteractor player,
        SceneTravelActivationKind activationKind,
        ResolvedSceneTravelDirection resolved,
        SceneTransitionCoordinator transitionCoordinator)
    {
        if (endpoint == null || player?.Transform == null || transitionCoordinator == null)
            yield break;

        Transform playerTransform = player.Transform;
        Vector3 originalPosition = playerTransform.position;
        Quaternion originalRotation = playerTransform.rotation;
        SceneTravelPresentationProfileSO profile = resolved.Direction.PresentationProfile;

        yield return PlayDeparture(endpoint, playerTransform, profile);

        if (endpoint == null || playerTransform == null)
            yield break;

        DungeonGenerator activeDungeon = Object.FindAnyObjectByType<DungeonGenerator>();
        activeDungeon?.CaptureStateBeforeSceneExit();

        if (resolved.Direction.PreservePlayerRuntimeState)
            CapturePlayerRuntimeState(endpoint, playerTransform.gameObject);
        else
            RunSessionStore.ClearPendingPlayerState();

        ApplyRunAction(resolved.Direction);

        SceneTransitionContext context = CreateTransitionContext(endpoint, resolved, profile);
        RunSessionStore.PrepareTransition(context);
        ActivateDestinationRouteContext(resolved);

        bool accepted = profile != null
            ? transitionCoordinator.TryLoadScene(
                resolved.Destination.SceneName,
                profile.TransitionVisualMode,
                profile.CoverDuration,
                profile.RevealDuration)
            : transitionCoordinator.TryLoadScene(resolved.Destination.SceneName);

        if (accepted)
        {
            Debug.Log(
                $"[SceneConnectionTravelService] Travel accepted. connection={endpoint.Connection.ConnectionId}, " +
                $"from={resolved.Source.SceneName}:{resolved.Source.EndpointId}, " +
                $"to={resolved.Destination.SceneName}:{resolved.Destination.EndpointId}, activation={activationKind}",
                endpoint);
            yield break;
        }

        RunSessionStore.ConsumePendingTransition();
        endpoint.ReleaseTravelReservation();
        playerTransform.SetPositionAndRotation(originalPosition, originalRotation);
        player.SetInteractState(InteractState.Idle);
        WarningPopupPlayback.ShowMessage("지금은 이동할 수 없습니다.");
    }

    /// <summary>
    /// 책임 : 승인된 SceneConnection 이동의 목적지 경로 문맥을 기존 PortalRouteManager에 반영해 씬 로드 콜백 전에 BGM·지역명 해석을 준비한다.
    /// </summary>
    private static void ActivateDestinationRouteContext(ResolvedSceneTravelDirection resolved)
    {
        SceneRouteContextSO routeContext = resolved.Destination.RouteContext;
        if (routeContext == null)
            return;

        PortalRouteManager routeManager = PortalRouteManager.EnsureInstance();
        if (routeManager != null &&
            routeManager.ActivateSceneConnectionRouteContext(
                routeContext,
                resolved.Destination.SceneName))
        {
            return;
        }

        Debug.LogError(
            $"[SceneConnectionTravelService] Destination route context is invalid. " +
            $"scene={resolved.Destination.SceneName}, context={routeContext.name}",
            routeContext);
    }

    private static IEnumerator PlayDeparture(
        SceneTravelEndpoint endpoint,
        Transform playerTransform,
        SceneTravelPresentationProfileSO profile)
    {
        if (endpoint == null || playerTransform == null || profile == null ||
            profile.DepartureMode == SceneTravelDepartureMode.None)
        {
            yield break;
        }

        GameObject playerObject = playerTransform.gameObject;
        var presentationRuntime = new GameplayPresentationRuntime(playerObject);
        GameplayCueParams cueParams = presentationRuntime.BuildParams(
            target: playerObject,
            sourceObject: endpoint,
            explicitPosition: endpoint.DepartureAnchor.position,
            hasExplicitPosition: true,
            causer: endpoint.gameObject);
        presentationRuntime.Start(profile.DeparturePresentation, cueParams);
        SoundPlaybackUtility.Play(
            profile.DepartureSound,
            instigator: playerObject,
            causer: endpoint.gameObject,
            target: playerObject,
            position: endpoint.DepartureAnchor.position,
            sourceObject: endpoint);

        Vector3 startPosition = playerTransform.position;
        Quaternion startRotation = playerTransform.rotation;
        Vector3 targetPosition = endpoint.DepartureAnchor.TransformPoint(profile.DepartureTargetOffset);
        float duration = profile.DepartureDuration;

        if (profile.DepartureMode == SceneTravelDepartureMode.PullIntoEndpoint && duration > 0f)
        {
            float elapsed = 0f;
            while (elapsed < duration && endpoint != null && playerTransform != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                playerTransform.position = Vector3.LerpUnclamped(startPosition, targetPosition, t);
                playerTransform.rotation = startRotation *
                                           Quaternion.Euler(0f, 0f, profile.DepartureRotationDegrees * t);
                yield return null;
            }
        }

        if (playerTransform != null)
        {
            playerTransform.position = targetPosition;
            playerTransform.rotation = startRotation *
                                       Quaternion.Euler(0f, 0f, profile.DepartureRotationDegrees);
        }

        presentationRuntime.Stop(profile.DeparturePresentation, cueParams, playRemove: true);
    }

    private static void CapturePlayerRuntimeState(SceneTravelEndpoint endpoint, GameObject playerObject)
    {
        if (endpoint == null || playerObject == null)
            return;

        AbilitySystem abilitySystem = playerObject.GetComponent<AbilitySystem>();
        abilitySystem?.CancelAllForSceneTransition(endpoint.SceneTravelCleanupTagSets);

        PlayerRuntimeCaptureBridge captureBridge = playerObject.GetComponent<PlayerRuntimeCaptureBridge>();
        if (captureBridge == null)
        {
            Debug.LogWarning(
                "[SceneConnectionTravelService] PlayerRuntimeCaptureBridge is missing; continuing without player state preservation.",
                playerObject);
            return;
        }

        RunSessionStore.PreparePlayerState(captureBridge.CaptureRuntimeState());
    }

    private static void ApplyRunAction(SceneTravelDirectionData direction)
    {
        switch (direction.RunAction)
        {
            case SceneTravelRunAction.StartRun:
                if (!RunSessionStore.IsRunActive)
                    RunSessionStore.StartRun();
                break;
            case SceneTravelRunAction.EndRun:
                RunSessionStore.EndRun(direction.RunEndReason);
                break;
        }
    }

    private static SceneTransitionContext CreateTransitionContext(
        SceneTravelEndpoint endpoint,
        ResolvedSceneTravelDirection resolved,
        SceneTravelPresentationProfileSO profile)
    {
        return new SceneTransitionContext
        {
            fromScene = SceneManager.GetActiveScene().name,
            toScene = resolved.Destination.SceneName,
            exitPointId = resolved.Source.EndpointId,
            entryPointId = resolved.Destination.EndpointId,
            connectionId = endpoint.Connection.ConnectionId,
            sourceEndpointId = resolved.Source.EndpointId,
            destinationEndpointId = resolved.Destination.EndpointId,
            travelPresentationProfile = profile,
            transitionType = TransitionType.None,
            fullyHealPlayer = resolved.Direction.FullyHealPlayer,
            resetCooldowns = resolved.Direction.ResetCooldowns,
            clearAllEffects = resolved.Direction.ClearAllEffects,
            clearCombatOnlyEffects = resolved.Direction.ClearCombatOnlyEffects
        };
    }
}
