using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 책임:
/// - 절차 생성용 방 조각 authoring 루트가 가진 메타데이터와 고정 Grid/Tilemap 슬롯 참조를 보관한다.
/// - Editor Room Piece 툴이 방 조각을 검증하고 RoomTemplateSO로 bake할 때 기준점 역할을 한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class RoomPieceAuthoring : MonoBehaviour
{
    [Header("Room Metadata")]
    [SerializeField] private string roomId = "Room_New";
    [SerializeField] private RoomType roomType = RoomType.Combat;
    [SerializeField] private Vector2Int size = new(12, 8);
    [SerializeField, Min(0)] private int difficultyTier;
    [SerializeField, Min(0f)] private float selectionWeight = 1f;
    [SerializeField] private RoomTopologyPlacementData topologyPlacement;

    [Header("Authoring Tilemaps")]
    [SerializeField] private Grid grid;
    [SerializeField] private Tilemap underFloorTilemap;
    [SerializeField] private Tilemap floorTilemap;
    [SerializeField] private Tilemap floorDetailTilemap;
    [SerializeField] private Tilemap groundDecorationTilemap;
    [SerializeField] private Tilemap wallTilemap;
    [SerializeField] private Tilemap wallDetailTilemap;
    [SerializeField] private Tilemap foregroundTilemap;
    [SerializeField] private Tilemap overlayFxTilemap;

    [HideInInspector, SerializeField] private RoomTemplateSO sourceTemplate;

    public string RoomId => roomId;
    public RoomType RoomType => roomType;
    public Vector2Int Size => size;
    public int DifficultyTier => difficultyTier;
    public float SelectionWeight => selectionWeight;
    public RoomTopologyPlacementData TopologyPlacement => topologyPlacement;
    public Grid Grid => grid;
    public Tilemap UnderFloorTilemap => underFloorTilemap;
    public Tilemap FloorTilemap => floorTilemap;
    public Tilemap FloorDetailTilemap => floorDetailTilemap;
    public Tilemap GroundDecorationTilemap => groundDecorationTilemap;
    public Tilemap WallTilemap => wallTilemap;
    public Tilemap WallDetailTilemap => wallDetailTilemap;
    public Tilemap ForegroundTilemap => foregroundTilemap;
    public Tilemap OverlayFxTilemap => overlayFxTilemap;
    public RoomTemplateSO SourceTemplate => sourceTemplate;

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
    public void EditorAssignTilemaps(Grid targetGrid, Tilemap floor, Tilemap wall)
    {
        EditorAssignTilemaps(
            targetGrid,
            null,
            floor,
            null,
            null,
            wall,
            null,
            null,
            null);
    }

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

    public void EditorAssignSourceTemplate(RoomTemplateSO template)
    {
        sourceTemplate = template;
    }

    private void OnValidate()
    {
        size = new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));
        selectionWeight = Mathf.Max(0f, selectionWeight);
        difficultyTier = Mathf.Max(0, difficultyTier);
        topologyPlacement.minimumGraphDistanceFromStart = Mathf.Max(
            0,
            topologyPlacement.minimumGraphDistanceFromStart);
    }
#endif

    private void OnDrawGizmos()
    {
        if (grid == null)
            return;

        Vector3 bottomLeft = grid.CellToWorld(Vector3Int.zero);
        Vector3 bottomRight = grid.CellToWorld(new Vector3Int(size.x, 0, 0));
        Vector3 topRight = grid.CellToWorld(new Vector3Int(size.x, size.y, 0));
        Vector3 topLeft = grid.CellToWorld(new Vector3Int(0, size.y, 0));

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(bottomLeft, bottomRight);
        Gizmos.DrawLine(bottomRight, topRight);
        Gizmos.DrawLine(topRight, topLeft);
        Gizmos.DrawLine(topLeft, bottomLeft);
    }
}
