using UnityEngine;
using UnityGAS;

public class Mob : Enemy
{
    [Header("Mob's Settings")]
    [Tooltip("접촉 피해 및 기본 공격 시도 후 다시 공격할 수 있을 때까지의 시간입니다.")]
    [SerializeField] private float damageInterval = 1.0f;

    [Header("Mob's Ability")]
    [Tooltip("잡몹 기본 공격으로 사용할 GAS Ability입니다.")]
    [SerializeField] private AbilityDefinition tackleAbility;

    [Header("Referenses")]
    [Tooltip("플레이어 추적 의도를 제공하는 컴포넌트입니다.")]
    [SerializeField] private EnemyChaseIntent2D chaseIntent;
    //[SerializeField] private TagSystem tagSystem;

    [Header("Tackle Range")]
    [Tooltip("Tackle 공격 시도를 시작하는 원형 범위의 지름입니다.")]
    [SerializeField] private float tackleAttackRangeDiameter = 6.0f;

    [Header("Movement Tags")]
    [Tooltip("공격 후 쿨타임 동안 추적 의도 이동을 막는 태그")]
    [SerializeField] private GameplayTag blockIntentMoveTag;

    [Tooltip("사망/완전 정지 상태에서 모든 이동을 막는 태그")]
    [SerializeField] private GameplayTag freezeAllMovementTag;

    private float currentCooltime;
    private bool isDead = false;

    private bool intentMoveTagApplied;
    private bool freezeMoveTagApplied;

    private bool hasPendingPreparedTackle;
    private PreparedTackleContext pendingPreparedTackle;

    public float TackleAttackRangeRadius => Mathf.Max(0f, tackleAttackRangeDiameter * 0.5f);

    public struct PreparedTackleContext
    {
        public GameObject Target;
        public Vector2 StartPosition;
        public Vector2 Direction;
        public Vector2 ImpactPosition;
        public float LungeDistance;
    }

    protected override void Awake()
    {
        base.Awake();

        if (chaseIntent == null)
            chaseIntent = GetComponent<EnemyChaseIntent2D>();

        if (tagSystem == null)
            tagSystem = GetComponent<TagSystem>();
    }

    private void Update()
    {
        if (isDead)
            return;

        UpdateCooldown();
        SyncMovementTags();
        TryStartPreparedTackle();

        if (animator != null && movementMotor != null)
            animator.SetBool("isMoving", movementMotor.IsMoving);
    }

    private void UpdateCooldown()
    {
        if (currentCooltime <= 0f)
            return;

        currentCooltime -= Time.deltaTime;
        if (currentCooltime < 0f)
            currentCooltime = 0f;
    }

    private void SyncMovementTags()
    {
        // 쿨타임/공격 실행 중에는 추적 의도 이동 금지
        bool shouldBlockIntentMove = currentCooltime > 0f || (abilitySystem != null && abilitySystem.IsBusy);
        SetTagActive(blockIntentMoveTag, shouldBlockIntentMove, ref intentMoveTagApplied);
    }

    private void TryStartPreparedTackle()
    {
        if (target == null) return;
        if (abilitySystem == null || tackleAbility == null) return;
        if (currentCooltime > 0f) return;
        if (abilitySystem.IsBusy) return;
        if (abilitySystem.GetCooldownRemaining(tackleAbility) > 0f) return;
        if (!IsTargetInTackleAttackRange()) return;

        PrepareTackleContext(target.gameObject);
        bool activated = abilitySystem.TryActivateAbility(tackleAbility, target.gameObject);

        if (!activated)
        {
            ClearPreparedTackleContext();
            return;
        }

        currentCooltime = damageInterval;
        SyncMovementTags(); // 즉시 반영
    }

    private bool IsTargetInTackleAttackRange()
    {
        if (target == null)
            return false;

        float attackRangeRadius = TackleAttackRangeRadius;
        if (attackRangeRadius <= 0f)
            return false;

        Vector2 toTarget = (Vector2)(target.position - transform.position);
        return toTarget.sqrMagnitude <= attackRangeRadius * attackRangeRadius;
    }

