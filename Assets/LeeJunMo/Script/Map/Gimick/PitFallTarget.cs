using UnityEngine;
using UnityGAS;

public readonly struct PitFallTarget
{
    private const string PlayerTag = "Player";

    public readonly AbilitySystem AbilitySystem;
    public readonly SafetyTracker SafetyTracker;
    public readonly Transform Transform;
    public readonly GameObject GameObject;

    private PitFallTarget(AbilitySystem abilitySystem, SafetyTracker safetyTracker, Transform transform)
    {
        AbilitySystem = abilitySystem;
        SafetyTracker = safetyTracker;
        Transform = transform;
        GameObject = transform != null ? transform.gameObject : null;
    }

    public bool IsValid =>
        AbilitySystem != null &&
        SafetyTracker != null &&
        Transform != null &&
        GameObject != null;

    public static bool TryCreatePlayer(Collider2D collider, out PitFallTarget target)
    {
        target = default;

        if (collider == null || !collider.CompareTag(PlayerTag))
            return false;

        AbilitySystem abilitySystem = collider.GetComponent<AbilitySystem>();
        SafetyTracker safetyTracker = collider.GetComponent<SafetyTracker>();

        target = new PitFallTarget(abilitySystem, safetyTracker, collider.transform);
        return target.IsValid;
    }
}
