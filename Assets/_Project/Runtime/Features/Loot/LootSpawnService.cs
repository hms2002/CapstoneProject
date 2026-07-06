using UnityEngine;

/// <summary>
/// 책임 : 전투/보상 gameplay가 월드 픽업, 회복 픽업, 재화 픽업을 생성하고 낙하 위치를 계산하게 한다.
/// </summary>
public sealed class LootSpawnService
{
    private const float FieldHealDropMinDistance = 0.25f;
    private const float FieldHealDropMaxDistance = 0.85f;

    private readonly GameObject worldItemPrefab;
    private readonly GameObject fieldItemPrefab;
    private readonly GameObject magicStonePrefab;
    private readonly GroundTileDropPositionResolver groundPositionResolver;

    public LootSpawnService(GameObject worldItemPrefab, GameObject fieldItemPrefab, GameObject magicStonePrefab)
    {
        this.worldItemPrefab = worldItemPrefab;
        this.fieldItemPrefab = fieldItemPrefab;
        this.magicStonePrefab = magicStonePrefab;
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

        if (!WorldItemDropAnimationPlayback.TryPlayDrop(
                pickup.gameObject,
                spawnPosition,
                landingPosition,
                () => pickup.SetInteractionLocked(false)))
        {
            pickup.transform.position = landingPosition;
            pickup.SetInteractionLocked(false);
        }

        return pickup;
    }

    public void SpawnFieldHealPickup(Vector3 position)
    {
        if (fieldItemPrefab == null)
        {
            Debug.LogError("LootManager: FieldItemPrefab is not assigned.");
            return;
        }

        Vector3 landingPosition = position + GetRandomFieldHealDropOffset();
        GameObject go = Object.Instantiate(fieldItemPrefab, position, Quaternion.identity);
        FieldHealPickup2D pickup = go.GetComponent<FieldHealPickup2D>();
        if (pickup != null)
        {
            pickup.PlayDrop(position, landingPosition);
            return;
        }

        go.transform.position = landingPosition;
    }

    public void SpawnMagicStonePickup(Vector3 position, int amount)
    {
        if (amount <= 0)
            return;

        if (magicStonePrefab == null)
        {
            Debug.LogError("LootManager: MagicStonePrefab is not assigned.");
            return;
        }

        GameObject go = Object.Instantiate(magicStonePrefab, position, Quaternion.identity);
        MagicStonePickup pickup = go.GetComponent<MagicStonePickup>();
        if (pickup != null)
            pickup.amount = amount;
    }

    public Vector3 GetRandomScatterOffset(float range = 0.5f)
    {
        return new Vector3(Random.Range(-range, range), Random.Range(-range, range), 0f);
    }

    private static Vector3 GetRandomFieldHealDropOffset()
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float distance = Random.Range(FieldHealDropMinDistance, FieldHealDropMaxDistance);
        return new Vector3(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance, 0f);
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
