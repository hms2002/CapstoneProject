using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(fileName = "LevelRewardEffect_HuntingFlow", menuName = "Game/Progression/Level Reward Effects/Hunting Flow")]
public sealed class HuntingFlowLevelRewardEffectSO : LevelRewardEffectSO
{
    [SerializeField] private AttributeDefinition attackSpeedAttribute;
    [SerializeField, Min(1)] private int killsRequired = 5;
    [SerializeField, Min(0.1f)] private float killWindowSeconds = 5f;
    [SerializeField, Min(0.1f)] private float buffDurationSeconds = 5f;
    [SerializeField] private float attackSpeedPercent = 0.2f;

    public override LevelRewardEffectLifetime Lifetime => LevelRewardEffectLifetime.Persistent;

    public override bool CanApply(LevelRewardEligibilityContext context, out string failureReason)
    {
        if (!base.CanApply(context, out failureReason))
            return false;

        if (attackSpeedAttribute == null || !attackSpeedAttribute.AllowsModifier())
        {
            failureReason = "공격 속도 Attribute 구성이 올바르지 않습니다.";
            return false;
        }

        if (context.Player.GetComponent<AbilitySystem>() == null ||
            context.Player.GetComponent<AttributeSet>() == null)
        {
            failureReason = "플레이어 능력/스탯 구성이 없습니다.";
            return false;
        }

        failureReason = null;
        return true;
    }

    public override ILevelRewardEffectHandle Apply(LevelRewardApplyContext context)
    {
        PlayerInteractor2D player = context.Player;
        AbilitySystem abilities = player != null ? player.GetComponent<AbilitySystem>() : null;
        AttributeSet attributes = player != null ? player.GetComponent<AttributeSet>() : null;
        if (player == null || abilities == null || attributes == null || abilities.KillConfirmedTag == null)
            return null;

        var killTimes = new Queue<float>();
        bool buffActive = false;
        float buffExpiresAt = 0f;
        Coroutine expirationRoutine = null;

        void SetBuffActive(bool active)
        {
            attributes.RemoveModifiersFromSource(this);
            buffActive = false;

            if (!active)
                return;

            buffActive = attributes.TryAddModifier(
                attackSpeedAttribute,
                new AttributeModifier(ModifierType.Percent, attackSpeedPercent, this));
        }

        IEnumerator WatchExpiration()
        {
            while (buffActive)
            {
                if (Time.time >= buffExpiresAt)
                {
                    SetBuffActive(false);
                    killTimes.Clear();
                    expirationRoutine = null;
                    yield break;
                }

                yield return null;
            }

            expirationRoutine = null;
        }

        void HandleGameplayEvent(GameplayTag tag, AbilityEventData data)
        {
            if (tag != abilities.KillConfirmedTag)
                return;

            float now = Time.time;
            if (buffActive)
            {
                buffExpiresAt = now + Mathf.Max(0.1f, buffDurationSeconds);
                return;
            }

            float window = Mathf.Max(0.1f, killWindowSeconds);
            killTimes.Enqueue(now);
            while (killTimes.Count > 0 && now - killTimes.Peek() > window)
                killTimes.Dequeue();

            if (killTimes.Count < Mathf.Max(1, killsRequired))
                return;

            killTimes.Clear();
            SetBuffActive(true);
            if (!buffActive)
                return;

            buffExpiresAt = now + Mathf.Max(0.1f, buffDurationSeconds);
            if (expirationRoutine == null)
                expirationRoutine = player.StartCoroutine(WatchExpiration());
        }

        abilities.SubscribeGameplayEvent(HandleGameplayEvent);
        return new LevelRewardEffectHandle(() =>
        {
            abilities.UnsubscribeGameplayEvent(HandleGameplayEvent);
            if (player != null && expirationRoutine != null)
                player.StopCoroutine(expirationRoutine);
            expirationRoutine = null;
            attributes.RemoveModifiersFromSource(this);
        });
    }
}
