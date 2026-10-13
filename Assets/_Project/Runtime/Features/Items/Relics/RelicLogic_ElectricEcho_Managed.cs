using System;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(menuName = "Game/Relic Logic/Electric Echo (Managed)")]
public sealed class RelicLogic_ElectricEcho_Managed : RelicLogic
{
    public float damageRatio = 0.5f;

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
        manager.Register(new EchoProc(ctx, Mathf.Max(0f, damageRatio)));
    }
    public override RelicTooltipData BuildTooltip(RelicDefinition definition, int previewLevel, ItemDetailContext ctx)
    {
        return new RelicTooltipData { effectText = RelicTooltipFormatter.ReplaceTokens(
            GameText.Get("relic.electric.echo.template", "● 연쇄 번개를 한 번 더 반복하며, 피해는 {damage_ratio} 적용\n● 최초 대상은 제외하며 [[감전]] 지속시간은 늘어나지 않음"),
            new Dictionary<string, string>
            {
                ["damage_ratio"] = RelicTooltipFormatter.FormatUnsignedValueToken(Mathf.Max(0f, damageRatio), true)
            }) };
    }
    private sealed class EchoProc : IRelicProc
    {
        private readonly IDisposable registration;
        public UnityEngine.Object Token { get; }
        public EchoProc(RelicContext ctx, float ratio)
        {
            Token = ctx.token;
            registration = ElectricDischargeRelicBonuses.Register(instigator =>
                ctx.owner != null && instigator == ctx.owner ? new ElectricDischargeRelicBonus(echoDamageRatio: ratio) : default);
        }
        public void Handle(GameplayTag tag, AbilityEventData data) { }
        public void Tick(float deltaTime) { }
        public void Dispose() => registration.Dispose();
    }
}
