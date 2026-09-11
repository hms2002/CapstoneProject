using UnityEngine;

/// <summary>
/// 책임 : MonsterSpawner가 몬스터를 생성할 때 필요한 최소 입력값을 묶는다.
/// 어떤 몬스터를, 어디에, 어떤 회전으로 생성할지와
/// 스폰 후 어느 방/상자 잠금 조건과 원본 스폰 포인트에 결과를 전달할지를 함께 보관한다.
/// 스폰 출처가 요구하는 추가 HP 보정도 같이 운반한다.
/// </summary>
public readonly struct MonsterSpawnRequest
{
    public readonly GameObject MonsterPrefab;
    public readonly Vector3 Position;
    public readonly Quaternion Rotation;
    public readonly MonsterRoomArea2D RoomArea;
    public readonly ChestMonsterKillLock LinkedChestKillLock;
    public readonly MonsterSpawnRoomGroup SourceRoomGroup;
    public readonly MonsterSpawnContainer SourceContainer;
    public readonly float SpawnHpMultiplier;

    public MonsterSpawnRequest(
        GameObject monsterPrefab,
        Vector3 position,
        Quaternion rotation,
        MonsterRoomArea2D roomArea,
        ChestMonsterKillLock linkedChestKillLock,
        MonsterSpawnRoomGroup sourceRoomGroup = null,
        MonsterSpawnContainer sourceContainer = null,
        float spawnHpMultiplier = 1f)
    {
        MonsterPrefab = monsterPrefab;
        Position = position;
        Rotation = rotation;
        RoomArea = roomArea;
        LinkedChestKillLock = linkedChestKillLock;
        SourceRoomGroup = sourceRoomGroup;
        SourceContainer = sourceContainer;
        SpawnHpMultiplier = Mathf.Max(0f, spawnHpMultiplier);
    }

    public bool IsValid => MonsterPrefab != null;
}
