<<<<<<< Updated upstream
using Cainos.PixelArtTopDown_Basic;
using System.Collections;
using UnityEngine;
using UnityGAS; // 네임스페이스 확인

public class HoleTrap : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float trapDamage = 10f;
    [SerializeField] private float trapDuration = 1.0f;

    [Header("GAS References")]
    [SerializeField] private GameplayEffect damageEffect; // GE_Damage_Spec
    [SerializeField] private GameplayEffect fallingEffect; // GE_Falling (이동불가 등)
    [SerializeField] private GameplayTag damageTag; // Data.Damage

    private bool isTriggered = false;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isTriggered) return;
        if (!collision.CompareTag("Player")) return;

        var abilitySystem = collision.GetComponent<AbilitySystem>();
        var controller = collision.GetComponent<TopDownCharacterController>();

        if (abilitySystem != null && controller != null)
        {
            StartCoroutine(ApplyTrapRoutine(abilitySystem, controller, collision.transform));
        }
    }

    private IEnumerator ApplyTrapRoutine(AbilitySystem asc, TopDownCharacterController controller, Transform playerTransform)
    {
        isTriggered = true;

        // -----------------------------------------------------------
        // 1. 상태 이상 적용 (이동 불가) - 반환값을 받지 않음 (void)
        // -----------------------------------------------------------
        if (fallingEffect != null)
        {
            // Spec 생성
            var statusSpec = asc.MakeSpec(fallingEffect, asc.gameObject);

            // [수정] 결과를 변수에 담지 않고 바로 실행
            asc.EffectRunner.ApplyEffectSpec(statusSpec, asc.gameObject);
        }

        // 2. 물리 속도 초기화
        var rb = playerTransform.GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = Vector2.zero;

        // 3. 연출 대기
        yield return new WaitForSeconds(trapDuration);

        // -----------------------------------------------------------
        // 4. 데미지 적용 (SetByCaller)
        // -----------------------------------------------------------
        if (damageEffect != null)
        {
            var damageSpec = asc.MakeSpec(damageEffect, asc.gameObject);

            // 데미지 수치 주입 (SetSetByCallerMagnitude가 없다면 SetMagnitude 등 확인 필요)
            if (damageTag != null)
            {
                damageSpec.SetSetByCallerMagnitude(damageTag, trapDamage);
            }

            asc.EffectRunner.ApplyEffectSpec(damageSpec, asc.gameObject);
        }

        // 5. 리스폰
        if (controller != null)
        {
            playerTransform.position = controller.LastSafePosition;
        }

        // -----------------------------------------------------------
        // 6. 상태 이상 해제 (이펙트 원본 에셋으로 삭제 요청)
        // -----------------------------------------------------------
        if (fallingEffect != null)
        {
            // [수정] RemoveActiveEffect 대신 RemoveEffect 사용 (Effect 원본과 타겟을 넘김)
            asc.EffectRunner.RemoveEffect(fallingEffect, asc.gameObject);
        }

        isTriggered = false;
    }
}
=======
using UnityEngine;

public class HoleTrap : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
>>>>>>> Stashed changes
