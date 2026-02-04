using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class UnlockSlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI References")]
    [SerializeField] private Image iconImage;
    [SerializeField] private RectTransform slotRect; // UIHoverManager 위치 계산용

    // 툴팁을 띄우기 위해 원본 데이터(SO)를 보관
    private ScriptableObject assignedItem;

    private void Awake()
    {
        // RectTransform 캐싱
        if (slotRect == null) slotRect = transform as RectTransform;
    }

    /// <summary>
    /// 외부(ResultUI)에서 데이터를 받아서 세팅하는 함수
    /// </summary>
    public void Setup(ScriptableObject itemDef)
    {
        this.assignedItem = itemDef;

        // 기존 ItemSlotUI에서 쓰시던 방식대로 AsDef() 확장 메서드를 사용하거나 캐스팅
        // (사용하시는 프로젝트의 확장 메서드 구조에 맞춰주세요)
        var def = itemDef.AsDef();

        if (def != null && def.Icon != null)
        {
            iconImage.sprite = def.Icon;
            iconImage.enabled = true;
        }
        else
        {
            // 아이콘이 없으면 투명하게 처리
            iconImage.enabled = false;
        }
    }

    // ---------------------------------------------------------
    // [핵심] 인벤토리와 동일한 UIHoverManager 호출
    // ---------------------------------------------------------
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (assignedItem == null) return;

        // 인벤토리에서 쓰는 그 매니저를 그대로 호출합니다.
        // 드래그 중이 아닐 때만 띄웁니다.
        if (UIHoverManager.Instance != null)
        {
            UIHoverManager.Instance.HoverSlot(slotRect, assignedItem);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // 마우스가 나가면 툴팁 끄기 요청
        if (UIHoverManager.Instance != null)
        {
            UIHoverManager.Instance.UnhoverSlot(slotRect);
        }
    }
}