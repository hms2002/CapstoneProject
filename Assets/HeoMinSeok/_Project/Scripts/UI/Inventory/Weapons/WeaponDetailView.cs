using System.Text;
using UnityEngine;
using UnityGAS;
using UnityGAS.Sample;

public class WeaponDetailView : MonoBehaviour, IItemDetailView
{
    [SerializeField] private SectionListView sections;

    public bool CanShow(object def) => def is WeaponDefinition;

    public void Show(object def, ItemDetailContext ctx, ItemDetailPanelServices services)
    {
        gameObject.SetActive(true);
        sections?.Clear();

        var w = (WeaponDefinition)def;

        // ✅ 상단: 무기 설명 + 장착 시 적용되는 스탯
        AddWeaponSummarySection(w, services);

        AddAbilitySection("일반공격", w.attack, DamageAttackKind.Normal, ctx, services, w.attackInputHint);
        AddAbilitySection("스킬 1", w.skill1, DamageAttackKind.Skill, ctx, services, w.skill1InputHint);
        AddAbilitySection("스킬 2", w.skill2, DamageAttackKind.Skill, ctx, services, w.skill2InputHint);
    }

    public void Hide()
    {
        sections?.Clear();
        gameObject.SetActive(false);
    }

    private void AddAbilitySection(string header, AbilityDefinition ability, DamageAttackKind kind, ItemDetailContext ctx, ItemDetailPanelServices services, string inputHint)
    {
        if (sections == null) return;
        if (ability == null) return;

        string body = BuildAbilityBody(ability, kind, ctx, inputHint);
        if (services != null && services.formatText != null)
            body = services.formatText(body);

        sections.Add(header, body, services != null ? services.showGlossary : null);

    }

    private void AddWeaponSummarySection(WeaponDefinition w, ItemDetailPanelServices services)
    {
        if (sections == null || w == null) return;

        var sb = new StringBuilder();

        if (!string.IsNullOrEmpty(w.description))
            sb.AppendLine(w.description);

        if (w.statModifiers != null && w.statModifiers.Count > 0)
        {
            if (sb.Length > 0) sb.AppendLine();
            sb.AppendLine("<b>능력치</b>");

            for (int i = 0; i < w.statModifiers.Count; i++)
            {
                var e = w.statModifiers[i];
                if (e.attribute == null) continue;

                string label = !string.IsNullOrEmpty(e.labelOverride)
                    ? e.labelOverride
                    : (!string.IsNullOrEmpty(e.attribute.attributeName) ? e.attribute.attributeName : e.attribute.name);

                string valueText;
                if (e.type == ModifierType.Percent)
                {
                    // Percent는 +0.1 => +10%
                    valueText = $"+{e.value * 100f:0.#}%";
                }
                else
                {
                    valueText = $"+{e.value:0.##}";
                }

                sb.AppendLine($"- {label} <color=#FFD54F>{valueText}</color>");
            }
        }

        string body = sb.ToString().TrimEnd();
        if (services != null && services.formatText != null)
            body = services.formatText(body);

        if (!string.IsNullOrEmpty(body))
            sections.Add("요약", body, services != null ? services.showGlossary : null);
    }

    private string BuildAbilityBody(AbilityDefinition ad, DamageAttackKind kind, ItemDetailContext ctx, string inputHint)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"<b>{ad.abilityName}</b>");
        if (!string.IsNullOrEmpty(ad.description))
            sb.AppendLine(ad.description);

        if (!string.IsNullOrEmpty(inputHint))
            sb.AppendLine($"입력: <color=#A7E1FF>{inputHint}</color>");

        if (ad.cooldown > 0f)
            sb.AppendLine($"쿨다운: <color=#FFD54F>{ad.cooldown:0.##}s</color>");

        // Tags (brief)
        if (ad.abilityTags != null && ad.abilityTags.Count > 0)
            sb.AppendLine($"태그: {JoinTags(ad.abilityTags)}");

        // SourceObject details (best-effort, sample types supported)
        if (ad.sourceObject != null)
        {
            sb.AppendLine();
            sb.AppendLine("<b>상세</b>");
            AppendSourceObjectDetails(sb, ad.sourceObject, kind, ctx);
        }

        return sb.ToString().TrimEnd();
    }
    private void AppendSourceObjectDetails(StringBuilder sb, Object sourceObj, DamageAttackKind kind, ItemDetailContext ctx)
    {
        if (sourceObj == null)
        {
            sb.AppendLine("(추가 정보 없음)");
            return;
        }

        // ✅ 너가 이미 가진 방식: sourceObject가 스스로 블록 제공
        if (sourceObj is IDetailProvider provider)
        {
            var block = provider.BuildDetailBlock(ctx);

            // block.title은 선택: 본문에 소제목처럼 넣고 싶으면 사용
            if (!string.IsNullOrEmpty(block.title))
                sb.AppendLine($"<color=#A7E1FF>{block.title}</color>");

            if (!string.IsNullOrEmpty(block.body))
                sb.AppendLine(block.body);
            else
                sb.AppendLine("(추가 정보 없음)");

            return;
        }

        // fallback
        sb.AppendLine($"{sourceObj.name}");
        sb.AppendLine("(디테일 제공 인터페이스 미구현)");
    }

    private string JoinTags(System.Collections.Generic.List<GameplayTag> tags)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < tags.Count; i++)
        {
            if (tags[i] == null) continue;
            if (sb.Length > 0) sb.Append(", ");
            sb.Append(tags[i].ToString());
        }
        return sb.ToString();
    }

    // (Damage preview moved to per-sourceObject IDetailProvider implementations.)
}
