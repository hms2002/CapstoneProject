using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 책임:
/// - 같은 DungeonRoomBuilder가 런타임 전체 구현과 안전한 시각 미리보기를 구분해 실행하도록 생성 범위를 지정한다.
/// - 타일/복도 연결 규칙은 공유하되 문과 게임플레이 오브젝트의 생성 여부를 명시적으로 전달한다.
/// </summary>
public readonly struct DungeonBuildOptions
{
    public bool BuildConnectedDoors { get; }
    public bool BuildGameplayObjects { get; }
    public bool BuildDecorationObjects { get; }

    public static DungeonBuildOptions Full => new(true, true, true);
    public static DungeonBuildOptions VisualOnly => new(false, false, true);

    public DungeonBuildOptions(
        bool buildConnectedDoors,
        bool buildGameplayObjects,
        bool buildDecorationObjects)
    {
        BuildConnectedDoors = buildConnectedDoors;
        BuildGameplayObjects = buildGameplayObjects;
        BuildDecorationObjects = buildDecorationObjects;
    }
}

/// <summary>
/// 책임 : 재사용 RoomTemplate의 room Id·slot Id를 현재 씬의 SceneConnectionSO 한쪽 endpoint에 결합한다.
/// </summary>
[System.Serializable]
public struct ProceduralRoomTravelBinding
{
    [SerializeField] private string roomId;
    [SerializeField] private string slotId;
    [SerializeField] private SceneConnectionSO connection;
    [SerializeField] private SceneConnectionEndpointSide connectionSide;

    public string RoomId => roomId;
    public string SlotId => slotId;
    public SceneConnectionSO Connection => connection;
    public SceneConnectionEndpointSide ConnectionSide => connectionSide;
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(roomId) &&
        !string.IsNullOrWhiteSpace(slotId) &&
        connection != null;
}

