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
    [SerializeField] private GE_Damage_Spec damageEffect;
    [SerializeField] private GameplayEffect fallingEffect;

    [Header("Ignore Settings")]
    [Tooltip("이 태그가 있으면 함정이 발동하지 않습니다 (예: Action.Dash)")]
    [SerializeField] private GameplayTag ignoreTag;

    private bool isTriggered = false;

    private void Start()
    {
        DOTween.Init();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        CheckAndActivateTrap(collision);
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        CheckAndActivateTrap(collision);
    }

    private void CheckAndActivateTrap(Collider2D collision)
    {
        if (isTriggered) return;
        if (!collision.CompareTag("Player")) return;

        var abilitySystem = collision.GetComponent<AbilitySystem>();

        if (ignoreTag != null && abilitySystem != null)
        {
            if (abilitySystem.TagSystem.HasTag(ignoreTag))
                return;
        }

        var safetyTracker = collision.GetComponent<SafetyTracker>();
        if (abilitySystem != null && safetyTracker != null)
        {
            StartCoroutine(ApplyTrapRoutine(abilitySystem, safetyTracker, collision.transform));
        }
    }

    private IEnumerator ApplyTrapRoutine(AbilitySystem asc, SafetyTracker tracker, Transform playerTransform)
    {
        isTriggered = true;

        // 1. 상태 이상 적용
        if (fallingEffect != null)
        {
            var statusSpec = asc.MakeSpec(fallingEffect, this.gameObject);
            asc.EffectRunner.ApplyEffectSpec(statusSpec, asc.gameObject);
        }

        // 2. 물리 속도 초기화
        var rb = playerTransform.GetComponent<Rigidbody2D>();
        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        // 3. 연출 대기
        yield return new WaitForSeconds(trapDuration);

        // 4. 환경 피해 적용
        if (damageEffect != null)
        {
            HazardDamageAction.ApplyDamage(
                targetSystem: asc,
                target: asc.gameObject,
                damageEffect: damageEffect,
                finalHpDamage: trapDamage,
                causer: gameObject,
                sourceObject: this
            );
        }

        // 5. 리스폰
        playerTransform.position = tracker.GetRespawnPosition();

        // 6. 상태 이상 해제
        if (fallingEffect != null)
        {
            asc.EffectRunner.RemoveEffect(fallingEffect, asc.gameObject);
        }

        isTriggered = false;
    }
}