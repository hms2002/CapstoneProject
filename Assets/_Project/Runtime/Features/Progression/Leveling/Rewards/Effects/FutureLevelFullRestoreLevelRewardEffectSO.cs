using System;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(fileName = "LevelRewardEffect_FutureLevelFullRestore", menuName = "Game/Progression/Level Reward Effects/Future Level Growth")]
public sealed class FutureLevelFullRestoreLevelRewardEffectSO : LevelRewardEffectSO
{
    [SerializeField] private AttributeDefinition healthAttribute;
    [SerializeField] private AttributeDefinition maxHealthAttribute;
    [SerializeField] private AttributeDefinition soulHeartAttribute;
    [SerializeField, Min(0f)] private float healPerLevel = 2f;
    [SerializeField, Min(0f)] private float maxHealthPerFlawlessLevel = 1f;
    [SerializeField, Min(0)] private int maximumMaxHealthGrants = 3;

    [Serializable]
    private sealed class State
    {
        public int maxHealthGrantCount;
        public bool tookDamageSinceLastLevel;
    }

    public override LevelRewardEffectLifetime Lifetime => LevelRewardEffectLifetime.Persistent;

    public override bool CanApply(LevelRewardEligibilityContext context, out string failureReason)
    {
        if (!base.CanApply(context, out failureReason)) return false;
        if (healthAttribute == null || maxHealthAttribute == null || soulHeartAttribute == null ||
            !maxHealthAttribute.AllowsModifier() || context.Player.GetComponent<AttributeSet>() == null)
        {
            failureReason = "체력/최대 체력/소울 하트 Attribute 구성이 없습니다.";
            return false;
        }

        failureReason = null;
        return true;
    }

    public override ILevelRewardEffectHandle Apply(LevelRewardApplyContext context)
    {
        AttributeSet attributes = context.Player != null ? context.Player.GetComponent<AttributeSet>() : null;
        if (attributes == null) return null;

        State state = string.IsNullOrWhiteSpace(context.EffectState.json)
            ? new State()
            : JsonUtility.FromJson<State>(context.EffectState.json) ?? new State();
        bool isUpdatingMaxHealth = false;

        void SaveState()
        {
            context.EffectState.json = JsonUtility.ToJson(state);
        }

        void ApplyMaxHealthModifier()
        {
            float currentHealth = attributes.GetAttributeValue(healthAttribute);
            isUpdatingMaxHealth = true;
            try
            {
                attributes.RemoveModifiersFromSource(this);
                float grantedMaxHealth = Mathf.Max(0, state.maxHealthGrantCount) *
                                         Mathf.Max(0f, maxHealthPerFlawlessLevel);
                if (grantedMaxHealth > 0f)
                {
                    attributes.TryAddModifier(
                        maxHealthAttribute,
                        new AttributeModifier(ModifierType.Flat, grantedMaxHealth, this));
                }

                float currentMaxHealth = attributes.GetAttributeValue(maxHealthAttribute);
                attributes.TrySetCurrentValue(
                    healthAttribute,
                    Mathf.Min(currentHealth, currentMaxHealth),
                    this);
            }
            finally
            {
                isUpdatingMaxHealth = false;
            }
        }

        void HandleAttributeChanged(AttributeDefinition definition, float oldValue, float newValue)
        {
            if (isUpdatingMaxHealth || newValue >= oldValue)
                return;

            if (definition == healthAttribute || definition == soulHeartAttribute)
            {
                state.tookDamageSinceLastLevel = true;
                SaveState();
            }
        }

        void HandleExperienceGranted(LevelProgressionGrantResult result)
        {
            int levelsGained = Mathf.Max(0, result.LevelsGained);
            if (levelsGained <= 0)
                return;

            for (int i = 0; i < levelsGained; i++)
            {
                if (!state.tookDamageSinceLastLevel &&
                    state.maxHealthGrantCount < Mathf.Max(0, maximumMaxHealthGrants))
                {
                    state.maxHealthGrantCount++;
                }

                state.tookDamageSinceLastLevel = false;
            }

            ApplyMaxHealthModifier();
            attributes.TryModifyAttributeValue(
                healthAttribute,
                Mathf.Max(0f, healPerLevel) * levelsGained,
                this);
            SaveState();
        }

        ApplyMaxHealthModifier();
        attributes.OnAttributeChanged += HandleAttributeChanged;
        RunLevelProgression.ExperienceGranted += HandleExperienceGranted;
        return new LevelRewardEffectHandle(() =>
        {
            RunLevelProgression.ExperienceGranted -= HandleExperienceGranted;
            if (attributes != null)
            {
                attributes.OnAttributeChanged -= HandleAttributeChanged;
                attributes.RemoveModifiersFromSource(this);
            }
        });
    }
}
