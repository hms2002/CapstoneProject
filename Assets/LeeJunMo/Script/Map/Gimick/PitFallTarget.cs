using UnityEngine;
using UnityGAS;

public readonly struct PitFallTarget
{
    private const string PlayerTag = "Player";
    private static readonly Vector2 SlimeQueenRespawnPosition = new Vector2(-22f, 4f);

    public readonly AbilitySystem AbilitySystem;
    public readonly SafetyTracker SafetyTracker;
    public readonly Transform Transform;
    public readonly GameObject GameObject;
    public readonly Vector3 RespawnPosition;

    private PitFallTarget(
        AbilitySystem abilitySystem,
        SafetyTracker safetyTracker,
        Transform transform,
        Vector3 respawnPosition)
    {
        AbilitySystem = abilitySystem;
        SafetyTracker = safetyTracker;
        Transform = transform;
        GameObject = transform != null ? transform.gameObject : null;
        RespawnPosition = respawnPosition;
    }

    public bool IsValid =>
        AbilitySystem != null &&
        Transform != null &&
        GameObject != null;

    public static bool TryCreate(Collider2D collider, out PitFallTarget target)
    {
        if (TryCreatePlayer(collider, out target))
            return true;

        return TryCreateSlimeQueen(collider, out target);
    }

    public static bool TryCreatePlayer(Collider2D collider, out PitFallTarget target)
    {
        target = default;

        if (collider == null || !collider.CompareTag(PlayerTag))
            return false;

        AbilitySystem abilitySystem = collider.GetComponent<AbilitySystem>();
        SafetyTracker safetyTracker = collider.GetComponent<SafetyTracker>();
        if (safetyTracker == null)
            return false;

        target = new PitFallTarget(
            abilitySystem,
            safetyTracker,
            collider.transform,
            safetyTracker.GetRespawnPosition());

        return target.IsValid;
    }

    private static bool TryCreateSlimeQueen(Collider2D collider, out PitFallTarget target)
    {
        target = default;

        if (collider == null)
            return false;

        SlimeQueenBossBase slimeQueen = collider.GetComponentInParent<SlimeQueenBossBase>();
        if (slimeQueen == null)
            return false;

        if (!slimeQueen.CanTriggerPitFall)
            return false;

        AbilitySystem abilitySystem = slimeQueen.AbilitySystem != null
            ? slimeQueen.AbilitySystem
            : slimeQueen.GetComponent<AbilitySystem>();

        Vector3 respawnPosition = new Vector3(
            SlimeQueenRespawnPosition.x,
            SlimeQueenRespawnPosition.y,
            slimeQueen.transform.position.z);

        target = new PitFallTarget(
            abilitySystem,
            null,
            slimeQueen.transform,
            respawnPosition);

        return target.IsValid;
    }
}
