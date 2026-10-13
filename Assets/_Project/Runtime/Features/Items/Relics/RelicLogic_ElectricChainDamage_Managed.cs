using System;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(menuName = "Game/Relic Logic/Electric Chain Damage (Managed)")]
public sealed class RelicLogic_ElectricChainDamage_Managed : RelicLogic
{
    public float bonusPerChainStep = 0.2f;
    public float maxChainBonus = 0.6f;

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
        manager.Register(new ChainDamageProc(ctx, new ElectricDischargeRelicBonus(bonusPerChainStep, maxChainBonus)));
    }
    public override RelicTooltipData BuildTooltip(RelicDefinition definition, int previewLevel, ItemDetailContext ctx)
    {
        return new RelicTooltipData { effectText = RelicTooltipFormatter.ReplaceTokens(
            GameText.Get("relic.electric.conductor.template", "● 연쇄될 때마다 연쇄 피해 {step_bonus} 증가\n● 최대 {max_bonus} 증가"),
            new Dictionary<string, string>
            {
                ["step_bonus"] = RelicTooltipFormatter.FormatUnsignedValueToken(Mathf.Max(0f, bonusPerChainStep), true),
                ["max_bonus"] = RelicTooltipFormatter.FormatUnsignedValueToken(Mathf.Max(0f, maxChainBonus), true)
            }) };
    }
    private sealed class ChainDamageProc : IRelicProc
    {
        private readonly IDisposable registration;
        public UnityEngine.Object Token { get; }
        public ChainDamageProc(RelicContext ctx, ElectricDischargeRelicBonus value)
        {
            Token = ctx.token;
            registration = ElectricDischargeRelicBonuses.Register(instigator =>
                ctx.owner != null && instigator == ctx.owner ? value : default);
        }
        public void Handle(GameplayTag tag, AbilityEventData data) { }
        public void Tick(float deltaTime) { }
        public void Dispose() => registration.Dispose();
    }
}
