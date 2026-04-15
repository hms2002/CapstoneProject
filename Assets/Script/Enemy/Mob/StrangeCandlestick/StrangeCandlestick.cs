using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - StrangeCandlestick의 발사 조건 판단과 발사 설정 데이터를 보관한다.
/// - 실제 락온-발사 시퀀스 실행은 AD/runner에 위임하고, 본체는 상위 판단과 데이터 제공에 집중한다.
/// </summary>
public class StrangeCandlestick : Mob
{
    private static readonly System.Collections.Generic.List<StrangeCandlestick> instances = new();
    private const float ProjectileAttackInterval = 2f;
    private const int WallLayer = 30;

    [Header("LightBead")]
    [Tooltip("발사할 LightBead 프리팹입니다.")]
    [SerializeField] private GameObject lightBeadPrefab;

    [Tooltip("LightBead 이동 속도입니다.")]
    [SerializeField] private float projectileSpeed = 3f;

    [Tooltip("플레이어 적중 시 적용할 데미지 이펙트입니다.")]
    [SerializeField] private GE_Damage_Spec projectileDamageEffect;

    [Tooltip("LightBead 한 발의 피해량입니다.")]
    [SerializeField] private float projectileDamage = 1f;

    [Header("Lock On Warning")]
    [SerializeField] private float lockOnDuration = 0.8f;
    [SerializeField] private float lockOnLineWidth = 0.28f;
    [SerializeField] private Color lockOnColor = new Color(1f, 0.15f, 0.15f, 1f);
    [SerializeField] private AttackTelegraphStyle lockOnStyleAsset;
    [Header("Ability")]
    [SerializeField] private AbilityDefinition attackAbilityDefinition;
    [SerializeField] private MobAbilityCoordinator abilityCoordinator;
    [SerializeField] private StrangeCandlestickAttackRunner attackRunner;

    private EnemyChaseIntent2D detectionSensor;
    private CandlestickSeal candlestickSeal;
    private AttackTelegraphService telegraphService;
    private AttackTelegraphStyle runtimeLockOnStyle;
    private float nextProjectileFireTime;
    private bool hasLoggedInvalidConfig;
    private bool ownsRuntimeAbilityDefinition;
    private AbilityLogic_StrangeCandlestickAttack runtimeAttackLogic;

    public static System.Collections.Generic.IReadOnlyList<StrangeCandlestick> Instances => instances;
    public bool IsSealed => candlestickSeal != null && candlestickSeal.IsSealed;

    protected override void Awake()
    {
        base.Awake();
        detectionSensor = GetComponent<EnemyChaseIntent2D>();
        candlestickSeal = GetComponent<CandlestickSeal>();
        telegraphService = GetComponent<AttackTelegraphService>();
        runtimeLockOnStyle = lockOnStyleAsset == null ? MakeLockOnStyle() : null;

        if (abilityCoordinator == null)
            abilityCoordinator = GetComponent<MobAbilityCoordinator>();
        if (abilityCoordinator == null)
            abilityCoordinator = gameObject.AddComponent<MobAbilityCoordinator>();

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

        if (ownsRuntimeAbilityDefinition)
        {
            if (runtimeAttackLogic != null)
                Destroy(runtimeAttackLogic);
            if (attackAbilityDefinition != null)
                Destroy(attackAbilityDefinition);
        }
    }

    public override bool CanUseChaseMovement()
    {
        return false;
    }

    /// <summary>고정형 촛대 몬스터는 타겟을 따라 방향을 바꾸지 않습니다.</summary>
    protected override void UpdateFacing()
    {
    }

