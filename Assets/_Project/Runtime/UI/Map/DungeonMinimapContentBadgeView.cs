using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Responsibility: project one content-kind count onto authored icon/count UI slots.</summary>
[DisallowMultipleComponent]
public sealed class DungeonMinimapContentBadgeView : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text countLabel;
    public RectTransform Rect => (RectTransform)transform;

    public bool Apply(int count, DungeonMinimapContentIconData style)
    {
        bool visible = count > 0 && style.Icon != null && icon != null;
        gameObject.SetActive(visible);
        if (icon != null)
        {
            icon.sprite = style.Icon;
            icon.color = style.Tint;
        }
        if (countLabel != null)
        {
            countLabel.gameObject.SetActive(visible && count > 1);
            if (count > 99)
                countLabel.SetText("99+");
            else
                countLabel.SetText("{0}", count);
        }
        return visible;
    }
}
