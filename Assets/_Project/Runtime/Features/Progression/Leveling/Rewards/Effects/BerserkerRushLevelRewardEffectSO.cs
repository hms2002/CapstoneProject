using System;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(fileName = "LevelRewardEffect_BerserkerRush", menuName = "Game/Progression/Level Reward Effects/Berserker Rush")]
public sealed class BerserkerRushLevelRewardEffectSO : LevelRewardEffectSO
{
    [SerializeField] private AttributeDefinition attackSpeedAttribute;
    [SerializeField] private AttributeDefinition moveSpeedAttribute;
    [SerializeField, Min(1f)] private float incomingDamageMultiplier = 2f;
    [SerializeField] private float attackSpeedPercent = 0.4f;
    [SerializeField] private float moveSpeedPercent = 0.2f;

    public override LevelRewardEffectLifetime Lifetime => LevelRewardEffectLifetime.Persistent;

    public override bool CanApply(LevelRewardEligibilityContext context, out string failureReason)
    {
        if (!base.CanApply(context, out failureReason))
            return false;

        if (attackSpeedAttribute == null || moveSpeedAttribute == null ||
            !attackSpeedAttribute.AllowsModifier() || !moveSpeedAttribute.AllowsModifier())
        {
            failureReason = "공격/이동 속도 Attribute 구성이 올바르지 않습니다.";
            return false;
        }

        if (context.Player.GetComponent<AttributeSet>() == null)
        {
            failureReason = "플레이어 스탯 구성이 없습니다.";
            return false;
        }

        failureReason = null;
        return true;
    }

    public override ILevelRewardEffectHandle Apply(LevelRewardApplyContext context)
    {
        GameObject player = context.Player != null ? context.Player.gameObject : null;
        AttributeSet attributes = player != null ? player.GetComponent<AttributeSet>() : null;
        if (player == null || attributes == null)
            return null;

        attributes.RemoveModifiersFromSource(this);
        bool attackAdded = attributes.TryAddModifier(
            attackSpeedAttribute,
            new AttributeModifier(ModifierType.Percent, attackSpeedPercent, this));
        bool moveAdded = attributes.TryAddModifier(
            moveSpeedAttribute,
            new AttributeModifier(ModifierType.Percent, moveSpeedPercent, this));

        if (!attackAdded || !moveAdded)
        {
            attributes.RemoveModifiersFromSource(this);
            return null;
        }

        IDisposable incomingDamage = CombatIncomingDamageModifiers.Register(damageContext =>
            damageContext.Target == player
                ? damageContext.BaseDamage * Mathf.Max(1f, incomingDamageMultiplier)
                : damageContext.BaseDamage);

        return new LevelRewardEffectHandle(() =>
        {
            incomingDamage.Dispose();
            if (attributes != null)
                attributes.RemoveModifiersFromSource(this);
        });
    }
}
