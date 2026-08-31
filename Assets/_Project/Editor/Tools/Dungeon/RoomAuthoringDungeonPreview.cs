using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// 책임:
/// - 저장되지 않는 방 제작 작업 공간에서 동적 던전 미리보기 루트를 식별한다.
/// - 기존 게임 씬의 생성 루트와 에디터 미리보기 오브젝트가 섞이지 않게 한다.
/// </summary>
[DisallowMultipleComponent]
internal sealed class RoomAuthoringDungeonPreviewMarker : MonoBehaviour
{
}

/// <summary>
/// 책임:
/// - Room Piece 툴이 레이아웃 조립기와 시각 빌더에 전달할 라이브러리, 편집 중인 방, 생성 설정을 묶는다.
/// - 프리뷰 생성 과정이 에디터 창의 직렬화 필드와 UI 구현을 직접 알지 않게 한다.
/// </summary>
internal readonly struct RoomAuthoringDungeonPreviewRequest
{
    public RoomThemeLibrarySO Library { get; }
    public DungeonLayoutPolicySO LayoutPolicy { get; }
    public bool IncludeCurrentRoom { get; }
    public RoomTemplateSO ReplacedTemplate { get; }
    public RoomLayoutData CurrentLayout { get; }
    public RoomBuildData CurrentBuild { get; }
    public Grid AnchorGrid { get; }
    public Vector2Int AnchorRoomSize { get; }
    public TileBase CorridorFloorTile { get; }
    public TileBase CorridorWallTile { get; }
    public int Seed { get; }
    public int RoomCount { get; }
    public bool IncludeBossRoom { get; }
    public int MaxPlacementAttemptsPerRoom { get; }
    public int MinimumCorridorLength { get; }
    public float CorridorLengthPerRoomCell { get; }
    public int CorridorLengthVariation { get; }
    public IReadOnlyList<RoomTemplateSO> GuaranteedRoomTemplates { get; }

    public RoomAuthoringDungeonPreviewRequest(
        RoomThemeLibrarySO library,
        DungeonLayoutPolicySO layoutPolicy,
        bool includeCurrentRoom,
        RoomTemplateSO replacedTemplate,
        RoomLayoutData currentLayout,
        RoomBuildData currentBuild,
        Grid anchorGrid,
        Vector2Int anchorRoomSize,
        TileBase corridorFloorTile,
        TileBase corridorWallTile,
        int seed,
        int roomCount,
        bool includeBossRoom,
        int maxPlacementAttemptsPerRoom,
        int minimumCorridorLength,
        float corridorLengthPerRoomCell,
        int corridorLengthVariation,
        IReadOnlyList<RoomTemplateSO> guaranteedRoomTemplates)
    {
        Library = library;
        LayoutPolicy = layoutPolicy;
        IncludeCurrentRoom = includeCurrentRoom;
        ReplacedTemplate = replacedTemplate;
        CurrentLayout = currentLayout;
        CurrentBuild = currentBuild;
        AnchorGrid = anchorGrid;
        AnchorRoomSize = anchorRoomSize;
        CorridorFloorTile = corridorFloorTile;
        CorridorWallTile = corridorWallTile;
        Seed = seed;
        RoomCount = roomCount;
        IncludeBossRoom = includeBossRoom;
        MaxPlacementAttemptsPerRoom = maxPlacementAttemptsPerRoom;
        MinimumCorridorLength = minimumCorridorLength;
        CorridorLengthPerRoomCell = corridorLengthPerRoomCell;
        CorridorLengthVariation = corridorLengthVariation;
        GuaranteedRoomTemplates = guaranteedRoomTemplates;
    }
}

/// <summary>
/// 책임:
/// - 한 번의 에디터 미리보기 생성 성공 여부, 완성도와 기획자가 확인할 통계를 반환한다.
/// - 에디터 창이 런타임 레이아웃 객체의 수명을 소유하지 않고도 결과를 설명하게 한다.
/// </summary>
internal readonly struct RoomAuthoringDungeonPreviewResult
{
    public bool WasBuilt { get; }
    public bool IsComplete { get; }
    public int RoomCount { get; }
    public int RequestedRoomCount { get; }
    public int ConnectionCount { get; }
    public int CurrentRoomPlacementCount { get; }
    public string Message { get; }
    public string CorridorFloorTileName { get; }
    public string CorridorWallTileName { get; }
    public int ShortestCorridorLength { get; }
    public int LongestCorridorLength { get; }
    public bool UsedCorridorLengthRelaxation { get; }

    public RoomAuthoringDungeonPreviewResult(
        bool wasBuilt,
        bool isComplete,
        int roomCount,
        int requestedRoomCount,
        int connectionCount,
        int currentRoomPlacementCount,
        string message,
        string corridorFloorTileName,
        string corridorWallTileName,
        int shortestCorridorLength,
        int longestCorridorLength,
        bool usedCorridorLengthRelaxation)
    {
        WasBuilt = wasBuilt;
        IsComplete = isComplete;
        RoomCount = roomCount;
        RequestedRoomCount = requestedRoomCount;
        ConnectionCount = connectionCount;
        CurrentRoomPlacementCount = currentRoomPlacementCount;
        Message = message ?? string.Empty;
        CorridorFloorTileName = corridorFloorTileName ?? string.Empty;
        CorridorWallTileName = corridorWallTileName ?? string.Empty;
        ShortestCorridorLength = Mathf.Max(0, shortestCorridorLength);
        LongestCorridorLength = Mathf.Max(0, longestCorridorLength);
        UsedCorridorLengthRelaxation = usedCorridorLengthRelaxation;
    }

    public static RoomAuthoringDungeonPreviewResult Failed(string message)
    {
        return new RoomAuthoringDungeonPreviewResult(
            false,
            false,
            0,
            0,
            0,
            0,
            message,
            string.Empty,
            string.Empty,
            0,
            0,
            false);
    }
}

