using System.Collections;
using UnityEngine;
using UnityGAS; // 프로젝트 GAS 네임스페이스

public class HoleTrap : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float trapDamage = 10f;
    [SerializeField] private float trapDuration = 1.0f; // 떨어지는 연출 시간

    [Header("GAS References")]
    [Tooltip("데미지를 주는 즉시 이펙트 (GE_Damage_Spec)")]
    [SerializeField] private GameplayEffect damageEffect;

    [Tooltip("추락 중 상태이상 이펙트 (Infinite Duration). State.Move.Blocked 등의 태그 포함")]
    [SerializeField] private GameplayEffect fallingEffect;

    [Tooltip("데미지 값을 전달할 태그 (Data.Damage)")]
    [SerializeField] private GameplayTag damageTag;

    private bool isTriggered = false;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isTriggered) return;
        if (!collision.CompareTag("Player")) return;

        var abilitySystem = collision.GetComponent<AbilitySystem>();

        // SafetyTracker 컴포넌트 가져오기
        var safetyTracker = collision.GetComponent<SafetyTracker>();

        if (abilitySystem != null && safetyTracker != null)
        {
            StartCoroutine(ApplyTrapRoutine(abilitySystem, safetyTracker, collision.transform));
        }
    }

    private IEnumerator ApplyTrapRoutine(AbilitySystem asc, SafetyTracker tracker, Transform playerTransform)
    {
        isTriggered = true;

        // -----------------------------------------------------------
        // 1. 상태 이상 적용 (이동/행동 불가)
        // -----------------------------------------------------------
        if (fallingEffect != null)
        {
            // Spec 생성 및 적용 (반환값 void이므로 변수 저장 안 함)
            var statusSpec = asc.MakeSpec(fallingEffect, asc.gameObject);
            asc.EffectRunner.ApplyEffectSpec(statusSpec, asc.gameObject);
        }

        // 2. 물리 속도 초기화 (미끄러짐 방지)
        var rb = playerTransform.GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = Vector2.zero;

        // 3. 연출 대기 (떨어지는 애니메이션이나 Cue가 재생될 시간)
        yield return new WaitForSeconds(trapDuration);

        // -----------------------------------------------------------
        // 4. 데미지 적용 (SetByCaller 패턴)
        // -----------------------------------------------------------
        if (damageEffect != null)
        {
            var damageSpec = asc.MakeSpec(damageEffect, asc.gameObject);

            // 데미지 수치 주입
            if (damageTag != null)
            {
                damageSpec.SetSetByCallerMagnitude(damageTag, trapDamage);
            }

            asc.EffectRunner.ApplyEffectSpec(damageSpec, asc.gameObject);
        }

        // 5. 리스폰 (SafetyTracker에게 위치 요청)
        playerTransform.position = tracker.GetRespawnPosition();

        // -----------------------------------------------------------
        // 6. 상태 이상 해제 (이펙트 원본을 기준으로 제거 요청)
        // -----------------------------------------------------------
        if (fallingEffect != null)
        {
            asc.EffectRunner.RemoveEffect(fallingEffect, asc.gameObject);
        }

        isTriggered = false;
    }
}