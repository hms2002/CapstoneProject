using System;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(menuName = "Game/Relic Logic/Electric Chain Targets (Managed)")]
public sealed class RelicLogic_ElectricChainTargets_Managed : RelicLogic
{
    public List<int> additionalTargetsByLevel = new() { 1, 2, 3 };
    public override void OnEquipped(RelicContext ctx) => Register(ctx);
    public override void OnRestoreAttached(RelicContext ctx) => Register(ctx);
    public override void OnRestoreDetached(RelicContext ctx) => OnUnequipped(ctx);
    public override void OnUnequipped(RelicContext ctx)
    {
        if (ctx.owner != null && ctx.token != null)
            ctx.owner.GetComponent<RelicProcManager>()?.UnregisterAll(ctx.token);
    }
    private void Register(RelicContext ctx)
    {
        if (ctx.owner == null || ctx.token == null) return;
        var manager = ctx.owner.GetComponent<RelicProcManager>();
        if (manager == null) manager = ctx.owner.AddComponent<RelicProcManager>();
        manager.UnregisterAll(ctx.token);
        int value = additionalTargetsByLevel == null || additionalTargetsByLevel.Count == 0 ? 0 :
            Mathf.Max(0, additionalTargetsByLevel[Mathf.Clamp(ctx.level - 1, 0, additionalTargetsByLevel.Count - 1)]);
        manager.Register(new TargetBonusProc(ctx, value));
    }
    public override RelicTooltipData BuildTooltip(RelicDefinition definition, int previewLevel, ItemDetailContext ctx)
    {
        int value = additionalTargetsByLevel == null || additionalTargetsByLevel.Count == 0 ? 0 :
            Mathf.Max(0, additionalTargetsByLevel[Mathf.Clamp(previewLevel - 1, 0, additionalTargetsByLevel.Count - 1)]);
        return new RelicTooltipData { effectText = RelicTooltipFormatter.ReplaceTokens(
            GameText.Get("relic.electric.targets.template", "● 전기 연쇄의 최대 대상 수 {value} 증가\n● 최초 대상 포함 기본 3명"),
            new Dictionary<string, string> { ["value"] = RelicTooltipFormatter.FormatSignedValueToken(value, false) }) };
    }
    private sealed class TargetBonusProc : IRelicProc
    {
        private readonly IDisposable registration;
        public UnityEngine.Object Token { get; }
        public TargetBonusProc(RelicContext ctx, int value)
        {
            Token = ctx.token;
            registration = ElectricChainTargetBonuses.Register(instigator =>
                ctx.owner != null && instigator == ctx.owner ? value : 0);
        }
        public void Handle(GameplayTag tag, AbilityEventData data) { }
        public void Tick(float deltaTime) { }
        public void Dispose() => registration.Dispose();
    }
}
