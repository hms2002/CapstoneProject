using System;
using UnityEngine;

/// <summary>
/// 책임:
/// - 씬 스폰 포인트가 고정 몬스터 프리팹을 쓸지, 스테이지별 몬스터 세트를 쓸지 구분한다.
/// - 맵 authoring 데이터와 런타임 resolve 정책 사이의 선택값을 명시한다.
/// </summary>
public enum MonsterSpawnSourceKind
{
    FixedPrefab,
    StageMonsterSet
}

/// <summary>
/// 책임 : 씬에 배치된 몬스터 스폰 위치 데이터를 보관한다.
/// 자신의 설정을 바탕으로 MonsterSpawner가 사용할 스폰 요청을 생성하고,
/// 필요 시 특정 상자의 몬스터 처치 잠금 조건과 연결한다.
/// </summary>
public class MonsterSpawnContainer : MonoBehaviour
{
    // 이 클래스의 책임:
    // 씬에 배치된 스폰 포인트 1개의 위치/방/연결 정보를 보관하고, MonsterSpawner가 사용할 스폰 요청을 생성한다.

    [Header("Spawn")]
    [SerializeField] private MonsterSpawnSourceKind sourceKind = MonsterSpawnSourceKind.FixedPrefab;
    [SerializeField] private GameObject monsterPrefab;
    [SerializeField] private StageMonsterSetSO stageMonsterSet;
    [SerializeField] private bool spawnByDefault = true;

    [Tooltip("추가 난이도 옵션으로 몬스터를 더 뽑을 때 후보가 될 수 있는 위치")]
    [SerializeField] private bool allowExtraSpawn = true;

    [Header("Override")]
    [SerializeField] private Transform spawnAnchor;

    [Header("Room")]
    [SerializeField] private MonsterRoomArea2D roomArea;
    [SerializeField] private MonsterSpawnRoomGroup roomGroup;

    [Header("Chest Lock")]
    [Tooltip("이 스폰 포인트에서 생성한 몬스터를 특정 상자의 잠금 해제 조건으로 등록할 때 사용")]
    [SerializeField] private ChestMonsterKillLock linkedChestKillLock;

    private Action<GameObject> runtimeSpawnedCallback;

    public MonsterSpawnSourceKind SourceKind => sourceKind;
    public GameObject MonsterPrefab => monsterPrefab;
    public StageMonsterSetSO StageMonsterSet => stageMonsterSet;
    public bool SpawnByDefault => spawnByDefault;
    public bool AllowExtraSpawn => allowExtraSpawn;
    public ChestMonsterKillLock LinkedChestKillLock => linkedChestKillLock;
    public MonsterRoomArea2D RoomArea => roomArea;
    public MonsterSpawnRoomGroup RoomGroup => roomGroup != null ? roomGroup : GetComponentInParent<MonsterSpawnRoomGroup>();

    public Vector3 SpawnPosition => spawnAnchor != null ? spawnAnchor.position : transform.position;
    public Quaternion SpawnRotation => spawnAnchor != null ? spawnAnchor.rotation : transform.rotation;

    /// <summary>
    /// 책임:
    /// - 절차 생성기가 스테이지 고정 몬스터 프리팹을 실제 몬스터가 아닌 지연 스폰 포인트로 구성한다.
    /// - 방 진입 전에는 생성하지 않고 기존 MonsterSpawner 요청 계약을 그대로 사용한다.
    /// </summary>
    public void ConfigureRuntime(
        GameObject configuredMonsterPrefab,
        MonsterRoomArea2D configuredRoomArea,
        MonsterSpawnRoomGroup configuredRoomGroup,
        ChestMonsterKillLock configuredChestKillLock,
        Action<GameObject> onRuntimeSpawned = null)
    {
        ConfigureRuntime(
            configuredStageMonsterSet: null,
            configuredMonsterPrefab,
            configuredRoomArea,
            configuredRoomGroup,
            configuredChestKillLock,
            onRuntimeSpawned);
    }

    /// <summary>
    /// 책임:
    /// - 역할형 절차 몬스터 지점에 연결된 StageMonsterSetSO를 현재 진행도에서 해석할 스폰 소스로 구성한다.
    /// - StageMonsterSetSO가 없으면 기획자가 지정한 스테이지 고정 프리팹을 스폰 소스로 사용한다.
    /// </summary>
    public void ConfigureRuntime(
        StageMonsterSetSO configuredStageMonsterSet,
        GameObject configuredMonsterPrefab,
        MonsterRoomArea2D configuredRoomArea,
        MonsterSpawnRoomGroup configuredRoomGroup,
        ChestMonsterKillLock configuredChestKillLock,
        Action<GameObject> onRuntimeSpawned = null)
    {
        sourceKind = configuredStageMonsterSet != null
            ? MonsterSpawnSourceKind.StageMonsterSet
            : MonsterSpawnSourceKind.FixedPrefab;
        monsterPrefab = configuredMonsterPrefab;
        stageMonsterSet = configuredStageMonsterSet;
        spawnByDefault = true;
        allowExtraSpawn = true;
        spawnAnchor = null;
        roomArea = configuredRoomArea;
        roomGroup = configuredRoomGroup;
        linkedChestKillLock = configuredChestKillLock;
        runtimeSpawnedCallback = onRuntimeSpawned;
    }

    /// <summary>
    /// 책임:
    /// - 이 포인트에서 실제 몬스터가 생성된 결과를 절차 던전 상태 추적자 같은 런타임 소유자에게 전달한다.
    /// </summary>
    public void NotifyRuntimeSpawned(GameObject spawnedMonster)
    {
        runtimeSpawnedCallback?.Invoke(spawnedMonster);
    }

    public bool TryResolveMonsterPrefab(int stageIndex, out GameObject resolvedPrefab)
    {
        resolvedPrefab = null;
        switch (sourceKind)
        {
            case MonsterSpawnSourceKind.FixedPrefab:
                resolvedPrefab = monsterPrefab;
                return resolvedPrefab != null;

            case MonsterSpawnSourceKind.StageMonsterSet:
                return stageMonsterSet != null &&
                       stageMonsterSet.TryResolveMonsterPrefab(stageIndex, out resolvedPrefab);

            default:
                return false;
        }
    }

    public MonsterSpawnRequest CreateRequest(GameObject overrideMonsterPrefab = null)
    {
        GameObject resolvedPrefab = overrideMonsterPrefab != null ? overrideMonsterPrefab : monsterPrefab;
        return new MonsterSpawnRequest(
            resolvedPrefab,
            SpawnPosition,
            SpawnRotation,
            roomArea,
            linkedChestKillLock,
            RoomGroup,
            this);
    }

    public MonsterSpawnRequest CreateRequest(int stageIndex, GameObject overrideMonsterPrefab = null)
    {
        if (overrideMonsterPrefab != null)
            return CreateRequest(overrideMonsterPrefab);

        TryResolveMonsterPrefab(stageIndex, out GameObject resolvedPrefab);
        return CreateRequest(resolvedPrefab);
    }

    public bool TryCreateRequest(int stageIndex, out MonsterSpawnRequest request)
    {
        if (!TryResolveMonsterPrefab(stageIndex, out GameObject resolvedPrefab))
        {
            request = default;
            return false;
        }

        request = CreateRequest(resolvedPrefab);
        return true;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = spawnByDefault ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(SpawnPosition, 0.2f);
    }
#endif
}
