using DG.Tweening;
using UnityEngine;
using UnityGAS;

public class Mob : Enemy
{
    [Header("Mob's Settings")]
    [SerializeField] private float detectionRange   = 6.0f; // 탐지 거리
    [SerializeField] private float moveSpeed        = 3.0f; // 이동 속도
    [SerializeField] private float damageInterval   = 1.0f; // 데미지 주기 (초)
    [SerializeField] private float staggerDuration  = 0.4f; // 피격 경직 시간 (초)

    [Header("Mob's Ability")]
    [SerializeField] private AbilityDefinition tackleAbility; // AD_Tackle

    // Variables
    private float   currentCooltime; // 마지막 공격 시간을 저장하여 쿨타임 체크용으로 사용
    private bool    isDead = false;

    private float   knockbackRecoveryTime = 0f;                     // 넉백에서 회복하기까지 남은 시간
    private bool    IsKnockbacked => knockbackRecoveryTime > 0f;    // 현재 넉백 상태인지 확인하는 프로퍼티

    // ========================================================================
    // [1] AI 및 이동 로직

    private void Update()
    {
        if (isDead) return;

        // 공격 쿨타임 처리
        if (currentCooltime > 0)
        {
            currentCooltime -= Time.deltaTime;
            if (currentCooltime < 0) currentCooltime = 0;
        }

        // 넉백 회복 타이머 처리
        if (knockbackRecoveryTime > 0)
        {
            knockbackRecoveryTime -= Time.deltaTime;
            if (knockbackRecoveryTime < 0) knockbackRecoveryTime = 0;
        }

        // 애니메이션 업데이트
        animator.SetBool("isMoving", rigid2D.linearVelocity.sqrMagnitude > 0.01f);
    }

    private void FixedUpdate()
    {
        if (isDead || target == null) return;

        if (IsKnockbacked) return;

        targetDistance = Vector2.Distance(transform.position, target.position);

        // 타겟 탐지
        if (currentCooltime <= 0)
        {
            if (targetDistance <= detectionRange)
            {
                moveDirection           = (target.position - transform.position).normalized;
                rigid2D.linearVelocity  = moveDirection * moveSpeed;


                // 스프라이트 반전
                if (transform.position.x > target.position.x) sprite.flipX = true;
                else if (transform.position.x < target.position.x) sprite.flipX = false;
            }
            else
            {
                rigid2D.linearVelocity = Vector2.zero;
            }
        }
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

    /// <summary>
    /// 외부(예: 무기 타격, 스킬 효과 등)에서 몹에게 넉백을 가할 때 호출합니다.
    /// </summary>
    /// <param name="force">가해질 힘의 벡터 (방향 * 세기)</param>
    /// <param name="recoveryTime">넉백 상태(AI 정지)를 유지할 시간</param>
    public void ApplyKnockback(Vector2 force, float recoveryTime = 0.5f)
    {
        if (isDead) return;

        // 넉백 타이머 설정 (이 시간 동안 AI 이동 멈춤)
        knockbackRecoveryTime = recoveryTime;

        // 기존 속도를 초기화하고 새로운 물리적 힘을 가함
        rigid2D.linearVelocity = Vector2.zero;
        rigid2D.AddForce(force, ForceMode2D.Impulse);
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