using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityGAS;

[DisallowMultipleComponent]
public sealed class EncyclopediaItemRightPage : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject contentRoot;
    [SerializeField] private GameObject emptyRoot;

    [Header("Common Header")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private RectTransform namePanelRoot;

    [Header("Shared Description")]
    [SerializeField] private GameObject descriptionRoot;
    [SerializeField] private TMP_Text descriptionTitleText;
    [SerializeField] private TMP_Text storyText;

    [SerializeField, HideInInspector] private TMP_Text descriptionText;
    [SerializeField, HideInInspector] private TMP_Text metadataText;
    [SerializeField, HideInInspector] private TMP_Text relicDescriptionText;
    [SerializeField, HideInInspector] private TMP_Text consumableDescriptionText;
    [SerializeField, HideInInspector] private GameObject weaponDetailRoot;
    [SerializeField, HideInInspector] private GameObject relicDetailRoot;
    [SerializeField, HideInInspector] private GameObject consumableDetailRoot;

    [Header("Weapon Sections")]
    [SerializeField] private GameObject weaponStatsRoot;
    [SerializeField] private TMP_Text weaponStatsText;
    [SerializeField] private GameObject weaponAbilityRoot;
    [SerializeField] private Transform abilityContainer;
    [SerializeField] private WeaponAbilityBlockView abilityBlockPrefab;
    [SerializeField] private bool hideTemplateAbilityBlock = true;

    [Header("Weapon Stat / Relic Preview Sections")]
    [SerializeField] private GameObject relicPreviewRoot;
    [SerializeField] private TMP_Text relicLevelText;
    [SerializeField] private GameObject relicPreviewPreviousGuideRoot;
    [SerializeField] private Image relicPreviewPreviousGuideIcon;
    [SerializeField] private CanvasGroup relicPreviewPreviousGuideCanvasGroup;
    [SerializeField] private GameObject relicPreviewNextGuideRoot;
    [SerializeField] private Image relicPreviewNextGuideIcon;
    [SerializeField] private CanvasGroup relicPreviewNextGuideCanvasGroup;
    [SerializeField, Range(0f, 1f)] private float disabledGuideAlpha = 0.35f;
    [SerializeField] private GameObject relicEffectRoot;
    [SerializeField] private TMP_Text relicEffectText;

    [Header("Glossary")]
    [SerializeField] private GlossaryDatabase glossary;
    [SerializeField] private GlossaryPopup glossaryPopup;
    [SerializeField] private TooltipColorPalette tooltipColorPalette;
    [SerializeField] private string glossaryLinkColorHex = "5EC8FF";

    [Header("Scroll")]
    [SerializeField] private ScrollRect detailScrollRect;
    [SerializeField] private bool resetScrollOnBind = true;

    private const string EmptyDescriptionText = "설명 준비 중";
    private const string EmptyRelicEffectText = "효과 정보 없음";

    private readonly List<WeaponAbilityBlockView> abilityBlockPool = new();
    private readonly List<VariantAbilityEntry> variantEntries = new();
    private readonly StringBuilder builder = new();
    private ItemDisplayIconDefaultState iconDefaultState;
    private ItemDetailContext detailContext;
    private Action<string> glossaryClickHandler;
    private int abilityBlockCursor;
    private Coroutine pendingScrollReset;
    private RelicDefinition currentRelic;
    private int relicPreviewLevel = 1;
    private bool warnedMissingHeader;
    private bool warnedMissingDescription;
    private bool warnedMissingAbilityAuthoring;
    private bool warnedMissingScrollRect;

#if UNITY_EDITOR
    private const string AbilityBlockPrefabPath = "Assets/LeeJunMo/Prefab/UI/PopupUI/Encyclopedia/Panel_AbilityBlock_Encyclopedia.prefab";
#endif

    private void Awake()
    {
        InitializeRuntimeState();
        ValidateRequiredReferences();
    }

    private void Update()
    {
        if (!isActiveAndEnabled || (variantEntries.Count == 0 && currentRelic == null))
            return;

        InputBindingService input = InputBindingService.EnsureInstance();

        if (variantEntries.Count > 0)
        {
            RefreshVariantPreviewLayouts();
            if (input.WasPressedThisFrame(InputContextShortcutId.TooltipVariantNext))
                CycleFirstAvailableVariant();
        }

        if (currentRelic != null)
            HandleRelicPreviewInput(input);
    }

    private void OnDisable()
    {
        CancelPendingScrollReset();
    }

#if UNITY_EDITOR
    private void Reset()
    {
        ResolveReferences();
    }

    [ContextMenu("Auto Wire References")]
    private void AutoWireReferences()
    {
        ResolveReferences();
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

    public void ResolveReferences()
    {
        if (contentRoot == null || contentRoot == gameObject)
        {
            GameObject resolvedContentRoot = EncyclopediaReferenceResolver.FindGameObject(
                transform,
                "ItemRightContent",
                "ContentRoot",
                "ScrollContent",
                "ViewportContent",
                "Content");
            if (resolvedContentRoot != null && resolvedContentRoot != gameObject)
                contentRoot = resolvedContentRoot;
            else if (contentRoot == null)
                contentRoot = gameObject;
        }

        if (emptyRoot == null)
            emptyRoot = EncyclopediaReferenceResolver.FindGameObject(transform, "EmptyRoot", "EmptyPanel", "EmptyView");

        if (iconImage == null)
            iconImage = EncyclopediaReferenceResolver.FindComponent<Image>(transform, "Icon", "ItemIcon", "DetailIcon", "DetailImage");

        if (titleText == null)
        {
            titleText = EncyclopediaReferenceResolver.FindComponent<TMP_Text>(transform, "TitleText", "NameText", "Name");
            if (titleText == null)
                titleText = EncyclopediaReferenceResolver.FindComponentUnderParent<TMP_Text>(transform, "Name_Panel", "Text", "Text (TMP)", "Text(TMP)");
            if (titleText == null)
                titleText = EncyclopediaReferenceResolver.FindComponentUnderParent<TMP_Text>(transform, "Header", "TitleText", "NameText", "Text", "Text (TMP)", "Text(TMP)");
        }

        if (namePanelRoot == null)
        {
            Transform namePanel = EncyclopediaReferenceResolver.FindTransform(transform, "Name_Panel", "NamePanel", "HeaderNamePanel");
            if (namePanel is RectTransform namePanelRect)
                namePanelRoot = namePanelRect;
            else if (titleText != null)
                namePanelRoot = titleText.transform.parent as RectTransform;
        }

        if (descriptionRoot == null)
            descriptionRoot = EncyclopediaReferenceResolver.FindGameObject(transform, "DescriptionRoot", "DescriptionSection", "CommonDescriptionRoot", "DescriptionPanel");

        Transform descriptionTransform = descriptionRoot != null ? descriptionRoot.transform : null;
        if (descriptionTitleText == null && descriptionTransform != null)
            descriptionTitleText = EncyclopediaReferenceResolver.FindComponent<TMP_Text>(descriptionTransform, "DescriptionTitleText", "SectionTitleText", "TitleText", "Title");

        if (storyText == null)
            storyText = EncyclopediaReferenceResolver.FindComponent<TMP_Text>(transform, "StoryText", "Story");

        if (descriptionText == null && descriptionTransform != null)
            descriptionText = EncyclopediaReferenceResolver.FindComponent<TMP_Text>(descriptionTransform, "DescriptionText", "BodyText");

        if (storyText == null)
            storyText = descriptionText;

        if (descriptionText == null)
            descriptionText = storyText;

        if (weaponStatsRoot == null || weaponStatsRoot == relicPreviewRoot)
        {
            GameObject resolvedWeaponStatsRoot = EncyclopediaReferenceResolver.FindGameObject(
                transform,
                "WeaponStatsRoot",
                "WeaponStatsPanel",
                "WeaponStatsSection",
                "StatTextPanel",
                "StatsRoot",
                "StatRoot");
            if (resolvedWeaponStatsRoot != null && (weaponStatsRoot == null || resolvedWeaponStatsRoot != relicPreviewRoot))
                weaponStatsRoot = resolvedWeaponStatsRoot;
        }

        if (weaponAbilityRoot == null)
            weaponAbilityRoot = EncyclopediaReferenceResolver.FindGameObject(transform, "WeaponAbilityRoot", "AbilityRoot", "AbilitySection", "AbilityContainerRoot");

        Transform abilitySearchRoot = weaponAbilityRoot != null ? weaponAbilityRoot.transform : transform;
        if (abilityContainer == null)
            abilityContainer = EncyclopediaReferenceResolver.FindTransform(abilitySearchRoot, "AbilityContainer", "AbilityBlockContainer");

        if (abilityContainer == null)
            abilityContainer = EncyclopediaReferenceResolver.FindTransform(transform, "AbilityContainer", "AbilityBlockContainer");

        if (abilityBlockPrefab == null && abilityContainer != null)
            abilityBlockPrefab = EncyclopediaReferenceResolver.FindComponent<WeaponAbilityBlockView>(abilityContainer, "Panel_AbilityBlock_Encyclopedia", "Panel_AbilityBlock", "AbilityBlock");

#if UNITY_EDITOR
        if (abilityBlockPrefab == null && !Application.isPlaying)
            abilityBlockPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<WeaponAbilityBlockView>(AbilityBlockPrefabPath);
#endif

        if (relicPreviewRoot == null || relicPreviewRoot == weaponStatsRoot)
        {
            GameObject resolvedRelicPreviewRoot = EncyclopediaReferenceResolver.FindGameObject(
                transform,
                "RelicPreviewRoot",
                "RelicLevelPreviewRoot",
                "LevelPreviewRoot",
                "LvPanel",
                "LevelPanel");
            if (resolvedRelicPreviewRoot != null && (relicPreviewRoot == null || resolvedRelicPreviewRoot != weaponStatsRoot))
                relicPreviewRoot = resolvedRelicPreviewRoot;
        }

        if (weaponStatsRoot == null)
            weaponStatsRoot = relicPreviewRoot;
        if (relicPreviewRoot == null)
            relicPreviewRoot = weaponStatsRoot;

        Transform weaponStatsTransform = weaponStatsRoot != null ? weaponStatsRoot.transform : transform;
        if (weaponStatsText == null || weaponStatsText == relicLevelText)
        {
            TMP_Text resolvedWeaponStatsText = EncyclopediaReferenceResolver.FindComponent<TMP_Text>(
                weaponStatsTransform,
                "WeaponStatsText",
                "StatsText",
                "StatText",
                "StatValueText",
                "Text",
                "Text (TMP)",
                "Text(TMP)");
            if (resolvedWeaponStatsText != null && (weaponStatsText == null || resolvedWeaponStatsText != relicLevelText))
                weaponStatsText = resolvedWeaponStatsText;
        }

        Transform relicPreviewTransform = relicPreviewRoot != null ? relicPreviewRoot.transform : transform;
        if (relicLevelText == null || relicLevelText == weaponStatsText)
        {
            TMP_Text resolvedRelicLevelText = EncyclopediaReferenceResolver.FindComponent<TMP_Text>(
                relicPreviewTransform,
                "LvTxt",
                "LvText",
                "LevelText",
                "RelicLevelText",
                "PreviewLevelText");
            if (resolvedRelicLevelText != null && (relicLevelText == null || resolvedRelicLevelText != weaponStatsText))
                relicLevelText = resolvedRelicLevelText;
        }

        ResolveRelicPreviewGuideReferences(relicPreviewTransform);

        if (relicEffectRoot == null)
            relicEffectRoot = EncyclopediaReferenceResolver.FindGameObject(transform, "RelicEffectRoot", "EffectRoot", "RelicEffectSection");

        Transform relicEffectTransform = relicEffectRoot != null ? relicEffectRoot.transform : null;
        if (relicEffectText == null && relicEffectTransform != null)
            relicEffectText = EncyclopediaReferenceResolver.FindComponent<TMP_Text>(relicEffectTransform, "RelicEffectText", "EffectText", "BodyText", "DescriptionText", "Text", "Text (TMP)", "Text(TMP)");

        if (glossaryPopup == null)
            glossaryPopup = GetComponentInChildren<GlossaryPopup>(true);

        if (detailScrollRect == null)
            detailScrollRect = GetComponent<ScrollRect>() ?? GetComponentInChildren<ScrollRect>(true);

        if (detailScrollRect != null && detailScrollRect.content == null && contentRoot != null)
        {
            RectTransform contentRect = contentRoot.transform as RectTransform;
            if (contentRect != null && contentRect != detailScrollRect.transform)
                detailScrollRect.content = contentRect;
        }

        if (iconImage != null)
            iconDefaultState = new ItemDisplayIconDefaultState(iconImage);

        InitializeRuntimeState();
    }

    private void InitializeRuntimeState()
    {
        if (iconImage != null)
            iconDefaultState = new ItemDisplayIconDefaultState(iconImage);

        detailContext ??= new ItemDetailContext();
        glossaryClickHandler ??= ShowGlossaryPopup;
    }

    private void ValidateRequiredReferences()
    {
        if (titleText == null && !warnedMissingHeader)
        {
            warnedMissingHeader = true;
            Debug.LogWarning("[EncyclopediaItemRightPage] TitleText is not assigned. Item name cannot be displayed.", this);
        }

        if (storyText == null && !warnedMissingDescription)
        {
            warnedMissingDescription = true;
            Debug.LogWarning("[EncyclopediaItemRightPage] StoryText is not assigned. Item description cannot be displayed.", this);
        }

        if (detailScrollRect == null && !warnedMissingScrollRect)
        {
            warnedMissingScrollRect = true;
            Debug.LogWarning("[EncyclopediaItemRightPage] ScrollRect is not assigned. Long item details will not auto-reset scroll position.", this);
        }
    }

    private void ValidateWeaponAuthoring()
    {
        if ((abilityContainer == null || abilityBlockPrefab == null) && !warnedMissingAbilityAuthoring)
        {
            warnedMissingAbilityAuthoring = true;
            Debug.LogWarning("[EncyclopediaItemRightPage] AbilityContainer or ability block prefab is not assigned. Weapon skills cannot be displayed.", this);
        }
    }

    public void SetContentVisible(bool visible)
    {
        if (contentRoot != null)
            contentRoot.SetActive(visible);
    }

    public void SettleLayout()
    {
        RebuildDetailLayout();
    }

    public void SettleLayoutAndResetScroll()
    {
        CancelPendingScrollReset();
        if (!resetScrollOnBind)
        {
            RebuildDetailLayout();
            return;
        }

        ApplyScrollReset();
    }

    public void Clear()
    {
        InitializeRuntimeState();
        currentRelic = null;
        relicPreviewLevel = 1;
        SetVisible(false);
        SetText(titleText, string.Empty);
        SetText(descriptionTitleText, string.Empty);
        SetText(storyText, string.Empty);
        SetText(weaponStatsText, string.Empty);
        SetText(relicLevelText, string.Empty);
        SetText(relicEffectText, string.Empty);
        ItemDisplayIconUtility.Clear(iconImage, iconDefaultState);
        HideTypeSections();
        HideAbilityBlocks();
        RebuildHeaderLayout();
        glossaryPopup?.Hide();
        QueueScrollReset();
    }

    public void ShowWeapon(WeaponDefinition weapon)
    {
        InitializeRuntimeState();
        ValidateRequiredReferences();
        ValidateWeaponAuthoring();
        if (weapon == null)
        {
            Clear();
            return;
        }

        currentRelic = null;
        relicPreviewLevel = 1;
        SetVisible(true);
        HideTypeSections();
        ApplyItemHeader(weapon);
        SetDescriptionSection("스토리", weapon.storyText);

        string statsText = BuildWeaponStatsText(weapon);
        SetText(weaponStatsText, statsText);
        SetActive(weaponStatsRoot, !string.IsNullOrWhiteSpace(statsText));
        SetRelicPreviewGuidesVisible(false);

        SetActive(weaponAbilityRoot, true);
        int abilityCount = BuildWeaponAbilityBlocks(weapon);
        SetActive(weaponAbilityRoot, abilityCount > 0);

        QueueScrollReset();
    }

    public void ShowRelic(RelicDefinition relic)
    {
        InitializeRuntimeState();
        ValidateRequiredReferences();
        if (relic == null)
        {
            Clear();
            return;
        }

        SetVisible(true);
        HideTypeSections();
        HideAbilityBlocks();
        currentRelic = relic;
        relicPreviewLevel = 1;

        ApplyItemHeader(relic);
        SetStorySectionVisible(false);
        SetActive(relicPreviewRoot, true);
        RefreshRelicPreview();
        QueueScrollReset();
    }

    public void ShowConsumable(ConsumableDefinition consumable)
    {
        InitializeRuntimeState();
        ValidateRequiredReferences();
        if (consumable == null)
        {
            Clear();
            return;
        }

        currentRelic = null;
        relicPreviewLevel = 1;
        SetVisible(true);
        HideTypeSections();
        HideAbilityBlocks();
        ApplyItemHeader(consumable);
        SetDescriptionSection("설명", consumable.description);
        QueueScrollReset();
    }

    private void ApplyItemHeader(ScriptableObject item)
    {
        if (item != null)
            ItemDisplayIconUtility.Apply(iconImage, item, ItemDisplayIconContext.InventorySlot, iconDefaultState);
        else
            ItemDisplayIconUtility.Clear(iconImage, iconDefaultState);

        SetText(titleText, GetItemDisplayName(item));
        RebuildHeaderLayout();
        glossaryPopup?.Hide();
    }

    private void SetDescriptionSection(string title, string rawDescription)
    {
        string description = FormatTextOrFallback(rawDescription, EmptyDescriptionText);
        SetActive(descriptionRoot, true);
        SetActive(descriptionTitleText != null ? descriptionTitleText.gameObject : null, true);
        SetActive(storyText != null ? storyText.gameObject : null, true);
        SetText(descriptionTitleText, title);
        SetText(storyText, description);
    }

    private int BuildWeaponAbilityBlocks(WeaponDefinition weapon)
    {
        HideAbilityBlocks();
        abilityBlockCursor = 0;
        ConfigureAbilityContainerLayoutForBlocks();

        if (weapon == null)
            return 0;

        int count = 0;
        if (AddAbilityBlock("스킬 1", weapon.GetAbility(WeaponAbilitySlot.Skill1), weapon.skill1InputHint, InputActionId.Skill1))
            count++;
        if (AddAbilityBlock("스킬 2", weapon.GetAbility(WeaponAbilitySlot.Skill2), weapon.skill2InputHint, InputActionId.Skill2))
            count++;

        RebuildDetailLayout();
        RefreshVariantPreviewLayouts();
        return count;
    }

    private bool AddAbilityBlock(string header, AbilityDefinition ability, string inputHint, InputActionId inputAction)
    {
        if (abilityContainer == null || abilityBlockPrefab == null || ability == null)
            return false;

        List<AbilityDisplayState> displayStates = BuildAbilityDisplayStates(ability, header, inputHint);
        if (displayStates.Count == 0)
            return false;

        AbilityDisplayState initial = displayStates[0];
        WeaponAbilityBlockView view = GetAbilityBlock();
        if (view == null)
            return false;

        view.Set(
            initial.Title,
            initial.Icon,
            initial.InputHint,
            initial.CooldownSeconds,
            initial.ExtraMeta,
            initial.Body,
            inputAction,
            glossaryClickHandler);

        if (displayStates.Count > 1)
        {
            WeaponAbilityBlockView nextView = GetAbilityBlock();
            if (nextView != null)
            {
                nextView.name = $"{view.name}_Next";
                view.SetExternalShuffleNextView(nextView);
                ApplyExternalPreview(view, displayStates[1], inputAction, glossaryClickHandler);

                InputGlyphPresentation glyph = InputBindingService.EnsureInstance()
                    .GetContextShortcutGlyph(InputContextShortcutId.TooltipVariantNext);
                string guideLabel = glyph.HasIcon
                    ? "모드 전환"
                    : $"{glyph.DisplayLabel} 모드 전환";

                view.SetVariantSwitchGuide(true, glyph.Icon, guideLabel);
                variantEntries.Add(new VariantAbilityEntry(view, displayStates, inputAction, glossaryClickHandler));
            }
        }
        else
        {
            view.SetExternalShuffleNextView(null);
            view.SetVariantSwitchGuide(false, null, null);
        }

        return true;
    }

    private WeaponAbilityBlockView GetAbilityBlock()
    {
        if (abilityBlockPrefab == null || abilityContainer == null)
            return null;

        if (hideTemplateAbilityBlock && abilityBlockPrefab.transform.IsChildOf(abilityContainer))
            abilityBlockPrefab.gameObject.SetActive(false);

        while (abilityBlockCursor < abilityBlockPool.Count && abilityBlockPool[abilityBlockCursor] == null)
            abilityBlockCursor++;

        WeaponAbilityBlockView view;
        if (abilityBlockCursor < abilityBlockPool.Count)
        {
            view = abilityBlockPool[abilityBlockCursor];
        }
        else
        {
            view = Instantiate(abilityBlockPrefab, abilityContainer);
            abilityBlockPool.Add(view);
        }

        abilityBlockCursor++;
        view.ResetPooledPresentationState();
        view.gameObject.SetActive(true);
        return view;
    }

    private void ConfigureAbilityContainerLayoutForBlocks()
    {
        if (abilityContainer == null)
            return;

        if (abilityContainer.TryGetComponent(out VerticalLayoutGroup verticalLayout))
        {
            verticalLayout.childControlWidth = true;
            verticalLayout.childControlHeight = true;
            verticalLayout.childForceExpandWidth = true;
            verticalLayout.childForceExpandHeight = false;
        }
    }

    private void HideAbilityBlocks()
    {
        variantEntries.Clear();
        abilityBlockCursor = 0;

        for (int i = 0; i < abilityBlockPool.Count; i++)
        {
            WeaponAbilityBlockView view = abilityBlockPool[i];
            if (view == null)
                continue;

            view.ResetPooledPresentationState();
            view.gameObject.SetActive(false);
        }

        if (hideTemplateAbilityBlock && abilityBlockPrefab != null && abilityContainer != null && abilityBlockPrefab.transform.IsChildOf(abilityContainer))
            abilityBlockPrefab.gameObject.SetActive(false);
    }

    private List<AbilityDisplayState> BuildAbilityDisplayStates(AbilityDefinition ability, string header, string inputHint)
    {
        var states = new List<AbilityDisplayState>();
        if (ability == null)
            return states;

        if (ability.sourceObject is IAbilityTooltipVariantProvider variantProvider)
        {
            int count = Mathf.Max(0, variantProvider.GetAbilityTooltipVariantCount(ability, detailContext));
            for (int i = 0; i < count; i++)
            {
                AbilityTooltipVariant variant = variantProvider.BuildAbilityTooltipVariant(ability, i, detailContext);
                states.Add(BuildDisplayState(ability, variant, header, inputHint));
            }
        }

        if (states.Count == 0)
            states.Add(BuildDefaultDisplayState(ability, header, inputHint));

        return states;
    }

    private AbilityDisplayState BuildDisplayState(AbilityDefinition ability, AbilityTooltipVariant variant, string header, string inputHint)
    {
        string title = !string.IsNullOrWhiteSpace(variant.Title)
            ? variant.Title
            : (!string.IsNullOrWhiteSpace(ability.abilityName) ? ability.abilityName : header);

        Sprite icon = variant.Icon != null ? variant.Icon : ability.icon;
        string body = !string.IsNullOrWhiteSpace(variant.Body) ? variant.Body : BuildAbilityBody(ability);
        string resolvedInputHint = !string.IsNullOrWhiteSpace(variant.InputHint) ? variant.InputHint : inputHint;
        string extraMeta = !string.IsNullOrWhiteSpace(variant.ExtraMeta) ? variant.ExtraMeta : "-";

        return new AbilityDisplayState(
            title,
            icon,
            resolvedInputHint,
            variant.CooldownSeconds ?? ability.cooldown,
            extraMeta,
            FormatText(body));
    }

    private AbilityDisplayState BuildDefaultDisplayState(AbilityDefinition ability, string header, string inputHint)
    {
        string title = !string.IsNullOrWhiteSpace(ability.abilityName) ? ability.abilityName : header;
        return new AbilityDisplayState(
            title,
            ability.icon,
            inputHint,
            ability.cooldown,
            "-",
            FormatText(BuildAbilityBody(ability)));
    }

    private string BuildAbilityBody(AbilityDefinition ability)
    {
        builder.Clear();

        if (ability != null && !string.IsNullOrWhiteSpace(ability.description))
            builder.AppendLine(ability.description);

        if (ability != null && ability.sourceObject is IDetailProvider provider)
        {
            ItemDetailBlock block = provider.BuildDetailBlock(detailContext);
            if (!string.IsNullOrWhiteSpace(block.body))
            {
                if (builder.Length > 0)
                    builder.AppendLine();

                builder.AppendLine(block.body);
            }
        }

        return builder.ToString().TrimEnd();
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

    private void HandleRelicPreviewInput(InputBindingService input)
    {
        if (currentRelic == null || input == null)
            return;

        int nextLevel = relicPreviewLevel;
        if (input.WasPressedThisFrame(InputContextShortcutId.RelicPreviewPrevious))
            nextLevel--;
        if (input.WasPressedThisFrame(InputContextShortcutId.RelicPreviewNext))
            nextLevel++;

        int maxLevel = Mathf.Max(1, currentRelic.maxLevel);
        nextLevel = Mathf.Clamp(nextLevel, 1, maxLevel);
        if (nextLevel == relicPreviewLevel)
            return;

        relicPreviewLevel = nextLevel;
        RefreshRelicPreview();
    }

    private void RefreshRelicPreview()
    {
        if (currentRelic == null)
            return;

        int maxLevel = Mathf.Max(1, currentRelic.maxLevel);
        relicPreviewLevel = Mathf.Clamp(relicPreviewLevel, 1, maxLevel);
        string effectText = FormatText(BuildRelicEffectText(currentRelic, relicPreviewLevel));
        SetText(relicLevelText, $"Lv {relicPreviewLevel} / {maxLevel}");
        SetRelicEffectSection(effectText);

        RefreshRelicPreviewGuides(maxLevel);
        RebuildDetailLayout();
    }

    private string BuildRelicEffectText(RelicDefinition relic, int previewLevel)
    {
        if (relic == null || relic.logic == null)
            return EmptyRelicEffectText;

        RelicTooltipData tooltip = relic.logic.BuildTooltip(relic, previewLevel, detailContext);
        return tooltip != null && !string.IsNullOrWhiteSpace(tooltip.effectText)
            ? tooltip.effectText
            : EmptyRelicEffectText;
    }

    private void RefreshRelicPreviewGuides(int maxLevel)
    {
        InputBindingService input = InputBindingService.EnsureInstance();
        ApplyRelicPreviewGuide(
            relicPreviewPreviousGuideRoot,
            relicPreviewPreviousGuideIcon,
            relicPreviewPreviousGuideCanvasGroup,
            input.GetContextShortcutGlyph(InputContextShortcutId.RelicPreviewPrevious),
            true,
            relicPreviewLevel > 1);
        ApplyRelicPreviewGuide(
            relicPreviewNextGuideRoot,
            relicPreviewNextGuideIcon,
            relicPreviewNextGuideCanvasGroup,
            input.GetContextShortcutGlyph(InputContextShortcutId.RelicPreviewNext),
            true,
            relicPreviewLevel < maxLevel);
    }

    private void ResolveRelicPreviewGuideReferences(Transform relicPreviewTransform)
    {
        if (relicPreviewPreviousGuideRoot == null)
            relicPreviewPreviousGuideRoot = EncyclopediaReferenceResolver.FindGameObject(relicPreviewTransform, "RelicPreviewPreviousGuide", "PreviousGuide", "PrevGuide", "PrevPreview");
        if (relicPreviewNextGuideRoot == null)
            relicPreviewNextGuideRoot = EncyclopediaReferenceResolver.FindGameObject(relicPreviewTransform, "RelicPreviewNextGuide", "NextGuide", "NextPreview");

        if (relicPreviewPreviousGuideIcon == null && relicPreviewPreviousGuideRoot != null)
            relicPreviewPreviousGuideIcon = ResolveGuideIcon(relicPreviewPreviousGuideRoot.transform);
        if (relicPreviewNextGuideIcon == null && relicPreviewNextGuideRoot != null)
            relicPreviewNextGuideIcon = ResolveGuideIcon(relicPreviewNextGuideRoot.transform);

        if (relicPreviewPreviousGuideCanvasGroup == null && relicPreviewPreviousGuideRoot != null)
            relicPreviewPreviousGuideCanvasGroup = relicPreviewPreviousGuideRoot.GetComponent<CanvasGroup>();
        if (relicPreviewNextGuideCanvasGroup == null && relicPreviewNextGuideRoot != null)
            relicPreviewNextGuideCanvasGroup = relicPreviewNextGuideRoot.GetComponent<CanvasGroup>();
    }

    private void ShowGlossaryPopup(string key)
    {
        if (glossaryPopup == null)
            return;

        if (glossary != null && glossary.TryGet(key, out string description))
            glossaryPopup.Show(key, description);
        else
            glossaryPopup.Show(key, EmptyDescriptionText);
    }

    private static Image ResolveGuideIcon(Transform guideRoot)
    {
        if (guideRoot == null)
            return null;

        Image childIcon = EncyclopediaReferenceResolver.FindComponent<Image>(guideRoot, "Icon", "GuideIcon");
        return childIcon != null ? childIcon : guideRoot.GetComponent<Image>();
    }

    private void SetVisible(bool visible)
    {
        SetActive(contentRoot, visible);
        SetActive(emptyRoot, !visible);
    }

    private void HideTypeSections()
    {
        SetActive(weaponStatsRoot, false);
        SetActive(weaponAbilityRoot, false);
        SetActive(relicPreviewRoot, false);
        SetActive(relicEffectRoot, false);
        SetActive(relicEffectText != null ? relicEffectText.gameObject : null, false);
        SetRelicPreviewGuidesVisible(false);
        SetText(weaponStatsText, string.Empty);
        SetText(relicLevelText, string.Empty);
        SetText(relicEffectText, string.Empty);
    }

    private void SetRelicPreviewGuidesVisible(bool visible)
    {
        SetActive(relicPreviewPreviousGuideRoot, visible);
        SetActive(relicPreviewNextGuideRoot, visible);
    }

    private void SetStorySectionVisible(bool visible)
    {
        SetActive(descriptionRoot, visible);
        SetActive(descriptionTitleText != null ? descriptionTitleText.gameObject : null, visible);
        SetActive(storyText != null ? storyText.gameObject : null, visible);
        if (!visible)
        {
            SetText(descriptionTitleText, string.Empty);
            SetText(storyText, string.Empty);
        }
    }

    private void SetRelicEffectSection(string effectText)
    {
        bool hasDedicatedEffectText = relicEffectText != null && relicEffectText != storyText;
        SetActive(relicEffectRoot, hasDedicatedEffectText);
        SetActive(relicEffectText != null ? relicEffectText.gameObject : null, hasDedicatedEffectText);
        if (hasDedicatedEffectText)
            SetText(relicEffectText, effectText);
    }

    private void QueueScrollReset()
    {
        CancelPendingScrollReset();

        if (!resetScrollOnBind)
        {
            RebuildDetailLayout();
            return;
        }

        ApplyScrollReset();
    }

    private void CancelPendingScrollReset()
    {
        if (pendingScrollReset == null)
            return;

        StopCoroutine(pendingScrollReset);
        pendingScrollReset = null;
    }

    private void ApplyScrollReset()
    {
        RebuildDetailLayout();

        if (detailScrollRect == null)
            return;

        detailScrollRect.StopMovement();
        if (detailScrollRect.vertical)
            detailScrollRect.verticalNormalizedPosition = 1f;
        if (detailScrollRect.horizontal)
            detailScrollRect.horizontalNormalizedPosition = 0f;
    }

    private void RebuildDetailLayout()
    {
        ForceDetailTextMeshes();
        Canvas.ForceUpdateCanvases();

        RebuildHeaderLayout();

        if (abilityContainer is RectTransform abilityRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(abilityRect);

        if (contentRoot != null && contentRoot.transform is RectTransform contentRootRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRootRect);

        if (detailScrollRect != null)
        {
            if (detailScrollRect.content != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(detailScrollRect.content);

            if (detailScrollRect.viewport != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(detailScrollRect.viewport);
        }

        Canvas.ForceUpdateCanvases();
    }

    private void ForceDetailTextMeshes()
    {
        ForceTextMeshUpdate(descriptionTitleText);
        ForceTextMeshUpdate(storyText);
        ForceTextMeshUpdate(weaponStatsText);
        ForceTextMeshUpdate(relicLevelText);
        ForceTextMeshUpdate(relicEffectText);
    }

    private void RebuildHeaderLayout()
    {
        if (titleText != null)
        {
            titleText.ForceMeshUpdate();
            LayoutRebuilder.ForceRebuildLayoutImmediate(titleText.rectTransform);
        }

        if (namePanelRoot != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(namePanelRoot);
    }

    private static void ForceTextMeshUpdate(TMP_Text text)
    {
        if (text != null && text.gameObject.activeInHierarchy)
            text.ForceMeshUpdate();
    }

    private string FormatText(string raw)
    {
        return DetailTextFormatter.Format(raw ?? string.Empty, tooltipColorPalette, glossaryLinkColorHex);
    }

    private string FormatTextOrFallback(string raw, string fallback)
    {
        return FormatText(string.IsNullOrWhiteSpace(raw) ? fallback : raw);
    }

    private string BuildWeaponStatsText(WeaponDefinition weapon)
    {
        if (weapon == null || weapon.statModifiers == null || weapon.statModifiers.Count == 0)
            return string.Empty;

        builder.Clear();
        for (int i = 0; i < weapon.statModifiers.Count; i++)
        {
            WeaponDefinition.WeaponStatModifier modifier = weapon.statModifiers[i];
            if (modifier.attribute == null)
                continue;

            string label = !string.IsNullOrWhiteSpace(modifier.labelOverride)
                ? modifier.labelOverride
                : ResolveAttributeName(modifier.attribute);
            string value = modifier.type == ModifierType.Percent
                ? FormatTooltipValue(modifier.value, true)
                : FormatTooltipValue(modifier.value, false);

            if (builder.Length > 0)
                builder.AppendLine();

            builder.Append(label).Append(' ').Append(value);
        }

        return builder.ToString();
    }

    private static string FormatTooltipValue(float value, bool isPercent)
    {
        string sign = value > 0f ? "+" : string.Empty;
        float displayValue = isPercent ? value * 100f : value;
        string suffix = isPercent ? "%" : string.Empty;
        return $"[{sign}{displayValue:0.##}{suffix}]";
    }

    private static string ResolveAttributeName(AttributeDefinition attribute)
    {
        if (attribute == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(attribute.attributeName))
            return attribute.attributeName;

        return attribute.name;
    }

    private static string GetItemDisplayName(ScriptableObject item)
    {
        if (item is IInventoryItemDefinition definition && !string.IsNullOrWhiteSpace(definition.DisplayName))
            return definition.DisplayName;

        return item != null ? item.name : string.Empty;
    }

    private void ApplyRelicPreviewGuide(GameObject root, Image icon, CanvasGroup canvasGroup, InputGlyphPresentation glyph, bool visible, bool enabled)
    {
        SetActive(root, visible);
        if (icon != null)
        {
            if (glyph.HasIcon)
            {
                icon.sprite = glyph.Icon;
                icon.enabled = true;
            }
            else
            {
                icon.enabled = icon.sprite != null;
            }
        }

        if (canvasGroup == null)
            return;

        canvasGroup.alpha = enabled ? 1f : disabledGuideAlpha;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null)
            text.text = value ?? string.Empty;
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null)
            target.SetActive(active);
    }

    private readonly struct AbilityDisplayState
    {
        public AbilityDisplayState(string title, Sprite icon, string inputHint, float cooldownSeconds, string extraMeta, string body)
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
