using System;
using System.Collections;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(menuName = "Game/Progression/Level Reward Effects/Potion Cheer")]
public sealed class PotionCheerLevelRewardEffectSO : LevelRewardEffectSO
{
    [SerializeField] private ConsumableDefinition potion;
    [SerializeField] private AttributeDefinition attackSpeedAttribute;
    [SerializeField] private AttributeDefinition moveSpeedAttribute;
    [SerializeField] private StatusHudDefinition statusDefinition;
    [SerializeField] private float durationSeconds = 15f;
    [SerializeField] private float attackSpeedPercent = 0.15f;
    [SerializeField] private float moveSpeedPercent = 0.3f;

    [Serializable]
    private sealed class State
    {
        // Scaled session time continues across scene changes; run state is reset on run end.
        public float expiresAt;
    }

    public override LevelRewardEffectLifetime Lifetime => LevelRewardEffectLifetime.Persistent;

    public override bool CanApply(LevelRewardEligibilityContext context, out string failureReason)
    {
        if (!base.CanApply(context, out failureReason))
            return false;
        if (potion == null || statusDefinition == null || attackSpeedAttribute == null || moveSpeedAttribute == null ||
            !attackSpeedAttribute.AllowsModifier() || !moveSpeedAttribute.AllowsModifier() ||
            context.Player.GetComponent<AttributeSet>() == null ||
            context.Player.GetComponent<PlayerConsumableInventory>() == null)
        {
            failureReason = "포션/속도 Attribute/상태 HUD 구성이 없습니다.";
            return false;
        }
        return true;
    }

    public override ILevelRewardEffectHandle Apply(LevelRewardApplyContext context)
    {
        var player = context.Player;
        var attributes = player.GetComponent<AttributeSet>();
        var inventory = player.GetComponent<PlayerConsumableInventory>();
        if (attributes == null || inventory == null || potion == null || statusDefinition == null ||
            attackSpeedAttribute == null || moveSpeedAttribute == null)
            return null;

        var statusRuntime = PlayerStatusRuntime.GetOrAdd(player.gameObject);
        State state = string.IsNullOrWhiteSpace(context.EffectState.json)
            ? new State()
            : JsonUtility.FromJson<State>(context.EffectState.json) ?? new State();
        StatusHandle statusHandle = default;
        Coroutine timer = null;

        StatusApplyRequest BuildStatus()
        {
            float remaining = Mathf.Max(0f, state.expiresAt - Time.time);
            return new StatusApplyRequest(statusDefinition, "level_reward.potion_cheer",
                remainingTime: remaining, maxTime: durationSeconds,
                effectTextOverride: $"이동 속도 +{moveSpeedPercent * 100f:0.#}%, 공격 속도 +{attackSpeedPercent * 100f:0.#}%\n남은 시간: {remaining:0.0}초",
                showStacksOverride: false, showDurationOverride: false);
        }

        void ClearBuff()
        {
            if (attributes != null)
                attributes.RemoveModifiersFromSource(this);
            statusHandle.Release();
            statusHandle = default;
        }

        IEnumerator WatchExpiration()
        {
            while (Time.time < state.expiresAt)
            {
                statusRuntime.UpdateStatus(statusHandle, BuildStatus());
                yield return null;
            }
            ClearBuff();
            state.expiresAt = 0f;
            context.EffectState.json = JsonUtility.ToJson(state);
            timer = null;
        }

        void ApplyBuff()
        {
            // Replace our own values; drinking again refreshes duration, never stacks potency.
            attributes.RemoveModifiersFromSource(this);
            attributes.TryAddModifier(attackSpeedAttribute,
                new AttributeModifier(ModifierType.Percent, attackSpeedPercent, this));
            attributes.TryAddModifier(moveSpeedAttribute,
                new AttributeModifier(ModifierType.Percent, moveSpeedPercent, this));
            if (statusHandle.IsValid)
                statusRuntime.UpdateStatus(statusHandle, BuildStatus());
            else
                statusHandle = statusRuntime.Apply(BuildStatus());
            if (timer == null)
                timer = player.StartCoroutine(WatchExpiration());
        }

        void OnConsumed(ConsumableDefinition used)
        {
            if (used != potion || !RunSessionStore.IsRunActive)
                return;
            state.expiresAt = Time.time + durationSeconds;
            context.EffectState.json = JsonUtility.ToJson(state);
            ApplyBuff();
        }

        ClearBuff();
        if (state.expiresAt > Time.time)
            ApplyBuff();
        inventory.ConsumableUsed += OnConsumed;
        return new LevelRewardEffectHandle(() =>
        {
            inventory.ConsumableUsed -= OnConsumed;
            if (player != null && timer != null)
                player.StopCoroutine(timer);
            timer = null;
            ClearBuff();
        });
    }
}
