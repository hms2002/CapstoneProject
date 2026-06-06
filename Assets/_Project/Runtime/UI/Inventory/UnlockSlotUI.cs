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
        // [수정] ItemHoverController 사용
        if (_assignedItem != null && ItemHoverController.Instance != null)
        {
            ItemHoverController.Instance.HoverSlot(_rect, _assignedItem);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // [수정] ItemHoverController 사용
        if (ItemHoverController.Instance != null)
        {
            ItemHoverController.Instance.UnhoverSlot(_rect);
        }
    }
}