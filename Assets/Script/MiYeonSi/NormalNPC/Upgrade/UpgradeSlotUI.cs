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
    public Image lockIcon; // 잠김/구매완료 표시용 아이콘
    public GameObject purchasedCheckMark; // (선택) 구매 완료 체크 표시

    // 초기화
    public void InitSlot(System.Action<UpgradeNodeSO> onBuy)
    {
        if (assignedNode == null)
        {
            gameObject.SetActive(false);
            return;
        }

        RefreshUI();

        // 버튼 이벤트 연결
        buyButton.onClick.RemoveAllListeners();
        buyButton.onClick.AddListener(() => onBuy?.Invoke(assignedNode));
    }

    // [핵심] 상태 갱신 로직
    public void RefreshUI()
    {
        if (assignedNode == null) return;

        // 1. 기본 정보 표시
        priceText.text = assignedNode.price.ToString();
        if (assignedNode.icon != null) iconImage.sprite = assignedNode.icon;

        // 2. 매니저에게 현재 내 상태(LockType)를 물어봄
        // (매니저가 SaveData를 보고 계산해서 알려줌)
        LockType status = UpgradeManager.Instance.GetNodeStatus(assignedNode.nodeID);

        // 3. 상태에 따른 UI 처리
        switch (status)
        {
            case LockType.Purchased:
                // 이미 구매함 -> 버튼 비활성
                buyButton.interactable = false;
                if (lockIcon) lockIcon.enabled = false;
                if (purchasedCheckMark) purchasedCheckMark.SetActive(true); // 체크 표시 켜기

                // 혹은 아이콘 색을 어둡게 처리
                iconImage.color = Color.gray;
                break;

            case LockType.UnLocked:
                // 구매 가능 -> 버튼 활성
                buyButton.interactable = true;
                if (lockIcon) lockIcon.enabled = false; // 자물쇠 끄기
                if (purchasedCheckMark) purchasedCheckMark.SetActive(false);

                iconImage.color = Color.white;
                break;

            case LockType.Locked:
                // 잠김 (조건 불충족) -> 버튼 비활성
                buyButton.interactable = false;
                if (lockIcon) lockIcon.enabled = true; // 자물쇠 켜기
                if (purchasedCheckMark) purchasedCheckMark.SetActive(false);

                iconImage.color = new Color(0.3f, 0.3f, 0.3f); // 아주 어둡게
                break;
        }
    }

    // 마우스 올렸을 때 툴팁 등 처리
    public void OnPointerEnter(PointerEventData eventData)
    {
        // 예: TooltipManager.Show(assignedNode.upgradeName, assignedNode.description);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // 예: TooltipManager.Hide();
    }
}