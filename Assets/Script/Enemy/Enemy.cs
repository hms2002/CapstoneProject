using UnityEngine;
using UnityGAS;

[RequireComponent(typeof(Rigidbody2D), typeof(Animator))]
[RequireComponent(typeof(AbilitySystem), typeof(AttributeSet), typeof(GameplayEffectRunner))]
[RequireComponent(typeof(TagSystem))]
public class Enemy : MonoBehaviour
{
    // My Components =============================
    protected Rigidbody2D       rigid2D;
    protected Collider2D        collision;
    protected SpriteRenderer    sprite;
    protected Animator          animator;

    protected AbilitySystem         abilitySystem;
    protected AttributeSet          attributeSet;
    protected GameplayEffectRunner  effectRunner;
    protected TagSystem             tagSystem;

    [Header("Enemy's Attributes")]
    [SerializeField] protected AttributeDefinition maxHealthDef;
    [SerializeField] protected AttributeDefinition healthDef;

    // My Variables =============================
    [Header("Enemy's Settings")]
    [SerializeField] protected string   enemyName;
                     protected Vector2  moveDirection;

    // Target Components
    protected Transform target;


    protected virtual void Awake()
    {
        rigid2D     = GetComponent<Rigidbody2D>();
        collision   = GetComponent<Collider2D>();
        sprite      = GetComponent<SpriteRenderer>();
        animator    = GetComponent<Animator>();

        abilitySystem   = GetComponent<AbilitySystem>();
        attributeSet    = GetComponent<AttributeSet>();
        effectRunner    = GetComponent<GameplayEffectRunner>();
        tagSystem       = GetComponent<TagSystem>();

        attributeSet.OnAttributeChanged += OnEnemyAttributeChanged; // 속성 변화 구독
    }

    protected virtual void Start()
    {
        // 타겟 위치 설정
        target = GameObject.FindWithTag("Player").gameObject.transform;
        if (target == null) Debug.LogWarning(enemyName + ": No target found with tag 'Player'");

        if (animator == null) Debug.LogWarning(enemyName + ": No animator found");
        if (collision == null) Debug.LogWarning(enemyName + ": No collision found");
    }

    protected virtual void OnEnemyAttributeChanged(AttributeDefinition attribute, float oldValue, float newValue) { }

    protected virtual void Die()
    {
        // 적 사망 처리 (예시: 오브젝트 비활성화)
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
}
