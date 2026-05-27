using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// - 슬라임 계열 Knight 몬스터의 점프 내려찍기 공격 데이터와 분열 사망 처리를 정의한다.
/// - 공격 착지 지점, 피해 판정, 이동 가능 여부 같은 Knight 고유 전투 문맥을 제공한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MobAbilityCoordinator))]
[RequireComponent(typeof(KnightJumpSlamRunner))]
public class Knight : Slime
{
    private const string JumpTriggerName = "jump";
    private const string SlamTriggerName = "slam";
    private const string DieTriggerName = "die";
    private const float AttackRange = 5.5f;
    private const float TravelSeconds = 0.85f;
    private const float TravelEaseOutPower = 2.2f;
    private const float HorizontalTravelNormalized = 0.28f;
    private const float AirborneVisualHeight = 1.4f;
    private const float AirborneBodyHeight = 1f;
    private const float LandingDropSeconds = 0.04f;
    private const float LandingDropSharpness = 0.22f;
    private const float ImpactDiameter = 3.2f;
    private const float MaxHealth = 6f;
    private const float VisualScale = 0.9f;
    private const float ChaseSpeedMultiplier = 1f;
    private const float KnockbackImpulse = 12f;
    private const float SplitSpread = 0.55f;
    private const float AttackRecoverSeconds = 0.25f;
    private const float DefaultJumpBlockedSkin = 0.08f;
    private const float DefaultJumpLandingProbeRadius = 0.22f;

    private static readonly AnimationCurve JumpCurve = new(
        new Keyframe(0f, 0f),
        new Keyframe(HorizontalTravelNormalized, 1f),
        new Keyframe(0.86f, 1f),
        new Keyframe(0.96f, 0.9f),
        new Keyframe(1f, 0f));

    [SerializeField] private GameObject splitPrefab;
    [SerializeField] private AbilityDefinition jumpSlamAbility;
    [SerializeField] private GE_Damage_Spec damageEffect;
    [SerializeField] private GE_Knockback_Spec knockbackEffect;
    [SerializeField] private float slamDamageAmount = 1f;
    [SerializeField, Min(0)] private int splitCount = 4;
    [Header("Jump Landing")]
    [Tooltip("점프 내려찍기 착지 지점이 벽/사물 너머로 잡히지 않도록 검사할 레이어입니다. 비워두면 Wall, Default, Non_FightCollision을 사용합니다.")]
    [SerializeField] private LayerMask jumpBlockedLayers;
    [SerializeField, Min(0f)] private float jumpBlockedSkin = DefaultJumpBlockedSkin;
    [SerializeField, Min(0.01f)] private float jumpLandingProbeRadius = DefaultJumpLandingProbeRadius;

    private KnightJumpSlamRunner jumpSlamRunner;
    private readonly RaycastHit2D[] jumpLandingHits = new RaycastHit2D[8];
    private bool hasJumpTrigger;
    private bool hasSlamTrigger;
    private bool hasDieTrigger;
    private bool hasLoggedInvalidConfig;

    public readonly struct JumpSlamContext
    {
        public readonly GameObject Target;
        public readonly Vector2 StartPos;
        public readonly Vector2 ImpactPos;
        public readonly float TravelSeconds;
        public readonly float HorizontalTravelSeconds;
        public readonly float TravelEaseOutPower;
        public readonly float AirborneVisualHeight;
        public readonly float AirborneBodyHeight;
        public readonly float LandingDropSeconds;
        public readonly float LandingDropSharpness;
        public readonly float ImpactDiameter;
        public readonly CombatHitPayload HitPayload;

        public JumpSlamContext(
            GameObject target,
            Vector2 startPos,
            Vector2 impactPos,
            float travelSeconds,
            float horizontalTravelSeconds,
            float travelEaseOutPower,
            float airborneVisualHeight,
            float airborneBodyHeight,
            float landingDropSeconds,
            float landingDropSharpness,
            float impactDiameter,
            CombatHitPayload hitPayload)
        {
            Target = target;
            StartPos = startPos;
            ImpactPos = impactPos;
            TravelSeconds = travelSeconds;
            HorizontalTravelSeconds = horizontalTravelSeconds;
            TravelEaseOutPower = travelEaseOutPower;
            AirborneVisualHeight = airborneVisualHeight;
            AirborneBodyHeight = airborneBodyHeight;
            LandingDropSeconds = landingDropSeconds;
            LandingDropSharpness = landingDropSharpness;
            ImpactDiameter = impactDiameter;
            HitPayload = hitPayload;
        }
    }

