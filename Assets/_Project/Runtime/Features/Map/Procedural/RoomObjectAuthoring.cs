using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임:
/// - 방 제작 씬에서 한 오브젝트 배치의 식별자, 종류, 원본 프리팹 또는 몬스터 역할 세트를 표시한다.
/// - 몬스터 스폰 지점의 공통 Warrior/Mage/Tank 역할 또는 스테이지 고정 프리팹과 연결 상자 Id를 보관한다.
/// - Transform을 RoomPieceAuthoring 또는 복도 장식 모듈 Grid 기준 셀/오프셋/회전/크기 데이터로 변환한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class RoomObjectAuthoring : MonoBehaviour
{
    [SerializeField] private string placementId = "Object_01";
    [SerializeField] private RoomObjectKind kind = RoomObjectKind.Prop;
    [SerializeField] private GameObject prefab;
    [SerializeField] private RoomMonsterSpawnRole monsterSpawnRole = RoomMonsterSpawnRole.Warrior;
    [SerializeField] private StageMonsterSetSO monsterStageSet;
    [SerializeField] private List<RoomObjectChildPoseOverrideData> childPoseOverrides = new();
    [SerializeField] private string linkedChestLockPlacementId;

    public string PlacementId => placementId;
    public RoomObjectKind Kind => kind;
    public GameObject Prefab => prefab;
    public RoomMonsterSpawnRole MonsterSpawnRole => monsterSpawnRole;
    public StageMonsterSetSO MonsterStageSet => kind == RoomObjectKind.Monster
        ? monsterStageSet
        : null;
    public bool UsesCommonMonsterRole => kind == RoomObjectKind.Monster && monsterStageSet != null;
    public IReadOnlyList<RoomObjectChildPoseOverrideData> ChildPoseOverrides => childPoseOverrides;
    public string LinkedChestLockPlacementId => kind == RoomObjectKind.Monster
        ? linkedChestLockPlacementId
        : string.Empty;

    public bool TryGetPlacementData(out RoomObjectPlacementData data)
    {
        data = default;
        if (!TryResolveAuthoringGrid(out Grid grid))
            return false;

        Vector3Int cell = grid.WorldToCell(transform.position);
        Vector3 cellCenter = grid.GetCellCenterWorld(cell);
        Vector3 localOffset = grid.transform.InverseTransformVector(transform.position - cellCenter);
        float localRotation = Mathf.DeltaAngle(
            grid.transform.eulerAngles.z,
            transform.eulerAngles.z);

#if UNITY_EDITOR
        EditorCaptureCompositePoseOverrides();
#endif

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
            childPoseOverrides = CloneChildPoseOverrides(childPoseOverrides),
            linkedChestLockPlacementId = LinkedChestLockPlacementId
        };
        return true;
    }

    public bool TryGetCompositePoseAuthoring(out RoomCompositePoseAuthoring composite)
    {
        composite = GetComponent<RoomCompositePoseAuthoring>();
        if (composite == null)
            composite = GetComponentInChildren<RoomCompositePoseAuthoring>(true);

        return composite != null;
    }

    /// <summary>
    /// 책임 : 동일한 오브젝트 마커가 방과 복도 장식 제작 문맥에서 각각 올바른 기준 Grid를 찾게 한다.
    /// </summary>
    private bool TryResolveAuthoringGrid(out Grid grid)
    {
        RoomPieceAuthoring room = GetComponentInParent<RoomPieceAuthoring>();
        if (room != null && room.Grid != null)
        {
            grid = room.Grid;
            return true;
        }

        CorridorDecorationModuleAuthoring corridor =
            GetComponentInParent<CorridorDecorationModuleAuthoring>();
        if (corridor != null && corridor.Grid != null)
        {
            grid = corridor.Grid;
            return true;
        }

        grid = null;
        return false;
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
        childPoseOverrides = CloneChildPoseOverrides(data.childPoseOverrides);

        if (!TryResolveAuthoringGrid(out Grid grid))
        {
            EditorApplyCompositePoseOverrides();
            return;
        }

        Vector3Int cell = new(data.localCell.x, data.localCell.y, 0);
        Vector3 worldOffset = grid.transform.TransformVector(
            new Vector3(data.localOffset.x, data.localOffset.y, 0f));
        transform.position = grid.GetCellCenterWorld(cell) + worldOffset;
        transform.rotation = grid.transform.rotation *
            Quaternion.Euler(0f, 0f, data.localRotationDegrees);
        transform.localScale = data.localScale == Vector3.zero ? Vector3.one : data.localScale;
        EditorApplyCompositePoseOverrides();
    }

    /// <summary>
    /// 책임:
    /// 방 제작 툴이 슬롯 하나의 선택적 위치·회전·크기 재정의를 현재 방 배치에 저장하고 미리보기에 반영하게 한다.
    /// </summary>
    public void EditorSetChildPoseOverride(RoomObjectChildPoseOverrideData poseOverride)
    {
        childPoseOverrides ??= new List<RoomObjectChildPoseOverrideData>();
        for (int i = 0; i < childPoseOverrides.Count; i++)
        {
            if (!string.Equals(
                    childPoseOverrides[i].slotId,
                    poseOverride.slotId,
                    System.StringComparison.Ordinal))
            {
                continue;
            }

            childPoseOverrides[i] = poseOverride;
            EditorApplyCompositePoseOverrides();
            return;
        }

        childPoseOverrides.Add(poseOverride);
        EditorApplyCompositePoseOverrides();
    }

    /// <summary>
    /// 책임:
    /// 방별 슬롯 재정의를 제거하고 작업 공간의 대상 Transform을 원본 복합 프리팹 자세로 복구한다.
    /// </summary>
    public void EditorRemoveChildPoseOverride(string slotId, bool restorePrefabPose)
    {
        if (childPoseOverrides != null)
        {
            childPoseOverrides.RemoveAll(entry => string.Equals(
                entry.slotId,
                slotId,
                System.StringComparison.Ordinal));
        }

        if (!restorePrefabPose ||
            !TryGetCompositePoseAuthoring(out RoomCompositePoseAuthoring instanceComposite) ||
            !instanceComposite.TryGetSlot(slotId, out RoomCompositePoseSlotData instanceSlot) ||
            instanceSlot.Target == null ||
            prefab == null)
        {
            return;
        }

        RoomCompositePoseAuthoring prefabComposite =
            prefab.GetComponentInChildren<RoomCompositePoseAuthoring>(true);
        if (prefabComposite == null ||
            !prefabComposite.TryGetSlot(slotId, out RoomCompositePoseSlotData prefabSlot) ||
            prefabSlot.Target == null)
        {
            return;
        }

        instanceSlot.Target.localPosition = prefabSlot.Target.localPosition;
        instanceSlot.Target.localRotation = prefabSlot.Target.localRotation;
        instanceSlot.Target.localScale = prefabSlot.Target.localScale;
    }

    public bool EditorTryGetChildPoseOverride(
        string slotId,
        out RoomObjectChildPoseOverrideData poseOverride)
    {
        poseOverride = default;
        if (childPoseOverrides == null)
            return false;

        for (int i = 0; i < childPoseOverrides.Count; i++)
        {
            RoomObjectChildPoseOverrideData candidate = childPoseOverrides[i];
            if (!string.Equals(candidate.slotId, slotId, System.StringComparison.Ordinal))
                continue;

            poseOverride = candidate;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 책임:
    /// Scene View에서 직접 움직인 활성 슬롯 Transform을 직렬화할 방별 재정의 값으로 동기화한다.
    /// </summary>
    public void EditorCaptureCompositePoseOverrides()
    {
        if (childPoseOverrides == null ||
            childPoseOverrides.Count == 0 ||
            !TryGetCompositePoseAuthoring(out RoomCompositePoseAuthoring composite))
        {
            return;
        }

        for (int i = 0; i < childPoseOverrides.Count; i++)
        {
            RoomObjectChildPoseOverrideData poseOverride = childPoseOverrides[i];
            if (!composite.TryGetSlot(poseOverride.slotId, out RoomCompositePoseSlotData slot) ||
                slot.Target == null)
            {
                continue;
            }

            if (poseOverride.overridePosition)
                poseOverride.localPosition = slot.Target.localPosition;
            if (poseOverride.overrideRotation)
            {
                poseOverride.localRotationDegrees = Mathf.DeltaAngle(
                    0f,
                    slot.Target.localEulerAngles.z);
            }
            if (poseOverride.overrideScale)
                poseOverride.localScale = slot.Target.localScale;
            childPoseOverrides[i] = poseOverride;
        }

        EditorApplyCompositePoseOverrides();
    }

    private void EditorApplyCompositePoseOverrides()
    {
        if (childPoseOverrides == null || childPoseOverrides.Count == 0)
            return;

        if (!TryGetCompositePoseAuthoring(out RoomCompositePoseAuthoring composite))
            return;

        RoomCompositePoseAuthoring prefabComposite = prefab != null
            ? prefab.GetComponentInChildren<RoomCompositePoseAuthoring>(true)
            : null;
        if (prefabComposite != null)
        {
            for (int i = 0; i < childPoseOverrides.Count; i++)
            {
                string slotId = childPoseOverrides[i].slotId;
                if (!composite.TryGetSlot(slotId, out RoomCompositePoseSlotData instanceSlot) ||
                    !prefabComposite.TryGetSlot(slotId, out RoomCompositePoseSlotData prefabSlot) ||
                    instanceSlot.Target == null ||
                    prefabSlot.Target == null)
                {
                    continue;
                }

                instanceSlot.Target.localPosition = prefabSlot.Target.localPosition;
                instanceSlot.Target.localRotation = prefabSlot.Target.localRotation;
                instanceSlot.Target.localScale = prefabSlot.Target.localScale;
            }
        }

        if (!composite.TryApplyPoseOverrides(childPoseOverrides, out string failureReason))
            Debug.LogError($"{name}: {failureReason}", this);
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

        childPoseOverrides ??= new List<RoomObjectChildPoseOverrideData>();

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

    private static List<RoomObjectChildPoseOverrideData> CloneChildPoseOverrides(
        IReadOnlyList<RoomObjectChildPoseOverrideData> source)
    {
        if (source == null || source.Count == 0)
            return new List<RoomObjectChildPoseOverrideData>();

        var clone = new List<RoomObjectChildPoseOverrideData>(source.Count);
        for (int i = 0; i < source.Count; i++)
            clone.Add(source[i]);
        return clone;
    }
}
