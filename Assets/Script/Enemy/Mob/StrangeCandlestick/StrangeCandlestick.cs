using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - StrangeCandlestick의 발사 조건 판단과 발사 설정 데이터를 보관한다.
/// - 실제 락온-발사 시퀀스 실행은 AD/runner에 위임하고, 본체는 상위 판단과 데이터 제공에 집중한다.
/// </summary>
public class StrangeCandlestick : Mob, IMobAttackDecisionSource
{
    private static readonly System.Collections.Generic.List<StrangeCandlestick> instances = new();
    private const int WallLayer = 30;
    [Header("Ability")]
    [SerializeField] private AbilityDefinition attackAbilityDefinition;
    [SerializeField] private MobAbilityCoordinator abilityCoordinator;
    [SerializeField] private StrangeCandlestickAttackRunner attackRunner;

    private EnemyChaseIntent2D detectionSensor;
    private IMobAbilityHelperAccess helperAccess;
    private CandlestickSeal candlestickSeal;
    private AttackTelegraphService telegraphService;
    private AttackTelegraphStyle runtimeLockOnStyle;
    private float nextProjectileFireTime;
    private bool hasLoggedInvalidConfig;

    public static System.Collections.Generic.IReadOnlyList<StrangeCandlestick> Instances => instances;
    public bool IsSealed => candlestickSeal != null && candlestickSeal.IsSealed;

    protected override void Awake()
    {
        base.Awake();
        detectionSensor = GetComponent<EnemyChaseIntent2D>();
        candlestickSeal = GetComponent<CandlestickSeal>();
        telegraphService = GetComponent<AttackTelegraphService>();
        runtimeLockOnStyle = null;

        if (abilityCoordinator == null)
            abilityCoordinator = GetComponent<MobAbilityCoordinator>();
        if (abilityCoordinator == null)
            abilityCoordinator = gameObject.AddComponent<MobAbilityCoordinator>();
        helperAccess = abilityCoordinator as IMobAbilityHelperAccess;

        if (attackRunner == null)
            attackRunner = GetComponent<StrangeCandlestickAttackRunner>();
        if (attackRunner == null)
            attackRunner = gameObject.AddComponent<StrangeCandlestickAttackRunner>();
    }

    protected override void Start()
    {
        base.Start();
        EnsureAttackAbility();
    }

    /// <summary>
    /// 책임 :
    /// - StrangeCandlestick 공격 패턴 데이터의 현재 공식 소유자를 AL로 통일해 helper와 runner가 같은 설정을 보게 한다.
    /// - AL asset이 없거나 잘못 연결된 경우를 바로 드러내고, 패턴 실행 데이터가 owner fallback로 되돌아가지 않게 한다.
    /// </summary>
    public AbilityLogic_StrangeCandlestickAttack.PatternData GetAttackPatternData()
    {
        AbilityLogic_StrangeCandlestickAttack logic = GetAttackLogic();
        return logic != null ? logic.Data : default;
    }

    private AbilityLogic_StrangeCandlestickAttack GetAttackLogic()
    {
        return attackAbilityDefinition != null
            ? attackAbilityDefinition.logic as AbilityLogic_StrangeCandlestickAttack
            : null;
    }

    private void OnEnable()
    {
        if (!instances.Contains(this))
            instances.Add(this);
    }

    private void OnDisable()
    {
        instances.Remove(this);
        attackRunner?.Cancel();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        if (runtimeLockOnStyle != null)
            Destroy(runtimeLockOnStyle);
    }

    public override bool CanUseChaseMovement()
    {
        return false;
    }

    /// <summary>고정형 촛대 몬스터는 타겟을 따라 방향을 바꾸지 않습니다.</summary>
    protected override void UpdateFacing()
    {
    }

    /// <summary>촛대를 봉인 상태로 만듭니다.</summary>
    public bool Seal()
    {
        if (candlestickSeal == null)
            return false;

        candlestickSeal.Seal();
        return true;
    }

