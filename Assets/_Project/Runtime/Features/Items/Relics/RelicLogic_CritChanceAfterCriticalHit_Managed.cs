using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(menuName = "Game/Relic Logic/Crit Chance After Critical Hit (Managed)")]
public sealed class RelicLogic_CritChanceAfterCriticalHit_Managed : RelicLogic
{
    [SerializeField] private GameplayTag hitConfirmTag;
    [SerializeField] private AttributeDefinition critChanceAddAttribute;
    [SerializeField] private List<float> chanceByLevel = new();
    [SerializeField, Min(0.01f)] private float durationSeconds = 3f;

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
            "● 치명타 적중 시 {duration} 동안 [[치명타 확률]] {chance} 증가\n● 치명타 적중 시 지속 시간 초기화",
            new Dictionary<string, string>
            {
                ["duration"] = RelicTooltipFormatter.FormatSeconds(durationSeconds),
                ["chance"] = RelicTooltipFormatter.FormatSignedValueToken(EvaluateChance(previewLevel), true)
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

        manager.Register(new CritChanceAfterCriticalHitProc(
            ctx,
            hitConfirmTag,
            critChanceAddAttribute,
            EvaluateChance(ctx.level),
            Mathf.Max(0.01f, durationSeconds)));
    }

    private float EvaluateChance(int level)
    {
        if (chanceByLevel == null || chanceByLevel.Count == 0)
            return 0f;

        return Mathf.Max(0f, chanceByLevel[Mathf.Clamp(level - 1, 0, chanceByLevel.Count - 1)]);
    }

    private sealed class CritChanceAfterCriticalHitProc : IRelicProc
    {
        public Object Token { get; }

        private readonly RelicContext ctx;
        private readonly GameplayTag hitConfirmTag;
        private readonly AttributeDefinition critChanceAttribute;
        private readonly float chance;
        private readonly float durationSeconds;

        public CritChanceAfterCriticalHitProc(
            RelicContext ctx,
            GameplayTag hitConfirmTag,
            AttributeDefinition critChanceAttribute,
            float chance,
            float durationSeconds)
        {
            this.ctx = ctx;
            Token = ctx.token;
            this.hitConfirmTag = hitConfirmTag;
            this.critChanceAttribute = critChanceAttribute;
            this.chance = Mathf.Max(0f, chance);
            this.durationSeconds = Mathf.Max(0.01f, durationSeconds);
        }

        public void Handle(GameplayTag tag, AbilityEventData data)
        {
            if (tag != hitConfirmTag || !data.IsCriticalHit)
                return;

            if (ctx.abilitySystem != null && data.AbilitySystem != ctx.abilitySystem)
                return;

            if (ctx.attributeSet == null || critChanceAttribute == null || chance <= 0f)
                return;

            ctx.attributeSet.RemoveModifiersFromSource(Token);
            ctx.attributeSet.TryAddModifier(
                critChanceAttribute,
                new AttributeModifier(
                    ModifierType.Flat,
                    chance,
                    Token,
                    duration: durationSeconds));
        }

        public void Tick(float deltaTime)
        {
        }

        public void Dispose()
        {
            if (ctx.attributeSet != null)
                ctx.attributeSet.RemoveModifiersFromSource(Token);
        }
    }
}
