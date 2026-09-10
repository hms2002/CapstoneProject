using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 책임:
/// - 절차 생성으로 개방된 방 소켓 주변의 남는 문/벽 흔적을 타일 패치 목록으로 보정한다.
/// - 테마별 생성 프로필이 동일한 소켓 후처리 규칙을 런타임 빌더와 에디터 미리보기에 공유하게 한다.
/// </summary>
[CreateAssetMenu(fileName = "RoomSocketCleanupProfile", menuName = "Gameplay/Dungeon/Room Socket Cleanup Profile")]
public sealed class RoomSocketCleanupProfileSO : ScriptableObject
{
    [SerializeField] private List<RoomSocketCleanupRule> rules = new();

    public IReadOnlyList<RoomSocketCleanupRule> Rules =>
        rules ?? (IReadOnlyList<RoomSocketCleanupRule>)Array.Empty<RoomSocketCleanupRule>();
}

/// <summary>
/// 책임:
/// - 하나의 소켓 방향/연결 상태 조건에서 어느 Tilemap 레이어에 어떤 패치를 찍을지 정의한다.
/// - 방향 공통 룰과 방향별 예외 룰을 함께 표현해 방 템플릿 수정 없이 문 흔적을 제거하게 한다.
/// </summary>
[Serializable]
public sealed class RoomSocketCleanupRule
{
    [SerializeField] private bool matchAnyDirection = true;
    [SerializeField] private RoomSocketDirection direction;
    [SerializeField] private RoomSocketCleanupConnectionFilter connectionFilter =
        RoomSocketCleanupConnectionFilter.Connected;
    [SerializeField] private RoomTileLayerKind layer = RoomTileLayerKind.Wall;
    [SerializeField] private List<RoomSocketCleanupTilePatch> patches = new();

    public bool MatchAnyDirection => matchAnyDirection;
    public RoomSocketDirection Direction => direction;
    public RoomSocketCleanupConnectionFilter ConnectionFilter => connectionFilter;
    public RoomTileLayerKind Layer => layer;
    public IReadOnlyList<RoomSocketCleanupTilePatch> Patches =>
        patches ?? (IReadOnlyList<RoomSocketCleanupTilePatch>)Array.Empty<RoomSocketCleanupTilePatch>();

    public bool Matches(RoomSocketDirection socketDirection, bool isConnected)
    {
        if (!matchAnyDirection && direction != socketDirection)
            return false;

        return connectionFilter switch
        {
            RoomSocketCleanupConnectionFilter.Connected => isConnected,
            RoomSocketCleanupConnectionFilter.Unconnected => !isConnected,
            RoomSocketCleanupConnectionFilter.Any => true,
            _ => false
        };
    }
}

/// <summary>
/// 책임:
/// - 소켓 시작 셀 기준 tangent/forward 좌표와 적용할 타일 소스를 함께 보관한다.
/// - x는 소켓 폭 방향, y는 소켓이 바라보는 바깥 방향으로 해석해 방향별 좌표 계산을 단순화한다.
/// </summary>
[Serializable]
public struct RoomSocketCleanupTilePatch
{
    [SerializeField] private Vector2Int socketSpaceOffset;
    [SerializeField] private RoomSocketCleanupTileSource tileSource;
    [SerializeField] private TileBase explicitTile;

    public Vector2Int SocketSpaceOffset => socketSpaceOffset;
    public RoomSocketCleanupTileSource TileSource => tileSource;
    public TileBase ExplicitTile => explicitTile;
}

/// <summary>
/// 책임 : 소켓 흔적 제거 룰이 연결된 소켓, 닫힌 소켓, 양쪽 모두 중 어디에 적용될지 구분한다.
/// </summary>
public enum RoomSocketCleanupConnectionFilter
{
    Connected = 0,
    Unconnected = 1,
    Any = 2
}

/// <summary>
/// 책임 : 소켓 흔적 제거 패치가 직접 지정 타일, 복도 기본 타일, 또는 셀 비우기 중 무엇을 적용할지 구분한다.
/// </summary>
public enum RoomSocketCleanupTileSource
{
    ExplicitTile = 0,
    Clear = 1,
    CorridorFloorTile = 2,
    CorridorWallTile = 3
}