    protected override void OnEnemyAttributeChanged(AttributeDefinition attribute, float oldValue, float newValue)
    {
        if (attribute == healthDef &&
            newValue < oldValue &&
            candlestickSeal != null &&
            candlestickSeal.UseHit())
        {
            attributeSet?.TrySetCurrentValue(healthDef, oldValue, this);
            return;
        }

        base.OnEnemyAttributeChanged(attribute, oldValue, newValue);
    }

    protected override void OnDeathStarted()
    {
        abilityCoordinator?.CancelActiveAbility(true);
        base.OnDeathStarted();
    }

    /// <summary>촛대 종료 애니메이션을 재생합니다.</summary>
    protected override void PlayDeathAnimation()
    {
        if (animator != null)
            animator.SetBool("isLightOn", false);
    }

    /// <summary>지금 탄막을 발사할 수 있는지 확인합니다.</summary>
    public bool CanTryProjectileAttack(GameObject explicitTarget)
    {
        if (isDead)
            return false;

        if (Time.time < nextProjectileFireTime)
            return false;

        if (helperAccess != null &&
            attackAbilityDefinition != null &&
            helperAccess.GetCooldownRemaining(attackAbilityDefinition) > 0f)
            return false;

        if (!HasShootData())
            return false;

        if (explicitTarget == null)
            return false;

        return IsTargetInRange(explicitTarget.transform);
    }

    /// <summary>발사에 필요한 참조가 있는지 확인합니다.</summary>
    private bool HasShootData()
    {
        AbilityLogic_StrangeCandlestickAttack.PatternData data = GetAttackPatternData();

        bool isValid = data.projectilePrefab != null &&
                       data.damageEffect != null &&
                       abilitySystem != null;

        if (isValid)
            return true;

        if (!hasLoggedInvalidConfig)
        {
            Debug.LogError(
                $"[{nameof(StrangeCandlestick)}] LightBead 발사 설정이 비어 있습니다.",
                this);

            hasLoggedInvalidConfig = true;
        }

        return false;
    }

    /// <summary>플레이어가 감지 범위 안에 있는지 확인합니다.</summary>
    private bool IsTargetInRange(Transform targetTransform)
    {
        if (targetTransform == null || detectionSensor == null)
            return false;

        float detectionRange = Mathf.Max(0f, detectionSensor.DetectionRange);
        if (detectionRange <= 0f)
            return false;

        Vector2 toTarget = (Vector2)(targetTransform.position - transform.position);
        return toTarget.sqrMagnitude <= detectionRange * detectionRange;
    }

    /// <summary>runner가 사용할 공격 문맥을 구성합니다.</summary>
    public bool TryBuildProjectileAttackContext(GameObject explicitTarget, out StrangeCandlestickAttackRunner.AttackContext context)
    {
        context = default;

        GameObject targetObject = explicitTarget != null
            ? explicitTarget
            : target != null ? target.gameObject : null;

        if (!CanTryProjectileAttack(targetObject))
            return false;

        AbilityLogic_StrangeCandlestickAttack.PatternData data = GetAttackPatternData();
        context = new StrangeCandlestickAttackRunner.AttackContext(
            targetObject,
            Mathf.Max(0f, data.lockOnDuration));
        return true;
    }

    /// <summary>발사 가능 여부를 평가하고, 가능하면 bridge를 통해 발사 어빌리티를 요청합니다.</summary>
    public bool TryRequestProjectileAttack(GameObject explicitTarget)
    {
        if (abilityCoordinator == null || attackAbilityDefinition == null)
            return false;

        GameObject targetObject = explicitTarget != null
            ? explicitTarget
            : target != null ? target.gameObject : null;

        if (!CanTryProjectileAttack(targetObject))
            return false;

        if (!TryBuildProjectileAttackContext(targetObject, out _))
            return false;

        return abilityCoordinator.TryStartAbility(attackAbilityDefinition, targetObject);
    }

