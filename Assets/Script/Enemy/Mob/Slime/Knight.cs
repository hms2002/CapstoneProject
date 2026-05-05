using UnityEngine;
using UnityGAS;

[DisallowMultipleComponent]
[RequireComponent(typeof(MobAbilityCoordinator))]
[RequireComponent(typeof(KnightJumpSlamRunner))]
public class Knight : Slime
{
    private const float AttackRange = 5.5f;
    private const float TravelSeconds = 1.4f;
    private const float TravelEaseOutPower = 3.5f;
    private const float AirborneVisualHeight = 1.4f;
    private const float AirborneBodyHeight = 1f;
    private const float LandingDropSeconds = 0.18f;
    private const float LandingDropSharpness = 3.5f;
    private const float ImpactDiameter = 3.2f;
    private const float MaxHealth = 6f;
    private const float VisualScale = 0.9f;
    private const float ChaseSpeedMultiplier = 1f;
    private const float DamageAmount = 1f;
    private const float KnockbackImpulse = 8f;
    private const float SplitSpread = 0.55f;
    private const float AttackRecoverSeconds = 0.25f;

    private static readonly AnimationCurve JumpCurve = new(
        new Keyframe(0f, 0f),
        new Keyframe(0.18f, 1f),
        new Keyframe(0.78f, 0.95f),
        new Keyframe(0.92f, 0.35f),
        new Keyframe(1f, 0f));

    [SerializeField] private GameObject splitPrefab;
    [SerializeField] private AbilityDefinition jumpSlamAbility;
    [SerializeField] private GE_Damage_Spec damageEffect;
    [SerializeField] private GE_Knockback_Spec knockbackEffect;
    [SerializeField, Min(0)] private int splitCount = 4;

    private KnightJumpSlamRunner jumpSlamRunner;
    private bool hasLoggedInvalidConfig;

    public readonly struct JumpSlamContext
    {
        public readonly GameObject Target;
        public readonly Vector2 StartPos;
        public readonly Vector2 ImpactPos;
        public readonly float TravelSeconds;
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
        SpawnSplit<Pawn>(splitPrefab, splitCount, SplitSpread);
        base.OnDeathStarted();
    }

    protected override void PlayDeathAnimation()
    {
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

        context = new JumpSlamContext(
            targetObject,
            transform.position,
            targetObject.transform.position,
            TravelSeconds,
            TravelEaseOutPower,
            AirborneVisualHeight,
            AirborneBodyHeight,
            LandingDropSeconds,
            LandingDropSharpness,
            ImpactDiameter,
            MakePayload(system, spec, damageEffect, knockbackEffect, DamageAmount, KnockbackImpulse));
        return true;
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
}
