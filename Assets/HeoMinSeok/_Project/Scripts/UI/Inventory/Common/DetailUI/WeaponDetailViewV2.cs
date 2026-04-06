using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityGAS;

public class WeaponDetailViewV2 : MonoBehaviour, IItemDetailView
{
    [Header("Summary")]
    [SerializeField] private TMP_Text summaryText;

    [Header("Stats")]
    [SerializeField] private Transform statRoot;
    [SerializeField] private WeaponStatLineView statLinePrefab;

    [Header("Abilities")]
    [SerializeField] private Transform abilityRoot;
    [SerializeField] private WeaponAbilityBlockView abilityBlockPrefab;

    private readonly List<WeaponStatLineView> spawnedStats = new();
    private readonly List<WeaponAbilityBlockView> spawnedAbilities = new();

    public bool CanShow(object def) => def is WeaponDefinition;

    public void Show(object def, ItemDetailContext ctx, ItemDetailPanelServices services)
    {
        gameObject.SetActive(true);
        Clear();

        WeaponDefinition weapon = (WeaponDefinition)def;

        if (summaryText != null)
        {
            string text = weapon.storyText ?? string.Empty;
            if (services?.formatText != null)
                text = services.formatText(text);

            summaryText.text = text;
        }

        BuildStatLines(weapon);

        AddAbilityBlock("스킬 1", weapon.skill1, weapon.skill1InputHint, InputActionId.Skill1, ctx, services);
        AddAbilityBlock("스킬 2", weapon.skill2, weapon.skill2InputHint, InputActionId.Skill2, ctx, services);

        Canvas.ForceUpdateCanvases();
    }

    public void Hide()
    {
        Clear();
        gameObject.SetActive(false);
    }

    private void Clear()
    {
        for (int i = 0; i < spawnedStats.Count; i++)
        {
            if (spawnedStats[i] != null)
                Destroy(spawnedStats[i].gameObject);
        }

        spawnedStats.Clear();

        for (int i = 0; i < spawnedAbilities.Count; i++)
        {
            if (spawnedAbilities[i] != null)
                Destroy(spawnedAbilities[i].gameObject);
        }

        spawnedAbilities.Clear();
    }

    private void BuildStatLines(WeaponDefinition weapon)
    {
        if (statRoot == null || statLinePrefab == null || weapon == null || weapon.statModifiers == null)
            return;

        for (int i = 0; i < weapon.statModifiers.Count; i++)
        {
            WeaponDefinition.WeaponStatModifier entry = weapon.statModifiers[i];
            if (entry.attribute == null)
                continue;

            string label = !string.IsNullOrEmpty(entry.labelOverride)
                ? entry.labelOverride
                : (!string.IsNullOrEmpty(entry.attribute.attributeName) ? entry.attribute.attributeName : entry.attribute.name);

            string value = entry.type == ModifierType.Percent
                ? FormatTooltipValue(entry.value, true)
                : FormatTooltipValue(entry.value, false);

            WeaponStatLineView line = Instantiate(statLinePrefab, statRoot);
            line.Set(label, value);
            spawnedStats.Add(line);
        }
    }

    private void AddAbilityBlock(
        string header,
        AbilityDefinition ability,
        string inputHint,
        InputActionId inputAction,
        ItemDetailContext ctx,
        ItemDetailPanelServices services)
    {
        if (abilityRoot == null || abilityBlockPrefab == null || ability == null)
            return;

        string body = BuildAbilityBody(ability, ctx);
        if (services?.formatText != null)
            body = services.formatText(body);

        string displayHeader = !string.IsNullOrEmpty(ability.abilityName) ? ability.abilityName : header;
        WeaponAbilityBlockView view = Instantiate(abilityBlockPrefab, abilityRoot);
        view.Set(
            displayHeader,
            ability.icon,
            inputHint,
            ability.cooldown,
            "-",
            body,
            inputAction,
            services?.showGlossary);

        spawnedAbilities.Add(view);
    }

    private string BuildAbilityBody(AbilityDefinition ability, ItemDetailContext ctx)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrEmpty(ability.description))
            sb.AppendLine(ability.description);

        if (ability.sourceObject is IDetailProvider provider)
        {
            ItemDetailBlock block = provider.BuildDetailBlock(ctx);
            if (!string.IsNullOrEmpty(block.body))
            {
                if (sb.Length > 0)
                    sb.AppendLine();

                sb.AppendLine(block.body);
            }
        }

        return sb.ToString().TrimEnd();
    }

    private static string FormatTooltipValue(float value, bool isPercent)
    {
        string sign = value > 0f ? "+" : string.Empty;
        float absValue = isPercent ? value * 100f : value;
        string suffix = isPercent ? "%" : string.Empty;
        return $"[{sign}{absValue:0.##}{suffix}]";
    }
}
