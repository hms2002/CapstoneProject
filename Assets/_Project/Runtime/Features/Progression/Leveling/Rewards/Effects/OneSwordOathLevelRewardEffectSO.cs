using System;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(
    fileName = "LevelRewardEffect_OneSwordOath",
    menuName = "Game/Progression/Level Reward Effects/One Sword Oath")]
public sealed class OneSwordOathLevelRewardEffectSO : LevelRewardEffectSO
{
    [Header("Weapon Slots")]
    [SerializeField] private int mainWeaponSlotIndex = 0;
    [SerializeField] private int sealedWeaponSlotIndex = 1;

    [Header("Bonuses")]
    [SerializeField] private AttributeDefinition attackPowerAttribute;
    [SerializeField] private float attackPowerPercent = 0.3f;
    [SerializeField] private float mainWeaponCooldownMultiplier = 0.6f;

    public override LevelRewardEffectLifetime Lifetime => LevelRewardEffectLifetime.Persistent;

    public override bool CanApply(LevelRewardEligibilityContext context, out string failureReason)
    {
        if (!base.CanApply(context, out failureReason))
            return false;

        if (!TryResolve(context.Player, out WeaponInventory2D inventory, out _, out _))
        {
            failureReason = "플레이어의 무기/능력/스탯 구성이 없습니다.";
            return false;
        }

        if (attackPowerAttribute == null || !attackPowerAttribute.AllowsModifier())
        {
            failureReason = "공격력 Attribute가 없거나 modifier를 허용하지 않습니다.";
            return false;
        }

        if (inventory.GetWeaponInSlot(mainWeaponSlotIndex) == null)
        {
            failureReason = "1번 슬롯에 무기가 없습니다.";
            return false;
        }

        if (mainWeaponSlotIndex == sealedWeaponSlotIndex ||
            mainWeaponCooldownMultiplier <= 0f ||
            !inventory.CanAcquireSlotSeal(sealedWeaponSlotIndex))
        {
            failureReason = "현재 상태에서는 2번 슬롯을 안전하게 봉인할 수 없습니다.";
            return false;
        }

        failureReason = null;
        return true;
    }

    public override ILevelRewardEffectHandle Apply(LevelRewardApplyContext context)
    {
        if (!TryResolve(context.Player, out WeaponInventory2D inventory, out AbilitySystem abilities, out AttributeSet attributes))
            return null;

        IDisposable slotSeal = inventory.TryAcquireSlotSeal(sealedWeaponSlotIndex);
        if (slotSeal == null)
        {
            Debug.LogWarning("[OneSwordOath] Failed to seal the secondary weapon slot.", this);
            return null;
        }

        attributes.RemoveModifiersFromSource(this);
        bool modifierAdded = attributes.TryAddModifier(
            attackPowerAttribute,
            new AttributeModifier(ModifierType.Percent, attackPowerPercent, this));

        if (!modifierAdded)
        {
            slotSeal.Dispose();
            return null;
        }

        IDisposable cooldownMultiplier = abilities.AddScopedCooldownDurationMultiplier(
            ability => IsGrantedByMainWeapon(inventory, ability),
            mainWeaponCooldownMultiplier);

        if (cooldownMultiplier == null)
        {
            attributes.RemoveModifiersFromSource(this);
            slotSeal.Dispose();
            return null;
        }

        return new EffectHandle(attributes, this, cooldownMultiplier, slotSeal);
    }

    private bool TryResolve(
        PlayerInteractor2D player,
        out WeaponInventory2D inventory,
        out AbilitySystem abilities,
        out AttributeSet attributes)
    {
        inventory = player != null ? player.GetComponent<WeaponInventory2D>() : null;
        abilities = player != null ? player.GetComponent<AbilitySystem>() : null;
        attributes = player != null ? player.GetComponent<AttributeSet>() : null;
        return inventory != null && abilities != null && attributes != null;
    }

    private bool IsGrantedByMainWeapon(WeaponInventory2D inventory, AbilityDefinition ability)
    {
        if (inventory == null || ability == null)
            return false;

        WeaponDefinition weapon = inventory.GetWeaponInSlot(mainWeaponSlotIndex);
        if (weapon == null)
            return false;

        foreach (AbilityDefinition grantedAbility in weapon.EnumerateGrantedAbilities())
        {
            if (grantedAbility == ability)
                return true;
        }

        return false;
    }

    private sealed class EffectHandle : ILevelRewardEffectHandle
    {
        private AttributeSet attributes;
        private UnityEngine.Object modifierSource;
        private IDisposable cooldownMultiplier;
        private IDisposable slotSeal;

        public EffectHandle(
            AttributeSet attributes,
            UnityEngine.Object modifierSource,
            IDisposable cooldownMultiplier,
            IDisposable slotSeal)
        {
            this.attributes = attributes;
            this.modifierSource = modifierSource;
            this.cooldownMultiplier = cooldownMultiplier;
            this.slotSeal = slotSeal;
        }

        public void Dispose()
        {
            cooldownMultiplier?.Dispose();
            cooldownMultiplier = null;

            if (attributes != null && modifierSource != null)
                attributes.RemoveModifiersFromSource(modifierSource);

            attributes = null;
            modifierSource = null;

            slotSeal?.Dispose();
            slotSeal = null;
        }
    }
}
