using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(menuName = "Game/Relic Logic/Crit Chance On Non-Critical Hit (Managed)")]
public sealed class RelicLogic_CritChanceOnNonCriticalHit_Managed : RelicLogic
{
    [SerializeField] private GameplayTag hitConfirmTag;
    [SerializeField] private AttributeDefinition critChanceAddAttribute;
    [SerializeField] private List<float> chancePerStackByLevel = new();
    [SerializeField, Min(1)] private int maximumStacks = 8;

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
        float chancePerStack = EvaluateChancePerStack(previewLevel);
        float maximumChance = chancePerStack * Mathf.Max(1, maximumStacks);
        return BuildTemplatedTooltip(
            "● 치명타가 아닌 공격 적중 시 [[치명타 확률]] {chance_per_stack}\n● 최대 {maximum_stacks}회 중첩, 총 {maximum_chance}\n● 치명타 발생 시 {neg:누적 보너스 초기화}",
            new Dictionary<string, string>
            {
                ["chance_per_stack"] = RelicTooltipFormatter.FormatSignedValueToken(chancePerStack, true),
                ["maximum_stacks"] = RelicTooltipFormatter.FormatUnsignedValueToken(Mathf.Max(1, maximumStacks), false),
                ["maximum_chance"] = RelicTooltipFormatter.FormatSignedValueToken(maximumChance, true)
            });
    }

    private void Register(RelicContext ctx)
    {
        if (ctx.owner == null || ctx.token == null || ctx.attributeSet == null)
            return;

        if (hitConfirmTag == null || critChanceAddAttribute == null)
            return;

        RelicProcManager manager = ctx.owner.GetComponent<RelicProcManager>();
        if (manager == null)
            manager = ctx.owner.AddComponent<RelicProcManager>();

        manager.Register(new CritChanceOnNonCriticalHitProc(
            ctx,
            hitConfirmTag,
            critChanceAddAttribute,
            EvaluateChancePerStack(ctx.level),
            Mathf.Max(1, maximumStacks)));
    }

    private float EvaluateChancePerStack(int level)
    {
        if (chancePerStackByLevel == null || chancePerStackByLevel.Count == 0)
            return 0f;

        return Mathf.Max(0f, chancePerStackByLevel[Mathf.Clamp(level - 1, 0, chancePerStackByLevel.Count - 1)]);
    }

    private sealed class CritChanceOnNonCriticalHitProc : IRelicProc
    {
        public Object Token { get; }

        private readonly RelicContext ctx;
        private readonly GameplayTag hitConfirmTag;
        private readonly AttributeDefinition critChanceAttribute;
        private readonly float chancePerStack;
        private readonly int maximumStacks;
        private int currentStacks;

        public CritChanceOnNonCriticalHitProc(
            RelicContext ctx,
            GameplayTag hitConfirmTag,
            AttributeDefinition critChanceAttribute,
            float chancePerStack,
            int maximumStacks)
        {
            this.ctx = ctx;
            Token = ctx.token;
            this.hitConfirmTag = hitConfirmTag;
            this.critChanceAttribute = critChanceAttribute;
            this.chancePerStack = Mathf.Max(0f, chancePerStack);
            this.maximumStacks = Mathf.Max(1, maximumStacks);
        }

        public void Handle(GameplayTag tag, AbilityEventData data)
        {
            if (tag != hitConfirmTag)
                return;

            if (ctx.abilitySystem != null && data.AbilitySystem != ctx.abilitySystem)
                return;

            if (data.IsCriticalHit)
            {
                ResetStacks();
                return;
            }

            if (currentStacks >= maximumStacks || chancePerStack <= 0f)
                return;

            currentStacks++;
            ApplyCurrentBonus();
        }

        public void Tick(float deltaTime)
        {
        }

        public void Dispose()
        {
            if (ctx.attributeSet != null)
                ctx.attributeSet.RemoveModifiersFromSource(Token);
        }

        private void ResetStacks()
        {
            if (currentStacks <= 0)
                return;

            currentStacks = 0;
            if (ctx.attributeSet != null)
                ctx.attributeSet.RemoveModifiersFromSource(Token);
        }

        private void ApplyCurrentBonus()
        {
            if (ctx.attributeSet == null || critChanceAttribute == null)
                return;

            ctx.attributeSet.RemoveModifiersFromSource(Token);
            ctx.attributeSet.TryAddModifier(
                critChanceAttribute,
                new AttributeModifier(
                    ModifierType.Flat,
                    currentStacks * chancePerStack,
                    Token,
                    duration: 0f));
        }
    }
}
