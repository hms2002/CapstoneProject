using UnityEngine;

public class TreasureChest : MonoBehaviour
{
    [Header("Chest Data (Manual Fill)")]
    public int capacity = 16;
    public WeaponDefinition[] initialWeapons; // 테스트용

    private ChestInventory inventory;

    private void Awake()
    {
        inventory = new ChestInventory(capacity);

        // 수동 채우기(테스트)
        if (initialWeapons != null)
        {
            for (int i = 0; i < initialWeapons.Length && i < capacity; i++)
                inventory.Set(i, initialWeapons[i]);
        }
    }

    public ChestInventory GetInventory() => inventory;

    public void Open()
    {
        ChestUIManager.Instance.OpenChest(this);
    }
}