/// <summary>
/// 책임:
/// - DungeonLayoutAssembler의 실제 결과를 Room Authoring 전용 씬에 타일 미리보기로 구현한다.
/// - 편집 중인 미저장 방을 임시 라이브러리에 주입하고 원본 RoomTemplateSO와 RoomThemeLibrarySO는 변경하지 않는다.
/// - 문, 몬스터, 상자, 포털의 게임플레이 컴포넌트 대신 Scene View 표식을 그린다.
/// - 재생성/초기화 시 자신이 만든 프리뷰 루트만 폐기하고 기존 씬과 authoring 루트는 보존한다.
/// </summary>
internal static class RoomAuthoringDungeonPreview
{
    private const int PreviewGapCells = 12;
    private const string PreviewRootName = "[Preview] Procedural Dungeon";

    /// <summary>
    /// 책임:
    /// - Scene View에 그릴 방의 예약 영역, 표시 이름과 현재 편집 방 여부를 복사해 보관한다.
    /// </summary>
    private readonly struct PreviewRoomInfo
    {
        public int PlacementId { get; }
        public string RoomId { get; }
        public RoomType RoomType { get; }
        public RectInt WorldBounds { get; }
        public bool IsCurrentRoom { get; }

        public PreviewRoomInfo(
            int placementId,
            string roomId,
            RoomType roomType,
            RectInt worldBounds,
            bool isCurrentRoom)
        {
            PlacementId = placementId;
            RoomId = roomId ?? string.Empty;
            RoomType = roomType;
            WorldBounds = worldBounds;
            IsCurrentRoom = isCurrentRoom;
        }
    }

    /// <summary>
    /// 책임:
    /// - 연결된 두 소켓의 월드 셀 범위와 복도 예약 영역을 Scene View 선으로 표시할 수 있게 보관한다.
    /// </summary>
    private readonly struct PreviewConnectionInfo
    {
        public Vector2Int FirstSocketCell { get; }
        public RoomSocketDirection FirstDirection { get; }
        public int FirstWidth { get; }
        public Vector2Int SecondSocketCell { get; }
        public RoomSocketDirection SecondDirection { get; }
        public int SecondWidth { get; }
        public RectInt CorridorBounds { get; }

        public PreviewConnectionInfo(
            Vector2Int firstSocketCell,
            RoomSocketDirection firstDirection,
            int firstWidth,
            Vector2Int secondSocketCell,
            RoomSocketDirection secondDirection,
            int secondWidth,
            RectInt corridorBounds)
        {
            FirstSocketCell = firstSocketCell;
            FirstDirection = firstDirection;
            FirstWidth = firstWidth;
            SecondSocketCell = secondSocketCell;
            SecondDirection = secondDirection;
            SecondWidth = secondWidth;
            CorridorBounds = corridorBounds;
        }
    }

    /// <summary>
    /// 책임:
    /// - 실제 프리팹을 생성하지 않고 방 오브젝트의 종류와 배치 셀을 Scene View 아이콘으로 표현한다.
    /// </summary>
    private readonly struct PreviewObjectInfo
    {
        public RoomObjectKind Kind { get; }
        public Vector2Int WorldCell { get; }

        public PreviewObjectInfo(RoomObjectKind kind, Vector2Int worldCell)
        {
            Kind = kind;
            WorldCell = worldCell;
        }
    }

    /// <summary>
    /// 책임:
    /// - 실제 이동 매개체를 생성하지 않고 방의 씬 이동 슬롯 종류와 출발·도착 배치를 Scene View 표식으로 표현한다.
    /// </summary>
    private readonly struct PreviewTravelEndpointInfo
    {
        public RoomTravelEndpointKind Kind { get; }
        public Vector2Int WorldCell { get; }
        public Vector2 LocalOffset { get; }
        public float LocalRotationDegrees { get; }
        public Vector2 TriggerSize { get; }
        public bool UseSeparateArrivalPoint { get; }
        public Vector2Int ArrivalWorldCell { get; }
        public Vector2 ArrivalLocalOffset { get; }

        public PreviewTravelEndpointInfo(
            RoomTravelEndpointKind kind,
            Vector2Int worldCell,
            Vector2 localOffset,
            float localRotationDegrees,
            Vector2 triggerSize,
            bool useSeparateArrivalPoint,
            Vector2Int arrivalWorldCell,
            Vector2 arrivalLocalOffset)
        {
            Kind = kind;
            WorldCell = worldCell;
            LocalOffset = localOffset;
            LocalRotationDegrees = localRotationDegrees;
            TriggerSize = triggerSize;
            UseSeparateArrivalPoint = useSeparateArrivalPoint;
            ArrivalWorldCell = arrivalWorldCell;
            ArrivalLocalOffset = arrivalLocalOffset;
        }
    }

