using UnityEngine;

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
    [SerializeField] private GameObject monsterPrefab;
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

    public GameObject MonsterPrefab => monsterPrefab;
    public bool SpawnByDefault => spawnByDefault;
    public bool AllowExtraSpawn => allowExtraSpawn;
    public ChestMonsterKillLock LinkedChestKillLock => linkedChestKillLock;
    public MonsterRoomArea2D RoomArea => roomArea;
    public MonsterSpawnRoomGroup RoomGroup => roomGroup != null ? roomGroup : GetComponentInParent<MonsterSpawnRoomGroup>();

    public Vector3 SpawnPosition => spawnAnchor != null ? spawnAnchor.position : transform.position;
    public Quaternion SpawnRotation => spawnAnchor != null ? spawnAnchor.rotation : transform.rotation;

    public MonsterSpawnRequest CreateRequest(GameObject overrideMonsterPrefab = null)
    {
        return new MonsterSpawnRequest(
            overrideMonsterPrefab != null ? overrideMonsterPrefab : monsterPrefab,
            SpawnPosition,
            SpawnRotation,
            roomArea,
            linkedChestKillLock,
            RoomGroup);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = spawnByDefault ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(SpawnPosition, 0.2f);
    }
#endif
}
