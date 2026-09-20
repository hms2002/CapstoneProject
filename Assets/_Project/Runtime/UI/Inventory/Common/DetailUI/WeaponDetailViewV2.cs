using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
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
    private bool showDetailedDescription;

    public bool CanShow(object def) => def is WeaponDefinition;

    public void Show(object def, ItemDetailContext ctx, ItemDetailPanelServices services)
    {
        Show(def, ctx, services, false);
    }

    public void Show(object def, ItemDetailContext ctx, ItemDetailPanelServices services, bool detailed)
    {
        showDetailedDescription = detailed;
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

        if (weapon.weaponId == "Weapon.CrimsonBoundary")
            AddAbilityBlock("기본 공격", weapon.GetAbility(WeaponAbilitySlot.Attack), weapon.attackInputHint, InputActionId.PrimaryAttack, ctx, services);

        AddAbilityBlock("스킬 1", weapon.GetAbility(WeaponAbilitySlot.Skill1), weapon.skill1InputHint, InputActionId.Skill1, ctx, services);
        AddAbilityBlock("스킬 2", weapon.GetAbility(WeaponAbilitySlot.Skill2), weapon.skill2InputHint, InputActionId.Skill2, ctx, services);



        if (abilityRoot is RectTransform abilityRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(abilityRect);

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
            {
                spawnedStats[i].gameObject.SetActive(false);
                Destroy(spawnedStats[i].gameObject);
            }
        }

        spawnedStats.Clear();

        for (int i = 0; i < spawnedAbilities.Count; i++)
        {
            if (spawnedAbilities[i] != null)
            {
                spawnedAbilities[i].gameObject.SetActive(false);
                Destroy(spawnedAbilities[i].gameObject);
            }
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
        InputActionId? inputAction,
        ItemDetailContext ctx,
        ItemDetailPanelServices services)
    {
        if (abilityRoot == null || abilityBlockPrefab == null || ability == null)
            return;

        List<AbilityDisplayState> displayStates = BuildAbilityDisplayStates(ability, header, inputHint, ctx, services);
        if (displayStates.Count == 0)
            return;

        foreach (AbilityDisplayState state in displayStates)
        {
            WeaponAbilityBlockView view = Instantiate(abilityBlockPrefab, abilityRoot);
            view.Set(state.Title, state.Icon, state.InputHint, state.CooldownSeconds,
                state.ExtraMeta, state.Body, inputAction, services?.showGlossary);
            spawnedAbilities.Add(view);
        }
    }

    private List<AbilityDisplayState> BuildAbilityDisplayStates(
        AbilityDefinition ability,
        string header,
        string inputHint,
        ItemDetailContext ctx,
        ItemDetailPanelServices services)
    {
        List<AbilityDisplayState> states = new();

        if (ability.sourceObject is IAbilityTooltipVariantProvider variantProvider)
        {
            int count = Mathf.Max(0, variantProvider.GetAbilityTooltipVariantCount(ability, ctx));
            for (int i = 0; i < count; i++)
            {
                AbilityTooltipVariant variant = variantProvider.BuildAbilityTooltipVariant(ability, i, ctx);
                states.Add(BuildDisplayState(ability, variant, header, inputHint, ctx, services));
            }
        }

        if (states.Count == 0)
            states.Add(BuildDefaultDisplayState(ability, header, inputHint, ctx, services));

        return states;
    }

    private AbilityDisplayState BuildDisplayState(
        AbilityDefinition ability,
        AbilityTooltipVariant variant,
        string header,
        string inputHint,
        ItemDetailContext ctx,
        ItemDetailPanelServices services)
    {
        string title = !string.IsNullOrWhiteSpace(variant.Title)
            ? variant.Title
            : (!string.IsNullOrEmpty(ability.abilityName) ? ability.abilityName : header);

        Sprite icon = variant.Icon != null ? variant.Icon : ability.icon;
        string body = !showDetailedDescription && !string.IsNullOrWhiteSpace(variant.SimpleBody)
            ? variant.SimpleBody
            : (!string.IsNullOrWhiteSpace(variant.Body) ? variant.Body : BuildAbilityBody(ability, ctx));
        if (services?.formatText != null)
            body = services.formatText(body);

        string resolvedInputHint = !string.IsNullOrWhiteSpace(variant.InputHint)
            ? variant.InputHint
            : inputHint;

        string extraMeta = !string.IsNullOrWhiteSpace(variant.ExtraMeta)
            ? variant.ExtraMeta
            : "-";

        return new AbilityDisplayState(
            title,
            icon,
            resolvedInputHint,
            variant.CooldownSeconds ?? ability.cooldown,
            extraMeta,
            body);
    }

    private AbilityDisplayState BuildDefaultDisplayState(
        AbilityDefinition ability,
        string header,
        string inputHint,
        ItemDetailContext ctx,
        ItemDetailPanelServices services)
    {
        string body = BuildAbilityBody(ability, ctx);
        if (services?.formatText != null)
            body = services.formatText(body);

        string displayHeader = !string.IsNullOrEmpty(ability.abilityName) ? ability.abilityName : header;
        return new AbilityDisplayState(
            displayHeader,
            ability.icon,
            inputHint,
            ability.cooldown,
            "-",
            body);
    }

    private string BuildAbilityBody(AbilityDefinition ability, ItemDetailContext ctx)
    {
        if (!showDetailedDescription && !string.IsNullOrWhiteSpace(ability.simpleDescription))
            return ability.simpleDescription;

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

    private readonly struct AbilityDisplayState
    {
        public AbilityDisplayState(
            string title,
            Sprite icon,
            string inputHint,
            float cooldownSeconds,
            string extraMeta,
            string body)
        {
            Title = title;
            Icon = icon;
            InputHint = inputHint;
            CooldownSeconds = cooldownSeconds;
            ExtraMeta = extraMeta;
            Body = body;
        }

        public string Title { get; }
        public Sprite Icon { get; }
        public string InputHint { get; }
        public float CooldownSeconds { get; }
        public string ExtraMeta { get; }
        public string Body { get; }
    }

}

/// <summary>Presentation-only description shortcut shared by hover and encyclopedia views.</summary>
[Serializable]
public sealed class WeaponDescriptionHint
{
    [SerializeField] private GameObject root;
    [SerializeField] private UnityEngine.UI.Image keyIcon;
    [SerializeField] private TMP_Text label;

    public void Refresh(bool visible, bool detailed)
    {
        if (root == null)
            return;
        root.SetActive(visible);
        if (!visible)
            return;

        InputGlyphPresentation glyph = InputBindingService.EnsureInstance()
            .GetContextShortcutGlyph(InputContextShortcutId.TooltipDescriptionToggle);
        if (keyIcon != null)
        {
            keyIcon.sprite = glyph.Icon;
            keyIcon.gameObject.SetActive(glyph.HasIcon);
        }
        if (label != null)
        {
            string caption = detailed ? "간단히 설명" : "자세히 설명";
            label.text = glyph.HasIcon ? caption : $"~ {caption}";
        }
    }

    public static bool WasTogglePressed()
    {
        GameObject selected = UnityEngine.EventSystems.EventSystem.current != null
            ? UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject : null;
        if (selected != null && (selected.GetComponent<TMP_InputField>() != null ||
            selected.GetComponent<UnityEngine.UI.InputField>() != null))
            return false;
        return InputBindingService.EnsureInstance()
            .WasPressedThisFrame(InputContextShortcutId.TooltipDescriptionToggle);
    }
}
