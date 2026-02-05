using DG.Tweening;
using UnityEngine;
using UnityGAS;

public class Mob : Enemy
{
    [Header("Mob's Settings")]
    [SerializeField] private float detectionRange   = 6.0f; // 탐지 거리
    [SerializeField] private float moveSpeed        = 3.0f; // 이동 속도
    [SerializeField] private float damageInterval   = 1.0f; // 데미지 주기 (초)

    [Header("Mob's Ability")]
    [SerializeField] private AbilityDefinition tackleAbility; // AD_Tackle

    // Variables
    private float   attackCoolTime; // 마지막 공격 시간을 저장하여 쿨타임 체크용으로 사용
    private bool    isDead = false;

    // ========================================================================
    // [1] AI 및 이동 로직

    private void Update()
    {
        if (isDead || target == null) return;

        Vector2 finalVelocity   = Vector2.zero;
                targetDistance  = Vector2.Distance(transform.position, target.position);

        // 타겟 탐지
        if (targetDistance <= detectionRange)
        {
            moveDirection = (target.position - transform.position).normalized;
            finalVelocity = moveDirection * moveSpeed;

            // 스프라이트 반전
            if      (transform.position.x > target.position.x) sprite.flipX = true;
            else if (transform.position.x < target.position.x) sprite.flipX = false;
        }

        // [넉백 예외처리]: 외부 충격(넉백)으로 인해 속도가 엄청 빠르면 내 의지로 덮어쓰지 않음
        if (rigid2D.linearVelocity.magnitude <= moveSpeed * 1.5f)
        {
            rigid2D.linearVelocity = finalVelocity; 
        }

        animator.SetBool("isMoving", finalVelocity.sqrMagnitude > 0.01f);
    }

    // ========================================================================
    // [2] 공격 트리거 (Collision -> GAS 요청)

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (isDead) return;

        if (collision.gameObject.CompareTag("Player"))
        {
            // Mob.cs 레벨에서의 쿨타임 체크 (불필요한 GAS 호출 방지)
            if (Time.time >= attackCoolTime + damageInterval)
            {
                RequestAttack(collision.gameObject);
                attackCoolTime = Time.time;
            }
        }
    }

    private void RequestAttack(GameObject targetObj)
    {
        // 1. 할당된 어빌리티(AD)가 있는지 확인
        if (tackleAbility != null)
        {
            // 2. GAS 시스템에 시전 요청
            // Mob은 "어떻게 때리는지" 모름. 그냥 AD_Tackle을 실행하라고 시킬 뿐.
            abilitySystem.TryActivateAbility(tackleAbility, targetObj);
        }
    }

    // ========================================================================
    // [3] 상태 처리 (사망 등)

    protected override void OnEnemyAttributeChanged(AttributeDefinition attribute, float oldValue, float newValue)
    {
        base.OnEnemyAttributeChanged(attribute, oldValue, newValue);

        if (attribute == healthDef && newValue <= 0 && !isDead)
        {
            Die();
        }
    }

    protected override void Die()
    {
        if (isDead) return;

        isDead = true;

        if (collision != null)  collision.enabled = false;
        if (rigid2D != null)    rigid2D.simulated = false;
        if (animator != null)   animator.SetTrigger("Die");

        Destroy(gameObject, 1.0f);
    }

    private void OnDrawGizmos()
    {
        // 1. 그리기 색상 지정 (빨간색)
        Gizmos.color = Color.red;

        // 2. 와이어(선)로 된 원 그리기
        // 중심점: 내 위치 (transform.position)
        // 반지름: 탐지 거리 (detectionRange)
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}