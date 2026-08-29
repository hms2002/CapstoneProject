using UnityEngine;
using UnityGAS;

[CreateAssetMenu(fileName = "LevelRewardEffect_SoulHeartGrant", menuName = "Game/Progression/Level Reward Effects/Soul Heart Grant")]
public sealed class SoulHeartGrantLevelRewardEffectSO : LevelRewardEffectSO
{
    [SerializeField] private AttributeDefinition soulHeartAttribute;
    [SerializeField, Min(0f)] private float amount = 5f;

    public override LevelRewardEffectLifetime Lifetime => LevelRewardEffectLifetime.InstantOnce;

    public override bool CanApply(LevelRewardEligibilityContext context, out string failureReason)
    {
        if (!base.CanApply(context, out failureReason)) return false;
        if (soulHeartAttribute == null || context.Player.GetComponent<AttributeSet>() == null)
        {
            failureReason = "소울 하트 Attribute 구성이 없습니다.";
            return false;
        }

        failureReason = null;
        return true;
    }

    public override ILevelRewardEffectHandle Apply(LevelRewardApplyContext context)
    {
        AttributeSet attributes = context.Player != null ? context.Player.GetComponent<AttributeSet>() : null;
        if (attributes != null && soulHeartAttribute != null)
            attributes.TryModifyAttributeValue(soulHeartAttribute, Mathf.Max(0f, amount), this);
        return null;
    }
}
