using UnityEngine;

/// <summary>
/// 책임 : MonsterSpawner가 몬스터를 생성할 때 필요한 최소 입력값을 묶는다.
/// 어떤 몬스터를, 어디에, 어떤 회전으로 생성할지와
/// 스폰 후 어느 상자 잠금 조건에 등록할지를 함께 전달한다.
/// </summary>
public readonly struct MonsterSpawnRequest
{
    public readonly GameObject MonsterPrefab;
    public readonly Vector3 Position;
    public readonly Quaternion Rotation;
    public readonly MonsterRoomArea2D RoomArea;
    public readonly ChestMonsterKillLock LinkedChestKillLock;

    public MonsterSpawnRequest(
        GameObject monsterPrefab,
        Vector3 position,
        Quaternion rotation,
        MonsterRoomArea2D roomArea,
        ChestMonsterKillLock linkedChestKillLock)
    {
        MonsterPrefab = monsterPrefab;
        Position = position;
        Rotation = rotation;
        RoomArea = roomArea;
        LinkedChestKillLock = linkedChestKillLock;
    }

    public bool IsValid => MonsterPrefab != null;
}
