using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 책임:
/// - 현재 active scene의 spawn point, room group, scene service를 수집한다.
/// - 씬 로컬 몬스터 생성/정리/난이도 적용/스폰 문맥 주입을 실행한다.
/// - 전역 난이도 보관과 singleton 생명주기는 MonsterSpawner에 남긴다.
/// </summary>
internal sealed class SceneMonsterSpawnDirector
{
    private readonly List<MonsterSpawnContainer> spawnPoints;
    private readonly List<MonsterSpawnRoomGroup> spawnRooms;
    private readonly List<GameObject> spawnedMonsters = new();

    private MonsterElementGaugeViewInstaller gaugeViewInstaller;
    private TilemapPathfinder2D pathfinder;

    public MonsterElementGaugeViewInstaller GaugeViewInstaller => gaugeViewInstaller;
    public TilemapPathfinder2D Pathfinder => pathfinder;

    public SceneMonsterSpawnDirector(
        List<MonsterSpawnContainer> spawnPoints,
        List<MonsterSpawnRoomGroup> spawnRooms,
        MonsterElementGaugeViewInstaller gaugeViewInstaller,
        TilemapPathfinder2D pathfinder)
    {
        this.spawnPoints = spawnPoints ?? new List<MonsterSpawnContainer>();
        this.spawnRooms = spawnRooms ?? new List<MonsterSpawnRoomGroup>();
        this.gaugeViewInstaller = gaugeViewInstaller;
        this.pathfinder = pathfinder;
    }

    public void HandleSceneLoaded(SceneMonsterSpawnPolicy policy, DifficultyModifiers difficultyModifiers)
    {
        ResolveSceneServices();

        if (policy.RecollectSpawnPoints)
            CollectSpawnPointsFromActiveScene();

        CompactSpawnedMonsterList();

        if (policy.ClearAliveMonstersBeforeSceneSpawn)
            ClearSpawnedMonsters();

        if (policy.SpawnOnSceneLoaded)
            SpawnAll(difficultyModifiers);
    }

    public void CollectSpawnPointsFromActiveScene()
    {
        spawnPoints.Clear();
        spawnRooms.Clear();

        Scene activeScene = SceneManager.GetActiveScene();

#if UNITY_2023_1_OR_NEWER
        var found = Object.FindObjectsByType<MonsterSpawnContainer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        var foundRooms = Object.FindObjectsByType<MonsterSpawnRoomGroup>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
        var found = Object.FindObjectsOfType<MonsterSpawnContainer>();
        var foundRooms = Object.FindObjectsOfType<MonsterSpawnRoomGroup>();
#endif

        for (int i = 0; i < found.Length; i++)
        {
            MonsterSpawnContainer point = found[i];
            if (point == null || point.gameObject.scene != activeScene)
                continue;

            spawnPoints.Add(point);
        }

        for (int i = 0; i < foundRooms.Length; i++)
        {
            MonsterSpawnRoomGroup room = foundRooms[i];
            if (room == null || room.gameObject.scene != activeScene)
                continue;

            spawnRooms.Add(room);
        }
    }

    public void SpawnAll(DifficultyModifiers difficultyModifiers)
    {
        SpawnGroupedRooms(difficultyModifiers);
        SpawnUngroupedFallback(difficultyModifiers);
    }

    public GameObject SpawnOne(MonsterSpawnRequest request, DifficultyModifiers difficultyModifiers)
    {
        if (!request.IsValid)
            return null;

        GameObject monster = Object.Instantiate(
            request.MonsterPrefab,
            request.Position,
            request.Rotation);

        ApplyDifficulty(monster, difficultyModifiers);
        InstallViews(monster);
        ApplySpawnContext(monster, request);
        ApplyLockTrackingContext(monster, request);

        if (request.LinkedChestKillLock != null)
            request.LinkedChestKillLock.RegisterMonster(monster);

        if (request.SourceRoomGroup != null)
            request.SourceRoomGroup.NotifyMonsterSpawned(monster);

        spawnedMonsters.Add(monster);
        return monster;
    }

    public void ClearSpawnedMonsters()
    {
        for (int i = spawnedMonsters.Count - 1; i >= 0; i--)
        {
            GameObject monster = spawnedMonsters[i];
            if (monster != null)
                Object.Destroy(monster);
        }

        spawnedMonsters.Clear();
    }

    public void ReapplyDifficultyToAliveMonsters(DifficultyModifiers difficultyModifiers)
    {
        CompactSpawnedMonsterList();

        for (int i = 0; i < spawnedMonsters.Count; i++)
        {
            GameObject monster = spawnedMonsters[i];
            if (monster == null)
                continue;

            ApplyDifficulty(monster, difficultyModifiers);
        }
    }

    public void RegisterSpawnPoint(MonsterSpawnContainer point)
    {
        if (point == null)
            return;

        if (!spawnPoints.Contains(point))
            spawnPoints.Add(point);
    }

    public void UnregisterSpawnPoint(MonsterSpawnContainer point)
    {
        if (point == null)
            return;

        spawnPoints.Remove(point);
    }

