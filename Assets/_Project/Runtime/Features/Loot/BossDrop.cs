using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임:
/// - 오래된 보스 보상 드롭 컴포넌트의 씬 직렬화 참조를 보존한다.
/// - 현재 LootManager/TreasureChest API를 사용해 레거시 OnBossDead 호출을 최소 호환 처리한다.
/// </summary>
public sealed class BossDrop : MonoBehaviour
{
    [Header("Chest Settings")]
    public GameObject chestPrefab;
    public Transform chestSpawnPoint;

    [Header("Boss Unique Loot")]
    public List<BossSpecificLoot> bossUniqueLoots = new();

    [Header("Currency Drop")]
    public GameObject magicStonePrefab;

    public void OnBossDead()
    {
        SpawnTreasureChest();
        SpawnBossCurrency();
    }

    private void SpawnTreasureChest()
    {
        if (chestPrefab == null)
            return;

        Vector3 spawnPosition = chestSpawnPoint != null ? chestSpawnPoint.position : transform.position;
        GameObject chestObject = Instantiate(chestPrefab, spawnPosition, Quaternion.identity);
        TreasureChest chest = chestObject.GetComponent<TreasureChest>();
        if (chest == null)
            return;

        List<ScriptableObject> finalLoots = LootManager.Instance != null
            ? LootManager.Instance.GenerateBossChestLoot()
            : new List<ScriptableObject>();

        AddRolledBossUniqueLoots(finalLoots);
        chest.InitializeWithLoot(finalLoots);
    }

    private void AddRolledBossUniqueLoots(List<ScriptableObject> finalLoots)
    {
        if (finalLoots == null || bossUniqueLoots == null)
            return;

        for (int i = 0; i < bossUniqueLoots.Count; i++)
        {
            BossSpecificLoot entry = bossUniqueLoots[i];
            if (entry.item != null && Random.Range(0, 100) < entry.dropChance)
                finalLoots.Add(entry.item);
        }
    }

    private void SpawnBossCurrency()
    {
        int count = LootManager.Instance != null ? LootManager.Instance.GetBossMagicStoneCount() : 0;
        if (count <= 0)
            return;

        for (int i = 0; i < count; i++)
        {
            Vector3 spawnPosition = ResolveScatterPosition();
            if (LootManager.Instance != null)
            {
                LootManager.Instance.SpawnMagicStonePickup(spawnPosition, 1);
                continue;
            }

            if (magicStonePrefab == null)
                continue;

            GameObject stoneObject = Instantiate(magicStonePrefab, spawnPosition, Quaternion.identity);
            if (stoneObject.TryGetComponent(out MagicStonePickup pickup))
                pickup.amount = 1;
        }
    }

    private Vector3 ResolveScatterPosition()
    {
        Vector2 offset = Random.insideUnitCircle * 1.5f;
        return transform.position + new Vector3(offset.x, offset.y, 0f);
    }
}
