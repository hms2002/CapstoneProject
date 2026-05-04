using UnityEngine.EventSystems;
using UnityEngine;
using UnityEngine.UI;
using System;
using TMPro;

public interface IItemContainer
{
    int SlotCount { get; }
    event Action OnChanged;
    ScriptableObject Get(int index);
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

/// <summary>
/// 책임 : 개별 인벤토리 슬롯 UI를 표시하고 drag, drop, quick move 입력을 컨테이너 동작으로 변환한다.
/// </summary>
public class ItemSlotUI : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("UI")]
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI levelText;
    private IItemContainer container;
    private int index;
    [SerializeField] private RectTransform slotRect;

    public RectTransform SlotRect
    {
        get
        {
            if (slotRect == null)
                slotRect = transform as RectTransform;

            return slotRect;
        }
    }

    public ScriptableObject CurrentItem => container != null ? container.Get(index) : null;
    public bool HasItem => CurrentItem != null;
    public bool HasEpicItem => CurrentItem is RelicDefinition relic && relic.rarity == ItemRarity.Epic;

    private void Awake()
    {
        if (slotRect == null)
            slotRect = transform as RectTransform;
    }
    private void OnDisable()
    {
        ItemDragContext.CancelActiveDragSession();
        MouseCursorService.Instance?.SetDragging(this, false);
        MouseCursorService.Instance?.SetInteractable(this, false);

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

        // [수정] 드래그 시작 시 호버 패널 숨김 (UIManager 활용)
        if (UIManager.Instance != null) UIManager.Instance.HideHoverImmediate();

        var so = container.Get(index);
        if (so == null) return;

        var def = so.AsDef();
        if (def == null) return;

        int relicLevel = 0;
        if (so is RelicDefinition && container is IRelicLevelProvider p)
            p.TryGetRelicLevel(index, out relicLevel);

        ItemDragContext.Begin(container, index, so, relicLevel);
        MouseCursorService.EnsureInstance().SetInteractable(this, false);
        MouseCursorService.EnsureInstance().SetDragging(this, true);

        DropZoneUI.ActiveInstance?.Show();
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
        ItemDragContext.CancelActiveDragSession();
        MouseCursorService.EnsureInstance().SetDragging(this, false);
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (container == null) return;
        if (!ItemDragContext.Active) return;

        ItemDragContext.TryDrop(container, index);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (container == null) return;

        if (eventData.button == PointerEventData.InputButton.Right)
        {
            TryQuickMove();
            return;
        }
    }

    // 책임 : 우클릭 빠른 이동 시 아이템 종류와 현재 컨테이너에 따라
    // 가장 자연스러운 대상 컨테이너와 슬롯을 선택해 이동을 시도한다.
    private void TryQuickMove()
    {
        if (container == null) return;

        var so = container.Get(index);
        if (so == null) return;

        var def = so.AsDef();
        if (def == null) return;

        var chest = ItemContainerGroupRegistry.Chest;
        var c = ItemContainerGroupRegistry.ConsumableEquip;
        var w = ItemContainerGroupRegistry.WeaponEquip;
        var r = ItemContainerGroupRegistry.RelicEquip;

        if (c == null || w == null || r == null) return;

        IItemContainer target = null;
        int targetIndex = -1;

        if (container == chest && chest != null)
        {
            if (def.Kind == InventoryItemKind.Consumable)
            {
                target = c;
                targetIndex = FindFirstEmptyIndex(target, so);
            }
            else if (def.Kind == InventoryItemKind.Weapon)
            {
                target = w;
                targetIndex = FindFirstEmptyIndex(target, so);
            }
            else if (so is RelicDefinition relic)
            {
                target = r;
                targetIndex = FindRelicQuickMoveIndex(target, relic);
            }
            else
            {
                target = r;
                targetIndex = FindFirstEmptyIndex(target, so);
            }
        }
        else if (container is WorldLootContainerAdapter)
        {
            if (def.Kind == InventoryItemKind.Consumable)
            {
                target = c;
                targetIndex = FindFirstEmptyIndex(target, so);
            }
            else if (def.Kind == InventoryItemKind.Weapon)
            {
                target = w;
                targetIndex = FindFirstEmptyIndex(target, so);
            }
            else if (so is RelicDefinition relic)
            {
                target = r;
                targetIndex = FindRelicQuickMoveIndex(target, relic);
            }
        }
        else if (container == c && chest != null)
        {
            target = chest;
            targetIndex = FindFirstEmptyIndex(target, so);
        }
        else if (container == w && chest != null)
        {
            target = chest;
            targetIndex = FindFirstEmptyIndex(target, so);
        }
        else if (container == r && chest != null)
        {
            target = chest;
            targetIndex = FindFirstEmptyIndex(target, so);
        }

        if (target == null) return;
        if (targetIndex < 0)
        {
            ShowQuickMoveFailureWarning(so, target);
            return;
        }

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
            if (target.Get(i) != null) continue;
            if (!target.CanPlace(moving, i)) continue;
            return i;
        }
        return -1;
    }
    // 책임 : 컨테이너 안에서 같은 relicId를 가진 슬롯 인덱스를 찾는다.
    private static int FindSameRelicIndex(IItemContainer target, RelicDefinition relic)
    {
        if (target == null || relic == null) return -1;

        for (int i = 0; i < target.SlotCount; i++)
        {
            var existing = target.Get(i) as RelicDefinition;
            if (existing == null) continue;
            if (existing.relicId != relic.relicId) continue;
            return i;
        }

        return -1;
    }
    // 책임 : 비어 있지 않아도 "드롭 시도용"으로 사용할 수 있는 슬롯을 찾는다.
    // 동일 유물 슬롯 자체를 target으로 잡으면 merge 조건이 어긋날 수 있으므로 제외할 슬롯을 받을 수 있다.
    private static int FindAnyPlaceableIndex(IItemContainer target, ScriptableObject moving, int excludeIndex = -1)
    {
        if (target == null) return -1;

        for (int i = 0; i < target.SlotCount; i++)
        {
            if (i == excludeIndex) continue;
            if (!target.CanPlace(moving, i)) continue;
            return i;
        }

        return -1;
    }
    // 책임 : chest -> 플레이어 relic 인벤토리 quick move 시
    // 빈 슬롯이 있으면 빈 슬롯으로,
    // 빈 슬롯이 없고 동일 유물이 있으면 merge가 일어날 수 있는 "다른" 슬롯을 반환한다.
    private static int FindRelicQuickMoveIndex(IItemContainer target, RelicDefinition relic)
    {
        if (target == null || relic == null) return -1;

        int emptyIndex = FindFirstEmptyIndex(target, relic);
        if (emptyIndex >= 0)
            return emptyIndex;

        int sameRelicIndex = FindSameRelicIndex(target, relic);
        if (sameRelicIndex >= 0)
            return FindAnyPlaceableIndex(target, relic, excludeIndex: sameRelicIndex);

        return -1;
    }

    /// <summary>
    /// 책임 :
    /// - 빠른 이동 대상 슬롯을 찾지 못했을 때 아이템 종류에 맞는 공통 경고 팝업을 요청한다.
    /// - 인벤토리 가득 참 같은 조용한 실패를 사용자에게 즉시 피드백한다.
    /// </summary>
    private static void ShowQuickMoveFailureWarning(ScriptableObject item, IItemContainer target)
    {
        if (item == null || target == null || UIManager.Instance == null)
            return;

        WarningPopupCode code = item switch
        {
            WeaponDefinition => WarningPopupCode.WeaponInventoryFull,
            RelicDefinition => WarningPopupCode.RelicInventoryFull,
            ConsumableDefinition => WarningPopupCode.ConsumableInventoryFull,
            _ => WarningPopupCode.None
        };

        if (code != WarningPopupCode.None)
            UIManager.Instance.ShowWarning(code);
    }
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (container == null) return;
        if (ItemDragContext.Active) return;

        var so = container.Get(index);
        if (so == null)
        {
            MouseCursorService.EnsureInstance().SetInteractable(this, false);
            if (UIManager.Instance != null) UIManager.Instance.HideHoverImmediate();
            return;
        }

        MouseCursorService.EnsureInstance().SetInteractable(this, true);

        // [수정] HoverController에게 띄우라고 요청
        if (ItemHoverController.Instance != null)
        {
            ItemHoverController.Instance.HoverSlot(slotRect, so, container, index);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        MouseCursorService.EnsureInstance().SetInteractable(this, false);

        if (ItemDragContext.Active) return;

        // [수정] HoverController에게 끄라고 요청
        if (ItemHoverController.Instance != null)
        {
            ItemHoverController.Instance.UnhoverSlot(slotRect);
        }
    }
}
