using System;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(fileName = "LevelRewardEffect_SteelTraining", menuName = "Game/Progression/Level Reward Effects/Steel Training")]
public sealed class SteelTrainingLevelRewardEffectSO : LevelRewardEffectSO
{
    [SerializeField] private AttributeDefinition healthAttribute;
    [SerializeField] private AttributeDefinition maxHealthAttribute;
    [SerializeField] private AttributeDefinition soulHeartAttribute;
    [SerializeField] private StatusHudDefinition curseStatus;
    [SerializeField] private StatusHudDefinition completedStatus;
    [SerializeField, Min(1)] private int requiredDamageCount = 4;
    [SerializeField, Min(0f)] private float damageCountCooldownSeconds = 0.5f;
    [SerializeField, Min(0f)] private float maximumHealthPenalty = 2f;
    [SerializeField, Min(0f)] private float maximumHealthReward = 2f;

    [Serializable]
    private sealed class State
    {
        public int damageCount;
        public bool completed;
    }

    public override LevelRewardEffectLifetime Lifetime => LevelRewardEffectLifetime.Persistent;

    public override bool CanApply(LevelRewardEligibilityContext context, out string failureReason)
    {
        if (!base.CanApply(context, out failureReason))
            return false;

        AttributeSet attributes = context.Player.GetComponent<AttributeSet>();
        if (attributes == null || healthAttribute == null || maxHealthAttribute == null || soulHeartAttribute == null ||
            !maxHealthAttribute.AllowsModifier())
        {
            failureReason = "체력/최대 체력/소울 하트 Attribute 구성이 없습니다.";
            return false;
        }

        if (curseStatus == null || completedStatus == null)
        {
            failureReason = "강철의 수련 상태 HUD 구성이 없습니다.";
            return false;
        }

        if (attributes.GetAttributeValue(maxHealthAttribute) - Mathf.Max(0f, maximumHealthPenalty) < 1f)
        {
            failureReason = "현재 최대 체력이 저주를 감당할 수 없습니다.";
            return false;
        }

        failureReason = null;
        return true;
    }

    public override ILevelRewardEffectHandle Apply(LevelRewardApplyContext context)
    {
        PlayerInteractor2D player = context.Player;
        AttributeSet attributes = player != null ? player.GetComponent<AttributeSet>() : null;
        PlayerStatusRuntime statusRuntime = player != null ? PlayerStatusRuntime.GetOrAdd(player.gameObject) : null;
        if (player == null || attributes == null || statusRuntime == null)
            return null;

        State state = string.IsNullOrWhiteSpace(context.EffectState.json)
            ? new State()
            : JsonUtility.FromJson<State>(context.EffectState.json) ?? new State();
        state.damageCount = Mathf.Clamp(state.damageCount, 0, Mathf.Max(1, requiredDamageCount));

        bool suppressDamageTracking = false;
        float nextDamageCountTime = 0f;
        StatusHandle statusHandle = default;
        StatusHudDefinition displayedStatus = null;

        void SaveState()
        {
            context.EffectState.json = JsonUtility.ToJson(state);
        }

        void ApplyMaximumHealthModifier()
        {
            float currentHealth = attributes.GetCurrentValue(healthAttribute);
            suppressDamageTracking = true;
            try
            {
                attributes.RemoveModifiersFromSource(this);
                float modifierValue = state.completed
                    ? Mathf.Max(0f, maximumHealthReward)
                    : -Mathf.Max(0f, maximumHealthPenalty);
                attributes.TryAddModifier(
                    maxHealthAttribute,
                    new AttributeModifier(ModifierType.Flat, modifierValue, this));

                if (!state.completed)
                {
                    float currentMaximumHealth = attributes.GetAttributeValue(maxHealthAttribute);
                    attributes.TrySetCurrentValue(
                        healthAttribute,
                        Mathf.Min(currentHealth, currentMaximumHealth),
                        this);
                }
            }
            finally
            {
                suppressDamageTracking = false;
            }
        }

        StatusApplyRequest BuildStatusRequest()
        {
            int target = Mathf.Max(1, requiredDamageCount);
            string effectText = state.completed
                ? $"저주 해제\n최대 체력 +{Mathf.Max(0f, maximumHealthReward):0.#}"
                : $"최대 체력 -{Mathf.Max(0f, maximumHealthPenalty):0.#}\n피해를 받은 횟수: {state.damageCount}/{target}";
            return new StatusApplyRequest(
                state.completed ? completedStatus : curseStatus,
                ownerKey: "level_reward.steel_training",
                effectTextOverride: effectText,
                showStacksOverride: false,
                showDurationOverride: false);
        }

        void RefreshStatus(bool forceReplace = false)
        {
            StatusHudDefinition nextDefinition = state.completed ? completedStatus : curseStatus;
            if (forceReplace || displayedStatus != nextDefinition || !statusHandle.IsValid)
            {
                statusHandle.Release();
                statusHandle = statusRuntime.Apply(BuildStatusRequest());
                displayedStatus = nextDefinition;
                return;
            }

            statusRuntime.UpdateStatus(statusHandle, BuildStatusRequest());
        }

        void CompleteTraining()
        {
            if (state.completed)
                return;

            state.damageCount = Mathf.Max(1, requiredDamageCount);
            state.completed = true;
            ApplyMaximumHealthModifier();
            SaveState();
            RefreshStatus(forceReplace: true);
        }

        void HandleAttributeChanged(AttributeDefinition definition, float oldValue, float newValue)
        {
            if (state.completed || suppressDamageTracking || newValue >= oldValue)
                return;

            if (definition != healthAttribute && definition != soulHeartAttribute)
                return;

            float now = Time.time;
            if (now < nextDamageCountTime)
                return;

            nextDamageCountTime = now + Mathf.Max(0f, damageCountCooldownSeconds);
            state.damageCount = Mathf.Min(Mathf.Max(1, requiredDamageCount), state.damageCount + 1);
            if (state.damageCount >= Mathf.Max(1, requiredDamageCount))
            {
                CompleteTraining();
                return;
            }

            SaveState();
            RefreshStatus();
        }

        ApplyMaximumHealthModifier();
        SaveState();
        RefreshStatus();
        attributes.OnAttributeChanged += HandleAttributeChanged;
        return new LevelRewardEffectHandle(() =>
        {
            attributes.OnAttributeChanged -= HandleAttributeChanged;
            attributes.RemoveModifiersFromSource(this);
            statusHandle.Release();
        });
    }
}
