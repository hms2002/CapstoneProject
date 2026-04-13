using UnityEngine;
using System.Collections.Generic;

public class BossDrop : MonoBehaviour
{
    [Header("Chest Settings")]
    public GameObject chestPrefab;
    public Transform chestSpawnPoint;
    public GameObject portalObj;

    [Header("Boss Unique Loot")]
    public List<BossSpecificLoot> bossUniqueLoots;

    [Header("Currency Drop")]
    public GameObject magicStonePrefab;
    private bool hasProcessedDeath;

    private void Start()
    {
        if (portalObj != null)
        {
            portalObj.SetActive(false);
        }
    }

    public void OnBossDead()
    {
        if (hasProcessedDeath)
            return;

        hasProcessedDeath = true;
        // 1. 상자 생성
        SpawnTreasureChest();

        // 2. 마정석 낱개 드롭
        SpawnBossCurrency();

        ActivePortal();
    }

    private void SpawnTreasureChest()
    {
        Vector3 spawnPos = chestSpawnPoint != null ? chestSpawnPoint.position : transform.position;

        if (chestPrefab == null) return;

        GameObject chestObj = Instantiate(chestPrefab, spawnPos, Quaternion.identity);
        TreasureChest chest = chestObj.GetComponent<TreasureChest>();

        if (chest == null) return;

        List<ScriptableObject> finalLoots = new List<ScriptableObject>();

        // [수정] 플레이어 인벤토리를 뒤지던 banList 로직 완벽히 제거!

        if (LootManager.Instance != null)
        {
            // 매개변수 없이 깔끔하게 호출만 합니다.
            List<ScriptableObject> randomLoots = LootManager.Instance.GenerateChestLoot();
            if (randomLoots != null)
            {
                finalLoots.AddRange(randomLoots);
            }
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

        int count = 0;
        if (LootManager.Instance != null)
        {
            count = LootManager.Instance.GetBossMagicStoneCount();
        }

        if (count <= 0) return;

        for (int i = 0; i < count; i++)
        {
            Vector3 spawnPos = transform.position + (Vector3)(Random.insideUnitCircle * 1.5f);
            GameObject stoneObj = Instantiate(magicStonePrefab, spawnPos, Quaternion.identity);

            var pickup = stoneObj.GetComponent<MagicStonePickup>();
            if (pickup != null)
            {
                pickup.amount = 1;
            }
        }
    }

    private void ActivePortal()
    {
        if (portalObj == null)
        {
            Debug.LogWarning("[BossDrop] portalObj is not assigned, so no exit portal could be activated.", this);
            return;
        }

        Transform portalTransform = portalObj.transform;
        if (portalTransform != null && portalTransform.IsChildOf(transform))
            portalTransform.SetParent(null, true);

        portalObj.SetActive(true);
        RestorePortalVisibilityAndInteraction(portalObj);
    }

    private static void RestorePortalVisibilityAndInteraction(GameObject portalRoot)
    {
        if (portalRoot == null)
            return;

        Renderer[] renderers = portalRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = true;
        }

        Collider2D[] colliders = portalRoot.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = true;
        }

        ScenePortal[] scenePortals = portalRoot.GetComponentsInChildren<ScenePortal>(true);
        for (int i = 0; i < scenePortals.Length; i++)
        {
            if (scenePortals[i] != null)
                scenePortals[i].enabled = true;
        }
    }
}