    protected override void Awake()
    {
        base.Awake();

        CacheCoordinator();

        jumpSlamRunner = GetComponent<KnightJumpSlamRunner>();
        if (jumpSlamRunner == null)
            jumpSlamRunner = gameObject.AddComponent<KnightJumpSlamRunner>();

        CacheAnimatorParameters();
        ApplyStats();
    }

    protected override void Start()
    {
        base.Start();
        GiveAbility(jumpSlamAbility);
    }

    public override bool CanUseChaseMovement()
    {
        UpdateSpeed(ChaseSpeedMultiplier);

        if (!CanMove()) return false;

        return jumpSlamRunner == null || !jumpSlamRunner.IsRunning;
    }

    protected override void OnDeathStarted()
    {
        CancelAbility();
        if (!IsPitFallDeath && splitPrefab != null && splitCount > 0)
        {
            PlaySplitDeathVanishEffect();
            SpawnSplit<Pawn>(splitPrefab, splitCount, SplitSpread);
        }

        base.OnDeathStarted();
    }

    protected override void PlayDeathAnimation()
    {
        SetAnimatorTriggerIfAvailable(DieTriggerName, hasDieTrigger);
    }

    /// <summary>
    /// 책임:
    /// - Knight 점프 시작 타이밍을 Animator trigger로 전달한다.
    /// - 실제 이동/높이 처리는 Runner가 유지하고, 이 메서드는 표현 상태 전환만 담당한다.
    /// </summary>
    public void PlayJumpAnimation()
    {
        SetAnimatorTriggerIfAvailable(JumpTriggerName, hasJumpTrigger);
    }

    /// <summary>
    /// 책임:
    /// - Knight 급강하/내려찍기 타이밍을 Animator trigger로 전달한다.
    /// - 착지 피해 판정과 분리해 애니메이션 전환 책임만 가진다.
    /// </summary>
    public void PlaySlamAnimation()
    {
        SetAnimatorTriggerIfAvailable(SlamTriggerName, hasSlamTrigger);
    }

