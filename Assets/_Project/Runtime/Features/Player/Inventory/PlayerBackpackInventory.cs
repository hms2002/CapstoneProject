using System;
using UnityEngine;

/// <summary>
/// 책임 :
/// - 플레이어가 무기와 유물을 함께 보관할 수 있는 런타임 가방 인벤토리를 제공한다.
/// - 실제 슬롯 저장/교환 동작은 ChestInventory에 위임하고, 플레이어 소유 컴포넌트로 변경 이벤트를 중계한다.
/// </summary>
public class PlayerBackpackInventory : MonoBehaviour
{
    [Header("Bag")]
    [SerializeField] private int capacity = 16;

    [SerializeField] private ChestInventory inventory;

    public event Action OnChanged;

    public int Capacity => inventory != null ? inventory.Capacity : 0;

    public ChestInventory Inventory => inventory;

    private void Awake()
    {
        if (inventory == null || inventory.Capacity != Mathf.Max(0, capacity))
            inventory = new ChestInventory();

        inventory.OnChanged += HandleChanged;
    }

    private void OnDestroy()
    {
        if (inventory != null)
            inventory.OnChanged -= HandleChanged;
    }

    private void HandleChanged()
    {
        OnChanged?.Invoke();
    }

    public ScriptableObject Get(int index)
    {
        return inventory != null ? inventory.Get(index) : null;
    }

    public bool Set(int index, ScriptableObject item)
    {
        return inventory != null && inventory.Set(index, item);
    }

    public bool Swap(int a, int b)
    {
        return inventory != null && inventory.Swap(a, b);
    }

    public int GetRelicLevelInSlot(int index)
    {
        return inventory != null ? inventory.GetRelicLevelInSlot(index) : 0;
    }

    public bool SetRelicWithLevel(int index, RelicDefinition relic, int level)
    {
        return inventory != null && inventory.SetRelicWithLevel(index, relic, level);
    }

    public bool TryAdd(ScriptableObject item)
    {
        if (inventory == null || item == null)
            return false;

        if (!inventory.TryFindEmpty(out int index))
            return false;

        return inventory.Set(index, item);
    }
}
