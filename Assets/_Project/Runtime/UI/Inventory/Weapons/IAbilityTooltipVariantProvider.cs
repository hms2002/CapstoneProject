using UnityEngine;
using UnityGAS;

public readonly struct AbilityTooltipVariant
{
    public AbilityTooltipVariant(
        string id,
        string title,
        Sprite icon,
        string body,
        float? cooldownSeconds = null,
        string inputHint = null,
        string extraMeta = null)
    {
        Id = id;
        Title = title;
        Icon = icon;
        Body = body;
        CooldownSeconds = cooldownSeconds;
        InputHint = inputHint;
        ExtraMeta = extraMeta;
    }

    public string Id { get; }
    public string Title { get; }
    public Sprite Icon { get; }
    public string Body { get; }
    public float? CooldownSeconds { get; }
    public string InputHint { get; }
    public string ExtraMeta { get; }
}

public interface IAbilityTooltipVariantProvider
{
    int GetAbilityTooltipVariantCount(AbilityDefinition ability, ItemDetailContext ctx);
    AbilityTooltipVariant BuildAbilityTooltipVariant(AbilityDefinition ability, int variantIndex, ItemDetailContext ctx);
}
