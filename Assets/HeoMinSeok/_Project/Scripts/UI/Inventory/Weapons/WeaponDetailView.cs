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

        AddAbilitySection("일반공격", w.attack, DamageAttackKind.Normal, ctx, services);
        AddAbilitySection("스킬 1", w.skill1, DamageAttackKind.Skill, ctx, services);
        AddAbilitySection("스킬 2", w.skill2, DamageAttackKind.Skill, ctx, services);
    }

    public void Hide()
    {
        sections?.Clear();
        gameObject.SetActive(false);
    }

    private void AddAbilitySection(string header, AbilityDefinition ability, DamageAttackKind kind, ItemDetailContext ctx, ItemDetailPanelServices services)
    {
        if (sections == null) return;
        if (ability == null) return;

        string body = BuildAbilityBody(ability, kind, ctx);
        if (services != null && services.formatText != null)
            body = services.formatText(body);

        sections.Add(header, body, services != null ? services.showGlossary : null);

    }

    private string BuildAbilityBody(AbilityDefinition ad, DamageAttackKind kind, ItemDetailContext ctx)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"<b>{ad.abilityName}</b>");
        if (!string.IsNullOrEmpty(ad.description))
            sb.AppendLine(ad.description);

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

    private void AppendDamagePreview(
        StringBuilder sb,
        DamageFormulaStats stats,
        DamageAttackKind kind,
        float baseHp,
        float baseStagger,
        bool includeElement,
        bool includeStagger,
        ItemDetailContext ctx)
    {
        // If we can compute with player stats, show non-crit & crit.
        if (ctx != null && ctx.attributeSet != null && stats != null)
        {
            var noCrit = DamageFormulaUtil.Compute(ctx.attributeSet, stats, kind, baseHp, baseStagger, includeElement, includeStagger, critRoll01: 1f, forceCrit: false);
            var crit  = DamageFormulaUtil.Compute(ctx.attributeSet, stats, kind, baseHp, baseStagger, includeElement, includeStagger, critRoll01: 0f, forceCrit: true);

            sb.AppendLine($"기본 피해: {baseHp:0.##}");
            sb.AppendLine($"예상 피해: <color=#FFD54F>{noCrit.hpDamage:0}</color> (치명타 <color=#FFB3C7>{crit.hpDamage:0}</color>)");

            if (includeElement) sb.AppendLine($"속성 피해: {noCrit.elementDamage:0} (치명타 {crit.elementDamage:0})");
            if (includeStagger) sb.AppendLine($"무력화: {noCrit.staggerDamage:0} (치명타 {crit.staggerDamage:0})");
        }
        else
        {
            sb.AppendLine($"기본 피해: {baseHp:0.##}");
            sb.AppendLine("(예상 피해 계산을 위해 플레이어 AttributeSet / DamageFormulaStats가 필요)");
        }
    }
}
