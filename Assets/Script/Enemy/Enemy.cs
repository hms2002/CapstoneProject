using UnityEngine;
using UnityGAS;

/// <summary>
/// 이 클래스의 책임: 
/// 모든 적이 공유하는 공통 사망 진입 상태와 사망 연출 재생/제거 흐름의 단일 진실 원천이 된다.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(AbilitySystem), typeof(AttributeSet), typeof(GameplayEffectRunner))]
[RequireComponent(typeof(TagSystem))]
[RequireComponent(typeof(MovementMotor2D), typeof(AttributeStatSource), typeof(AbilityMotionController2D))]
[RequireComponent(typeof(ExternalMovementController2D), typeof(KnockbackReceiver2D))]
public class Enemy : MonoBehaviour, ICombatDeathCommand
{
    private const float DeathStateEnterTimeout = 1f;
    private const float DeathDestroyFailSafeTimeout = 5f;
    private static readonly RaycastHit2D[] DoorSightHitBuffer = new RaycastHit2D[64];

    // Components =============================
    protected Rigidbody2D       rigid2D;
    protected Collider2D        collision;
    protected EntityCollisionProfile2D collisionProfile;
    [Header("Enemy Visual")]
    [SerializeField] protected SpriteRenderer sprite;
    [SerializeField] protected Animator animator;

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
    [SerializeField] private LayerMask targetSearchLayers = Physics2D.DefaultRaycastLayers;

    protected bool isDead;
    private Coroutine deathDestroyRoutine;
    private int deathStartStateHash;
    private bool rightFacingFlipX;

    protected Transform target;
    public virtual Transform Target => target;
    public bool IsDead => isDead;
    public virtual string EnemyName => enemyName;

    protected virtual void Awake()
    {
        rigid2D     = GetComponent<Rigidbody2D>();
        collisionProfile = GetComponent<EntityCollisionProfile2D>();
        collision   = ResolvePrimaryBodyCollider();
        CacheVisualComponents();
        CacheInitialFacingFlipX();

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

    /// <summary>
    /// 책임 :
    /// - 적의 본체 visual이 root 또는 child 어디에 있든 공통 sprite/animator 참조를 해결한다.
    /// - root는 물리/전투 좌표를 유지하고 visual child는 높이 연출을 받을 수 있도록 결합을 낮춘다.
    /// </summary>
    private void CacheVisualComponents()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (sprite == null)
            sprite = GetComponent<SpriteRenderer>();

        if (sprite == null && animator != null)
            sprite = animator.GetComponentInChildren<SpriteRenderer>(true);

        if (sprite == null)
            sprite = GetComponentInChildren<SpriteRenderer>(true);
    }

    /// <summary>
    /// 책임 :
    /// - 프리팹이 가진 기본 flipX 값을 "오른쪽을 바라보는 기준값"으로 저장한다.
    /// - 몬스터별 스프라이트 원본 방향이 달라도 공통 방향 전환 로직이 authoring 값을 보존하게 한다.
    /// </summary>
    private void CacheInitialFacingFlipX()
    {
        rightFacingFlipX = sprite != null && sprite.flipX;
    }

    /// <summary>
    /// 책임 :
    /// - 타겟의 X 위치에 따라 스프라이트 방향을 바꾸되, 초기 flipX 기준값을 유지한다.
    /// - 프리팹에서 flipX로 기본 방향을 보정한 몬스터가 런타임 방향 전환에서 다시 뒤집히지 않게 한다.
    /// </summary>
    protected bool TryApplySpriteFacingTargetX(float targetX, float deadZone = 0.001f)
    {
        if (sprite == null)
            return false;

        float deltaX = targetX - transform.position.x;
        if (Mathf.Abs(deltaX) <= deadZone)
            return false;

        sprite.flipX = deltaX > 0f ? rightFacingFlipX : !rightFacingFlipX;
        return true;
    }

