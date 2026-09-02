using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 책임:
/// - 복도 장식 제작 작업 공간의 +X 진행축 길이, 역할, 고정 Tilemap 슬롯과 원본 모듈 참조를 보관한다.
/// - 제작 툴이 레이어 타일과 Pivot 기반 GroundProp을 CorridorDecorationModuleSO로 Bake할 기준점을 제공한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class CorridorDecorationModuleAuthoring : MonoBehaviour
{
    [SerializeField] private string moduleId = "Corridor_Module";
    [SerializeField] private CorridorDecorationModuleRole role =
        CorridorDecorationModuleRole.Middle;
    [SerializeField, Min(1)] private int length = 2;
    [SerializeField] private Grid grid;
    [SerializeField] private Tilemap underFloorTilemap;
    [SerializeField] private Tilemap floorTilemap;
    [SerializeField] private Tilemap floorDetailTilemap;
    [SerializeField] private Tilemap groundDecorationTilemap;
    [SerializeField] private Tilemap wallTilemap;
    [SerializeField] private Tilemap wallDetailTilemap;
    [SerializeField] private Tilemap foregroundTilemap;
    [SerializeField] private Tilemap overlayFxTilemap;
    [HideInInspector, SerializeField] private CorridorDecorationModuleSO sourceModule;

    public string ModuleId => moduleId;
    public CorridorDecorationModuleRole Role => role;
    public int Length => Mathf.Max(1, length);
    public Grid Grid => grid;
    public CorridorDecorationModuleSO SourceModule => sourceModule;

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
    /// <summary>
    /// 책임 : 제작 창 입력값과 편집 대상 에셋을 임시 복도 조각 루트에 적용한다.
    /// </summary>
    public void EditorConfigure(
        string id,
        CorridorDecorationModuleRole moduleRole,
        int moduleLength,
        CorridorDecorationModuleSO editedModule)
    {
        moduleId = id ?? string.Empty;
        role = moduleRole;
        length = Mathf.Max(1, moduleLength);
        sourceModule = editedModule;
    }

    /// <summary>
    /// 책임 : 툴이 생성한 Grid와 8개 고정 레이어 참조를 한 번에 연결한다.
    /// </summary>
    public void EditorAssignTilemaps(
        Grid targetGrid,
        Tilemap underFloor,
        Tilemap floor,
        Tilemap floorDetail,
        Tilemap groundDecoration,
        Tilemap wall,
        Tilemap wallDetail,
        Tilemap foreground,
        Tilemap overlayFx)
    {
        grid = targetGrid;
        underFloorTilemap = underFloor;
        floorTilemap = floor;
        floorDetailTilemap = floorDetail;
        groundDecorationTilemap = groundDecoration;
        wallTilemap = wall;
        wallDetailTilemap = wallDetail;
        foregroundTilemap = foreground;
        overlayFxTilemap = overlayFx;
    }

    private void OnValidate()
    {
        moduleId ??= string.Empty;
        length = Mathf.Max(1, length);
    }
#endif

    private void OnDrawGizmos()
    {
        if (grid == null)
            return;

        Vector3 bottomLeft = grid.CellToWorld(new Vector3Int(0, -1, 0));
        Vector3 bottomRight = grid.CellToWorld(new Vector3Int(Length, -1, 0));
        Vector3 topRight = grid.CellToWorld(new Vector3Int(Length, 3, 0));
        Vector3 topLeft = grid.CellToWorld(new Vector3Int(0, 3, 0));
        Gizmos.color = new Color(0.2f, 0.9f, 1f);
        Gizmos.DrawLine(bottomLeft, bottomRight);
        Gizmos.DrawLine(bottomRight, topRight);
        Gizmos.DrawLine(topRight, topLeft);
        Gizmos.DrawLine(topLeft, bottomLeft);
    }
}
