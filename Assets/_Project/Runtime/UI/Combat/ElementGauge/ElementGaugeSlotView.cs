using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityGAS;

public class ElementGaugeSlotView : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Image fillImage;
    [SerializeField] private TMP_Text percentText;

    public void SetData(in ElementGaugeUiModel model)
    {
        if (iconImage != null)
            iconImage.sprite = model.Icon;

        if (fillImage != null)
            fillImage.fillAmount = model.Ratio;

        if (percentText != null)
            percentText.text = Mathf.RoundToInt(model.Ratio * 100f) + "%";

        gameObject.SetActive(model.Visible);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}