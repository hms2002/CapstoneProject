using UnityEngine;
using UnityGAS;

public class Mob : Enemy
{
    [Header("Mob Settings")]
    [SerializeField] private float detectionRange   = 6.0f; // 탐지 거리
    [SerializeField] private float moveSpeed        = 3.0f; // 이동 속도
    [SerializeField] private float power            = 3.0f; // 몸통박치기 데미지
    [SerializeField] private float damageInterval   = 1.0f; // 데미지 주기 (초)

    [Header("Mob's Effects")]
    [SerializeField] private GameplayEffect damageEffect; // 플레이어에게 적용할 GE

    private float attackCoolTime; // 공격 쿨타임 체크용
    private bool isDead = false;

    // ----------------------------------------------------
    // AI Loop

    private void Update()
    {
        if (isDead || target == null) return;

        float distance = Vector2.Distance(transform.position, target.position);

        // 탐지 거리 안으로 들어왔는가?
        if (distance <= detectionRange)
        {
            // 플레이어 쪽으로 방향 설정 (단순 추적)
            Vector2 direction = (target.position - transform.position).normalized;

            // Flip 처리가 필요하다면 여기서 direction.x 확인
            Vector2 nextPos = rigid2D.position + direction * moveSpeed * Time.deltaTime;
            rigid2D.MovePosition(nextPos);

            // 걷기 애니메이션이 있다면 여기서 파라미터 전달
            if (animator != null) animator.SetBool("IsWalking", true);
        }
        else
        {
            // 거리 밖이면 대기
            if (animator != null) animator.SetBool("IsWalking", false);
        }
    }

    // ----------------------------------------------------
    // 공격 로직

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (isDead) return;

        if (collision.gameObject.CompareTag("Player"))
        {
            // 쿨타임 체크 후 공격 실행
            if (Time.time >= attackCoolTime + damageInterval)
            {
                AttackTarget(collision.gameObject);
                attackCoolTime = Time.time;
            }
        }
    }

    private void AttackTarget(GameObject targetObj)
    {
        GameplayEffectRunner targetRunner = targetObj.GetComponent<GameplayEffectRunner>();

        if (targetRunner != null && damageEffect != null)
        {
            // 1. [Source: Mob] 데미지 명세서(Spec) 생성
            GameplayEffectSpec spec = abilitySystem.MakeSpec(damageEffect, gameObject);

            // 2. [Data] 데미지 수치 주입
            // 단계 A: 문자열로 ID(int)를 먼저 찾습니다.
            int damageTagId = TagRegistry.GetIdByPath("Data.Damage");

            // 단계 B: ID로 태그 객체(GameplayTag)를 가져옵니다.
            GameplayTag damageTag = TagRegistry.GetTag(damageTagId);

            if (damageTag != null)
            {
                // 단계 C: 태그 객체를 키값으로 사용하여 수치 설정
                spec.SetSetByCallerMagnitude(damageTag, power);
            }
            else
            {
                Debug.LogError("[Mob] 'Data.Damage' 태그를 찾을 수 없습니다.");
                return;
            }

            // 3. [Target: Player] 적용
            targetRunner.ApplyEffectSpec(spec, targetObj);
        }
    }

    // ----------------------------------------------------
    // [3] 피격 및 사망 처리 (GAS Event Override)

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

        Debug.Log($"[Mob] {name} 사망!");

        // 1. 충돌/물리 끄기
        if (collision != null) collision.enabled = false;
        if (rigid2D != null) rigid2D.simulated = false;

        // 2. 사망 애니메이션 재생 (Trigger)
        if (animator != null) animator.SetTrigger("Die");

        // 3. 잠시 후 삭제 (애니메이션 재생 시간 고려)
        Destroy(gameObject, 1.0f);
    }
}