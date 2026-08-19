using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 책임:
/// - DungeonLayoutResult의 배치 좌표에 각 방과 연결 복도의 Floor/Wall 타일, 런타임 오브젝트를 구현한다.
/// - 몬스터 배치는 기존 MonsterSpawner를 경유하고 상자·포털·프롭은 프리팹 인스턴스로 생성한다.
/// - 몬스터 배치가 지정한 상자 Placement Id를 기존 MonsterSpawnRequest의 Kill Lock 연결로 해석한다.
/// - 몬스터 방에는 기존 MonsterSpawnRoomGroup/RoomDoorMonsterKillLock 기반 encounter를 구성한다.
/// - 모든 2칸 소켓을 Wall 타일과 전용 물리 Collider로 닫은 뒤 연결이 확정된 소켓만 개방한다.
/// - 개방한 두 소켓 사이에 연결별 가변 길이의 2칸 폭 직선 복도를 만들고 양쪽 소켓 경계에 비영구 문을 하나씩 생성한다.
/// - 테마 씬에서 추출한 바닥/벽 변형 타일을 레이아웃 Seed와 셀 좌표로 결정해 같은 맵을 항상 같은 모습으로 구현한다.
/// - 전투 잠금 정책이 붙기 전 프로토타입에서는 생성 문을 열린 상태로 시작할 수 있게 한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class DungeonRoomBuilder : MonoBehaviour
{
    private const int RoomEntryBoundaryInsetCells = 1;

    [SerializeField] private Tilemap floorTilemap;
    [SerializeField] private Tilemap wallTilemap;

    [Header("Corridors")]
    [SerializeField] private TileBase corridorFloorTile;
    [SerializeField] private TileBase corridorWallTile;
    [SerializeField] private List<TileBase> corridorFloorVariants = new();
    [SerializeField] private List<TileBase> horizontalCorridorWallVariants = new();
    [SerializeField] private List<TileBase> verticalCorridorWallVariants = new();

    [Header("Connected Doors")]
    [SerializeField] private DoorObject connectedDoorPrefab;
    [SerializeField] private Transform generatedDoorRoot;
    [SerializeField] private Transform generatedSocketBlockerRoot;
    [SerializeField] private bool openConnectedDoorsInitially = true;

    [Header("Room Objects")]
    [SerializeField] private Transform generatedObjectRoot;

    [Header("Room Encounters")]
    [SerializeField] private Transform generatedEncounterRoot;

    private readonly List<DoorObject> generatedDoors = new();
    private readonly List<BoxCollider2D> generatedSocketBlockers = new();
    private readonly Dictionary<long, BoxCollider2D> generatedSocketBlockersByEndpoint = new();
    private readonly List<GameObject> generatedRoomObjects = new();
    private readonly List<MonsterSpawnRoomGroup> generatedRoomEncounterGroups = new();
    private readonly List<RoomDoorMonsterKillLock> generatedRoomDoorLocks = new();
    private readonly Dictionary<int, MonsterSpawnRoomGroup> generatedRoomGroupsByPlacement = new();
    private readonly Dictionary<int, MonsterRoomArea2D> generatedRoomAreasByPlacement = new();

    public Tilemap FloorTilemap => floorTilemap;
    public Tilemap WallTilemap => wallTilemap;
    public TileBase CorridorFloorTile => corridorFloorTile;
    public TileBase CorridorWallTile => corridorWallTile;
    public IReadOnlyList<TileBase> CorridorFloorVariants => corridorFloorVariants;
    public IReadOnlyList<TileBase> HorizontalCorridorWallVariants => horizontalCorridorWallVariants;
    public IReadOnlyList<TileBase> VerticalCorridorWallVariants => verticalCorridorWallVariants;
    public DoorObject ConnectedDoorPrefab => connectedDoorPrefab;
    public Transform GeneratedDoorRoot => generatedDoorRoot;
    public Transform GeneratedSocketBlockerRoot => generatedSocketBlockerRoot;
    public Transform GeneratedObjectRoot => generatedObjectRoot;
    public Transform GeneratedEncounterRoot => generatedEncounterRoot;
    public bool OpenConnectedDoorsInitially => openConnectedDoorsInitially;
    public IReadOnlyList<DoorObject> GeneratedDoors => generatedDoors;
    public IReadOnlyList<BoxCollider2D> GeneratedSocketBlockers => generatedSocketBlockers;
    public IReadOnlyList<GameObject> GeneratedRoomObjects => generatedRoomObjects;
    public IReadOnlyList<MonsterSpawnRoomGroup> GeneratedRoomEncounterGroups => generatedRoomEncounterGroups;
    public IReadOnlyList<RoomDoorMonsterKillLock> GeneratedRoomDoorLocks => generatedRoomDoorLocks;

#if UNITY_EDITOR
    public void EditorAssignTilemaps(Tilemap floor, Tilemap wall)
    {
        floorTilemap = floor;
        wallTilemap = wall;
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

    public void EditorAssignEncounterRoot(Transform encounterRoot)
    {
        generatedEncounterRoot = encounterRoot;
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

    public bool TryBuild(DungeonLayoutResult layout)
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

        if (layout.Connections.Count > 0 && connectedDoorPrefab == null)
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
            PlaceTiles(floorTilemap, buildData.floorTiles, placement.Origin);
            PlaceTiles(wallTilemap, buildData.wallTiles, placement.Origin);
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
            if (!TryBuildConnection(layout, connection, i))
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

        if (!TryBuildRoomEncounters(layout))
        {
            ClearGeneratedContent();
            return false;
        }

        if (!TryBuildRoomObjects(layout))
        {
            ClearGeneratedContent();
            return false;
        }

        floorTilemap.CompressBounds();
        wallTilemap.CompressBounds();
        return true;
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
    }

    public void ClearGeneratedTiles()
    {
        if (floorTilemap != null)
            floorTilemap.ClearAllTiles();

        if (wallTilemap != null && wallTilemap != floorTilemap)
            wallTilemap.ClearAllTiles();
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
                generatedRoomObjects.Add(roomInstances[objectIndex]);
        }

        return true;
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

            if (!IsPrefabCompatibleWithKind(objectPlacement.prefab, objectPlacement.kind))
            {
                Debug.LogError(
                    $"Room '{roomName}' object '{objectPlacement.placementId}' has an invalid " +
                    $"{objectPlacement.kind} prefab.",
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
        if (objectPlacement.kind == RoomObjectKind.Monster && Application.isPlaying)
        {
            MonsterSpawner spawner = MonsterSpawner.Instance;
            if (spawner == null)
            {
                Debug.LogError(
                    $"Room '{roomName}' object '{objectPlacement.placementId}' requires an active MonsterSpawner.",
                    this);
                return false;
            }

            MonsterSpawnRequest request = new(
                objectPlacement.prefab,
                worldPosition,
                worldRotation,
                roomArea,
                linkedChestLock,
                roomGroup);
            instance = spawner.SpawnOne(request);
            if (instance != null)
                instance.transform.SetParent(objectRoot, true);
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
        instance.transform.localScale = objectPlacement.localScale == Vector3.zero
            ? objectPlacement.prefab.transform.localScale
            : objectPlacement.localScale;
        return true;
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

    private static void PlaceTiles(
        Tilemap target,
        List<RoomTileData> tileData,
        Vector2Int roomOrigin)
    {
        if (tileData == null)
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
        int connectionIndex)
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

    private Transform ResolveGeneratedEncounterRoot()
    {
        if (generatedEncounterRoot != null)
            return generatedEncounterRoot;

        GameObject root = new("GeneratedRoomEncounters");
        root.transform.SetParent(transform, false);
        generatedEncounterRoot = root.transform;
        return generatedEncounterRoot;
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
