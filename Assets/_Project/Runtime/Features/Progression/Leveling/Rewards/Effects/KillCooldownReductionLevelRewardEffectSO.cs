using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(fileName = "LevelRewardEffect_KillCooldownReduction", menuName = "Game/Progression/Level Reward Effects/Kill Cooldown Reduction")]
public sealed class KillCooldownReductionLevelRewardEffectSO : LevelRewardEffectSO
{
    [SerializeField, Range(0f, 1f)] private float reductionRatio = 0.05f;

    public override LevelRewardEffectLifetime Lifetime => LevelRewardEffectLifetime.Persistent;

    public override bool CanApply(LevelRewardEligibilityContext context, out string failureReason)
    {
        if (!base.CanApply(context, out failureReason)) return false;
        if (context.Player.GetComponent<AbilitySystem>() == null || context.Player.GetComponent<WeaponInventory2D>() == null)
        {
            failureReason = "플레이어 무기/능력 구성이 없습니다.";
            return false;
        }

        failureReason = null;
        return true;
    }

    public override ILevelRewardEffectHandle Apply(LevelRewardApplyContext context)
    {
        AbilitySystem abilities = context.Player != null ? context.Player.GetComponent<AbilitySystem>() : null;
        WeaponInventory2D inventory = context.Player != null ? context.Player.GetComponent<WeaponInventory2D>() : null;
        if (abilities == null || inventory == null) return null;

        void HandleDeath(Enemy enemy)
        {
            float ratio = Mathf.Clamp01(reductionRatio);
            var visited = new HashSet<AbilityDefinition>();
            for (int slot = 0; slot < inventory.SlotCount; slot++)
            {
                WeaponDefinition weapon = inventory.GetWeaponInSlot(slot);
                if (weapon == null) continue;

                foreach (AbilityDefinition ability in weapon.EnumerateGrantedAbilities())
                {
                    if (ability == null || !visited.Add(ability)) continue;
                    float remaining = abilities.GetCooldownRemaining(ability);
                    if (remaining > 0f)
                        abilities.TrySetCooldownRemaining(ability, remaining * (1f - ratio));
                }
            }
        }

        Enemy.AnyDeathStarted += HandleDeath;
        return new LevelRewardEffectHandle(() => Enemy.AnyDeathStarted -= HandleDeath);
    }
}
