using TMPro;
using UnityEngine;

public class WeaponStatLineView : MonoBehaviour
{
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private TMP_Text valueText;

    public void Set(string label, string value)
    {
        if (labelText != null) labelText.text = label;
        if (valueText != null) valueText.text = value;
    }
}
