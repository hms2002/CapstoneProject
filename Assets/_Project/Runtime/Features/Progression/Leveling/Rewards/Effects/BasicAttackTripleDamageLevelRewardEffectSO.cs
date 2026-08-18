using System;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(fileName = "LevelRewardEffect_BasicAttackTripleDamage", menuName = "Game/Progression/Level Reward Effects/Basic Attack Triple Damage")]
public sealed class BasicAttackTripleDamageLevelRewardEffectSO : LevelRewardEffectSO
{
    [SerializeField, Range(0f, 1f)] private float procChance = 0.1f;
    [SerializeField, Min(1f)] private float damageMultiplier = 3f;

    public override LevelRewardEffectLifetime Lifetime => LevelRewardEffectLifetime.Persistent;

    public override bool CanApply(LevelRewardEligibilityContext context, out string failureReason)
    {
        if (!base.CanApply(context, out failureReason)) return false;
        if (context.Player.GetComponent<AbilitySystem>() == null || context.Player.GetComponent<PlayerCombatInput2D>() == null)
        {
            failureReason = "플레이어 기본 공격 판별 구성이 없습니다.";
            return false;
        }

        failureReason = null;
        return true;
    }

    public override ILevelRewardEffectHandle Apply(LevelRewardApplyContext context)
    {
        AbilitySystem abilities = context.Player != null ? context.Player.GetComponent<AbilitySystem>() : null;
        PlayerCombatInput2D combatInput = context.Player != null ? context.Player.GetComponent<PlayerCombatInput2D>() : null;
        if (abilities == null || combatInput == null) return null;

        IDisposable registration = CombatOutgoingDamageModifiers.Register(damageContext =>
        {
            AbilityDefinition definition = damageContext.SourceSpec?.Definition;
            if (damageContext.SourceSystem != abilities || !combatInput.IsKnownBasicAttackAbility(definition))
                return damageContext.BaseDamage;

            return UnityEngine.Random.value < Mathf.Clamp01(procChance)
                ? damageContext.BaseDamage * Mathf.Max(1f, damageMultiplier)
                : damageContext.BaseDamage;
        });

        return new LevelRewardEffectHandle(registration.Dispose);
    }
}
