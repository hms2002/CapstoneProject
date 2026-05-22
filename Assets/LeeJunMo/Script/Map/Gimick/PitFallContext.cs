using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// - HoleTrap 낙하 실행에 필요한 대상, 연출, 피해, 리스폰, 후처리 정보를 한 번에 전달한다.
/// - PitFallExecutor가 감지/authoring 세부사항을 다시 조회하지 않게 하는 실행 문맥이다.
/// </summary>
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
    public readonly IPitFallReaction Reaction;
    public readonly bool LogDebug;

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
        Object sourceObject,
        IPitFallReaction reaction = null,
        bool logDebug = false)
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
        Reaction = reaction;
        LogDebug = logDebug;
    }

    public bool IsValid =>
        AbilitySystem != null &&
        TargetTransform != null &&
        TargetObject != null &&
        TrapObject != null;
}
