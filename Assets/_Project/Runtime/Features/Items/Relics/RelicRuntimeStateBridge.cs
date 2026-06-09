using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임 :
/// - 유물 슬롯별 런타임 상태 저장/복원 순서를 오케스트레이션한다.
/// - 실제 유물별 직렬화/복원 규칙은 각 RelicLogic의 선택적 serializer 구현에 위임한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class RelicRuntimeStateBridge : MonoBehaviour, IRelicRuntimeStateCapturer, IRelicRuntimeStateRestorer
{
    [Header("Refs")]
    [SerializeField] private RelicInventory relicInventory;
    [SerializeField] private RelicRuntimeStateHub runtimeStateHub;

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

        if (runtimeStateHub == null)
            runtimeStateHub = GetComponent<RelicRuntimeStateHub>();
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

        for (int slotIndex = 0; slotIndex < relicInventory.Capacity; slotIndex++)
        {
            var relic = relicInventory.GetRelicInSlot(slotIndex);
            if (relic == null)
                continue;

            if (!relicInventory.TryGetRuntimeContextForSlot(slotIndex, out var ctx))
                continue;

            if (TryBuildRuntimeState(ctx, slotIndex, relic, out var state))
                output.Add(state);
        }
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

        if (!relicInventory.TryGetRuntimeContextForSlot(state.slotIndex, out var ctx))
            return;

        if (currentRelic.logic is IRelicRuntimeStateSerializer serializer)
            serializer.RestoreRuntimeState(ctx, state, runtimeStateHub);
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

    /// <summary>
    /// 책임 : 현재 슬롯 유물이 저장이 필요한 유물이라면 해당 runtime payload를 생성한다.
    /// 지원하지 않는 유물은 false를 반환해 상위 루프가 조용히 건너뛰게 한다.
    /// </summary>
    private bool TryBuildRuntimeState(
        RelicContext ctx,
        int slotIndex,
        RelicDefinition relic,
        out RelicRuntimeState state)
    {
        state = null;

        if (relic == null || runtimeStateHub == null)
            return false;

        if (relic.logic is IRelicRuntimeStateSerializer serializer)
            return serializer.TryCaptureRuntimeState(ctx, runtimeStateHub, slotIndex, out state);

        return false;
    }
}
