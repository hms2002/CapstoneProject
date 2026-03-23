using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 : 현재 플레이어 객체의 장비 배치, GAS 런타임 상태,
/// 장비 개별 런타임 상태를 PlayerRuntimeState로 캡처하는 상위 오케스트레이터다.
/// </summary>
public static class PlayerRuntimeCaptureCoordinator
{
    /// <summary>
    /// 책임 : 현재 플레이어의 상태를 PlayerRuntimeState로 캡처한다.
    /// </summary>
    public static PlayerRuntimeState CaptureAll(
        WeaponInventory2D weaponInventory,
        RelicInventory relicInventory,
        AttributeSet attributeSet,
        GameplayEffectRunner effectRunner,
        TagSystem tagSystem,
        AbilitySystem abilitySystem,
        IWeaponRuntimeStateCapturer weaponRuntimeCapturer = null,
        IRelicRuntimeStateCapturer relicRuntimeCapturer = null)
    {
        var state = new PlayerRuntimeState();

        // 1) 장비 배치 상태
        if (weaponInventory != null)
            state.weaponInventory = weaponInventory.CaptureInventoryState();

        if (relicInventory != null)
            state.relicInventory = relicInventory.CaptureInventoryState();

        // 2) GAS 런타임 상태
        CaptureAttributes(state.attributes, attributeSet);
        CaptureExplicitTags(state.explicitTags, tagSystem);
        CaptureEffects(state.activeEffects, effectRunner);
        CaptureAbilities(state.abilities, abilitySystem);

        // 3) 장비 개별 런타임 상태
        weaponRuntimeCapturer?.CaptureWeaponRuntimeStates(weaponInventory, state.weaponRuntimeStates);
        relicRuntimeCapturer?.CaptureRelicRuntimeStates(relicInventory, state.relicRuntimeStates);

        return state;
    }

    /// <summary>
    /// 책임 : 현재 AttributeSet의 definition/current 값을 저장용 스냅샷으로 캡처한다.
    /// </summary>
    private static void CaptureAttributes(
        List<AttributeRuntimeSnapshot> output,
        AttributeSet attributeSet)
    {
        if (output == null || attributeSet == null)
            return;

        foreach (var def in attributeSet.EnumerateDefinitions())
        {
            if (def == null)
                continue;

            output.Add(new AttributeRuntimeSnapshot
            {
                attributeId = def.name,
                baseValue = attributeSet.GetBaseValue(def),
                currentValue = attributeSet.GetCurrentValue(def)
            });
        }
    }

    /// <summary>
    /// 책임 : 현재 explicit tag 상태를 저장용 스냅샷으로 캡처한다.
    /// </summary>
    private static void CaptureExplicitTags(
        List<ExplicitTagSnapshot> output,
        TagSystem tagSystem)
    {
        if (output == null || tagSystem == null)
            return;

        foreach (var entry in tagSystem.EnumerateExplicitTagsWithCount())
        {
            if (entry == null)
                continue;

            if (string.IsNullOrEmpty(entry.tagName))
                continue;

            if (entry.count <= 0)
                continue;

            output.Add(new ExplicitTagSnapshot
            {
                tagName = entry.tagName,
                count = entry.count
            });
        }
    }

    /// <summary>
    /// 책임 : 현재 활성 effect 상태를 저장용 스냅샷으로 캡처한다.
    /// 실제 effect 식별자 채우기는 프로젝트 Runner 구현에 맞게 확장한다.
    /// </summary>
    private static void CaptureEffects(
        List<ActiveGameplayEffectSnapshot> output,
        GameplayEffectRunner effectRunner)
    {
        if (output == null || effectRunner == null)
            return;

        var snapshots = effectRunner.CaptureActiveEffectSnapshots();
        if (snapshots == null)
            return;

        for (int i = 0; i < snapshots.Count; i++)
        {
            var entry = snapshots[i];
            if (entry == null)
                continue;

            output.Add(new ActiveGameplayEffectSnapshot
            {
                effectId = entry.effectId,
                remainingTime = entry.remainingTime,
                stackCount = entry.stackCount
            });
        }
    }

    /// <summary>
    /// 책임 : 현재 ability 런타임 상태를 저장용 스냅샷으로 캡처한다.
    /// 주 용도는 cooldown 유지다.
    /// </summary>
    private static void CaptureAbilities(
       List<AbilityRuntimeSnapshot> output,
       AbilitySystem abilitySystem)
    {
        if (output == null || abilitySystem == null)
            return;

        var snapshots = abilitySystem.CaptureAbilitySnapshots();
        if (snapshots == null)
            return;

        for (int i = 0; i < snapshots.Count; i++)
        {
            var entry = snapshots[i];
            if (entry == null)
                continue;

            output.Add(new AbilityRuntimeSnapshot
            {
                abilityId = entry.abilityId,
                cooldownRemaining = entry.cooldownRemaining,
                chargesRemaining = entry.chargesRemaining
            });
        }
    }
}