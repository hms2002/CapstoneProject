using System;
using System.Collections;
using System.Collections.Generic;
using CapstoneAudio;
using UnityEngine;

/// <summary>
/// 책임:
/// - 하나의 방에 속한 MonsterSpawnContainer들을 묶어 관리한다.
/// - 플레이어가 방에 처음 들어왔을 때 연결된 MonsterSpawnContainer들을 VFX와 함께 생성한다.
/// - 플레이어의 방 encounter 진입/이탈을 연결된 문 잠금 장치에 전파한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class MonsterSpawnRoomGroup : MonoBehaviour
{
    private const string DefaultRoomEntrySpawnSettingsResourcePath = "MonsterRoomEntrySpawnSettings";

    public static event Action<MonsterSpawnRoomGroup> ActiveRoomEntered;
    public static event Action<MonsterSpawnRoomGroup> ActiveRoomExited;

    [SerializeField] private MonsterRoomSpawnProfileSO spawnProfile;
    [SerializeField] private bool autoCollectChildContainers = true;
    [SerializeField] private List<MonsterSpawnContainer> spawnContainers = new();

    [Header("Entry Spawn Presentation")]
    [SerializeField] private GameObject spawnVfxPrefab;
    [SerializeField, Min(0f)] private float spawnVfxDelaySeconds = 0.35f;
    [SerializeField] private Vector3 spawnVfxOffset;
    [SerializeField] private SoundRef spawnSound;

    [Header("Debug")]
    [SerializeField] private bool logRoomEntrySpawnDebug;

    private readonly List<MonsterSpawnContainer> reusableContainers = new();
    private readonly List<GameObject> reusableSpawnPlan = new();
    private readonly List<RoomDoorMonsterKillLock> runtimeDoorLocks = new();
    private readonly List<GameObject> runtimeSpawnedMonsters = new();
    private readonly List<Coroutine> activeSpawnRoutines = new();
    private readonly List<ChestMonsterKillLock> pendingChestLocks = new();
    private readonly List<GameObject> activeSpawnVfx = new();
    private MonsterRoomEntrySpawnSettingsSO cachedDefaultSpawnSettings;
    private bool playerEncounterEntered;
    private bool roomEntrySpawnStarted;
    private int pendingRoomEntrySpawnCount;

    public MonsterRoomSpawnProfileSO SpawnProfile => spawnProfile;
    public bool PlayerEncounterEntered => playerEncounterEntered;
    public int PendingRoomEntrySpawnCount => pendingRoomEntrySpawnCount;
    public int RemainingRegisteredOrPendingCount => CountAliveRegisteredMonsters() + pendingRoomEntrySpawnCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticEvents()
    {
        ActiveRoomEntered = null;
        ActiveRoomExited = null;
    }

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

    public int GetAliveRegisteredMonstersNonAlloc(List<GameObject> results)
    {
        if (results == null)
            return 0;

        results.Clear();
        CompactRuntimeLists();

        for (int i = 0; i < runtimeSpawnedMonsters.Count; i++)
        {
            GameObject monster = runtimeSpawnedMonsters[i];
            if (monster != null)
                results.Add(monster);
        }

        return results.Count;
    }

    public bool HasPendingRoomEntrySpawns()
    {
        return pendingRoomEntrySpawnCount > 0;
    }

    public void NotifyPlayerEnteredEncounter()
    {
        if (playerEncounterEntered)
            return;

        playerEncounterEntered = true;
        CompactRuntimeLists();
        StartRoomEntrySpawnIfNeeded();

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

        ActiveRoomEntered?.Invoke(this);
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

        ActiveRoomExited?.Invoke(this);
    }

    private void OnDisable()
    {
        CancelActiveSpawnRoutines();
        ReleaseAllPendingSpawns();
    }

    /// <summary>
    /// 책임:
    /// - 방 첫 입장 시 기존 authoring으로 배치된 spawnByDefault 컨테이너들을 지연 스폰한다.
    /// - 맵 툴 세팅을 바꾸지 않고도 기존 방 몬스터 배치가 입장 스폰으로 동작하게 한다.
    /// </summary>
    private void StartRoomEntrySpawnIfNeeded()
    {
        if (roomEntrySpawnStarted)
        {
            LogRoomEntrySpawn("skip: room entry spawn already started.");
            return;
        }

        roomEntrySpawnStarted = true;
        RefreshContainersIfNeeded();
        LogRoomEntrySpawn($"start: containers={reusableContainers.Count}");

        int stageIndex = ResolveCurrentStageIndex();
        for (int i = 0; i < reusableContainers.Count; i++)
        {
            MonsterSpawnContainer container = reusableContainers[i];
            if (container == null || !container.SpawnByDefault)
            {
                LogRoomEntrySpawn($"skip container[{i}]: nullOrSpawnByDefaultFalse container={FormatContainer(container)}");
                continue;
            }

            if (!container.TryCreateRequest(stageIndex, out MonsterSpawnRequest request))
            {
                LogRoomEntrySpawn($"skip container[{i}]: failed request. container={FormatContainer(container)}, stage={stageIndex}");
                continue;
            }

            ReservePendingSpawn(request);
            LogRoomEntrySpawn($"reserved container[{i}]: prefab={FormatObject(request.MonsterPrefab)}, pos={request.Position}, pending={pendingRoomEntrySpawnCount}, chestLock={FormatObject(request.LinkedChestKillLock)}");
            Coroutine routine = StartCoroutine(SpawnRoomEntryMonsterRoutine(request));
            activeSpawnRoutines.Add(routine);
        }
    }

    private IEnumerator SpawnRoomEntryMonsterRoutine(MonsterSpawnRequest request)
    {
        GameObject vfx = null;
        GameObject resolvedVfxPrefab = ResolveSpawnVfxPrefab();
        float resolvedDelaySeconds = ResolveSpawnVfxDelaySeconds(resolvedVfxPrefab);
        Vector3 resolvedOffset = ResolveSpawnVfxOffset();
        SoundRef resolvedSound = ResolveSpawnSound();

        LogRoomEntrySpawn($"routine start: prefab={FormatObject(request.MonsterPrefab)}, pos={request.Position}, vfx={FormatObject(resolvedVfxPrefab)}, delay={resolvedDelaySeconds:0.###}");
        if (resolvedVfxPrefab != null)
        {
            vfx = Instantiate(
                resolvedVfxPrefab,
                request.Position + resolvedOffset,
                request.Rotation);
            activeSpawnVfx.Add(vfx);
            LogRoomEntrySpawn($"vfx instantiated: vfx={FormatObject(vfx)}, pos={vfx.transform.position}");
        }

        PlaySpawnSound(resolvedSound, request.Position + resolvedOffset);

        float delaySeconds = resolvedVfxPrefab != null ? resolvedDelaySeconds : 0f;
        if (delaySeconds > 0f)
        {
            LogRoomEntrySpawn($"wait delay: seconds={delaySeconds:0.###}, prefab={FormatObject(request.MonsterPrefab)}");
            yield return new WaitForSeconds(delaySeconds);
        }

        MonsterSpawner spawner = MonsterSpawner.Instance;
        if (spawner != null)
        {
            GameObject spawnedMonster = spawner.SpawnOne(request);
            LogRoomEntrySpawn($"spawn result: prefab={FormatObject(request.MonsterPrefab)}, monster={FormatObject(spawnedMonster)}");
            ReleasePendingSpawn(request, releaseChestPending: spawnedMonster == null);
        }
        else
        {
            LogRoomEntrySpawn($"spawn failed: MonsterSpawner.Instance is null. prefab={FormatObject(request.MonsterPrefab)}");
            ReleasePendingSpawn(request, releaseChestPending: true);
        }

        activeSpawnRoutines.RemoveAll(routine => routine == null);
        activeSpawnVfx.Remove(vfx);
        if (vfx != null)
            Destroy(vfx);
    }

    /// <summary>
    /// 책임:
    /// - VFX 대기 중인 아직 생성되지 않은 몬스터를 문/상자 kill lock 카운트에 포함한다.
    /// - 실제 몬스터가 생성되기 전 클리어로 오판되는 것을 막는다.
    /// </summary>
    private void ReservePendingSpawn(MonsterSpawnRequest request)
    {
        pendingRoomEntrySpawnCount++;
        LogRoomEntrySpawn($"pending++: pending={pendingRoomEntrySpawnCount}, prefab={FormatObject(request.MonsterPrefab)}");
        if (request.LinkedChestKillLock == null)
            return;

        request.LinkedChestKillLock.ReservePendingMonster();
        pendingChestLocks.Add(request.LinkedChestKillLock);
        LogRoomEntrySpawn($"chest pending++: chestLock={FormatObject(request.LinkedChestKillLock)}");
    }

    private void ReleasePendingSpawn(MonsterSpawnRequest request, bool releaseChestPending)
    {
        pendingRoomEntrySpawnCount = Mathf.Max(0, pendingRoomEntrySpawnCount - 1);
        LogRoomEntrySpawn($"pending--: pending={pendingRoomEntrySpawnCount}, prefab={FormatObject(request.MonsterPrefab)}, releaseChestPending={releaseChestPending}");
        if (request.LinkedChestKillLock == null)
            return;

        if (releaseChestPending)
        {
            request.LinkedChestKillLock.ReleasePendingMonster();
            LogRoomEntrySpawn($"chest pending--: chestLock={FormatObject(request.LinkedChestKillLock)}");
        }

        pendingChestLocks.Remove(request.LinkedChestKillLock);
    }

    private void CancelActiveSpawnRoutines()
    {
        for (int i = 0; i < activeSpawnRoutines.Count; i++)
        {
            Coroutine routine = activeSpawnRoutines[i];
            if (routine != null)
                StopCoroutine(routine);
        }

        activeSpawnRoutines.Clear();

        for (int i = activeSpawnVfx.Count - 1; i >= 0; i--)
        {
            GameObject vfx = activeSpawnVfx[i];
            if (vfx != null)
                Destroy(vfx);
        }

        activeSpawnVfx.Clear();
    }

    private void ReleaseAllPendingSpawns()
    {
        pendingRoomEntrySpawnCount = 0;
        for (int i = 0; i < pendingChestLocks.Count; i++)
        {
            ChestMonsterKillLock chestLock = pendingChestLocks[i];
            if (chestLock != null)
                chestLock.ReleasePendingMonster();
        }

        pendingChestLocks.Clear();
    }

    private void PlaySpawnSound(SoundRef soundRef, Vector3 position)
    {
        if (!soundRef.IsSet)
            return;

        SoundPlaybackUtility.Play(soundRef, causer: gameObject, position: position, sourceObject: this);
        LogRoomEntrySpawn($"sound played: key={soundRef.key}, pos={position}");
    }

    private GameObject ResolveSpawnVfxPrefab()
    {
        if (spawnVfxPrefab != null)
            return spawnVfxPrefab;

        MonsterRoomEntrySpawnSettingsSO settings = ResolveDefaultSpawnSettings();
        return settings != null ? settings.DefaultSpawnVfxPrefab : null;
    }

    private float ResolveSpawnVfxDelaySeconds(GameObject resolvedVfxPrefab)
    {
        if (spawnVfxPrefab != null)
            return spawnVfxDelaySeconds;

        MonsterRoomEntrySpawnSettingsSO settings = ResolveDefaultSpawnSettings();
        return settings != null ? settings.DefaultSpawnVfxDelaySeconds : spawnVfxDelaySeconds;
    }

    private Vector3 ResolveSpawnVfxOffset()
    {
        if (spawnVfxPrefab != null || spawnVfxOffset != Vector3.zero)
            return spawnVfxOffset;

        MonsterRoomEntrySpawnSettingsSO settings = ResolveDefaultSpawnSettings();
        return settings != null ? settings.DefaultSpawnVfxOffset : spawnVfxOffset;
    }

    private SoundRef ResolveSpawnSound()
    {
        if (spawnSound.IsSet)
            return spawnSound;

        MonsterRoomEntrySpawnSettingsSO settings = ResolveDefaultSpawnSettings();
        return settings != null ? settings.DefaultSpawnSound : spawnSound;
    }

    private MonsterRoomEntrySpawnSettingsSO ResolveDefaultSpawnSettings()
    {
        if (cachedDefaultSpawnSettings != null)
            return cachedDefaultSpawnSettings;

        cachedDefaultSpawnSettings = Resources.Load<MonsterRoomEntrySpawnSettingsSO>(DefaultRoomEntrySpawnSettingsResourcePath);
        return cachedDefaultSpawnSettings;
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

                AddReusableContainerIfNeeded(child);
            }

            CollectSceneContainersLinkedToThisRoom();
            return;
        }

        for (int i = 0; i < spawnContainers.Count; i++)
        {
            MonsterSpawnContainer container = spawnContainers[i];
            if (container == null)
                continue;

            AddReusableContainerIfNeeded(container);
        }
    }

    /// <summary>
    /// 책임:
    /// - 맵 툴이 MonsterSpawnContainer.roomGroup 참조로 연결한 스폰 포인트를 방 그룹 authoring 데이터로 수집한다.
    /// - 스폰 포인트가 방 그룹 Transform의 자식이 아니어도 기존 맵 툴 세팅 그대로 방 입장 스폰에 참여하게 한다.
    /// </summary>
    private void CollectSceneContainersLinkedToThisRoom()
    {
#if UNITY_2023_1_OR_NEWER
        MonsterSpawnContainer[] sceneContainers = FindObjectsByType<MonsterSpawnContainer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
        MonsterSpawnContainer[] sceneContainers = FindObjectsOfType<MonsterSpawnContainer>();
#endif

        for (int i = 0; i < sceneContainers.Length; i++)
        {
            MonsterSpawnContainer container = sceneContainers[i];
            if (container == null || container.gameObject.scene != gameObject.scene)
                continue;

            if (container.RoomGroup != this)
                continue;

            AddReusableContainerIfNeeded(container);
        }

        LogRoomEntrySpawn($"collect linked containers: total={reusableContainers.Count}");
    }

    private void AddReusableContainerIfNeeded(MonsterSpawnContainer container)
    {
        if (container == null || reusableContainers.Contains(container))
            return;

        reusableContainers.Add(container);
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
            int j = UnityEngine.Random.Range(i, list.Count);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private void CompactRuntimeLists()
    {
        runtimeDoorLocks.RemoveAll(doorLock => doorLock == null);
        runtimeSpawnedMonsters.RemoveAll(monster => monster == null);
    }

    private int CountAliveRegisteredMonsters()
    {
        CompactRuntimeLists();
        return runtimeSpawnedMonsters.Count;
    }

    private void LogRoomEntrySpawn(string message)
    {
        if (!logRoomEntrySpawnDebug)
            return;

        Debug.Log($"[RoomEntrySpawn] {name}: {message}", this);
    }

    private static string FormatContainer(MonsterSpawnContainer container)
    {
        return container != null ? container.name : "null";
    }

    private static string FormatObject(UnityEngine.Object target)
    {
        return target != null ? target.name : "null";
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        spawnContainers.RemoveAll(container => container == null);
    }
#endif
}
