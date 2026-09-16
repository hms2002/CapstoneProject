using System;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(fileName = "LevelRewardEffect_Overheat", menuName = "Game/Progression/Level Reward Effects/Overheat")]
public sealed class OverheatLevelRewardEffectSO : LevelRewardEffectSO
{
    [SerializeField, Range(0f, 1f)] private float directDamageMultiplier = 0.8f;
    [SerializeField] private int burnApplicationBonus = 2;
    [SerializeField, Min(0f)] private float burnDamageMultiplier = 1.3f;

    public override LevelRewardEffectLifetime Lifetime => LevelRewardEffectLifetime.Persistent;

    public override bool CanApply(LevelRewardEligibilityContext context, out string failureReason)
    {
        if (!base.CanApply(context, out failureReason))
            return false;

        if (context.Player.GetComponent<AbilitySystem>() == null ||
            context.Player.GetComponent<WeaponInventory2D>() == null)
        {
            failureReason = "플레이어 무기/능력 구성이 없습니다.";
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
        BurnSourceRuntime burnRuntime = abilities != null ? BurnSourceRuntime.Resolve(abilities) : null;
        if (player == null || abilities == null || inventory == null || burnRuntime == null)
            return null;

        object burnModifierSource = new object();
        burnRuntime.SetModifier(
            burnModifierSource,
            new BurnSourceRuntime.Modifier(
                tickIntervalMultiplier: 1f,
                damageRatioAdd: 0f,
                applicationAdd: burnApplicationBonus,
                firstApplicationAdd: 0,
                allowCritical: false,
                damageRatioMultiplier: burnDamageMultiplier));

        IDisposable damageModifier = CombatOutgoingDamageModifiers.Register(damageContext =>
            LevelRewardDirectDamageUtility.IsDirectWeaponDamage(damageContext, abilities, inventory)
                ? damageContext.BaseDamage * Mathf.Clamp01(directDamageMultiplier)
                : damageContext.BaseDamage);

        return new LevelRewardEffectHandle(() =>
        {
            damageModifier.Dispose();
            if (burnRuntime != null)
                burnRuntime.RemoveModifier(burnModifierSource);
        });
    }
}
