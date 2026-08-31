using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임:
/// - 한 번의 던전 조립 결과인 방 배치, 소켓 연결, 완성 여부와 실패 사유를 보관한다.
/// - Tilemap 빌더가 절차 생성 알고리즘을 몰라도 결과를 구현할 수 있는 경계 데이터 역할을 한다.
/// - 권장 복도 길이를 충돌 회피 목적으로 자동 축소했는지 진단 정보로 제공한다.
/// </summary>
public sealed class DungeonLayoutResult
{
    private readonly List<DungeonRoomPlacement> rooms = new();
    private readonly List<DungeonSocketConnection> connections = new();

    public int Seed { get; }
    public int RequestedRoomCount { get; }
    public IReadOnlyList<DungeonRoomPlacement> Rooms => rooms;
    public IReadOnlyList<DungeonSocketConnection> Connections => connections;
    public bool IsComplete { get; private set; }
    public string FailureReason { get; private set; }
    public bool UsesGraphFirstLayout { get; private set; }
    public int BossGraphDistance { get; private set; }
    public int MeaningfulBranchCount { get; private set; }
    public int CycleConnectionCount { get; private set; }
    public int DeadEndCount { get; private set; }
    public bool UsedCorridorLengthRelaxation { get; private set; }

    internal DungeonLayoutResult(int seed, int requestedRoomCount)
    {
        Seed = seed;
        RequestedRoomCount = requestedRoomCount;
        FailureReason = string.Empty;
    }

    internal void AddRoom(DungeonRoomPlacement room)
    {
        rooms.Add(room);
    }

    internal void AddConnection(DungeonSocketConnection connection)
    {
        connections.Add(connection);
    }

    internal DungeonRoomPlacement GetRoom(int placementId)
    {
        return placementId >= 0 && placementId < rooms.Count
            ? rooms[placementId]
            : null;
    }

    internal void MarkComplete()
    {
        IsComplete = true;
        FailureReason = string.Empty;
    }

    internal void SetTopologyMetrics(
        int bossGraphDistance,
        int meaningfulBranchCount,
        int cycleConnectionCount,
        int deadEndCount)
    {
        UsesGraphFirstLayout = true;
        BossGraphDistance = Mathf.Max(0, bossGraphDistance);
        MeaningfulBranchCount = Mathf.Max(0, meaningfulBranchCount);
        CycleConnectionCount = Mathf.Max(0, cycleConnectionCount);
        DeadEndCount = Mathf.Max(0, deadEndCount);
    }

    internal void MarkFailed(string reason)
    {
        IsComplete = false;
        FailureReason = reason ?? string.Empty;
    }

    internal void MarkCorridorLengthRelaxed()
    {
        UsedCorridorLengthRelaxation = true;
    }
}

/// <summary>
/// 책임:
/// - 선택된 RoomTemplateSO 하나가 던전 셀 공간에서 차지하는 원점과 예약 bounds를 보관한다.
/// - 배치 ID를 통해 소켓 연결 데이터와 실제 방 구현 데이터를 이어준다.
/// - 특수 방 배치 검증에 필요한 시작점 거리, 막다른 길, 순환 우회로 여부를 노출한다.
/// </summary>
public sealed class DungeonRoomPlacement
{
    public int PlacementId { get; }
    public RoomTemplateSO Template { get; }
    public Vector2Int Origin { get; }
    public RectInt WorldBounds { get; }
    public int GraphDistanceFromStart { get; }
    public bool IsDeadEnd { get; }
    public bool IsCycleDetour { get; }

    internal DungeonRoomPlacement(
        int placementId,
        RoomTemplateSO template,
        Vector2Int origin,
        RectInt worldBounds,
        int graphDistanceFromStart = 0,
        bool isDeadEnd = false,
        bool isCycleDetour = false)
    {
        PlacementId = placementId;
        Template = template;
        Origin = origin;
        WorldBounds = worldBounds;
        GraphDistanceFromStart = Mathf.Max(0, graphDistanceFromStart);
        IsDeadEnd = isDeadEnd;
        IsCycleDetour = isCycleDetour;
    }
}

/// <summary>
/// 책임:
/// - 배치된 두 방에서 실제로 소비된 소켓 인덱스 한 쌍과 방 크기에 따라 결정된 직선 복도의 길이/예약 영역을 보관한다.
/// - Tilemap 빌더가 연결 지점의 벽을 제거하고 충돌 없는 복도를 구현할 수 있도록 방 배치와 소켓을 연결한다.
/// </summary>
public readonly struct DungeonSocketConnection
{
    public int FirstRoomPlacementId { get; }
    public int FirstSocketIndex { get; }
    public int SecondRoomPlacementId { get; }
    public int SecondSocketIndex { get; }
    public int CorridorLength { get; }
    public RectInt CorridorBounds { get; }

    internal DungeonSocketConnection(
        int firstRoomPlacementId,
        int firstSocketIndex,
        int secondRoomPlacementId,
        int secondSocketIndex,
        int corridorLength,
        RectInt corridorBounds)
    {
        FirstRoomPlacementId = firstRoomPlacementId;
        FirstSocketIndex = firstSocketIndex;
        SecondRoomPlacementId = secondRoomPlacementId;
        SecondSocketIndex = secondSocketIndex;
        CorridorLength = Mathf.Max(0, corridorLength);
        CorridorBounds = corridorBounds;
    }
}
