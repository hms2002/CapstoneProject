using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// - Pawn의 접촉 피해를 물리 충돌이 아닌 trigger 접촉으로 처리한다.
/// - 플레이어 조작을 가두지 않으면서 일정 간격의 접촉 피해만 전투 파이프라인으로 전달한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PawnContactDamageDealer2D : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Pawn owner;
    [SerializeField] private AbilitySystem abilitySystem;
    [SerializeField] private GE_Damage_Spec damageEffect;

    [Header("Damage")]
    [SerializeField, Min(0f)] private float contactDamage = 0.5f;
    [SerializeField, Min(0.01f)] private float contactDamageInterval = 0.45f;

    private float nextDamageTime;
    private bool hasLoggedInvalidConfig;

    private void Awake()
    {
        if (owner == null)
            owner = GetComponentInParent<Pawn>();

        if (owner != null)
        {
            if (abilitySystem == null)
                abilitySystem = owner.GetComponent<AbilitySystem>();

            if (damageEffect == null)
                damageEffect = owner.ContactDamageEffect;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryHit(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryHit(other);
    }

    /// <summary>접촉한 플레이어에게 기존 CombatDamageAction 경로로 주기 피해를 전달한다.</summary>
    private void TryHit(Collider2D other)
    {
        if (!CanHit())
            return;

        if (!TryGetPlayer(other, out GameObject hitTarget))
            return;

        Vector3 hitPoint = other != null ? other.ClosestPoint(transform.position) : transform.position;
        CombatDamageAction.ApplyDamageAndEmitHit(
            system: abilitySystem,
            spec: null,
            damageEffect: damageEffect,
            knockbackEffect: null,
            target: hitTarget,
            finalHpDamage: contactDamage,
            finalStaggerBuildUp: 0f,
            finalKnockbackImpulse: 0f,
            hitConfirmedTag: null,
            hitWorldPosition: hitPoint,
            causer: owner != null ? owner.gameObject : gameObject);

        nextDamageTime = Time.time + contactDamageInterval;
    }

    /// <summary>피해 쿨다운과 필수 전투 의존성이 준비되었는지 확인한다.</summary>
    private bool CanHit()
    {
        if (owner == null || owner.IsDead || !owner.CanDealContactDamage || Time.time < nextDamageTime)
            return false;

        bool isValid = abilitySystem != null && damageEffect != null;
        if (isValid)
            return true;

        if (!hasLoggedInvalidConfig)
        {
            Debug.LogError($"[{nameof(PawnContactDamageDealer2D)}] 접촉 피해 설정이 비어 있습니다.", this);
            hasLoggedInvalidConfig = true;
        }

        return false;
    }

    /// <summary>접촉 콜라이더에서 플레이어 루트 오브젝트를 찾는다.</summary>
    private static bool TryGetPlayer(Collider2D other, out GameObject playerObject)
    {
        playerObject = null;
        if (other == null)
            return false;

        Transform current = other.transform;
        while (current != null)
        {
            if (current.CompareTag("Player"))
            {
                playerObject = current.gameObject;
                return true;
            }

            current = current.parent;
        }

        CombatHurtbox2D hurtbox = other.GetComponent<CombatHurtbox2D>();
        GameObject resolved = hurtbox != null ? hurtbox.ResolveTargetRoot() : null;
        if (resolved == null || !resolved.CompareTag("Player"))
            return false;

        playerObject = resolved;
        return true;
    }
}
