using UnityEngine.EventSystems;
using UnityEngine;
using UnityEngine.UI;
using System;
using TMPro;

public interface IItemContainer
{
    int SlotCount { get; }
    event Action OnChanged;

    /// <summary>슬롯의 아이템(WeaponDefinition / RelicDefinition / null)</summary>
    ScriptableObject Get(int index);

    /// <summary>
    /// 해당 슬롯에 item을 놓을 수 있는지(타입/중복/규칙 체크).
    /// ignoreIndex는 “내부 구현에서 필요한 경우”만 쓰고 기본은 -1.
    /// </summary>
    bool CanPlace(ScriptableObject item, int index, int ignoreIndex = -1);

    bool TrySet(int index, ScriptableObject item);
    bool TrySwap(int a, int b);
}
public interface IRelicLevelProvider
{
    bool TryGetRelicLevel(int index, out int level);
}

public interface IRelicSlotReceiver
{
    bool TrySetRelicWithLevel(int index, RelicDefinition relic, int level);
}

public class ItemSlotUI : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("UI")]
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI levelText;
    private IItemContainer container;
    private int index;
    [SerializeField] private RectTransform slotRect; // 없으면 Awake에서 transform as RectTransform

    private void Awake()
    {
        if (slotRect == null) slotRect = transform as RectTransform;
    }
    private void OnDisable()
    {
        if (container != null)
            container.OnChanged -= Refresh;
    }

    public void Bind(IItemContainer container, int index)
    {
        if (this.container != null)
            this.container.OnChanged -= Refresh;

        this.container = container;
        this.index = index;

        if (this.container != null)
            this.container.OnChanged += Refresh;

        Refresh();
    }

    public void Refresh()
    {
        if (container == null || icon == null) return;

        var so = container.Get(index);
        var def = so.AsDef();

        if (def == null || def.Icon == null)
        {
            icon.enabled = false;
            icon.sprite = null;
        }
        else
        {
            icon.enabled = true;
            icon.sprite = def.Icon;
        }
        if (so is RelicDefinition && container is IRelicLevelProvider p && p.TryGetRelicLevel(index, out var lvl))
        {
            levelText.gameObject.SetActive(true);
            levelText.text = $"Lv {lvl}";
        }
        else levelText.gameObject.SetActive(false);

    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (container == null) return;
        ItemDetailPanel.Instance?.Hide(); // ✅ 드래그 시작하면 패널 숨김
        UIHoverManager.Instance?.HideImmediate();
        var so = container.Get(index);
        if (so == null) return;

        var def = so.AsDef();
        if (def == null) return;

        int relicLevel = 0;
        if (so is RelicDefinition && container is IRelicLevelProvider p)
            p.TryGetRelicLevel(index, out relicLevel);

        ItemDragContext.Begin(container, index, so, relicLevel);

        DragIcon.Instance?.Show(def.Icon);
        DragIcon.Instance?.Follow(eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!ItemDragContext.Active) return;
        DragIcon.Instance?.Follow(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        DragIcon.Instance?.Hide();
        ItemDragContext.Clear();
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (container == null) return;
        if (!ItemDragContext.Active) return;

        // source -> target 처리 (Swap 방식)
        ItemDragContext.TryDrop(container, index);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (container == null) return;

        // Right click -> Quick Move
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            TryQuickMove();
            return;
        }
    }


    private void TryQuickMove()
    {
        if (container == null) return;

        var so = container.Get(index);
        if (so == null) return;

        var def = so.AsDef();
        if (def == null) return;

        var chest = ItemContainerGroupRegistry.Chest;
        var w = ItemContainerGroupRegistry.WeaponEquip;
        var r = ItemContainerGroupRegistry.RelicEquip;

        // 그룹이 설정되지 않았으면 아무것도 안함
        if (chest == null || w == null || r == null) return;

        IItemContainer target = null;

        // 1) 장착 슬롯에서 -> 상자(=Backpack)
        if (container == w || container == r)
        {
            target = chest;
        }
        // 2) 상자(=Backpack)에서 -> 타입에 맞는 장착 슬롯
        else if (container == chest)
        {
            target = def.Kind == InventoryItemKind.Weapon ? w : r;
        }
        // 3) 주변 루트(loot)에서 -> 상자(=Backpack)
        else if (container is WorldLootContainerAdapter)
        {
            target = chest;
        }

        if (target == null) return;

        int targetIndex = FindFirstEmptyIndex(target, so);
        if (targetIndex < 0) return;

        // 드롭(스왑 로직) 재활용
        int relicLevel = 0;
        if (so is RelicDefinition && container is IRelicLevelProvider p)
            p.TryGetRelicLevel(index, out relicLevel);

        ItemDragContext.Begin(container, index, so, relicLevel);
        ItemDragContext.TryDrop(target, targetIndex);
        DragIcon.Instance?.Hide();
        ItemDragContext.Clear();
    }

    private static int FindFirstEmptyIndex(IItemContainer target, ScriptableObject moving)
    {
        for (int i = 0; i < target.SlotCount; i++)
        {
            if (target.Get(i) != null) continue; // 빈 칸만
            if (!target.CanPlace(moving, i)) continue;
            return i;
        }
        return -1;
    }

    // 슬롯 간 이동 시 "Exit -> Enter" 순서로 훅이 들어오면서 패널이 깜빡일 수 있어서
    // 한 프레임 지연 후 Hide를 시도하는 토큰 방식
    private static int s_hoverSerial = 0;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (container == null) return;
        if (ItemDragContext.Active) return;

        var so = container.Get(index);
        //if (so == null)
        //{
        //    UIHoverManager.Instance?.HideImmediate();
        //    return;
        //}

        UIHoverManager.Instance?.HoverSlot(slotRect, so, container, index);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (ItemDragContext.Active) return;
        UIHoverManager.Instance?.UnhoverSlot(slotRect);
    }


    private System.Collections.IEnumerator HideNextFrame(int serialAtExit)
    {
        yield return null; // 1프레임 대기: 다른 슬롯 Enter가 같은 프레임에 오면 Hide 방지

        if (ItemDragContext.Active) yield break;

        // Exit 이후 다른 슬롯 Enter가 발생했으면(Serial 증가) Hide하지 않음
        if (s_hoverSerial != serialAtExit) yield break;

        ItemDetailPanel.Instance?.Hide();
    }

}
