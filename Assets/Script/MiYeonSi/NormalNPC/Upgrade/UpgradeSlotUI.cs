using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UpgradeSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("데이터 연결")]
    public UpgradeNodeSO assignedNode; // 에디터/런타임에서 주입됨

    [Header("UI 컴포넌트")]
    public TextMeshProUGUI priceText;
    public Image iconImage;
    public Button buyButton;
    public Image lockIcon;
    public GameObject purchasedCheckMark;

    // 초기화
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

    // 상태 갱신 로직
    public void RefreshUI()
    {
        if (assignedNode == null) return;

        // 1. 기본 정보 표시
        priceText.text = assignedNode.price.ToString();
        if (assignedNode.icon != null) iconImage.sprite = assignedNode.icon;

        // 2. 매니저에게 상태 확인
        LockType status = LockType.Locked;
        if (UpgradeManager.Instance != null)
        {
            status = UpgradeManager.Instance.GetNodeStatus(assignedNode.nodeID);
        }

        // 3. 상태에 따른 UI 처리
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

    // =========================================================
    // [툴팁 연동 부분]
    // =========================================================
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (assignedNode == null) return;

        // 툴팁 매니저가 있다면 호출
        if (UpgradeTooltip.Instance != null)
        {
            // 제목, 내용, 그리고 '이 슬롯의 위치(transform.position)'를 넘겨줍니다.
            UpgradeTooltip.Instance.Show(
                assignedNode.upgradeName,
                assignedNode.description,
                transform.position
            );
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // 마우스 나가면 숨김
        if (UpgradeTooltip.Instance != null)
        {
            UpgradeTooltip.Instance.Hide();
        }
    }
}