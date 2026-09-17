using System;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(menuName = "Game/Progression/Level Reward Effects/Potion Growth")]
public sealed class PotionGrowthLevelRewardEffectSO : LevelRewardEffectSO
{
    [SerializeField] private ConsumableDefinition potion;
    [SerializeField] private AttributeDefinition attackSpeedAttribute;
    [SerializeField] private float attackSpeedPerUse = 0.02f;

    [Serializable]
    private sealed class State
    {
        public int uses;
    }

    // Instant card artwork does not mean the trigger should use InstantOnce lifetime.
    public override LevelRewardEffectLifetime Lifetime => LevelRewardEffectLifetime.Persistent;

    public override bool CanApply(LevelRewardEligibilityContext context, out string failureReason)
    {
        if (!base.CanApply(context, out failureReason))
            return false;
        if (potion == null || attackSpeedAttribute == null || !attackSpeedAttribute.AllowsModifier() ||
            context.Player.GetComponent<AttributeSet>() == null ||
            context.Player.GetComponent<PlayerConsumableInventory>() == null)
        {
            failureReason = "포션/공격 속도/소모품 인벤토리 구성이 없습니다.";
            return false;
        }
        return true;
    }

    public override ILevelRewardEffectHandle Apply(LevelRewardApplyContext context)
    {
        var attributes = context.Player.GetComponent<AttributeSet>();
        var inventory = context.Player.GetComponent<PlayerConsumableInventory>();
        if (attributes == null || inventory == null || potion == null || attackSpeedAttribute == null)
            return null;

        State state = string.IsNullOrWhiteSpace(context.EffectState.json)
            ? new State()
            : JsonUtility.FromJson<State>(context.EffectState.json) ?? new State();
        state.uses = Mathf.Max(0, state.uses);

        void RefreshModifier()
        {
            attributes.RemoveModifiersFromSource(this);
            if (state.uses > 0)
                attributes.TryAddModifier(attackSpeedAttribute,
                    new AttributeModifier(ModifierType.Percent, state.uses * attackSpeedPerUse, this));
        }

        void OnConsumed(ConsumableDefinition used)
        {
            if (used != potion || !RunSessionStore.IsRunActive)
                return;
            state.uses++;
            context.EffectState.json = JsonUtility.ToJson(state);
            RefreshModifier();
        }

        RefreshModifier();
        inventory.ConsumableUsed += OnConsumed;
        return new LevelRewardEffectHandle(() =>
        {
            inventory.ConsumableUsed -= OnConsumed;
            if (attributes != null)
                attributes.RemoveModifiersFromSource(this);
        });
    }
}
