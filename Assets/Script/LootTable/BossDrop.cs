using UnityEngine;
using System.Collections.Generic;

public class BossDrop : MonoBehaviour
{
    [Header("Chest Settings")]
    public GameObject chestPrefab;
    public Transform chestSpawnPoint;

    [Header("Boss Unique Loot")]
    public List<BossSpecificLoot> bossUniqueLoots;

    [Header("Currency Drop")]
    public GameObject magicStonePrefab;

    public void OnBossDead()
    {
        // 1. 상자 생성
        SpawnTreasureChest();

        // 2. 마정석 낱개 드롭 (수정됨)
        SpawnBossCurrency();
    }

    private void SpawnTreasureChest()
    {
        // ... (기존 상자 생성 코드와 동일) ...
        Vector3 spawnPos = chestSpawnPoint != null ? chestSpawnPoint.position : transform.position;

        if (chestPrefab == null) return;

        GameObject chestObj = Instantiate(chestPrefab, spawnPos, Quaternion.identity);
        TreasureChest chest = chestObj.GetComponent<TreasureChest>();

        if (chest == null) return;

        List<ScriptableObject> finalLoots = new List<ScriptableObject>();

        HashSet<string> banList = new HashSet<string>();
        if (SampleTopDownPlayer.Instance != null && SampleTopDownPlayer.Instance.weaponInventory != null)
        {
            List<string> playerWeaponIDs = SampleTopDownPlayer.Instance.weaponInventory.GetAllWeaponIDs();
            banList.UnionWith(playerWeaponIDs);
        }

        if (LootManager.Instance != null)
        {
            List<ScriptableObject> randomLoots = LootManager.Instance.GenerateChestLoot(banList);
            finalLoots.AddRange(randomLoots);
        }

        foreach (var entry in bossUniqueLoots)
        {
            if (Random.Range(0, 100) < entry.dropChance)
            {
                if (entry.item != null) finalLoots.Add(entry.item);
            }
        }

        chest.InitializeWithLoot(finalLoots);
    }

    private void SpawnBossCurrency()
    {
        if (magicStonePrefab == null) return;

        // 1. 개수 가져오기 (5, 10, 15...)
        int count = 0;
        if (LootManager.Instance != null)
        {
            count = LootManager.Instance.GetBossMagicStoneCount();
        }

        if (count <= 0) return;

        // 2. 개수만큼 반복해서 생성 (번들 아님, 낱개 생성)
        for (int i = 0; i < count; i++)
        {
            // 위치를 약간씩 랜덤하게 흩뿌림
            Vector3 spawnPos = transform.position + (Vector3)(Random.insideUnitCircle * 1.5f);

            GameObject stoneObj = Instantiate(magicStonePrefab, spawnPos, Quaternion.identity);

            // 마정석 1개 오브젝트 = 가치 1
            // (MagicStonePickup의 기본 amount가 1이라면 Setup 호출 안 해도 됨, 명시적으로 하려면 아래 코드 사용)
            var pickup = stoneObj.GetComponent<MagicStonePickup>();
            if (pickup != null)
            {
                pickup.amount = 1;
            }
        }
    }
}