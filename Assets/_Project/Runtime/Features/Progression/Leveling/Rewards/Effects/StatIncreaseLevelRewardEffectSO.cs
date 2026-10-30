using UnityEngine;
using UnityGAS;

/// <summary>One repeatable, run-owned stat increase. Each selection owns its exact modifier.</summary>
[CreateAssetMenu(fileName = "LevelRewardEffect_StatIncrease", menuName = "Game/Progression/Level Reward Effects/Stat Increase")]
public sealed class StatIncreaseLevelRewardEffectSO : LevelRewardEffectSO
{
    [SerializeField] private StatId statId;
    [SerializeField] private AttributeDefinition attribute;
    [SerializeField] private ModifierType modifierType;
    [SerializeField] private float value;
    [SerializeField, Range(0, 2)] private int tier;

    public StatId Stat => statId;
    public int Tier => tier;
    public float Value => value;
    public int TierWeight => tier == 0 ? 70 : tier == 1 ? 25 : 5;
    public bool IsElement => statId == StatId.FireFinal || statId == StatId.ElectricFinal ||
                             statId == StatId.PoisonFinal || statId == StatId.BleedFinal;
    public override LevelRewardEffectLifetime Lifetime => LevelRewardEffectLifetime.Persistent;

    public override bool CanApply(LevelRewardEligibilityContext context, out string failureReason)
    {
        if (!base.CanApply(context, out failureReason)) return false;
        var attributes = context.Player.GetComponent<AttributeSet>();
        if (statId == StatId.None || attribute == null || !attribute.AllowsModifier() ||
            attributes == null || attributes.GetAttribute(attribute) == null || value <= 0f || tier < 0 || tier > 2)
        {
            failureReason = "수치 보상 Attribute 구성이 없습니다.";
            return false;
        }

        // Do not exclude a stat at 100%: its overflow remains available to future effects.
        failureReason = null;
        return true;
    }

    public override ILevelRewardEffectHandle Apply(LevelRewardApplyContext context)
    {
        var attributes = context.Player != null ? context.Player.GetComponent<AttributeSet>() : null;
        if (attributes == null || attribute == null) return null;

        var modifier = new AttributeModifier(modifierType, value, this);
        if (!attributes.TryAddModifier(attribute, modifier)) return null;
        return new LevelRewardEffectHandle(() =>
        {
            if (attributes != null) attributes.TryRemoveModifier(attribute, modifier);
        });
    }

    public static StatIncreaseLevelRewardEffectSO Find(LevelRewardDefinitionSO definition)
    {
        if (definition == null) return null;
        foreach (var effect in definition.Effects)
            if (effect is StatIncreaseLevelRewardEffectSO stat) return stat;
        return null;
    }
}
