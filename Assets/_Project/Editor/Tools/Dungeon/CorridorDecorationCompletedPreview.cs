using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// 책임:
/// - 완성 복도 미리보기의 길이, 문 안전 여백과 조립된 모듈 구간을 Scene View 표식으로 보여준다.
/// - 미리보기 루트를 식별해 편집 중인 모듈이나 다른 던전 프리뷰와 독립적으로 제거할 수 있게 한다.
/// </summary>
[DisallowMultipleComponent]
internal sealed class CorridorDecorationCompletedPreviewMarker : MonoBehaviour
{
    private int corridorLength;
    private int doorClearance;
    private Grid grid;
    private readonly List<CorridorDecorationPreviewSegment> segments = new();

    public void EditorConfigure(
        int length,
        int clearance,
        Grid previewGrid,
        IReadOnlyList<CorridorDecorationPlacement> placements)
    {
        corridorLength = Mathf.Max(1, length);
        doorClearance = Mathf.Clamp(clearance, 0, corridorLength / 2);
        grid = previewGrid;
        segments.Clear();
        if (placements == null)
            return;

        for (int placementIndex = 0; placementIndex < placements.Count; placementIndex++)
        {
            CorridorDecorationPlacement placement = placements[placementIndex];
            if (placement.Module != null)
            {
                segments.Add(new CorridorDecorationPreviewSegment(
                    placement.Module.ModuleId,
                    placement.ForwardOffset,
                    placement.Module.Length));
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (grid == null || corridorLength <= 0)
            return;

        DrawClearanceCells();
        DrawModuleLabels();
        Handles.color = new Color(0.35f, 0.9f, 1f, 0.95f);
        Handles.Label(CellCenter(0, 3), "문 A");
        Handles.Label(CellCenter(corridorLength - 1, 3), "문 B");
    }

    private void DrawClearanceCells()
    {
        Gizmos.color = new Color(1f, 0.35f, 0.2f, 0.18f);
        for (int x = 0; x < doorClearance; x++)
        {
            Gizmos.DrawCube(CellCenter(x, 0), new Vector3(0.92f, 0.92f, 0.02f));
            Gizmos.DrawCube(CellCenter(x, 1), new Vector3(0.92f, 0.92f, 0.02f));
        }

        for (int x = corridorLength - doorClearance; x < corridorLength; x++)
        {
            Gizmos.DrawCube(CellCenter(x, 0), new Vector3(0.92f, 0.92f, 0.02f));
            Gizmos.DrawCube(CellCenter(x, 1), new Vector3(0.92f, 0.92f, 0.02f));
        }
    }

    private void DrawModuleLabels()
    {
        Handles.color = Color.white;
        for (int segmentIndex = 0; segmentIndex < segments.Count; segmentIndex++)
        {
            CorridorDecorationPreviewSegment segment = segments[segmentIndex];
            int labelCell = segment.ForwardOffset + Mathf.Max(0, segment.Length - 1) / 2;
            Handles.Label(
                CellCenter(labelCell, 2) + Vector3.up * 0.65f,
                $"{segment.ModuleId}\n[{segment.ForwardOffset}.." +
                $"{segment.ForwardOffset + segment.Length - 1}]");
        }
    }

    private Vector3 CellCenter(int x, int y)
    {
        return grid.GetCellCenterWorld(new Vector3Int(x, y, 0));
    }
}

/// <summary>
/// 책임 : Scene View에 표시할 한 장식 모듈의 ID와 전체 복도 진행축상 점유 구간을 보관한다.
/// </summary>
internal readonly struct CorridorDecorationPreviewSegment
{
    public string ModuleId { get; }
    public int ForwardOffset { get; }
    public int Length { get; }

    public CorridorDecorationPreviewSegment(string moduleId, int forwardOffset, int length)
    {
        ModuleId = moduleId ?? string.Empty;
        ForwardOffset = Mathf.Max(0, forwardOffset);
        Length = Mathf.Max(1, length);
    }
}

/// <summary>
/// 책임 : 완성 복도 미리보기 생성 성공 여부와 기획자가 확인할 조립 순서 설명을 반환한다.
/// </summary>
internal readonly struct CorridorDecorationCompletedPreviewResult
{
    public bool Success { get; }
    public string Message { get; }

    public CorridorDecorationCompletedPreviewResult(bool success, string message)
    {
        Success = success;
        Message = message ?? string.Empty;
    }
}

/// <summary>
/// 책임:
/// - 입력받은 전체 복도 길이와 Seed를 런타임 CorridorDecorationComposer에 전달해 같은 모듈 순서를 계산한다.
/// - 테마 룸 라이브러리의 기본 Floor/Wall 위에 8개 장식 레이어와 Pivot 프롭을 +X 완성 복도로 조립한다.
/// - 결과를 저장되지 않는 Room Authoring 작업 공간에만 생성하고 원본 씬과 에셋을 변경하지 않는다.
/// </summary>
internal static class CorridorDecorationCompletedPreview
{
    private const string PreviewRootName = "[Preview] Completed Corridor";
    private const float PreviewVerticalOffset = -8f;

    public static CorridorDecorationCompletedPreviewResult Show(
        DungeonGenerationProfileSO generationProfile,
        CorridorDecorationProfileSO decorationProfile,
        int corridorLength,
        int seed,
        int connectionIndex = 0)
    {
        if (decorationProfile == null)
        {
            return new CorridorDecorationCompletedPreviewResult(
                false,
                "장식 프로필이 필요합니다.");
        }

        corridorLength = Mathf.Clamp(corridorLength, 1, 512);
        connectionIndex = Mathf.Max(0, connectionIndex);
        List<CorridorDecorationPlacement> placements =
            CorridorDecorationComposer.Compose(
                decorationProfile,
                corridorLength,
                seed,
                connectionIndex);

        try
        {
            RoomAuthoringDungeonPreview.Clear();
            bool created = RoomAuthoringWorkspace.ExecutePreviewMutation(
                workspaceScene => Build(
                    workspaceScene,
                    generationProfile,
                    decorationProfile,
                    corridorLength,
                    placements));
            if (!created)
            {
                return new CorridorDecorationCompletedPreviewResult(
                    false,
                    "안전 작업 공간을 열 수 없습니다.");
            }

            SceneView.RepaintAll();
            return new CorridorDecorationCompletedPreviewResult(
                true,
                BuildSummary(
                    decorationProfile,
                    corridorLength,
                    seed,
                    connectionIndex,
                    placements));
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Clear();
            return new CorridorDecorationCompletedPreviewResult(false, exception.Message);
        }
    }

    public static void Clear()
    {
        if (!RoomAuthoringWorkspace.IsOpen)
            return;

        RoomAuthoringWorkspace.ExecutePreviewMutation(
            _ =>
            {
                CorridorDecorationCompletedPreviewMarker[] markers =
                    Resources.FindObjectsOfTypeAll<CorridorDecorationCompletedPreviewMarker>();
                for (int markerIndex = 0; markerIndex < markers.Length; markerIndex++)
                {
                    CorridorDecorationCompletedPreviewMarker marker = markers[markerIndex];
                    if (marker != null && RoomAuthoringWorkspace.IsInWorkspace(marker.gameObject))
                        UnityEngine.Object.DestroyImmediate(marker.gameObject);
                }
            });
        SceneView.RepaintAll();
    }

    private static void Build(
        Scene workspaceScene,
        DungeonGenerationProfileSO generationProfile,
        CorridorDecorationProfileSO decorationProfile,
        int corridorLength,
        IReadOnlyList<CorridorDecorationPlacement> placements)
    {
        ClearExistingRoots(workspaceScene);

        GameObject root = new($"{PreviewRootName} · {corridorLength} cells");
        root.transform.position = new Vector3(0f, PreviewVerticalOffset, 0f);
        SceneManager.MoveGameObjectToScene(root, workspaceScene);
        CorridorDecorationCompletedPreviewMarker marker =
            root.AddComponent<CorridorDecorationCompletedPreviewMarker>();

        GameObject gridObject = new("PreviewGrid");
        gridObject.transform.SetParent(root.transform, false);
        Grid grid = gridObject.AddComponent<Grid>();
        var tilemaps = new Dictionary<RoomTileLayerKind, Tilemap>();
        for (int layerIndex = 0;
             layerIndex < RoomTileLayerContract.OrderedLayers.Count;
             layerIndex++)
        {
            RoomTileLayerKind layer = RoomTileLayerContract.OrderedLayers[layerIndex];
            tilemaps[layer] = CreateTilemap(gridObject.transform, layer);
        }

        PaintBaseCorridor(
            generationProfile != null ? generationProfile.RoomLibrary : null,
            decorationProfile,
            corridorLength,
            tilemaps);
        for (int placementIndex = 0; placementIndex < placements.Count; placementIndex++)
            PaintModule(grid, tilemaps, placements[placementIndex], placementIndex);

        marker.EditorConfigure(
            corridorLength,
            decorationProfile.DoorClearanceCells,
            grid,
            placements);
        Selection.activeObject = root;
        Frame(root.transform.position, corridorLength);
    }

    private static void PaintBaseCorridor(
        RoomThemeLibrarySO roomLibrary,
        CorridorDecorationProfileSO decorationProfile,
        int corridorLength,
        IReadOnlyDictionary<RoomTileLayerKind, Tilemap> tilemaps)
    {
        TileBase underFloor = ResolveBaseTile(
            roomLibrary,
            decorationProfile,
            RoomTileLayerKind.UnderFloor);
        TileBase floor = ResolveBaseTile(
            roomLibrary,
            decorationProfile,
            RoomTileLayerKind.Floor);
        TileBase wall = ResolveBaseTile(
            roomLibrary,
            decorationProfile,
            RoomTileLayerKind.Wall);
        if (floor == null || wall == null)
        {
            throw new InvalidOperationException(
                "완성 복도 미리보기에는 테마 룸 라이브러리나 장식 모듈의 Floor/Wall 타일이 필요합니다.");
        }

        for (int x = 0; x < corridorLength; x++)
        {
            SetTile(tilemaps[RoomTileLayerKind.UnderFloor], x, 0, underFloor);
            SetTile(tilemaps[RoomTileLayerKind.UnderFloor], x, 1, underFloor);
            SetTile(tilemaps[RoomTileLayerKind.Floor], x, 0, floor);
            SetTile(tilemaps[RoomTileLayerKind.Floor], x, 1, floor);
            SetTile(tilemaps[RoomTileLayerKind.Wall], x, -1, wall);
            SetTile(tilemaps[RoomTileLayerKind.Wall], x, 2, wall);
        }
    }

    private static void PaintModule(
        Grid grid,
        IReadOnlyDictionary<RoomTileLayerKind, Tilemap> tilemaps,
        CorridorDecorationPlacement placement,
        int placementIndex)
    {
        CorridorDecorationModuleSO module = placement.Module;
        if (module == null)
            return;

        RoomBuildData build = module.BuildData;
        for (int layerIndex = 0;
             layerIndex < RoomTileLayerContract.OrderedLayers.Count;
             layerIndex++)
        {
            RoomTileLayerKind layer = RoomTileLayerContract.OrderedLayers[layerIndex];
            List<RoomTileData> tiles = build.GetTiles(layer);
            if (tiles == null)
                continue;

            for (int tileIndex = 0; tileIndex < tiles.Count; tileIndex++)
            {
                RoomTileData tile = tiles[tileIndex];
                if (tile.tile == null)
                    continue;
                ValidateLocalCell(module, tile.localCell, $"{layer} tile");
                SetTile(
                    tilemaps[layer],
                    placement.ForwardOffset + tile.localCell.x,
                    tile.localCell.y,
                    tile.tile);
            }
        }

        if (build.objectPlacements == null)
            return;

        for (int objectIndex = 0; objectIndex < build.objectPlacements.Count; objectIndex++)
        {
            RoomObjectPlacementData objectPlacement = build.objectPlacements[objectIndex];
            if (objectPlacement.kind != RoomObjectKind.Prop || objectPlacement.prefab == null)
            {
                throw new InvalidOperationException(
                    $"'{module.ModuleId}'의 '{objectPlacement.placementId}'은 유효한 GroundProp이 아닙니다.");
            }

            ValidateLocalCell(module, objectPlacement.localCell, "object Pivot");
            CreatePropPreview(
                grid,
                placement.ForwardOffset,
                placementIndex,
                objectIndex,
                objectPlacement);
        }
    }

    private static void CreatePropPreview(
        Grid grid,
        int forwardOffset,
        int placementIndex,
        int objectIndex,
        RoomObjectPlacementData placement)
    {
        GameObject instance = PrefabUtility.InstantiatePrefab(
            placement.prefab,
            grid.transform) as GameObject;
        if (instance == null)
            instance = UnityEngine.Object.Instantiate(placement.prefab, grid.transform);
        if (instance == null)
            throw new InvalidOperationException($"'{placement.placementId}' 프리팹을 생성할 수 없습니다.");

        int x = forwardOffset + placement.localCell.x;
        Vector3 cellCenter = grid.GetCellCenterWorld(
            new Vector3Int(x, placement.localCell.y, 0));
        Vector3 localOffset = new(placement.localOffset.x, placement.localOffset.y, 0f);
        instance.transform.position = cellCenter + grid.transform.TransformVector(localOffset);
        instance.transform.rotation = grid.transform.rotation *
                                      Quaternion.Euler(0f, 0f, placement.localRotationDegrees);
        instance.transform.localScale = placement.localScale == Vector3.zero
            ? placement.prefab.transform.localScale
            : placement.localScale;
        instance.name =
            $"PreviewProp_{placementIndex}_{objectIndex}_{placement.placementId}";
    }

    private static TileBase ResolveBaseTile(
        RoomThemeLibrarySO roomLibrary,
        CorridorDecorationProfileSO decorationProfile,
        RoomTileLayerKind layer)
    {
        TileBase fromRooms = ResolveMostFrequentRoomTile(roomLibrary, layer);
        if (fromRooms != null)
            return fromRooms;

        IReadOnlyList<CorridorDecorationModuleSO> modules = decorationProfile.Modules;
        for (int moduleIndex = 0; moduleIndex < modules.Count; moduleIndex++)
        {
            CorridorDecorationModuleSO module = modules[moduleIndex];
            List<RoomTileData> tiles = module != null
                ? module.BuildData.GetTiles(layer)
                : null;
            if (tiles == null)
                continue;

            for (int tileIndex = 0; tileIndex < tiles.Count; tileIndex++)
            {
                if (tiles[tileIndex].tile != null)
                    return tiles[tileIndex].tile;
            }
        }

        return null;
    }

    private static TileBase ResolveMostFrequentRoomTile(
        RoomThemeLibrarySO library,
        RoomTileLayerKind layer)
    {
        if (library == null || library.Rooms == null)
            return null;

        var counts = new Dictionary<TileBase, int>();
        for (int roomIndex = 0; roomIndex < library.Rooms.Count; roomIndex++)
        {
            RoomTemplateSO room = library.Rooms[roomIndex];
            List<RoomTileData> tiles = room != null
                ? room.BuildData.GetTiles(layer)
                : null;
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

        TileBase best = null;
        int bestCount = -1;
        string bestPath = string.Empty;
        foreach (KeyValuePair<TileBase, int> pair in counts)
        {
            string path = AssetDatabase.GetAssetPath(pair.Key);
            if (pair.Value > bestCount ||
                (pair.Value == bestCount && string.CompareOrdinal(path, bestPath) < 0))
            {
                best = pair.Key;
                bestCount = pair.Value;
                bestPath = path;
            }
        }

        return best;
    }

    private static Tilemap CreateTilemap(Transform parent, RoomTileLayerKind layer)
    {
        GameObject layerObject = new(RoomTileLayerContract.GetLayerName(layer));
        layerObject.transform.SetParent(parent, false);
        Tilemap tilemap = layerObject.AddComponent<Tilemap>();
        TilemapRenderer renderer = layerObject.AddComponent<TilemapRenderer>();
        renderer.sortingLayerName = RoomTileLayerContract.GetSortingLayerName(layer);
        renderer.sortingOrder = RoomTileLayerContract.GetSortingOrder(layer);
        return tilemap;
    }

    private static void SetTile(Tilemap tilemap, int x, int y, TileBase tile)
    {
        if (tilemap == null || tile == null)
            return;

        Vector3Int cell = new(x, y, 0);
        tilemap.SetTile(cell, tile);
        tilemap.SetTileFlags(cell, TileFlags.None);
        tilemap.SetTransformMatrix(cell, Matrix4x4.identity);
    }

    private static void ValidateLocalCell(
        CorridorDecorationModuleSO module,
        Vector2Int cell,
        string contentName)
    {
        if (cell.x >= 0 && cell.x < module.Length && cell.y >= -1 && cell.y <= 2)
            return;

        throw new InvalidOperationException(
            $"'{module.ModuleId}' {contentName} {cell}이 x=0..{module.Length - 1}, y=-1..2 범위를 벗어났습니다.");
    }

    private static void ClearExistingRoots(Scene workspaceScene)
    {
        GameObject[] roots = workspaceScene.GetRootGameObjects();
        for (int rootIndex = roots.Length - 1; rootIndex >= 0; rootIndex--)
        {
            if (roots[rootIndex].GetComponent<CorridorDecorationCompletedPreviewMarker>() != null)
                UnityEngine.Object.DestroyImmediate(roots[rootIndex]);
        }
    }

    private static string BuildSummary(
        CorridorDecorationProfileSO profile,
        int corridorLength,
        int seed,
        int connectionIndex,
        IReadOnlyList<CorridorDecorationPlacement> placements)
    {
        var parts = new List<string>(placements.Count);
        for (int placementIndex = 0; placementIndex < placements.Count; placementIndex++)
        {
            CorridorDecorationPlacement placement = placements[placementIndex];
            if (placement.Module != null)
            {
                parts.Add(
                    $"{placement.Module.ModuleId} " +
                    $"[{placement.ForwardOffset}..{placement.EndOffsetExclusive - 1}]");
            }
        }

        string sequence = parts.Count > 0
            ? string.Join(" → ", parts)
            : "배치 모듈 없음";
        return $"전체 {corridorLength}칸 · 문 앞 여백 {profile.DoorClearanceCells}칸 · " +
               $"Seed {seed} · 연결 번호 {connectionIndex}\n{sequence}";
    }

    private static void Frame(Vector3 rootPosition, int corridorLength)
    {
        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView == null)
            return;

        Vector3 center = rootPosition + new Vector3(corridorLength * 0.5f, 0.5f, 0f);
        Bounds bounds = new(center, new Vector3(Mathf.Max(6f, corridorLength + 3f), 8f, 1f));
        sceneView.Frame(bounds, instant: false);
    }
}
