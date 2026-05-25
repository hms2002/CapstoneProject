using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임:
/// - 하나의 방에 속한 MonsterSpawnContainer들을 묶어 관리한다.
/// - 방 전용 스폰 프로파일에서 무작위로 스폰 테이블 하나를 선택해 스폰 요청 목록을 생성한다.
/// - 플레이어의 방 encounter 진입/이탈을 연결된 문 잠금 장치에 전파한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class MonsterSpawnRoomGroup : MonoBehaviour
{
    [SerializeField] private MonsterRoomSpawnProfileSO spawnProfile;
    [SerializeField] private bool autoCollectChildContainers = true;
    [SerializeField] private List<MonsterSpawnContainer> spawnContainers = new();

    private readonly List<MonsterSpawnContainer> reusableContainers = new();
    private readonly List<GameObject> reusableSpawnPlan = new();
    private readonly List<RoomDoorMonsterKillLock> runtimeDoorLocks = new();
    private readonly List<GameObject> runtimeSpawnedMonsters = new();
    private bool playerEncounterEntered;

    public MonsterRoomSpawnProfileSO SpawnProfile => spawnProfile;
    public bool PlayerEncounterEntered => playerEncounterEntered;

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

        int stageIndex = ResolveCurrentStageIndex();
        if (!spawnProfile.TryGetRandomSpawnTable(out MonsterRoomSpawnProfileSO.SpawnTable table, stageIndex))
            return;

        RefreshContainersIfNeeded();
        if (reusableContainers.Count == 0)
            return;

        if (!table.TryBuildSpawnPlan(reusableSpawnPlan, stageIndex) || reusableSpawnPlan.Count == 0)
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
    public void RegisterDoorLock(RoomDoorMonsterKillLock doorLock)
    {
        if (doorLock == null || runtimeDoorLocks.Contains(doorLock))
            return;

        runtimeDoorLocks.Add(doorLock);
        CompactRuntimeLists();

        for (int i = 0; i < runtimeSpawnedMonsters.Count; i++)
        {
            GameObject monster = runtimeSpawnedMonsters[i];
            if (monster != null)
                doorLock.RegisterMonster(monster);
        }

        if (playerEncounterEntered)
            doorLock.NotifyRoomEncounterEntered();
    }

    public void UnregisterDoorLock(RoomDoorMonsterKillLock doorLock)
    {
        if (doorLock == null)
            return;

        runtimeDoorLocks.Remove(doorLock);
    }

    public void NotifyMonsterSpawned(GameObject monster)
    {
        if (monster == null)
            return;

        CompactRuntimeLists();

        if (!runtimeSpawnedMonsters.Contains(monster))
            runtimeSpawnedMonsters.Add(monster);

        for (int i = runtimeDoorLocks.Count - 1; i >= 0; i--)
        {
            RoomDoorMonsterKillLock doorLock = runtimeDoorLocks[i];
            if (doorLock == null)
            {
                runtimeDoorLocks.RemoveAt(i);
                continue;
            }

            doorLock.RegisterMonster(monster);
        }
    }

    public void NotifyPlayerEnteredEncounter()
    {
        if (playerEncounterEntered)
            return;

        playerEncounterEntered = true;
        CompactRuntimeLists();

        for (int i = runtimeDoorLocks.Count - 1; i >= 0; i--)
        {
            RoomDoorMonsterKillLock doorLock = runtimeDoorLocks[i];
            if (doorLock == null)
            {
                runtimeDoorLocks.RemoveAt(i);
                continue;
            }

            doorLock.NotifyRoomEncounterEntered();
        }
    }

    public void NotifyPlayerExitedEncounter()
    {
        if (!playerEncounterEntered)
            return;

        playerEncounterEntered = false;
        CompactRuntimeLists();

        for (int i = runtimeDoorLocks.Count - 1; i >= 0; i--)
        {
            RoomDoorMonsterKillLock doorLock = runtimeDoorLocks[i];
            if (doorLock == null)
            {
                runtimeDoorLocks.RemoveAt(i);
                continue;
            }

            doorLock.NotifyRoomEncounterExited();
        }
    }

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

    /// <summary>
    /// 책임:
    /// - 현재 런 진행 단계의 0-based stage index를 스폰 프로파일 해석에 제공한다.
    /// - 런 플랜이 없는 개발/테스트 씬에서는 첫 단계로 취급해 기존 동작을 유지한다.
    /// </summary>
    private static int ResolveCurrentStageIndex()
    {
        PortalRouteManager routeManager = PortalRouteManager.Instance;
        if (routeManager == null || !routeManager.HasActivePlan)
            return 0;

        return Mathf.Max(0, routeManager.CurrentStageIndex);
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

    private void CompactRuntimeLists()
    {
        runtimeDoorLocks.RemoveAll(doorLock => doorLock == null);
        runtimeSpawnedMonsters.RemoveAll(monster => monster == null);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        spawnContainers.RemoveAll(container => container == null);
    }
#endif
}
