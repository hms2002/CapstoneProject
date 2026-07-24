using UnityEngine;

public sealed class PlayerSpawner : MonoBehaviour
{
    // 이 클래스의 책임:
    // 씬 진입 시 플레이어 스폰 위치를 결정해 플레이어를 생성하고, 런타임 레지스트리와 후속 연출 시스템에 플레이어를 연결한다.

    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private PlayerSpawnPoint defaultSpawnPoint;
    [SerializeField] private PlayerSpawnPoint[] spawnPoints;

    private void Start()
    {
        SpawnIfNeeded();
    }

    public void SpawnIfNeeded()
    {
        if (playerPrefab == null)
        {
            Debug.LogError("[PlayerSpawner] playerPrefab is missing.");
            return;
        }

        var playerTransform = PlayerRuntimeRegistry.GetPlayerTransform();
        var existingPlayer = playerTransform != null 
            ? playerTransform.gameObject 
            : GameObject.FindGameObjectWithTag("Player");

        if (existingPlayer != null)
        {
            var existingInteractor = existingPlayer.GetComponent<PlayerInteractor2D>();
            if (existingInteractor != null)
                PlayerRuntimeRegistry.Register(existingInteractor);

            TryAttachGlobalVisionMask(existingPlayer);
            ApplyPendingHubReturnFullHeal(existingPlayer);
            ApplyPendingHubLoadFullHeal(existingPlayer);
            TryStartHubSpawnPresentation(existingPlayer);

            Debug.Log("[PlayerSpawner] Player already exists in the scene. Skipping spawn.");
            return;
        }

        var gameplay = GamePlayDataManager.Instance;
        var transitionContext = gameplay != null
            ? gameplay.PeekPendingTransition()
            : null;

        var spawnPoint = ResolveSpawnPoint(transitionContext);
        if (spawnPoint == null)
        {
            Debug.LogError("[PlayerSpawner] Failed to resolve a player spawn point.");
            return;
        }

        ApplySpawnRuntimePolicy(spawnPoint, gameplay);

        var player = Instantiate(
            playerPrefab,
            spawnPoint.transform.position,
            spawnPoint.transform.rotation);

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
    }

    /// <summary>
    /// 책임 : 런 종료 후 Hub에 돌아온 플레이어의 상태형 체력을 최대치로 회복한다.
    /// Attribute 초기화/복원 정책이 아니라 Hub 복귀 1회성 정리 정책으로만 동작한다.
    /// </summary>
    private static void ApplyPendingHubReturnFullHeal(GameObject player)
    {
        if (player == null)
            return;

        if (!SceneDomainScenePolicy.IsHubSceneName(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name))
            return;

        GamePlayDataManager gameplay = GamePlayDataManager.Instance;
        if (gameplay == null || !gameplay.ConsumePendingHubReturnFullHeal())
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

        if (!SceneDomainScenePolicy.IsHubSceneName(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name))
            return;

        GamePlayDataManager gameplay = GamePlayDataManager.Instance;
        if (gameplay == null || gameplay.PeekPendingPlayerState() != null || !gameplay.ConsumePendingHubLoadFullHeal())
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

    private static void ApplySpawnRuntimePolicy(
        PlayerSpawnPoint spawnPoint,
        GamePlayDataManager gameplay)
    {
        if (spawnPoint == null || gameplay == null)
            return;

        if (spawnPoint.runtimePolicy != PlayerSpawnRuntimePolicy.ResetToSceneDefault)
            return;

        gameplay.ClearPendingPlayerState();
    }
}
