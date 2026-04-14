using UnityEngine;
using UnityGAS;

/// <summary>
/// 이 클래스의 책임: 
/// 모든 적이 공유하는 공통 사망 진입 상태와 사망 연출 재생/제거 흐름의 단일 진실 원천이 된다.
/// </summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(Animator))]
[RequireComponent(typeof(AbilitySystem), typeof(AttributeSet), typeof(GameplayEffectRunner))]
[RequireComponent(typeof(TagSystem))]
[RequireComponent(typeof(MovementMotor2D), typeof(AttributeStatSource), typeof(AbilityMotionController2D))]
[RequireComponent(typeof(ExternalMovementController2D), typeof(KnockbackReceiver2D))]
public class Enemy : MonoBehaviour
{
    private const float DeathStateEnterTimeout = 1f;
    private const float DeathDestroyFailSafeTimeout = 5f;

    // Components =============================
    protected Rigidbody2D       rigid2D;
    protected Collider2D        collision;
    protected SpriteRenderer    sprite;
    protected Animator          animator;

    protected AbilitySystem         abilitySystem;
    protected AttributeSet          attributeSet;
    protected GameplayEffectRunner  effectRunner;
    protected TagSystem             tagSystem;

    protected MovementMotor2D               movementMotor;
    protected AttributeStatSource           attributeStatSource;
    protected ExternalMovementController2D  externalMovement;
    protected KnockbackReceiver2D           knockbackReceiver;

    [Header("Enemy's Attributes")]
    [SerializeField] protected AttributeDefinition maxHealthDef;
    [SerializeField] protected AttributeDefinition healthDef;

    [Header("Enemy's Settings")]
    [SerializeField] protected string enemyName;
    [SerializeField] private string targetTag = "Player";

    protected bool isDead;
    private Coroutine deathDestroyRoutine;
    private int deathStartStateHash;

    protected Transform target;
    public virtual Transform Target => target;
    public bool IsDead => isDead;
    public virtual string EnemyName => enemyName;

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

        movementMotor       = GetComponent<MovementMotor2D>();
        attributeStatSource = GetComponent<AttributeStatSource>();
        externalMovement    = GetComponent<ExternalMovementController2D>();
        knockbackReceiver   = GetComponent<KnockbackReceiver2D>();

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

        deathDestroyRoutine = null;
    }

    /// <summary>타겟 태그로 현재 추적 대상을 갱신합니다.</summary>
    protected void RefreshTarget()
    {
        if (string.IsNullOrWhiteSpace(targetTag))
            return;

        GameObject found = GameObject.FindWithTag(targetTag);
        target = found != null ? found.transform : null;

        if (target == null)
            Debug.LogWarning($"{enemyName}: No target found with tag '{targetTag}'");
    }

    /// <summary>현재 추적 대상을 지정한 Transform으로 교체합니다.</summary>
    protected void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    /// <summary>적 Attribute 값이 바뀔 때 파생 클래스가 반응할 수 있는 훅입니다.</summary>
    protected virtual void OnEnemyAttributeChanged(AttributeDefinition attribute, float oldValue, float newValue) { }

    /// <summary>적 사망 처리의 공통 진입점입니다.</summary>
    protected virtual void Die()
    {
        if (isDead) return;

        isDead = true;
        deathStartStateHash = animator != null ? animator.GetCurrentAnimatorStateInfo(0).fullPathHash : 0;
        OnDeathStarted();
        StopDeathGameplay();
        PlayDeathAnimation();
        DestroyAfterDelay();
    }

    /// <summary>파생 클래스가 공통 사망 처리 시작 직전에 전용 정리를 끼워 넣는 훅입니다.</summary>
    protected virtual void OnDeathStarted() { }

    /// <summary>사망 시 이동, 충돌, 물리 처리를 정지합니다.</summary>
    protected virtual void StopDeathGameplay()
    {
        if (movementMotor != null)
            movementMotor.StopAllMotion();

        if (collision != null)
            collision.enabled = false;

        if (rigid2D != null)
            rigid2D.simulated = false;
    }

    /// <summary>Animator에 Die 트리거를 전달해 사망 애니메이션을 재생합니다.</summary>
    protected virtual void PlayDeathAnimation()
    {
        if (animator != null)
            animator.SetTrigger("die");
    }

    /// <summary>사망 애니메이션이 끝나면 적 오브젝트를 제거합니다.</summary>
    protected virtual void DestroyAfterDelay()
    {
        if (deathDestroyRoutine != null)
            StopCoroutine(deathDestroyRoutine);

        if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null)
        {
            Destroy(gameObject);
            return;
        }

        deathDestroyRoutine = StartCoroutine(WaitForDeathAnimationAndDestroy());
    }

    /// <summary>죽는 상태가 끝날 때까지 기다린 뒤 오브젝트를 제거합니다.</summary>
    private System.Collections.IEnumerator WaitForDeathAnimationAndDestroy()
    {
        float stateEnterElapsed = 0f;
        float totalElapsed = 0f;
        bool hasLeftStartState = false;

        yield return null;

        while (animator != null)
        {
            totalElapsed += Time.deltaTime;
            if (totalElapsed >= DeathDestroyFailSafeTimeout)
                break;

            if (!animator.isActiveAndEnabled)
                break;

            if (animator.IsInTransition(0))
            {
                stateEnterElapsed += Time.deltaTime;
                if (!hasLeftStartState && stateEnterElapsed >= DeathStateEnterTimeout)
                    break;

                yield return null;
                continue;
            }

            AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);

            if (!hasLeftStartState)
            {
                hasLeftStartState = currentState.fullPathHash != deathStartStateHash;

                if (!hasLeftStartState)
                {
                    stateEnterElapsed += Time.deltaTime;
                    if (stateEnterElapsed >= DeathStateEnterTimeout)
                        break;

                    yield return null;
                    continue;
                }
            }

            if (!currentState.loop &&
                !animator.IsInTransition(0) &&
                currentState.normalizedTime >= 1f)
            {
                break;
            }

            yield return null;
        }

        deathDestroyRoutine = null;
        Destroy(gameObject);
    }

    /// <summary>후보 이름과 일치하는 애니메이션 클립 길이를 찾습니다.</summary>
    protected float FindAnimationClipLength(params string[] clipNames)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return 0f;

        AnimationClip[] animationClips = animator.runtimeAnimatorController.animationClips;
        if (animationClips == null || clipNames == null || clipNames.Length == 0)
            return 0f;

        for (int i = 0; i < animationClips.Length; i++)
        {
            AnimationClip animationClip = animationClips[i];
            if (animationClip == null)
                continue;

            for (int j = 0; j < clipNames.Length; j++)
            {
                string clipName = clipNames[j];
                if (string.IsNullOrWhiteSpace(clipName))
                    continue;

                if (animationClip.name == clipName)
                    return animationClip.length;
            }
        }

        for (int i = 0; i < animationClips.Length; i++)
        {
            AnimationClip animationClip = animationClips[i];
            if (animationClip == null)
                continue;

            for (int j = 0; j < clipNames.Length; j++)
            {
                string clipName = clipNames[j];
                if (string.IsNullOrWhiteSpace(clipName))
                    continue;

                if (animationClip.name.IndexOf(clipName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return animationClip.length;
            }
        }

        return 0f;
    }
}
