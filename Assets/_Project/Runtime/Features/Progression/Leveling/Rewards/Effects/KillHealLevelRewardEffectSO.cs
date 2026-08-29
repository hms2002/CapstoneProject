using System;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(fileName = "LevelRewardEffect_KillHeal", menuName = "Game/Progression/Level Reward Effects/Kill Heal")]
public sealed class KillHealLevelRewardEffectSO : LevelRewardEffectSO
{
    [SerializeField] private AttributeDefinition healthAttribute;
    [SerializeField, Min(1)] private int killsPerHeal = 20;
    [SerializeField, Min(0f)] private float healAmount = 1f;

    [Serializable]
    private sealed class State { public int killCount; }

    public override LevelRewardEffectLifetime Lifetime => LevelRewardEffectLifetime.Persistent;

    public override bool CanApply(LevelRewardEligibilityContext context, out string failureReason)
    {
        if (!base.CanApply(context, out failureReason)) return false;
        if (healthAttribute == null || context.Player.GetComponent<AttributeSet>() == null)
        {
            failureReason = "체력 Attribute 구성이 없습니다.";
            return false;
        }

        failureReason = null;
        return true;
    }

    public override ILevelRewardEffectHandle Apply(LevelRewardApplyContext context)
    {
        AttributeSet attributes = context.Player != null ? context.Player.GetComponent<AttributeSet>() : null;
        if (attributes == null || healthAttribute == null) return null;

        State state = string.IsNullOrWhiteSpace(context.EffectState.json)
            ? new State()
            : JsonUtility.FromJson<State>(context.EffectState.json) ?? new State();

        void HandleDeath(Enemy enemy)
        {
            state.killCount++;
            int threshold = Mathf.Max(1, killsPerHeal);
            if (state.killCount >= threshold)
            {
                state.killCount %= threshold;
                attributes.TryModifyAttributeValue(healthAttribute, Mathf.Max(0f, healAmount), this);
            }

            context.EffectState.json = JsonUtility.ToJson(state);
        }

        Enemy.AnyDeathStarted += HandleDeath;
        return new LevelRewardEffectHandle(() => Enemy.AnyDeathStarted -= HandleDeath);
    }
}
