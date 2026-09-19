using System;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(fileName = "LevelRewardEffect_UnextinguishedFire", menuName = "Game/Progression/Level Reward Effects/Unextinguished Fire")]
public sealed class UnextinguishedFireLevelRewardEffectSO : LevelRewardEffectSO
{
    [SerializeField] private StatusHudDefinition curseStatus;
    [SerializeField] private StatusHudDefinition completedStatus;
    [SerializeField, Min(1)] private int requiredBurnKills = 10;
    [SerializeField, Range(0f, 1f)] private float directDamageMultiplier = 0.85f;
    [SerializeField] private int completedBurnApplicationBonus = 1;
    [SerializeField, Min(0f)] private float completedBurnDamageMultiplier = 1.25f;

    [Serializable]
    private sealed class State
    {
        public int burnKillCount;
        public bool completed;
    }

    public override LevelRewardEffectLifetime Lifetime => LevelRewardEffectLifetime.Persistent;

    public override bool CanApply(LevelRewardEligibilityContext context, out string failureReason)
    {
        if (!base.CanApply(context, out failureReason))
            return false;

        if (context.Player.GetComponent<AbilitySystem>() == null ||
            context.Player.GetComponent<WeaponInventory2D>() == null)
        {
            failureReason = "플레이어 무기/능력 구성이 없습니다.";
            return false;
        }

        if (curseStatus == null || completedStatus == null)
        {
            failureReason = "꺼지지 않는 불 상태 HUD 구성이 없습니다.";
            return false;
        }

        failureReason = null;
        return true;
    }

    public override ILevelRewardEffectHandle Apply(LevelRewardApplyContext context)
    {
        PlayerInteractor2D player = context.Player;
        GameObject playerObject = player != null ? player.gameObject : null;
        AbilitySystem abilities = playerObject != null ? playerObject.GetComponent<AbilitySystem>() : null;
        WeaponInventory2D inventory = playerObject != null ? playerObject.GetComponent<WeaponInventory2D>() : null;
        BurnSourceRuntime burnRuntime = abilities != null ? BurnSourceRuntime.Resolve(abilities) : null;
        PlayerStatusRuntime statusRuntime = playerObject != null ? PlayerStatusRuntime.GetOrAdd(playerObject) : null;
        if (playerObject == null || abilities == null || inventory == null || burnRuntime == null || statusRuntime == null)
            return null;

        State state = string.IsNullOrWhiteSpace(context.EffectState.json)
            ? new State()
            : JsonUtility.FromJson<State>(context.EffectState.json) ?? new State();
        state.burnKillCount = Mathf.Clamp(state.burnKillCount, 0, Mathf.Max(1, requiredBurnKills));

        object burnModifierSource = new object();
        IDisposable directDamageModifier = null;
        bool subscribedToBurnKills = false;
        StatusHandle statusHandle = default;
        StatusHudDefinition displayedStatus = null;

        void SaveState()
        {
            context.EffectState.json = JsonUtility.ToJson(state);
        }

        StatusApplyRequest BuildStatusRequest()
        {
            int target = Mathf.Max(1, requiredBurnKills);
            string effectText = state.completed
                ? $"저주 해제\n화상 피해 +{(Mathf.Max(0f, completedBurnDamageMultiplier) - 1f) * 100f:0.#}%\n화상 부여량 +{completedBurnApplicationBonus}"
                : $"직접 피해 -{(1f - Mathf.Clamp01(directDamageMultiplier)) * 100f:0.#}%\n화상 피해로 처치: {state.burnKillCount}/{target}";
            return new StatusApplyRequest(
                state.completed ? completedStatus : curseStatus,
                ownerKey: "level_reward.unextinguished_fire",
                effectTextOverride: effectText,
                showStacksOverride: false,
                showDurationOverride: false,
                progressText: state.completed ? null : $"{state.burnKillCount}/{target}");
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

        void ApplyCompletedBonus()
        {
            burnRuntime.SetModifier(
                burnModifierSource,
                new BurnSourceRuntime.Modifier(
                    tickIntervalMultiplier: 1f,
                    damageRatioAdd: 0f,
                    applicationAdd: completedBurnApplicationBonus,
                    firstApplicationAdd: 0,
                    allowCritical: false,
                    damageRatioMultiplier: completedBurnDamageMultiplier));
        }

        void RemoveCursePenalty()
        {
            directDamageModifier?.Dispose();
            directDamageModifier = null;
        }

        void HandleBurnKill(BurnKillContext burnKill)
        {
            if (state.completed || burnKill.SourceSystem != abilities)
                return;

            state.burnKillCount = Mathf.Min(Mathf.Max(1, requiredBurnKills), state.burnKillCount + 1);
            if (state.burnKillCount >= Mathf.Max(1, requiredBurnKills))
            {
                state.completed = true;
                RemoveCursePenalty();
                if (subscribedToBurnKills)
                {
                    BurnStatus2D.BurnKillConfirmed -= HandleBurnKill;
                    subscribedToBurnKills = false;
                }
                ApplyCompletedBonus();
                SaveState();
                RefreshStatus(forceReplace: true);
                return;
            }

            SaveState();
            RefreshStatus();
        }

        if (state.completed)
        {
            ApplyCompletedBonus();
        }
        else
        {
            directDamageModifier = CombatOutgoingDamageModifiers.Register(damageContext =>
                LevelRewardDirectDamageUtility.IsDirectWeaponDamage(damageContext, abilities, inventory)
                    ? damageContext.BaseDamage * Mathf.Clamp01(directDamageMultiplier)
                    : damageContext.BaseDamage);
            BurnStatus2D.BurnKillConfirmed += HandleBurnKill;
            subscribedToBurnKills = true;
        }

        SaveState();
        RefreshStatus();
        return new LevelRewardEffectHandle(() =>
        {
            RemoveCursePenalty();
            if (subscribedToBurnKills)
                BurnStatus2D.BurnKillConfirmed -= HandleBurnKill;
            burnRuntime.RemoveModifier(burnModifierSource);
            statusHandle.Release();
        });
    }
}