    /// <summary>
    /// 책임 :
    /// - root 또는 child에 배치된 이동 방해용 body collider 대표값을 찾는다.
    /// - hurtbox/hitbox trigger는 피해 판정용이므로 Enemy의 기본 물리 충돌 대표로 사용하지 않는다.
    /// </summary>
    private Collider2D ResolvePrimaryBodyCollider()
    {
        Collider2D rootCollider = GetComponent<Collider2D>();
        if (rootCollider != null && !rootCollider.isTrigger)
            return rootCollider;

        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D candidate = colliders[i];
            if (candidate == null || candidate.isTrigger)
                continue;

            return candidate;
        }

        return rootCollider;
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
        TryRefreshTarget(logWarning: true);
    }

    /// <summary>타겟 태그로 현재 추적 대상을 갱신하고 성공 여부를 반환합니다.</summary>
    protected bool TryRefreshTarget(bool logWarning = true)
    {
        if (string.IsNullOrWhiteSpace(targetTag))
            return false;

        if (UsesPlayerTargetTag() && TryResolveRegisteredPlayerTarget(out Transform playerTarget))
        {
            target = playerTarget;
            return true;
        }

        GameObject found = GameObject.FindWithTag(targetTag);
        target = found != null ? ResolveTaggedTargetTransform(found.transform) : null;

        if (target == null && logWarning)
            Debug.LogWarning($"{enemyName}: No target found with tag '{targetTag}'");

        return target != null;
    }

    /// <summary>현재 추적 대상을 지정한 Transform으로 교체합니다.</summary>
    protected void SetTarget(Transform newTarget)
    {
        target = NormalizeAssignedTarget(newTarget);
    }

    private Transform NormalizeAssignedTarget(Transform newTarget)
    {
        if (newTarget == null || !UsesPlayerTargetTag())
            return newTarget;

        Transform registeredPlayer = PlayerRuntimeRegistry.GetPlayerTransform();
        if (registeredPlayer != null &&
            (newTarget == registeredPlayer || newTarget.IsChildOf(registeredPlayer)))
        {
            return registeredPlayer;
        }

        PlayerInteractor2D player = newTarget.GetComponentInParent<PlayerInteractor2D>();
        return player != null ? player.transform : newTarget;
    }

    /// <summary>
    /// 책임 :
    /// 스폰 순서나 일시적인 target 누락 상황에서 적이 자기 주변 감지 범위 안의 유효 target을 다시 획득하게 한다.
    /// </summary>
    public bool TryAcquireTargetInRange(float range)
    {
        if (Target != null)
            return CanPerceiveTarget(Target);

        float searchRange = Mathf.Max(0f, range);
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, searchRange, targetSearchLayers);
        Transform nearestTarget = null;
        float nearestSqrDistance = float.PositiveInfinity;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            Transform candidate = ResolveTargetCandidate(hit);
            if (candidate == null)
                continue;

            if (!CanPerceiveTarget(candidate))
                continue;

            float sqrDistance = ((Vector2)(candidate.position - transform.position)).sqrMagnitude;
            if (sqrDistance >= nearestSqrDistance)
                continue;

            nearestTarget = candidate;
            nearestSqrDistance = sqrDistance;
        }

        if (nearestTarget == null)
            return false;

        SetTarget(nearestTarget);
        return true;
    }

    public bool CanPerceiveTarget(Transform candidate)
    {
        if (candidate == null)
            return false;

        Vector2 origin = ResolvePerceptionOrigin();
        Vector2 destination = ResolvePerceptionPoint(candidate);
        Vector2 delta = destination - origin;
        float distance = delta.magnitude;
        if (distance <= 0.001f)
            return true;

        ContactFilter2D filter = new ContactFilter2D();
        filter.useLayerMask = false;
        filter.useTriggers = false;

        int hitCount = Physics2D.Raycast(
            origin,
            delta / distance,
            filter,
            DoorSightHitBuffer,
            distance);

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider = DoorSightHitBuffer[i].collider;
            if (hitCollider == null || hitCollider.isTrigger)
                continue;

            if (IsColliderOwnedByTransform(hitCollider, transform) ||
                IsColliderOwnedByTransform(hitCollider, candidate))
            {
                continue;
            }

            if (CombatPathBlocker2DUtility.BlocksCombatPath(hitCollider, gameObject, CombatPathBlockerQuery.Sight))
                return false;
        }

        return true;
    }

    private Vector2 ResolvePerceptionOrigin()
    {
        if (collision != null)
            return collision.bounds.center;

        return transform.position;
    }

    private static Vector2 ResolvePerceptionPoint(Transform candidate)
    {
        if (candidate == null)
            return Vector2.zero;

        Collider2D collider = candidate.GetComponent<Collider2D>();
        if (collider == null)
            collider = candidate.GetComponentInChildren<Collider2D>();

        if (collider != null)
            return collider.bounds.center;

        return candidate.position;
    }

    /// <summary>
    /// 책임 :
    /// 범위 검색에 걸린 collider가 실제 추적 대상인지 태그 기준으로 확인하고 대표 Transform을 반환한다.
    /// </summary>
    private Transform ResolveTargetCandidate(Collider2D hit)
    {
        if (UsesPlayerTargetTag())
            return ResolvePlayerTargetCandidate(hit);

        if (hit.CompareTag(targetTag))
            return hit.transform;

        Transform current = hit.transform.parent;
        while (current != null)
        {
            if (current.CompareTag(targetTag))
                return current;

            current = current.parent;
        }

        return null;
    }

    private bool UsesPlayerTargetTag()
    {
        return string.Equals(targetTag, "Player", System.StringComparison.Ordinal);
    }

    private Transform ResolveTaggedTargetTransform(Transform foundTransform)
    {
        if (!UsesPlayerTargetTag() || foundTransform == null)
            return foundTransform;

        PlayerInteractor2D player = foundTransform.GetComponentInParent<PlayerInteractor2D>();
        if (player != null)
            return player.transform;

        return foundTransform;
    }

    private static bool TryResolveRegisteredPlayerTarget(out Transform playerTarget)
    {
        playerTarget = PlayerRuntimeRegistry.GetPlayerTransform();
        return playerTarget != null;
    }

    private Transform ResolvePlayerTargetCandidate(Collider2D hit)
    {
        if (hit == null)
            return null;

        Transform registeredPlayer = PlayerRuntimeRegistry.GetPlayerTransform();
        if (registeredPlayer != null && IsColliderOwnedByTransform(hit, registeredPlayer))
            return registeredPlayer;

        PlayerInteractor2D player = hit.GetComponentInParent<PlayerInteractor2D>();
        if (player != null)
            return player.transform;

        return hit.CompareTag(targetTag) ? hit.transform : null;
    }

    private static bool IsColliderOwnedByTransform(Collider2D hit, Transform owner)
    {
        if (hit == null || owner == null)
            return false;

        Transform hitTransform = hit.transform;
        if (hitTransform == owner || hitTransform.IsChildOf(owner))
            return true;

        Rigidbody2D attachedBody = hit.attachedRigidbody;
        return attachedBody != null &&
               (attachedBody.transform == owner || attachedBody.transform.IsChildOf(owner));
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

    /// <summary>외부 시스템이 적에게 안전한 사망 명령을 보낼 수 있는 공용 인터페이스 구현입니다.</summary>
    public void RequestDeath(GameObject killer = null)
    {
        Die();
    }

    /// <summary>파생 클래스가 공통 사망 처리 시작 직전에 전용 정리를 끼워 넣는 훅입니다.</summary>
    protected virtual void OnDeathStarted() { }

    /// <summary>사망 시 이동, 충돌, 물리 처리를 정지합니다.</summary>
    protected virtual void StopDeathGameplay()
    {
        if (movementMotor != null)
            movementMotor.StopAllMotion();

        if (collisionProfile != null)
            collisionProfile.SetBodyCollisionMode(EntityCollisionProfile2D.BodyCollisionMode.Disabled);
        else if (collision != null)
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
