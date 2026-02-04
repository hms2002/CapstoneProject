using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class UnlockSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image iconImage;
    private RectTransform _rect;
    private ScriptableObject _assignedItem;

    private void Awake() => _rect = transform as RectTransform;

    public void Setup(ScriptableObject itemDef)
    {
        _assignedItem = itemDef;
        // IInventoryItemDefinition 인터페이스를 통해 아이콘 추출
        var iItem = itemDef as IInventoryItemDefinition;
        if (iItem != null)
        {
            iconImage.sprite = iItem.Icon;
            iconImage.enabled = true;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_assignedItem != null && UIHoverManager.Instance != null)
        {
            UIHoverManager.Instance.HoverSlot(_rect, _assignedItem);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (UIHoverManager.Instance != null)
        {
            UIHoverManager.Instance.UnhoverSlot(_rect);
        }
    }
}