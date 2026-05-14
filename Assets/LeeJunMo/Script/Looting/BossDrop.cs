using System.Collections.Generic;
using UnityEngine;

public class BossDrop : MonoBehaviour
{
    [Header("Chest Settings")]
    public GameObject chestPrefab;
    public Transform chestSpawnPoint;

    [Header("Portal Settings")]
    public GameObject portalObj;
    public Transform portalSpawnPoint;
    public Vector3 portalSpawnOffset;

    [Header("Boss Unique Loot")]
    public List<BossSpecificLoot> bossUniqueLoots;

    [Header("Currency Drop")]
    public GameObject magicStonePrefab;

    private bool hasProcessedDeath;

    private void Start()
    {
        if (portalObj != null)
            portalObj.SetActive(false);
    }

    public void OnBossDead()
    {
        if (hasProcessedDeath)
            return;

        hasProcessedDeath = true;
        RunProgressCoordinator.EnsureInstance()?.NotifyLegacyBossRewardsReady(this);
    }
}
