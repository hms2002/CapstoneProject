using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// 책임:
/// - 완성 복도 미리보기의 길이와 조립된 모듈 구간을 Scene View 표식으로 보여준다.
/// - 미리보기 루트를 식별해 편집 중인 모듈이나 다른 던전 프리뷰와 독립적으로 제거할 수 있게 한다.
/// </summary>
[DisallowMultipleComponent]
internal sealed class CorridorDecorationCompletedPreviewMarker : MonoBehaviour
{
    private int corridorLength;
    private CorridorDecorationAxis axis;
    private Grid grid;
    private readonly List<CorridorDecorationPreviewSegment> segments = new();

    public void EditorConfigure(
        int length,
        CorridorDecorationAxis previewAxis,
        Grid previewGrid,
        IReadOnlyList<CorridorDecorationPlacement> placements)
    {
        corridorLength = Mathf.Max(1, length);
        axis = previewAxis;
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

        DrawModuleLabels();
        Handles.color = new Color(0.35f, 0.9f, 1f, 0.95f);
        Handles.Label(CellCenterByProgress(0, 3), "문 A");
        Handles.Label(CellCenterByProgress(corridorLength - 1, 3), "문 B");
    }

    private void DrawModuleLabels()
    {
        Handles.color = Color.white;
        for (int segmentIndex = 0; segmentIndex < segments.Count; segmentIndex++)
        {
            CorridorDecorationPreviewSegment segment = segments[segmentIndex];
            int labelCell = segment.ForwardOffset + Mathf.Max(0, segment.Length - 1) / 2;
            Handles.Label(
                CellCenterByProgress(labelCell, 2) + Vector3.up * 0.65f,
                $"{segment.ModuleId}\n[{segment.ForwardOffset}.." +
                $"{segment.ForwardOffset + segment.Length - 1}]");
        }
    }

