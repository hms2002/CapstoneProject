using UnityEngine;

/// <summary>
/// 책임:
/// - 방 제작 씬에서 한 오브젝트 배치의 식별자, 종류, 원본 프리팹 또는 몬스터 역할 세트를 표시한다.
/// - 몬스터 스폰 지점의 공통 Warrior/Mage/Tank 역할 또는 스테이지 고정 프리팹과 연결 상자 Id를 보관한다.
/// - Transform을 RoomPieceAuthoring Grid 기준 셀/오프셋/회전/크기 데이터로 변환한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class RoomObjectAuthoring : MonoBehaviour
{
    [SerializeField] private string placementId = "Object_01";
    [SerializeField] private RoomObjectKind kind = RoomObjectKind.Prop;
    [SerializeField] private GameObject prefab;
    [SerializeField] private RoomMonsterSpawnRole monsterSpawnRole = RoomMonsterSpawnRole.Warrior;
    [SerializeField] private StageMonsterSetSO monsterStageSet;
    [SerializeField] private string linkedChestLockPlacementId;

    public string PlacementId => placementId;
    public RoomObjectKind Kind => kind;
    public GameObject Prefab => prefab;
    public RoomMonsterSpawnRole MonsterSpawnRole => monsterSpawnRole;
    public StageMonsterSetSO MonsterStageSet => kind == RoomObjectKind.Monster
        ? monsterStageSet
        : null;
    public bool UsesCommonMonsterRole => kind == RoomObjectKind.Monster && monsterStageSet != null;
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
            monsterSpawnRole = monsterSpawnRole,
            monsterStageSet = MonsterStageSet,
            localCell = new Vector2Int(cell.x, cell.y),
            localOffset = new Vector2(localOffset.x, localOffset.y),
            localRotationDegrees = localRotation,
            localScale = transform.localScale,
            linkedChestLockPlacementId = LinkedChestLockPlacementId
        };
        return true;
    }

#if UNITY_EDITOR
    public void EditorConfigure(
        string id,
        RoomObjectKind objectKind,
        GameObject sourcePrefab,
        RoomMonsterSpawnRole spawnRole,
        StageMonsterSetSO stageMonsterSet)
    {
        placementId = id ?? string.Empty;
        kind = objectKind;
        prefab = sourcePrefab;
        monsterSpawnRole = spawnRole;
        monsterStageSet = objectKind == RoomObjectKind.Monster ? stageMonsterSet : null;
    }

    public void EditorSetPlacement(RoomObjectPlacementData data)
    {
        monsterSpawnRole = data.monsterSpawnRole;
        monsterStageSet = data.kind == RoomObjectKind.Monster
            ? data.monsterStageSet
            : null;
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

        if (kind != RoomObjectKind.Monster)
            monsterStageSet = null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = kind switch
        {
            RoomObjectKind.Monster when !UsesCommonMonsterRole => new Color(0.75f, 0.35f, 1f),
            RoomObjectKind.Monster => ResolveCommonMonsterRoleColor(monsterSpawnRole),
            RoomObjectKind.Chest => Color.yellow,
            RoomObjectKind.Portal => Color.magenta,
            _ => Color.white
        };
        Gizmos.DrawWireSphere(transform.position, 0.3f);
        if (kind == RoomObjectKind.Monster)
        {
            string label = UsesCommonMonsterRole
                ? monsterSpawnRole.ToString()
                : prefab != null
                    ? $"Stage: {prefab.name}"
                    : "Stage Monster";
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.4f, label);
        }
    }

    /// <summary>
    /// 책임:
    /// - Scene View에서 공통 몬스터 역할을 빠르게 구분할 일관된 표식 색상을 제공한다.
    /// </summary>
    private static Color ResolveCommonMonsterRoleColor(RoomMonsterSpawnRole role)
    {
        return role switch
        {
            RoomMonsterSpawnRole.Warrior => new Color(0.95f, 0.3f, 0.2f),
            RoomMonsterSpawnRole.Mage => new Color(0.25f, 0.55f, 1f),
            RoomMonsterSpawnRole.Tank => new Color(0.8f, 0.7f, 0.15f),
            _ => Color.red
        };
    }
#endif
}
