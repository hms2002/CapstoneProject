using UnityEngine;
using UnityEngine.EventSystems;

public class UIHoverArea : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public void OnPointerEnter(PointerEventData eventData)
    {
        UIHoverManager.Instance?.HoverPanel();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        UIHoverManager.Instance?.UnhoverPanel();
    }
}
