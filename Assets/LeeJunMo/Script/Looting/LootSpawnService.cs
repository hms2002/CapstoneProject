using UnityEngine;

public sealed class LootSpawnService
{
    private readonly GameObject worldItemPrefab;

    public LootSpawnService(GameObject worldItemPrefab)
    {
        this.worldItemPrefab = worldItemPrefab;
    }

    public void SpawnLootObject(Vector3 position, ScriptableObject itemData)
    {
        if (worldItemPrefab == null)
        {
            Debug.LogError("LootManager: WorldItemPrefab is not assigned.");
            return;
        }

        if (itemData == null)
            return;

        GameObject go = Object.Instantiate(worldItemPrefab, position, Quaternion.identity);
        var pickup = go.GetComponent<WorldItemPickup2D>();
        if (pickup != null)
            pickup.SetItem(itemData);
    }

    public Vector3 GetRandomScatterOffset(float range = 0.5f)
    {
        return new Vector3(Random.Range(-range, range), Random.Range(-range, range), 0f);
    }
}
