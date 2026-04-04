using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityGAS;

/// <summary>
/// 책임 :
/// 첨부된 새 기획 레이아웃(상단 설명+스탯, 아래 능력 박스 3개)을 위한 무기 디테일 뷰.
///
/// ⚠️ 프리팹/레퍼런스는 인스펙터에서 연결해야 합니다.
/// - summaryText: 무기 설명(따옴표 박스 등)
/// - statRoot + statLinePrefab: "이동속도 [+10%]" 같은 정형화된 줄들
/// - abilityRoot + abilityBlockPrefab: 일반/스킬1/스킬2 박스
///
/// 본문 텍스트는 기존과 동일하게 IDetailProvider(BuildDetailBlock)로부터 받아옵니다.
/// </summary>
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

    // spawned caches
    private readonly List<WeaponStatLineView> _spawnedStats = new();
    private readonly List<WeaponAbilityBlockView> _spawnedAbilities = new();

    public bool CanShow(object def) => def is WeaponDefinition;

    public void Show(object def, ItemDetailContext ctx, ItemDetailPanelServices services)
    {
        gameObject.SetActive(true);
        Clear();

        var w = (WeaponDefinition)def;

        // Summary
        if (summaryText != null)
        {
            string text = string.IsNullOrWhiteSpace(w.storyText) ? w.description ?? "" : w.storyText;
            if (services != null && services.formatText != null)
                text = services.formatText(text);
            summaryText.text = text;
        }

        // Stats
        BuildStatLines(w);

        // Abilities
        AddAbilityBlock("스킬 1", w.skill1, w.skill1InputHint, ctx, services);
        AddAbilityBlock("스킬 2", w.skill2, w.skill2InputHint, ctx, services);

        Canvas.ForceUpdateCanvases();
    }

    public void Hide()
    {
        Clear();
        gameObject.SetActive(false);
    }

    private void Clear()
    {
        // stats
        for (int i = 0; i < _spawnedStats.Count; i++)
            if (_spawnedStats[i] != null) Destroy(_spawnedStats[i].gameObject);
        _spawnedStats.Clear();

        // abilities
        for (int i = 0; i < _spawnedAbilities.Count; i++)
            if (_spawnedAbilities[i] != null) Destroy(_spawnedAbilities[i].gameObject);
        _spawnedAbilities.Clear();
    }

    private void BuildStatLines(WeaponDefinition w)
    {
        if (statRoot == null || statLinePrefab == null || w == null) return;

        if (w.tooltipStats != null && w.tooltipStats.Count > 0)
        {
            for (int i = 0; i < w.tooltipStats.Count; i++)
            {
                var lineData = w.tooltipStats[i];
                if (string.IsNullOrWhiteSpace(lineData.label))
                    continue;

                var line = Instantiate(statLinePrefab, statRoot);
                line.Set(lineData.label, FormatTooltipValue(lineData.value, lineData.isPercent));
                _spawnedStats.Add(line);
            }

            return;
        }

        if (w.tooltipStatLines != null && w.tooltipStatLines.Count > 0)
        {
            for (int i = 0; i < w.tooltipStatLines.Count; i++)
            {
                var lineText = w.tooltipStatLines[i];
                if (string.IsNullOrWhiteSpace(lineText))
                    continue;

                var line = Instantiate(statLinePrefab, statRoot);
                line.Set(lineText, string.Empty);
                _spawnedStats.Add(line);
            }

            return;
        }

        if (w.statModifiers == null) return;

        for (int i = 0; i < w.statModifiers.Count; i++)
        {
            var e = w.statModifiers[i];
            if (e.attribute == null) continue;

            string label = !string.IsNullOrEmpty(e.labelOverride)
                ? e.labelOverride
                : (!string.IsNullOrEmpty(e.attribute.attributeName) ? e.attribute.attributeName : e.attribute.name);

            string value;
            if (e.type == ModifierType.Percent)
                value = FormatTooltipValue(e.value, true);
            else
                value = FormatTooltipValue(e.value, false);

            var line = Instantiate(statLinePrefab, statRoot);
            line.Set(label, value);
            _spawnedStats.Add(line);
        }
    }

    private void AddAbilityBlock(string header, AbilityDefinition ability, string inputHint, ItemDetailContext ctx, ItemDetailPanelServices services)
    {
        if (abilityRoot == null || abilityBlockPrefab == null) return;
        if (ability == null) return;

        string body = BuildAbilityBody(ability, ctx);
        if (services != null && services.formatText != null)
            body = services.formatText(body);

        string displayHeader = !string.IsNullOrEmpty(ability.abilityName) ? ability.abilityName : header;
        var view = Instantiate(abilityBlockPrefab, abilityRoot);
        view.Set(displayHeader, ability.icon, inputHint, ability.cooldown, "-", body,
            services != null ? services.showGlossary : null);

        _spawnedAbilities.Add(view);
    }

    private string BuildAbilityBody(AbilityDefinition ad, ItemDetailContext ctx)
    {
        var sb = new StringBuilder();

        // 상단 설명
        if (!string.IsNullOrEmpty(ad.description))
            sb.AppendLine(ad.description);

        // 상세(DetailProvider)
        if (ad.sourceObject is IDetailProvider provider)
        {
            var block = provider.BuildDetailBlock(ctx);
            if (!string.IsNullOrEmpty(block.body))
            {
                if (sb.Length > 0) sb.AppendLine();
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
