using UnityEngine;

public sealed class LootSpawnService
{
    private readonly GameObject worldItemPrefab;
    private readonly GameObject fieldItemPrefab;

    public LootSpawnService(GameObject worldItemPrefab, GameObject fieldItemPrefab)
    {
        this.worldItemPrefab = worldItemPrefab;
        this.fieldItemPrefab = fieldItemPrefab;
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

    public void SpawnFieldHealPickup(Vector3 position)
    {
        if (fieldItemPrefab == null)
        {
            Debug.LogError("LootManager: FieldItemPrefab is not assigned.");
            return;
        }

        Object.Instantiate(fieldItemPrefab, position, Quaternion.identity);
    }

    public Vector3 GetRandomScatterOffset(float range = 0.5f)
    {
        return new Vector3(Random.Range(-range, range), Random.Range(-range, range), 0f);
    }
}
