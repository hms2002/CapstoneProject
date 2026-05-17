using UnityEngine;
using UnityGAS;

[DisallowMultipleComponent]
[RequireComponent(typeof(MobAbilityCoordinator))]
public class Pawn : Slime
{
    private const float MaxHealth = 2f;
    private const float VisualScale = 0.55f;
    private const float ChaseSpeedMultiplier = 2f;
    private const float ContactDamage = 0.5f;
    private const float ContactDamageInterval = 0.45f;

    [SerializeField] private GE_Damage_Spec damageEffect;

    private float nextDamageTime;
    private bool hasLoggedInvalidConfig;

    protected override void Awake()
    {
        base.Awake();

        CacheCoordinator();
        ApplyStats();
    }

    public override bool CanUseChaseMovement()
    {
        UpdateSpeed(ChaseSpeedMultiplier);
        return CanMove();
    }

    protected override void OnDeathStarted()
    {
        CancelAbility();
        base.OnDeathStarted();
    }

    protected override void PlayDeathAnimation()
    {
    }

    /// <summary>폰은 별도 공격 상태를 만들지 않고 충돌로 피해를 줍니다.</summary>
    public override bool TryBuildAttackRequest(out MobAttackRequest request)
    {
        request = default;
        return false;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryHit(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        TryHit(collision);
    }

    /// <summary>충돌 대상이 플레이어면 접촉 피해를 적용합니다.</summary>
    private void TryHit(Collision2D collision)
    {
        if (!CanHit()) return;
        if (collision == null || collision.collider == null) return;
        if (!TryGetPlayer(collision.collider, out GameObject hitTarget)) return;

        Vector3 hitPoint = collision.contactCount > 0
            ? collision.GetContact(0).point
            : transform.position;

        CombatDamageAction.ApplyDamageAndEmitHit(
            system: abilitySystem,
            spec: null,
            damageEffect: damageEffect,
            knockbackEffect: null,
            target: hitTarget,
            finalHpDamage: ContactDamage,
            finalStaggerBuildUp: 0f,
            finalKnockbackImpulse: 0f,
            hitConfirmedTag: null,
            hitWorldPosition: hitPoint,
            causer: gameObject);

        nextDamageTime = Time.time + ContactDamageInterval;
    }

    /// <summary>폰의 기본 스탯과 크기를 적용합니다.</summary>
    protected override void ApplyStats()
    {
        SetStats("Pawn", MaxHealth, VisualScale);
    }

    /// <summary>지금 접촉 피해를 줄 수 있는지 확인합니다.</summary>
    private bool CanHit()
    {
        if (!CanAct() || Time.time < nextDamageTime) return false;

        bool isValid = abilitySystem != null && damageEffect != null;
        if (isValid) return true;

        if (!hasLoggedInvalidConfig)
        {
            Debug.LogError($"[{nameof(Pawn)}] 접촉 피해 설정이 비어 있습니다.", this);
            hasLoggedInvalidConfig = true;
        }

        return false;
    }

    /// <summary>충돌 콜라이더에서 플레이어 루트 오브젝트를 찾습니다.</summary>
    private bool TryGetPlayer(Collider2D other, out GameObject playerObject)
    {
        playerObject = null;
        if (other == null) return false;

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
        if (hurtbox == null) return false;

        GameObject resolved = hurtbox.ResolveTargetRoot();
        if (resolved == null || !resolved.CompareTag("Player")) return false;

        playerObject = resolved;
        return true;
    }
}
