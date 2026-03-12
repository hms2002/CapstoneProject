using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityGAS;

/// <summary>
/// 첨부된 새 기획 레이아웃(상단 설명+스탯, 아래 능력 박스 3개)을 위한 무기 디테일 뷰.
///
/// ⚠️ 프리팹/레퍼런스는 인스펙터에서 연결해야 합니다.
/// - summaryText: 무기 설명(따옴표 박스 등)
/// - statRoot + statLinePrefab: "이동속도 +10%" 같은 줄들
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
            string text = w.description ?? "";
            if (services != null && services.formatText != null)
                text = services.formatText(text);
            summaryText.text = text;
        }

        // Stats
        BuildStatLines(w);

        // Abilities (order matches screenshot: 기본/스킬2/스킬1 등은 기획에 맞춰 바꿔도 됨)
        AddAbilityBlock("우클릭", w.attack, w.attackInputHint, ctx, services);
        AddAbilityBlock("Q", w.skill1, w.skill1InputHint, ctx, services);
        AddAbilityBlock("E", w.skill2, w.skill2InputHint, ctx, services);

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
                value = $"+{e.value * 100f:0.#}%";
            else
                value = $"+{e.value:0.##}";

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

        var view = Instantiate(abilityBlockPrefab, abilityRoot);
        view.Set(header, ability.icon, inputHint, ability.cooldown, body,
            services != null ? services.showGlossary : null);

        _spawnedAbilities.Add(view);
    }

    private string BuildAbilityBody(AbilityDefinition ad, ItemDetailContext ctx)
    {
        var sb = new StringBuilder();

        // 상단 타이틀/설명
        if (!string.IsNullOrEmpty(ad.abilityName))
            sb.AppendLine(ad.abilityName);
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
}
