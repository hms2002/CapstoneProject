using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityGAS;

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

    private MonoBehaviour gaugeViewInstaller;
    private TilemapPathfinder2D pathfinder;

    public MonoBehaviour GaugeViewInstaller => gaugeViewInstaller;
    public TilemapPathfinder2D Pathfinder => pathfinder;

    public SceneMonsterSpawnDirector(
        List<MonsterSpawnContainer> spawnPoints,
        List<MonsterSpawnRoomGroup> spawnRooms,
        MonoBehaviour gaugeViewInstaller,
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

        request.SourceContainer?.NotifyRuntimeSpawned(monster);

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
        gaugeViewInstaller = ResolveSceneContract<IMonsterElementGaugeViewInstaller>(gaugeViewInstaller, activeScene);
        pathfinder = ResolveSceneObject(pathfinder, activeScene);
    }

    public void RemoveNullAuthoringEntries()
    {
        spawnPoints.RemoveAll(point => point == null);
        spawnRooms.RemoveAll(room => room == null);
    }

    private void ApplyDifficulty(GameObject monster, DifficultyModifiers difficultyModifiers)
    {
        if (monster == null || difficultyModifiers == null)
            return;

        IMonsterDifficultyReceiver[] receivers = monster.GetComponentsInChildren<IMonsterDifficultyReceiver>(true);
        if (receivers.Length == 0 && ShouldAttachDefaultDifficultyReceiver(monster))
        {
            MonsterDifficultyReceiver receiver = monster.GetComponent<MonsterDifficultyReceiver>();
            if (receiver == null)
                receiver = monster.AddComponent<MonsterDifficultyReceiver>();

            receiver.ApplyDifficulty(difficultyModifiers);
            return;
        }

        for (int i = 0; i < receivers.Length; i++)
            receivers[i].ApplyDifficulty(difficultyModifiers);
    }

    /// <summary>
    /// 책임:
    /// - legacy 일반 몬스터 프리팹에 MonsterDifficultyReceiver authoring이 빠져 있어도 스테이지 보정이 누락되지 않게 한다.
    /// - 스포너가 생성한 Enemy 본체만 대상으로 삼아 투사체/이펙트/장판 같은 전투 부속물에는 난이도 수신기를 붙이지 않는다.
    /// </summary>
    private static bool ShouldAttachDefaultDifficultyReceiver(GameObject monster)
    {
        if (monster == null)
            return false;

        return monster.GetComponent<Enemy>() != null &&
               monster.GetComponent<AttributeSet>() != null;
    }

    private void InstallViews(GameObject monster)
    {
        IMonsterElementGaugeViewInstaller installer = gaugeViewInstaller as IMonsterElementGaugeViewInstaller;
        if (installer == null || !IsSceneServiceCurrent(installer.InstallerComponent))
            ResolveSceneServices();

        installer = gaugeViewInstaller as IMonsterElementGaugeViewInstaller;
        installer?.InstallFor(monster);
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

    private MonoBehaviour ResolveSceneContract<TContract>(MonoBehaviour current, Scene activeScene)
        where TContract : class
    {
        if (current != null &&
            current.gameObject.scene == activeScene &&
            current is TContract)
        {
            return current;
        }

#if UNITY_2023_1_OR_NEWER
        MonoBehaviour[] candidates = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        MonoBehaviour[] candidates = Object.FindObjectsOfType<MonoBehaviour>(true);
#endif

        for (int i = 0; i < candidates.Length; i++)
        {
            MonoBehaviour candidate = candidates[i];
            if (candidate == null || candidate.gameObject.scene != activeScene)
                continue;

            if (candidate is TContract)
                return candidate;
        }

        return null;
    }

    private static bool IsSceneServiceCurrent(Component service)
    {
        return service != null && service.gameObject.scene == SceneManager.GetActiveScene();
    }

    private void CompactSpawnedMonsterList()
    {
        for (int i = spawnedMonsters.Count - 1; i >= 0; i--)
        {
            if (spawnedMonsters[i] == null)
                spawnedMonsters.RemoveAt(i);
        }
    }

}
