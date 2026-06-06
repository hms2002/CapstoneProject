using UnityEngine;

/// <summary>
/// 책임:
/// - 몬스터가 스폰 시점에 기억해야 하는 최소 문맥을 묶어 전달한다.
/// - 홈 위치, 소속 방, 복귀 경로 서비스 참조를 함께 보관한다.
/// </summary>
public readonly struct MonsterSpawnContext
{
    public readonly Vector3 HomePosition;
    public readonly Quaternion HomeRotation;
    public readonly MonsterRoomArea2D RoomArea;
    public readonly TilemapPathfinder2D Pathfinder;

    public MonsterSpawnContext(
        Vector3 homePosition,
        Quaternion homeRotation,
        MonsterRoomArea2D roomArea,
        TilemapPathfinder2D pathfinder)
    {
        HomePosition = homePosition;
        HomeRotation = homeRotation;
        RoomArea = roomArea;
        Pathfinder = pathfinder;
    }
}
