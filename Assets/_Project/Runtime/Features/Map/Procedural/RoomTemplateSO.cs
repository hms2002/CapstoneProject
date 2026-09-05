using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 책임:
/// - 절차 생성 던전에서 사용할 방 템플릿의 레이아웃 정보와 타일 구현 정보를 보관한다.
/// - Room Piece authoring 결과를 런타임 빌더가 읽을 수 있는 ScriptableObject 데이터로 제공한다.
/// </summary>
[CreateAssetMenu(fileName = "RoomTemplate", menuName = "Gameplay/Dungeon/Room Template")]
public sealed class RoomTemplateSO : ScriptableObject
{
    [SerializeField] private RoomLayoutData layoutData;
    [SerializeField] private RoomBuildData buildData;

    public RoomLayoutData LayoutData => layoutData;
    public RoomBuildData BuildData => buildData;

#if UNITY_EDITOR
    public void EditorSetData(RoomLayoutData layout, RoomBuildData build)
    {
        layoutData = layout;
        buildData = build;
    }
#endif
}

/// <summary>
/// 책임:
/// - 레이아웃 생성기가 방 배치 가능성을 판단하는 데 필요한 최소 방 정보를 보관한다.
/// - v0에서는 직사각형 예약 영역만 사용해 방 겹침 검사를 단순화한다.
/// </summary>
[Serializable]
public struct RoomLayoutData
{
    public string roomId;
    public RoomType roomType;
    public Vector2Int size;
    public RectInt localBounds;
    public List<RoomSocketData> sockets;
    public int difficultyTier;
    public float selectionWeight;
    public RoomCombatMetadata combatMetadata;
    public RoomTopologyPlacementData topologyPlacement;
}

/// <summary>
/// 책임:
/// - Combat 방 안에서 생성 규칙이 필터링할 보상 잠금 여부와 크기 성격을 보관한다.
/// - 기존 방 데이터는 Auto 값을 통해 BuildData와 예약 bounds 기반 추론을 유지한다.
/// </summary>
[Serializable]
public struct RoomCombatMetadata
{
    public RoomCombatSizeTag sizeTag;
    public RoomKillLockRewardTag killLockRewardTag;
}

/// <summary>
/// 책임:
/// - Combat 방의 제작 의도상 크기 분류를 표현한다.
/// - Auto는 기존 에셋 호환을 위해 방 bounds에서 Normal 또는 Large를 추론하게 한다.
/// </summary>
public enum RoomCombatSizeTag
{
    Auto = 0,
    Normal = 1,
    Large = 2
}

/// <summary>
/// 책임:
/// - Combat 방이 몬스터 처치로 열리는 보상 상자를 포함하는지 표현한다.
/// - Auto는 기존 에셋 호환을 위해 오브젝트 배치의 KillLock 연결 정보를 추론하게 한다.
/// </summary>
public enum RoomKillLockRewardTag
{
    Auto = 0,
    None = 1,
    Present = 2
}

/// <summary>
/// 책임:
/// - RoomTemplateSO의 새 Combat 메타데이터와 기존 BuildData 관례를 하나의 조회 규칙으로 정규화한다.
/// - 생성기와 제작 도구가 직렬화 버전에 상관없이 같은 필터링 결과를 사용하게 한다.
/// </summary>
public static class RoomTemplateCombatMetadataUtility
{
    private const int LargeRoomMinimumDimension = 40;
    private const int LargeRoomMinimumArea = 1600;

    public static RoomCombatSizeTag ResolveSizeTag(RoomTemplateSO template)
    {
        if (template == null)
            return RoomCombatSizeTag.Normal;

        RoomLayoutData layout = template.LayoutData;
        RoomCombatSizeTag authoredTag = layout.combatMetadata.sizeTag;
        if (authoredTag != RoomCombatSizeTag.Auto)
            return authoredTag;

        RectInt bounds = ResolveLocalBounds(layout);
        int area = Mathf.Max(0, bounds.width) * Mathf.Max(0, bounds.height);
        bool inferredLarge =
            bounds.width >= LargeRoomMinimumDimension ||
            bounds.height >= LargeRoomMinimumDimension ||
            area >= LargeRoomMinimumArea ||
            layout.difficultyTier > 0;
        return inferredLarge ? RoomCombatSizeTag.Large : RoomCombatSizeTag.Normal;
    }

