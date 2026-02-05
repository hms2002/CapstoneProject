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
    private float   currentCooltime; // 마지막 공격 시간을 저장하여 쿨타임 체크용으로 사용
    private bool    isDead = false;

    // ========================================================================
    // [1] AI 및 이동 로직

    private void Update()
    {
        if (isDead || target == null) return;

        // 쿨타임 처리 (카운트다운)
        if (currentCooltime > 0)
        {
            currentCooltime -= Time.deltaTime;
            if (currentCooltime < 0) currentCooltime = 0;
        }

        Vector2 finalVelocity   = Vector2.zero;
                targetDistance  = Vector2.Distance(transform.position, target.position);

        // 타겟 탐지
        if (currentCooltime <= 0)
        {
            if (targetDistance <= detectionRange)
            {
                moveDirection = (target.position - transform.position).normalized;
                finalVelocity = moveDirection * moveSpeed;

                // 스프라이트 반전
                if      (transform.position.x > target.position.x) sprite.flipX = true;
                else if (transform.position.x < target.position.x) sprite.flipX = false;
            }

            // 넉백 예외처리 - 외부 충격(넉백)으로 인해 속도가 엄청 빠르면 내 의지로 덮어쓰지 않음
            if (rigid2D.linearVelocity.magnitude <= moveSpeed * 1.5f)
            {
                rigid2D.linearVelocity = finalVelocity;
            }
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
            // 쿨타임이 0일 때만 공격 시도
            if (currentCooltime <= 0)
            {
                abilitySystem.TryActivateAbility(tackleAbility, target.gameObject);

                // 공격 성공 시 쿨타임(경직) 시작
                currentCooltime = damageInterval;
            }
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