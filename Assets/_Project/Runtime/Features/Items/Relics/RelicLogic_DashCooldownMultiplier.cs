using System;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;
using Object = UnityEngine.Object;

[CreateAssetMenu(menuName = "Game/Relic Logic/Dash Cooldown Multiplier")]
public sealed class RelicLogic_DashCooldownMultiplier : RelicLogic
{
    [SerializeField] private AbilityDefinition dashAbility;
    [SerializeField] private List<float> cooldownReductionByLevel = new();

    protected override string DefaultEffectTemplate => "대쉬 재사용 대기시간이 {reduction} 감소";

    public override void OnEquipped(RelicContext ctx) => Register(ctx);
    public override void OnRestoreAttached(RelicContext ctx) => Register(ctx);

    public override void OnUnequipped(RelicContext ctx)
    {
        if (ctx.owner == null || ctx.token == null)
            return;

        ctx.owner.GetComponent<RelicProcManager>()?.UnregisterAll(ctx.token);
    }

    public override void OnRestoreDetached(RelicContext ctx) => OnUnequipped(ctx);

    public override RelicTooltipData BuildTooltip(RelicDefinition definition, int previewLevel, ItemDetailContext ctx)
    {
        return BuildTemplatedTooltip(
            DefaultEffectTemplate,
            new Dictionary<string, string>
            {
                ["reduction"] = RelicTooltipFormatter.FormatUnsignedValueToken(
                    EvaluateCooldownReduction(previewLevel),
                    true)
            });
    }

    private void Register(RelicContext ctx)
    {
        if (ctx.owner == null || ctx.token == null || ctx.abilitySystem == null || dashAbility == null)
            return;

        float multiplier = Mathf.Clamp01(1f - EvaluateCooldownReduction(ctx.level));
        IDisposable handle = ctx.abilitySystem.AddScopedCooldownDurationMultiplier(
            ability => ability == dashAbility,
            multiplier);
        if (handle == null)
            return;

        RelicProcManager manager = ctx.owner.GetComponent<RelicProcManager>();
        if (manager == null)
            manager = ctx.owner.AddComponent<RelicProcManager>();

        manager.Register(new DashCooldownMultiplierProc(ctx.token, handle));
    }

    private float EvaluateCooldownReduction(int level)
    {
        if (cooldownReductionByLevel == null || cooldownReductionByLevel.Count == 0)
            return 0f;

        return Mathf.Clamp01(
            cooldownReductionByLevel[Mathf.Clamp(level - 1, 0, cooldownReductionByLevel.Count - 1)]);
    }

    private sealed class DashCooldownMultiplierProc : IRelicProc
    {
        private IDisposable cooldownMultiplier;

        public DashCooldownMultiplierProc(Object token, IDisposable cooldownMultiplier)
        {
            Token = token;
            this.cooldownMultiplier = cooldownMultiplier;
        }

        public Object Token { get; }

        public void Handle(GameplayTag tag, AbilityEventData data)
        {
        }

        public void Tick(float deltaTime)
        {
        }

        public void Dispose()
        {
            cooldownMultiplier?.Dispose();
            cooldownMultiplier = null;
        }
    }
}