    private void PrepareTackleContext(GameObject targetObject)
    {
        Vector2 startPosition = transform.position;
        Vector2 impactPosition = targetObject != null ? targetObject.transform.position : transform.position;
        Vector2 direction = impactPosition - startPosition;
        float distance = direction.magnitude;

        if (distance <= 0.0001f)
        {
            direction = Vector2.right;
            distance = 0f;
        }
        else
        {
            direction /= distance;
        }

        pendingPreparedTackle = new PreparedTackleContext
        {
            Target = targetObject,
            StartPosition = startPosition,
            Direction = direction,
            ImpactPosition = impactPosition,
            LungeDistance = Mathf.Min(distance, TackleAttackRangeRadius)
        };

        hasPendingPreparedTackle = true;
    }

    public bool TryConsumePreparedTackleContext(out PreparedTackleContext context)
    {
        context = pendingPreparedTackle;

        if (!hasPendingPreparedTackle)
            return false;

        ClearPreparedTackleContext();
        return true;
    }

    private void ClearPreparedTackleContext()
    {
        hasPendingPreparedTackle = false;
        pendingPreparedTackle = default(PreparedTackleContext);
    }

    private void OnCollisionStay2D(Collision2D other)
    {
        if (isDead) return;
        if (!other.gameObject.CompareTag("Player")) return;
        if (currentCooltime > 0f) return;
        if (abilitySystem != null && abilitySystem.IsBusy) return;

        bool activated = abilitySystem != null &&
                         abilitySystem.TryActivateAbility(
                             tackleAbility,
                             target != null ? target.gameObject : other.gameObject);

        if (activated)
        {
            currentCooltime = damageInterval;
            SyncMovementTags(); // 즉시 반영
        }
    }

    protected override void OnEnemyAttributeChanged(AttributeDefinition attribute, float oldValue, float newValue)
    {
        base.OnEnemyAttributeChanged(attribute, oldValue, newValue);

        if (attribute == healthDef && newValue <= 0f && !isDead)
            Die();
    }

    /// <summary>
    /// 정식 넉백 진입점.
    /// GE_Knockback_Spec와 같은 구조처럼 causer + impulse를 받아 KnockbackReceiver2D로 위임한다.
    /// </summary>
    public void ApplyKnockbackFrom(GameObject causer, float impulse)
    {
        if (isDead) return;
        if (knockbackReceiver == null) return;

        knockbackReceiver.ApplyKnockback(causer, impulse);
    }

    protected override void Die()
    {
        if (isDead) return;
        isDead = true;

        // 쿨타임 기반 이동 차단은 정리
        SetTagActive(blockIntentMoveTag, false, ref intentMoveTagApplied);

        // 사망 중에는 완전 정지
        SetTagActive(freezeAllMovementTag, true, ref freezeMoveTagApplied);

        if (movementMotor != null)
            movementMotor.StopAllMotion();

        if (collision != null)
            collision.enabled = false;

        if (rigid2D != null)
            rigid2D.simulated = false;

        if (animator != null)
            animator.SetTrigger("Die");

        Destroy(gameObject, 1.0f);
    }

    private void OnDisable()
    {
        // 풀링/비정상 종료에도 태그 잔류 방지
        ClearPreparedTackleContext();
        SetTagActive(blockIntentMoveTag, false, ref intentMoveTagApplied);
        SetTagActive(freezeAllMovementTag, false, ref freezeMoveTagApplied);
    }

    private void SetTagActive(GameplayTag tag, bool shouldBeActive, ref bool appliedFlag)
    {
        if (tagSystem == null || tag == null)
            return;

        if (shouldBeActive)
        {
            if (appliedFlag)
                return;

            tagSystem.AddTag(tag);
            appliedFlag = true;
        }
        else
        {
            if (!appliedFlag)
                return;

            tagSystem.RemoveTag(tag);
            appliedFlag = false;
        }
    }

    private void OnDrawGizmos()
    {
        EnemyChaseIntent2D gizmoChaseIntent = chaseIntent != null
            ? chaseIntent
            : GetComponent<EnemyChaseIntent2D>();

        if (gizmoChaseIntent != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, gizmoChaseIntent.DetectionRange);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, gizmoChaseIntent.StopRange);
        }

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, TackleAttackRangeRadius);
    }
}
