using UnityEngine;
using UnityGAS;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(Animator))]
[RequireComponent(typeof(AbilitySystem), typeof(AttributeSet), typeof(GameplayEffectRunner))]
[RequireComponent(typeof(TagSystem))]
[RequireComponent(typeof(MovementMotor2D), typeof(AttributeStatSource))]
[RequireComponent(typeof(ExternalMovementController2D), typeof(KnockbackReceiver2D))]
public class Enemy : MonoBehaviour
{
    // Components =============================
    protected Rigidbody2D rigid2D;
    protected Collider2D collision;
    protected SpriteRenderer sprite;
    protected Animator animator;

    protected AbilitySystem abilitySystem;
    protected AttributeSet attributeSet;
    protected GameplayEffectRunner effectRunner;
    protected TagSystem tagSystem;

    protected MovementMotor2D movementMotor;
    protected AttributeStatSource attributeStatSource;
    protected ExternalMovementController2D externalMovement;
    protected KnockbackReceiver2D knockbackReceiver;

    [Header("Enemy's Attributes")]
    [SerializeField] protected AttributeDefinition maxHealthDef;
    [SerializeField] protected AttributeDefinition healthDef;

    [Header("Enemy's Settings")]
    [SerializeField] protected string enemyName;
    [SerializeField] private string targetTag = "Player";

    protected Transform target;
    public Transform Target => target;

    protected virtual void Awake()
    {
        rigid2D = GetComponent<Rigidbody2D>();
        collision = GetComponent<Collider2D>();
        sprite = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();

        abilitySystem = GetComponent<AbilitySystem>();
        attributeSet = GetComponent<AttributeSet>();
        effectRunner = GetComponent<GameplayEffectRunner>();
        tagSystem = GetComponent<TagSystem>();

        movementMotor = GetComponent<MovementMotor2D>();
        attributeStatSource = GetComponent<AttributeStatSource>();
        externalMovement = GetComponent<ExternalMovementController2D>();
        knockbackReceiver = GetComponent<KnockbackReceiver2D>();

        if (attributeSet != null)
            attributeSet.OnAttributeChanged += OnEnemyAttributeChanged;
    }

    protected virtual void Start()
    {
        RefreshTarget();
    }

    protected virtual void OnDestroy()
    {
        if (attributeSet != null)
            attributeSet.OnAttributeChanged -= OnEnemyAttributeChanged;
    }

    protected void RefreshTarget()
    {
        if (string.IsNullOrWhiteSpace(targetTag))
            return;

        GameObject found = GameObject.FindWithTag(targetTag);
        target = found != null ? found.transform : null;

        if (target == null)
            Debug.LogWarning($"{enemyName}: No target found with tag '{targetTag}'");
    }

    protected virtual void OnEnemyAttributeChanged(AttributeDefinition attribute, float oldValue, float newValue) { }

    protected virtual void Die()
    {
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
}