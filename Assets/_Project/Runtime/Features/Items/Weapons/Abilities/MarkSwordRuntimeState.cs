using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 표식검 장착 중 프리팹 live adapter로서 현재 슬롯 MarkSwordRuntimeData와 무기 상호작용 계층을 연결한다.
/// - 기본 공격 적중 시 표식을 쌓고, 검 스킬 소비 같은 교차 무기 상호작용은 interaction layer에 사실만 전달한다.
/// </summary>
public sealed class MarkSwordRuntimeState : WeaponAbilityRuntimeState
{
    private const string HitConfirmTagResourcePath = "Tags/Event.HitConfirm";

    private static GameplayTag hitConfirmRootTag;

    private WeaponInventory2D weaponInventory;
    private IWeaponInteractionLayer interactionLayer;

    public MarkSwordRuntimeData BoundData => GetBoundData();

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

        if (weapon.abilityLoadout is not MarkSwordLoadout loadout)
            return;

        if (activatedAbility != loadout.ReboundSlash)
            return;

        MarkSwordRuntimeData swordData = GetBoundData();
        if (swordData == null)
            return;

        BuildInteractionLayer()?.NotifyAbilityActivated(new WeaponInteractionContext(
            weaponInventory,
            weapon,
            weaponInventory != null ? weaponInventory.ActiveIndex : -1,
            slot,
            this,
            swordData,
            activatedAbility,
            weaponInventory != null ? weaponInventory.GetOtherWeaponInSlot(weaponInventory.ActiveIndex) : null,
            weaponInventory != null ? weaponInventory.GetOtherSlotIndex(weaponInventory.ActiveIndex) : -1,
            weaponInventory != null ? weaponInventory.GetOtherRuntimeData(weaponInventory.ActiveIndex) : null));
    }

    public override void HandleGameplayEvent(WeaponDefinition weapon, GameplayTag tag, in AbilityEventData data)
    {
        if (weapon == null || tag == null || data.Spec?.Definition == null)
            return;

        if (weapon.abilityLoadout is not MarkSwordLoadout loadout)
            return;

        if (!MatchesHitConfirmTag(tag))
            return;

        if (data.Spec.Definition != loadout.BaseAttack)
            return;

        MarkSwordRuntimeData swordData = GetBoundData();
        if (swordData == null)
            return;

        swordData.AddMarkStack();
    }

    private void CacheInventory()
    {
        if (weaponInventory == null)
            weaponInventory = GetComponentInParent<WeaponInventory2D>();

        if (interactionLayer == null && weaponInventory != null)
            interactionLayer = weaponInventory.InteractionLayer;
    }

    private MarkSwordRuntimeData GetBoundData()
    {
        CacheInventory();
        return weaponInventory != null
            ? weaponInventory.ActiveRuntimeData as MarkSwordRuntimeData
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
