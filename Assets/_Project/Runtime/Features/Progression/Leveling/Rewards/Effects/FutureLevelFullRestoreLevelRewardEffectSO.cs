using UnityEngine;
using UnityGAS;

[CreateAssetMenu(fileName = "LevelRewardEffect_FutureLevelFullRestore", menuName = "Game/Progression/Level Reward Effects/Future Level Full Restore")]
public sealed class FutureLevelFullRestoreLevelRewardEffectSO : LevelRewardEffectSO
{
    public override LevelRewardEffectLifetime Lifetime => LevelRewardEffectLifetime.Persistent;

    public override bool CanApply(LevelRewardEligibilityContext context, out string failureReason)
    {
        if (!base.CanApply(context, out failureReason)) return false;
        if (context.Player.GetComponent<AbilitySystem>() == null)
        {
            failureReason = "플레이어 능력 시스템 구성이 없습니다.";
            return false;
        }

        failureReason = null;
        return true;
    }

    public override ILevelRewardEffectHandle Apply(LevelRewardApplyContext context)
    {
        GameObject player = context.Player != null ? context.Player.gameObject : null;
        AbilitySystem abilities = player != null ? player.GetComponent<AbilitySystem>() : null;
        if (player == null || abilities == null) return null;

        void HandleExperienceGranted(LevelProgressionGrantResult result)
        {
            if (result.LevelsGained <= 0) return;
            PlayerHealthRestoreUtility.FillLinkedHealthToMax(player, this);
            abilities.RestoreAllCooldowns();
        }

        RunLevelProgression.ExperienceGranted += HandleExperienceGranted;
        return new LevelRewardEffectHandle(() => RunLevelProgression.ExperienceGranted -= HandleExperienceGranted);
    }
}
