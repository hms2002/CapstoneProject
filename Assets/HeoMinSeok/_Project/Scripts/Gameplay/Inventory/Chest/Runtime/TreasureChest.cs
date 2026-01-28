using UnityEngine;

public class TreasureChest : MonoBehaviour
{
    [Header("Chest Data (Manual Fill)")]
    public int capacity = 16;
    public ScriptableObject[] initialItems; // WeaponDefinition or RelicDefinition

    private ChestInventory inventory;

    private void Awake()
    {
        inventory = new ChestInventory(capacity);

        // 수동 채우기(테스트)
        if (initialItems != null)
        {
            for (int i = 0; i < initialItems.Length && i < capacity; i++)
                inventory.Set(i, initialItems[i]);
        }
    }

    public ChestInventory GetInventory() => inventory;

    public void Open()
    {
        ChestUIManager.Instance.OpenChest(this);
    }
}
