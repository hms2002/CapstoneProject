using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RelicSlotView : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;
    [SerializeField] private GameObject emptyOverlay;

    private int index;
    private Action<int> onClick;

    public void Bind(int index, Action<int> onClick)
    {
        this.index = index;
        this.onClick = onClick;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => this.onClick?.Invoke(this.index));
    }

    public void SetContent(RelicDefinition def)
    {
        bool has = def != null;

        if (iconImage != null)
        {
            iconImage.enabled = has && def.icon != null;
            iconImage.sprite = has ? def.icon : null;
        }

        if (emptyOverlay != null)
            emptyOverlay.SetActive(!has);

        // 슬롯 자체는 항상 클릭 가능하게 두고,
        // 비어있으면 "아무것도 없음"으로 상세패널 띄우거나 무시하면 됨.
    }
}
