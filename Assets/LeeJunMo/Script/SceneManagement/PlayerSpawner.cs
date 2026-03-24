using UnityEngine;

public sealed class PlayerSpawner : MonoBehaviour
{
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

        var existingPlayer = GameObject.FindGameObjectWithTag("Player");
        if (existingPlayer != null)
        {
            var existingInteractor = existingPlayer.GetComponent<SampleTopDownPlayer>();
            if (existingInteractor != null)
                PlayerRuntimeRegistry.Register(existingInteractor);

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

        var playerInteractor = player.GetComponent<SampleTopDownPlayer>();
        if (playerInteractor != null)
        {
            PlayerRuntimeRegistry.Register(playerInteractor);
        }
        else
        {
            Debug.LogWarning("[PlayerSpawner] SampleTopDownPlayer was not found on the spawned player.");
        }
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
