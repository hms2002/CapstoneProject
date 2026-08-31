using UnityEngine;

/// <summary>
/// 책임:
/// - 방 조각 제작 중 2칸 연결 소켓의 식별자, 시작 셀, 바깥 방향을 보관한다.
/// - 자신의 Transform 위치를 RoomPieceAuthoring Grid의 시작 셀 좌표로 변환해 bake 입력으로 제공한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class RoomSocketAuthoring : MonoBehaviour
{
    [SerializeField] private string socketId = "Socket_01";
    [SerializeField] private RoomSocketDirection direction = RoomSocketDirection.Up;

    public string SocketId => socketId;
    public RoomSocketDirection Direction => direction;
    public int Width => RoomSocketGeometry.RequiredWidth;

    public bool TryGetLocalCell(out Vector2Int localCell)
    {
        RoomPieceAuthoring room = GetComponentInParent<RoomPieceAuthoring>();
        if (room == null || room.Grid == null)
        {
            localCell = default;
            return false;
        }

        Vector3Int cell = room.Grid.WorldToCell(transform.position);
        localCell = new Vector2Int(cell.x, cell.y);
        return true;
    }

#if UNITY_EDITOR
    public void EditorSetLocalCell(Vector2Int localCell)
    {
        RoomPieceAuthoring room = GetComponentInParent<RoomPieceAuthoring>();
        if (room == null || room.Grid == null)
            return;

        transform.position = room.Grid.GetCellCenterWorld(new Vector3Int(localCell.x, localCell.y, 0));
    }

    private void OnValidate()
    {
        if (socketId == null)
            socketId = string.Empty;
    }
#endif

    private void OnDrawGizmos()
    {
        RoomPieceAuthoring room = GetComponentInParent<RoomPieceAuthoring>();
        Vector3 origin = transform.position;
        Vector3 spanCenter = origin;

        if (room != null && room.Grid != null && TryGetLocalCell(out Vector2Int localCell))
        {
            RoomSocketData socket = new()
            {
                localCell = localCell,
                direction = direction,
                width = Width
            };

            Vector2Int firstCell = RoomSocketGeometry.GetLocalCell(socket, 0);
            Vector3 firstCenter = room.Grid.GetCellCenterWorld(
                new Vector3Int(firstCell.x, firstCell.y, 0));
            Vector2Int lastCell = RoomSocketGeometry.GetLocalCell(socket, Width - 1);
            Vector3 lastCenter = room.Grid.GetCellCenterWorld(
                new Vector3Int(lastCell.x, lastCell.y, 0));
            spanCenter = Vector3.Lerp(firstCenter, lastCenter, 0.5f);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(firstCenter, Vector3.one * 0.35f);
            Gizmos.DrawWireCube(lastCenter, Vector3.one * 0.35f);
            Gizmos.DrawLine(firstCenter, lastCenter);
        }

        Vector3 directionVector = room != null && room.Grid != null
            ? room.Grid.transform.TransformDirection(DirectionToVector(direction)).normalized
            : DirectionToVector(direction);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(spanCenter, spanCenter + directionVector * 0.75f);
        Gizmos.DrawSphere(spanCenter + directionVector * 0.75f, 0.08f);
    }

    private static Vector3 DirectionToVector(RoomSocketDirection socketDirection)
    {
        return socketDirection switch
        {
            RoomSocketDirection.Up => Vector3.up,
            RoomSocketDirection.Right => Vector3.right,
            RoomSocketDirection.Down => Vector3.down,
            RoomSocketDirection.Left => Vector3.left,
            _ => Vector3.zero
        };
    }
}