    /// <summary>
    /// 책임:
    /// - 임시 ScriptableObject가 폐기된 뒤에도 Scene View가 그릴 수 있는 복사본과 프리뷰 Grid 참조를 보관한다.
    /// </summary>
    private sealed class PreviewSnapshot
    {
        public Grid Grid { get; }
        public RectInt LayoutBounds { get; }
        public IReadOnlyList<PreviewRoomInfo> Rooms { get; }
        public IReadOnlyList<PreviewConnectionInfo> Connections { get; }
        public IReadOnlyList<PreviewObjectInfo> Objects { get; }
        public IReadOnlyList<PreviewTravelEndpointInfo> TravelEndpoints { get; }

        public PreviewSnapshot(
            Grid grid,
            RectInt layoutBounds,
            IReadOnlyList<PreviewRoomInfo> rooms,
            IReadOnlyList<PreviewConnectionInfo> connections,
            IReadOnlyList<PreviewObjectInfo> objects,
            IReadOnlyList<PreviewTravelEndpointInfo> travelEndpoints)
        {
            Grid = grid;
            LayoutBounds = layoutBounds;
            Rooms = rooms;
            Connections = connections;
            Objects = objects;
            TravelEndpoints = travelEndpoints;
        }
    }

    private static PreviewSnapshot lastSnapshot;

    public static bool HasPreview
    {
        get
        {
            return lastSnapshot != null && lastSnapshot.Grid != null;
        }
    }

    public static RoomAuthoringDungeonPreviewResult Generate(
        RoomAuthoringDungeonPreviewRequest request)
    {
        if (request.Library == null)
            return RoomAuthoringDungeonPreviewResult.Failed("테마 룸 라이브러리가 필요합니다.");

        if (request.AnchorGrid == null)
            return RoomAuthoringDungeonPreviewResult.Failed("편집 중인 방의 Grid를 찾을 수 없습니다.");

        RoomThemeLibrarySO previewLibrary = null;
        RoomTemplateSO transientCurrentRoom = null;
        try
        {
            previewLibrary = CreatePreviewLibrary(request, out transientCurrentRoom);
            IReadOnlyList<RoomTemplateSO> previewGuaranteedRooms =
                ResolvePreviewGuaranteedRooms(request, transientCurrentRoom);
            DungeonLayoutResult layout = request.LayoutPolicy != null && request.IncludeBossRoom
                ? new DungeonGraphLayoutAssembler().Assemble(
                    previewLibrary,
                    request.LayoutPolicy,
                    request.Seed,
                    request.RoomCount,
                    request.MaxPlacementAttemptsPerRoom,
                    request.MinimumCorridorLength,
                    request.CorridorLengthPerRoomCell,
                    request.CorridorLengthVariation,
                    previewGuaranteedRooms)
                : new DungeonLayoutAssembler().Assemble(
                    previewLibrary,
                    request.Seed,
                    request.RoomCount,
                    request.IncludeBossRoom,
                    request.MaxPlacementAttemptsPerRoom,
                    request.MinimumCorridorLength,
                    request.CorridorLengthPerRoomCell,
                    request.CorridorLengthVariation);

            if (layout.Rooms.Count == 0)
            {
                return RoomAuthoringDungeonPreviewResult.Failed(
                    string.IsNullOrWhiteSpace(layout.FailureReason)
                        ? "배치 가능한 방이 없습니다."
                        : layout.FailureReason);
            }

            TileBase floorTile = request.CorridorFloorTile != null
                ? request.CorridorFloorTile
                : ResolveMostFrequentTile(previewLibrary, useFloorTiles: true);
            TileBase wallTile = request.CorridorWallTile != null
                ? request.CorridorWallTile
                : ResolveMostFrequentTile(previewLibrary, useFloorTiles: false);

            bool buildSucceeded = false;
            Grid previewGrid = null;
            bool mutationExecuted = RoomAuthoringWorkspace.ExecutePreviewMutation(workspaceScene =>
            {
                buildSucceeded = TryCreatePreviewRoot(
                    workspaceScene,
                    request,
                    layout,
                    floorTile,
                    wallTile,
                    out previewGrid);
            });

            if (!mutationExecuted || !buildSucceeded || previewGrid == null)
            {
                return RoomAuthoringDungeonPreviewResult.Failed(
                    "타일 미리보기를 구현하지 못했습니다. Console의 DungeonRoomBuilder 검증 메시지를 확인하세요.");
            }

            lastSnapshot = CreateSnapshot(layout, previewGrid, transientCurrentRoom);
            int currentRoomPlacementCount = CountCurrentRoomPlacements(
                layout,
                transientCurrentRoom);
            FramePreview(lastSnapshot);
            SceneView.RepaintAll();
            ResolveCorridorLengthRange(
                layout,
                out int shortestCorridorLength,
                out int longestCorridorLength);

            return new RoomAuthoringDungeonPreviewResult(
                true,
                layout.IsComplete,
                layout.Rooms.Count,
                layout.RequestedRoomCount,
                layout.Connections.Count,
                currentRoomPlacementCount,
                layout.FailureReason,
                floorTile != null ? floorTile.name : "없음",
                wallTile != null ? wallTile.name : "없음",
                shortestCorridorLength,
                longestCorridorLength,
                layout.UsedCorridorLengthRelaxation);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return RoomAuthoringDungeonPreviewResult.Failed(exception.Message);
        }
        finally
        {
            if (transientCurrentRoom != null)
                UnityEngine.Object.DestroyImmediate(transientCurrentRoom);
            if (previewLibrary != null)
                UnityEngine.Object.DestroyImmediate(previewLibrary);
        }
    }