/// <summary>
/// 책임:
/// - DungeonLayoutResult의 배치 좌표에 각 방의 고정 시각 Tilemap 슬롯과 연결 복도의 Floor/Wall, 런타임 오브젝트를 구현한다.
/// - 몬스터 배치는 기존 MonsterSpawner를 경유하고 상자·포털·프롭은 프리팹 인스턴스로 생성한다.
/// - 몬스터 배치가 지정한 상자 Placement Id를 기존 MonsterSpawnRequest의 Kill Lock 연결로 해석한다.
/// - 몬스터 방에는 기존 MonsterSpawnRoomGroup/RoomDoorMonsterKillLock 기반 encounter를 구성한다.
/// - 모든 2칸 소켓을 Wall 타일과 전용 물리 Collider로 닫은 뒤 연결이 확정된 소켓만 개방한다.
/// - 개방한 두 소켓 사이에 연결별 가변 길이의 2칸 폭 직선 복도를 만들고 양쪽 소켓 경계에 비영구 문을 하나씩 생성한다.
/// - 테마 씬에서 추출한 바닥/벽 변형 타일을 레이아웃 Seed와 셀 좌표로 결정해 같은 맵을 항상 같은 모습으로 구현한다.
/// - 테마 복도 장식 프로필의 레이어별 모듈과 Pivot 기반 GroundProp을 연결 길이와 방향에 맞춰 조합한다.
/// - 방 템플릿의 이동 슬롯을 씬별 연결 binding과 결합해 상호작용·trigger·도착 전용 endpoint로 구현한다.
/// - 모든 방 오브젝트 생성 후 로컬/던전 앵커를 수집하고 NPC 같은 런타임 방 기능의 외부 참조를 연결한다.
/// - 전투 여부와 무관하게 모든 생성 방에 미니맵 발견용 내부 진입 트리거를 구성한다.
/// - 전투 잠금 정책이 붙기 전 프로토타입에서는 생성 문을 열린 상태로 시작할 수 있게 한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class DungeonRoomBuilder : MonoBehaviour
{
    private const int RoomEntryBoundaryInsetCells = 1;

    [SerializeField] private Tilemap underFloorTilemap;
    [SerializeField] private Tilemap floorTilemap;
    [SerializeField] private Tilemap floorDetailTilemap;
    [SerializeField] private Tilemap groundDecorationTilemap;
    [SerializeField] private Tilemap wallTilemap;
    [SerializeField] private Tilemap wallDetailTilemap;
    [SerializeField] private Tilemap foregroundTilemap;
    [SerializeField] private Tilemap overlayFxTilemap;

    [Header("Corridors")]
    [SerializeField] private TileBase corridorFloorTile;
    [SerializeField] private TileBase corridorWallTile;
    [SerializeField] private List<TileBase> corridorFloorVariants = new();
    [SerializeField] private List<TileBase> horizontalCorridorWallVariants = new();
    [SerializeField] private List<TileBase> verticalCorridorWallVariants = new();
    [SerializeField] private CorridorDecorationProfileSO corridorDecorationProfile;

    [Header("Connected Doors")]
    [SerializeField] private DoorObject connectedDoorPrefab;
    [SerializeField] private Transform generatedDoorRoot;
    [SerializeField] private Transform generatedSocketBlockerRoot;
    [SerializeField] private bool openConnectedDoorsInitially = true;

    [Header("Room Objects")]
    [SerializeField] private Transform generatedObjectRoot;

    [Header("Travel Endpoints")]
    [SerializeField] private Transform generatedTravelEndpointRoot;
    [SerializeField] private List<ProceduralRoomTravelBinding> travelEndpointBindings = new();

    [Header("Room Encounters")]
    [SerializeField] private Transform generatedEncounterRoot;

    [Header("Map Discovery")]
    [SerializeField] private Transform generatedMapDiscoveryRoot;

    private readonly List<DoorObject> generatedDoors = new();
    private readonly List<BoxCollider2D> generatedSocketBlockers = new();
    private readonly Dictionary<long, BoxCollider2D> generatedSocketBlockersByEndpoint = new();
    private readonly List<GameObject> generatedRoomObjects = new();
    private readonly Dictionary<string, GameObject> generatedRoomObjectsByStateId =
        new(System.StringComparer.Ordinal);
    private readonly Dictionary<int, List<GameObject>> generatedRoomObjectsByPlacement = new();
    private readonly List<SceneTravelEndpoint> generatedTravelEndpoints = new();
    private readonly List<MonsterSpawnRoomGroup> generatedRoomEncounterGroups = new();
    private readonly List<RoomDoorMonsterKillLock> generatedRoomDoorLocks = new();
    private readonly Dictionary<int, MonsterSpawnRoomGroup> generatedRoomGroupsByPlacement = new();
    private readonly Dictionary<int, MonsterRoomArea2D> generatedRoomAreasByPlacement = new();

    public Tilemap UnderFloorTilemap => underFloorTilemap;
    public Tilemap FloorTilemap => floorTilemap;
    public Tilemap FloorDetailTilemap => floorDetailTilemap;
    public Tilemap GroundDecorationTilemap => groundDecorationTilemap;
    public Tilemap WallTilemap => wallTilemap;
    public Tilemap WallDetailTilemap => wallDetailTilemap;
    public Tilemap ForegroundTilemap => foregroundTilemap;
    public Tilemap OverlayFxTilemap => overlayFxTilemap;
    public bool HasCompleteTilemapSet
    {
        get
        {
            HashSet<Tilemap> uniqueTilemaps = new();
            for (int i = 0; i < RoomTileLayerContract.OrderedLayers.Count; i++)
            {
                Tilemap tilemap = GetTilemap(RoomTileLayerContract.OrderedLayers[i]);
                if (tilemap == null || !uniqueTilemaps.Add(tilemap))
                    return false;
            }

            return true;
        }
    }
    public TileBase CorridorFloorTile => corridorFloorTile;
    public TileBase CorridorWallTile => corridorWallTile;
    public IReadOnlyList<TileBase> CorridorFloorVariants => corridorFloorVariants;
    public IReadOnlyList<TileBase> HorizontalCorridorWallVariants => horizontalCorridorWallVariants;
    public IReadOnlyList<TileBase> VerticalCorridorWallVariants => verticalCorridorWallVariants;
    public CorridorDecorationProfileSO CorridorDecorationProfile => corridorDecorationProfile;
    public DoorObject ConnectedDoorPrefab => connectedDoorPrefab;
    public Transform GeneratedDoorRoot => generatedDoorRoot;
    public Transform GeneratedSocketBlockerRoot => generatedSocketBlockerRoot;
    public Transform GeneratedObjectRoot => generatedObjectRoot;
    public Transform GeneratedTravelEndpointRoot => generatedTravelEndpointRoot;
    public Transform GeneratedEncounterRoot => generatedEncounterRoot;
    public Transform GeneratedMapDiscoveryRoot => generatedMapDiscoveryRoot;
    public bool OpenConnectedDoorsInitially => openConnectedDoorsInitially;
    public IReadOnlyList<DoorObject> GeneratedDoors => generatedDoors;
    public IReadOnlyList<BoxCollider2D> GeneratedSocketBlockers => generatedSocketBlockers;
    public IReadOnlyList<GameObject> GeneratedRoomObjects => generatedRoomObjects;
    public IReadOnlyList<SceneTravelEndpoint> GeneratedTravelEndpoints => generatedTravelEndpoints;
    public IReadOnlyList<MonsterSpawnRoomGroup> GeneratedRoomEncounterGroups => generatedRoomEncounterGroups;
    public IReadOnlyList<RoomDoorMonsterKillLock> GeneratedRoomDoorLocks => generatedRoomDoorLocks;

    public Tilemap GetTilemap(RoomTileLayerKind layer)
    {
        return layer switch
        {
            RoomTileLayerKind.UnderFloor => underFloorTilemap,
            RoomTileLayerKind.Floor => floorTilemap,
            RoomTileLayerKind.FloorDetail => floorDetailTilemap,
            RoomTileLayerKind.GroundDecoration => groundDecorationTilemap,
            RoomTileLayerKind.Wall => wallTilemap,
            RoomTileLayerKind.WallDetail => wallDetailTilemap,
            RoomTileLayerKind.Foreground => foregroundTilemap,
            RoomTileLayerKind.OverlayFX => overlayFxTilemap,
            _ => null
        };
    }

#if UNITY_EDITOR
    public void EditorAssignTilemaps(Tilemap floor, Tilemap wall)
    {
        EditorAssignTilemaps(null, floor, null, null, wall, null, null, null);
    }

    public void EditorAssignTilemaps(
        Tilemap underFloor,
        Tilemap floor,
        Tilemap floorDetail,
        Tilemap groundDecoration,
        Tilemap wall,
        Tilemap wallDetail,
        Tilemap foreground,
        Tilemap overlayFx)
    {
        underFloorTilemap = underFloor;
        floorTilemap = floor;
        floorDetailTilemap = floorDetail;
        groundDecorationTilemap = groundDecoration;
        wallTilemap = wall;
        wallDetailTilemap = wallDetail;
        foregroundTilemap = foreground;
        overlayFxTilemap = overlayFx;
    }

    public void EditorAssignCorridorTiles(TileBase floorTile, TileBase wallTile)
    {
        corridorFloorTile = floorTile;
        corridorWallTile = wallTile;
    }

    public void EditorAssignCorridorTilePalette(
        IReadOnlyList<TileBase> floorVariants,
        IReadOnlyList<TileBase> horizontalWallVariants,
        IReadOnlyList<TileBase> verticalWallVariants)
    {
        CopyUniqueTiles(floorVariants, corridorFloorVariants);
        CopyUniqueTiles(horizontalWallVariants, horizontalCorridorWallVariants);
        CopyUniqueTiles(verticalWallVariants, verticalCorridorWallVariants);
    }

    /// <summary>
    /// 책임 : 런타임 생성기와 에디터 미리보기가 현재 테마의 복도 장식 프로필을 빌드 직전에 지정한다.
    /// </summary>
    public void ConfigureCorridorDecoration(CorridorDecorationProfileSO profile)
    {
        corridorDecorationProfile = profile;
    }

    private static void CopyUniqueTiles(
        IReadOnlyList<TileBase> source,
        List<TileBase> destination)
    {
        destination.Clear();
        if (source == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            TileBase tile = source[i];
            if (tile != null && !destination.Contains(tile))
                destination.Add(tile);
        }
    }

    public void EditorAssignConnectedDoorSetup(
        DoorObject doorPrefab,
        Transform doorRoot,
        bool shouldOpenInitially)
    {
        connectedDoorPrefab = doorPrefab;
        generatedDoorRoot = doorRoot;
        openConnectedDoorsInitially = shouldOpenInitially;
    }

    public void EditorAssignSocketBlockerRoot(Transform blockerRoot)
    {
        generatedSocketBlockerRoot = blockerRoot;
    }

    public void EditorAssignObjectRoot(Transform objectRoot)
    {
        generatedObjectRoot = objectRoot;
    }

    public void EditorAssignTravelEndpointRoot(Transform endpointRoot)
    {
        generatedTravelEndpointRoot = endpointRoot;
    }

    public void EditorAssignEncounterRoot(Transform encounterRoot)
    {
        generatedEncounterRoot = encounterRoot;
    }

    public void EditorAssignMapDiscoveryRoot(Transform discoveryRoot)
    {
        generatedMapDiscoveryRoot = discoveryRoot;
    }
#endif

    public bool TryGetGeneratedRoomEncounter(
        int roomPlacementId,
        out MonsterSpawnRoomGroup roomGroup,
        out MonsterRoomArea2D roomArea)
    {
        bool hasGroup = generatedRoomGroupsByPlacement.TryGetValue(roomPlacementId, out roomGroup);
        bool hasArea = generatedRoomAreasByPlacement.TryGetValue(roomPlacementId, out roomArea);
        return hasGroup && hasArea && roomGroup != null && roomArea != null;
    }

    /// <summary>
    /// 책임 : 현재 생성 오브젝트를 안정적인 방 배치/오브젝트 배치 Id 기준의 런 보존 DTO로 캡처한다.
    /// </summary>
    public List<DungeonObjectRuntimeStateData> CaptureGeneratedObjectStates()
    {
        var states = new List<DungeonObjectRuntimeStateData>(generatedRoomObjectsByStateId.Count);
        foreach (KeyValuePair<string, GameObject> pair in generatedRoomObjectsByStateId)
        {
            GameObject instance = pair.Value;
            TreasureChest chest = instance != null
                ? instance.GetComponentInChildren<TreasureChest>(includeInactive: true)
                : null;
            states.Add(new DungeonObjectRuntimeStateData
            {
                stateId = pair.Key,
                isPresent = instance != null,
                isActive = instance != null && instance.activeSelf,
                isChestOpened = chest != null && chest.IsOpened,
                chestLoot = chest != null && chest.IsOpened
                    ? chest.CaptureDungeonLootState()
                    : new List<DungeonChestLootRuntimeStateData>()
            });
        }

        return states;
    }

    /// <summary>
    /// 책임 : 보존된 생존·활성·상자 개봉 상태를 같은 seed로 다시 생성된 오브젝트에 적용한다.
    /// </summary>
    public void RestoreGeneratedObjectStates(IReadOnlyList<DungeonObjectRuntimeStateData> states)
    {
        if (states == null)
            return;

        for (int i = 0; i < states.Count; i++)
        {
            DungeonObjectRuntimeStateData state = states[i];
            if (state == null || string.IsNullOrWhiteSpace(state.stateId) ||
                !generatedRoomObjectsByStateId.TryGetValue(state.stateId, out GameObject instance) ||
                instance == null)
            {
                continue;
            }

            if (!state.isPresent)
            {
                if (Application.isPlaying)
                    Destroy(instance);
                else
                    DestroyImmediate(instance);
                continue;
            }

            TreasureChest chest = instance.GetComponentInChildren<TreasureChest>(includeInactive: true);
            if (state.isChestOpened && chest != null)
                chest.RestoreOpenedStateForDungeon(state.chestLoot);

            instance.SetActive(state.isActive);
        }
    }

    public bool TryBuild(DungeonLayoutResult layout)
    {
        return TryBuild(layout, DungeonBuildOptions.Full);
    }

    public bool TryBuild(DungeonLayoutResult layout, DungeonBuildOptions options)
    {
        if (layout == null)
        {
            Debug.LogError("DungeonRoomBuilder requires a layout result.", this);
            return false;
        }

        if (floorTilemap == null || wallTilemap == null)
        {
            Debug.LogError("DungeonRoomBuilder requires Floor and Wall Tilemap references.", this);
            return false;
        }

        if (floorTilemap == wallTilemap)
        {
            Debug.LogError("DungeonRoomBuilder Floor and Wall must be different Tilemaps.", this);
            return false;
        }

        if (!ValidateConfiguredTilemapsAreUnique())
            return false;

        if (!ValidateRequiredTilemaps(layout))
            return false;

        if (options.BuildGameplayObjects && !options.BuildConnectedDoors)
        {
            Debug.LogError(
                "DungeonRoomBuilder gameplay objects require connected doors for room encounter locks.",
                this);
            return false;
        }

        if (options.BuildConnectedDoors &&
            layout.Connections.Count > 0 &&
            connectedDoorPrefab == null)
        {
            Debug.LogError("DungeonRoomBuilder requires a connected Door prefab.", this);
            return false;
        }

        if (HasSpacedConnection(layout) &&
            (corridorFloorTile == null || corridorWallTile == null))
        {
            Debug.LogError(
                "DungeonRoomBuilder requires Floor and Wall tiles for spaced corridors.",
                this);
            return false;
        }

        ClearGeneratedContent();

        for (int i = 0; i < layout.Rooms.Count; i++)
        {
            DungeonRoomPlacement placement = layout.Rooms[i];
            RoomBuildData buildData = placement.Template.BuildData;
            PlaceRoomTileLayers(buildData, placement.Origin);
        }

        if (!ValidateAllSocketWallsBeforeOpening(layout))
        {
            ClearGeneratedContent();
            return false;
        }

        BuildClosedSocketBlockers(layout);

        for (int i = 0; i < layout.Connections.Count; i++)
        {
            DungeonSocketConnection connection = layout.Connections[i];
            if (!TryBuildConnection(
                    layout,
                    connection,
                    i,
                    options))
            {
                ClearGeneratedContent();
                return false;
            }
        }

        if (!ValidateUnconnectedSocketsRemainSealed(layout))
        {
            ClearGeneratedContent();
            return false;
        }

        if (options.BuildGameplayObjects &&
            (!TryBuildRoomEncounters(layout) ||
             !TryBuildRoomObjects(layout) ||
             !TryBuildTravelEndpoints(layout) ||
             !TryBindGeneratedRoomFeatures(layout) ||
             !TryBuildRoomDiscoveryTriggers(layout)))
        {
            ClearGeneratedContent();
            return false;
        }

        CompressConfiguredTilemaps();
        return true;
    }

    private bool ValidateConfiguredTilemapsAreUnique()
    {
        Dictionary<Tilemap, RoomTileLayerKind> assignedLayers = new();
        for (int i = 0; i < RoomTileLayerContract.OrderedLayers.Count; i++)
        {
            RoomTileLayerKind layer = RoomTileLayerContract.OrderedLayers[i];
            Tilemap tilemap = GetTilemap(layer);
            if (tilemap == null)
                continue;

            if (assignedLayers.TryGetValue(tilemap, out RoomTileLayerKind existingLayer))
            {
                Debug.LogError(
                    $"DungeonRoomBuilder {existingLayer} and {layer} must use different Tilemaps.",
                    this);
                return false;
            }

            assignedLayers.Add(tilemap, layer);
        }

        return true;
    }

    private bool ValidateRequiredTilemaps(DungeonLayoutResult layout)
    {
        for (int roomIndex = 0; roomIndex < layout.Rooms.Count; roomIndex++)
        {
            DungeonRoomPlacement placement = layout.Rooms[roomIndex];
            RoomBuildData buildData = placement.Template.BuildData;
            for (int layerIndex = 0;
                 layerIndex < RoomTileLayerContract.OrderedLayers.Count;
                 layerIndex++)
            {
                RoomTileLayerKind layer = RoomTileLayerContract.OrderedLayers[layerIndex];
                List<RoomTileData> tiles = buildData.GetTiles(layer);
                if (tiles == null || tiles.Count == 0 || GetTilemap(layer) != null)
                    continue;

                Debug.LogError(
                    $"DungeonRoomBuilder requires the {RoomTileLayerContract.GetLayerName(layer)} " +
                    $"Tilemap because room '{placement.Template.name}' contains that layer.",
                    this);
                return false;
            }
        }

        return true;
    }

    private void PlaceRoomTileLayers(RoomBuildData buildData, Vector2Int roomOrigin)
    {
        for (int i = 0; i < RoomTileLayerContract.OrderedLayers.Count; i++)
        {
            RoomTileLayerKind layer = RoomTileLayerContract.OrderedLayers[i];
            PlaceTiles(GetTilemap(layer), buildData.GetTiles(layer), roomOrigin);
        }
    }

    private void CompressConfiguredTilemaps()
    {
        HashSet<Tilemap> compressed = new();
        for (int i = 0; i < RoomTileLayerContract.OrderedLayers.Count; i++)
        {
            Tilemap tilemap = GetTilemap(RoomTileLayerContract.OrderedLayers[i]);
            if (tilemap != null && compressed.Add(tilemap))
                tilemap.CompressBounds();
        }
    }

    private bool ValidateAllSocketWallsBeforeOpening(DungeonLayoutResult layout)
    {
        for (int roomIndex = 0; roomIndex < layout.Rooms.Count; roomIndex++)
        {
            DungeonRoomPlacement placement = layout.Rooms[roomIndex];
            List<RoomSocketData> sockets = placement.Template.LayoutData.sockets;
            if (sockets == null)
                continue;

            for (int socketIndex = 0; socketIndex < sockets.Count; socketIndex++)
            {
                RoomSocketData socket = sockets[socketIndex];
                if (!RoomSocketGeometry.IsValid(
                        socket,
                        ResolveLocalBounds(placement.Template.LayoutData)))
                {
                    Debug.LogError(
                        $"Room '{placement.Template.name}' socket '{socket.socketId}' is not a valid " +
                        $"{RoomSocketGeometry.RequiredWidth}-cell boundary socket.",
                        placement.Template);
                    return false;
                }

                int width = RoomSocketGeometry.ResolveWidth(socket);
                for (int cellIndex = 0; cellIndex < width; cellIndex++)
                {
                    Vector2Int localCell = RoomSocketGeometry.GetLocalCell(socket, cellIndex);
                    Vector2Int worldCell = placement.Origin + localCell;
                    Vector3Int tileCell = new(worldCell.x, worldCell.y, 0);
                    if (floorTilemap.HasTile(tileCell) && wallTilemap.HasTile(tileCell))
                        continue;

                    Debug.LogError(
                        $"Room '{placement.Template.name}' socket '{socket.socketId}' cell {localCell} " +
                        "requires both Floor and closed Wall tiles.",
                        placement.Template);
                    return false;
                }
            }
        }

        return true;
    }

    private bool ValidateUnconnectedSocketsRemainSealed(DungeonLayoutResult layout)
    {
        for (int roomIndex = 0; roomIndex < layout.Rooms.Count; roomIndex++)
        {
            DungeonRoomPlacement placement = layout.Rooms[roomIndex];
            List<RoomSocketData> sockets = placement.Template.LayoutData.sockets;
            if (sockets == null)
                continue;

            for (int socketIndex = 0; socketIndex < sockets.Count; socketIndex++)
            {
                if (IsConnectedSocket(layout, placement.PlacementId, socketIndex))
                    continue;

                RoomSocketData socket = sockets[socketIndex];
                int width = RoomSocketGeometry.ResolveWidth(socket);
                for (int cellIndex = 0; cellIndex < width; cellIndex++)
                {
                    Vector2Int localCell = RoomSocketGeometry.GetLocalCell(socket, cellIndex);
                    Vector2Int worldCell = placement.Origin + localCell;
                    if (wallTilemap.HasTile(new Vector3Int(worldCell.x, worldCell.y, 0)))
                        continue;

                    Debug.LogError(
                        $"Room '{placement.Template.name}' unused socket '{socket.socketId}' " +
                        $"cell {localCell} was left open.",
                        placement.Template);
                    return false;
                }
            }
        }

        return true;
    }

    private void BuildClosedSocketBlockers(DungeonLayoutResult layout)
    {
        Transform blockerRoot = ResolveGeneratedSocketBlockerRoot();
        GridLayout grid = wallTilemap.layoutGrid;
        Vector3 gridCellSize = grid != null ? grid.cellSize : Vector3.one;
        Vector2 singleCellSize = new(
            Mathf.Max(0.01f, Mathf.Abs(gridCellSize.x)),
            Mathf.Max(0.01f, Mathf.Abs(gridCellSize.y)));

        for (int roomIndex = 0; roomIndex < layout.Rooms.Count; roomIndex++)
        {
            DungeonRoomPlacement placement = layout.Rooms[roomIndex];
            List<RoomSocketData> sockets = placement.Template.LayoutData.sockets;
            if (sockets == null)
                continue;

            for (int socketIndex = 0; socketIndex < sockets.Count; socketIndex++)
            {
                RoomSocketData socket = sockets[socketIndex];
                int width = RoomSocketGeometry.ResolveWidth(socket);
                Vector2Int firstWorldCell = placement.Origin +
                    RoomSocketGeometry.GetLocalCell(socket, 0);
                Vector2Int lastWorldCell = placement.Origin +
                    RoomSocketGeometry.GetLocalCell(socket, width - 1);
                Vector3 firstCenter = wallTilemap.GetCellCenterWorld(
                    new Vector3Int(firstWorldCell.x, firstWorldCell.y, 0));
                Vector3 lastCenter = wallTilemap.GetCellCenterWorld(
                    new Vector3Int(lastWorldCell.x, lastWorldCell.y, 0));

                Vector2 blockerSize = singleCellSize;
                Vector2Int tangent = RoomSocketGeometry.GetTangent(socket.direction);
                if (tangent.x != 0)
                    blockerSize.x *= width;
                else
                    blockerSize.y *= width;

                GameObject blockerObject = new(
                    $"ClosedSocketBlocker_{placement.PlacementId}_{socketIndex}");
                blockerObject.layer = wallTilemap.gameObject.layer;
                blockerObject.transform.SetParent(blockerRoot, false);
                blockerObject.transform.SetPositionAndRotation(
                    Vector3.Lerp(firstCenter, lastCenter, 0.5f),
                    wallTilemap.transform.rotation);

                BoxCollider2D blocker = blockerObject.AddComponent<BoxCollider2D>();
                blocker.size = blockerSize;
                generatedSocketBlockers.Add(blocker);
                generatedSocketBlockersByEndpoint.Add(
                    GetSocketEndpointKey(placement.PlacementId, socketIndex),
                    blocker);
            }
        }
    }

    private static bool IsConnectedSocket(
        DungeonLayoutResult layout,
        int roomPlacementId,
        int socketIndex)
    {
        for (int i = 0; i < layout.Connections.Count; i++)
        {
            DungeonSocketConnection connection = layout.Connections[i];
            if ((connection.FirstRoomPlacementId == roomPlacementId &&
                 connection.FirstSocketIndex == socketIndex) ||
                (connection.SecondRoomPlacementId == roomPlacementId &&
                 connection.SecondSocketIndex == socketIndex))
            {
                return true;
            }
        }

        return false;
    }

    public void ClearGeneratedContent()
    {
        ClearGeneratedTiles();
        ClearGeneratedRoomEncounters();
        ClearGeneratedDoors();
        ClearGeneratedSocketBlockers();
        ClearGeneratedRoomObjects();
        ClearGeneratedTravelEndpoints();
        ClearGeneratedMapDiscoveryTriggers();
    }

    public void ClearGeneratedTiles()
    {
        HashSet<Tilemap> cleared = new();
        for (int i = 0; i < RoomTileLayerContract.OrderedLayers.Count; i++)
        {
            Tilemap tilemap = GetTilemap(RoomTileLayerContract.OrderedLayers[i]);
            if (tilemap != null && cleared.Add(tilemap))
                tilemap.ClearAllTiles();
        }
    }

    public void ClearGeneratedDoors()
    {
        generatedDoors.Clear();

        if (generatedDoorRoot == null)
            return;

        for (int i = generatedDoorRoot.childCount - 1; i >= 0; i--)
        {
            GameObject child = generatedDoorRoot.GetChild(i).gameObject;
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }
    }

    public void ClearGeneratedSocketBlockers()
    {
        generatedSocketBlockers.Clear();
        generatedSocketBlockersByEndpoint.Clear();

        if (generatedSocketBlockerRoot == null)
            return;

        for (int i = generatedSocketBlockerRoot.childCount - 1; i >= 0; i--)
        {
            GameObject child = generatedSocketBlockerRoot.GetChild(i).gameObject;
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }
    }

    public void ClearGeneratedRoomObjects()
    {
        generatedRoomObjects.Clear();
        generatedRoomObjectsByStateId.Clear();
        generatedRoomObjectsByPlacement.Clear();

        if (generatedObjectRoot == null)
            return;

        for (int i = generatedObjectRoot.childCount - 1; i >= 0; i--)
        {
            GameObject child = generatedObjectRoot.GetChild(i).gameObject;
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }
    }

    public void ClearGeneratedTravelEndpoints()
    {
        generatedTravelEndpoints.Clear();

        if (generatedTravelEndpointRoot == null)
            return;

        for (int i = generatedTravelEndpointRoot.childCount - 1; i >= 0; i--)
        {
            GameObject child = generatedTravelEndpointRoot.GetChild(i).gameObject;
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }
    }

    public void ClearGeneratedRoomEncounters()
    {
        generatedRoomEncounterGroups.Clear();
        generatedRoomDoorLocks.Clear();
        generatedRoomGroupsByPlacement.Clear();
        generatedRoomAreasByPlacement.Clear();

        if (generatedEncounterRoot == null)
            return;

        for (int i = generatedEncounterRoot.childCount - 1; i >= 0; i--)
        {
            GameObject child = generatedEncounterRoot.GetChild(i).gameObject;
            if (Application.isPlaying)
            {
                child.SetActive(false);
                Destroy(child);
            }
            else
            {
                DestroyImmediate(child);
            }
        }
    }

    public void ClearGeneratedMapDiscoveryTriggers()
    {
        if (generatedMapDiscoveryRoot == null)
            return;

        for (int i = generatedMapDiscoveryRoot.childCount - 1; i >= 0; i--)
        {
            GameObject child = generatedMapDiscoveryRoot.GetChild(i).gameObject;
            if (Application.isPlaying)
            {
                child.SetActive(false);
                Destroy(child);
            }
            else
            {
                DestroyImmediate(child);
            }
        }
    }

    private bool TryBuildRoomDiscoveryTriggers(DungeonLayoutResult layout)
    {
        DungeonMapRuntimeController mapRuntime = GetComponent<DungeonMapRuntimeController>();
        if (mapRuntime == null)
            return true;

        Transform discoveryRoot = ResolveGeneratedMapDiscoveryRoot();
        for (int roomIndex = 0; roomIndex < layout.Rooms.Count; roomIndex++)
        {
            DungeonRoomPlacement roomPlacement = layout.Rooms[roomIndex];
            int boundaryInset = roomPlacement.WorldBounds.width > RoomEntryBoundaryInsetCells * 2 &&
                                roomPlacement.WorldBounds.height > RoomEntryBoundaryInsetCells * 2
                ? RoomEntryBoundaryInsetCells
                : 0;

            GameObject triggerObject = new($"RoomDiscovery_{roomPlacement.PlacementId}");
            triggerObject.SetActive(false);
            triggerObject.transform.SetParent(discoveryRoot, false);
            PolygonCollider2D areaCollider = triggerObject.AddComponent<PolygonCollider2D>();
            areaCollider.isTrigger = true;
            areaCollider.points = CreateRoomAreaPoints(
                triggerObject.transform,
                roomPlacement.WorldBounds,
                boundaryInset);

            if (areaCollider.points.Length < 3)
            {
                Debug.LogError(
                    $"Room {roomPlacement.PlacementId} could not create a valid map discovery area.",
                    this);
                return false;
            }

            DungeonRoomDiscoveryTrigger2D discoveryTrigger =
                triggerObject.AddComponent<DungeonRoomDiscoveryTrigger2D>();
            discoveryTrigger.Configure(mapRuntime, roomPlacement.PlacementId);
            triggerObject.SetActive(true);
        }

        return true;
    }

    private bool TryBuildRoomEncounters(DungeonLayoutResult layout)
    {
        for (int roomIndex = 0; roomIndex < layout.Rooms.Count; roomIndex++)
        {
            DungeonRoomPlacement roomPlacement = layout.Rooms[roomIndex];
            if (!HasMonsterPlacement(roomPlacement.Template.BuildData.objectPlacements))
                continue;

            if (!TryCreateRoomEncounter(
                    roomPlacement,
                    out MonsterSpawnRoomGroup roomGroup,
                    out MonsterRoomArea2D roomArea))
            {
                return false;
            }

            generatedRoomEncounterGroups.Add(roomGroup);
            generatedRoomGroupsByPlacement.Add(roomPlacement.PlacementId, roomGroup);
            generatedRoomAreasByPlacement.Add(roomPlacement.PlacementId, roomArea);
        }

        if (generatedDoors.Count != layout.Connections.Count * 2)
        {
            Debug.LogError(
                "DungeonRoomBuilder cannot map generated endpoint doors to room encounters.",
                this);
            return false;
        }

        for (int connectionIndex = 0; connectionIndex < layout.Connections.Count; connectionIndex++)
        {
            DungeonSocketConnection connection = layout.Connections[connectionIndex];
            if (!TryAttachRoomDoorLock(
                    layout,
                    connection.FirstRoomPlacementId,
                    connection.FirstSocketIndex,
                    generatedDoors[connectionIndex * 2]))
            {
                return false;
            }

            if (!TryAttachRoomDoorLock(
                    layout,
                    connection.SecondRoomPlacementId,
                    connection.SecondSocketIndex,
                    generatedDoors[connectionIndex * 2 + 1]))
            {
                return false;
            }
        }

        return true;
    }

    private bool TryCreateRoomEncounter(
        DungeonRoomPlacement roomPlacement,
        out MonsterSpawnRoomGroup roomGroup,
        out MonsterRoomArea2D roomArea)
    {
        roomGroup = null;
        roomArea = null;

        if (roomPlacement.WorldBounds.width <= RoomEntryBoundaryInsetCells * 2 ||
            roomPlacement.WorldBounds.height <= RoomEntryBoundaryInsetCells * 2)
        {
            Debug.LogError(
                $"Monster room {roomPlacement.PlacementId} is too small for an interior entry trigger.",
                this);
            return false;
        }

        GameObject encounterObject = new($"RoomEncounter_{roomPlacement.PlacementId}");
        encounterObject.SetActive(false);
        encounterObject.transform.SetParent(ResolveGeneratedEncounterRoot(), false);

        PolygonCollider2D areaCollider = encounterObject.AddComponent<PolygonCollider2D>();
        areaCollider.isTrigger = true;
        areaCollider.points = CreateRoomAreaPoints(
            encounterObject.transform,
            roomPlacement.WorldBounds,
            boundaryInsetCells: 0);

        roomGroup = encounterObject.AddComponent<MonsterSpawnRoomGroup>();
        roomArea = encounterObject.AddComponent<MonsterRoomArea2D>();
        roomArea.Configure(areaCollider);

        GameObject entryTriggerObject = new("InteriorEntryTrigger");
        entryTriggerObject.SetActive(false);
        entryTriggerObject.transform.SetParent(encounterObject.transform, false);
        PolygonCollider2D entryCollider = entryTriggerObject.AddComponent<PolygonCollider2D>();
        entryCollider.isTrigger = true;
        entryCollider.points = CreateRoomAreaPoints(
            entryTriggerObject.transform,
            roomPlacement.WorldBounds,
            RoomEntryBoundaryInsetCells);
        RoomEncounterEntryTrigger2D entryTrigger =
            entryTriggerObject.AddComponent<RoomEncounterEntryTrigger2D>();
        entryTrigger.Configure(roomGroup);
        entryTriggerObject.SetActive(true);
        encounterObject.SetActive(true);

        if (areaCollider.points.Length >= 3 && entryCollider.points.Length >= 3)
            return true;

        Debug.LogError(
            $"Room {roomPlacement.PlacementId} could not create a valid monster encounter area.",
            this);
        return false;
    }

    private Vector2[] CreateRoomAreaPoints(
        Transform targetTransform,
        RectInt worldBounds,
        int boundaryInsetCells)
    {
        Vector3Int bottomLeftCell = new(
            worldBounds.xMin + boundaryInsetCells,
            worldBounds.yMin + boundaryInsetCells,
            0);
        Vector3Int bottomRightCell = new(
            worldBounds.xMax - boundaryInsetCells,
            worldBounds.yMin + boundaryInsetCells,
            0);
        Vector3Int topRightCell = new(
            worldBounds.xMax - boundaryInsetCells,
            worldBounds.yMax - boundaryInsetCells,
            0);
        Vector3Int topLeftCell = new(
            worldBounds.xMin + boundaryInsetCells,
            worldBounds.yMax - boundaryInsetCells,
            0);

        return new[]
        {
            (Vector2)targetTransform.InverseTransformPoint(floorTilemap.CellToWorld(bottomLeftCell)),
            (Vector2)targetTransform.InverseTransformPoint(floorTilemap.CellToWorld(bottomRightCell)),
            (Vector2)targetTransform.InverseTransformPoint(floorTilemap.CellToWorld(topRightCell)),
            (Vector2)targetTransform.InverseTransformPoint(floorTilemap.CellToWorld(topLeftCell))
        };
    }

    private bool TryAttachRoomDoorLock(
        DungeonLayoutResult layout,
        int roomPlacementId,
        int socketIndex,
        DoorObject door)
    {
        if (!generatedRoomGroupsByPlacement.TryGetValue(
                roomPlacementId,
                out MonsterSpawnRoomGroup roomGroup))
        {
            return true;
        }

        if (door == null)
        {
            Debug.LogError(
                $"Monster room {roomPlacementId} references a missing generated endpoint door.",
                this);
            return false;
        }

        if (!TryGetPlacedSocket(
                layout,
                roomPlacementId,
                socketIndex,
                out _,
                out RoomSocketData socket))
        {
            Debug.LogError(
                $"Monster room {roomPlacementId} references an invalid door socket {socketIndex}.",
                this);
            return false;
        }

        GameObject lockObject = new($"DoorKillLock_{door.name}");
        lockObject.SetActive(false);
        lockObject.transform.SetParent(roomGroup.transform, false);
        RoomDoorMonsterKillLock doorLock = lockObject.AddComponent<RoomDoorMonsterKillLock>();
        doorLock.Configure(door, roomGroup, -DirectionToVector(socket.direction));
        lockObject.SetActive(true);
        generatedRoomDoorLocks.Add(doorLock);
        return true;
    }

    private static bool HasMonsterPlacement(IReadOnlyList<RoomObjectPlacementData> placements)
    {
        if (placements == null)
            return false;

        for (int i = 0; i < placements.Count; i++)
        {
            if (placements[i].kind == RoomObjectKind.Monster)
                return true;
        }

        return false;
    }

    private bool TryBuildRoomObjects(DungeonLayoutResult layout)
    {
        for (int roomIndex = 0; roomIndex < layout.Rooms.Count; roomIndex++)
        {
            DungeonRoomPlacement roomPlacement = layout.Rooms[roomIndex];
            List<RoomObjectPlacementData> objectPlacements =
                roomPlacement.Template.BuildData.objectPlacements;
            if (objectPlacements == null)
                continue;

            if (!TryValidateRoomObjectPlacements(
                    roomPlacement,
                    objectPlacements,
                    out Dictionary<string, int> placementIndices))
            {
                return false;
            }

            GameObject[] roomInstances = new GameObject[objectPlacements.Count];
            TryGetGeneratedRoomEncounter(
                roomPlacement.PlacementId,
                out MonsterSpawnRoomGroup roomGroup,
                out MonsterRoomArea2D roomArea);
            for (int objectIndex = 0; objectIndex < objectPlacements.Count; objectIndex++)
            {
                RoomObjectPlacementData objectPlacement = objectPlacements[objectIndex];
                if (objectPlacement.kind == RoomObjectKind.Monster)
                    continue;

                if (!TryBuildRoomObject(
                        roomPlacement,
                        objectPlacement,
                        null,
                        null,
                        null,
                        out roomInstances[objectIndex]))
                {
                    return false;
                }
            }

            for (int objectIndex = 0; objectIndex < objectPlacements.Count; objectIndex++)
            {
                RoomObjectPlacementData objectPlacement = objectPlacements[objectIndex];
                if (objectPlacement.kind != RoomObjectKind.Monster)
                    continue;

                if (!TryResolveLinkedChestLock(
                        roomPlacement,
                        objectPlacement,
                        objectPlacements,
                        placementIndices,
                        roomInstances,
                        out ChestMonsterKillLock linkedChestLock))
                {
                    return false;
                }

                if (!TryBuildRoomObject(
                        roomPlacement,
                        objectPlacement,
                        linkedChestLock,
                        roomArea,
                        roomGroup,
                        out roomInstances[objectIndex]))
                {
                    return false;
                }
            }

            for (int objectIndex = 0; objectIndex < roomInstances.Length; objectIndex++)
            {
                GameObject roomInstance = roomInstances[objectIndex];
                generatedRoomObjects.Add(roomInstance);
                generatedRoomObjectsByStateId[CreateRuntimeStateId(
                    roomPlacement.PlacementId,
                    objectPlacements[objectIndex].placementId)] = roomInstance;

                if (!generatedRoomObjectsByPlacement.TryGetValue(
                        roomPlacement.PlacementId,
                        out List<GameObject> roomObjectInstances))
                {
                    roomObjectInstances = new List<GameObject>();
                    generatedRoomObjectsByPlacement.Add(
                        roomPlacement.PlacementId,
                        roomObjectInstances);
                }

                roomObjectInstances.Add(roomInstance);
            }
        }

        return true;
    }

    private bool TryBindGeneratedRoomFeatures(DungeonLayoutResult layout)
    {
        var localAnchorsByPlacement =
            new Dictionary<int, Dictionary<string, Transform>>();
        var dungeonAnchors = new Dictionary<string, Transform>(System.StringComparer.Ordinal);

        for (int roomIndex = 0; roomIndex < layout.Rooms.Count; roomIndex++)
        {
            DungeonRoomPlacement roomPlacement = layout.Rooms[roomIndex];
            var localAnchors = new Dictionary<string, Transform>(System.StringComparer.Ordinal);
            localAnchorsByPlacement.Add(roomPlacement.PlacementId, localAnchors);

            if (!generatedRoomObjectsByPlacement.TryGetValue(
                    roomPlacement.PlacementId,
                    out List<GameObject> roomObjects))
            {
                continue;
            }

            for (int objectIndex = 0; objectIndex < roomObjects.Count; objectIndex++)
            {
                GameObject roomObject = roomObjects[objectIndex];
                if (roomObject == null)
                    continue;

                ProceduralRoomAnchor[] anchors =
                    roomObject.GetComponentsInChildren<ProceduralRoomAnchor>(includeInactive: true);
                for (int anchorIndex = 0; anchorIndex < anchors.Length; anchorIndex++)
                {
                    if (!TryRegisterGeneratedRoomAnchor(
                            roomPlacement,
                            anchors[anchorIndex],
                            localAnchors,
                            dungeonAnchors))
                    {
                        return false;
                    }
                }
            }
        }

        for (int roomIndex = 0; roomIndex < layout.Rooms.Count; roomIndex++)
        {
            DungeonRoomPlacement roomPlacement = layout.Rooms[roomIndex];
            if (!generatedRoomObjectsByPlacement.TryGetValue(
                    roomPlacement.PlacementId,
                    out List<GameObject> roomObjects))
            {
                continue;
            }

            var context = new ProceduralRoomRuntimeContext(
                roomPlacement.PlacementId,
                roomPlacement.Template,
                localAnchorsByPlacement[roomPlacement.PlacementId],
                dungeonAnchors,
                CollectConnectedSocketDirections(layout, roomPlacement));
            for (int objectIndex = 0; objectIndex < roomObjects.Count; objectIndex++)
            {
                GameObject roomObject = roomObjects[objectIndex];
                if (roomObject == null)
                    continue;

                MonoBehaviour[] components =
                    roomObject.GetComponentsInChildren<MonoBehaviour>(includeInactive: true);
                for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
                {
                    MonoBehaviour component = components[componentIndex];
                    if (component is not IProceduralRoomRuntimeFeature feature)
                        continue;

                    if (feature.TryBindProceduralRoom(context, out string failureReason))
                        continue;

                    Debug.LogError(
                        $"Room '{roomPlacement.Template.name}' runtime feature " +
                        $"'{component.GetType().Name}' could not bind: {failureReason}",
                        component);
                    return false;
                }
            }
        }

        return true;
    }

    private static IReadOnlyList<RoomSocketDirection> CollectConnectedSocketDirections(
        DungeonLayoutResult layout,
        DungeonRoomPlacement roomPlacement)
    {
        var directions = new List<RoomSocketDirection>();
        IReadOnlyList<DungeonSocketConnection> connections = layout.Connections;
        IReadOnlyList<RoomSocketData> sockets = roomPlacement.Template.LayoutData.sockets;
        for (int connectionIndex = 0; connectionIndex < connections.Count; connectionIndex++)
        {
            DungeonSocketConnection connection = connections[connectionIndex];
            int socketIndex = connection.FirstRoomPlacementId == roomPlacement.PlacementId
                ? connection.FirstSocketIndex
                : connection.SecondRoomPlacementId == roomPlacement.PlacementId
                    ? connection.SecondSocketIndex
                    : -1;
            if (socketIndex < 0 || socketIndex >= sockets.Count)
                continue;

            RoomSocketDirection direction = sockets[socketIndex].direction;
            if (!directions.Contains(direction))
                directions.Add(direction);
        }

        return directions;
    }

    private static bool TryRegisterGeneratedRoomAnchor(
        DungeonRoomPlacement roomPlacement,
        ProceduralRoomAnchor anchor,
        Dictionary<string, Transform> localAnchors,
        Dictionary<string, Transform> dungeonAnchors)
    {
        if (anchor == null)
            return true;

        if (string.IsNullOrWhiteSpace(anchor.SlotId))
        {
            Debug.LogError(
                $"Room '{roomPlacement.Template.name}' contains an empty procedural anchor slot Id.",
                anchor);
            return false;
        }

        Dictionary<string, Transform> destination =
            anchor.Scope == ProceduralRoomAnchorScope.Dungeon
                ? dungeonAnchors
                : localAnchors;
        if (destination.TryAdd(anchor.SlotId, anchor.Target))
            return true;

        string scopeLabel = anchor.Scope == ProceduralRoomAnchorScope.Dungeon
            ? "generated dungeon"
            : $"room placement {roomPlacement.PlacementId}";
        Debug.LogError(
            $"Room '{roomPlacement.Template.name}' has duplicate procedural anchor slot " +
            $"'{anchor.SlotId}' in {scopeLabel} scope.",
            anchor);
        return false;
    }

    private static string CreateRuntimeStateId(int roomPlacementId, string objectPlacementId)
    {
        return $"{roomPlacementId}:{objectPlacementId}";
    }

    private bool TryBuildTravelEndpoints(DungeonLayoutResult layout)
    {
        for (int roomIndex = 0; roomIndex < layout.Rooms.Count; roomIndex++)
        {
            DungeonRoomPlacement roomPlacement = layout.Rooms[roomIndex];
            List<RoomTravelEndpointPlacementData> placements =
                roomPlacement.Template.BuildData.travelEndpointPlacements;
            if (placements == null || placements.Count == 0)
                continue;

            var slotIds = new HashSet<string>(System.StringComparer.Ordinal);
            for (int placementIndex = 0; placementIndex < placements.Count; placementIndex++)
            {
                RoomTravelEndpointPlacementData placement = placements[placementIndex];
                if (string.IsNullOrWhiteSpace(placement.slotId) || !slotIds.Add(placement.slotId))
                {
                    Debug.LogError(
                        $"Room '{roomPlacement.Template.name}' has an empty or duplicate travel slot Id '{placement.slotId}'.",
                        roomPlacement.Template);
                    return false;
                }

                if (!TryBuildTravelEndpoint(roomPlacement, placement, out SceneTravelEndpoint endpoint))
                    return false;

                generatedTravelEndpoints.Add(endpoint);
            }
        }

        return true;
    }

    private bool TryBuildTravelEndpoint(
        DungeonRoomPlacement roomPlacement,
        RoomTravelEndpointPlacementData placement,
        out SceneTravelEndpoint endpoint)
    {
        endpoint = null;
        Vector2Int worldCell = roomPlacement.Origin + placement.localCell;
        Vector3Int tileCell = new(worldCell.x, worldCell.y, 0);
        if (!floorTilemap.HasTile(tileCell))
        {
            Debug.LogError(
                $"Room '{roomPlacement.Template.name}' travel slot '{placement.slotId}' requires a Floor tile at {placement.localCell}.",
                roomPlacement.Template);
            return false;
        }

        GridLayout grid = floorTilemap.layoutGrid;
        Vector3 worldOffset = grid != null
            ? grid.transform.TransformVector(new Vector3(placement.localOffset.x, placement.localOffset.y, 0f))
            : new Vector3(placement.localOffset.x, placement.localOffset.y, 0f);
        Vector3 worldPosition = floorTilemap.GetCellCenterWorld(tileCell) + worldOffset;
        Quaternion worldRotation = (grid != null ? grid.transform.rotation : Quaternion.identity) *
                                   Quaternion.Euler(0f, 0f, placement.localRotationDegrees);
        Transform endpointRoot = ResolveGeneratedTravelEndpointRoot();
        GameObject instance = placement.mediumPrefab != null
            ? Instantiate(placement.mediumPrefab, worldPosition, worldRotation, endpointRoot)
            : new GameObject();

        if (placement.mediumPrefab == null)
        {
            instance.name = $"TravelEndpoint_{roomPlacement.PlacementId}_{placement.slotId}";
            instance.transform.SetParent(endpointRoot, false);
            instance.transform.SetPositionAndRotation(worldPosition, worldRotation);
        }
        else
        {
            instance.name = $"TravelEndpoint_{roomPlacement.PlacementId}_{placement.slotId}_{placement.mediumPrefab.name}";
        }

        instance.transform.localScale = placement.localScale == Vector3.zero
            ? placement.mediumPrefab != null ? placement.mediumPrefab.transform.localScale : Vector3.one
            : placement.localScale;

        endpoint = instance.GetComponentInChildren<SceneTravelEndpoint>(includeInactive: true);
        if (endpoint == null)
            endpoint = instance.AddComponent<SceneTravelEndpoint>();
        endpoint.ConfigureRuntimeSlot(placement.slotId);
        if (placement.useSeparateArrivalPoint)
        {
            Vector2Int arrivalWorldCell = roomPlacement.Origin + placement.arrivalLocalCell;
            Vector3Int arrivalTileCell = new(arrivalWorldCell.x, arrivalWorldCell.y, 0);
            if (!floorTilemap.HasTile(arrivalTileCell))
            {
                Debug.LogError(
                    $"Room '{roomPlacement.Template.name}' travel slot '{placement.slotId}' " +
                    $"requires a Floor tile at separate arrival cell {placement.arrivalLocalCell}.",
                    roomPlacement.Template);
                Destroy(instance);
                endpoint = null;
                return false;
            }

            Vector3 arrivalWorldOffset = grid != null
                ? grid.transform.TransformVector(new Vector3(
                    placement.arrivalLocalOffset.x,
                    placement.arrivalLocalOffset.y,
                    0f))
                : new Vector3(
                    placement.arrivalLocalOffset.x,
                    placement.arrivalLocalOffset.y,
                    0f);
            Vector3 arrivalWorldPosition =
                floorTilemap.GetCellCenterWorld(arrivalTileCell) + arrivalWorldOffset;
            GameObject arrivalAnchorObject = new("ArrivalAnchor");
            arrivalAnchorObject.transform.SetParent(instance.transform, worldPositionStays: true);
            arrivalAnchorObject.transform.SetPositionAndRotation(
                arrivalWorldPosition,
                worldRotation);
            endpoint.ConfigureRuntimeArrivalAnchor(arrivalAnchorObject.transform);
        }
        ConfigureTravelMedium(
            endpoint.gameObject,
            placement.kind,
            RoomTravelEndpointGeometry.ResolveTriggerSize(placement));

        ProceduralRoomTravelBinding binding = FindTravelBinding(
            roomPlacement.Template.LayoutData.roomId,
            placement.slotId);
        if (!binding.IsConfigured)
        {
            SetTravelMediumEnabled(endpoint.gameObject, false);
            Debug.LogWarning(
                $"[DungeonRoomBuilder] Travel slot '{roomPlacement.Template.LayoutData.roomId}:{placement.slotId}' " +
                "has no scene binding and will remain inactive.",
                this);
            return true;
        }

        if (endpoint.BindRuntime(binding.Connection, binding.ConnectionSide))
            return true;

        Debug.LogError(
            $"[DungeonRoomBuilder] Failed to bind travel slot '{binding.RoomId}:{binding.SlotId}' " +
            $"to connection '{binding.Connection.name}'. Check the connection side scene name and duplicate endpoints.",
            this);
        return false;
    }

    private static void SetTravelMediumEnabled(GameObject endpointObject, bool enabled)
    {
        if (endpointObject == null)
            return;

        SceneTravelInteractable interactable = endpointObject.GetComponent<SceneTravelInteractable>();
        if (interactable != null)
            interactable.enabled = enabled;

        SceneTravelTrigger2D trigger = endpointObject.GetComponent<SceneTravelTrigger2D>();
        if (trigger != null)
            trigger.enabled = enabled;
    }

    private ProceduralRoomTravelBinding FindTravelBinding(string roomId, string slotId)
    {
        if (travelEndpointBindings == null)
            return default;

        for (int i = 0; i < travelEndpointBindings.Count; i++)
        {
            ProceduralRoomTravelBinding binding = travelEndpointBindings[i];
            if (string.Equals(binding.RoomId, roomId, System.StringComparison.Ordinal) &&
                string.Equals(binding.SlotId, slotId, System.StringComparison.Ordinal))
            {
                return binding;
            }
        }

        return default;
    }

    private static void ConfigureTravelMedium(
        GameObject endpointObject,
        RoomTravelEndpointKind kind,
        Vector2 triggerSize)
    {
        if (endpointObject == null)
            return;

        SceneTravelInteractable interactable = endpointObject.GetComponent<SceneTravelInteractable>();
        SceneTravelTrigger2D trigger = endpointObject.GetComponent<SceneTravelTrigger2D>();
        switch (kind)
        {
            case RoomTravelEndpointKind.Interaction:
                interactable ??= endpointObject.AddComponent<SceneTravelInteractable>();
                interactable.enabled = true;
                if (trigger != null)
                    trigger.enabled = false;
                EnsureTravelTriggerCollider(endpointObject);
                break;

            case RoomTravelEndpointKind.Trigger:
                trigger ??= endpointObject.AddComponent<SceneTravelTrigger2D>();
                trigger.enabled = true;
                if (interactable != null)
                    interactable.enabled = false;
                EnsureTravelTriggerBoxCollider(endpointObject, triggerSize);
                break;

            case RoomTravelEndpointKind.ArrivalOnly:
                if (interactable != null)
                    interactable.enabled = false;
                if (trigger != null)
                    trigger.enabled = false;
                break;
        }
    }

    private static void EnsureTravelTriggerCollider(GameObject endpointObject)
    {
        Collider2D collider = endpointObject.GetComponent<Collider2D>();
        if (collider == null)
            collider = endpointObject.AddComponent<BoxCollider2D>();

        collider.isTrigger = true;
    }

    private static void EnsureTravelTriggerBoxCollider(
        GameObject endpointObject,
        Vector2 desiredWorldSize)
    {
        BoxCollider2D boxCollider = endpointObject.GetComponent<BoxCollider2D>();
        if (boxCollider == null)
            boxCollider = endpointObject.AddComponent<BoxCollider2D>();

        Collider2D[] colliders = endpointObject.GetComponents<Collider2D>();
        for (int colliderIndex = 0; colliderIndex < colliders.Length; colliderIndex++)
        {
            Collider2D collider = colliders[colliderIndex];
            if (collider != boxCollider && collider.isTrigger)
                collider.enabled = false;
        }

        boxCollider.enabled = true;
        boxCollider.isTrigger = true;
        boxCollider.size = RoomTravelEndpointGeometry.ResolveLocalColliderSize(
            desiredWorldSize,
            endpointObject.transform.lossyScale);
    }

    private bool TryValidateRoomObjectPlacements(
        DungeonRoomPlacement roomPlacement,
        IReadOnlyList<RoomObjectPlacementData> objectPlacements,
        out Dictionary<string, int> placementIndices)
    {
        placementIndices = new Dictionary<string, int>(System.StringComparer.Ordinal);
        string roomName = roomPlacement.Template.name;
        RectInt localBounds = ResolveLocalBounds(roomPlacement.Template.LayoutData);
        for (int objectIndex = 0; objectIndex < objectPlacements.Count; objectIndex++)
        {
            RoomObjectPlacementData objectPlacement = objectPlacements[objectIndex];
            if (string.IsNullOrWhiteSpace(objectPlacement.placementId) ||
                !placementIndices.TryAdd(objectPlacement.placementId, objectIndex))
            {
                Debug.LogError(
                    $"Room '{roomName}' object {objectIndex} requires a unique non-empty Placement Id.",
                    roomPlacement.Template);
                return false;
            }

            if (!IsPlacementSourceCompatible(objectPlacement))
            {
                Debug.LogError(
                    $"Room '{roomName}' object '{objectPlacement.placementId}' has no valid " +
                    $"{objectPlacement.kind} source. Monster placements require a common-role " +
                    $"StageMonsterSetSO or a stage-fixed Enemy prefab.",
                    roomPlacement.Template);
                return false;
            }

            if (!localBounds.Contains(objectPlacement.localCell))
            {
                Debug.LogError(
                    $"Room '{roomName}' object '{objectPlacement.placementId}' cell " +
                    $"{objectPlacement.localCell} is outside room bounds {localBounds}.",
                    roomPlacement.Template);
                return false;
            }

            Vector2Int worldCell = roomPlacement.Origin + objectPlacement.localCell;
            Vector3Int tileCell = new(worldCell.x, worldCell.y, 0);
            if (!floorTilemap.HasTile(tileCell))
            {
                Debug.LogError(
                    $"Room '{roomName}' object '{objectPlacement.placementId}' requires a Floor tile " +
                    $"at local cell {objectPlacement.localCell}.",
                    roomPlacement.Template);
                return false;
            }
        }

        return true;
    }

    private bool TryResolveLinkedChestLock(
        DungeonRoomPlacement roomPlacement,
        RoomObjectPlacementData monsterPlacement,
        IReadOnlyList<RoomObjectPlacementData> placements,
        IReadOnlyDictionary<string, int> placementIndices,
        IReadOnlyList<GameObject> instances,
        out ChestMonsterKillLock linkedChestLock)
    {
        linkedChestLock = null;
        string targetPlacementId = monsterPlacement.linkedChestLockPlacementId;
        if (string.IsNullOrWhiteSpace(targetPlacementId))
            return true;

        if (!placementIndices.TryGetValue(targetPlacementId, out int targetIndex) ||
            targetIndex < 0 ||
            targetIndex >= placements.Count ||
            targetIndex >= instances.Count)
        {
            Debug.LogError(
                $"Room '{roomPlacement.Template.name}' monster '{monsterPlacement.placementId}' references " +
                $"missing chest placement '{targetPlacementId}'.",
                roomPlacement.Template);
            return false;
        }

        RoomObjectPlacementData targetPlacement = placements[targetIndex];
        GameObject targetInstance = instances[targetIndex];
        if (targetPlacement.kind != RoomObjectKind.Chest || targetInstance == null)
        {
            Debug.LogError(
                $"Room '{roomPlacement.Template.name}' monster '{monsterPlacement.placementId}' target " +
                $"'{targetPlacementId}' is not a generated chest.",
                roomPlacement.Template);
            return false;
        }

        linkedChestLock = targetInstance.GetComponentInChildren<ChestMonsterKillLock>(true);
        if (linkedChestLock != null)
            return true;

        Debug.LogError(
            $"Room '{roomPlacement.Template.name}' monster '{monsterPlacement.placementId}' target chest " +
            $"'{targetPlacementId}' has no ChestMonsterKillLock.",
            roomPlacement.Template);
        return false;
    }

    private bool TryBuildRoomObject(
        DungeonRoomPlacement roomPlacement,
        RoomObjectPlacementData objectPlacement,
        ChestMonsterKillLock linkedChestLock,
        MonsterRoomArea2D roomArea,
        MonsterSpawnRoomGroup roomGroup,
        out GameObject instance)
    {
        instance = null;
        string roomName = roomPlacement.Template.name;
        Vector2Int worldCell = roomPlacement.Origin + objectPlacement.localCell;
        Vector3Int tileCell = new(worldCell.x, worldCell.y, 0);
        GridLayout grid = floorTilemap.layoutGrid;
        Vector3 gridOffset = new(
            objectPlacement.localOffset.x,
            objectPlacement.localOffset.y,
            0f);
        Vector3 worldOffset = grid != null
            ? grid.transform.TransformVector(gridOffset)
            : gridOffset;
        Vector3 worldPosition = floorTilemap.GetCellCenterWorld(tileCell) + worldOffset;
        Quaternion gridRotation = grid != null
            ? grid.transform.rotation
            : Quaternion.identity;
        Quaternion worldRotation = gridRotation *
            Quaternion.Euler(0f, 0f, objectPlacement.localRotationDegrees);

        Transform objectRoot = ResolveGeneratedObjectRoot();
        if (objectPlacement.kind == RoomObjectKind.Monster)
        {
            instance = new GameObject();
            instance.SetActive(false);
            instance.transform.SetParent(objectRoot, false);
            instance.transform.SetPositionAndRotation(worldPosition, worldRotation);
            MonsterSpawnContainer spawnContainer =
                instance.AddComponent<MonsterSpawnContainer>();
            GameObject spawnPoint = instance;
            spawnContainer.ConfigureRuntime(
                objectPlacement.monsterStageSet,
                objectPlacement.prefab,
                roomArea,
                roomGroup,
                linkedChestLock,
                spawnedMonster => HandleGeneratedMonsterSpawned(
                    roomPlacement.PlacementId,
                    objectPlacement.placementId,
                    spawnPoint,
                    spawnedMonster));
            instance.SetActive(true);
        }
        else
        {
            instance = Instantiate(
                objectPlacement.prefab,
                worldPosition,
                worldRotation,
                objectRoot);
        }

        if (instance == null)
        {
            Debug.LogError(
                $"Room '{roomName}' object '{objectPlacement.placementId}' could not be created.",
                this);
            return false;
        }

        instance.name = $"RoomObject_{roomPlacement.PlacementId}_{objectPlacement.placementId}";
        instance.transform.localScale = objectPlacement.kind == RoomObjectKind.Monster
            ? Vector3.one
            : objectPlacement.localScale == Vector3.zero
                ? objectPlacement.prefab.transform.localScale
                : objectPlacement.localScale;

        if (!TryApplyCompositePoseOverrides(
                roomName,
                objectPlacement,
                instance))
        {
            Destroy(instance);
            instance = null;
            return false;
        }

        return true;
    }

    /// <summary>
    /// 책임:
    /// 생성된 복합 프리팹에 현재 방 배치가 저장한 슬롯별 자세 재정의를 기능 연결 전에 적용한다.
    /// </summary>
    private bool TryApplyCompositePoseOverrides(
        string roomName,
        RoomObjectPlacementData objectPlacement,
        GameObject instance)
    {
        IReadOnlyList<RoomObjectChildPoseOverrideData> overrides =
            objectPlacement.childPoseOverrides;
        if (overrides == null || overrides.Count == 0)
            return true;

        RoomCompositePoseAuthoring composite =
            instance.GetComponentInChildren<RoomCompositePoseAuthoring>(true);
        if (composite == null)
        {
            Debug.LogError(
                $"Room '{roomName}' object '{objectPlacement.placementId}' has child pose overrides " +
                "but its prefab has no RoomCompositePoseAuthoring component.",
                this);
            return false;
        }

        if (composite.TryApplyPoseOverrides(overrides, out string failureReason))
            return true;

        Debug.LogError(
            $"Room '{roomName}' object '{objectPlacement.placementId}' could not apply child pose overrides: " +
            failureReason,
            this);
        return false;
    }

    /// <summary>
    /// 책임:
    /// - 지연 스폰 포인트가 만든 실제 몬스터를 기존 던전 오브젝트 상태 Id에 인계한다.
    /// - 복도 이탈 시 살아 있는 몬스터와 처치된 몬스터의 보존 판정이 기존 즉시 스폰 방식과 같게 유지되도록 한다.
    /// </summary>
    private void HandleGeneratedMonsterSpawned(
        int roomPlacementId,
        string placementId,
        GameObject spawnPoint,
        GameObject spawnedMonster)
    {
        if (spawnedMonster == null)
            return;

        string stateId = CreateRuntimeStateId(roomPlacementId, placementId);
        spawnedMonster.name = $"RoomObject_{roomPlacementId}_{placementId}";
        spawnedMonster.transform.SetParent(ResolveGeneratedObjectRoot(), true);
        generatedRoomObjectsByStateId[stateId] = spawnedMonster;
        ReplaceTrackedGeneratedObject(generatedRoomObjects, spawnPoint, spawnedMonster);
        if (generatedRoomObjectsByPlacement.TryGetValue(
                roomPlacementId,
                out List<GameObject> roomObjects))
        {
            ReplaceTrackedGeneratedObject(roomObjects, spawnPoint, spawnedMonster);
        }

        if (spawnPoint != null)
            Destroy(spawnPoint);
    }

    /// <summary>
    /// 책임:
    /// - 생성 오브젝트 추적 목록에서 스폰 포인트를 실제 몬스터로 원자적으로 교체한다.
    /// </summary>
    private static void ReplaceTrackedGeneratedObject(
        List<GameObject> trackedObjects,
        GameObject previous,
        GameObject replacement)
    {
        if (trackedObjects == null || replacement == null)
            return;

        int index = trackedObjects.IndexOf(previous);
        if (index >= 0)
            trackedObjects[index] = replacement;
        else if (!trackedObjects.Contains(replacement))
            trackedObjects.Add(replacement);
    }

    private static bool IsPrefabCompatibleWithKind(GameObject prefab, RoomObjectKind kind)
    {
        if (prefab == null)
            return false;

        return kind switch
        {
            RoomObjectKind.Prop => true,
            RoomObjectKind.Monster => prefab.GetComponentInChildren<Enemy>(true) != null,
            RoomObjectKind.Chest => prefab.GetComponentInChildren<TreasureChest>(true) != null,
            RoomObjectKind.Portal => prefab.GetComponentInChildren<ScenePortal>(true) != null,
            _ => false
        };
    }

    /// <summary>
    /// 책임:
    /// - 공통 역할 Monster의 StageMonsterSetSO와 스테이지 고정 Monster의 Enemy 프리팹을 모두 검증한다.
    /// - 다른 오브젝트 종류는 기존 프리팹 컴포넌트 계약을 그대로 검증한다.
    /// </summary>
    private static bool IsPlacementSourceCompatible(RoomObjectPlacementData placement)
    {
        if (placement.kind == RoomObjectKind.Monster)
        {
            return placement.monsterStageSet != null ||
                   IsPrefabCompatibleWithKind(placement.prefab, RoomObjectKind.Monster);
        }

        return IsPrefabCompatibleWithKind(placement.prefab, placement.kind);
    }

    private static void PlaceTiles(
        Tilemap target,
        List<RoomTileData> tileData,
        Vector2Int roomOrigin)
    {
        if (target == null || tileData == null)
            return;

        for (int i = 0; i < tileData.Count; i++)
        {
            RoomTileData entry = tileData[i];
            if (entry.tile == null)
                continue;

            Vector2Int worldCell = roomOrigin + entry.localCell;
            target.SetTile(new Vector3Int(worldCell.x, worldCell.y, 0), entry.tile);
        }
    }

    private bool TryBuildConnection(
        DungeonLayoutResult layout,
        DungeonSocketConnection connection,
        int connectionIndex,
        DungeonBuildOptions options)
    {
        if (!TryGetPlacedSocket(
                layout,
                connection.FirstRoomPlacementId,
                connection.FirstSocketIndex,
                out Vector2Int firstCell,
                out RoomSocketData firstSocket) ||
            !TryGetPlacedSocket(
                layout,
                connection.SecondRoomPlacementId,
                connection.SecondSocketIndex,
                out Vector2Int secondCell,
                out RoomSocketData secondSocket))
        {
            Debug.LogError($"Dungeon connection {connectionIndex} references an invalid room socket.", this);
            return false;
        }

        int firstWidth = RoomSocketGeometry.ResolveWidth(firstSocket);
        int secondWidth = RoomSocketGeometry.ResolveWidth(secondSocket);
        int corridorLength = Mathf.Max(0, connection.CorridorLength);
        if (secondSocket.direction != Opposite(firstSocket.direction) ||
            firstWidth != RoomSocketGeometry.RequiredWidth ||
            secondWidth != firstWidth ||
            secondCell != firstCell +
                DirectionToVector(firstSocket.direction) * (corridorLength + 1))
        {
            Debug.LogError($"Dungeon connection {connectionIndex} contains misaligned sockets.", this);
            return false;
        }

        if (!TryOpenSocket(
                connection.FirstRoomPlacementId,
                connection.FirstSocketIndex,
                firstCell,
                firstSocket) ||
            !TryOpenSocket(
                connection.SecondRoomPlacementId,
                connection.SecondSocketIndex,
                secondCell,
                secondSocket))
        {
            Debug.LogError(
                $"Dungeon connection {connectionIndex} could not open its closed socket endpoints.",
                this);
            return false;
        }

        if (!TryBuildStraightCorridor(
                firstCell,
                firstSocket,
                corridorLength,
                layout.Seed,
                connectionIndex))
        {
            Debug.LogError(
                $"Dungeon connection {connectionIndex} could not build its straight corridor.",
                this);
            return false;
        }

        if (!TryBuildCorridorDecorations(
                firstCell,
                firstSocket,
                corridorLength,
                layout.Seed,
                connectionIndex,
                options.BuildDecorationObjects))
        {
            Debug.LogError(
                $"Dungeon connection {connectionIndex} could not build its decoration modules.",
                this);
            return false;
        }

        if (!options.BuildConnectedDoors)
            return true;

        Transform doorRoot = ResolveGeneratedDoorRoot();
        Vector3 firstCenter = GetSocketWorldCenter(firstCell, firstSocket);
        Vector3 secondCenter = GetSocketWorldCenter(secondCell, secondSocket);
        CreateConnectedSocketDoor(
            firstCenter,
            firstSocket.direction,
            doorRoot,
            connection.FirstRoomPlacementId,
            connection.FirstSocketIndex,
            layout.Seed,
            connectionIndex,
            "A");
        CreateConnectedSocketDoor(
            secondCenter,
            secondSocket.direction,
            doorRoot,
            connection.SecondRoomPlacementId,
            connection.SecondSocketIndex,
            layout.Seed,
            connectionIndex,
            "B");
        return true;
    }

    private void CreateConnectedSocketDoor(
        Vector3 socketCenter,
        RoomSocketDirection socketDirection,
        Transform doorRoot,
        int roomPlacementId,
        int socketIndex,
        int layoutSeed,
        int connectionIndex,
        string endpointSuffix)
    {
        Quaternion doorRotation = IsHorizontalConnection(socketDirection)
            ? Quaternion.Euler(0f, 0f, 90f)
            : Quaternion.identity;
        DoorObject door = Instantiate(
            connectedDoorPrefab,
            socketCenter,
            doorRotation,
            doorRoot);
        door.name = $"ConnectedDoor_Room{roomPlacementId}_Socket{socketIndex}";
        door.mapID = gameObject.scene.name;
        door.doorID = $"Procedural_{layoutSeed}_{connectionIndex:D3}_{endpointSuffix}";
        door.ApplyConfigurationFromShortcut(DoorObject.DoorType.Normal, false, this);
        if (openConnectedDoorsInitially)
            door.ForceOpen(immediate: true, save: false, playPresentation: false);

        generatedDoors.Add(door);
    }

    private bool TryBuildStraightCorridor(
        Vector2Int firstSocketCell,
        RoomSocketData socket,
        int corridorLength,
        int layoutSeed,
        int connectionIndex)
    {
        if (corridorLength <= 0)
            return true;

        if (corridorFloorTile == null || corridorWallTile == null)
            return false;

        Vector2Int direction = DirectionToVector(socket.direction);
        Vector2Int tangent = RoomSocketGeometry.GetTangent(socket.direction);
        int width = RoomSocketGeometry.ResolveWidth(socket);
        IReadOnlyList<TileBase> wallVariants =
            socket.direction == RoomSocketDirection.Up ||
            socket.direction == RoomSocketDirection.Down
                ? verticalCorridorWallVariants
                : horizontalCorridorWallVariants;
        int floorSalt = layoutSeed ^ unchecked(connectionIndex * 486187739);
        int firstWallSalt = floorSalt ^ 0x2D2816FE;
        int secondWallSalt = floorSalt ^ 0x55C7F3A1;
        for (int step = 1; step <= corridorLength; step++)
        {
            Vector2Int corridorStart = firstSocketCell + direction * step;
            for (int cellIndex = 0; cellIndex < width; cellIndex++)
            {
                Vector2Int floorCell = corridorStart + tangent * cellIndex;
                floorTilemap.SetTile(
                    new Vector3Int(floorCell.x, floorCell.y, 0),
                    ResolveTileVariant(
                        corridorFloorVariants,
                        corridorFloorTile,
                        floorCell,
                        floorSalt));
            }

            Vector2Int firstWallCell = corridorStart - tangent;
            Vector2Int secondWallCell = corridorStart + tangent * width;
            wallTilemap.SetTile(
                new Vector3Int(firstWallCell.x, firstWallCell.y, 0),
                ResolveTileVariant(
                    wallVariants,
                    corridorWallTile,
                    firstWallCell,
                    firstWallSalt));
            wallTilemap.SetTile(
                new Vector3Int(secondWallCell.x, secondWallCell.y, 0),
                ResolveTileVariant(
                    wallVariants,
                    corridorWallTile,
                    secondWallCell,
                    secondWallSalt));
        }

        return true;
    }

    /// <summary>
    /// 책임 : 현재 연결 길이에 맞춰 선택된 장식 모듈의 모든 타일 레이어와 Pivot 기반 GroundProp을 복도 방향으로 변환해 구현한다.
    /// </summary>
    private bool TryBuildCorridorDecorations(
        Vector2Int firstSocketCell,
        RoomSocketData socket,
        int corridorLength,
        int layoutSeed,
        int connectionIndex,
        bool buildDecorationObjects)
    {
        if (corridorDecorationProfile == null || corridorLength <= 0)
            return true;

        CorridorDecorationAxis axis = IsHorizontalConnection(socket.direction)
            ? CorridorDecorationAxis.Horizontal
            : CorridorDecorationAxis.Vertical;
        List<CorridorDecorationPlacement> placements =
            CorridorDecorationComposer.Compose(
                corridorDecorationProfile,
                corridorLength,
                layoutSeed,
                connectionIndex,
                axis);
        for (int placementIndex = 0; placementIndex < placements.Count; placementIndex++)
        {
            CorridorDecorationPlacement placement = placements[placementIndex];
            if (placement.Module == null ||
                !TryBuildCorridorDecorationModule(
                    firstSocketCell,
                    socket,
                    corridorLength,
                    connectionIndex,
                    placementIndex,
                    placement,
                    buildDecorationObjects))
            {
                return false;
            }
        }

        return true;
    }

    private bool TryBuildCorridorDecorationModule(
        Vector2Int firstSocketCell,
        RoomSocketData socket,
        int corridorLength,
        int connectionIndex,
        int placementIndex,
        CorridorDecorationPlacement placement,
        bool buildDecorationObjects)
    {
        CorridorDecorationModuleSO module = placement.Module;
        if (placement.ForwardOffset < 0 ||
            placement.EndOffsetExclusive > corridorLength)
        {
            Debug.LogError(
                $"Corridor decoration '{module.ModuleId}' exceeds connection {connectionIndex} bounds.",
                module);
            return false;
        }

        RoomBuildData buildData = module.BuildData;
        for (int layerIndex = 0;
             layerIndex < RoomTileLayerContract.OrderedLayers.Count;
             layerIndex++)
        {
            RoomTileLayerKind layer = RoomTileLayerContract.OrderedLayers[layerIndex];
            List<RoomTileData> tiles = buildData.GetTiles(layer);
            if (tiles == null || tiles.Count == 0)
                continue;

            Tilemap target = GetTilemap(layer);
            if (target == null)
            {
                Debug.LogError(
                    $"Corridor decoration '{module.ModuleId}' uses {layer}, but the builder has no matching Tilemap.",
                    module);
                return false;
            }

            if (!TryPlaceCorridorDecorationTiles(
                    target,
                    tiles,
                    module,
                    firstSocketCell,
                    socket,
                    placement.ForwardOffset))
            {
                return false;
            }
        }

        return !buildDecorationObjects ||
               TryBuildCorridorDecorationObjects(
                   buildData.objectPlacements,
                   module,
                   firstSocketCell,
                   socket,
                   placement.ForwardOffset,
                   connectionIndex,
                   placementIndex);
    }

    private bool TryPlaceCorridorDecorationTiles(
        Tilemap target,
        IReadOnlyList<RoomTileData> tiles,
        CorridorDecorationModuleSO module,
        Vector2Int firstSocketCell,
        RoomSocketData socket,
        int forwardOffset)
    {
        Vector2Int direction = DirectionToVector(socket.direction);
        Vector2Int tangent = RoomSocketGeometry.GetTangent(socket.direction);
        ResolveCorridorModuleBasis(
            module.Axis,
            direction,
            tangent,
            out Vector2Int basisX,
            out Vector2Int basisY);
        Matrix4x4 orientation = CreateCorridorOrientationMatrix(basisX, basisY);
        for (int tileIndex = 0; tileIndex < tiles.Count; tileIndex++)
        {
            RoomTileData tile = tiles[tileIndex];
            if (tile.tile == null)
                continue;

            if (!IsInsideCorridorModuleFootprint(tile.localCell, module))
            {
                Debug.LogError(
                    $"Corridor decoration '{module.ModuleId}' tile {tile.localCell} is outside " +
                    $"the canonical {GetCorridorModuleFootprintDescription(module)} footprint.",
                    module);
                return false;
            }

            Vector2Int worldCell = TransformCorridorModuleCell(
                firstSocketCell,
                direction,
                tangent,
                forwardOffset,
                tile.localCell,
                module.Axis);
            Vector3Int targetCell = new(worldCell.x, worldCell.y, 0);
            target.SetTile(targetCell, tile.tile);
            target.SetTileFlags(targetCell, TileFlags.None);
            target.SetTransformMatrix(targetCell, orientation);
        }

        return true;
    }

    private bool TryBuildCorridorDecorationObjects(
        IReadOnlyList<RoomObjectPlacementData> objectPlacements,
        CorridorDecorationModuleSO module,
        Vector2Int firstSocketCell,
        RoomSocketData socket,
        int forwardOffset,
        int connectionIndex,
        int modulePlacementIndex)
    {
        if (objectPlacements == null)
            return true;

        Vector2Int direction = DirectionToVector(socket.direction);
        Vector2Int tangent = RoomSocketGeometry.GetTangent(socket.direction);
        ResolveCorridorModuleBasis(
            module.Axis,
            direction,
            tangent,
            out Vector2Int basisX,
            out Vector2Int basisY);
        int orientationSign = basisX.x * basisY.y - basisX.y * basisY.x;
        float basisXDegrees = Mathf.Atan2(basisX.y, basisX.x) * Mathf.Rad2Deg;
        GridLayout grid = floorTilemap.layoutGrid;
        Quaternion gridRotation = grid != null ? grid.transform.rotation : Quaternion.identity;
        Transform objectRoot = ResolveGeneratedObjectRoot();
        for (int objectIndex = 0; objectIndex < objectPlacements.Count; objectIndex++)
        {
            RoomObjectPlacementData objectPlacement = objectPlacements[objectIndex];
            if (objectPlacement.kind != RoomObjectKind.Prop || objectPlacement.prefab == null)
            {
                Debug.LogError(
                    $"Corridor decoration '{module.ModuleId}' object " +
                    $"'{objectPlacement.placementId}' must be a Prop with a prefab.",
                    module);
                return false;
            }

            if (!IsInsideCorridorModuleFootprint(
                    objectPlacement.localCell,
                    module))
            {
                Debug.LogError(
                    $"Corridor decoration '{module.ModuleId}' object " +
                    $"'{objectPlacement.placementId}' Pivot {objectPlacement.localCell} is outside " +
                    $"the canonical {GetCorridorModuleFootprintDescription(module)} footprint.",
                    module);
                return false;
            }

            Vector2Int worldCell = TransformCorridorModuleCell(
                firstSocketCell,
                direction,
                tangent,
                forwardOffset,
                objectPlacement.localCell,
                module.Axis);
            Vector3Int targetCell = new(worldCell.x, worldCell.y, 0);
            Vector2 orientedLocalOffset =
                (Vector2)basisX * objectPlacement.localOffset.x +
                (Vector2)basisY * objectPlacement.localOffset.y;
            Vector3 gridLocalOffset = new(
                orientedLocalOffset.x,
                orientedLocalOffset.y,
                0f);
            Vector3 worldOffset = grid != null
                ? grid.transform.TransformVector(gridLocalOffset)
                : gridLocalOffset;
            Vector3 worldPosition = floorTilemap.GetCellCenterWorld(targetCell) + worldOffset;
            float localRotation = orientationSign < 0
                ? -objectPlacement.localRotationDegrees
                : objectPlacement.localRotationDegrees;
            Quaternion worldRotation = gridRotation * Quaternion.Euler(
                0f,
                0f,
                basisXDegrees + localRotation);
            GameObject instance = Instantiate(
                objectPlacement.prefab,
                worldPosition,
                worldRotation,
                objectRoot);
            if (instance == null)
            {
                Debug.LogError(
                    $"Corridor decoration '{module.ModuleId}' object " +
                    $"'{objectPlacement.placementId}' could not be created.",
                    module);
                return false;
            }

            instance.name =
                $"CorridorObject_{connectionIndex}_{modulePlacementIndex}_{objectPlacement.placementId}";
            Vector3 sourceScale = objectPlacement.localScale == Vector3.zero
                ? objectPlacement.prefab.transform.localScale
                : objectPlacement.localScale;
            if (orientationSign < 0)
                sourceScale.y = -sourceScale.y;
            instance.transform.localScale = sourceScale;
            generatedRoomObjects.Add(instance);
            generatedRoomObjectsByStateId[
                CreateCorridorRuntimeStateId(
                    connectionIndex,
                    modulePlacementIndex,
                    objectPlacement.placementId)] = instance;
        }

        return true;
    }

    private static bool IsInsideCorridorModuleFootprint(
        Vector2Int localCell,
        CorridorDecorationModuleSO module)
    {
        if (module == null)
            return false;

        return module.Axis == CorridorDecorationAxis.Horizontal
            ? localCell.x >= 0 &&
              localCell.x < module.Length &&
              localCell.y >= -1 &&
              localCell.y <= RoomSocketGeometry.RequiredWidth
            : localCell.y >= 0 &&
              localCell.y < module.Length &&
              localCell.x >= -1 &&
              localCell.x <= RoomSocketGeometry.RequiredWidth;
    }

    private static string GetCorridorModuleFootprintDescription(
        CorridorDecorationModuleSO module)
    {
        return module.Axis == CorridorDecorationAxis.Horizontal
            ? $"x=0..{module.Length - 1}, y=-1..2"
            : $"x=-1..2, y=0..{module.Length - 1}";
    }

    private static Vector2Int TransformCorridorModuleCell(
        Vector2Int firstSocketCell,
        Vector2Int direction,
        Vector2Int tangent,
        int forwardOffset,
        Vector2Int localCell,
        CorridorDecorationAxis axis)
    {
        int forward = axis == CorridorDecorationAxis.Horizontal
            ? localCell.x
            : localCell.y;
        int lateral = axis == CorridorDecorationAxis.Horizontal
            ? localCell.y
            : localCell.x;
        return firstSocketCell +
               direction * (forwardOffset + forward + 1) +
               tangent * lateral;
    }

    private static void ResolveCorridorModuleBasis(
        CorridorDecorationAxis axis,
        Vector2Int direction,
        Vector2Int tangent,
        out Vector2Int basisX,
        out Vector2Int basisY)
    {
        if (axis == CorridorDecorationAxis.Horizontal)
        {
            basisX = direction;
            basisY = tangent;
            return;
        }

        basisX = tangent;
        basisY = direction;
    }

    private static Matrix4x4 CreateCorridorOrientationMatrix(
        Vector2Int basisX,
        Vector2Int basisY)
    {
        Matrix4x4 orientation = Matrix4x4.identity;
        orientation.m00 = basisX.x;
        orientation.m01 = basisY.x;
        orientation.m10 = basisX.y;
        orientation.m11 = basisY.y;
        return orientation;
    }

    /// <summary>
    /// 책임 : 같은 Seed로 재생성된 복도 GroundProp의 파괴·활성 상태를 연결/모듈/Pivot 배치 Id로 다시 찾게 한다.
    /// </summary>
    private static string CreateCorridorRuntimeStateId(
        int connectionIndex,
        int modulePlacementIndex,
        string objectPlacementId)
    {
        return $"corridor:{connectionIndex}:{modulePlacementIndex}:{objectPlacementId}";
    }

    private static TileBase ResolveTileVariant(
        IReadOnlyList<TileBase> variants,
        TileBase fallback,
        Vector2Int cell,
        int seedSalt)
    {
        if (variants == null || variants.Count == 0)
            return fallback;

        int index = ResolveStableVariantIndex(cell, seedSalt, variants.Count);
        TileBase selected = variants[index];
        return selected != null ? selected : fallback;
    }

    private static int ResolveStableVariantIndex(
        Vector2Int cell,
        int seedSalt,
        int variantCount)
    {
        unchecked
        {
            uint hash = (uint)(cell.x * 73856093);
            hash ^= (uint)(cell.y * 19349663);
            hash ^= (uint)(seedSalt * 83492791);
            hash ^= hash >> 16;
            return (int)(hash % (uint)variantCount);
        }
    }

    private bool TryOpenSocket(
        int roomPlacementId,
        int socketIndex,
        Vector2Int worldStartCell,
        RoomSocketData socket)
    {
        long endpointKey = GetSocketEndpointKey(roomPlacementId, socketIndex);
        if (!generatedSocketBlockersByEndpoint.TryGetValue(endpointKey, out BoxCollider2D blocker) ||
            blocker == null)
        {
            return false;
        }

        int width = RoomSocketGeometry.ResolveWidth(socket);
        Vector2Int tangent = RoomSocketGeometry.GetTangent(socket.direction);
        for (int cellIndex = 0; cellIndex < width; cellIndex++)
        {
            Vector2Int worldCell = worldStartCell + tangent * cellIndex;
            if (!wallTilemap.HasTile(new Vector3Int(worldCell.x, worldCell.y, 0)))
                return false;
        }

        for (int cellIndex = 0; cellIndex < width; cellIndex++)
        {
            Vector2Int worldCell = worldStartCell + tangent * cellIndex;
            wallTilemap.SetTile(new Vector3Int(worldCell.x, worldCell.y, 0), null);
        }

        blocker.enabled = false;
        generatedSocketBlockers.Remove(blocker);
        generatedSocketBlockersByEndpoint.Remove(endpointKey);

        if (Application.isPlaying)
            Destroy(blocker.gameObject);
        else
            DestroyImmediate(blocker.gameObject);

        return true;
    }

    private Vector3 GetSocketWorldCenter(Vector2Int worldStartCell, RoomSocketData socket)
    {
        int width = RoomSocketGeometry.ResolveWidth(socket);
        Vector2Int tangent = RoomSocketGeometry.GetTangent(socket.direction);
        Vector2Int worldEndCell = worldStartCell + tangent * (width - 1);
        Vector3 firstCenter = floorTilemap.GetCellCenterWorld(
            new Vector3Int(worldStartCell.x, worldStartCell.y, 0));
        Vector3 lastCenter = floorTilemap.GetCellCenterWorld(
            new Vector3Int(worldEndCell.x, worldEndCell.y, 0));
        return Vector3.Lerp(firstCenter, lastCenter, 0.5f);
    }

    private static long GetSocketEndpointKey(int roomPlacementId, int socketIndex)
    {
        return ((long)roomPlacementId << 32) | (uint)socketIndex;
    }

    private Transform ResolveGeneratedDoorRoot()
    {
        if (generatedDoorRoot != null)
            return generatedDoorRoot;

        GameObject root = new("GeneratedDoors");
        root.transform.SetParent(transform, false);
        generatedDoorRoot = root.transform;
        return generatedDoorRoot;
    }

    private Transform ResolveGeneratedSocketBlockerRoot()
    {
        if (generatedSocketBlockerRoot != null)
            return generatedSocketBlockerRoot;

        GameObject root = new("GeneratedSocketBlockers");
        root.transform.SetParent(transform, false);
        generatedSocketBlockerRoot = root.transform;
        return generatedSocketBlockerRoot;
    }

    private Transform ResolveGeneratedObjectRoot()
    {
        if (generatedObjectRoot != null)
            return generatedObjectRoot;

        GameObject root = new("GeneratedRoomObjects");
        root.transform.SetParent(transform, false);
        generatedObjectRoot = root.transform;
        return generatedObjectRoot;
    }

    private Transform ResolveGeneratedTravelEndpointRoot()
    {
        if (generatedTravelEndpointRoot != null)
            return generatedTravelEndpointRoot;

        GameObject root = new("GeneratedTravelEndpoints");
        root.transform.SetParent(transform, false);
        generatedTravelEndpointRoot = root.transform;
        return generatedTravelEndpointRoot;
    }

    private Transform ResolveGeneratedEncounterRoot()
    {
        if (generatedEncounterRoot != null)
            return generatedEncounterRoot;

        GameObject root = new("GeneratedRoomEncounters");
        root.transform.SetParent(transform, false);
        generatedEncounterRoot = root.transform;
        return generatedEncounterRoot;
    }

    private Transform ResolveGeneratedMapDiscoveryRoot()
    {
        if (generatedMapDiscoveryRoot != null)
            return generatedMapDiscoveryRoot;

        GameObject root = new("GeneratedMapDiscovery");
        root.transform.SetParent(transform, false);
        generatedMapDiscoveryRoot = root.transform;
        return generatedMapDiscoveryRoot;
    }

    private static bool TryGetPlacedSocket(
        DungeonLayoutResult layout,
        int roomPlacementId,
        int socketIndex,
        out Vector2Int worldCell,
        out RoomSocketData socket)
    {
        worldCell = default;
        socket = default;

        DungeonRoomPlacement placement = layout.GetRoom(roomPlacementId);
        if (placement == null)
            return false;

        List<RoomSocketData> sockets = placement.Template.LayoutData.sockets;
        if (sockets == null || socketIndex < 0 || socketIndex >= sockets.Count)
            return false;

        socket = sockets[socketIndex];
        worldCell = placement.Origin + socket.localCell;
        return true;
    }

    private static bool HasSpacedConnection(DungeonLayoutResult layout)
    {
        for (int i = 0; i < layout.Connections.Count; i++)
        {
            if (layout.Connections[i].CorridorLength > 0)
                return true;
        }

        return false;
    }

    private static RectInt ResolveLocalBounds(RoomLayoutData layout)
    {
        if (layout.localBounds.width > 0 && layout.localBounds.height > 0)
            return layout.localBounds;

        return new RectInt(Vector2Int.zero, layout.size);
    }

    private static bool IsHorizontalConnection(RoomSocketDirection direction)
    {
        return direction == RoomSocketDirection.Left || direction == RoomSocketDirection.Right;
    }

    private static RoomSocketDirection Opposite(RoomSocketDirection direction)
    {
        return direction switch
        {
            RoomSocketDirection.Up => RoomSocketDirection.Down,
            RoomSocketDirection.Right => RoomSocketDirection.Left,
            RoomSocketDirection.Down => RoomSocketDirection.Up,
            RoomSocketDirection.Left => RoomSocketDirection.Right,
            _ => direction
        };
    }

    private static Vector2Int DirectionToVector(RoomSocketDirection direction)
    {
        return direction switch
        {
            RoomSocketDirection.Up => Vector2Int.up,
            RoomSocketDirection.Right => Vector2Int.right,
            RoomSocketDirection.Down => Vector2Int.down,
            RoomSocketDirection.Left => Vector2Int.left,
            _ => Vector2Int.zero
        };
    }
}
