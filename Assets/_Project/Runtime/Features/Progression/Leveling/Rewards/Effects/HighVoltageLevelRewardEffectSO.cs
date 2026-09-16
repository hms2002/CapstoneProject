using System;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(fileName = "LevelRewardEffect_HighVoltage", menuName = "Game/Progression/Level Reward Effects/High Voltage")]
public sealed class HighVoltageLevelRewardEffectSO : LevelRewardEffectSO
{
    [SerializeField] private GameplayTag electricElementTag;
    [SerializeField, Range(0f, 1f)] private float directDamageMultiplier = 0.8f;
    [SerializeField, Min(0f)] private float electricBuildUpMultiplier = 1.25f;
    [SerializeField] private float electricDischargeDamageBonus = 2f;

    public override LevelRewardEffectLifetime Lifetime => LevelRewardEffectLifetime.Persistent;

    public override bool CanApply(LevelRewardEligibilityContext context, out string failureReason)
    {
        if (!base.CanApply(context, out failureReason))
            return false;

        if (electricElementTag == null ||
            context.Player.GetComponent<AbilitySystem>() == null ||
            context.Player.GetComponent<WeaponInventory2D>() == null)
        {
            failureReason = "전기 태그 또는 플레이어 무기/능력 구성이 없습니다.";
            return false;
        }

        failureReason = null;
        return true;
    }

    public override ILevelRewardEffectHandle Apply(LevelRewardApplyContext context)
    {
        GameObject player = context.Player != null ? context.Player.gameObject : null;
        AbilitySystem abilities = player != null ? player.GetComponent<AbilitySystem>() : null;
        WeaponInventory2D inventory = player != null ? player.GetComponent<WeaponInventory2D>() : null;
        if (player == null || abilities == null || inventory == null || electricElementTag == null)
            return null;

        IDisposable damageModifier = CombatOutgoingDamageModifiers.Register(damageContext =>
            LevelRewardDirectDamageUtility.IsDirectWeaponDamage(damageContext, abilities, inventory)
                ? damageContext.BaseDamage * Mathf.Clamp01(directDamageMultiplier)
                : damageContext.BaseDamage);

        IDisposable buildUpModifier = ElementBuildUpModifiers.Register(buildUpContext =>
            buildUpContext.Attacker == player && buildUpContext.ElementType == electricElementTag
                ? buildUpContext.BaseAmount * Mathf.Max(0f, electricBuildUpMultiplier)
                : buildUpContext.BaseAmount);

        IDisposable dischargeModifier = ElectricDischargeDamageModifiers.Register(dischargeContext =>
            dischargeContext.Instigator == player
                ? dischargeContext.BaseDamage + electricDischargeDamageBonus
                : dischargeContext.BaseDamage);

        return new LevelRewardEffectHandle(() =>
        {
            damageModifier.Dispose();
            buildUpModifier.Dispose();
            dischargeModifier.Dispose();
        });
    }
}
