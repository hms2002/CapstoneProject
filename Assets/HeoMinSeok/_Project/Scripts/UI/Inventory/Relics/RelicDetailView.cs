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

        // Description
        if (!string.IsNullOrEmpty(r.description))
        {
            var desc = services.formatText != null ? services.formatText(r.description) : r.description;
            sections.Add("설명", desc, services.showGlossary);
        }

        // Effects
        string effect = BuildEffectText(r, ctx);
        effect = services.formatText != null ? services.formatText(effect) : effect;
        sections.Add("효과", effect, services.showGlossary);
    }

    public void Hide()
    {
        sections?.Clear();
        gameObject.SetActive(false);
    }

    private string BuildEffectText(RelicDefinition r, ItemDetailContext ctx)
    {
        var sb = new StringBuilder();

        if (r.logic == null)
        {
            sb.AppendLine("(로직 없음)");
            return sb.ToString();
        }

        sb.AppendLine($"로직: {r.logic.GetType().Name}");

        // Stat modifier generic
        if (r.logic is RelicLogic_StatModifiers mods)
        {
            if (mods.entries == null || mods.entries.Count == 0)
            {
                sb.AppendLine("(스탯 변경 없음)");
                return sb.ToString();
            }

            sb.AppendLine();
            for (int i = 0; i < mods.entries.Count; i++)
            {
                var e = mods.entries[i];
                if (e.attribute == null) continue;

                string name = e.attribute.attributeName;
                string type = e.type.ToString();

                string valueStr = e.type == ModifierType.Percent
                    ? $"{e.value * 100f:0.#}%"
                    : $"{e.value:0.##}";

                sb.Append($"- [[{name}]]: {type} {valueStr}");

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

        // Fallback: show linked param if exists
        if (r.param != null)
        {
            sb.AppendLine();
            sb.AppendLine($"Param: {r.param.name}");
        }

        sb.AppendLine();
        sb.AppendLine("(이 유물 로직 타입에 대한 상세 표시를 추가할 수 있어요)");
        return sb.ToString().TrimEnd();
    }
}
