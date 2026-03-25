using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임 : 유물별 개별 런타임 상태의 캡처/복원 진입점을 제공한다.
/// 현재는 구조 정리용 기본 구현을 담당하며,
/// 실제 저장이 필요한 유물 타입이 생기면 이 클래스에 상태 분기를 추가한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class RelicRuntimeStateBridge : MonoBehaviour, IRelicRuntimeStateCapturer, IRelicRuntimeStateRestorer
{
    [Header("Refs")]
    [SerializeField] private RelicInventory relicInventory;

    private void Awake()
    {
        CacheComponents();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheComponents();
    }
#endif

    /// <summary>
    /// 책임 : 같은 오브젝트에 붙은 핵심 참조를 자동 캐싱한다.
    /// </summary>
    private void CacheComponents()
    {
        if (relicInventory == null)
            relicInventory = GetComponent<RelicInventory>();
    }

    /// <summary>
    /// 책임 : 현재 장착된 유물 중 개별 런타임 상태 저장이 필요한 유물만 output에 기록한다.
    /// 아직 프로젝트에 공통 저장 규약이 확정되지 않았으므로 기본 구현은 no-op이다.
    /// 이후 lightning 내부 쿨타임, proc 버프 남은 시간 등을 여기서 추가 저장하면 된다.
    /// </summary>
    public void CaptureRelicRuntimeStates(
        RelicInventory relicInventory,
        List<RelicRuntimeState> output)
    {
        if (relicInventory == null || output == null)
            return;

        // 현재는 저장이 필요한 유물별 런타임 payload가 아직 확정되지 않았으므로 비워 둔다.
        // 추후 필요한 유물만 아래 구조로 추가:
        //
        // for (int slotIndex = 0; slotIndex < relicInventory.Capacity; slotIndex++)
        // {
        //     var relic = relicInventory.GetRelicInSlot(slotIndex);
        //     if (relic == null) continue;
        //
        //     if (TryBuildRuntimeState(relicInventory, slotIndex, relic, out var state))
        //         output.Add(state);
        // }
    }

    /// <summary>
    /// 책임 : 저장된 유물 런타임 상태 하나를 현재 장착 상태 위에 복원한다.
    /// 현재는 구조 정리 단계이므로 미지원 stateType은 조용히 무시한다.
    /// </summary>
    public void RestoreRelicRuntimeState(
        RelicInventory relicInventory,
        RelicRuntimeState state,
        IPlayerRuntimeResolver resolver)
    {
        if (relicInventory == null || state == null || resolver == null)
            return;

        if (!TryValidateSlotState(relicInventory, state, out var currentRelic))
            return;

        if (string.IsNullOrWhiteSpace(state.stateType))
            return;

        // 현재는 아직 지원하는 유물별 stateType이 없으므로 no-op.
        // 추후 state.stateType 분기 추가:
        //
        // switch (state.stateType)
        // {
        //     case LightningCooldownState.StateTypeKey:
        //         RestoreLightningCooldown(...);
        //         break;
        // }
    }

    /// <summary>
    /// 책임 : 저장된 런타임 상태가 현재 슬롯/유물과 대응되는지 검증한다.
    /// shell restore 이후 잘못된 유물에 상태가 주입되는 것을 막는다.
    /// </summary>
    private static bool TryValidateSlotState(
        RelicInventory relicInventory,
        RelicRuntimeState state,
        out RelicDefinition currentRelic)
    {
        currentRelic = null;

        if (relicInventory == null || state == null)
            return false;

        currentRelic = relicInventory.GetRelicInSlot(state.slotIndex);
        if (currentRelic == null)
        {
            Debug.LogWarning(
                $"[RelicRuntimeStateBridge] 슬롯 {state.slotIndex}에 유물이 없어 runtime 복원을 건너뜁니다.");
            return false;
        }

        if (!string.IsNullOrEmpty(state.relicId) &&
            !string.Equals(currentRelic.relicId, state.relicId, StringComparison.Ordinal))
        {
            Debug.LogWarning(
                $"[RelicRuntimeStateBridge] relicId 불일치로 runtime 복원을 건너뜁니다. saved={state.relicId}, current={currentRelic.relicId}");
            return false;
        }

        return true;
    }
}