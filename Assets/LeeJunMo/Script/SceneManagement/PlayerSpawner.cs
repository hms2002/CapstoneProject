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
            Debug.LogError("[PlayerSpawner] playerPrefab이 비어 있다.");
            return;
        }

        var existingPlayer = GameObject.FindGameObjectWithTag("Player");
        if (existingPlayer != null)
        {
            var existingInteractor = existingPlayer.GetComponent<SampleTopDownPlayer>();
            if (existingInteractor != null)
                PlayerRuntimeRegistry.Register(existingInteractor);

            Debug.Log("[PlayerSpawner] 이미 Player가 씬에 존재해서 스폰을 건너뜀.");
            return;
        }

        var ctx = GamePlayDataManager.Instance != null
            ? GamePlayDataManager.Instance.PeekPendingTransition()
            : null;

        var spawnPoint = ResolveSpawnPoint(ctx);

        if (spawnPoint == null)
        {
            Debug.LogError("[PlayerSpawner] 스폰 포인트를 찾지 못했다.");
            return;
        }

        var player = Instantiate(playerPrefab, spawnPoint.transform.position, spawnPoint.transform.rotation);

        var playerInteractor = player.GetComponent<SampleTopDownPlayer>();
        if (playerInteractor != null)
            PlayerRuntimeRegistry.Register(playerInteractor);
        else
            Debug.LogWarning("[PlayerSpawner] SampleTopDownPlayer를 찾지 못해 레지스트리 등록을 건너뜀.");

        // 지금 단계에서는 위치 스폰만 함.
        // 나중에 PlayerRuntimeState 복원/바인딩이 모두 끝난 뒤에도 같은 지점에서 등록을 유지하면 된다.
    }

    private PlayerSpawnPoint ResolveSpawnPoint(SceneTransitionContext ctx)
    {
        if (ctx != null && !string.IsNullOrEmpty(ctx.entryPointId))
        {
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                var point = spawnPoints[i];
                if (point != null && point.pointId == ctx.entryPointId)
                    return point;
            }
        }

        if (defaultSpawnPoint != null)
            return defaultSpawnPoint;

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (spawnPoints[i] != null && spawnPoints[i].isDefault)
                return spawnPoints[i];
        }

        return spawnPoints != null && spawnPoints.Length > 0 ? spawnPoints[0] : null;
    }
}