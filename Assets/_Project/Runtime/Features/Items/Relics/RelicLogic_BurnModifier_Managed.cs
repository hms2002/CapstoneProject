using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Relic Logic/Burn Modifier (Managed)")]
public sealed class RelicLogic_BurnModifier_Managed : RelicLogic
{
    [Header("Leveled Burn Rules")]
    [SerializeField] private List<float> tickIntervalMultipliers = new();
    [SerializeField] private List<float> damageRatioAdds = new();
    [SerializeField] private List<int> applicationAdds = new();
    [SerializeField] private List<int> firstApplicationAdds = new();
    [SerializeField] private bool allowCritical;

    [Header("Stack-based Burn Damage")]
    [SerializeField] private List<int> stackDamageThresholds = new();
    [SerializeField] private List<float> stackDamageRatiosPerStep = new();
    [SerializeField] private float stackDamageRatioMax;

    public override void OnEquipped(RelicContext ctx) => Apply(ctx);

    public override void OnUnequipped(RelicContext ctx)
    {
        if (ctx.abilitySystem == null || ctx.token == null)
            return;

        BurnSourceRuntime runtime = ctx.abilitySystem.GetComponent<BurnSourceRuntime>();
        runtime?.RemoveModifier(ctx.token);
    }

    public override void OnRestoreAttached(RelicContext ctx) => Apply(ctx);

    public override void OnRestoreDetached(RelicContext ctx) => OnUnequipped(ctx);

    private void Apply(RelicContext ctx)
    {
        if (ctx.abilitySystem == null || ctx.token == null)
            return;

        int level = Mathf.Max(1, ctx.level);
        BurnSourceRuntime runtime = BurnSourceRuntime.Resolve(ctx.abilitySystem);
        runtime?.SetModifier(ctx.token, new BurnSourceRuntime.Modifier(
            tickIntervalMultiplier: Evaluate(tickIntervalMultipliers, level, 1f),
            damageRatioAdd: Evaluate(damageRatioAdds, level, 0f),
            applicationAdd: Evaluate(applicationAdds, level, 0),
            firstApplicationAdd: Evaluate(firstApplicationAdds, level, 0),
            allowCritical: allowCritical,
            stackDamageThreshold: Evaluate(stackDamageThresholds, level, 0),
            stackDamageRatioPerStep: Evaluate(stackDamageRatiosPerStep, level, 0f),
            stackDamageRatioMax: Mathf.Max(0f, stackDamageRatioMax)));
    }

    public override RelicTooltipData BuildTooltip(RelicDefinition definition, int previewLevel, ItemDetailContext ctx)
    {
        int level = Mathf.Max(1, previewLevel);
        var lines = new List<string>();

        float intervalMultiplier = Evaluate(tickIntervalMultipliers, level, 1f);
        if (!Mathf.Approximately(intervalMultiplier, 1f))
            lines.Add($"● [[화상]] 소모 주기 감소 {(1f - intervalMultiplier) * 100f:0}% (주기 {intervalMultiplier:0.##}초)");

        float damageAdd = Evaluate(damageRatioAdds, level, 0f);
        if (damageAdd > 0f)
            lines.Add($"● [[화상 피해]] 계수 +{damageAdd * 100f:0}%p");

        int applicationAdd = Evaluate(applicationAdds, level, 0);
        if (applicationAdd > 0)
            lines.Add($"● [[화상]] 부여량 +{applicationAdd}");

        int firstApplicationAdd = Evaluate(firstApplicationAdds, level, 0);
        if (firstApplicationAdd > 0)
            lines.Add($"● 비화상 대상 첫 [[화상]] 부여량 +{firstApplicationAdd}");

        if (allowCritical)
            lines.Add("● [[화상 피해]]에 치명타 확률과 치명타 피해 적용");

        int threshold = Evaluate(stackDamageThresholds, level, 0);
        float ratioPerStep = Evaluate(stackDamageRatiosPerStep, level, 0f);
        if (threshold > 0 && ratioPerStep > 0f)
            lines.Add($"● 적의 [[화상]] {threshold}마다 [[화상 피해]] +{ratioPerStep * 100f:0}% (최대 {stackDamageRatioMax * 100f:0}%)");

        return new RelicTooltipData { effectText = string.Join("\n", lines) };
    }

    private static float Evaluate(List<float> values, int level, float fallback)
    {
        if (values == null || values.Count == 0)
            return fallback;
        return values[Mathf.Clamp(level - 1, 0, values.Count - 1)];
    }

    private static int Evaluate(List<int> values, int level, int fallback)
    {
        if (values == null || values.Count == 0)
            return fallback;
        return values[Mathf.Clamp(level - 1, 0, values.Count - 1)];
    }
}
