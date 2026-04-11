using UnityEngine;
using UnityGAS;

public class StrangeCandlestick : Mob
{
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

    private EnemyChaseIntent2D detectionSensor;
    private CandlestickSeal candlestickSeal;
    private float nextProjectileFireTime;
    private bool hasLoggedInvalidConfig;

    protected override void Awake()
    {
        base.Awake();
        detectionSensor = GetComponent<EnemyChaseIntent2D>();
        candlestickSeal = GetComponent<CandlestickSeal>();
    }

    public override bool CanUseChaseMovement()
    {
        return false;
    }

    protected override void UpdateAttack()
    {
        if (!CanShoot())
            return;

        Shoot();
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

    /// <summary>지금 탄막을 발사할 수 있는지 확인합니다.</summary>
    private bool CanShoot()
    {
        if (isDead)
            return false;

        if (Time.time < nextProjectileFireTime)
            return false;

        if (!HasShootData())
            return false;

        if (target == null)
            return false;

        return IsTargetInRange();
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
    private bool IsTargetInRange()
    {
        if (target == null || detectionSensor == null)
            return false;

        float detectionRange = Mathf.Max(0f, detectionSensor.DetectionRange);
        if (detectionRange <= 0f)
            return false;

        Vector2 toTarget = (Vector2)(target.position - transform.position);
        return toTarget.sqrMagnitude <= detectionRange * detectionRange;
    }

    /// <summary>LightBead를 생성하고 발사 설정을 넘깁니다.</summary>
    private bool Shoot()
    {
        Vector2 shotDirection = GetLaunchDirection();

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
            damageLayers = GetDamageMask(),
            hitPayload = payload,
            direction = shotDirection,
            speed = Mathf.Max(0f, projectileSpeed)
        };

        lightBead.Setup(context);
        nextProjectileFireTime = Time.time + ProjectileAttackInterval;
        return true;
    }

    /// <summary>발사 방향을 계산합니다.</summary>
    private Vector2 GetLaunchDirection()
    {
        if (target == null)
            return sprite != null && sprite.flipX ? Vector2.left : Vector2.right;

        Vector2 toTarget = (Vector2)(target.position - transform.position);

        if (toTarget.sqrMagnitude <= 0.0001f)
            return sprite != null && sprite.flipX ? Vector2.left : Vector2.right;

        return toTarget.normalized;
    }

    /// <summary>플레이어 레이어를 데미지 마스크로 만듭니다.</summary>
    private LayerMask GetDamageMask()
    {
        return target != null
            ? (LayerMask)(1 << target.gameObject.layer)
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
}
