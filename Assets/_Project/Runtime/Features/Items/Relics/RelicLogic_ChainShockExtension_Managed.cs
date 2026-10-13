using System;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(menuName = "Game/Relic Logic/Chain Shock Extension (Managed)")]
public sealed class RelicLogic_ChainShockExtension_Managed : RelicLogic
{
    public GameplayEffect electrocutedStatusEffect;
    public List<float> extensionSecondsByLevel = new() { 0.5f, 1f, 1.5f };

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
        manager.Register(new ExtensionProc(ctx, Evaluate(ctx.level)));
    }

    private float Evaluate(int level) => extensionSecondsByLevel == null || extensionSecondsByLevel.Count == 0
        ? 0f : Mathf.Max(0f, extensionSecondsByLevel[Mathf.Clamp(level - 1, 0, extensionSecondsByLevel.Count - 1)]);

    public override RelicTooltipData BuildTooltip(RelicDefinition definition, int previewLevel, ItemDetailContext ctx)
    {
        string template = GameText.Get("relic.electric.extension.template",
            "● 연쇄에 맞은 적의 [[감전]] 지속시간 {extension} 증가\n● 최대 {cap}까지 증가");
        return new RelicTooltipData { effectText = RelicTooltipFormatter.ReplaceTokens(template,
            new Dictionary<string, string>
            {
                ["extension"] = RelicTooltipFormatter.FormatSeconds(Evaluate(previewLevel)),
                ["cap"] = RelicTooltipFormatter.FormatSeconds(electrocutedStatusEffect != null ? electrocutedStatusEffect.duration : 4f)
            }) };
    }

    private sealed class ExtensionProc : IRelicProc
    {
        private readonly IDisposable registration;
        public UnityEngine.Object Token { get; }
        public ExtensionProc(RelicContext ctx, float seconds)
        {
            Token = ctx.token;
            registration = ElectricChainDurationBonuses.Register(instigator =>
                ctx.owner != null && instigator == ctx.owner ? seconds : 0f);
        }
        public void Handle(GameplayTag tag, AbilityEventData data) { }
        public void Tick(float deltaTime) { }
        public void Dispose() => registration.Dispose();
    }
}
