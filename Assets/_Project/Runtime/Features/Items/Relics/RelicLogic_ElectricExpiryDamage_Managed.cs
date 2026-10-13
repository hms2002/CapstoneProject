using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>자연 종료된 감전의 적용자를 확인하고, 종료 시점의 전기 속성으로 추가 피해를 계산한다.</summary>
[CreateAssetMenu(menuName = "Game/Relic Logic/Electric Expiry Damage (Managed)")]
public sealed class RelicLogic_ElectricExpiryDamage_Managed : RelicLogic
{
    public GameplayEffect electrocutedStatusEffect;
    public GE_ElectricShockTrigger electricTriggerEffect;
    [Min(0f)] public float electricCoefficient = 0.4f;

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
        if (ctx.owner == null || ctx.token == null)
            return;

        var manager = ctx.owner.GetComponent<RelicProcManager>();
        manager?.UnregisterAll(ctx.token);
        if (electrocutedStatusEffect == null || electricTriggerEffect == null)
            return;
        if (manager == null)
            manager = ctx.owner.AddComponent<RelicProcManager>();

        manager.Register(new ExpiryDamageProc(ctx, electrocutedStatusEffect, electricTriggerEffect,
            Mathf.Max(0f, electricCoefficient)));
    }

    public override RelicTooltipData BuildTooltip(RelicDefinition definition, int previewLevel, ItemDetailContext ctx)
    {
        string template = GameText.Get("relic.electric.expiry.template",
            "● 내가 부여한 [[감전]]의 지속시간이 끝나면 [[전기 속성]]의 {electric_ratio} 피해");
        return new RelicTooltipData
        {
            effectText = RelicTooltipFormatter.ReplaceTokens(template, new Dictionary<string, string>
            {
                ["electric_ratio"] = RelicTooltipFormatter.FormatUnsignedValueToken(Mathf.Max(0f, electricCoefficient), true)
            })
        };
    }

    private sealed class ExpiryDamageProc : IRelicProc
    {
        private readonly RelicContext ctx;
        private readonly GameplayEffect status;
        private readonly GE_ElectricShockTrigger trigger;
        private readonly float coefficient;
        private bool disposed;

        public Object Token => ctx.token;

        public ExpiryDamageProc(RelicContext ctx, GameplayEffect status, GE_ElectricShockTrigger trigger, float coefficient)
        {
            this.ctx = ctx;
            this.status = status;
            this.trigger = trigger;
            this.coefficient = coefficient;
            GameplayEffectRunner.NaturallyExpired += OnNaturallyExpired;
        }

        public void Handle(GameplayTag tag, AbilityEventData data) { }
        public void Tick(float deltaTime) { }

        private void OnNaturallyExpired(ActiveGameplayEffect expired)
        {
            if (disposed || expired == null || expired.Effect != status || expired.Instigator != ctx.owner ||
                !IsAlive(ctx.owner) || !IsAlive(expired.Target))
                return;

            float damage = GE_ElectricShockTrigger.ReadElectricAttribute(ctx.owner) * coefficient;
            if (damage <= 0f)
                return;
            trigger.ApplySecondaryElectricDamage(expired.Target, ctx.owner, expired.Causer, damage);
        }

        private static bool IsAlive(GameObject actor)
        {
            if (CombatHitPause2D.IsActionStoppedOn(actor))
                return false;
            var playerDeath = actor.GetComponent<PlayerDeathReturnToHub2D>();
            return playerDeath == null || !playerDeath.IsDeathSequenceRunning;
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            GameplayEffectRunner.NaturallyExpired -= OnNaturallyExpired;
        }
    }
}
