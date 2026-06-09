using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 책임 :
/// - 플레이어 스탯 패널의 한 줄 비주얼(아이콘, 라벨, 값)을 표시한다.
/// - 상위 패널이 계산한 표시 문자열을 받아 화면 요소에 반영한다.
/// </summary>
public sealed class PlayerStatRowView : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private TMP_Text valueText;

    public void Set(StatInfoUIDefinition definition, string value)
    {
        if (definition == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        if (iconImage != null)
        {
            iconImage.sprite = definition.Icon;
            iconImage.enabled = definition.Icon != null;
        }

        if (labelText != null)
            labelText.text = definition.Label;

        if (valueText != null)
            valueText.text = value;
    }
}
