using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(menuName = "Game/Relic Logic/Last Stand Critical")]
public sealed class RelicLogic_LastStandCritical : RelicLogic
{
    [SerializeField] private AttributeDefinition healthAttribute;
    [SerializeField] private AttributeDefinition critChanceAddAttribute;
    [SerializeField] private List<float> healthThresholdByLevel = new();
    [SerializeField] private StatusHudDefinition statusDefinition;

    protected override string DefaultEffectTemplate =>
        "현재 체력이 {threshold} 이하일 경우 모든 공격이 반드시 치명타로 적중.";

    public override void OnEquipped(RelicContext ctx) => Register(ctx);
    public override void OnRestoreAttached(RelicContext ctx) => Register(ctx);

    public override void AppendPreviewModifiers(RelicContext ctx, AttributeDefinition attribute, List<AttributeModifier> results)
    {
        if (attribute != critChanceAddAttribute || healthAttribute == null || results == null) return;
        float health = ctx.ReadPreviewAttribute(healthAttribute);
        if (health > 0f && health <= EvaluateThreshold(ctx.level))
            results.Add(new AttributeModifier(ModifierType.Flat, 1f, ctx.token, 0f));
    }

    public override void OnUnequipped(RelicContext ctx)
    {
        if (ctx.owner == null || ctx.token == null)
            return;

        ctx.owner.GetComponent<RelicProcManager>()?.UnregisterAll(ctx.token);
    }

    public override void OnRestoreDetached(RelicContext ctx) => OnUnequipped(ctx);

    public override RelicTooltipData BuildTooltip(
        RelicDefinition definition,
        int previewLevel,
        ItemDetailContext ctx)
    {
        return BuildTemplatedTooltip(
            DefaultEffectTemplate,
            new Dictionary<string, string>
            {
                ["threshold"] = RelicTooltipFormatter.FormatUnsignedValueToken(
                    EvaluateThreshold(previewLevel),
                    false)
            });
    }

    private void Register(RelicContext ctx)
    {
        if (ctx.owner == null || ctx.token == null || ctx.attributeSet == null)
            return;

        if (healthAttribute == null || critChanceAddAttribute == null)
            return;

        RelicProcManager manager = ctx.owner.GetComponent<RelicProcManager>();
        if (manager == null)
            manager = ctx.owner.AddComponent<RelicProcManager>();

        manager.Register(new LastStandCriticalProc(
            ctx,
            healthAttribute,
            critChanceAddAttribute,
            EvaluateThreshold(ctx.level),
            statusDefinition));
    }

    private float EvaluateThreshold(int level)
    {
        if (healthThresholdByLevel == null || healthThresholdByLevel.Count == 0)
            return 0f;

        return Mathf.Max(
            0f,
            healthThresholdByLevel[Mathf.Clamp(level - 1, 0, healthThresholdByLevel.Count - 1)]);
    }

    private sealed class LastStandCriticalProc : IRelicProc
    {
        private readonly RelicContext context;
        private readonly AttributeDefinition healthAttribute;
        private readonly AttributeDefinition critChanceAddAttribute;
        private readonly float healthThreshold;
        private readonly StatusHudDefinition statusDefinition;
        private readonly PlayerStatusRuntime statusRuntime;
        private readonly string statusOwnerKey;

        private StatusHandle statusHandle;
        private bool isApplied;

        public LastStandCriticalProc(
            RelicContext context,
            AttributeDefinition healthAttribute,
            AttributeDefinition critChanceAddAttribute,
            float healthThreshold,
            StatusHudDefinition statusDefinition)
        {
            this.context = context;
            Token = context.token;
            this.healthAttribute = healthAttribute;
            this.critChanceAddAttribute = critChanceAddAttribute;
            this.healthThreshold = Mathf.Max(0f, healthThreshold);
            this.statusDefinition = statusDefinition;

            statusRuntime = context.owner != null
                ? PlayerStatusRuntime.GetOrAdd(context.owner)
                : null;
            string relicId = context.relicDef != null ? context.relicDef.relicId : "relic";
            int tokenId = Token != null ? Token.GetInstanceID() : 0;
            statusOwnerKey = $"relic.last_stand.{relicId}.{tokenId}";

            context.attributeSet.OnAttributeChanged += OnAttributeChanged;
            Refresh();
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
            if (context.attributeSet != null)
                context.attributeSet.OnAttributeChanged -= OnAttributeChanged;

            Deactivate();
        }

        private void OnAttributeChanged(
            AttributeDefinition definition,
            float oldValue,
            float newValue)
        {
            if (definition == healthAttribute)
                Refresh();
        }

        private void Refresh()
        {
            if (context.attributeSet == null)
                return;

            float currentHealth = context.attributeSet.GetCurrentValue(healthAttribute);
            bool shouldApply = currentHealth > 0f && currentHealth <= healthThreshold;
            if (shouldApply == isApplied)
                return;

            if (shouldApply)
                Activate();
            else
                Deactivate();
        }

        private void Activate()
        {
            if (context.attributeSet == null || critChanceAddAttribute == null)
                return;

            context.attributeSet.RemoveModifiersFromSource(Token);
            isApplied = context.attributeSet.TryAddModifier(
                critChanceAddAttribute,
                new AttributeModifier(
                    ModifierType.Flat,
                    1f,
                    Token,
                    duration: 0f));

            if (!isApplied || statusDefinition == null || statusRuntime == null)
                return;

            StatusApplyRequest request = new(
                statusDefinition,
                statusOwnerKey,
                isVisible: true,
                showStacksOverride: false,
                showDurationOverride: false);
            statusHandle = statusRuntime.Apply(request);
        }

        private void Deactivate()
        {
            if (context.attributeSet != null)
                context.attributeSet.RemoveModifiersFromSource(Token);

            if (statusHandle.IsValid)
                statusHandle.Release();

            statusHandle = default;
            isApplied = false;
        }
    }
}