    public static RoomKillLockRewardTag ResolveKillLockRewardTag(RoomTemplateSO template)
    {
        if (template == null)
            return RoomKillLockRewardTag.None;

        RoomKillLockRewardTag authoredTag =
            template.LayoutData.combatMetadata.killLockRewardTag;
        if (authoredTag != RoomKillLockRewardTag.Auto)
            return authoredTag;

        return HasInferredKillLockReward(template.BuildData.objectPlacements)
            ? RoomKillLockRewardTag.Present
            : RoomKillLockRewardTag.None;
    }

    private static bool HasInferredKillLockReward(
        IReadOnlyList<RoomObjectPlacementData> placements)
    {
        if (placements == null)
            return false;

        for (int i = 0; i < placements.Count; i++)
        {
            RoomObjectPlacementData placement = placements[i];
            if (placement.kind == RoomObjectKind.Monster &&
                !string.IsNullOrWhiteSpace(placement.linkedChestLockPlacementId))
            {
                return true;
            }

            if (placement.kind == RoomObjectKind.Chest &&
                !string.IsNullOrWhiteSpace(placement.placementId) &&
                placement.placementId.IndexOf(
                    "KillLock",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static RectInt ResolveLocalBounds(RoomLayoutData layout)
    {
        return layout.localBounds.width > 0 && layout.localBounds.height > 0
            ? layout.localBounds
            : new RectInt(Vector2Int.zero, layout.size);
    }
}

/// <summary>
/// 책임:
/// - 필수 방 템플릿이 그래프에서 만족해야 하는 위치 성격과 시작점 최소 거리를 보관한다.
/// - 콘텐츠별 배치 의도를 방 ID 하드코딩 없이 레이아웃 조립기에 전달한다.
/// </summary>
[Serializable]
public struct RoomTopologyPlacementData
{
    public RoomTopologyPlacementMode mode;
    [Min(0)] public int minimumGraphDistanceFromStart;
    public bool requireDeadEnd;
}

/// <summary>
/// 책임:
/// - 필수 방을 일반 분산 배치, 순환 지름길, 시작점 최원거리 중 어떤 기준으로 고를지 정의한다.
/// </summary>
public enum RoomTopologyPlacementMode
{
    Default = 0,
    CycleDetour = 1,
    FarthestFromStart = 2
}

/// <summary>
/// 책임:
/// - 방 예약 영역의 경계 시작 셀, 바깥 방향, 통로 폭을 한 쌍으로 보관한다.
/// - 레이아웃 생성기가 두 방의 2칸 연결 가능 여부와 정렬 위치를 계산할 때 사용하는 논리 연결 지점이다.
/// </summary>
[Serializable]
public struct RoomSocketData
{
    public string socketId;
    public Vector2Int localCell;
    public RoomSocketDirection direction;
    public int width;
}

/// <summary>
/// 책임:
/// - 방향별 소켓 진행축을 한 규칙으로 정의하고 논리 소켓이 차지하는 셀들을 계산한다.
/// - 제작 툴, 레이아웃 조립기, 런타임 빌더가 동일한 2칸 소켓 경계 검증을 공유하게 한다.
/// </summary>
public static class RoomSocketGeometry
{
    public const int RequiredWidth = 2;

    public static int ResolveWidth(RoomSocketData socket)
    {
        // width 필드가 없던 기존 직렬화 데이터의 0은 현재 기본 폭으로 마이그레이션한다.
        return socket.width == 0 ? RequiredWidth : socket.width;
    }

    public static Vector2Int GetTangent(RoomSocketDirection direction)
    {
        return direction switch
        {
            RoomSocketDirection.Up => Vector2Int.right,
            RoomSocketDirection.Down => Vector2Int.right,
            RoomSocketDirection.Right => Vector2Int.up,
            RoomSocketDirection.Left => Vector2Int.up,
            _ => Vector2Int.zero
        };
    }

    public static Vector2Int GetLocalCell(RoomSocketData socket, int cellIndex)
    {
        int width = ResolveWidth(socket);
        if (cellIndex < 0 || cellIndex >= width)
            throw new ArgumentOutOfRangeException(nameof(cellIndex));

        return socket.localCell + GetTangent(socket.direction) * cellIndex;
    }

    public static bool IsValid(RoomSocketData socket, RectInt bounds)
    {
        int width = ResolveWidth(socket);
        if (width != RequiredWidth || bounds.width <= 0 || bounds.height <= 0)
            return false;

        for (int cellIndex = 0; cellIndex < width; cellIndex++)
        {
            Vector2Int cell = GetLocalCell(socket, cellIndex);
            if (!bounds.Contains(cell) || !IsOnExpectedBoundary(cell, socket.direction, bounds))
                return false;
        }

        return true;
    }

    private static bool IsOnExpectedBoundary(
        Vector2Int cell,
        RoomSocketDirection direction,
        RectInt bounds)
    {
        return direction switch
        {
            RoomSocketDirection.Up => cell.y == bounds.yMax - 1,
            RoomSocketDirection.Right => cell.x == bounds.xMax - 1,
            RoomSocketDirection.Down => cell.y == bounds.yMin,
            RoomSocketDirection.Left => cell.x == bounds.xMin,
            _ => false
        };
    }
}

/// <summary>
/// 책임:
/// - 런타임 방 구현기가 실제 Tilemap에 찍어낼 고정 시각 레이어 타일과 프리팹 배치 데이터를 보관한다.
/// - 역할별 몬스터 스폰 위치, 상자, 포털, 일반 프롭의 로컬 배치 정보를 전달한다.
/// - 씬 연결 자체와 분리된 이동 endpoint 슬롯의 매개체 종류와 로컬 배치를 전달한다.
/// - 연결 문은 방 데이터가 아니라 조립 결과의 소켓 연결을 기준으로 DungeonRoomBuilder가 생성한다.
/// </summary>
[Serializable]
public struct RoomBuildData
{
    public List<RoomTileData> underFloorTiles;
    public List<RoomTileData> floorTiles;
    public List<RoomTileData> floorDetailTiles;
    public List<RoomTileData> groundDecorationTiles;
    public List<RoomTileData> wallTiles;
    public List<RoomTileData> wallDetailTiles;
    public List<RoomTileData> foregroundTiles;
    public List<RoomTileData> overlayFxTiles;
    public List<RoomObjectPlacementData> objectPlacements;
    public List<RoomTravelEndpointPlacementData> travelEndpointPlacements;

    public List<RoomTileData> GetTiles(RoomTileLayerKind layer)
    {
        return layer switch
        {
            RoomTileLayerKind.UnderFloor => underFloorTiles,
            RoomTileLayerKind.Floor => floorTiles,
            RoomTileLayerKind.FloorDetail => floorDetailTiles,
            RoomTileLayerKind.GroundDecoration => groundDecorationTiles,
            RoomTileLayerKind.Wall => wallTiles,
            RoomTileLayerKind.WallDetail => wallDetailTiles,
            RoomTileLayerKind.Foreground => foregroundTiles,
            RoomTileLayerKind.OverlayFX => overlayFxTiles,
            _ => null
        };
    }
}

/// <summary>
/// 책임 : 기획자가 방 조각에서 사용할 고정 Tilemap 슬롯을 직렬화와 툴 전반에 동일한 식별자로 제공한다.
/// </summary>
public enum RoomTileLayerKind
{
    UnderFloor = 0,
    Floor = 1,
    FloorDetail = 2,
    GroundDecoration = 3,
    Wall = 4,
    WallDetail = 5,
    Foreground = 6,
    OverlayFX = 7
}

/// <summary>
/// 책임:
/// - 고정 방 Tilemap 슬롯의 표시 순서, Hierarchy 이름, 렌더 정렬 및 물리 참여 규칙을 한 곳에서 정의한다.
/// - 제작 툴, 미리보기와 런타임 씬 설치기가 서로 다른 레이어 설정을 만들지 않게 한다.
/// </summary>
public static class RoomTileLayerContract
{
    private static readonly RoomTileLayerKind[] OrderedLayerValues =
    {
        RoomTileLayerKind.UnderFloor,
        RoomTileLayerKind.Floor,
        RoomTileLayerKind.FloorDetail,
        RoomTileLayerKind.GroundDecoration,
        RoomTileLayerKind.Wall,
        RoomTileLayerKind.WallDetail,
        RoomTileLayerKind.Foreground,
        RoomTileLayerKind.OverlayFX
    };

    public static IReadOnlyList<RoomTileLayerKind> OrderedLayers => OrderedLayerValues;

    public static string GetLayerName(RoomTileLayerKind layer)
    {
        return layer switch
        {
            RoomTileLayerKind.UnderFloor => "UnderFloor",
            RoomTileLayerKind.Floor => "Floor",
            RoomTileLayerKind.FloorDetail => "FloorDetail",
            RoomTileLayerKind.GroundDecoration => "GroundDecoration",
            RoomTileLayerKind.Wall => "Wall",
            RoomTileLayerKind.WallDetail => "WallDetail",
            RoomTileLayerKind.Foreground => "Foreground",
            RoomTileLayerKind.OverlayFX => "OverlayFX",
            _ => layer.ToString()
        };
    }

    public static string GetDisplayName(RoomTileLayerKind layer)
    {
        return layer switch
        {
            RoomTileLayerKind.UnderFloor => "바닥 아래 배경",
            RoomTileLayerKind.Floor => "기본 이동 바닥",
            RoomTileLayerKind.FloorDetail => "바닥 위 평면 장식",
            RoomTileLayerKind.GroundDecoration => "통과 가능한 입체 장식",
            RoomTileLayerKind.Wall => "기본 벽과 방 경계",
            RoomTileLayerKind.WallDetail => "벽 부착 장식",
            RoomTileLayerKind.Foreground => "캐릭터 앞 상단 장식",
            RoomTileLayerKind.OverlayFX => "화면·방 오버레이 효과",
            _ => GetLayerName(layer)
        };
    }

    public static int GetSortingOrder(RoomTileLayerKind layer)
    {
        return layer switch
        {
            RoomTileLayerKind.UnderFloor => 40,
            RoomTileLayerKind.Floor => 50,
            RoomTileLayerKind.FloorDetail => 51,
            RoomTileLayerKind.GroundDecoration => 52,
            RoomTileLayerKind.Wall => 60,
            RoomTileLayerKind.WallDetail => 61,
            RoomTileLayerKind.Foreground => 0,
            RoomTileLayerKind.OverlayFX => 10,
            _ => 0
        };
    }

    public static string GetSortingLayerName(RoomTileLayerKind layer)
    {
        return layer == RoomTileLayerKind.Foreground || layer == RoomTileLayerKind.OverlayFX
            ? "ForeGround"
            : "Default";
    }

    public static bool UsesGroundPhysicsLayer(RoomTileLayerKind layer)
    {
        return layer == RoomTileLayerKind.Floor || layer == RoomTileLayerKind.Wall;
    }

    public static bool RequiresCollider(RoomTileLayerKind layer)
    {
        return layer == RoomTileLayerKind.Wall;
    }
}

/// <summary>
/// 책임 : 방의 이동 슬롯이 상호작용, 자동 trigger, 도착 전용 중 어떤 매개체 동작을 제공하는지 정의한다.
/// </summary>
public enum RoomTravelEndpointKind
{
    Interaction = 0,
    Trigger = 1,
    ArrivalOnly = 2
}

/// <summary>
/// 책임 : 재사용 방 템플릿에 씬 연결을 직접 고정하지 않고 이동 슬롯의 안정 Id, 매개체와 출발·도착 로컬 배치를 저장한다.
/// </summary>
[Serializable]
public struct RoomTravelEndpointPlacementData
{
    public string slotId;
    public RoomTravelEndpointKind kind;
    public GameObject mediumPrefab;
    public Vector2Int localCell;
    public Vector2 localOffset;
    public float localRotationDegrees;
    public Vector3 localScale;
    public Vector2 triggerSize;
    public bool useSeparateArrivalPoint;
    public Vector2Int arrivalLocalCell;
    public Vector2 arrivalLocalOffset;
}

/// <summary>
/// 책임 : 이동 Trigger의 기획 크기를 매개체 Transform 크기와 분리하고, 기존 데이터 및 런타임 Collider 크기 변환 규칙을 제공한다.
/// </summary>
public static class RoomTravelEndpointGeometry
{
    public const float MinimumTriggerSize = 0.05f;

    public static Vector2 ResolveTriggerSize(RoomTravelEndpointPlacementData placement)
    {
        if (HasExplicitTriggerSize(placement.triggerSize))
            return SanitizeTriggerSize(placement.triggerSize);

        Vector3 legacyScale = placement.localScale;
        if (legacyScale == Vector3.zero)
        {
            legacyScale = placement.mediumPrefab != null
                ? placement.mediumPrefab.transform.localScale
                : Vector3.one;
        }

        return SanitizeTriggerSize(new Vector2(
            Mathf.Abs(legacyScale.x),
            Mathf.Abs(legacyScale.y)));
    }

    public static bool HasExplicitTriggerSize(Vector2 size)
    {
        return IsFinite(size.x) &&
               IsFinite(size.y) &&
               size.x > 0f &&
               size.y > 0f;
    }

    public static Vector2 SanitizeTriggerSize(Vector2 size)
    {
        return new Vector2(
            SanitizeDimension(size.x),
            SanitizeDimension(size.y));
    }

    public static Vector2 ResolveLocalColliderSize(
        Vector2 desiredWorldSize,
        Vector3 colliderLossyScale)
    {
        Vector2 sanitizedSize = SanitizeTriggerSize(desiredWorldSize);
        return new Vector2(
            sanitizedSize.x / Mathf.Max(0.0001f, Mathf.Abs(colliderLossyScale.x)),
            sanitizedSize.y / Mathf.Max(0.0001f, Mathf.Abs(colliderLossyScale.y)));
    }

    private static float SanitizeDimension(float value)
    {
        if (!IsFinite(value))
            return 1f;

        return Mathf.Max(MinimumTriggerSize, Mathf.Abs(value));
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

/// <summary>
/// 책임:
/// - 방에 배치되는 런타임 오브젝트의 식별자, 종류, 프리팹과 Grid 기준 위치/회전/크기를 보관한다.
/// - 몬스터 배치에는 공통 Warrior/Mage/Tank 역할 세트 또는 스테이지 고정 프리팹과 Kill Lock 상자의 안정적인 Placement Id를 보관한다.
/// - Monster 종류는 StageMonsterSetSO와 prefab 중 정확히 하나를 스폰 소스로 사용한다.
/// - DungeonRoomBuilder가 몬스터 배치는 지연 스폰 포인트로, 나머지는 프리팹 인스턴스로 구현할 수 있게 한다.
/// </summary>
[Serializable]
public struct RoomObjectPlacementData
{
    public string placementId;
    public RoomObjectKind kind;
    public GameObject prefab;
    public RoomMonsterSpawnRole monsterSpawnRole;
    public StageMonsterSetSO monsterStageSet;
    public Vector2Int localCell;
    public Vector2 localOffset;
    public float localRotationDegrees;
    public Vector3 localScale;
    public List<RoomObjectChildPoseOverrideData> childPoseOverrides;
    public string linkedChestLockPlacementId;
}

/// <summary>
/// 책임:
/// - 복합 방 오브젝트 프리팹의 안정적인 슬롯 하나에 적용할 방별 Transform 재정의를 보관한다.
/// - 재정의하지 않은 위치·회전·크기는 프리팹 기본값을 계속 따르게 한다.
/// </summary>
[Serializable]
public struct RoomObjectChildPoseOverrideData
{
    public string slotId;
    public bool overridePosition;
    public Vector3 localPosition;
    public bool overrideRotation;
    public float localRotationDegrees;
    public bool overrideScale;
    public Vector3 localScale;
}

/// <summary>
/// 책임:
/// - 기획자가 전투 방의 각 스폰 위치에 요구하는 전투 역할을 Warrior, Mage, Tank로 명시한다.
/// - 구체적인 몬스터 프리팹 선택은 역할에 연결된 StageMonsterSetSO와 현재 진행 단계에 맡긴다.
/// </summary>
public enum RoomMonsterSpawnRole
{
    Warrior = 0,
    Mage = 1,
    Tank = 2
}

/// <summary>
/// 책임:
/// - 방 오브젝트의 런타임 생성 정책과 제작 툴 검증 대상을 구분한다.
/// - 프리팹 자체의 세부 동작을 RoomTemplateSO에 중복 저장하지 않게 한다.
/// </summary>
public enum RoomObjectKind
{
    Prop = 0,
    Monster = 1,
    Chest = 2,
    Portal = 3
}

/// <summary>
/// 책임:
/// - 방 로컬 셀 좌표와 해당 위치에 배치할 TileBase 참조를 함께 보관한다.
/// - RoomTemplateSO를 런타임 단일 Grid/Tilemap에 재생성하기 위한 최소 타일 단위다.
/// </summary>
[Serializable]
public struct RoomTileData
{
    public Vector2Int localCell;
    public TileBase tile;
}

/// <summary>
/// 책임:
/// - 절차 생성 방의 큰 역할 분류를 표현한다.
/// - RoomLibrary가 후보를 나누고 LayoutGenerator가 필요한 역할의 방을 요청할 때 사용한다.
/// </summary>
public enum RoomType
{
    Combat,
    Start,
    Treasure,
    Shop,
    Event,
    Boss,
    Exit
}

/// <summary>
/// 책임:
/// - 방 소켓이 예약 영역 밖으로 향하는 네 방향을 표현한다.
/// - 레이아웃 생성기가 서로 반대 방향인 소켓만 연결하도록 판단하는 기준을 제공한다.
/// </summary>
public enum RoomSocketDirection
{
    Up = 0,
    Right = 1,
    Down = 2,
    Left = 3
}