    /// <summary>FSM AttackState가 사용할 발사 요청을 구성합니다.</summary>
    public bool TryBuildAttackRequest(out MobAttackRequest request)
    {
        request = default;

        GameObject targetObject = target != null ? target.gameObject : null;
        if (!TryBuildProjectileAttackContext(targetObject, out _))
            return false;

        request = new MobAttackRequest(attackAbilityDefinition, targetObject);
        return request.IsValid;
    }

    /// <summary>공격 상태 진입 시 StrangeCandlestick이 추가로 처리할 것이 없어 비워 둡니다.</summary>
    public void OnAttackStateEntered(MobAttackRequest request)
    {
    }

    /// <summary>공격 상태 종료 시 StrangeCandlestick이 추가로 정리할 것이 없어 비워 둡니다.</summary>
    public void OnAttackStateExited(MobAttackRequest request, bool wasCancelled)
    {
    }

    /// <summary>기존 runner 경로 호환을 위해 공격 문맥 생성 API를 유지합니다.</summary>
    public bool TryCreateAttackContext(GameObject explicitTarget, out StrangeCandlestickAttackRunner.AttackContext context)
    {
        return TryBuildProjectileAttackContext(explicitTarget, out context);
    }

    /// <summary>runner가 락온 도중 계속 공격을 유지할 수 있는지 확인합니다.</summary>
    public bool CanContinueAttack(GameObject explicitTarget)
    {
        if (explicitTarget == null)
            return false;

        return HasShootData() && !isDead && IsTargetInRange(explicitTarget.transform);
    }

    /// <summary>LightBead를 생성하고 발사 설정을 넘깁니다.</summary>
    public bool FireProjectile(GameObject explicitTarget)
    {
        if (explicitTarget == null)
            return false;

        AbilityLogic_StrangeCandlestickAttack.PatternData data = GetAttackPatternData();
        Vector2 shotDirection = GetLaunchDirection(explicitTarget.transform);

        CombatHitPayload payload = MakeHitPayload();
        if (payload == null)
            return false;

        GameObject lightBeadObject = Instantiate(data.projectilePrefab, transform.position, Quaternion.identity);
        LightBeadProjectile2D lightBead = lightBeadObject.GetComponent<LightBeadProjectile2D>();

        if (lightBead == null)
        {
            Debug.LogError(
                $"[{nameof(StrangeCandlestick)}] {data.projectilePrefab.name}에 {nameof(LightBeadProjectile2D)}가 없습니다.",
                lightBeadObject);

            Destroy(lightBeadObject);
            return false;
        }

        ProjectileAttackSpawnContext context = new ProjectileAttackSpawnContext
        {
            ownerSystem = abilitySystem,
            sourceSpec = null,
            causer = gameObject,
            ignoreTarget = gameObject,
            lifetime = float.MaxValue,
            wallLayers = 1 << WallLayer,
            damageLayers = GetDamageMask(explicitTarget),
            hitPayload = payload,
            direction = shotDirection,
            speed = Mathf.Max(0f, data.projectileSpeed)
        };

        lightBead.Setup(context);
        nextProjectileFireTime = Time.time + CombatTimingService.ScaleSeconds(
            abilitySystem,
            GetPostShotCooldown(),
            CombatTimingSlot.AttackInterval);

        if (animator != null)
            animator.SetTrigger("attack");

        return true;
    }

    private void HideLockOnTelegraph()
    {
        if (telegraphService != null)
            telegraphService.HideCurrent();
    }

