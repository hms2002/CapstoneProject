using UnityEngine;

/// <summary>
/// 책임:
/// - 방 제작 씬에서 한 프리팹 배치의 식별자, 종류, 원본 프리팹과 몬스터의 연결 상자 Id를 표시한다.
/// - Transform을 RoomPieceAuthoring Grid 기준 셀/오프셋/회전/크기 데이터로 변환한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class RoomObjectAuthoring : MonoBehaviour
{
    [SerializeField] private string placementId = "Object_01";
    [SerializeField] private RoomObjectKind kind = RoomObjectKind.Prop;
    [SerializeField] private GameObject prefab;
    [SerializeField] private string linkedChestLockPlacementId;

    public string PlacementId => placementId;
    public RoomObjectKind Kind => kind;
    public GameObject Prefab => prefab;
    public string LinkedChestLockPlacementId => kind == RoomObjectKind.Monster
        ? linkedChestLockPlacementId
        : string.Empty;

    public bool TryGetPlacementData(out RoomObjectPlacementData data)
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

        data = new RoomObjectPlacementData
        {
            placementId = placementId,
            kind = kind,
            prefab = prefab,
            localCell = new Vector2Int(cell.x, cell.y),
            localOffset = new Vector2(localOffset.x, localOffset.y),
            localRotationDegrees = localRotation,
            localScale = transform.localScale,
            linkedChestLockPlacementId = LinkedChestLockPlacementId
        };
        return true;
    }

#if UNITY_EDITOR
    public void EditorConfigure(string id, RoomObjectKind objectKind, GameObject sourcePrefab)
    {
        placementId = id ?? string.Empty;
        kind = objectKind;
        prefab = sourcePrefab;
    }

    public void EditorSetPlacement(RoomObjectPlacementData data)
    {
        linkedChestLockPlacementId = data.kind == RoomObjectKind.Monster
            ? data.linkedChestLockPlacementId ?? string.Empty
            : string.Empty;

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

    public void EditorSetLinkedChestLockPlacementId(string targetPlacementId)
    {
        linkedChestLockPlacementId = kind == RoomObjectKind.Monster
            ? targetPlacementId ?? string.Empty
            : string.Empty;
    }

    private void OnValidate()
    {
        if (placementId == null)
            placementId = string.Empty;

        if (linkedChestLockPlacementId == null || kind != RoomObjectKind.Monster)
            linkedChestLockPlacementId = string.Empty;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = kind switch
        {
            RoomObjectKind.Monster => Color.red,
            RoomObjectKind.Chest => Color.yellow,
            RoomObjectKind.Portal => Color.magenta,
            _ => Color.white
        };
        Gizmos.DrawWireSphere(transform.position, 0.3f);
    }
#endif
}