    public static void Clear()
    {
        lastSnapshot = null;
        if (!RoomAuthoringWorkspace.IsOpen)
        {
            SceneView.RepaintAll();
            return;
        }

        RoomAuthoringWorkspace.ExecutePreviewMutation(
            _ => DestroyPreviewRoots(excludedMarker: null));
        SceneView.RepaintAll();
    }

    public static void DrawSceneHandles()
    {
        PreviewSnapshot snapshot = lastSnapshot;
        if (snapshot == null || snapshot.Grid == null || !HasPreview)
            return;

        DrawConnections(snapshot);
        DrawRooms(snapshot);
        DrawObjectMarkers(snapshot);
        DrawTravelEndpointMarkers(snapshot);
    }

    private static RoomThemeLibrarySO CreatePreviewLibrary(
        RoomAuthoringDungeonPreviewRequest request,
        out RoomTemplateSO transientCurrentRoom)
    {
        RoomThemeLibrarySO previewLibrary =
            ScriptableObject.CreateInstance<RoomThemeLibrarySO>();
        previewLibrary.hideFlags = HideFlags.HideAndDontSave;

        IReadOnlyList<RoomTemplateSO> sourceRooms = request.Library.Rooms;
        for (int roomIndex = 0; roomIndex < sourceRooms.Count; roomIndex++)
        {
            RoomTemplateSO room = sourceRooms[roomIndex];
            if (room == null ||
                (request.IncludeCurrentRoom && room == request.ReplacedTemplate))
            {
                continue;
            }

            previewLibrary.EditorAddRoom(room);
        }

        transientCurrentRoom = null;
        if (!request.IncludeCurrentRoom)
            return previewLibrary;

        transientCurrentRoom = ScriptableObject.CreateInstance<RoomTemplateSO>();
        transientCurrentRoom.name = $"[Preview] {request.CurrentLayout.roomId}";
        transientCurrentRoom.hideFlags = HideFlags.HideAndDontSave;
        transientCurrentRoom.EditorSetData(request.CurrentLayout, request.CurrentBuild);
        previewLibrary.EditorAddRoom(transientCurrentRoom);
        return previewLibrary;
    }

    private static IReadOnlyList<RoomTemplateSO> ResolvePreviewGuaranteedRooms(
        RoomAuthoringDungeonPreviewRequest request,
        RoomTemplateSO transientCurrentRoom)
    {
        if (request.GuaranteedRoomTemplates == null ||
            request.GuaranteedRoomTemplates.Count == 0)
        {
            return request.GuaranteedRoomTemplates;
        }

        var resolvedRooms = new List<RoomTemplateSO>(request.GuaranteedRoomTemplates.Count);
        for (int roomIndex = 0; roomIndex < request.GuaranteedRoomTemplates.Count; roomIndex++)
        {
            RoomTemplateSO room = request.GuaranteedRoomTemplates[roomIndex];
            if (request.IncludeCurrentRoom && room == request.ReplacedTemplate)
                room = transientCurrentRoom;

            if (room != null && !resolvedRooms.Contains(room))
                resolvedRooms.Add(room);
        }

        return resolvedRooms;
    }

    private static bool TryCreatePreviewRoot(
        Scene workspaceScene,
        RoomAuthoringDungeonPreviewRequest request,
        DungeonLayoutResult layout,
        TileBase corridorFloorTile,
        TileBase corridorWallTile,
        out Grid previewGrid)
    {
        previewGrid = null;
        RectInt layoutBounds = CalculateLayoutBounds(layout);
        GameObject root = new($"{PreviewRootName} · Seed {layout.Seed}");
        root.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        SceneManager.MoveGameObjectToScene(root, workspaceScene);
        RoomAuthoringDungeonPreviewMarker marker =
            root.AddComponent<RoomAuthoringDungeonPreviewMarker>();

        CopyGridTransform(request, root.transform, layoutBounds);
        previewGrid = root.AddComponent<Grid>();
        CopyGridSettings(request.AnchorGrid, previewGrid);

        Tilemap floorTilemap = CreateTilemapLayer(root.transform, "PreviewFloor", 0);
        Tilemap wallTilemap = CreateTilemapLayer(root.transform, "PreviewWall", 10);
        GameObject blockerRootObject = new("PreviewSocketBlockers");
        blockerRootObject.transform.SetParent(root.transform, false);

        DungeonRoomBuilder builder = root.AddComponent<DungeonRoomBuilder>();
        builder.EditorAssignTilemaps(floorTilemap, wallTilemap);
        builder.EditorAssignCorridorTiles(corridorFloorTile, corridorWallTile);
        builder.EditorAssignSocketBlockerRoot(blockerRootObject.transform);

        if (!builder.TryBuild(layout, DungeonBuildOptions.VisualOnly))
        {
            UnityEngine.Object.DestroyImmediate(root);
            previewGrid = null;
            return false;
        }

        builder.ClearGeneratedSocketBlockers();
        DestroyPreviewRoots(marker);
        return true;
    }

