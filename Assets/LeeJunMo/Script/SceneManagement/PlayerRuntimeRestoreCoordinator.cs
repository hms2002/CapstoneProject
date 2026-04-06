using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 : 저장된 PlayerRuntimeState를 실제 플레이어 객체에 복원하는 상위 오케스트레이터다.
/// 장비 껍데기 복원, 런타임 훅 연결, GAS 상태 복원, 장비별 개별 상태 복원의 순서를 보장한다.
/// </summary>
public static class PlayerRuntimeRestoreCoordinator
{
    /// <summary>
    /// 책임 : 저장된 플레이어 상태를 새 씬 플레이어에 복원한다.
    /// 복원 순서는 Shell Equip → GAS Restore → Hook Attach → 장비 Runtime Restore 이다.
    /// explicit tag 복원이 끝나기 전에 장비가 복원 전용 태그를 다시 부여하면
    /// 이후 ClearAllExplicitTags에 지워질 수 있으므로 Hook Attach는 GAS 복원 뒤로 둔다.
    /// </summary>
    public static void RestoreAll(
        PlayerRuntimeState state,
        PlayerConsumableInventory consumableInventory,
        WeaponInventory2D weaponInventory,
        RelicInventory relicInventory,
        AttributeSet attributeSet,
        GameplayEffectRunner effectRunner,
        TagSystem tagSystem,
        AbilitySystem abilitySystem,
        IPlayerRuntimeResolver resolver,
        IWeaponRuntimeStateRestorer weaponRuntimeRestorer = null,
        IRelicRuntimeStateRestorer relicRuntimeRestorer = null,
        UnityEngine.Object restoreSource = null)
    {
        if (state == null)
            return;

        if (resolver == null)
        {
            Debug.LogError("[PlayerRuntimeRestoreCoordinator] resolver가 null입니다.");
            return;
        }

        // 1) 장비 껍데기 복원
        consumableInventory?.RestoreShellState(
            state.consumableInventory,
            resolver.ResolveConsumable);

        weaponInventory?.RestoreShellState(
            state.weaponInventory,
            resolver.ResolveWeapon,
            applyActiveVisual: true);

        relicInventory?.RestoreShellState(
            state.relicInventory,
            resolver.ResolveRelic);

        // 2) GAS 런타임 기본값 복원
        RestoreGasRuntime(
            state,
            attributeSet,
            effectRunner,
            tagSystem,
            abilitySystem,
            resolver,
            restoreSource);

        // 3) explicit tag 복원 이후 런타임 훅 연결
        // 책임 : 복원 장착이 다시 부여하는 장비/유물 전용 태그가
        // explicit tag clear 단계에 지워지지 않도록 순서를 보장한다.
        weaponInventory?.AttachRuntimeHooksForRestore();
        relicInventory?.AttachRuntimeHooksForRestore();

        // 4) 장비별 개별 런타임 상태 복원
        RestoreEquipmentRuntime(
            state,
            weaponInventory,
            relicInventory,
            resolver,
            weaponRuntimeRestorer,
            relicRuntimeRestorer);

        // 5) 장착형 modifier/effect가 모두 다시 붙은 뒤 상태형 current 값 복원
        RestoreAttributeCurrentValues(
            state.attributes,
            attributeSet,
            resolver,
            restoreSource);
    }

    /// <summary>
    /// 책임 : 저장된 GAS 런타임 상태를 복원한다.
    /// 진행 중 상태는 먼저 끊고, Attribute → Tag → Effect → Ability 순으로 적용한다.
    /// </summary>
    private static void RestoreGasRuntime(
        PlayerRuntimeState state,
        AttributeSet attributeSet,
        GameplayEffectRunner effectRunner,
        TagSystem tagSystem,
        AbilitySystem abilitySystem,
        IPlayerRuntimeResolver resolver,
        UnityEngine.Object restoreSource)
    {
        abilitySystem?.ResetTransientRuntimeState();

        RestoreAttributeBaseValues(state.attributes, attributeSet, resolver, restoreSource);
        RestoreExplicitTags(state.explicitTags, tagSystem, resolver);

        var target = attributeSet != null
            ? attributeSet.gameObject
            : (abilitySystem != null ? abilitySystem.gameObject : null);

        var instigator = abilitySystem != null
            ? abilitySystem.gameObject
            : target;

        RestoreEffects(state.activeEffects, effectRunner, resolver, target, instigator);
        RestoreAbilities(state.abilities, abilitySystem, resolver);
    }

