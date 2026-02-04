using UnityEngine;
using System.Collections.Generic;

public class TreasureChest : MonoBehaviour
{
    private ChestInventory inventory;

    // 상자 뚜껑이 열려있는지 여부 (애니메이션/스프라이트 제어용)
    // 로직(UI 열기)을 막는 용도가 아님!
    private bool isOpened = false;

    // 아이템 데이터가 생성되었는지 여부 (중복 파밍 방지용)
    private bool isGenerated = false;

    public int capacity = 16;

    private void Awake()
    {
        inventory = new ChestInventory(/*capacity*/);
    }

    // =========================================================
    // 1. 외부(보스 등)에서 아이템을 받을 때
    // =========================================================
    public void InitializeWithLoot(List<ScriptableObject> loots)
    {
        if (inventory == null) inventory = new ChestInventory(/*capacity*/);

        HashSet<string> banList = GetPlayerWeaponBanList();

        foreach (var item in loots)
        {
            if (item is WeaponDefinition weapon)
            {
                if (banList.Contains(weapon.weaponId)) continue;
            }
            inventory.TryAdd(item);
        }

        isGenerated = true;
    }

    // =========================================================
    // 2. 상호작용으로 열 때 (수정됨: 언제든 다시 열 수 있음)
    // =========================================================
    public void Open()
    {
        // [삭제됨] if (isOpened) return; 
        // -> 이 줄 때문에 다시 못 열었던 겁니다. 과감히 삭제!

        // 1. 데이터 생성 (아직 안 만들어졌을 때만 1회 실행)
        // -> 다시 열 때는 isGenerated가 true이므로 실행 안 됨 (내용물 보존)
        if (!isGenerated)
        {
            GenerateSelfLoot();
            isGenerated = true;
        }

        // 2. 비주얼 처리 (처음 열 때만 애니메이션 재생)
        if (!isOpened)
        {
            isOpened = true;
            // TODO: 여기서 애니메이션 재생 (예: animator.SetTrigger("Open"))
            // Debug.Log("상자 뚜껑이 열립니다!");
        }

        // 3. UI 열기 (항상 실행)
        // -> 이미 열린 상자라도 다시 누르면 인벤토리 창이 뜸
        if (ChestUIManager.Instance != null)
        {
            ChestUIManager.Instance.OpenChest(this);
        }
    }

    // =========================================================
    // 3. 스스로 아이템 생성
    // =========================================================
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
        if (SampleTopDownPlayer.Instance != null && SampleTopDownPlayer.Instance.weaponInventory != null)
        {
            List<string> playerWeaponIDs = SampleTopDownPlayer.Instance.weaponInventory.GetAllWeaponIDs();
            banList.UnionWith(playerWeaponIDs);
        }
        return banList;
    }

    public ChestInventory GetInventory() => inventory;
}