    private static void CopyGridTransform(
        RoomAuthoringDungeonPreviewRequest request,
        Transform previewTransform,
        RectInt layoutBounds)
    {
        Grid anchorGrid = request.AnchorGrid;
        int previewOriginCellX = request.AnchorRoomSize.x +
            PreviewGapCells -
            layoutBounds.xMin;
        Vector3 previewOrigin = anchorGrid.CellToWorld(
            new Vector3Int(previewOriginCellX, 0, 0));

        previewTransform.SetPositionAndRotation(
            previewOrigin,
            anchorGrid.transform.rotation);
        previewTransform.localScale = anchorGrid.transform.lossyScale;
    }

    private static void CopyGridSettings(Grid source, Grid destination)
    {
        destination.cellSize = source.cellSize;
        destination.cellGap = source.cellGap;
        destination.cellLayout = source.cellLayout;
        destination.cellSwizzle = source.cellSwizzle;
    }

    private static Tilemap CreateTilemapLayer(
        Transform parent,
        string layerName,
        int sortingOrder)
    {
        GameObject layerObject = new(layerName);
        layerObject.transform.SetParent(parent, false);
        Tilemap tilemap = layerObject.AddComponent<Tilemap>();
        TilemapRenderer renderer = layerObject.AddComponent<TilemapRenderer>();
        renderer.sortingOrder = sortingOrder;
        return tilemap;
    }

    private static void DestroyPreviewRoots(
        RoomAuthoringDungeonPreviewMarker excludedMarker)
    {
        RoomAuthoringDungeonPreviewMarker[] markers =
            Resources.FindObjectsOfTypeAll<RoomAuthoringDungeonPreviewMarker>();
        for (int markerIndex = 0; markerIndex < markers.Length; markerIndex++)
        {
            RoomAuthoringDungeonPreviewMarker marker = markers[markerIndex];
            if (marker == null ||
                marker == excludedMarker ||
                !RoomAuthoringWorkspace.IsInWorkspace(marker.gameObject))
            {
                continue;
            }

            UnityEngine.Object.DestroyImmediate(marker.gameObject);
        }
    }

    private static TileBase ResolveMostFrequentTile(
        RoomThemeLibrarySO library,
        bool useFloorTiles)
    {
        Dictionary<TileBase, int> counts = new();
        IReadOnlyList<RoomTemplateSO> rooms = library.Rooms;
        for (int roomIndex = 0; roomIndex < rooms.Count; roomIndex++)
        {
            RoomTemplateSO room = rooms[roomIndex];
            if (room == null)
                continue;

            List<RoomTileData> tiles = useFloorTiles
                ? room.BuildData.floorTiles
                : room.BuildData.wallTiles;
            if (tiles == null)
                continue;

            for (int tileIndex = 0; tileIndex < tiles.Count; tileIndex++)
            {
                TileBase tile = tiles[tileIndex].tile;
                if (tile == null)
                    continue;

                counts.TryGetValue(tile, out int count);
                counts[tile] = count + 1;
            }
        }

        TileBase selected = null;
        int selectedCount = -1;
        string selectedPath = string.Empty;
        foreach (KeyValuePair<TileBase, int> pair in counts)
        {
            string path = AssetDatabase.GetAssetPath(pair.Key);
            if (pair.Value > selectedCount ||
                (pair.Value == selectedCount &&
                 string.CompareOrdinal(path, selectedPath) < 0))
            {
                selected = pair.Key;
                selectedCount = pair.Value;
                selectedPath = path;
            }
        }

        return selected;
    }