    /// <summary>
    /// 책임 : 저장된 Attribute 현재값을 현재 플레이어 AttributeSet에 복원한다.
    /// </summary>
    private static void RestoreAttributeBaseValues(
        List<AttributeRuntimeSnapshot> snapshots,
        AttributeSet attributeSet,
        IPlayerRuntimeResolver resolver,
        UnityEngine.Object restoreSource)
    {
        if (attributeSet == null || snapshots == null)
            return;

        foreach (var entry in snapshots)
        {
            if (entry == null || string.IsNullOrEmpty(entry.attributeId))
                continue;

            var def = resolver.ResolveAttribute(entry.attributeId);
            if (def == null)
                continue;

            attributeSet.TrySetBaseValue(def, entry.baseValue, restoreSource);
        }
    }

    /// <summary>
    /// 책임 : 장착형 modifier/effect가 복원된 뒤 상태형 Attribute의 current 값만 다시 복원한다.
    /// HP처럼 실제 상태값은 살리고, 장비에서 계산되는 파생 스탯은 새 장비 source가 다시 계산하게 둔다.
    /// </summary>
    private static void RestoreAttributeCurrentValues(
        List<AttributeRuntimeSnapshot> snapshots,
        AttributeSet attributeSet,
        IPlayerRuntimeResolver resolver,
        UnityEngine.Object restoreSource)
    {
        if (attributeSet == null || snapshots == null)
            return;

        foreach (var entry in snapshots)
        {
            if (entry == null || string.IsNullOrEmpty(entry.attributeId))
                continue;

            var def = resolver.ResolveAttribute(entry.attributeId);
            if (def == null)
                continue;

            if (!attributeSet.ShouldRestoreCurrentValue(def))
                continue;

            attributeSet.TrySetCurrentValue(def, entry.currentValue, restoreSource);
        }
    }

    /// <summary>
    /// 책임 : 저장된 explicit tag 상태를 현재 TagSystem에 복원한다.
    /// 반드시 explicit 기준으로만 다룬다.
    /// </summary>
    private static void RestoreExplicitTags(
        List<ExplicitTagSnapshot> snapshots,
        TagSystem tagSystem,
        IPlayerRuntimeResolver resolver)
    {
        if (tagSystem == null || snapshots == null)
            return;

        tagSystem.ClearAllExplicitTags();

        foreach (var entry in snapshots)
        {
            if (entry == null || string.IsNullOrEmpty(entry.tagName))
                continue;

            var tag = resolver.ResolveTag(entry.tagName);
            if (tag == null)
                continue;

            int count = Mathf.Max(0, entry.count);
            for (int i = 0; i < count; i++)
                tagSystem.AddTag(tag);
        }
    }

    /// <summary>
    /// 책임 : 저장된 활성 effect 상태를 현재 GameplayEffectRunner에 복원한다.
    /// </summary>
    private static void RestoreEffects(
        List<ActiveGameplayEffectSnapshot> snapshots,
        GameplayEffectRunner effectRunner,
        IPlayerRuntimeResolver resolver,
        GameObject target,
        GameObject instigator)
    {
        if (effectRunner == null || resolver == null || target == null)
            return;

        effectRunner.ClearAllActiveEffects();
        effectRunner.RestoreActiveEffectSnapshots(
            snapshots,
            resolver.ResolveEffect,
            target,
            instigator);
    }

    /// <summary>
    /// 책임 : 저장된 ability 지속 상태를 현재 AbilitySystem에 복원한다.
    /// level, cooldown, charges, 커스텀 런타임 변수까지 함께 복원한다.
    /// </summary>
    private static void RestoreAbilities(
        List<AbilityPersistentState> states,
        AbilitySystem abilitySystem,
        IPlayerRuntimeResolver resolver)
    {
        if (abilitySystem == null || resolver == null)
            return;

        abilitySystem.RestorePersistentStates(
            states,
            resolver.ResolveAbility);
    }

    /// <summary>
    /// 책임 : 장비별 개별 런타임 상태를 슬롯 기준으로 복원한다.
    /// stack, 내부 카운터, 장비 전용 gauge 같은 상태를 이 단계에서 되살린다.
    /// </summary>
    private static void RestoreEquipmentRuntime(
        PlayerRuntimeState state,
        WeaponInventory2D weaponInventory,
        RelicInventory relicInventory,
        IPlayerRuntimeResolver resolver,
        IWeaponRuntimeStateRestorer weaponRuntimeRestorer,
        IRelicRuntimeStateRestorer relicRuntimeRestorer)
    {
        if (state == null)
            return;

        if (weaponRuntimeRestorer != null && state.weaponRuntimeStates != null)
        {
            foreach (var entry in state.weaponRuntimeStates)
            {
                if (entry == null)
                    continue;

                weaponRuntimeRestorer.RestoreWeaponRuntimeState(
                    weaponInventory,
                    entry,
                    resolver);
            }
        }

        if (relicRuntimeRestorer != null && state.relicRuntimeStates != null)
        {
            foreach (var entry in state.relicRuntimeStates)
            {
                if (entry == null)
                    continue;

                relicRuntimeRestorer.RestoreRelicRuntimeState(
                    relicInventory,
                    entry,
                    resolver);
            }
        }
    }
}
