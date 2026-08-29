using UnityEngine;

/// <summary>
/// 책임 : 방 제작 작업 공간에서 이동 endpoint 슬롯의 Id·매개체 종류·선택 프리팹과 Grid 기준 배치를 편집 데이터로 제공한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class RoomTravelEndpointAuthoring : MonoBehaviour
{
    [SerializeField] private string slotId = "Travel_01";
    [SerializeField] private RoomTravelEndpointKind kind = RoomTravelEndpointKind.Interaction;
    [SerializeField] private GameObject mediumPrefab;

    public string SlotId => slotId;
    public RoomTravelEndpointKind Kind => kind;
    public GameObject MediumPrefab => mediumPrefab;

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
            localScale = transform.localScale
        };
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
    }

    private void OnValidate()
    {
        slotId ??= string.Empty;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = kind switch
        {
            RoomTravelEndpointKind.Interaction => Color.cyan,
            RoomTravelEndpointKind.Trigger => new Color(0.2f, 0.8f, 1f),
            _ => Color.green
        };
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.8f);
    }
#endif
}