    private static PreviewSnapshot CreateSnapshot(
        DungeonLayoutResult layout,
        Grid grid,
        RoomTemplateSO transientCurrentRoom)
    {
        List<PreviewRoomInfo> rooms = new(layout.Rooms.Count);
        List<PreviewObjectInfo> objects = new();
        List<PreviewTravelEndpointInfo> travelEndpoints = new();
        for (int roomIndex = 0; roomIndex < layout.Rooms.Count; roomIndex++)
        {
            DungeonRoomPlacement placement = layout.Rooms[roomIndex];
            RoomLayoutData roomLayout = placement.Template.LayoutData;
            rooms.Add(new PreviewRoomInfo(
                placement.PlacementId,
                roomLayout.roomId,
                roomLayout.roomType,
                placement.WorldBounds,
                placement.Template == transientCurrentRoom));

            List<RoomObjectPlacementData> placements =
                placement.Template.BuildData.objectPlacements;
            if (placements != null)
            {
                for (int objectIndex = 0; objectIndex < placements.Count; objectIndex++)
                {
                    RoomObjectPlacementData objectPlacement = placements[objectIndex];
                    objects.Add(new PreviewObjectInfo(
                        objectPlacement.kind,
                        placement.Origin + objectPlacement.localCell));
                }
            }

            List<RoomTravelEndpointPlacementData> endpointPlacements =
                placement.Template.BuildData.travelEndpointPlacements;
            if (endpointPlacements != null)
            {
                for (int endpointIndex = 0;
                     endpointIndex < endpointPlacements.Count;
                     endpointIndex++)
                {
                    RoomTravelEndpointPlacementData endpointPlacement =
                        endpointPlacements[endpointIndex];
                    travelEndpoints.Add(new PreviewTravelEndpointInfo(
                        endpointPlacement.kind,
                        placement.Origin + endpointPlacement.localCell,
                        endpointPlacement.localOffset,
                        endpointPlacement.localRotationDegrees,
                        RoomTravelEndpointGeometry.ResolveTriggerSize(endpointPlacement),
                        endpointPlacement.useSeparateArrivalPoint,
                        placement.Origin + endpointPlacement.arrivalLocalCell,
                        endpointPlacement.arrivalLocalOffset));
                }
            }
        }

        List<PreviewConnectionInfo> connections = new(layout.Connections.Count);
        for (int connectionIndex = 0;
             connectionIndex < layout.Connections.Count;
             connectionIndex++)
        {
            DungeonSocketConnection connection = layout.Connections[connectionIndex];
            if (!TryResolvePlacedSocket(
                    layout,
                    connection.FirstRoomPlacementId,
                    connection.FirstSocketIndex,
                    out Vector2Int firstCell,
                    out RoomSocketData firstSocket) ||
                !TryResolvePlacedSocket(
                    layout,
                    connection.SecondRoomPlacementId,
                    connection.SecondSocketIndex,
                    out Vector2Int secondCell,
                    out RoomSocketData secondSocket))
            {
                continue;
            }

            connections.Add(new PreviewConnectionInfo(
                firstCell,
                firstSocket.direction,
                RoomSocketGeometry.ResolveWidth(firstSocket),
                secondCell,
                secondSocket.direction,
                RoomSocketGeometry.ResolveWidth(secondSocket),
                connection.CorridorBounds));
        }

        return new PreviewSnapshot(
            grid,
            CalculateLayoutBounds(layout),
            rooms,
            connections,
            objects,
            travelEndpoints);
    }

    private static bool TryResolvePlacedSocket(
        DungeonLayoutResult layout,
        int placementId,
        int socketIndex,
        out Vector2Int worldCell,
        out RoomSocketData socket)
    {
        worldCell = default;
        socket = default;
        if (placementId < 0 || placementId >= layout.Rooms.Count)
            return false;

        DungeonRoomPlacement placement = layout.Rooms[placementId];
        List<RoomSocketData> sockets = placement.Template.LayoutData.sockets;
        if (sockets == null || socketIndex < 0 || socketIndex >= sockets.Count)
            return false;

        socket = sockets[socketIndex];
        worldCell = placement.Origin + socket.localCell;
        return true;
    }

    private static int CountCurrentRoomPlacements(
        DungeonLayoutResult layout,
        RoomTemplateSO transientCurrentRoom)
    {
        if (transientCurrentRoom == null)
            return 0;

        int count = 0;
        for (int roomIndex = 0; roomIndex < layout.Rooms.Count; roomIndex++)
        {
            if (layout.Rooms[roomIndex].Template == transientCurrentRoom)
                count++;
        }

        return count;
    }

    private static RectInt CalculateLayoutBounds(DungeonLayoutResult layout)
    {
        if (layout.Rooms.Count == 0)
            return new RectInt(Vector2Int.zero, Vector2Int.one);

        RectInt bounds = layout.Rooms[0].WorldBounds;
        for (int roomIndex = 1; roomIndex < layout.Rooms.Count; roomIndex++)
            bounds = Encapsulate(bounds, layout.Rooms[roomIndex].WorldBounds);
        for (int connectionIndex = 0;
             connectionIndex < layout.Connections.Count;
             connectionIndex++)
        {
            RectInt corridorBounds = layout.Connections[connectionIndex].CorridorBounds;
            if (corridorBounds.width > 0 && corridorBounds.height > 0)
                bounds = Encapsulate(bounds, corridorBounds);
        }

        return bounds;
    }

    private static RectInt Encapsulate(RectInt first, RectInt second)
    {
        int xMin = Mathf.Min(first.xMin, second.xMin);
        int yMin = Mathf.Min(first.yMin, second.yMin);
        int xMax = Mathf.Max(first.xMax, second.xMax);
        int yMax = Mathf.Max(first.yMax, second.yMax);
        return new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
    }