    public void ResolveSceneServices()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        gaugeViewInstaller = ResolveSceneObject(gaugeViewInstaller, activeScene);
        pathfinder = ResolveSceneObject(pathfinder, activeScene);
    }

    public void RemoveNullAuthoringEntries()
    {
        spawnPoints.RemoveAll(point => point == null);
        spawnRooms.RemoveAll(room => room == null);
    }

    private void SpawnGroupedRooms(DifficultyModifiers difficultyModifiers)
    {
        List<MonsterSpawnRequest> requests = new List<MonsterSpawnRequest>();
        for (int i = 0; i < spawnRooms.Count; i++)
        {
            MonsterSpawnRoomGroup room = spawnRooms[i];
            if (room == null || room.SpawnProfile == null)
                continue;

            room.BuildSpawnRequests(requests);
        }

        for (int i = 0; i < requests.Count; i++)
            SpawnOne(requests[i], difficultyModifiers);
    }

    private void SpawnUngroupedFallback(DifficultyModifiers difficultyModifiers)
    {
        List<MonsterSpawnContainer> defaultPoints = new List<MonsterSpawnContainer>();
        List<MonsterSpawnContainer> extraCandidates = new List<MonsterSpawnContainer>();

        for (int i = 0; i < spawnPoints.Count; i++)
        {
            MonsterSpawnContainer point = spawnPoints[i];
            if (point == null || point.MonsterPrefab == null)
                continue;

            if (point.RoomGroup != null && point.RoomGroup.SpawnProfile != null)
                continue;

            if (point.SpawnByDefault)
                defaultPoints.Add(point);

            if (!point.SpawnByDefault && point.AllowExtraSpawn)
                extraCandidates.Add(point);
        }

        for (int i = 0; i < defaultPoints.Count; i++)
            SpawnOne(defaultPoints[i].CreateRequest(), difficultyModifiers);

        int extraCount = CalculateExtraSpawnCount(defaultPoints.Count, difficultyModifiers?.extraSpawnRatio ?? 0f);
        Shuffle(extraCandidates);

        for (int i = 0; i < extraCount && i < extraCandidates.Count; i++)
            SpawnOne(extraCandidates[i].CreateRequest(), difficultyModifiers);
    }

    private void ApplyDifficulty(GameObject monster, DifficultyModifiers difficultyModifiers)
    {
        if (monster == null || difficultyModifiers == null)
            return;

        IMonsterDifficultyReceiver[] receivers = monster.GetComponentsInChildren<IMonsterDifficultyReceiver>(true);
        for (int i = 0; i < receivers.Length; i++)
            receivers[i].ApplyDifficulty(difficultyModifiers);
    }

    private void InstallViews(GameObject monster)
    {
        if (!IsSceneServiceCurrent(gaugeViewInstaller))
            ResolveSceneServices();

        if (gaugeViewInstaller != null)
            gaugeViewInstaller.Install(monster);
    }

    private void ApplySpawnContext(GameObject monster, MonsterSpawnRequest request)
    {
        if (monster == null)
            return;

        if (!IsSceneServiceCurrent(pathfinder))
            ResolveSceneServices();

        EnemyChaseIntent2D chaseIntent = monster.GetComponent<EnemyChaseIntent2D>();
        if (chaseIntent != null && monster.GetComponent<MonsterReturnHome2D>() == null)
            monster.AddComponent<MonsterReturnHome2D>();

        MonsterSpawnContext context = new MonsterSpawnContext(
            request.Position,
            request.Rotation,
            request.RoomArea,
            pathfinder);

        IMonsterSpawnContextReceiver[] receivers = monster.GetComponentsInChildren<IMonsterSpawnContextReceiver>(true);
        for (int i = 0; i < receivers.Length; i++)
            receivers[i].ApplySpawnContext(context);
    }

    private static void ApplyLockTrackingContext(GameObject monster, MonsterSpawnRequest request)
    {
        if (monster == null)
            return;

        Mob mob = monster.GetComponent<Mob>();
        if (mob == null)
            return;

        mob.ApplyLockTrackingContext(request.LinkedChestKillLock, request.SourceRoomGroup);
    }

    private T ResolveSceneObject<T>(T current, Scene activeScene) where T : Component
    {
        if (current != null && current.gameObject.scene == activeScene)
            return current;

#if UNITY_2023_1_OR_NEWER
        T[] candidates = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        T[] candidates = Object.FindObjectsOfType<T>(true);
#endif

        for (int i = 0; i < candidates.Length; i++)
        {
            T candidate = candidates[i];
            if (candidate == null || candidate.gameObject.scene != activeScene)
                continue;

            return candidate;
        }

        return null;
    }

    private static bool IsSceneServiceCurrent(Component service)
    {
        return service != null && service.gameObject.scene == SceneManager.GetActiveScene();
    }

    private int CalculateExtraSpawnCount(int baseCount, float extraRatio)
    {
        if (baseCount <= 0 || extraRatio <= 0f)
            return 0;

        float raw = baseCount * extraRatio;
        int count = Mathf.FloorToInt(raw);

        float fractional = raw - count;
        if (Random.value < fractional)
            count += 1;

        return count;
    }

    private void CompactSpawnedMonsterList()
    {
        for (int i = spawnedMonsters.Count - 1; i >= 0; i--)
        {
            if (spawnedMonsters[i] == null)
                spawnedMonsters.RemoveAt(i);
        }
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int j = Random.Range(i, list.Count);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
