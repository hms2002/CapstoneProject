using System.Text;
using UnityEngine;
using UnityGAS;

public class RelicDetailView : MonoBehaviour, IItemDetailView
{
    [SerializeField] private SectionListView sections;

    public bool CanShow(object def) => def is RelicDefinition;

    public void Show(object def, ItemDetailContext ctx, ItemDetailPanelServices services)
    {
        gameObject.SetActive(true);
        sections?.Clear();

        var r = (RelicDefinition)def;

        // Level info (if the player already owns it)
        int level = 1;
        if (ctx != null && ctx.owner != null)
        {
            var inv = ctx.owner.GetComponent<RelicInventory>();
            if (inv != null && inv.TryGetRelicLevelById(r.relicId, out var ownedLevel))
                level = ownedLevel;
        }

        // Description
        if (!string.IsNullOrEmpty(r.description))
        {
            var desc = services.formatText != null ? services.formatText(r.description) : r.description;
            sections.Add("설명", desc, services.showGlossary);
        }

        // Upgrade / Level
        {
            var sb = new StringBuilder();
            sb.AppendLine($"현재 강화: +{level} / +{Mathf.Max(1, r.maxLevel)}");
            sb.AppendLine($"획득 시 강화량: +{Mathf.Max(1, r.dropLevel)}");
            sections.Add("강화", sb.ToString().TrimEnd(), services.showGlossary);
        }

        // Effects
        string effect = BuildEffectText(r, ctx, level);
        effect = services.formatText != null ? services.formatText(effect) : effect;
        sections.Add("효과", effect, services.showGlossary);
    }

    public void Hide()
    {
        sections?.Clear();
        gameObject.SetActive(false);
    }

    private static float EvalValue(float baseValue, System.Collections.Generic.List<float> table, int level)
    {
        if (level < 1) level = 1;
        if (table != null && table.Count > 0)
        {
            int idx = Mathf.Clamp(level - 1, 0, table.Count - 1);
            return table[idx];
        }
        return baseValue * level;
    }

    private string BuildEffectText(RelicDefinition r, ItemDetailContext ctx, int level)
    {
        var sb = new StringBuilder();

        if (r.logic == null)
        {
            sb.AppendLine("(로직 없음)");
            return sb.ToString().TrimEnd();
        }

        int maxLevel = Mathf.Max(1, r.maxLevel);
        int nextLevel = Mathf.Clamp(level + 1, 1, maxLevel);
        bool hasNext = nextLevel != level;

        // 1) Stat Modifiers
        if (r.logic is RelicLogic_StatModifiers mods)
        {
            if (mods.entries == null || mods.entries.Count == 0)
            {
                sb.AppendLine("(스탯 변경 없음)");
                return sb.ToString();
            }

            for (int i = 0; i < mods.entries.Count; i++)
            {
                var e = mods.entries[i];
                if (e.attribute == null) continue;

                float curV = EvalValue(e.value, e.valueByLevel, level);
                float nextV = hasNext ? EvalValue(e.value, e.valueByLevel, nextLevel) : curV;

                string name = e.attribute.attributeName;
                string type = e.type.ToString();

                string curStr = e.type == ModifierType.Percent ? $"{curV * 100f:0.##}%" : $"{curV:0.##}";
                string nextStr = e.type == ModifierType.Percent ? $"{nextV * 100f:0.##}%" : $"{nextV:0.##}";

                sb.Append($"- [[{name}]]: {type} {curStr}");
                if (hasNext && nextV != curV) sb.Append($"  →  <color=#90CAF9>{nextStr}</color>");
                if (e.duration > 0f) sb.Append($" ({e.duration:0.##}s)");

                // show current value if available
                if (ctx != null && ctx.attributeSet != null)
                {
                    float cur = ctx.attributeSet.GetAttributeValue(e.attribute);
                    sb.Append($"  | 현재: <color=#FFD54F>{cur:0.##}</color>");
                }

                sb.AppendLine();
            }

            return sb.ToString().TrimEnd();
        }

        // 2) Lightning proc
        if (r.logic is RelicLogic_LightningOnHitConfirmed_Managed lightning)
        {
            float curDmg = EvalValue(lightning.baseDamage, lightning.baseDamageByLevel, level);
            float nextDmg = hasNext ? EvalValue(lightning.baseDamage, lightning.baseDamageByLevel, nextLevel) : curDmg;

            sb.AppendLine("- 발동: HitConfirmed");
            sb.Append($"- 번개 피해: {curDmg:0.##}");
            if (hasNext && nextDmg != curDmg) sb.Append($"  →  <color=#90CAF9>{nextDmg:0.##}</color>");
            sb.AppendLine();

            sb.AppendLine($"- 반경: {lightning.radius:0.##}");
            if (lightning.cooldownSeconds > 0f) sb.AppendLine($"- 쿨다운: {lightning.cooldownSeconds:0.##}s");

            return sb.ToString().TrimEnd();
        }

        // Fallback: show linked param if exists
        if (r.param != null)
        {
            sb.AppendLine($"Param: {r.param.name}");
            sb.AppendLine();
        }

        sb.AppendLine("(이 유물 로직 타입에 대한 상세 표시를 추가할 수 있어요)");
        return sb.ToString().TrimEnd();
    }
}