    protected override void DrawAttackGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, AttackRange);
    }

    /// <summary>나이트의 점프 내려치기 공격 요청을 만듭니다.</summary>
    public override bool TryBuildAttackRequest(out MobAttackRequest request)
    {
        request = default;

        if (!CanAct()) return false;
        if (!HasJumpData()) return false;

        GameObject targetObject = target != null ? target.gameObject : null;
        if (!InRange(targetObject, AttackRange)) return false;
        if (!CanJumpDirectlyToTarget(targetObject)) return false;

        request = new MobAttackRequest(jumpSlamAbility, targetObject, AttackRecoverSeconds);
        return request.IsValid;
    }

    /// <summary>점프 내려치기에 필요한 실행 정보를 만듭니다.</summary>
    public bool TryBuildJumpContext(AbilitySystem system, AbilitySpec spec, GameObject explicitTarget, out JumpSlamContext context)
    {
        context = default;

        if (!CanAct()) return false;
        if (!HasJumpData()) return false;

        GameObject targetObject = GetTarget(explicitTarget);
        if (!InRange(targetObject, AttackRange)) return false;
        if (!CanJumpDirectlyToTarget(targetObject)) return false;

        context = new JumpSlamContext(
            targetObject,
            transform.position,
            ResolveReachableImpactPosition(transform.position, targetObject.transform.position),
            TravelSeconds,
            TravelSeconds * HorizontalTravelNormalized,
            TravelEaseOutPower,
            AirborneVisualHeight,
            AirborneBodyHeight,
            LandingDropSeconds,
            LandingDropSharpness,
            ImpactDiameter,
            MakePayload(system, spec, damageEffect, knockbackEffect, slamDamageAmount, KnockbackImpulse));
        return true;
    }

    /// <summary>점프 내려찍기 착지 위치가 벽/사물 너머로 잡히지 않도록 실제 도달 가능한 지점으로 보정합니다.</summary>
    private Vector2 ResolveReachableImpactPosition(Vector2 startPosition, Vector2 desiredImpactPosition)
    {
        Vector2 delta = desiredImpactPosition - startPosition;
        float distance = delta.magnitude;
        if (distance <= 0.001f)
            return desiredImpactPosition;

        LayerMask blockedLayers = ResolveJumpBlockedLayers();
        if (blockedLayers.value == 0)
            return desiredImpactPosition;

        Vector2 direction = delta / distance;
        ContactFilter2D filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = blockedLayers,
            useTriggers = false
        };

        int hitCount = Physics2D.CircleCast(
            startPosition,
            Mathf.Max(0.01f, jumpLandingProbeRadius),
            direction,
            filter,
            jumpLandingHits,
            distance);

        float nearestDistance = distance;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = jumpLandingHits[i];
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
                continue;

            nearestDistance = Mathf.Min(nearestDistance, hit.distance);
        }

        float allowedDistance = Mathf.Clamp(nearestDistance - Mathf.Max(0f, jumpBlockedSkin), 0f, distance);
        return startPosition + direction * allowedDistance;
    }

    /// <summary>
    /// 책임:
    /// - Knight가 목표 위치까지 벽/사물에 막히지 않고 직접 점프 내려찍기를 사용할 수 있는지 판단한다.
    /// - 조건을 만족하지 않으면 공격 요청을 만들지 않아 FSM이 Chase 상태로 위치를 다시 잡게 한다.
    /// </summary>
    private bool CanJumpDirectlyToTarget(GameObject targetObject)
    {
        if (targetObject == null)
            return false;

        Vector2 startPosition = transform.position;
        Vector2 targetPosition = targetObject.transform.position;
        Vector2 delta = targetPosition - startPosition;
        float distance = delta.magnitude;
        if (distance <= 0.001f)
            return true;

        LayerMask blockedLayers = ResolveJumpBlockedLayers();
        if (blockedLayers.value == 0)
            return true;

        Vector2 direction = delta / distance;
        ContactFilter2D filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = blockedLayers,
            useTriggers = false
        };

        int hitCount = Physics2D.CircleCast(
            startPosition,
            Mathf.Max(0.01f, jumpLandingProbeRadius),
            direction,
            filter,
            jumpLandingHits,
            distance);

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = jumpLandingHits[i];
            if (hit.collider == null)
                continue;

            if (hit.collider.transform.IsChildOf(transform))
                continue;

            if (hit.collider.transform.IsChildOf(targetObject.transform))
                continue;

            return false;
        }

        return true;
    }

    /// <summary>인스펙터 값이 비어 있으면 프로젝트 표준 벽/사물 레이어를 사용합니다.</summary>
    private LayerMask ResolveJumpBlockedLayers()
    {
        if (jumpBlockedLayers.value != 0)
            return jumpBlockedLayers;

        int mask = 0;
        int wallLayer = LayerMask.NameToLayer("Wall");
        int defaultLayer = LayerMask.NameToLayer("Default");
        int nonFightCollisionLayer = LayerMask.NameToLayer("Non_FightCollision");

        if (wallLayer >= 0)
            mask |= 1 << wallLayer;
        if (defaultLayer >= 0)
            mask |= 1 << defaultLayer;
        if (nonFightCollisionLayer >= 0)
            mask |= 1 << nonFightCollisionLayer;

        return mask;
    }

    /// <summary>점프 높이 곡선 값을 계산합니다.</summary>
    public float GetJumpHeight(float normalizedTime)
    {
        return Mathf.Max(0f, JumpCurve.Evaluate(Mathf.Clamp01(normalizedTime)));
    }

    /// <summary>착지 직전 급강하 보정 값을 계산합니다.</summary>
    public float GetDropScale(float elapsed, float duration)
    {
        float dropDuration = Mathf.Clamp(LandingDropSeconds, 0.01f, duration);
        float dropStart = Mathf.Max(0f, duration - dropDuration);
        if (elapsed < dropStart) return 1f;

        float normalizedDrop = Mathf.Clamp01((elapsed - dropStart) / dropDuration);
        return 1f - Mathf.Pow(normalizedDrop, LandingDropSharpness);
    }

    /// <summary>착지 범위 안의 대상에게 피해를 적용합니다.</summary>
    public void ApplyImpactDamage(JumpSlamContext context)
    {
        if (context.HitPayload == null || !context.HitPayload.IsValid()) return;

        float radius = Mathf.Max(0.05f, context.ImpactDiameter * 0.5f);
        LayerMask targetMask = context.Target != null
            ? (LayerMask)(1 << context.Target.layer)
            : Physics2D.DefaultRaycastLayers;
        Collider2D[] hits = Physics2D.OverlapCircleAll(context.ImpactPos, radius, targetMask);

        for (int i = 0; i < hits.Length; i++)
        {
            GameObject targetRoot = CombatTargetResolver2D.ResolveDamageTarget(hits[i]);
            if (targetRoot == null || targetRoot == gameObject) continue;

            CombatHitPayloadApplier.Apply(targetRoot, context.HitPayload, hits[i].ClosestPoint(context.ImpactPos));
            return;
        }
    }

    /// <summary>나이트의 기본 스탯과 크기를 적용합니다.</summary>
    protected override void ApplyStats()
    {
        SetStats("Knight", MaxHealth, VisualScale);
    }

    /// <summary>점프 내려치기 설정이 모두 연결되어 있는지 확인합니다.</summary>
    private bool HasJumpData()
    {
        bool isValid = jumpSlamAbility != null &&
                       damageEffect != null &&
                       knockbackEffect != null &&
                       abilitySystem != null &&
                       jumpSlamRunner != null;

        if (isValid) return true;

        if (!hasLoggedInvalidConfig)
        {
            Debug.LogError($"[{nameof(Knight)}] 점프 내려치기 설정이 비어 있습니다.", this);
            hasLoggedInvalidConfig = true;
        }

        return false;
    }

    /// <summary>Animator Controller에 Knight 전용 트리거가 있는지 캐시합니다.</summary>
    private void CacheAnimatorParameters()
    {
        hasJumpTrigger = HasAnimatorParameter(JumpTriggerName, AnimatorControllerParameterType.Trigger);
        hasSlamTrigger = HasAnimatorParameter(SlamTriggerName, AnimatorControllerParameterType.Trigger);
        hasDieTrigger = HasAnimatorParameter(DieTriggerName, AnimatorControllerParameterType.Trigger);
    }

    /// <summary>지정한 Animator 파라미터가 존재하고 타입이 맞는지 확인합니다.</summary>
    private bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType parameterType)
    {
        if (animator == null)
            return false;

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.type == parameterType && parameter.name == parameterName)
                return true;
        }

        return false;
    }

    /// <summary>파라미터가 존재할 때만 Animator trigger를 전달해 authoring 중 콘솔 오류를 방지합니다.</summary>
    private void SetAnimatorTriggerIfAvailable(string triggerName, bool hasTrigger)
    {
        if (!hasTrigger || animator == null)
            return;

        animator.SetTrigger(triggerName);
    }
}
