using System.Collections;
using CapstoneAudio;
using UnityEngine;
using UnityGAS;

public sealed class PlayerSpawner : MonoBehaviour
{
    // 이 클래스의 책임:
    // 씬 진입 시 플레이어 스폰 위치를 결정해 플레이어를 생성하고, 런타임 레지스트리와 후속 연출 시스템에 플레이어를 연결한다.

    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private PlayerSpawnPoint defaultSpawnPoint;
    [SerializeField] private PlayerSpawnPoint[] spawnPoints;
    [SerializeField, Min(0.1f)] private float dynamicEndpointWaitSeconds = 5f;

    private bool hasRequestedInitialHubHeal;

    private IEnumerator Start()
    {
        SceneTransitionContext transitionContext = RunSessionStore.PeekPendingTransition();
        bool waitsForDynamicEndpoint =
            transitionContext != null &&
            !string.IsNullOrWhiteSpace(transitionContext.destinationEndpointId);

        ISceneFadeTransitionHandle fadeService = null;
        if (waitsForDynamicEndpoint)
        {
            fadeService = SceneFadeTransitionPlayback.EnsureInstance(allowRuntimeFallback: true);
            fadeService?.SetPlayerUnlockBlocked(this, true);
        }

        SceneTravelEndpoint dynamicEndpoint = null;
        if (waitsForDynamicEndpoint)
        {
            float elapsed = 0f;
            while (elapsed < dynamicEndpointWaitSeconds &&
                   !SceneTravelEndpointRegistry.TryGetActiveScene(
                       transitionContext.destinationEndpointId,
                       out dynamicEndpoint))
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (dynamicEndpoint == null)
            {
                Debug.LogWarning(
                    $"[PlayerSpawner] Timed out waiting for dynamic endpoint '{transitionContext.destinationEndpointId}'. Falling back to PlayerSpawnPoint.",
                    this);
            }
        }

        GameObject player = SpawnIfNeeded(transitionContext, dynamicEndpoint);
        if (player != null && dynamicEndpoint != null)
        {
            SceneTravelTrigger2D arrivalTrigger =
                dynamicEndpoint.GetComponent<SceneTravelTrigger2D>();
            arrivalTrigger?.SuppressTravelUntilExit(player.transform);
            yield return PlayDynamicArrival(player, dynamicEndpoint, transitionContext.travelPresentationProfile);
        }

        fadeService?.SetPlayerUnlockBlocked(this, false);
    }

    public void SpawnIfNeeded()
    {
        SceneTransitionContext transitionContext = RunSessionStore.PeekPendingTransition();
        SceneTravelEndpoint dynamicEndpoint = null;
        if (transitionContext != null && !string.IsNullOrWhiteSpace(transitionContext.destinationEndpointId))
        {
            SceneTravelEndpointRegistry.TryGetActiveScene(
                transitionContext.destinationEndpointId,
                out dynamicEndpoint);
        }

        SpawnIfNeeded(transitionContext, dynamicEndpoint);
    }

    private GameObject SpawnIfNeeded(
        SceneTransitionContext transitionContext,
        SceneTravelEndpoint dynamicEndpoint)
    {
        if (playerPrefab == null)
        {
            Debug.LogError("[PlayerSpawner] playerPrefab is missing.");
            return null;
        }

        var playerTransform = PlayerRuntimeRegistry.GetPlayerTransform();
        var existingPlayer = playerTransform != null 
            ? playerTransform.gameObject 
            : GameObject.FindGameObjectWithTag("Player");

        if (existingPlayer != null)
        {
            RequestInitialHubSpawnFullHeal();
            ApplyDynamicEndpointTransform(existingPlayer, dynamicEndpoint);

            var existingInteractor = existingPlayer.GetComponent<PlayerInteractor2D>();
            if (existingInteractor != null)
                PlayerRuntimeRegistry.Register(existingInteractor);

            TryAttachGlobalVisionMask(existingPlayer);
            ApplyPendingHubReturnFullHeal(existingPlayer);
            ApplyPendingHubLoadFullHeal(existingPlayer);
            TryStartHubSpawnPresentation(existingPlayer);

            Debug.Log("[PlayerSpawner] Player already exists in the scene. Skipping spawn.");
            return existingPlayer;
        }

        var spawnPoint = ResolveSpawnPoint(transitionContext);
        if (dynamicEndpoint == null && spawnPoint == null)
        {
            Debug.LogError("[PlayerSpawner] Failed to resolve a player spawn point.");
            return null;
        }

        if (dynamicEndpoint == null)
            ApplySpawnRuntimePolicy(spawnPoint);

        Transform spawnTransform = dynamicEndpoint != null
            ? dynamicEndpoint.ArrivalAnchor
            : spawnPoint.transform;

        RequestInitialHubSpawnFullHeal();
        var player = Instantiate(
            playerPrefab,
            spawnTransform.position,
            spawnTransform.rotation);

        var playerInteractor = player.GetComponent<PlayerInteractor2D>();
        if (playerInteractor != null)
        {
            PlayerRuntimeRegistry.Register(playerInteractor);
        }
        else
        {
            Debug.LogWarning("[PlayerSpawner] PlayerInteractor2D was not found on the spawned player.");
        }

        TryAttachGlobalVisionMask(player);
        ApplyPendingHubReturnFullHeal(player);
        ApplyPendingHubLoadFullHeal(player);
        TryStartHubSpawnPresentation(player);
        return player;
    }

    // Direct Hub starts do not pass through the title's heal request.
    // Reuse that request so pending state restoration still finishes before healing.
    private void RequestInitialHubSpawnFullHeal()
    {
        if (hasRequestedInitialHubHeal || !RunSessionStore.IsAvailable || RunSessionStore.IsRunActive)
            return;

        if (!SceneDomainNamePolicy.IsHubSceneName(gameObject.scene.name))
            return;

        RunSessionStore.RequestPendingHubLoadFullHeal();
        hasRequestedInitialHubHeal = true;
    }

