using System.Text;
using UnityEngine;

/// <summary>
/// 책임 :
/// - 1회용 아이템 정의를 detail panel용 섹션 텍스트로 변환한다.
/// - 설명과 사용 효과를 기존 무기/유물 detail view와 같은 형식으로 출력한다.
/// </summary>
public class ConsumableDetailView : MonoBehaviour, IItemDetailView
{
    [SerializeField] private SectionListView sections;

    public bool CanShow(object def) => def is ConsumableDefinition;

    public void Show(object def, ItemDetailContext ctx, ItemDetailPanelServices services)
    {
        gameObject.SetActive(true);
        sections?.Clear();

        var consumable = (ConsumableDefinition)def;

        if (!string.IsNullOrEmpty(consumable.description))
        {
            var desc = services.formatText != null
                ? services.formatText(consumable.description)
                : consumable.description;
            sections?.Add("설명", desc, services.showGlossary);
        }

        string effect = BuildEffectText(consumable);
        effect = services.formatText != null ? services.formatText(effect) : effect;
        sections?.Add("효과", effect, services.showGlossary);
    }

    public void Hide()
    {
        sections?.Clear();
        gameObject.SetActive(false);
    }

    private static string BuildEffectText(ConsumableDefinition consumable)
    {
        var sb = new StringBuilder();

        if (consumable.TargetAttribute != null && consumable.RestoreAmount > 0)
        {
            sb.AppendLine($"- [[{consumable.TargetAttribute.attributeName}]] {consumable.RestoreAmount} 회복");
            sb.AppendLine("- 사용 시 1개 소모");
        }
        else
        {
            sb.AppendLine("(사용 효과 정보 없음)");
        }

        return sb.ToString().TrimEnd();
    }
}
