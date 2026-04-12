using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임:
/// - 하나의 방에 속한 MonsterSpawnContainer들을 묶어 관리한다.
/// - 방 전용 스폰 프로파일에서 무작위로 스폰 테이블 하나를 선택해 스폰 요청 목록을 생성한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class MonsterSpawnRoomGroup : MonoBehaviour
{
    [SerializeField] private MonsterRoomSpawnProfileSO spawnProfile;
    [SerializeField] private bool autoCollectChildContainers = true;
    [SerializeField] private List<MonsterSpawnContainer> spawnContainers = new();

    private readonly List<MonsterSpawnContainer> reusableContainers = new();
    private readonly List<GameObject> reusableSpawnPlan = new();

    public MonsterRoomSpawnProfileSO SpawnProfile => spawnProfile;

    /// <summary>현재 방 그룹이 관리하는 스폰 포인트들을 반환합니다.</summary>
    public IReadOnlyList<MonsterSpawnContainer> GetSpawnContainers()
    {
        RefreshContainersIfNeeded();
        return reusableContainers;
    }

    /// <summary>방 프로파일 기준으로 실제 스폰 요청 목록을 채웁니다.</summary>
    public void BuildSpawnRequests(List<MonsterSpawnRequest> requests)
    {
        if (requests == null || spawnProfile == null)
            return;

        if (!spawnProfile.TryGetRandomSpawnTable(out MonsterRoomSpawnProfileSO.SpawnTable table))
            return;

        RefreshContainersIfNeeded();
        if (reusableContainers.Count == 0)
            return;

        if (!table.TryBuildSpawnPlan(reusableSpawnPlan) || reusableSpawnPlan.Count == 0)
            return;

        List<MonsterSpawnContainer> candidates = new List<MonsterSpawnContainer>(reusableContainers);
        Shuffle(candidates);

        int spawnCount = Mathf.Min(reusableSpawnPlan.Count, candidates.Count);
        for (int i = 0; i < spawnCount; i++)
        {
            GameObject monsterPrefab = reusableSpawnPlan[i];
            if (monsterPrefab == null)
                continue;

            requests.Add(candidates[i].CreateRequest(monsterPrefab));
        }
    }

    /// <summary>자식 스폰 포인트 자동 수집 설정에 맞춰 캐시를 갱신합니다.</summary>
    private void RefreshContainersIfNeeded()
    {
        reusableContainers.Clear();

        if (autoCollectChildContainers)
        {
            MonsterSpawnContainer[] children = GetComponentsInChildren<MonsterSpawnContainer>(includeInactive: false);
            for (int i = 0; i < children.Length; i++)
            {
                MonsterSpawnContainer child = children[i];
                if (child == null)
                    continue;

                reusableContainers.Add(child);
            }

            return;
        }

        for (int i = 0; i < spawnContainers.Count; i++)
        {
            MonsterSpawnContainer container = spawnContainers[i];
            if (container == null)
                continue;

            reusableContainers.Add(container);
        }
    }

    /// <summary>간단한 셔플로 방 안 스폰 포인트 순서를 무작위화합니다.</summary>
    private static void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int j = Random.Range(i, list.Count);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        spawnContainers.RemoveAll(container => container == null);
    }
#endif
}
