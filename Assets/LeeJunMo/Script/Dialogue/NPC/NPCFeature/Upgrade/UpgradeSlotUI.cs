using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UpgradeSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Data")]
    public UpgradeNodeSO assignedNode;

    [Header("UI")]
    public TextMeshProUGUI priceText;
    public Image iconImage;
    public Button buyButton;
    public Image lockIcon;
    public GameObject purchasedCheckMark;

    public void InitSlot(System.Action<UpgradeNodeSO> onBuy)
    {
        if (assignedNode == null)
        {
            gameObject.SetActive(false);
            return;
        }

        RefreshUI();

        buyButton.onClick.RemoveAllListeners();
        buyButton.onClick.AddListener(() => onBuy?.Invoke(assignedNode));
    }

    public void RefreshUI()
    {
        if (assignedNode == null)
            return;

        priceText.text = assignedNode.price.ToString();
        if (assignedNode.icon != null)
            iconImage.sprite = assignedNode.icon;

        LockType status = LockType.Locked;
        if (UpgradeManager.Instance != null)
            status = UpgradeManager.Instance.GetNodeStatus(assignedNode.nodeID);

        switch (status)
        {
            case LockType.Purchased:
                buyButton.interactable = false;
                if (lockIcon) lockIcon.enabled = false;
                if (purchasedCheckMark) purchasedCheckMark.SetActive(true);
                iconImage.color = Color.gray;
                break;

            case LockType.UnLocked:
                buyButton.interactable = true;
                if (lockIcon) lockIcon.enabled = false;
                if (purchasedCheckMark) purchasedCheckMark.SetActive(false);
                iconImage.color = Color.white;
                break;

            case LockType.Locked:
                buyButton.interactable = false;
                if (lockIcon) lockIcon.enabled = true;
                if (purchasedCheckMark) purchasedCheckMark.SetActive(false);
                iconImage.color = new Color(0.3f, 0.3f, 0.3f);
                break;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        MouseCursorService.EnsureInstance().SetInteractable(this, assignedNode != null);

        if (assignedNode == null || UIManager.Instance == null || UpgradeTooltip.Instance == null)
            return;

        RectTransform slotRect = transform as RectTransform;
        if (slotRect == null)
            return;

        UIManager.Instance.ShowHover(UpgradeTooltip.Instance, slotRect, assignedNode);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        MouseCursorService.EnsureInstance().SetInteractable(this, false);

        if (UIManager.Instance == null || UpgradeTooltip.Instance == null)
            return;

        RectTransform slotRect = transform as RectTransform;
        if (slotRect == null)
            return;

        UIManager.Instance.HideHover(UpgradeTooltip.Instance, slotRect);
    }

    private void OnDisable()
    {
        MouseCursorService.Instance?.SetInteractable(this, false);
    }
}