    private static void DrawRooms(PreviewSnapshot snapshot)
    {
        for (int roomIndex = 0; roomIndex < snapshot.Rooms.Count; roomIndex++)
        {
            PreviewRoomInfo room = snapshot.Rooms[roomIndex];
            Color roomColor = room.IsCurrentRoom
                ? new Color(0.2f, 1f, 0.45f, 1f)
                : ResolveRoomColor(room.RoomType);
            Handles.color = roomColor;
            DrawCellBounds(snapshot.Grid, room.WorldBounds);

            Vector3 labelPosition = snapshot.Grid.CellToWorld(
                new Vector3Int(room.WorldBounds.xMin, room.WorldBounds.yMax, 0));
            string currentLabel = room.IsCurrentRoom ? " · 편집 중" : string.Empty;
            Handles.Label(
                labelPosition + Vector3.up * 0.2f,
                $"#{room.PlacementId} {room.RoomType} · {room.RoomId}{currentLabel}");
        }
    }

    private static void DrawConnections(PreviewSnapshot snapshot)
    {
        for (int connectionIndex = 0;
             connectionIndex < snapshot.Connections.Count;
             connectionIndex++)
        {
            PreviewConnectionInfo connection = snapshot.Connections[connectionIndex];
            Vector3 firstCenter = ResolveSocketCenter(
                snapshot.Grid,
                connection.FirstSocketCell,
                connection.FirstDirection,
                connection.FirstWidth);
            Vector3 secondCenter = ResolveSocketCenter(
                snapshot.Grid,
                connection.SecondSocketCell,
                connection.SecondDirection,
                connection.SecondWidth);

            Handles.color = Color.cyan;
            Handles.DrawDottedLine(firstCenter, secondCenter, 4f);
            Handles.DrawSolidDisc(firstCenter, snapshot.Grid.transform.forward, 0.12f);
            Handles.DrawSolidDisc(secondCenter, snapshot.Grid.transform.forward, 0.12f);
            if (connection.CorridorBounds.width > 0 &&
                connection.CorridorBounds.height > 0)
            {
                DrawCellBounds(snapshot.Grid, connection.CorridorBounds);
            }
        }
    }

    private static void DrawObjectMarkers(PreviewSnapshot snapshot)
    {
        for (int objectIndex = 0; objectIndex < snapshot.Objects.Count; objectIndex++)
        {
            PreviewObjectInfo roomObject = snapshot.Objects[objectIndex];
            Handles.color = ResolveObjectColor(roomObject.Kind);
            Vector3 center = snapshot.Grid.GetCellCenterWorld(
                new Vector3Int(roomObject.WorldCell.x, roomObject.WorldCell.y, 0));
            Handles.DrawWireDisc(center, snapshot.Grid.transform.forward, 0.28f);
            Handles.Label(center + Vector3.up * 0.12f, ResolveObjectGlyph(roomObject.Kind));
        }
    }

    private static void DrawTravelEndpointMarkers(PreviewSnapshot snapshot)
    {
        for (int endpointIndex = 0;
             endpointIndex < snapshot.TravelEndpoints.Count;
             endpointIndex++)
        {
            PreviewTravelEndpointInfo endpoint = snapshot.TravelEndpoints[endpointIndex];
            Handles.color = ResolveTravelEndpointColor(endpoint.Kind);
            Vector3 center = ResolvePreviewWorldPosition(
                snapshot.Grid,
                endpoint.WorldCell,
                endpoint.LocalOffset);
            if (endpoint.Kind == RoomTravelEndpointKind.Trigger)
            {
                Matrix4x4 previousMatrix = Handles.matrix;
                Handles.matrix = Matrix4x4.TRS(
                    center,
                    snapshot.Grid.transform.rotation *
                    Quaternion.Euler(0f, 0f, endpoint.LocalRotationDegrees),
                    Vector3.one);
                Handles.DrawWireCube(
                    Vector3.zero,
                    new Vector3(endpoint.TriggerSize.x, endpoint.TriggerSize.y, 0.01f));
                Handles.matrix = previousMatrix;
            }
            else
            {
                Handles.DrawWireCube(center, Vector3.one * 0.62f);
            }
            Handles.Label(center + Vector3.up * 0.12f, ResolveTravelEndpointGlyph(endpoint.Kind));

            if (!endpoint.UseSeparateArrivalPoint)
                continue;

            Vector3 arrival = ResolvePreviewWorldPosition(
                snapshot.Grid,
                endpoint.ArrivalWorldCell,
                endpoint.ArrivalLocalOffset);
            Handles.color = new Color(0.25f, 1f, 0.45f, 1f);
            Handles.DrawDottedLine(center, arrival, 4f);
            Handles.DrawWireDisc(arrival, snapshot.Grid.transform.forward, 0.3f);
            Handles.Label(arrival + Vector3.up * 0.12f, "A");
        }
    }

    private static Vector3 ResolvePreviewWorldPosition(
        Grid grid,
        Vector2Int worldCell,
        Vector2 localOffset)
    {
        Vector3 center = grid.GetCellCenterWorld(
            new Vector3Int(worldCell.x, worldCell.y, 0));
        return center + grid.transform.TransformVector(
            new Vector3(localOffset.x, localOffset.y, 0f));
    }

