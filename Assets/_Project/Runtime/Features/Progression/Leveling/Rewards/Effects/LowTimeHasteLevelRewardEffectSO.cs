using UnityEngine;
using UnityGAS;

[CreateAssetMenu(fileName = "LevelRewardEffect_LowTimeHaste", menuName = "Game/Progression/Level Reward Effects/Low Time Haste")]
public sealed class LowTimeHasteLevelRewardEffectSO : LevelRewardEffectSO
{
    [SerializeField] private AttributeDefinition moveSpeedAttribute;
    [SerializeField] private AttributeDefinition attackSpeedAttribute;
    [SerializeField, Min(0f)] private float thresholdSeconds = 360f;
    [SerializeField] private float moveSpeedPercent = 0.4f;
    [SerializeField] private float attackSpeedPercent = 0.25f;

    public override LevelRewardEffectLifetime Lifetime => LevelRewardEffectLifetime.Persistent;

    public override bool CanApply(LevelRewardEligibilityContext context, out string failureReason)
    {
        if (!base.CanApply(context, out failureReason)) return false;
        if (moveSpeedAttribute == null || attackSpeedAttribute == null ||
            !moveSpeedAttribute.AllowsModifier() || !attackSpeedAttribute.AllowsModifier())
        {
            failureReason = "이동/공격 속도 Attribute 구성이 올바르지 않습니다.";
            return false;
        }

        failureReason = null;
        return true;
    }

    public override ILevelRewardEffectHandle Apply(LevelRewardApplyContext context)
    {
        AttributeSet attributes = context.Player != null ? context.Player.GetComponent<AttributeSet>() : null;
        if (attributes == null) return null;

        RunTimeLimitSystem boundTimer = null;
        bool isApplied = false;

        void SetApplied(bool shouldApply)
        {
            if (isApplied == shouldApply) return;
            attributes.RemoveModifiersFromSource(this);
            isApplied = false;
            if (!shouldApply) return;

            bool moveAdded = attributes.TryAddModifier(
                moveSpeedAttribute,
                new AttributeModifier(ModifierType.Percent, moveSpeedPercent, this));
            bool attackAdded = attributes.TryAddModifier(
                attackSpeedAttribute,
                new AttributeModifier(ModifierType.Percent, attackSpeedPercent, this));
            isApplied = moveAdded && attackAdded;
            if (!isApplied)
                attributes.RemoveModifiersFromSource(this);
        }

        void HandleRemaining(float remaining)
        {
            SetApplied(remaining > 0f && remaining <= Mathf.Max(0f, thresholdSeconds));
        }

        void BindTimer(RunTimeLimitSystem timer)
        {
            if (boundTimer != null)
                boundTimer.OnRemainingTimeChanged -= HandleRemaining;

            boundTimer = timer;
            if (boundTimer != null)
            {
                boundTimer.OnRemainingTimeChanged += HandleRemaining;
                HandleRemaining(boundTimer.RemainingSeconds);
            }
            else
            {
                SetApplied(false);
            }
        }

        RunTimeLimitSystem.InstanceChanged += BindTimer;
        BindTimer(RunTimeLimitSystem.Instance);
        return new LevelRewardEffectHandle(() =>
        {
            RunTimeLimitSystem.InstanceChanged -= BindTimer;
            if (boundTimer != null)
                boundTimer.OnRemainingTimeChanged -= HandleRemaining;
            attributes.RemoveModifiersFromSource(this);
        });
    }
}
