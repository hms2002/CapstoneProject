using UnityEngine;
using UnityGAS;

public class Mob : Enemy
{
    [Header("Mob's Settings")]
    [SerializeField] private float damageInterval = 1.0f;

    [Header("Mob's Ability")]
    [SerializeField] private AbilityDefinition tackleAbility;

    [Header("Refs")]
    [SerializeField] private EnemyChaseIntent2D chaseIntent;
    [SerializeField] private TagSystem tagSystem;

    [Header("Movement Tags")]
    [Tooltip("공격 후 쿨타임 동안 추적 의도 이동을 막는 태그")]
    [SerializeField] private GameplayTag blockIntentMoveTag;

    [Tooltip("사망/완전 정지 상태에서 모든 이동을 막는 태그")]
    [SerializeField] private GameplayTag freezeAllMovementTag;

    private float currentCooltime;
    private bool isDead = false;

    private bool intentMoveTagApplied;
    private bool freezeMoveTagApplied;

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
        // 쿨타임 동안 추적 이동 금지
        SetTagActive(blockIntentMoveTag, currentCooltime > 0f, ref intentMoveTagApplied);
    }

    private void OnCollisionStay2D(Collision2D other)
    {
        if (isDead) return;
        if (!other.gameObject.CompareTag("Player")) return;
        if (currentCooltime > 0f) return;

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
        if (chaseIntent == null)
            return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, chaseIntent.DetectionRange);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, chaseIntent.StopRange);
    }
}