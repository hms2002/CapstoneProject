using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 : 테스트용 샌드백의 피격 반응, 데미지 팝업, 자동 회복을 처리한다.
/// 실제 전투 대상과 달리 프로토타입 씬에서 반복 테스트가 가능하도록 죽지 않는 동작을 제공한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(AttributeSet))]
[RequireComponent(typeof(TagSystem))]
[RequireComponent(typeof(GameplayEffectRunner))]
[RequireComponent(typeof(AbilitySystem))]
public class TrainingDummy2D : MonoBehaviour
{
    [Header("Damage Reaction")]
    [Tooltip("Attribute that decreases when the dummy takes damage.")]
    [SerializeField] private AttributeDefinition healthAttribute;

    [Tooltip("Fallback Animator trigger used when no configured hit state exists.")]
    [SerializeField] private string damagedTriggerName = "Damaged";

    [Tooltip("Animator states restarted immediately when the dummy is hit.")]
    [SerializeField] private string[] damagedStateNames = { "Hit_01", "Hit_02" };

    [Tooltip("Minimum interval between visual hit reactions.")]
    [SerializeField] private float minHurtInterval = 0.03f;

    [Header("Never Die")]
    [SerializeField] private bool neverDie = true;
    [SerializeField] private AttributeDefinition maxHealthAttribute;
    [SerializeField] private float healThreshold = 1f;

    private AttributeSet attributeSet;
    private Animator animator;
    private int damagedTriggerHash;
    private float nextHurtAllowedTime;

    private void Awake()
    {
        attributeSet = GetComponent<AttributeSet>();
        animator = GetComponent<Animator>();
        damagedTriggerHash = string.IsNullOrEmpty(damagedTriggerName) ? 0 : Animator.StringToHash(damagedTriggerName);
    }

    private void OnEnable()
    {
        if (attributeSet != null)
        {
            attributeSet.OnAttributeChanged += OnAttributeChanged;
        }
    }

    private void OnDisable()
    {
        if (attributeSet != null)
        {
            attributeSet.OnAttributeChanged -= OnAttributeChanged;
        }
    }

    private void OnAttributeChanged(AttributeDefinition attr, float oldValue, float newValue)
    {
        if (healthAttribute == null)
        {
            return;
        }

        if (attr != healthAttribute)
        {
            return;
        }

        if (newValue < oldValue)
        {
            float damage = oldValue - newValue;
            if (!DamagePopupDuplicateSuppressor.TryConsume(gameObject, damage))
                DamagePopupService.Show(damage, transform.position);

            PlayHurt();
        }

        if (neverDie && maxHealthAttribute != null && attributeSet != null && newValue <= healThreshold)
        {
            float maxHp = attributeSet.GetAttributeValue(maxHealthAttribute);
            float delta = maxHp - newValue;
            if (delta > 0f)
            {
                attributeSet.TryModifyAttributeValue(healthAttribute, delta, this);
            }
        }
    }

    private void PlayHurt()
    {
        if (animator == null)
        {
            return;
        }

        if (Time.time < nextHurtAllowedTime)
        {
            return;
        }

        nextHurtAllowedTime = Time.time + minHurtInterval;

        if (TryPlayRandomDamagedState())
        {
            return;
        }

        if (damagedTriggerHash == 0)
        {
            return;
        }

        animator.ResetTrigger(damagedTriggerHash);
        animator.SetTrigger(damagedTriggerHash);
    }

    private bool TryPlayRandomDamagedState()
    {
        if (damagedStateNames == null || damagedStateNames.Length == 0)
        {
            return false;
        }

        int startIndex = Random.Range(0, damagedStateNames.Length);
        for (int i = 0; i < damagedStateNames.Length; i++)
        {
            string stateName = damagedStateNames[(startIndex + i) % damagedStateNames.Length];
            if (string.IsNullOrEmpty(stateName))
            {
                continue;
            }

            if (TryPlayDamagedState(stateName))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryPlayDamagedState(string stateName)
    {
        int shortHash = Animator.StringToHash(stateName);
        if (animator.HasState(0, shortHash))
        {
            animator.Play(shortHash, 0, 0f);
            return true;
        }

        int baseLayerHash = Animator.StringToHash("Base Layer." + stateName);
        if (animator.HasState(0, baseLayerHash))
        {
            animator.Play(baseLayerHash, 0, 0f);
            return true;
        }

        return false;
    }
}
