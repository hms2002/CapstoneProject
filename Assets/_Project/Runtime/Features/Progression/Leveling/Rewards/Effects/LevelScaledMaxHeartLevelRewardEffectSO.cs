using System;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(fileName = "LevelRewardEffect_LevelScaledMaxHeart", menuName = "Game/Progression/Level Reward Effects/Level Scaled Max Heart")]
public sealed class LevelScaledMaxHeartLevelRewardEffectSO : LevelRewardEffectSO
{
    [SerializeField] private AttributeDefinition maxHealthAttribute;
    [SerializeField, Min(1)] private int levelsPerHeart = 3;
    [SerializeField, Min(0f)] private float maxHealthPerHeart = 1f;

    [Serializable]
    private sealed class State { public float grantedMaxHealth; }

    public override LevelRewardEffectLifetime Lifetime => LevelRewardEffectLifetime.Persistent;

    public override bool CanApply(LevelRewardEligibilityContext context, out string failureReason)
    {
        if (!base.CanApply(context, out failureReason)) return false;
        if (maxHealthAttribute == null || !maxHealthAttribute.AllowsModifier())
        {
            failureReason = "최대 체력 Attribute가 modifier를 허용하지 않습니다.";
            return false;
        }

        if (context.Progression == null || context.Progression.level < Mathf.Max(1, levelsPerHeart))
        {
            failureReason = "현재 레벨이 효과 최소 조건보다 낮습니다.";
            return false;
        }

        failureReason = null;
        return true;
    }

    public override ILevelRewardEffectHandle Apply(LevelRewardApplyContext context)
    {
        AttributeSet attributes = context.Player != null ? context.Player.GetComponent<AttributeSet>() : null;
        if (attributes == null || maxHealthAttribute == null) return null;

        State state = string.IsNullOrWhiteSpace(context.EffectState.json)
            ? new State()
            : JsonUtility.FromJson<State>(context.EffectState.json) ?? new State();

        if (!context.IsReapply && state.grantedMaxHealth <= 0f)
        {
            int hearts = Mathf.FloorToInt(context.Progression.level / (float)Mathf.Max(1, levelsPerHeart));
            state.grantedMaxHealth = hearts * Mathf.Max(0f, maxHealthPerHeart);
            context.EffectState.json = JsonUtility.ToJson(state);
        }

        if (state.grantedMaxHealth <= 0f) return null;
        attributes.RemoveModifiersFromSource(this);
        if (!attributes.TryAddModifier(maxHealthAttribute, new AttributeModifier(ModifierType.Flat, state.grantedMaxHealth, this)))
            return null;

        return new LevelRewardEffectHandle(() =>
        {
            if (attributes != null && this != null)
                attributes.RemoveModifiersFromSource(this);
        });
    }
}
