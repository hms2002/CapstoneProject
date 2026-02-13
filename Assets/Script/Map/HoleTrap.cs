using DG.Tweening;
using System.Collections;
using UnityEngine;
using UnityGAS;

public class HoleTrap : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float trapDamage = 10f;
    [SerializeField] private float trapDuration = 1.0f;

    [Header("GAS References")]
    [SerializeField] private GameplayEffect damageEffect;
    [SerializeField] private GameplayEffect fallingEffect;
    [SerializeField] private GameplayTag damageTag;

    [Header("Ignore Settings")]
    [Tooltip("이 태그가 있으면 함정이 발동하지 않습니다 (예: Action.Dash)")]
    [SerializeField] private GameplayTag ignoreTag;

    private bool isTriggered = false;

    private void Start()
    {
        DOTween.Init();
    }

    // 1. 진입 시 체크
    private void OnTriggerEnter2D(Collider2D collision)
    {
        CheckAndActivateTrap(collision);
    }

    // 2. [추가됨] 머무는 동안 계속 체크 (대쉬가 끝나는 순간을 잡기 위해 필수)
    private void OnTriggerStay2D(Collider2D collision)
    {
        CheckAndActivateTrap(collision);
    }

    // 함정 발동 조건을 검사하는 공통 로직
    private void CheckAndActivateTrap(Collider2D collision)
    {
        // 이미 발동 중이면 무시
        if (isTriggered) return;

        // 플레이어만 체크
        if (!collision.CompareTag("Player")) return;

        var abilitySystem = collision.GetComponent<AbilitySystem>();

        // [무시 조건] 대쉬 중인지 확인
        if (ignoreTag != null && abilitySystem != null)
        {
            // ignoreTag(Action.Dash)를 가지고 있다면 발동하지 않고 리턴
            if (abilitySystem.TagSystem.HasTag(ignoreTag))
            {
                return;
            }
        }

        // [발동 조건 충족]
        var safetyTracker = collision.GetComponent<SafetyTracker>();
        if (abilitySystem != null && safetyTracker != null)
        {
            StartCoroutine(ApplyTrapRoutine(abilitySystem, safetyTracker, collision.transform));
        }
    }

    private IEnumerator ApplyTrapRoutine(AbilitySystem asc, SafetyTracker tracker, Transform playerTransform)
    {
        isTriggered = true; // 중복 실행 방지 락(Lock)

        // 1. 상태 이상 적용 (이동 불가 등)
        if (fallingEffect != null)
        {
            // Causer를 함정 자신(this.gameObject)으로 설정하여 Cue에서 위치를 알 수 있게 함
            var statusSpec = asc.MakeSpec(fallingEffect, this.gameObject);
            asc.EffectRunner.ApplyEffectSpec(statusSpec, asc.gameObject);
        }

        // 2. 물리 속도 초기화 (미끄러짐 방지)
        var rb = playerTransform.GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = Vector2.zero;

        // 3. 연출 대기
        yield return new WaitForSeconds(trapDuration);

        // 4. 데미지 적용
        if (damageEffect != null)
        {
            var damageSpec = asc.MakeSpec(damageEffect, asc.gameObject);
            if (damageTag != null)
            {
                damageSpec.SetSetByCallerMagnitude(damageTag, trapDamage);
            }
            asc.EffectRunner.ApplyEffectSpec(damageSpec, asc.gameObject);
        }

        // 5. 리스폰 (안전한 위치로 이동)
        playerTransform.position = tracker.GetRespawnPosition();

        // 6. 상태 이상 해제
        if (fallingEffect != null)
        {
            asc.EffectRunner.RemoveEffect(fallingEffect, asc.gameObject);
        }

        isTriggered = false; // 락 해제
    }
}