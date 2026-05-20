using UnityEngine;
using UnityGAS;

public readonly struct PitFallContext
{
    public readonly AbilitySystem AbilitySystem;
    public readonly SafetyTracker SafetyTracker;
    public readonly Transform TargetTransform;
    public readonly GameObject TargetObject;
    public readonly GameObject TrapObject;
    public readonly GE_Damage_Spec DamageEffect;
    public readonly GameplayEffect FallingEffect;
    public readonly float Damage;
    public readonly float FallDuration;
    public readonly Vector3 FallCenter;
    public readonly Vector3 RespawnPosition;
    public readonly Object SourceObject;

    public PitFallContext(
        AbilitySystem abilitySystem,
        SafetyTracker safetyTracker,
        Transform targetTransform,
        GameObject trapObject,
        GE_Damage_Spec damageEffect,
        GameplayEffect fallingEffect,
        float damage,
        float fallDuration,
        Vector3 fallCenter,
        Vector3 respawnPosition,
        Object sourceObject)
    {
        AbilitySystem = abilitySystem;
        SafetyTracker = safetyTracker;
        TargetTransform = targetTransform;
        TargetObject = targetTransform != null ? targetTransform.gameObject : null;
        TrapObject = trapObject;
        DamageEffect = damageEffect;
        FallingEffect = fallingEffect;
        Damage = damage;
        FallDuration = Mathf.Max(0f, fallDuration);
        FallCenter = fallCenter;
        RespawnPosition = respawnPosition;
        SourceObject = sourceObject;
    }

    public bool IsValid =>
        AbilitySystem != null &&
        TargetTransform != null &&
        TargetObject != null &&
        TrapObject != null;
}
