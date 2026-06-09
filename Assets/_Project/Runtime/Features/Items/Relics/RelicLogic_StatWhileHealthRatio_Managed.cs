using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(menuName = "Game/Relic Logic/Stat While Health Ratio (Managed)")]
public sealed class RelicLogic_StatWhileHealthRatio_Managed : RelicLogic
{
    protected override string DefaultEffectTemplate => "체력 {min_ratio}~{max_ratio}일 때 [[{stat}]] {value}";

    [Header("Watch")]
    public AttributeDefinition healthAttribute;
    public AttributeDefinition maxHealthAttribute;
    [Range(0f, 1f)] public float minHealthRatioInclusive = 0f;
    [Range(0f, 1f)] public float maxHealthRatioInclusive = 1f;

    [Header("Apply To")]
    public AttributeDefinition attribute;
    public string displayNameOverride;
    public ModifierType modifierType = ModifierType.Flat;
    public float value;

    public override void OnEquipped(RelicContext ctx)
    {
        RegisterProc(ctx);
    }

    public override void OnUnequipped(RelicContext ctx)
    {
        if (ctx.owner == null || ctx.token == null)
            return;

        RelicProcManager manager = ctx.owner.GetComponent<RelicProcManager>();
        manager?.UnregisterAll(ctx.token);
    }

    public override void OnRestoreAttached(RelicContext ctx)
    {
        RegisterProc(ctx);
    }

    private void RegisterProc(RelicContext ctx)
    {
        if (ctx.owner == null || ctx.token == null || ctx.attributeSet == null)
            return;

        if (healthAttribute == null || maxHealthAttribute == null || attribute == null)
            return;

        RelicProcManager manager = ctx.owner.GetComponent<RelicProcManager>();
        if (manager == null)
            manager = ctx.owner.AddComponent<RelicProcManager>();

        manager.Register(new StatWhileHealthRatioProc(
            ctx,
            healthAttribute,
            maxHealthAttribute,
            minHealthRatioInclusive,
            maxHealthRatioInclusive,
            attribute,
            modifierType,
            value));
    }

    public override RelicTooltipData BuildTooltip(RelicDefinition definition, int previewLevel, ItemDetailContext ctx)
    {
        string displayName = ResolveDisplayName();
        return BuildTemplatedTooltip(
            DefaultEffectTemplate,
            new Dictionary<string, string>
            {
                ["min_ratio"] = FormatPercent01(Mathf.Clamp01(minHealthRatioInclusive)),
                ["max_ratio"] = FormatPercent01(Mathf.Clamp01(maxHealthRatioInclusive)),
                ["stat"] = displayName,
                ["value"] = RelicTooltipFormatter.FormatSignedValueToken(
                    value,
                    RelicTooltipFormatter.ShouldDisplayAsPercent(attribute, displayName, modifierType))
            });
    }

    private static string FormatPercent01(float ratio)
    {
        return $"{ratio * 100f:0.##}%";
    }

    private string ResolveDisplayName()
    {
        if (!string.IsNullOrWhiteSpace(displayNameOverride))
            return displayNameOverride;

        if (attribute == null)
            return string.Empty;

        return !string.IsNullOrEmpty(attribute.attributeName)
            ? attribute.attributeName
            : attribute.name;
    }

    private sealed class StatWhileHealthRatioProc : IRelicProc
    {
        public Object Token { get; }

        private readonly RelicContext ctx;
        private readonly AttributeDefinition healthAttribute;
        private readonly AttributeDefinition maxHealthAttribute;
        private readonly float minRatio;
        private readonly float maxRatio;
        private readonly AttributeDefinition attribute;
        private readonly ModifierType modifierType;
        private readonly float value;
        private bool isApplied;

        public StatWhileHealthRatioProc(
            RelicContext ctx,
            AttributeDefinition healthAttribute,
            AttributeDefinition maxHealthAttribute,
            float minRatio,
            float maxRatio,
            AttributeDefinition attribute,
            ModifierType modifierType,
            float value)
        {
            this.ctx = ctx;
            Token = ctx.token;
            this.healthAttribute = healthAttribute;
            this.maxHealthAttribute = maxHealthAttribute;
            this.minRatio = Mathf.Clamp01(Mathf.Min(minRatio, maxRatio));
            this.maxRatio = Mathf.Clamp01(Mathf.Max(minRatio, maxRatio));
            this.attribute = attribute;
            this.modifierType = modifierType;
            this.value = value;

            ctx.attributeSet.OnAttributeChanged += OnAttributeChanged;
            RecomputeAndApply();
        }

        public void Handle(GameplayTag tag, AbilityEventData data)
        {
        }

        public void Tick(float deltaTime)
        {
        }

        public void Dispose()
        {
            if (ctx.attributeSet != null)
                ctx.attributeSet.OnAttributeChanged -= OnAttributeChanged;

            RemoveModifier();
        }

        private void OnAttributeChanged(AttributeDefinition definition, float oldValue, float newValue)
        {
            if (definition != healthAttribute && definition != maxHealthAttribute)
                return;

            RecomputeAndApply();
        }

        private void RecomputeAndApply()
        {
            if (ctx.attributeSet == null)
                return;

            float maxHealth = ctx.attributeSet.GetAttributeValue(maxHealthAttribute);
            float currentHealth = ctx.attributeSet.GetAttributeValue(healthAttribute);
            float ratio = maxHealth > 0.0001f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
            bool shouldApply = ratio >= minRatio && ratio <= maxRatio;

            if (shouldApply == isApplied)
                return;

            if (shouldApply)
                ApplyModifier();
            else
                RemoveModifier();
        }

        private void ApplyModifier()
        {
            if (ctx.attributeSet == null || attribute == null)
                return;

            RemoveModifier();

            var modifier = new AttributeModifier(
                modifierType,
                value,
                Token,
                duration: 0f);

            ctx.attributeSet.TryAddModifier(attribute, modifier);
            isApplied = true;
        }

        private void RemoveModifier()
        {
            if (ctx.attributeSet != null)
                ctx.attributeSet.RemoveModifiersFromSource(Token);

            isApplied = false;
        }
    }
}
