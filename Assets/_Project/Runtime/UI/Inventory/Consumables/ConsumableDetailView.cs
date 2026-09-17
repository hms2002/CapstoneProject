using System.Text;
using UnityEngine;

/// <summary>
/// 책임 :
/// - 1회용 아이템 정의를 detail panel용 섹션 텍스트로 변환한다.
/// - 작성된 설명 문구를 효과 섹션에 한 번만 출력한다.
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
        bool hasRestoreEffect = consumable.TargetAttribute != null && consumable.RestoreAmount > 0;

        if (!string.IsNullOrWhiteSpace(consumable.description))
        {
            sb.AppendLine(consumable.description.Trim());
        }
        else if (hasRestoreEffect)
        {
            sb.AppendLine($"● [[{consumable.TargetAttribute.attributeName}]] {{pos:[+{consumable.RestoreAmount}]}} 회복");
        }
        else
        {
            sb.AppendLine("(사용 효과 정보 없음)");
        }

        return sb.ToString().TrimEnd();
    }
}