    private Vector3 CellCenterByProgress(int progress, int lateral)
    {
        int x = axis == CorridorDecorationAxis.Horizontal ? progress : lateral;
        int y = axis == CorridorDecorationAxis.Horizontal ? lateral : progress;
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
/// - 테마 룸 라이브러리의 기본 Floor/Wall 위에 선택한 가로(+X) 또는 세로(+Y) 전용 모듈을 실제 좌표 그대로 조립한다.
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
        int connectionIndex = 0,
        CorridorDecorationAxis axis = CorridorDecorationAxis.Horizontal)
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
                connectionIndex,
                axis);

        try
        {
            RoomAuthoringDungeonPreview.Clear();
            bool created = RoomAuthoringWorkspace.ExecutePreviewMutation(
                workspaceScene => Build(
                    workspaceScene,
                    generationProfile,
                    decorationProfile,
                    corridorLength,
                    axis,
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
                    corridorLength,
                    seed,
                    connectionIndex,
                    axis,
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
        CorridorDecorationAxis axis,
        IReadOnlyList<CorridorDecorationPlacement> placements)
    {
        ClearExistingRoots(workspaceScene);

        GameObject root = new($"{PreviewRootName} · {axis} · {corridorLength} cells");
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
            axis,
            tilemaps);
        for (int placementIndex = 0; placementIndex < placements.Count; placementIndex++)
            PaintModule(grid, tilemaps, placements[placementIndex], placementIndex, axis);

        marker.EditorConfigure(
            corridorLength,
            axis,
            grid,
            placements);
        Selection.activeObject = root;
        Frame(root.transform.position, corridorLength, axis);
    }

    private static void PaintBaseCorridor(
        RoomThemeLibrarySO roomLibrary,
        CorridorDecorationProfileSO decorationProfile,
        int corridorLength,
        CorridorDecorationAxis axis,
        IReadOnlyDictionary<RoomTileLayerKind, Tilemap> tilemaps)
    {
        TileBase underFloor = ResolveBaseTile(
            roomLibrary,
            decorationProfile,
            RoomTileLayerKind.UnderFloor,
            axis);
        TileBase floor = ResolveBaseTile(
            roomLibrary,
            decorationProfile,
            RoomTileLayerKind.Floor,
            axis);
        TileBase wall = ResolveBaseTile(
            roomLibrary,
            decorationProfile,
            RoomTileLayerKind.Wall,
            axis);
        if (floor == null || wall == null)
        {
            throw new InvalidOperationException(
                "완성 복도 미리보기에는 테마 룸 라이브러리나 장식 모듈의 Floor/Wall 타일이 필요합니다.");
        }

        for (int progress = 0; progress < corridorLength; progress++)
        {
            SetTileByProgress(tilemaps[RoomTileLayerKind.UnderFloor], progress, 0, underFloor, axis);
            SetTileByProgress(tilemaps[RoomTileLayerKind.UnderFloor], progress, 1, underFloor, axis);
            SetTileByProgress(tilemaps[RoomTileLayerKind.Floor], progress, 0, floor, axis);
            SetTileByProgress(tilemaps[RoomTileLayerKind.Floor], progress, 1, floor, axis);
            SetTileByProgress(tilemaps[RoomTileLayerKind.Wall], progress, -1, wall, axis);
            SetTileByProgress(tilemaps[RoomTileLayerKind.Wall], progress, 2, wall, axis);
        }
    }

    private static void PaintModule(
        Grid grid,
        IReadOnlyDictionary<RoomTileLayerKind, Tilemap> tilemaps,
        CorridorDecorationPlacement placement,
        int placementIndex,
        CorridorDecorationAxis axis)
    {
        CorridorDecorationModuleSO module = placement.Module;
        if (module == null)
            return;
        if (module.Axis != axis)
        {
            throw new InvalidOperationException(
                $"'{module.ModuleId}'의 축 {module.Axis}이 미리보기 축 {axis}과 다릅니다.");
        }

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
                Vector2Int previewCell = AddForwardOffset(
                    tile.localCell,
                    placement.ForwardOffset,
                    axis);
                SetTile(
                    tilemaps[layer],
                    previewCell.x,
                    previewCell.y,
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
                objectPlacement,
                axis);
        }
    }

    private static void CreatePropPreview(
        Grid grid,
        int forwardOffset,
        int placementIndex,
        int objectIndex,
        RoomObjectPlacementData placement,
        CorridorDecorationAxis axis)
    {
        GameObject instance = PrefabUtility.InstantiatePrefab(
            placement.prefab,
            grid.transform) as GameObject;
        if (instance == null)
            instance = UnityEngine.Object.Instantiate(placement.prefab, grid.transform);
        if (instance == null)
            throw new InvalidOperationException($"'{placement.placementId}' 프리팹을 생성할 수 없습니다.");

        Vector2Int previewCell = AddForwardOffset(
            placement.localCell,
            forwardOffset,
            axis);
        Vector3 cellCenter = grid.GetCellCenterWorld(
            new Vector3Int(previewCell.x, previewCell.y, 0));
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
        RoomTileLayerKind layer,
        CorridorDecorationAxis axis)
    {
        TileBase fromRooms = ResolveMostFrequentRoomTile(roomLibrary, layer);
        if (fromRooms != null)
            return fromRooms;

        IReadOnlyList<CorridorDecorationModuleSO> modules = decorationProfile.Modules;
        for (int moduleIndex = 0; moduleIndex < modules.Count; moduleIndex++)
        {
            CorridorDecorationModuleSO module = modules[moduleIndex];
            List<RoomTileData> tiles = module != null && module.Axis == axis
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

    private static void SetTileByProgress(
        Tilemap tilemap,
        int progress,
        int lateral,
        TileBase tile,
        CorridorDecorationAxis axis)
    {
        if (axis == CorridorDecorationAxis.Horizontal)
            SetTile(tilemap, progress, lateral, tile);
        else
            SetTile(tilemap, lateral, progress, tile);
    }

    private static Vector2Int AddForwardOffset(
        Vector2Int localCell,
        int forwardOffset,
        CorridorDecorationAxis axis)
    {
        return axis == CorridorDecorationAxis.Horizontal
            ? localCell + Vector2Int.right * forwardOffset
            : localCell + Vector2Int.up * forwardOffset;
    }

    private static void ValidateLocalCell(
        CorridorDecorationModuleSO module,
        Vector2Int cell,
        string contentName)
    {
        bool inside = module.Axis == CorridorDecorationAxis.Horizontal
            ? cell.x >= 0 && cell.x < module.Length && cell.y >= -1 && cell.y <= 2
            : cell.y >= 0 && cell.y < module.Length && cell.x >= -1 && cell.x <= 2;
        if (inside)
            return;

        string expected = module.Axis == CorridorDecorationAxis.Horizontal
            ? $"x=0..{module.Length - 1}, y=-1..2"
            : $"x=-1..2, y=0..{module.Length - 1}";
        throw new InvalidOperationException(
            $"'{module.ModuleId}' {contentName} {cell}이 {expected} 범위를 벗어났습니다.");
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
        int corridorLength,
        int seed,
        int connectionIndex,
        CorridorDecorationAxis axis,
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
        string axisLabel = axis == CorridorDecorationAxis.Horizontal ? "가로(+X)" : "세로(+Y)";
        return $"{axisLabel} · 전체 {corridorLength}칸 · Seed {seed} · " +
               $"연결 번호 {connectionIndex}\n{sequence}";
    }

    private static void Frame(
        Vector3 rootPosition,
        int corridorLength,
        CorridorDecorationAxis axis)
    {
        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView == null)
            return;

        Vector3 center = rootPosition + (axis == CorridorDecorationAxis.Horizontal
            ? new Vector3(corridorLength * 0.5f, 0.5f, 0f)
            : new Vector3(0.5f, corridorLength * 0.5f, 0f));
        Vector3 size = axis == CorridorDecorationAxis.Horizontal
            ? new Vector3(Mathf.Max(6f, corridorLength + 3f), 8f, 1f)
            : new Vector3(8f, Mathf.Max(6f, corridorLength + 3f), 1f);
        Bounds bounds = new(center, size);
        sceneView.Frame(bounds, instant: false);
    }
}
