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

        if (GameObject.FindGameObjectWithTag("Player") != null)
        {
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

        Instantiate(playerPrefab, spawnPoint.transform.position, spawnPoint.transform.rotation);

        // 지금 단계에서는 위치 스폰만 함.
        // 나중에 PlayerRuntimeState 복원 붙일 때 여기서 이어가면 된다.
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