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
    private readonly List<VariantAbilityEntry> variantEntries = new();

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

        if (abilityRoot is RectTransform abilityRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(abilityRect);

        Canvas.ForceUpdateCanvases();
        RefreshVariantPreviewLayouts();
    }

    public void Hide()
    {
        Clear();
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!gameObject.activeSelf || variantEntries.Count == 0)
            return;

        RefreshVariantPreviewLayouts();

        if (InputBindingService.EnsureInstance().WasPressedThisFrame(InputContextShortcutId.TooltipVariantNext))
            CycleFirstAvailableVariant();
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
        variantEntries.Clear();
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

        List<AbilityDisplayState> displayStates = BuildAbilityDisplayStates(ability, header, inputHint, ctx, services);
        if (displayStates.Count == 0)
            return;

        AbilityDisplayState initial = displayStates[0];
        WeaponAbilityBlockView view = Instantiate(abilityBlockPrefab, abilityRoot);
        view.Set(
            initial.Title,
            initial.Icon,
            initial.InputHint,
            initial.CooldownSeconds,
            initial.ExtraMeta,
            initial.Body,
            inputAction,
            services?.showGlossary);

        if (displayStates.Count > 1)
        {
            WeaponAbilityBlockView nextView = Instantiate(abilityBlockPrefab, abilityRoot);
            nextView.name = $"{view.name}_Next";
            view.SetExternalShuffleNextView(nextView);
            ApplyExternalPreview(view, displayStates[1], inputAction, services?.showGlossary);

            InputGlyphPresentation glyph = InputBindingService.EnsureInstance()
                .GetContextShortcutGlyph(InputContextShortcutId.TooltipVariantNext);
            string guideLabel = glyph.HasIcon
                ? "모드 전환"
                : $"{glyph.DisplayLabel} 모드 전환";

            view.SetVariantSwitchGuide(true, glyph.Icon, guideLabel);
            variantEntries.Add(new VariantAbilityEntry(view, displayStates, inputAction, services?.showGlossary));
            spawnedAbilities.Add(nextView);
        }
        else
        {
            view.SetVariantSwitchGuide(false, null, null);
        }

        spawnedAbilities.Add(view);
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
        string body = !string.IsNullOrWhiteSpace(variant.Body)
            ? variant.Body
            : BuildAbilityBody(ability, ctx);
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

    private void CycleFirstAvailableVariant()
    {
        for (int i = 0; i < variantEntries.Count; i++)
        {
            VariantAbilityEntry entry = variantEntries[i];
            if (entry == null || entry.View == null || entry.States.Count <= 1)
                continue;

            if (entry.View.IsVariantSwitching)
                return;

            entry.CurrentIndex = (entry.CurrentIndex + 1) % entry.States.Count;
            ApplyVariantEntry(entry, animate: true);
            Canvas.ForceUpdateCanvases();
            return;
        }
    }

    private void RefreshVariantPreviewLayouts()
    {
        for (int i = 0; i < variantEntries.Count; i++)
        {
            VariantAbilityEntry entry = variantEntries[i];
            if (entry?.View != null)
                entry.View.RefreshExternalShufflePreviewLayout();
        }
    }

    private static void ApplyVariantEntry(VariantAbilityEntry entry, bool animate)
    {
        AbilityDisplayState state = entry.States[entry.CurrentIndex];
        AbilityDisplayState previewState = entry.States[(entry.CurrentIndex + 1) % entry.States.Count];

        if (animate)
            QueueExternalPreview(entry.View, previewState, entry.InputAction, entry.OnGlossaryClick);

        entry.View.SetVariantDisplay(
            state.Title,
            state.Icon,
            state.InputHint,
            state.CooldownSeconds,
            state.ExtraMeta,
            state.Body,
            entry.InputAction,
            animate,
            entry.OnGlossaryClick);

        if (!animate)
            ApplyExternalPreview(entry.View, previewState, entry.InputAction, entry.OnGlossaryClick);
    }

    private static void ApplyExternalPreview(
        WeaponAbilityBlockView view,
        AbilityDisplayState state,
        InputActionId inputAction,
        Action<string> onGlossaryClick)
    {
        if (view == null)
            return;

        view.SetExternalShufflePreview(
            state.Title,
            state.Icon,
            state.InputHint,
            state.CooldownSeconds,
            state.ExtraMeta,
            state.Body,
            inputAction,
            onGlossaryClick);
    }

    private static void QueueExternalPreview(
        WeaponAbilityBlockView view,
        AbilityDisplayState state,
        InputActionId inputAction,
        Action<string> onGlossaryClick)
    {
        if (view == null)
            return;

        view.QueueExternalShufflePreview(
            state.Title,
            state.Icon,
            state.InputHint,
            state.CooldownSeconds,
            state.ExtraMeta,
            state.Body,
            inputAction,
            onGlossaryClick);
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

    private sealed class VariantAbilityEntry
    {
        public VariantAbilityEntry(
            WeaponAbilityBlockView view,
            List<AbilityDisplayState> states,
            InputActionId inputAction,
            Action<string> onGlossaryClick)
        {
            View = view;
            States = states;
            InputAction = inputAction;
            OnGlossaryClick = onGlossaryClick;
        }

        public WeaponAbilityBlockView View { get; }
        public List<AbilityDisplayState> States { get; }
        public InputActionId InputAction { get; }
        public Action<string> OnGlossaryClick { get; }
        public int CurrentIndex { get; set; }
    }
}
