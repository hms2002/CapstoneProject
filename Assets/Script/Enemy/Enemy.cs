using UnityEngine;
using UnityGAS;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(Animator))]
[RequireComponent(typeof(AbilitySystem), typeof(AttributeSet), typeof(GameplayEffectRunner))]
[RequireComponent(typeof(TagSystem))]
[RequireComponent(typeof(MovementMotor2D), typeof(AttributeStatSource), typeof(AbilityMotionController2D))]
[RequireComponent(typeof(ExternalMovementController2D), typeof(KnockbackReceiver2D))]
public class Enemy : MonoBehaviour
{
    private const string DeathAnimTriggerName = "die";
    private const string DeathAnimClipName = "die";

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

    [Header("Enemy's Death")]
    [Tooltip("Die 애니메이션 재생 후 오브젝트를 제거하기까지 기다릴 시간입니다. 0 이하이면 Animator에서 Die 클립 길이를 자동 탐색합니다.")]
    [SerializeField] protected float dieAnimTime = 0f;

    // 이 클래스의 책임:
    // 모든 적이 공유하는 공통 사망 진입 상태와 사망 연출 재생/제거 흐름의 단일 진실 원천이 된다.
    protected bool isDead;

    protected Transform target;
    public virtual Transform Target => target;
    public bool IsDead => isDead;

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
        if (TrySetAnimatorTrigger(DeathAnimTriggerName, "isDead", "death"))
            return;

        TryPlayAnimatorState("Die", "Death", DeathAnimTriggerName);
    }

    /// <summary>사망 대기 시간 이후 적 오브젝트를 제거합니다.</summary>
    protected virtual void DestroyAfterDelay()
    {
        float destroyDelay = Mathf.Max(0f, GetDeathDestroyDelay());
        Destroy(gameObject, destroyDelay);
    }

    /// <summary>사망 후 오브젝트 제거까지 기다릴 시간을 반환합니다.</summary>
    protected virtual float GetDeathDestroyDelay()
    {
        if (dieAnimTime > 0f)
            return dieAnimTime;

        return ResolveDeathAnimationClipLength();
    }

    /// <summary>Animator Controller에서 Die 애니메이션 클립 길이를 찾아 반환합니다.</summary>
    private float ResolveDeathAnimationClipLength()
    {
        return FindAnimationClipLength(DeathAnimClipName);
    }

    /// <summary>후보 이름과 일치하는 Animator 파라미터 해시를 찾습니다.</summary>
    protected bool TryFindAnimatorParameterHash(
        AnimatorControllerParameterType parameterType,
        string[] candidateNames,
        out int parameterHash)
    {
        parameterHash = 0;

        if (animator == null || candidateNames == null || candidateNames.Length == 0)
            return false;

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < candidateNames.Length; i++)
        {
            string candidateName = candidateNames[i];
            if (string.IsNullOrWhiteSpace(candidateName))
                continue;

            int candidateHash = Animator.StringToHash(candidateName);
            for (int j = 0; j < parameters.Length; j++)
            {
                AnimatorControllerParameter parameter = parameters[j];
                if (parameter.type == parameterType && parameter.nameHash == candidateHash)
                {
                    parameterHash = candidateHash;
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>후보 이름 중 존재하는 Trigger 파라미터를 발동합니다.</summary>
    protected bool TrySetAnimatorTrigger(params string[] candidateNames)
    {
        if (!TryFindAnimatorParameterHash(
                AnimatorControllerParameterType.Trigger,
                candidateNames,
                out int parameterHash))
        {
            return false;
        }

        animator.SetTrigger(parameterHash);
        return true;
    }

    /// <summary>후보 이름 중 존재하는 Bool 파라미터 값을 갱신합니다.</summary>
    protected bool TrySetAnimatorBool(bool value, params string[] candidateNames)
    {
        if (!TryFindAnimatorParameterHash(
                AnimatorControllerParameterType.Bool,
                candidateNames,
                out int parameterHash))
        {
            return false;
        }

        animator.SetBool(parameterHash, value);
        return true;
    }

    /// <summary>후보 이름 중 존재하는 상태를 즉시 재생합니다.</summary>
    protected bool TryPlayAnimatorState(params string[] stateNames)
    {
        if (animator == null || stateNames == null || stateNames.Length == 0)
            return false;

        for (int i = 0; i < stateNames.Length; i++)
        {
            string stateName = stateNames[i];
            if (string.IsNullOrWhiteSpace(stateName))
                continue;

            int stateHash = Animator.StringToHash(stateName);
            if (!animator.HasState(0, stateHash))
                continue;

            animator.Play(stateHash, 0, 0f);
            return true;
        }

        return false;
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
