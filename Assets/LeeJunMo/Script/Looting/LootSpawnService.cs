using UnityEngine;

public sealed class LootSpawnService
{
    private readonly GameObject worldItemPrefab;
    private readonly GameObject fieldItemPrefab;
    private readonly GroundTileDropPositionResolver groundPositionResolver;

    public LootSpawnService(GameObject worldItemPrefab, GameObject fieldItemPrefab)
    {
        this.worldItemPrefab = worldItemPrefab;
        this.fieldItemPrefab = fieldItemPrefab;
        groundPositionResolver = new GroundTileDropPositionResolver();
    }

    public void SpawnLootObject(Vector3 position, ScriptableObject itemData)
    {
        SpawnLootObject(position, itemData, 0);
    }

    public void SpawnLootObject(Vector3 position, ScriptableObject itemData, int relicLevel)
    {
        InstantiateWorldPickup(position, itemData, relicLevel);
    }

    public WorldItemPickup2D SpawnAnimatedLootObject(Vector3 spawnPosition, Vector3 landingPosition, ScriptableObject itemData, int relicLevel = 0)
    {
        WorldItemPickup2D pickup = InstantiateWorldPickup(spawnPosition, itemData, relicLevel);
        if (pickup == null)
            return null;

        pickup.SetInteractionLocked(true);

        WorldItemDropTweenAnimator animator = pickup.GetComponent<WorldItemDropTweenAnimator>();
        if (animator == null)
            animator = pickup.gameObject.AddComponent<WorldItemDropTweenAnimator>();

        animator.PlayDrop(spawnPosition, landingPosition, () => pickup.SetInteractionLocked(false));
        return pickup;
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

    public System.Collections.Generic.List<Vector3> GetNearbyGroundPositions(Vector3 origin, int tileRadius = 1)
    {
        return groundPositionResolver.GetNearbyGroundPositions(origin, tileRadius);
    }

    public System.Collections.Generic.List<Vector3> GetHorizontalGroundPositions(Vector3 origin, int horizontalRadius = 1)
    {
        return groundPositionResolver.GetHorizontalGroundPositions(origin, horizontalRadius);
    }

    public Vector3 ResolveForwardGroundPosition(Vector3 origin, Transform directionSource)
    {
        if (groundPositionResolver.TryResolveForwardGroundPosition(origin, directionSource, out Vector3 landingPosition))
            return landingPosition;

        return origin;
    }

    public System.Collections.Generic.List<Vector3> GetForwardGroundPositions(Vector3 origin, Transform directionSource)
    {
        return groundPositionResolver.GetForwardGroundPositions(origin, directionSource);
    }

    private WorldItemPickup2D InstantiateWorldPickup(Vector3 position, ScriptableObject itemData, int relicLevel)
    {
        if (worldItemPrefab == null)
        {
            Debug.LogError("LootManager: WorldItemPrefab is not assigned.");
            return null;
        }

        if (itemData == null)
            return null;

        GameObject go = Object.Instantiate(worldItemPrefab, position, Quaternion.identity);
        WorldItemPickup2D pickup = go.GetComponent<WorldItemPickup2D>();
        if (pickup == null)
        {
            Debug.LogError("LootManager: Spawned world item prefab is missing WorldItemPickup2D.");
            Object.Destroy(go);
            return null;
        }

        pickup.SetItem(itemData, relicLevel);
        return pickup;
    }
}
