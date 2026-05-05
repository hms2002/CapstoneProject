using UnityEngine;
using UnityGAS;

[DisallowMultipleComponent]
[RequireComponent(typeof(MobAbilityCoordinator))]
[RequireComponent(typeof(RookChargeRunner))]
public class Rook : Slime
{
    private const float AttackRange = 5f;
    private const float WarningTime = 1.8f;
    private const float DashDistance = 5f;
    private const float WarningWidth = 1.1f;
    private const float MaxHealth = 13f;
    private const float VisualScale = 1.2f;
    private const float ChaseSpeedMultiplier = 0.5f;
    private const float DamageAmount = 2f;
    private const float KnockbackImpulse = 8f;
    private const float SplitSpread = 0.55f;

    [SerializeField] private GameObject splitPrefab;
    [SerializeField] private AbilityDefinition chargeAbility;
    [SerializeField] private GE_Damage_Spec damageEffect;
    [SerializeField] private GE_Knockback_Spec knockbackEffect;
    [SerializeField] private float dashSpeedMultiplier = 3f;
    [SerializeField, Min(0)] private int splitCount = 2;

    private RookChargeRunner chargeRunner;
    private bool suppressSplit;
    private bool hasLoggedInvalidConfig;

    public readonly struct ChargeContext
    {
        public readonly GameObject Target;
        public readonly Vector2 StartPos;
        public readonly Vector2 Direction;
        public readonly float WarningTime;
        public readonly float DashDistance;
        public readonly float DashSpeed;
        public readonly float WarningWidth;
        public readonly CombatHitPayload HitPayload;

        public ChargeContext(
            GameObject target,
            Vector2 startPos,
            Vector2 direction,
            float warningTime,
            float dashDistance,
            float dashSpeed,
            float warningWidth,
            CombatHitPayload hitPayload)
        {
            Target = target;
            StartPos = startPos;
            Direction = direction;
            WarningTime = warningTime;
            DashDistance = dashDistance;
            DashSpeed = dashSpeed;
            WarningWidth = warningWidth;
            HitPayload = hitPayload;
        }
    }

    protected override void Awake()
    {
        base.Awake();

        CacheCoordinator();

        chargeRunner = GetComponent<RookChargeRunner>();
        if (chargeRunner == null)
            chargeRunner = gameObject.AddComponent<RookChargeRunner>();

        ApplyStats();
    }

    protected override void Start()
    {
        base.Start();
        GiveAbility(chargeAbility);
    }

    public override bool CanUseChaseMovement()
    {
        UpdateSpeed(ChaseSpeedMultiplier);

        if (!CanMove()) return false;

        return chargeRunner == null || !chargeRunner.IsRunning;
    }

    protected override void OnDeathStarted()
    {
        CancelAbility();

        if (!suppressSplit)
            SpawnSplit<Knight>(splitPrefab, splitCount, SplitSpread);

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

    /// <summary>분열로 생성된 룩의 대기 시간과 분열 가능 상태를 설정합니다.</summary>
    public override void InitSplit(Transform nextTarget)
    {
        suppressSplit = false;
        base.InitSplit(nextTarget);
    }

    /// <summary>룩 돌진 공격에 필요한 실행 정보를 만듭니다.</summary>
    public bool TryBuildChargeContext(AbilitySystem system, AbilitySpec spec, GameObject explicitTarget, out ChargeContext context)
    {
        context = default;

        if (!CanAct()) return false;
        if (!HasChargeData()) return false;

        GameObject targetObject = GetTarget(explicitTarget);
        if (!InRange(targetObject, AttackRange)) return false;

        Vector2 direction = GetDirection(targetObject);
        context = new ChargeContext(
            targetObject,
            transform.position,
            direction,
            WarningTime,
            DashDistance,
            GetDashSpeed(),
            WarningWidth,
            MakePayload(system, spec, damageEffect, knockbackEffect, DamageAmount, KnockbackImpulse));
        return true;
    }

    /// <summary>룩이 구덩이에 빠졌을 때 분열 없이 즉사 처리합니다.</summary>
    public void FallIntoHole()
    {
        if (isDead) return;

        suppressSplit = true;
        Die();
    }

    /// <summary>FSM에서 사용할 룩 돌진 공격 요청을 만듭니다.</summary>
    public override bool TryBuildAttackRequest(out MobAttackRequest request)
    {
        request = default;

        if (!CanAct()) return false;
        if (!HasChargeData()) return false;

        GameObject targetObject = target != null ? target.gameObject : null;
        if (!InRange(targetObject, AttackRange)) return false;

        request = new MobAttackRequest(chargeAbility, targetObject);
        return request.IsValid;
    }

    /// <summary>룩의 돌진 지속 시간을 계산합니다.</summary>
    public float GetDashTime(float dashSpeed)
    {
        if (dashSpeed <= 0f) return 0f;

        return DashDistance / dashSpeed;
    }

    /// <summary>룩이 사용할 돌진 속도를 계산합니다.</summary>
    public float GetDashSpeed()
    {
        return GetPlayerSpeed() * dashSpeedMultiplier;
    }

    /// <summary>룩의 기본 스탯과 크기를 적용합니다.</summary>
    protected override void ApplyStats()
    {
        SetStats("Rook", MaxHealth, VisualScale);
    }

    /// <summary>룩 돌진 설정이 모두 연결되어 있는지 확인합니다.</summary>
    private bool HasChargeData()
    {
        bool isValid = chargeAbility != null &&
                       damageEffect != null &&
                       knockbackEffect != null &&
                       abilitySystem != null &&
                       chargeRunner != null;

        if (isValid) return true;

        if (!hasLoggedInvalidConfig)
        {
            Debug.LogError($"[{nameof(Rook)}] 돌진 설정이 비어 있습니다.", this);
            hasLoggedInvalidConfig = true;
        }

        return false;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        chargeRunner?.HandleBodyCollision(collision);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        chargeRunner?.HandleTrigger(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        chargeRunner?.HandleTrigger(other);
    }
}