    private static void ApplyDynamicEndpointTransform(
        GameObject player,
        SceneTravelEndpoint dynamicEndpoint)
    {
        if (player == null || dynamicEndpoint == null)
            return;

        Transform anchor = dynamicEndpoint.ArrivalAnchor;
        player.transform.SetPositionAndRotation(anchor.position, anchor.rotation);
    }

    private static IEnumerator PlayDynamicArrival(
        GameObject player,
        SceneTravelEndpoint endpoint,
        SceneTravelPresentationProfileSO profile)
    {
        if (player == null || endpoint == null || profile == null ||
            profile.ArrivalMode == SceneTravelArrivalMode.None)
        {
            yield break;
        }

        Transform playerTransform = player.transform;
        Transform anchor = endpoint.ArrivalAnchor;
        Vector3 targetPosition = anchor.position;
        Quaternion targetRotation = anchor.rotation;
        Vector3 startPosition = anchor.TransformPoint(profile.ArrivalStartOffset);
        Quaternion startRotation = targetRotation *
                                   Quaternion.Euler(0f, 0f, profile.ArrivalRotationDegrees);

        playerTransform.SetPositionAndRotation(startPosition, startRotation);

        var presentationRuntime = new GameplayPresentationRuntime(player);
        GameplayCueParams cueParams = presentationRuntime.BuildParams(
            target: player,
            sourceObject: endpoint,
            explicitPosition: targetPosition,
            hasExplicitPosition: true,
            causer: endpoint.gameObject);
        presentationRuntime.Start(profile.ArrivalPresentation, cueParams);
        SoundPlaybackUtility.Play(
            profile.ArrivalSound,
            instigator: player,
            causer: endpoint.gameObject,
            target: player,
            position: targetPosition,
            sourceObject: endpoint);

        float duration = profile.ArrivalDuration;
        if (profile.ArrivalMode == SceneTravelArrivalMode.MoveFromOffset && duration > 0f)
        {
            float elapsed = 0f;
            while (elapsed < duration && player != null && endpoint != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                playerTransform.position = Vector3.LerpUnclamped(startPosition, targetPosition, t);
                playerTransform.rotation = Quaternion.SlerpUnclamped(startRotation, targetRotation, t);
                yield return null;
            }
        }

        if (player != null)
            playerTransform.SetPositionAndRotation(targetPosition, targetRotation);

        presentationRuntime.Stop(profile.ArrivalPresentation, cueParams, playRemove: true);
    }

    /// <summary>
    /// 책임 : 런 종료 후 Hub에 돌아온 플레이어의 상태형 체력을 최대치로 회복한다.
    /// Attribute 초기화/복원 정책이 아니라 Hub 복귀 1회성 정리 정책으로만 동작한다.
    /// </summary>
    private static void ApplyPendingHubReturnFullHeal(GameObject player)
    {
        if (player == null)
            return;

        if (!SceneDomainNamePolicy.IsHubSceneName(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name))
            return;

        if (!RunSessionStore.ConsumePendingHubReturnFullHeal())
            return;

        PlayerHealthRestoreUtility.FillLinkedHealthToMax(player, player);
    }

    /// <summary>
    /// 책임 : 타이틀에서 세이브 프로필을 통해 Hub에 진입한 플레이어를 저장 복원/업그레이드 재적용 이후 최대 체력까지 회복한다.
    /// </summary>
    private static void ApplyPendingHubLoadFullHeal(GameObject player)
    {
        if (player == null)
            return;

        if (!SceneDomainNamePolicy.IsHubSceneName(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name))
            return;

        if (RunSessionStore.PeekPendingPlayerState() != null || !RunSessionStore.ConsumePendingHubLoadFullHeal())
            return;

        PlayerHealthRestoreUtility.FillLinkedHealthToMax(player, player);
    }

    private static void TryAttachGlobalVisionMask(GameObject player)
    {
        if (player == null)
            return;

        GlobalVisionMaskController visionMaskController = GlobalVisionMaskController.Instance;
        if (visionMaskController == null)
            visionMaskController = FindFirstObjectByType<GlobalVisionMaskController>();

        if (visionMaskController == null)
            return;

        visionMaskController.AttachToPlayer(player.transform);
    }

    private static void TryStartHubSpawnPresentation(GameObject player)
    {
        if (player == null)
            return;

        var presentation = player.GetComponent<PlayerHubSpawnPresentation2D>();
        if (presentation == null)
            return;

        presentation.TryPlayIfEligible();
    }

    private PlayerSpawnPoint ResolveSpawnPoint(SceneTransitionContext context)
    {
        if (context != null && !string.IsNullOrEmpty(context.entryPointId) && spawnPoints != null)
        {
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                var point = spawnPoints[i];
                if (point != null && point.pointId == context.entryPointId)
                    return point;
            }
        }

        if (defaultSpawnPoint != null)
            return defaultSpawnPoint;

        if (spawnPoints != null)
        {
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                var point = spawnPoints[i];
                if (point != null && point.isDefault)
                    return point;
            }

            if (spawnPoints.Length > 0)
                return spawnPoints[0];
        }

        return null;
    }

    private static void ApplySpawnRuntimePolicy(PlayerSpawnPoint spawnPoint)
    {
        if (spawnPoint == null)
            return;

        if (spawnPoint.runtimePolicy != PlayerSpawnRuntimePolicy.ResetToSceneDefault)
            return;

        RunSessionStore.ClearPendingPlayerState();
    }
}
