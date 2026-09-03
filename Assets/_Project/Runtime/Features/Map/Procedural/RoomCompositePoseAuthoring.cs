using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임:
/// - 복합 방 오브젝트 프리팹에서 방별로 자세히 조정할 자식 Transform 슬롯을 공개한다.
/// - 런타임 생성기가 프리팹 기본 자세를 유지하면서 선택된 슬롯 재정의만 적용하게 한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class RoomCompositePoseAuthoring : MonoBehaviour
{
    [SerializeField] private List<RoomCompositePoseSlotData> poseSlots = new();

    public IReadOnlyList<RoomCompositePoseSlotData> PoseSlots => poseSlots;

    public bool TryGetSlot(string slotId, out RoomCompositePoseSlotData slot)
    {
        slot = default;
        if (string.IsNullOrWhiteSpace(slotId) || poseSlots == null)
            return false;

        for (int i = 0; i < poseSlots.Count; i++)
        {
            RoomCompositePoseSlotData candidate = poseSlots[i];
            if (!string.Equals(candidate.SlotId, slotId, StringComparison.Ordinal))
                continue;

            slot = candidate;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 책임:
    /// 저장된 방별 재정의를 슬롯 Id로 찾아 허용된 Transform 채널에만 적용한다.
    /// </summary>
    public bool TryApplyPoseOverrides(
        IReadOnlyList<RoomObjectChildPoseOverrideData> overrides,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (overrides == null || overrides.Count == 0)
            return true;

        if (!TryValidateSlots(out failureReason))
            return false;

        var overrideIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < overrides.Count; i++)
        {
            RoomObjectChildPoseOverrideData poseOverride = overrides[i];
            if (string.IsNullOrWhiteSpace(poseOverride.slotId) ||
                !overrideIds.Add(poseOverride.slotId))
            {
                failureReason = "Pose overrides contain an empty or duplicate slot Id.";
                return false;
            }

            if (!TryGetSlot(poseOverride.slotId, out RoomCompositePoseSlotData slot))
            {
                failureReason = $"Pose override slot '{poseOverride.slotId}' does not exist in '{name}'.";
                return false;
            }

            if ((poseOverride.overridePosition && !slot.AllowPosition) ||
                (poseOverride.overrideRotation && !slot.AllowRotation) ||
                (poseOverride.overrideScale && !slot.AllowScale))
            {
                failureReason = $"Pose override slot '{slot.SlotId}' uses a disallowed Transform channel.";
                return false;
            }

            if (poseOverride.overrideScale &&
                (Mathf.Approximately(poseOverride.localScale.x, 0f) ||
                 Mathf.Approximately(poseOverride.localScale.y, 0f) ||
                 Mathf.Approximately(poseOverride.localScale.z, 0f)))
            {
                failureReason = $"Pose override slot '{slot.SlotId}' has a zero Local Scale axis.";
                return false;
            }
        }

        for (int i = 0; i < overrides.Count; i++)
        {
            RoomObjectChildPoseOverrideData poseOverride = overrides[i];
            TryGetSlot(poseOverride.slotId, out RoomCompositePoseSlotData slot);
            Transform target = slot.Target;
            if (poseOverride.overridePosition && slot.AllowPosition)
                target.localPosition = poseOverride.localPosition;
            if (poseOverride.overrideRotation && slot.AllowRotation)
                target.localRotation = Quaternion.Euler(0f, 0f, poseOverride.localRotationDegrees);
            if (poseOverride.overrideScale && slot.AllowScale)
                target.localScale = poseOverride.localScale;
        }

        return true;
    }

    /// <summary>
    /// 책임:
    /// 복합 프리팹의 슬롯 Id 중복과 대상 Transform 누락을 저장 및 런타임 적용 전에 검증한다.
    /// </summary>
    public bool TryValidateSlots(out string failureReason)
    {
        failureReason = string.Empty;
        if (poseSlots == null || poseSlots.Count == 0)
        {
            failureReason = $"Composite object '{name}' has no pose slots.";
            return false;
        }

        var slotIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < poseSlots.Count; i++)
        {
            RoomCompositePoseSlotData slot = poseSlots[i];
            if (string.IsNullOrWhiteSpace(slot.SlotId))
            {
                failureReason = $"Composite object '{name}' has an empty pose slot Id.";
                return false;
            }

            if (!slotIds.Add(slot.SlotId))
            {
                failureReason = $"Composite object '{name}' has duplicate pose slot Id '{slot.SlotId}'.";
                return false;
            }

            if (slot.Target == null)
            {
                failureReason = $"Pose slot '{slot.SlotId}' in '{name}' has no target Transform.";
                return false;
            }
        }

        return true;
    }

#if UNITY_EDITOR
    /// <summary>
    /// 책임:
    /// 설치기와 제작 도구가 복합 프리팹의 슬롯 계약을 명시적으로 저장하게 한다.
    /// </summary>
    public void EditorSetPoseSlots(IReadOnlyList<RoomCompositePoseSlotData> slots)
    {
        poseSlots = slots != null
            ? new List<RoomCompositePoseSlotData>(slots)
            : new List<RoomCompositePoseSlotData>();
    }
#endif

    private void OnValidate()
    {
        if (poseSlots == null)
            poseSlots = new List<RoomCompositePoseSlotData>();
    }
}

/// <summary>
/// 책임:
/// - 복합 프리팹의 편집 가능한 자식 Transform과 안정 Id, 허용할 자세 채널을 한 항목으로 제공한다.
/// </summary>
[Serializable]
public struct RoomCompositePoseSlotData
{
    [SerializeField] private string slotId;
    [SerializeField] private string displayName;
    [SerializeField] private Transform target;
    [SerializeField] private bool allowPosition;
    [SerializeField] private bool allowRotation;
    [SerializeField] private bool allowScale;

    public RoomCompositePoseSlotData(
        string id,
        string label,
        Transform targetTransform,
        bool canMove = true,
        bool canRotate = true,
        bool canScale = true)
    {
        slotId = id ?? string.Empty;
        displayName = label ?? string.Empty;
        target = targetTransform;
        allowPosition = canMove;
        allowRotation = canRotate;
        allowScale = canScale;
    }

    public string SlotId => slotId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? slotId : displayName;
    public Transform Target => target;
    public bool AllowPosition => allowPosition;
    public bool AllowRotation => allowRotation;
    public bool AllowScale => allowScale;
}