    private static void DrawCellBounds(Grid grid, RectInt cellBounds)
    {
        Vector3 minimum = grid.CellToWorld(
            new Vector3Int(cellBounds.xMin, cellBounds.yMin, 0));
        Vector3 maximum = grid.CellToWorld(
            new Vector3Int(cellBounds.xMax, cellBounds.yMax, 0));
        Vector3 center = Vector3.Lerp(minimum, maximum, 0.5f);
        Vector3 size = new(
            Mathf.Abs(maximum.x - minimum.x),
            Mathf.Abs(maximum.y - minimum.y),
            0f);
        Handles.DrawWireCube(center, size);
    }

    private static Vector3 ResolveSocketCenter(
        Grid grid,
        Vector2Int startCell,
        RoomSocketDirection direction,
        int width)
    {
        Vector2Int tangent = RoomSocketGeometry.GetTangent(direction);
        Vector2Int endCell = startCell + tangent * Mathf.Max(0, width - 1);
        Vector3 firstCenter = grid.GetCellCenterWorld(
            new Vector3Int(startCell.x, startCell.y, 0));
        Vector3 lastCenter = grid.GetCellCenterWorld(
            new Vector3Int(endCell.x, endCell.y, 0));
        return Vector3.Lerp(firstCenter, lastCenter, 0.5f);
    }

    private static Color ResolveRoomColor(RoomType roomType)
    {
        return roomType switch
        {
            RoomType.Start => new Color(0.25f, 0.8f, 1f, 1f),
            RoomType.Boss => new Color(1f, 0.25f, 0.25f, 1f),
            RoomType.Treasure => new Color(1f, 0.8f, 0.2f, 1f),
            RoomType.Shop => new Color(0.3f, 1f, 0.8f, 1f),
            RoomType.Event => new Color(0.8f, 0.45f, 1f, 1f),
            RoomType.Exit => Color.white,
            _ => new Color(1f, 0.55f, 0.2f, 1f)
        };
    }

    private static Color ResolveObjectColor(RoomObjectKind kind)
    {
        return kind switch
        {
            RoomObjectKind.Monster => new Color(1f, 0.2f, 0.2f, 1f),
            RoomObjectKind.Chest => new Color(1f, 0.8f, 0.15f, 1f),
            RoomObjectKind.Portal => new Color(0.65f, 0.3f, 1f, 1f),
            _ => Color.white
        };
    }

    private static string ResolveObjectGlyph(RoomObjectKind kind)
    {
        return kind switch
        {
            RoomObjectKind.Monster => "M",
            RoomObjectKind.Chest => "C",
            RoomObjectKind.Portal => "P",
            _ => "O"
        };
    }

    private static Color ResolveTravelEndpointColor(RoomTravelEndpointKind kind)
    {
        return kind switch
        {
            RoomTravelEndpointKind.Interaction => new Color(0.95f, 0.35f, 1f, 1f),
            RoomTravelEndpointKind.Trigger => new Color(0.2f, 1f, 0.9f, 1f),
            RoomTravelEndpointKind.ArrivalOnly => new Color(0.35f, 0.65f, 1f, 1f),
            _ => Color.white
        };
    }

    private static string ResolveTravelEndpointGlyph(RoomTravelEndpointKind kind)
    {
        return kind switch
        {
            RoomTravelEndpointKind.Interaction => "EI",
            RoomTravelEndpointKind.Trigger => "ET",
            RoomTravelEndpointKind.ArrivalOnly => "EA",
            _ => "E"
        };
    }

    private static void ResolveCorridorLengthRange(
        DungeonLayoutResult layout,
        out int shortestLength,
        out int longestLength)
    {
        shortestLength = 0;
        longestLength = 0;
        if (layout == null || layout.Connections.Count == 0)
            return;

        shortestLength = int.MaxValue;
        for (int connectionIndex = 0;
             connectionIndex < layout.Connections.Count;
             connectionIndex++)
        {
            int length = layout.Connections[connectionIndex].CorridorLength;
            shortestLength = Mathf.Min(shortestLength, length);
            longestLength = Mathf.Max(longestLength, length);
        }
    }

    private static void FramePreview(PreviewSnapshot snapshot)
    {
        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView == null || snapshot.Grid == null)
            return;

        Vector3 minimum = snapshot.Grid.CellToWorld(
            new Vector3Int(snapshot.LayoutBounds.xMin, snapshot.LayoutBounds.yMin, 0));
        Vector3 maximum = snapshot.Grid.CellToWorld(
            new Vector3Int(snapshot.LayoutBounds.xMax, snapshot.LayoutBounds.yMax, 0));
        Bounds worldBounds = new(Vector3.Lerp(minimum, maximum, 0.5f), new Vector3(
            Mathf.Max(4f, Mathf.Abs(maximum.x - minimum.x) + 4f),
            Mathf.Max(4f, Mathf.Abs(maximum.y - minimum.y) + 4f),
            1f));
        sceneView.Frame(worldBounds, instant: false);
    }
}
