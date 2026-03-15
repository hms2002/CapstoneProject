using UnityEngine;
using System.Collections.Generic;

public class TreasureChest : MonoBehaviour
{
    private ChestInventory inventory;

    private bool isOpened = false;
    private bool isGenerated = false;

    public int capacity = 16;

    private void Awake()
    {
        inventory = new ChestInventory();
    }

    public void InitializeWithLoot(List<ScriptableObject> loots)
    {
        if (inventory == null)
            inventory = new ChestInventory();

        HashSet<string> banList = GetPlayerWeaponBanList();

        foreach (var item in loots)
        {
            if (item is WeaponDefinition weapon)
            {
                if (banList.Contains(weapon.weaponId))
                    continue;
            }

            inventory.TryAdd(item);
        }

        isGenerated = true;
    }

    public void Open()
    {
        if (!isGenerated)
        {
            GenerateSelfLoot();
            isGenerated = true;
        }

        if (!isOpened)
        {
            isOpened = true;
            // TODO: animator.SetTrigger("Open");
        }

        if (ChestUIManager.Instance != null)
        {
            ChestUIManager.Instance.OpenChest(this);
        }
    }

    private void GenerateSelfLoot()
    {
        HashSet<string> currentBanList = GetPlayerWeaponBanList();

        if (LootManager.Instance != null)
        {
            List<ScriptableObject> loots = LootManager.Instance.GenerateChestLoot(currentBanList);

            foreach (var item in loots)
            {
                inventory.TryAdd(item);
            }
        }
    }

    private HashSet<string> GetPlayerWeaponBanList()
    {
        HashSet<string> banList = new HashSet<string>();

        if (SampleTopDownPlayer.Instance == null)
            return banList;

        var weaponInventory = SampleTopDownPlayer.Instance.GetComponent<WeaponInventory2D>();
        if (weaponInventory == null)
            return banList;

        List<string> playerWeaponIDs = weaponInventory.GetAllWeaponIDs();
        banList.UnionWith(playerWeaponIDs);

        return banList;
    }

    public ChestInventory GetInventory() => inventory;
}