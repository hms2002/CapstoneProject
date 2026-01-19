using UnityEngine;
using UnityGAS;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(AttributeSet))]
[RequireComponent(typeof(TagSystem))]
[RequireComponent(typeof(GameplayEffectRunner))]
[RequireComponent(typeof(AbilitySystem))] // "적 오브젝트에 꼭 넣을 GAS"에 포함시키고 싶으면 유지
public class TrainingDummy2D : MonoBehaviour
{
    [Header("Damage Reaction")]
    [Tooltip("데미지로 감소하는 Attribute (보통 Health)")]
    [SerializeField] private AttributeDefinition healthAttribute;

    [Tooltip("Animator Trigger 이름 (요구사항: Damaged)")]
    [SerializeField] private string damagedTriggerName = "Damaged";

    [Tooltip("연타/다중히트로 트리거가 과도하게 들어가는 걸 막는 최소 간격")]
    [SerializeField] private float minHurtInterval = 0.03f;

    [Tooltip("데미지 UI로 표시하는 컴포넌트")]
    [SerializeField] private DamagePopupSpawner2D popupSpawner;

    private AttributeSet attributeSet;
    private Animator animator;
    private int damagedTriggerHash;
    private float nextHurtAllowedTime;

    private void Awake()
    {
        attributeSet = GetComponent<AttributeSet>();
        animator = GetComponent<Animator>();
        damagedTriggerHash = Animator.StringToHash(damagedTriggerName);
    }


    private void Start()
    {
        if (attributeSet != null)
            attributeSet.OnAttributeChanged += OnAttributeChanged;
    }

    private void OnDisable()
    {
        if (attributeSet != null)
            attributeSet.OnAttributeChanged -= OnAttributeChanged;
    }

    private void OnAttributeChanged(AttributeDefinition attr, float oldValue, float newValue)
    {
        if (healthAttribute == null) return;
        if (attr != healthAttribute) return;

        if (newValue < oldValue)
        {
            float dmg = oldValue - newValue;

            // 떠오르는 텍스트
            if (popupSpawner != null)
                popupSpawner.Spawn(dmg, transform.position);

            // 기존 Hurt 애니
            PlayHurt();
        }
    }

    private void PlayHurt()
    {
        if (animator == null) return;
        if (Time.time < nextHurtAllowedTime) return;

        nextHurtAllowedTime = Time.time + minHurtInterval;

        // 같은 프레임/짧은 간격 재피격에서도 확실히 다시 트리거되도록
        animator.ResetTrigger(damagedTriggerHash);
        animator.SetTrigger(damagedTriggerHash);
    }
}
