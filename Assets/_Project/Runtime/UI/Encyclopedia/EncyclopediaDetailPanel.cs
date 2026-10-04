using System.Collections;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnityGAS;

/// <summary>
/// 책임 : 도감 항목의 상세 정보, 아이콘, 설명, 인벤토리 전용 상세 뷰를 표시한다.
/// </summary>
[AddComponentMenu("")]
[DisallowMultipleComponent]
public sealed class EncyclopediaDetailPanel : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject contentRoot;
    [SerializeField] private GameObject emptyRoot;

    [Header("Header")]
    [FormerlySerializedAs("detailImage")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text titleText;
    [FormerlySerializedAs("categoryText")]
    [SerializeField] private TMP_Text subtitleText;

    [Header("Common Text Fallback")]
    [FormerlySerializedAs("typeText")]
    [SerializeField] private TMP_Text metadataText;
    [SerializeField] private TMP_Text storyText;
    [HideInInspector, FormerlySerializedAs("attackStyleText")]
    [SerializeField] private TMP_Text legacyAttackStyleText;
    [HideInInspector, FormerlySerializedAs("stageText")]
    [SerializeField] private TMP_Text legacyStageText;

    [Header("Inventory Detail Views")]
    [SerializeField] private WeaponDetailViewV2 weaponViewV2;
    [SerializeField] private WeaponDetailView weaponView;
    [SerializeField] private RelicDetailView relicView;
    [SerializeField] private ConsumableDetailView consumableView;

    [Header("Weapon Text Fallback")]
    [SerializeField] private GameObject weaponDetailRoot;
    [SerializeField] private TMP_Text weaponStatsText;
    [SerializeField] private TMP_Text weaponAbilitiesText;

    [HideInInspector]
    [SerializeField] private GameObject bossAffectionRoot;
    [HideInInspector]
    [SerializeField] private TMP_Text bossAffectionText;
    [HideInInspector]
    [SerializeField] private TMP_Text bossRewardText;

    [Header("Glossary")]
    [SerializeField] private GlossaryDatabase glossary;
    [SerializeField] private GlossaryPopup glossaryPopup;
    [SerializeField] private TooltipColorPalette tooltipColorPalette;
    [SerializeField] private string glossaryLinkColorHex = "5EC8FF";

    [Header("Scroll")]
    [SerializeField] private ScrollRect detailScrollRect;
    [SerializeField] private bool resetScrollOnBind = true;

    private readonly StringBuilder builder = new();
    private ItemDetailPanelServices services;
    private ItemDisplayIconDefaultState iconDefaultState;
    private ScriptableObject currentDefinition;
    private string currentHeaderLevelSuffix = string.Empty;
    private Coroutine pendingScrollReset;

    private void Awake()
    {
        ResolveReferences();
        EnsureServices();
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
        EditorAuthoringPlayback.MarkDirty(this);
    }
#endif

    private void OnDisable()
    {
        if (pendingScrollReset == null)
            return;

        StopCoroutine(pendingScrollReset);
        pendingScrollReset = null;
    }

    public void ResolveReferences()
    {
        if (contentRoot == null)
            contentRoot = EncyclopediaReferenceResolver.FindGameObject(transform, "ContentRoot", "ContentPanel", "Content");

        if (emptyRoot == null)
            emptyRoot = EncyclopediaReferenceResolver.FindGameObject(transform, "EmptyRoot", "EmptyPanel", "EmptyView");

        if (iconImage == null)
            iconImage = EncyclopediaReferenceResolver.FindComponent<Image>(transform, "Icon", "ICON", "ItemIcon", "DetailIcon", "DetailImage");

        if (titleText == null)
        {
            titleText = EncyclopediaReferenceResolver.FindComponent<TMP_Text>(transform, "TitleText", "NameText", "name", "Name");
            if (titleText == null)
                titleText = EncyclopediaReferenceResolver.FindComponentUnderParent<TMP_Text>(transform, "Name_Panel", "Text", "Text (TMP)", "Text(TMP)");
            if (titleText == null)
                titleText = EncyclopediaReferenceResolver.FindComponentUnderParent<TMP_Text>(transform, "Header", "TitleText", "NameText", "Text", "Text (TMP)", "Text(TMP)");
        }

        if (subtitleText == null)
            subtitleText = EncyclopediaReferenceResolver.FindComponent<TMP_Text>(transform, "SubtitleText", "CategoryText", "TypeText");

        if (metadataText == null)
            metadataText = EncyclopediaReferenceResolver.FindComponent<TMP_Text>(transform, "MetadataText", "InfoText", "Text_Ect", "ExtraMetaText");

        if (storyText == null)
            storyText = EncyclopediaReferenceResolver.FindComponent<TMP_Text>(transform, "StoryText", "DescriptionText", "Story");

        if (weaponViewV2 == null)
            weaponViewV2 = GetComponentInChildren<WeaponDetailViewV2>(true);

        if (weaponView == null)
            weaponView = GetComponentInChildren<WeaponDetailView>(true);

        if (relicView == null)
            relicView = GetComponentInChildren<RelicDetailView>(true);

        if (consumableView == null)
            consumableView = GetComponentInChildren<ConsumableDetailView>(true);

        if (weaponDetailRoot == null)
            weaponDetailRoot = EncyclopediaReferenceResolver.FindGameObject(transform, "WeaponDetailRoot", "WeaponDetailFallback", "WeaponTextFallback");

        if (weaponStatsText == null)
        {
            weaponStatsText = EncyclopediaReferenceResolver.FindComponent<TMP_Text>(transform, "WeaponStatsText", "StatsText", "StatText");
            if (weaponStatsText == null)
                weaponStatsText = EncyclopediaReferenceResolver.FindComponentUnderParent<TMP_Text>(transform, "LvPanel", "StatsText", "Text", "Text (TMP)", "Text(TMP)");
        }

        if (weaponAbilitiesText == null)
        {
            weaponAbilitiesText = EncyclopediaReferenceResolver.FindComponent<TMP_Text>(transform, "WeaponAbilitiesText", "AbilitiesText", "AbilityText", "SkillText");
            if (weaponAbilitiesText == null)
                weaponAbilitiesText = EncyclopediaReferenceResolver.FindComponentUnderParent<TMP_Text>(transform, "SkillInfoGroup", "WeaponAbilitiesText", "AbilitiesText", "AbilityText", "SkillText", "Text", "Text (TMP)", "Text(TMP)");
            if (weaponAbilitiesText == null)
                weaponAbilitiesText = EncyclopediaReferenceResolver.FindComponentUnderParent<TMP_Text>(transform, "AbilityBlockContainer", "WeaponAbilitiesText", "AbilitiesText", "AbilityText", "SkillText", "Text", "Text (TMP)", "Text(TMP)");
        }

        if (glossaryPopup == null)
            glossaryPopup = GetComponentInChildren<GlossaryPopup>(true);

        if (detailScrollRect == null)
            detailScrollRect = GetComponent<ScrollRect>() ?? GetComponentInChildren<ScrollRect>(true);

        if (iconImage != null)
            iconDefaultState = new ItemDisplayIconDefaultState(iconImage);
    }

    public void ShowWeapon(EncyclopediaWeaponEntry entry)
    {
        if (entry == null)
        {
            Clear();
            return;
        }

        if (entry.weapon != null)
        {
            ShowWeapon(entry.weapon);
            return;
        }

        ShowLegacyWeapon(entry.DisplayName, entry.Id, entry.Image, entry.StageText, null);
    }

    public void ShowWeapon(WeaponDefinition weapon)
    {
        if (weapon == null)
        {
            Clear();
            return;
        }

        ShowInventoryItem(weapon, GameText.Get("code.encyclopediadetailpanel.8c4dac1fcc", "무기"), () => ShowLegacyWeapon(GetDisplayName(weapon), weapon.weaponId, weapon.icon, string.Empty, weapon));
    }

    public void ShowRelic(RelicDefinition relic)
    {
        if (relic == null)
        {
            Clear();
            return;
        }

        ShowInventoryItem(relic, GameText.Get("code.encyclopediadetailpanel.b949ada423", "유물"), () => ShowLegacyItem(relic, GameText.Get("code.encyclopediadetailpanel.b949ada423", "유물"), relic.relicId, GameText.Asset(relic, "description", relic.description)));
    }

    public void ShowConsumable(ConsumableDefinition consumable)
    {
        if (consumable == null)
        {
            Clear();
            return;
        }

        ShowInventoryItem(consumable, GameText.Get("code.encyclopediadetailpanel.fd06a09504", "소모품"), () => ShowLegacyItem(consumable, GameText.Get("code.encyclopediadetailpanel.fd06a09504", "소모품"), consumable.consumableId, GameText.Asset(consumable, "description", consumable.description)));
    }

    public void ShowMonster(EncyclopediaMonsterEntry entry)
    {
        ResolveReferences();

        if (entry == null)
        {
            Clear();
            return;
        }

        SetVisible(true);
        HideInventoryViews();
        SetLegacyMode(weaponMode: false, bossMode: false);
        SetImage(iconImage, entry.image);
        SetText(titleText, entry.DisplayName);
        SetText(subtitleText, GameText.Get("code.encyclopediadetailpanel.fa2390684b", "몬스터"));
        SetText(metadataText, BuildMetadata(
            FormatLabeledLine(GameText.Get("code.encyclopediadetailpanel.8b1d74f0c2", "분류"), entry.type),
            FormatLabeledLine(GameText.Get("code.encyclopediadetailpanel.f465d8a381", "공격 방식"), entry.AttackStyle),
            FormatLabeledLine(GameText.Get("code.encyclopediadetailpanel.082c060ddf", "등장 구역"), entry.StageText)));
        SetText(legacyAttackStyleText, FormatLabeledLine(GameText.Get("code.encyclopediadetailpanel.f465d8a381", "공격 방식"), entry.AttackStyle));
        SetText(legacyStageText, FormatLabeledLine(GameText.Get("code.encyclopediadetailpanel.082c060ddf", "등장 구역"), entry.StageText));
        SetText(storyText, string.IsNullOrWhiteSpace(entry.StoryText) ? GameText.Get("code.encyclopediadetailpanel.c6793d9ba0", "설명 준비 중") : entry.StoryText);
        SetText(weaponStatsText, string.Empty);
        SetText(weaponAbilitiesText, string.Empty);
        SetText(bossAffectionText, string.Empty);
        SetText(bossRewardText, string.Empty);
        QueueScrollReset();
    }

    public void ShowBoss(EncyclopediaBossEntry entry)
    {
        ResolveReferences();

        if (entry == null)
        {
            Clear();
            return;
        }

        SetVisible(true);
        HideInventoryViews();
        SetLegacyMode(weaponMode: false, bossMode: true);
        SetImage(iconImage, entry.image);
        SetText(titleText, entry.DisplayName);
        SetText(subtitleText, GameText.Get("code.encyclopediadetailpanel.792e7e43e5", "보스"));
        SetText(metadataText, BuildMetadata(
            FormatLabeledLine(GameText.Get("code.encyclopediadetailpanel.8b1d74f0c2", "분류"), entry.type),
            FormatLabeledLine(GameText.Get("code.encyclopediadetailpanel.f465d8a381", "공격 방식"), entry.AttackStyle),
            FormatLabeledLine(GameText.Get("code.encyclopediadetailpanel.082c060ddf", "등장 구역"), entry.StageText)));
        SetText(legacyAttackStyleText, FormatLabeledLine(GameText.Get("code.encyclopediadetailpanel.f465d8a381", "공격 방식"), entry.AttackStyle));
        SetText(legacyStageText, FormatLabeledLine(GameText.Get("code.encyclopediadetailpanel.082c060ddf", "등장 구역"), entry.StageText));
        SetText(storyText, string.IsNullOrWhiteSpace(entry.StoryText) ? GameText.Get("code.encyclopediadetailpanel.c6793d9ba0", "설명 준비 중") : entry.StoryText);
        SetText(weaponStatsText, string.Empty);
        SetText(weaponAbilitiesText, string.Empty);
        SetText(bossAffectionText, BuildBossAffectionText(entry.npcData));
        SetText(bossRewardText, BuildBossRewardText(entry.npcData));
        QueueScrollReset();
    }

    public void Clear()
    {
        ResolveReferences();
        HideInventoryViews();

        currentDefinition = null;
        currentHeaderLevelSuffix = string.Empty;
        SetImage(iconImage, null);
        SetText(titleText, string.Empty);
        SetText(subtitleText, string.Empty);
        SetText(metadataText, string.Empty);
        SetText(legacyAttackStyleText, string.Empty);
        SetText(legacyStageText, string.Empty);
        SetText(storyText, string.Empty);
        SetText(weaponStatsText, string.Empty);
        SetText(weaponAbilitiesText, string.Empty);
        SetText(bossAffectionText, string.Empty);
        SetText(bossRewardText, string.Empty);
        SetLegacyMode(weaponMode: false, bossMode: false);
        SetVisible(false);
        glossaryPopup?.Hide();
        QueueScrollReset();
    }

    private void ShowInventoryItem(ScriptableObject definition, string subtitle, System.Action fallbackBinder)
    {
        ResolveReferences();
        EnsureServices();
        SetVisible(true);
        SetLegacyMode(weaponMode: false, bossMode: false);
        HideInventoryViews();

        currentDefinition = definition;
        currentHeaderLevelSuffix = string.Empty;
        ApplyItemIcon(definition);
        RefreshHeaderTitle();
        SetText(subtitleText, subtitle);
        SetText(metadataText, BuildInventoryMetadata(definition));
        SetText(storyText, string.Empty);
        SetText(weaponStatsText, string.Empty);
        SetText(weaponAbilitiesText, string.Empty);
        SetText(bossAffectionText, string.Empty);
        SetText(bossRewardText, string.Empty);
        glossaryPopup?.Hide();

        if (TryShowInventoryView(definition))
        {
            QueueScrollReset();
            return;
        }

        fallbackBinder?.Invoke();
        QueueScrollReset();
    }

    private bool TryShowInventoryView(ScriptableObject definition)
    {
        ItemDetailContext context = new();

        if (weaponViewV2 != null && weaponViewV2.CanShow(definition))
        {
            weaponViewV2.Show(definition, context, services);
            return true;
        }

        if (weaponView != null && weaponView.CanShow(definition))
        {
            weaponView.Show(definition, context, services);
            return true;
        }

        if (relicView != null && relicView.CanShow(definition))
        {
            relicView.Show(definition, context, services);
            return true;
        }

        if (consumableView != null && consumableView.CanShow(definition))
        {
            consumableView.Show(definition, context, services);
            return true;
        }

        return false;
    }

    private void HideInventoryViews()
    {
        if (weaponViewV2 != null)
            weaponViewV2.Hide();

        if (weaponView != null)
            weaponView.Hide();

        if (relicView != null)
            relicView.Hide();

        if (consumableView != null)
            consumableView.Hide();
    }

    private void ShowLegacyWeapon(string displayName, string itemId, Sprite icon, string stageText, WeaponDefinition weapon)
    {
        SetVisible(true);
        HideInventoryViews();
        SetLegacyMode(weaponMode: true, bossMode: false);
        SetImage(iconImage, icon);
        SetText(titleText, displayName);
        SetText(subtitleText, GameText.Get("code.encyclopediadetailpanel.8c4dac1fcc", "무기"));
        SetText(metadataText, BuildMetadata(
            FormatLabeledLine("ID", itemId),
            FormatLabeledLine(GameText.Get("code.encyclopediadetailpanel.082c060ddf", "등장 구역"), stageText)));
        SetText(storyText, weapon != null && !string.IsNullOrWhiteSpace(GameText.Asset(weapon, "storyText", weapon.storyText)) ? GameText.Asset(weapon, "storyText", weapon.storyText) : GameText.Get("code.encyclopediadetailpanel.c6793d9ba0", "설명 준비 중"));
        SetText(weaponStatsText, BuildWeaponStatsText(weapon));
        SetText(weaponAbilitiesText, BuildWeaponAbilitiesText(weapon));
    }

    private void ShowLegacyItem(IInventoryItemDefinition item, string subtitle, string itemId, string description)
    {
        SetVisible(true);
        HideInventoryViews();
        SetLegacyMode(weaponMode: false, bossMode: false);
        SetText(titleText, item != null ? item.DisplayName : string.Empty);
        SetText(subtitleText, subtitle);
        SetText(metadataText, FormatLabeledLine("ID", itemId));
        SetText(storyText, string.IsNullOrWhiteSpace(description) ? GameText.Get("code.encyclopediadetailpanel.c6793d9ba0", "설명 준비 중") : FormatText(description));
    }

    private void QueueScrollReset()
    {
        if (!resetScrollOnBind || detailScrollRect == null)
            return;

        if (!isActiveAndEnabled)
        {
            ApplyScrollReset();
            return;
        }

        if (pendingScrollReset != null)
            StopCoroutine(pendingScrollReset);

        pendingScrollReset = StartCoroutine(ResetScrollAfterLayout());
    }

    private IEnumerator ResetScrollAfterLayout()
    {
        yield return null;
        ApplyScrollReset();
        pendingScrollReset = null;
    }

    private void ApplyScrollReset()
    {
        if (detailScrollRect == null)
            return;

        Canvas.ForceUpdateCanvases();
        if (detailScrollRect.vertical)
            detailScrollRect.verticalNormalizedPosition = 1f;
        if (detailScrollRect.horizontal)
            detailScrollRect.horizontalNormalizedPosition = 0f;
    }

    private void SetVisible(bool visible)
    {
        SetActive(contentRoot, visible);
        SetActive(emptyRoot, !visible);
    }

    private void SetLegacyMode(bool weaponMode, bool bossMode)
    {
        SetActive(weaponDetailRoot, weaponMode);
        SetActive(bossAffectionRoot, bossMode);
    }

    private void EnsureServices()
    {
        services ??= new ItemDetailPanelServices
        {
            glossary = glossary,
            formatText = FormatText,
            showGlossary = ShowGlossaryPopup,
            setHeaderLevelText = SetHeaderLevelSuffix
        };

        services.glossary = glossary;
        services.formatText = FormatText;
        services.showGlossary = ShowGlossaryPopup;
        services.setHeaderLevelText = SetHeaderLevelSuffix;
    }

    private string FormatText(string raw)
    {
        return DetailTextFormatter.Format(raw, tooltipColorPalette, glossaryLinkColorHex);
    }

    private void ShowGlossaryPopup(string key)
    {
        if (glossaryPopup == null)
            return;

        if (glossary != null && glossary.TryGet(key, out string description))
            glossaryPopup.Show(key, description);
        else
            glossaryPopup.Show(key, GameText.Get("code.encyclopediadetailpanel.c6793d9ba0", "설명 준비 중"));
    }

    private void SetHeaderLevelSuffix(string text)
    {
        currentHeaderLevelSuffix = text ?? string.Empty;
        RefreshHeaderTitle();
    }

    private void RefreshHeaderTitle()
    {
        string baseTitle = currentDefinition != null ? currentDefinition.name : string.Empty;
        if (currentDefinition is IInventoryItemDefinition common)
            baseTitle = common.DisplayName;

        SetText(titleText, BuildHeaderTitle(baseTitle));
    }

    private string BuildHeaderTitle(string baseTitle)
    {
        if (string.IsNullOrWhiteSpace(currentHeaderLevelSuffix))
            return baseTitle ?? string.Empty;

        return $"{baseTitle} {currentHeaderLevelSuffix}";
    }

    private void ApplyItemIcon(ScriptableObject definition)
    {
        if (definition != null)
            ItemDisplayIconUtility.Apply(iconImage, definition, ItemDisplayIconContext.InventorySlot, iconDefaultState);
        else
            SetImage(iconImage, null);
    }

    private string BuildInventoryMetadata(ScriptableObject definition)
    {
        if (definition is IInventoryItemDefinition item)
            return FormatLabeledLine("ID", item.ItemId);

        return string.Empty;
    }

    private string BuildMetadata(params string[] lines)
    {
        builder.Clear();
        for (int i = 0; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;

            if (builder.Length > 0)
                builder.AppendLine();

            builder.Append(lines[i]);
        }

        return builder.ToString();
    }

    private string BuildWeaponStatsText(WeaponDefinition weapon)
    {
        if (weapon == null || weapon.statModifiers == null || weapon.statModifiers.Count == 0)
            return GameText.Get("code.encyclopediadetailpanel.e3f3e5b105", "스탯 변경 없음");

        builder.Clear();
        for (int i = 0; i < weapon.statModifiers.Count; i++)
        {
            WeaponDefinition.WeaponStatModifier modifier = weapon.statModifiers[i];
            string label = !string.IsNullOrWhiteSpace(modifier.labelOverride)
                ? modifier.labelOverride
                : modifier.attribute != null ? modifier.attribute.DisplayName : "Unknown";
            string value = FormatModifierValue(modifier.type, modifier.value);
            builder.Append(label).Append(": ").Append(value);

            if (i < weapon.statModifiers.Count - 1)
                builder.AppendLine();
        }

        return builder.ToString();
    }

    private string BuildWeaponAbilitiesText(WeaponDefinition weapon)
    {
        if (weapon == null)
            return GameText.Get("code.encyclopediadetailpanel.9c17813ec1", "스킬 데이터 없음");

        builder.Clear();
        AppendAbility(builder, GameText.Get("code.encyclopediadetailpanel.065f292f33", "기본 공격"), weapon.attackInputHint, weapon.GetAbility(WeaponAbilitySlot.Attack));
        AppendAbility(builder, GameText.Get("code.encyclopediadetailpanel.a091200702", "스킬 1"), weapon.skill1InputHint, weapon.GetAbility(WeaponAbilitySlot.Skill1));
        AppendAbility(builder, GameText.Get("code.encyclopediadetailpanel.01d28c5504", "스킬 2"), weapon.skill2InputHint, weapon.GetAbility(WeaponAbilitySlot.Skill2));

        return builder.Length > 0 ? builder.ToString() : GameText.Get("code.encyclopediadetailpanel.9c17813ec1", "스킬 데이터 없음");
    }

    private static void AppendAbility(StringBuilder target, string slotLabel, string inputHint, AbilityDefinition ability)
    {
        if (ability == null)
            return;

        if (target.Length > 0)
            target.AppendLine().AppendLine();

        target.Append(slotLabel);
        if (!string.IsNullOrWhiteSpace(inputHint))
            target.Append(" (").Append(inputHint).Append(')');

        target.Append(": ").Append(string.IsNullOrWhiteSpace(ability.DisplayName) ? ability.name : ability.DisplayName);

        if (ability.cooldown > 0f)
            target.Append(" / ").Append(ability.cooldown.ToString("0.##", CultureInfo.InvariantCulture)).Append("s");

        if (!string.IsNullOrWhiteSpace(ability.Description))
            target.AppendLine().Append(ability.Description);
    }

    private string BuildBossAffectionText(NPCData npcData)
    {
        if (npcData == null)
            return GameText.Get("code.encyclopediadetailpanel.3f92db343c", "호감도 데이터 미연결");

        int currentAffection = AffectionManager.Instance != null
            ? AffectionManager.Instance.GetAffection(npcData.id)
            : 0;

        return GameText.Format("code.encyclopediadetailpanel.0ecfc0b16e", "현재 호감도: {0}", currentAffection);
    }

    private string BuildBossRewardText(NPCData npcData)
    {
        if (npcData == null)
            return GameText.Get("code.encyclopediadetailpanel.1557bfa715", "보상 데이터 미연결");

        if (npcData.affectionRewards == null || npcData.affectionRewards.Count == 0)
            return GameText.Get("code.encyclopediadetailpanel.5df09cdf01", "등록된 호감도 보상 없음");

        int currentAffection = AffectionManager.Instance != null
            ? AffectionManager.Instance.GetAffection(npcData.id)
            : 0;

        builder.Clear();
        for (int i = 0; i < npcData.affectionRewards.Count; i++)
        {
            AffectionReward reward = npcData.affectionRewards[i];
            string state = currentAffection >= reward.targetLevel ? GameText.Get("code.encyclopediadetailpanel.d1496ce82d", "해금") : GameText.Get("code.encyclopediadetailpanel.b35f488f01", "미해금");
            string rewardText = reward.effect != null && !string.IsNullOrWhiteSpace(reward.effect.RewardText)
                ? reward.effect.RewardText
                : GameText.Get("code.encyclopediadetailpanel.00333329dd", "보상 설명 준비 중");

            builder.Append('[').Append(state).Append("] ")
                .Append(reward.targetLevel).Append(": ")
                .Append(rewardText);

            if (i < npcData.affectionRewards.Count - 1)
                builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string GetDisplayName(WeaponDefinition weapon)
    {
        if (weapon == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(weapon.DisplayName))
            return weapon.DisplayName;

        return !string.IsNullOrWhiteSpace(weapon.weaponId) ? weapon.weaponId : weapon.name;
    }

    private static string FormatLabeledLine(string label, string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : $"{label}: {value}";
    }

    private static string FormatModifierValue(ModifierType type, float value)
    {
        if (type == ModifierType.Percent)
            return FormatSigned(value * 100f) + "%";

        return FormatSigned(value);
    }

    private static string FormatSigned(float value)
    {
        string sign = value > 0f ? "+" : string.Empty;
        return sign + value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static void SetImage(Image image, Sprite sprite)
    {
        if (image == null)
            return;

        image.sprite = sprite;
        image.enabled = sprite != null;
        image.preserveAspect = true;
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
}
