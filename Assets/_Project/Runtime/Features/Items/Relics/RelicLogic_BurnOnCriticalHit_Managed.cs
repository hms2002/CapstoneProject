using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(menuName = "Game/Relic Logic/Burn On Critical Hit (Managed)")]
public sealed class RelicLogic_BurnOnCriticalHit_Managed : RelicLogic
{
    [SerializeField] private GameplayTag hitConfirmTag;
    [SerializeField] private GameplayEffect burnDamageEffect;
    [SerializeField] private List<int> burnStacksByLevel = new();
    [SerializeField, Min(0f)] private float cooldownSeconds = 0.5f;

    public override void OnEquipped(RelicContext ctx) => Register(ctx);

    public override void OnRestoreAttached(RelicContext ctx) => Register(ctx);

    public override void OnUnequipped(RelicContext ctx)
    {
        if (ctx.owner == null || ctx.token == null)
            return;

        ctx.owner.GetComponent<RelicProcManager>()?.UnregisterAll(ctx.token);
    }

    public override RelicTooltipData BuildTooltip(RelicDefinition definition, int previewLevel, ItemDetailContext ctx)
    {
        return BuildTemplatedTooltip(
            "● [[화상]] 상태인 적에게 [[치명타]] 적중 시 [[화상]] {burn_stacks}중첩 부여\n● 재사용 대기시간 {cooldown}",
            new Dictionary<string, string>
            {
                ["burn_stacks"] = RelicTooltipFormatter.FormatUnsignedValueToken(EvaluateBurnStacks(previewLevel), false),
                ["cooldown"] = RelicTooltipFormatter.FormatSeconds(cooldownSeconds)
            });
    }

    private void Register(RelicContext ctx)
    {
        if (ctx.owner == null || ctx.token == null || ctx.abilitySystem == null)
            return;

        if (hitConfirmTag == null || burnDamageEffect == null)
            return;

        RelicProcManager manager = ctx.owner.GetComponent<RelicProcManager>();
        if (manager == null)
            manager = ctx.owner.AddComponent<RelicProcManager>();

        manager.Register(new BurnOnCriticalHitProc(
            ctx,
            hitConfirmTag,
            burnDamageEffect,
            EvaluateBurnStacks(ctx.level),
            Mathf.Max(0f, cooldownSeconds)));
    }

    private int EvaluateBurnStacks(int level)
    {
        if (burnStacksByLevel == null || burnStacksByLevel.Count == 0)
            return 0;

        return Mathf.Max(0, burnStacksByLevel[Mathf.Clamp(level - 1, 0, burnStacksByLevel.Count - 1)]);
    }

    private sealed class BurnOnCriticalHitProc : IRelicProc
    {
        public Object Token { get; }

        private readonly RelicContext ctx;
        private readonly GameplayTag hitConfirmTag;
        private readonly GameplayEffect burnDamageEffect;
        private readonly int burnStacks;
        private readonly float cooldownSeconds;
        private float nextAvailableTime;

        public BurnOnCriticalHitProc(
            RelicContext ctx,
            GameplayTag hitConfirmTag,
            GameplayEffect burnDamageEffect,
            int burnStacks,
            float cooldownSeconds)
        {
            this.ctx = ctx;
            Token = ctx.token;
            this.hitConfirmTag = hitConfirmTag;
            this.burnDamageEffect = burnDamageEffect;
            this.burnStacks = Mathf.Max(0, burnStacks);
            this.cooldownSeconds = Mathf.Max(0f, cooldownSeconds);
        }

        public void Handle(GameplayTag tag, AbilityEventData data)
        {
            if (tag != hitConfirmTag || !data.IsCriticalHit || data.Target == null)
                return;

            if (ctx.abilitySystem != null && data.AbilitySystem != ctx.abilitySystem)
                return;

            if (burnStacks <= 0 || Time.time < nextAvailableTime)
                return;

            BurnStatus2D currentBurn = data.Target.GetComponent<BurnStatus2D>();
            if (currentBurn == null || currentBurn.CurrentStacks <= 0)
                return;

            BurnStatus2D.Apply(
                data.Target,
                ctx.abilitySystem,
                burnDamageEffect,
                ctx.owner,
                burnStacks);
            nextAvailableTime = Time.time + cooldownSeconds;
        }

        public void Tick(float deltaTime)
        {
        }

        public void Dispose()
        {
        }
    }
}
