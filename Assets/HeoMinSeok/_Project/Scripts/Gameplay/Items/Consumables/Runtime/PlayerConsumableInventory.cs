using System;
using UnityEngine;

/// <summary>
/// 책임 :
/// - 플레이어의 1회용 아이템 4칸 인벤토리를 관리하고 획득, 제거, 사용 흐름을 제공한다.
/// - 장착형 아이템과 분리된 고정 슬롯 인벤토리로 동작한다.
/// </summary>
public class PlayerConsumableInventory : MonoBehaviour
{
    public event Action OnChanged;

    [Header("Slots")]
    [SerializeField] private ConsumableDefinition[] slots = new ConsumableDefinition[4];

    public int Capacity => slots != null ? slots.Length : 0;
    public int SlotCount => Capacity;

    public static PlayerConsumableInventory GetOrAdd(Transform owner)
    {
        if (owner == null)
            return null;

        var inventory = owner.GetComponent<PlayerConsumableInventory>();
        return inventory != null ? inventory : owner.gameObject.AddComponent<PlayerConsumableInventory>();
    }

    public ConsumableDefinition GetConsumableInSlot(int slotIndex)
        => IsValidSlot(slotIndex) ? slots[slotIndex] : null;

    public bool TryAcquire(ConsumableDefinition consumable)
    {
        if (consumable == null)
            return false;

        int emptyIndex = FindFirstEmptySlot();
        if (emptyIndex < 0)
            return false;

        slots[emptyIndex] = consumable;
        OnChanged?.Invoke();
        return true;
    }

    public bool TryUseAt(int slotIndex)
    {
        if (!IsValidSlot(slotIndex))
            return false;

        ConsumableDefinition consumable = slots[slotIndex];
        if (consumable == null)
            return false;

        if (!consumable.TryUse(gameObject))
            return false;

        slots[slotIndex] = null;
        OnChanged?.Invoke();
        return true;
    }

    public bool CanPlaceConsumableInSlot(int slotIndex, ConsumableDefinition consumable)
    {
        if (!IsValidSlot(slotIndex))
            return false;

        return consumable == null || consumable.Kind == InventoryItemKind.Consumable;
    }

    public bool TrySetConsumableSlot(int slotIndex, ConsumableDefinition newConsumable)
    {
        if (!CanPlaceConsumableInSlot(slotIndex, newConsumable))
            return false;

        if (slots[slotIndex] == newConsumable)
            return true;

        slots[slotIndex] = newConsumable;
        OnChanged?.Invoke();
        return true;
    }

    public bool TrySwapConsumableSlots(int a, int b)
    {
        if (!IsValidSlot(a) || !IsValidSlot(b))
            return false;

        if (a == b)
            return true;

        (slots[a], slots[b]) = (slots[b], slots[a]);
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 책임 :
    /// - 현재 플레이어의 consumable 슬롯 배치를 저장용 DTO로 캡처한다.
    /// - 씬 이동 직전 소지 중인 1회용 아이템 구성을 보존하는 공식 창구다.
    /// </summary>
    public ConsumableInventoryState CaptureInventoryState()
    {
        var state = new ConsumableInventoryState
        {
            slots = new ConsumableSlotState[Capacity]
        };

        for (int i = 0; i < Capacity; i++)
        {
            var consumable = slots[i];
            state.slots[i] = new ConsumableSlotState
            {
                consumableId = consumable != null ? consumable.consumableId : null
            };
        }

        return state;
    }

    /// <summary>
    /// 책임 :
    /// - 저장된 consumable 슬롯 배치를 effect 없이 현재 플레이어에 복원한다.
    /// - 사용/소모 상태만 다루며 추가 효과는 발생시키지 않는다.
    /// </summary>
    public void RestoreShellState(
        ConsumableInventoryState state,
        Func<string, ConsumableDefinition> consumableResolver)
    {
        for (int i = 0; i < Capacity; i++)
            slots[i] = null;

        if (state == null || state.slots == null || consumableResolver == null)
        {
            OnChanged?.Invoke();
            return;
        }

        int copyCount = Mathf.Min(Capacity, state.slots.Length);
        for (int i = 0; i < copyCount; i++)
        {
            var entry = state.slots[i];
            if (entry == null || string.IsNullOrEmpty(entry.consumableId))
                continue;

            slots[i] = consumableResolver(entry.consumableId);
        }

        OnChanged?.Invoke();
    }

    private int FindFirstEmptySlot()
    {
        for (int i = 0; i < Capacity; i++)
        {
            if (slots[i] == null)
                return i;
        }

        return -1;
    }

    private bool IsValidSlot(int slotIndex)
        => slotIndex >= 0 && slotIndex < Capacity;
}
