using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 월영도 장착 중 프리팹 live adapter로서 현재 슬롯 MoonBladeRuntimeData와 무기 상호작용 계층을 연결한다.
/// - 월영도 공격 적중으로 냉기를 쌓고, 공명 피니시 시동 성공 사실은 interaction layer에만 전달한다.
/// </summary>
public sealed class MoonBladeRuntimeState : WeaponAbilityRuntimeState
{
    private const string HitConfirmTagResourcePath = "Tags/Event.HitConfirm";

    private static GameplayTag hitConfirmRootTag;

    private WeaponInventory2D weaponInventory;
    private IWeaponInteractionLayer interactionLayer;

    public MoonBladeRuntimeData BoundData => GetBoundData();

    private void Awake()
    {
        CacheInventory();
    }

    public override void HandleEquippedWeaponChanged(WeaponDefinition previousWeapon, WeaponDefinition newWeapon)
    {
        CacheInventory();
    }

    public override void HandleAbilityActivated(
        WeaponDefinition weapon,
        WeaponAbilitySlot slot,
        AbilityDefinition activatedAbility)
    {
        if (weapon == null || activatedAbility == null)
            return;

        if (weapon.abilityLoadout is not MoonBladeLoadout loadout)
            return;

        if (activatedAbility != loadout.LunarFinishStarter)
            return;

        MoonBladeRuntimeData moonData = GetBoundData();
        if (moonData == null)
            return;

        BuildInteractionLayer()?.NotifyAbilityActivated(new WeaponInteractionContext(
            weaponInventory,
            weapon,
            weaponInventory != null ? weaponInventory.ActiveIndex : -1,
            slot,
            this,
            moonData,
            activatedAbility,
            weaponInventory != null ? weaponInventory.GetOtherWeaponInSlot(weaponInventory.ActiveIndex) : null,
            weaponInventory != null ? weaponInventory.GetOtherSlotIndex(weaponInventory.ActiveIndex) : -1,
            weaponInventory != null ? weaponInventory.GetOtherRuntimeData(weaponInventory.ActiveIndex) : null));
    }

    public override void HandleGameplayEvent(WeaponDefinition weapon, GameplayTag tag, in AbilityEventData data)
    {
        if (weapon == null || tag == null || data.Spec?.Definition == null)
            return;

        if (weapon.abilityLoadout is not MoonBladeLoadout loadout)
            return;

        if (!MatchesHitConfirmTag(tag))
            return;

        if (data.Spec.Definition != loadout.BaseAttack && data.Spec.Definition != loadout.FrostedAttack)
            return;

        MoonBladeRuntimeData moonData = GetBoundData();
        if (moonData == null)
            return;

        moonData.AddColdStack();
        Debug.Log($"[MoonBladeRuntimeState] Cold stack gained: {moonData.ColdStacks}/{moonData.MaxColdStacks}, decay={moonData.ColdDecayRemaining:0.00}s", this);
    }

    private void CacheInventory()
    {
        if (weaponInventory == null)
            weaponInventory = GetComponentInParent<WeaponInventory2D>();

        if (interactionLayer == null && weaponInventory != null)
            interactionLayer = weaponInventory.InteractionLayer;
    }

    private MoonBladeRuntimeData GetBoundData()
    {
        CacheInventory();
        return weaponInventory != null
            ? weaponInventory.ActiveRuntimeData as MoonBladeRuntimeData
            : null;
    }

    private IWeaponInteractionLayer BuildInteractionLayer()
    {
        CacheInventory();
        return interactionLayer;
    }

    private static bool MatchesHitConfirmTag(GameplayTag raisedTag)
    {
        hitConfirmRootTag ??= Resources.Load<GameplayTag>(HitConfirmTagResourcePath);
        if (raisedTag == null || hitConfirmRootTag == null)
            return false;

        for (GameplayTag current = raisedTag; current != null; current = current.Parent)
        {
            if (current == hitConfirmRootTag)
                return true;
        }

        return false;
    }
}
