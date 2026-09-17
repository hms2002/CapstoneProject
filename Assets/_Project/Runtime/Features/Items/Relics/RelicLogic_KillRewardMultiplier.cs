using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Relic Logic/Kill Reward Multiplier")]
public sealed class RelicLogic_KillRewardMultiplier : RelicLogic, IRelicRuntimeStateSerializer
{
    public const string StateTypeKey = "RelicKillRewardMultiplier";

    [SerializeField] private KillRewardRelicKind rewardKind;
    [SerializeField] private List<float> bonusRateByLevel = new();

    protected override string DefaultEffectTemplate => rewardKind == KillRewardRelicKind.Gold
        ? "적 처치 시 얻는 금화의 획득량이 {bonus} 증가"
        : "경험치 획득량이 {bonus} 증가";

    public override void OnEquipped(RelicContext ctx) => Attach(ctx);
    public override void OnRestoreAttached(RelicContext ctx) => Attach(ctx);
    public override void OnUnequipped(RelicContext ctx) => Detach(ctx);
    public override void OnRestoreDetached(RelicContext ctx) => Detach(ctx);

    public override RelicTooltipData BuildTooltip(RelicDefinition definition, int previewLevel, ItemDetailContext ctx)
    {
        return BuildTemplatedTooltip(
            DefaultEffectTemplate,
            new Dictionary<string, string>
            {
                ["bonus"] = RelicTooltipFormatter.FormatUnsignedValueToken(EvaluateBonusRate(previewLevel), true)
            });
    }

    public bool TryCaptureRuntimeState(
        RelicContext ctx,
        RelicRuntimeStateHub hub,
        int slotIndex,
        out RelicRuntimeState state)
    {
        state = null;
        if (ctx.owner == null || ctx.token == null || ctx.relicDef == null)
            return false;

        RelicKillRewardMultiplierRuntime runtime = ctx.owner.GetComponent<RelicKillRewardMultiplierRuntime>();
        if (runtime == null || !runtime.TryGetFractionalRemainder(ctx.token, out float remainder))
            return false;

        state = new RelicRuntimeState
        {
            slotIndex = slotIndex,
            relicId = ctx.relicDef.relicId,
            level = Mathf.Max(1, ctx.level),
            stateType = StateTypeKey,
            json = JsonUtility.ToJson(new KillRewardMultiplierRuntimePayload
            {
                fractionalRemainder = remainder
            })
        };
        return true;
    }

    public void RestoreRuntimeState(RelicContext ctx, RelicRuntimeState state, RelicRuntimeStateHub hub)
    {
        if (ctx.owner == null || ctx.token == null || state == null ||
            !string.Equals(state.stateType, StateTypeKey, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(state.json))
        {
            return;
        }

        KillRewardMultiplierRuntimePayload payload =
            JsonUtility.FromJson<KillRewardMultiplierRuntimePayload>(state.json);
        ctx.owner.GetComponent<RelicKillRewardMultiplierRuntime>()?
            .RestoreFractionalRemainder(ctx.token, payload != null ? payload.fractionalRemainder : 0f);
    }

    private void Attach(RelicContext ctx)
    {
        if (ctx.owner == null || ctx.token == null)
            return;

        RelicKillRewardMultiplierRuntime runtime = ctx.owner.GetComponent<RelicKillRewardMultiplierRuntime>();
        if (runtime == null)
            runtime = ctx.owner.AddComponent<RelicKillRewardMultiplierRuntime>();

        runtime.Register(ctx.token, rewardKind, EvaluateBonusRate(ctx.level));
    }

    private void Detach(RelicContext ctx)
    {
        if (ctx.owner == null || ctx.token == null)
            return;

        ctx.owner.GetComponent<RelicKillRewardMultiplierRuntime>()?.Unregister(ctx.token);
    }

    private float EvaluateBonusRate(int level)
    {
        if (bonusRateByLevel == null || bonusRateByLevel.Count == 0)
            return 0f;

        return Mathf.Max(0f, bonusRateByLevel[Mathf.Clamp(level - 1, 0, bonusRateByLevel.Count - 1)]);
    }
}

[Serializable]
public sealed class KillRewardMultiplierRuntimePayload
{
    public float fractionalRemainder;
}