    public AttackTelegraphSpec MakeLockOnSpec(GameObject explicitTarget, float durationOverride = -1f)
    {
        AbilityLogic_StrangeCandlestickAttack.PatternData data = GetAttackPatternData();
        Vector2 start = transform.position;
        Vector2 end = explicitTarget != null ? (Vector2)explicitTarget.transform.position : start;
        Vector2 delta = end - start;
        float length = Mathf.Max(0.01f, delta.magnitude);
        Vector2 direction = delta.sqrMagnitude <= 0.0001f
            ? (sprite != null && sprite.flipX ? Vector2.left : Vector2.right)
            : delta / length;

        Vector3 center = start + direction * (length * 0.5f);
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        return AttackTelegraphSpec.CreateRectangle(
            center,
            new Vector2(length, Mathf.Max(0.01f, data.lockOnLineWidth)),
            angle,
            Mathf.Max(0.01f, durationOverride >= 0f ? durationOverride : data.lockOnDuration),
            GetLockOnStyle());
    }

    private AttackTelegraphStyle GetLockOnStyle()
    {
        AbilityLogic_StrangeCandlestickAttack.PatternData data = GetAttackPatternData();
        if (data.lockOnStyleAsset != null)
            return data.lockOnStyleAsset;

        if (runtimeLockOnStyle == null)
            runtimeLockOnStyle = MakeLockOnStyle();

        return runtimeLockOnStyle;
    }

    private AttackTelegraphStyle MakeLockOnStyle()
    {
        AbilityLogic_StrangeCandlestickAttack.PatternData data = GetAttackPatternData();
        AttackTelegraphStyle style = ScriptableObject.CreateInstance<AttackTelegraphStyle>();
        Color transparent = new Color(data.lockOnColor.r, data.lockOnColor.g, data.lockOnColor.b, 0f);

        style.fillColorStart = transparent;
        style.fillColorEnd = data.lockOnColor;
        style.borderColorStart = transparent;
        style.borderColorEnd = data.lockOnColor;
        style.progressCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        style.blinkStartNormalized = 1f;
        style.blinkFrequency = 0f;
        style.blinkAlphaMin = 1f;
        style.scaleFillWithProgress = false;
        style.fillScaleStart = 1f;
        style.fillScaleEnd = 1f;
        return style;
    }

    private float GetPostShotCooldown()
    {
        AbilityLogic_StrangeCandlestickAttack.PatternData data = GetAttackPatternData();
        return Mathf.Max(0f, data.attackIntervalSeconds - Mathf.Max(0f, data.lockOnDuration));
    }

    /// <summary>발사 방향을 계산합니다.</summary>
    private Vector2 GetLaunchDirection(Transform targetTransform)
    {
        if (targetTransform == null)
            return sprite != null && sprite.flipX ? Vector2.left : Vector2.right;

        Vector2 toTarget = (Vector2)(targetTransform.position - transform.position);

        if (toTarget.sqrMagnitude <= 0.0001f)
            return sprite != null && sprite.flipX ? Vector2.left : Vector2.right;

        return toTarget.normalized;
    }

    /// <summary>플레이어 레이어를 데미지 마스크로 만듭니다.</summary>
    private LayerMask GetDamageMask(GameObject explicitTarget)
    {
        return explicitTarget != null
            ? (LayerMask)(1 << explicitTarget.layer)
            : (LayerMask)0;
    }

    /// <summary>탄막이 사용할 피격 정보를 만듭니다.</summary>
    private CombatHitPayload MakeHitPayload()
    {
        AbilityLogic_StrangeCandlestickAttack.PatternData data = GetAttackPatternData();
        CombatDamageSnapshot snapshot = new CombatDamageSnapshot(
            finalHpDamage: data.damageAmount,
            finalStaggerBuildUp: 0f,
            finalKnockbackImpulse: 0f,
            isCriticalHit: false);

        return CombatHitPayload.FromSnapshot(
            sourceSystem: abilitySystem,
            sourceSpec: null,
            damageEffect: data.damageEffect,
            knockbackEffect: null,
            snapshot: snapshot,
            hitConfirmedTag: null,
            causer: gameObject);
    }

    private void EnsureAttackAbility()
    {
        if (abilitySystem == null || attackAbilityDefinition == null)
            return;

        if (abilitySystem.FindSpec(attackAbilityDefinition) == null)
            abilitySystem.GiveAbility(attackAbilityDefinition);
    }
}
