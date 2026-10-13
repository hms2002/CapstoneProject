using System;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(menuName = "Game/Relic Logic/Shock Paralysis (Managed)")]
public sealed class RelicLogic_ShockParalysis_Managed : RelicLogic
{
    public float paralysisSeconds = 0.1f;
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
        manager.Register(new ParalysisProc(ctx, Mathf.Max(0f, paralysisSeconds)));
    }
    public override RelicTooltipData BuildTooltip(RelicDefinition definition, int previewLevel, ItemDetailContext ctx)
        => new() { effectText = RelicTooltipFormatter.ReplaceTokens(
            GameText.Get("relic.electric.paralysis.template",
                "● [[감전]] 부여 시 {duration} 동안 마비\n● 보스에게는 적용되지 않음"),
            new Dictionary<string, string> { ["duration"] = RelicTooltipFormatter.FormatSeconds(Mathf.Max(0f, paralysisSeconds)) }) };
    private sealed class ParalysisProc : IRelicProc
    {
        private readonly IDisposable registration;
        public UnityEngine.Object Token { get; }
        public ParalysisProc(RelicContext ctx, float seconds)
        {
            Token = ctx.token;
            registration = ElectricShockApplications.Register(application =>
            {
                if (ctx.owner == null || application.Instigator != ctx.owner || application.Target == null) return;
                var enemy = application.Target.GetComponent<Enemy>();
                if (enemy == null || !enemy.isActiveAndEnabled || enemy.IsDead) return;
                CombatHitPause2D.ApplyParalysis(application.Target, seconds);
            });
        }
        public void Handle(GameplayTag tag, AbilityEventData data) { }
        public void Tick(float deltaTime) { }
        public void Dispose() => registration.Dispose();
    }
}
