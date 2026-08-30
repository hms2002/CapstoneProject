using UnityEngine;

/// <summary>
/// 책임 : 방 제작 작업 공간에서 이동 endpoint 슬롯의 Id·매개체 종류·선택 프리팹과 Grid 기준 출발·도착 배치를 편집 데이터로 제공한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class RoomTravelEndpointAuthoring : MonoBehaviour
{
    [SerializeField] private string slotId = "Travel_01";
    [SerializeField] private RoomTravelEndpointKind kind = RoomTravelEndpointKind.Interaction;
    [SerializeField] private GameObject mediumPrefab;
    [SerializeField] private Vector2 triggerSize = Vector2.one;
    [SerializeField] private bool useSeparateArrivalPoint;
    [SerializeField] private Vector2Int arrivalLocalCell;
    [SerializeField] private Vector2 arrivalLocalOffset;

    public string SlotId => slotId;
    public RoomTravelEndpointKind Kind => kind;
    public GameObject MediumPrefab => mediumPrefab;
    public Vector2 TriggerSize => RoomTravelEndpointGeometry.SanitizeTriggerSize(triggerSize);
    public bool UseSeparateArrivalPoint => useSeparateArrivalPoint;

    public bool TryGetPlacementData(out RoomTravelEndpointPlacementData data)
    {
        data = default;
        RoomPieceAuthoring room = GetComponentInParent<RoomPieceAuthoring>();
        if (room == null || room.Grid == null)
            return false;

        Vector3Int cell = room.Grid.WorldToCell(transform.position);
        Vector3 cellCenter = room.Grid.GetCellCenterWorld(cell);
        Vector3 localOffset = room.Grid.transform.InverseTransformVector(transform.position - cellCenter);
        float localRotation = Mathf.DeltaAngle(
            room.Grid.transform.eulerAngles.z,
            transform.eulerAngles.z);

        data = new RoomTravelEndpointPlacementData
        {
            slotId = slotId,
            kind = kind,
            mediumPrefab = mediumPrefab,
            localCell = new Vector2Int(cell.x, cell.y),
            localOffset = new Vector2(localOffset.x, localOffset.y),
            localRotationDegrees = localRotation,
            localScale = transform.localScale,
            triggerSize = TriggerSize,
            useSeparateArrivalPoint = useSeparateArrivalPoint,
            arrivalLocalCell = arrivalLocalCell,
            arrivalLocalOffset = arrivalLocalOffset
        };
        return true;
    }

    /// <summary>
    /// 책임 : 별도 도착 좌표를 현재 방 Grid 기준 월드 위치로 변환해 제작 핸들과 Gizmo가 같은 규칙을 사용하게 한다.
    /// </summary>
    public bool TryGetArrivalWorldPosition(out Vector3 worldPosition)
    {
        worldPosition = transform.position;
        if (!useSeparateArrivalPoint)
            return false;

        RoomPieceAuthoring room = GetComponentInParent<RoomPieceAuthoring>();
        if (room == null || room.Grid == null)
            return false;

        Vector3Int cell = new(arrivalLocalCell.x, arrivalLocalCell.y, 0);
        Vector3 worldOffset = room.Grid.transform.TransformVector(
            new Vector3(arrivalLocalOffset.x, arrivalLocalOffset.y, 0f));
        worldPosition = room.Grid.GetCellCenterWorld(cell) + worldOffset;
        return true;
    }

#if UNITY_EDITOR
    public void EditorConfigure(
        string id,
        RoomTravelEndpointKind endpointKind,
        GameObject sourcePrefab)
    {
        slotId = id ?? string.Empty;
        kind = endpointKind;
        mediumPrefab = sourcePrefab;
    }

    public void EditorSetPlacement(RoomTravelEndpointPlacementData data)
    {
        RoomPieceAuthoring room = GetComponentInParent<RoomPieceAuthoring>();
        if (room == null || room.Grid == null)
            return;

        Vector3Int cell = new(data.localCell.x, data.localCell.y, 0);
        Vector3 worldOffset = room.Grid.transform.TransformVector(
            new Vector3(data.localOffset.x, data.localOffset.y, 0f));
        transform.position = room.Grid.GetCellCenterWorld(cell) + worldOffset;
        transform.rotation = room.Grid.transform.rotation *
                             Quaternion.Euler(0f, 0f, data.localRotationDegrees);
        transform.localScale = data.localScale == Vector3.zero ? Vector3.one : data.localScale;
        triggerSize = RoomTravelEndpointGeometry.ResolveTriggerSize(data);
        useSeparateArrivalPoint = data.useSeparateArrivalPoint;
        arrivalLocalCell = data.arrivalLocalCell;
        arrivalLocalOffset = data.arrivalLocalOffset;
    }

    /// <summary>
    /// 책임 : 기획자가 별도 도착점을 켤 때 현재 endpoint 위치를 안전한 초기값으로 복사하고 기존 슬롯 동작을 명시적으로 보존한다.
    /// </summary>
    public void EditorSetUseSeparateArrivalPoint(bool value)
    {
        if (value && !useSeparateArrivalPoint)
            EditorSetArrivalWorldPosition(transform.position);

        useSeparateArrivalPoint = value;
    }

    /// <summary>
    /// 책임 : 도착점 숫자 입력을 방 Grid 셀과 셀 중심 오프셋 데이터에 반영한다.
    /// </summary>
    public void EditorSetArrivalPlacement(Vector2Int localCell, Vector2 localOffset)
    {
        arrivalLocalCell = localCell;
        arrivalLocalOffset = localOffset;
    }

    /// <summary>
    /// 책임 : 기획자가 입력한 Trigger 판정 크기를 매개체 Transform Scale과 독립된 값으로 저장한다.
    /// </summary>
    public void EditorSetTriggerSize(Vector2 size)
    {
        triggerSize = RoomTravelEndpointGeometry.SanitizeTriggerSize(size);
    }

    /// <summary>
    /// 책임 : Scene View 도착 핸들의 월드 위치를 방 Grid 기준 셀과 셀 중심 오프셋으로 변환한다.
    /// </summary>
    public void EditorSetArrivalWorldPosition(Vector3 worldPosition)
    {
        RoomPieceAuthoring room = GetComponentInParent<RoomPieceAuthoring>();
        if (room == null || room.Grid == null)
            return;

        Vector3Int cell = room.Grid.WorldToCell(worldPosition);
        Vector3 cellCenter = room.Grid.GetCellCenterWorld(cell);
        Vector3 localOffset = room.Grid.transform.InverseTransformVector(worldPosition - cellCenter);
        arrivalLocalCell = new Vector2Int(cell.x, cell.y);
        arrivalLocalOffset = new Vector2(localOffset.x, localOffset.y);
    }

    private void OnValidate()
    {
        slotId ??= string.Empty;
        if (!RoomTravelEndpointGeometry.HasExplicitTriggerSize(triggerSize))
        {
            triggerSize = RoomTravelEndpointGeometry.SanitizeTriggerSize(new Vector2(
                transform.localScale.x,
                transform.localScale.y));
        }
        else
        {
            triggerSize = RoomTravelEndpointGeometry.SanitizeTriggerSize(triggerSize);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = kind switch
        {
            RoomTravelEndpointKind.Interaction => Color.cyan,
            RoomTravelEndpointKind.Trigger => new Color(0.2f, 0.8f, 1f),
            _ => Color.green
        };
        Matrix4x4 previousMatrix = Gizmos.matrix;
        if (kind == RoomTravelEndpointKind.Trigger)
        {
            Gizmos.matrix = Matrix4x4.TRS(
                transform.position,
                transform.rotation,
                Vector3.one);
            Vector2 size = TriggerSize;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(size.x, size.y, 0.01f));
        }
        else
        {
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.8f);
        }
        Gizmos.matrix = previousMatrix;

        if (!TryGetArrivalWorldPosition(out Vector3 arrivalPosition))
            return;

        Gizmos.color = new Color(0.25f, 1f, 0.45f, 1f);
        Gizmos.DrawLine(transform.position, arrivalPosition);
        Gizmos.DrawWireSphere(arrivalPosition, 0.3f);
    }
#endif
}