    protected override void UpdateAttack()
    {
        if (abilityCoordinator == null || attackAbilityDefinition == null)
            return;

        if (abilityCoordinator.IsAbilityExecutionBusy)
            return;

        if (!CanShoot(target != null ? target.gameObject : null))
            return;

        abilityCoordinator.TryStartAbility(attackAbilityDefinition, target != null ? target.gameObject : null);
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
    private bool CanShoot(GameObject explicitTarget)
    {
        if (isDead)
            return false;

        if (Time.time < nextProjectileFireTime)
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
        bool isValid = lightBeadPrefab != null &&
                       projectileDamageEffect != null &&
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
    public bool TryCreateAttackContext(GameObject explicitTarget, out StrangeCandlestickAttackRunner.AttackContext context)
    {
        context = default;

        GameObject targetObject = explicitTarget != null
            ? explicitTarget
            : target != null ? target.gameObject : null;

        if (!CanShoot(targetObject))
            return false;

        context = new StrangeCandlestickAttackRunner.AttackContext(
            targetObject,
            Mathf.Max(0f, lockOnDuration));
        return true;
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

        Vector2 shotDirection = GetLaunchDirection(explicitTarget.transform);

        CombatHitPayload payload = MakeHitPayload();
        if (payload == null)
            return false;

        GameObject lightBeadObject = Instantiate(lightBeadPrefab, transform.position, Quaternion.identity);
        LightBeadProjectile2D lightBead = lightBeadObject.GetComponent<LightBeadProjectile2D>();

        if (lightBead == null)
        {
            Debug.LogError(
                $"[{nameof(StrangeCandlestick)}] {lightBeadPrefab.name}에 {nameof(LightBeadProjectile2D)}가 없습니다.",
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
            speed = Mathf.Max(0f, projectileSpeed)
        };

        lightBead.Setup(context);
        nextProjectileFireTime = Time.time + GetPostShotCooldown();

        if (animator != null)
            animator.SetTrigger("attack");

        return true;
    }

    private void HideLockOnTelegraph()
    {
        if (telegraphService != null)
            telegraphService.HideCurrent();
    }

    public AttackTelegraphSpec MakeLockOnSpec(GameObject explicitTarget)
    {
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
            new Vector2(length, Mathf.Max(0.01f, lockOnLineWidth)),
            angle,
            Mathf.Max(0.01f, lockOnDuration),
            GetLockOnStyle());
    }

    private AttackTelegraphStyle GetLockOnStyle()
    {
        if (lockOnStyleAsset != null)
            return lockOnStyleAsset;

        if (runtimeLockOnStyle == null)
            runtimeLockOnStyle = MakeLockOnStyle();

        return runtimeLockOnStyle;
    }

    private AttackTelegraphStyle MakeLockOnStyle()
    {
        AttackTelegraphStyle style = ScriptableObject.CreateInstance<AttackTelegraphStyle>();
        Color transparent = new Color(lockOnColor.r, lockOnColor.g, lockOnColor.b, 0f);

        style.fillColorStart = transparent;
        style.fillColorEnd = lockOnColor;
        style.borderColorStart = transparent;
        style.borderColorEnd = lockOnColor;
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
        return Mathf.Max(0f, ProjectileAttackInterval - Mathf.Max(0f, lockOnDuration));
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
        CombatDamageSnapshot snapshot = new CombatDamageSnapshot(
            finalHpDamage: projectileDamage,
            finalStaggerBuildUp: 0f,
            finalKnockbackImpulse: 0f,
            elementBuildUps: null,
            isCriticalHit: false);

        return CombatHitPayload.FromSnapshot(
            sourceSystem: abilitySystem,
            sourceSpec: null,
            damageEffect: projectileDamageEffect,
            knockbackEffect: null,
            snapshot: snapshot,
            hitConfirmedTag: null,
            causer: gameObject);
    }

    private void EnsureAttackAbility()
    {
        if (abilitySystem == null)
            return;

        if (attackAbilityDefinition != null)
        {
            if (abilitySystem.FindSpec(attackAbilityDefinition) == null)
                abilitySystem.GiveAbility(attackAbilityDefinition);

            return;
        }

        runtimeAttackLogic = ScriptableObject.CreateInstance<AbilityLogic_StrangeCandlestickAttack>();
        attackAbilityDefinition = ScriptableObject.CreateInstance<AbilityDefinition>();
        attackAbilityDefinition.name = "AD_StrangeCandlestick_Attack_Runtime";
        attackAbilityDefinition.abilityName = "AD_StrangeCandlestick_Attack_Runtime";
        attackAbilityDefinition.castTime = 0f;
        attackAbilityDefinition.recoveryTime = 0f;
        attackAbilityDefinition.animationChannel = AbilityDefinition.AnimationChannel.Player;
        attackAbilityDefinition.executionPolicy = AbilityDefinition.ExecutionPolicy.ExclusiveQueued;
        attackAbilityDefinition.logic = runtimeAttackLogic;
        abilitySystem.GiveAbility(attackAbilityDefinition);
        ownsRuntimeAbilityDefinition = true;
    }
}
