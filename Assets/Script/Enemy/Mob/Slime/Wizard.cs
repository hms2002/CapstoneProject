using UnityEngine;
using UnityGAS;

[DisallowMultipleComponent]
[RequireComponent(typeof(MobAbilityCoordinator))]
[RequireComponent(typeof(WizardScatterShotRunner))]
public class Wizard : Slime
{
    private const int WallLayer = 30;
    private const float AttackRange = 6.5f;
    private const float MaxHealth = 6f;
    private const float VisualScale = 0.85f;
    private const float ChaseSpeedMultiplier = 1f;
    private const float DamageAmount = 0.8f;
    private const float ProjectileSpeed = 5.5f;
    private const float ScatterAngle = 24f;
    private const float SplitSpread = 0.55f;
    private const float AttackRecoverSeconds = 0.25f;
    private const int ShotCount = 4;

    [SerializeField] private GameObject splitPrefab;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private AbilityDefinition attackAbility;
    [SerializeField] private GE_Damage_Spec damageEffect;
    [SerializeField, Min(0)] private int splitCount = 4;

    private WizardScatterShotRunner scatterShotRunner;
    private bool hasLoggedInvalidConfig;

    public readonly struct ScatterShotContext
    {
        public readonly GameObject Target;
        public readonly Vector2 Origin;
        public readonly Vector2 Direction;
        public readonly LayerMask DamageLayers;
        public readonly CombatHitPayload HitPayload;

        public ScatterShotContext(
            GameObject target,
            Vector2 origin,
            Vector2 direction,
            LayerMask damageLayers,
            CombatHitPayload hitPayload)
        {
            Target = target;
            Origin = origin;
            Direction = direction;
            DamageLayers = damageLayers;
            HitPayload = hitPayload;
        }
    }

    protected override void Awake()
    {
        base.Awake();

        CacheCoordinator();

        scatterShotRunner = GetComponent<WizardScatterShotRunner>();
        if (scatterShotRunner == null)
            scatterShotRunner = gameObject.AddComponent<WizardScatterShotRunner>();

        ApplyStats();
    }

    protected override void Start()
    {
        base.Start();
        GiveAbility(attackAbility);
    }

    public override bool CanUseChaseMovement()
    {
        UpdateSpeed(ChaseSpeedMultiplier);

        if (!CanMove()) return false;

        return scatterShotRunner == null || !scatterShotRunner.IsRunning;
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

    /// <summary>마법사의 산탄 공격 요청을 만듭니다.</summary>
    public override bool TryBuildAttackRequest(out MobAttackRequest request)
    {
        request = default;

        if (!CanAct()) return false;
        if (!HasShotData()) return false;

        GameObject targetObject = target != null ? target.gameObject : null;
        if (!InRange(targetObject, AttackRange)) return false;

        request = new MobAttackRequest(attackAbility, targetObject, AttackRecoverSeconds);
        return request.IsValid;
    }

    /// <summary>산탄 발사에 필요한 실행 정보를 만듭니다.</summary>
    public bool TryBuildShotContext(AbilitySystem system, AbilitySpec spec, GameObject explicitTarget, out ScatterShotContext context)
    {
        context = default;

        if (!CanAct()) return false;
        if (!HasShotData()) return false;

        GameObject targetObject = GetTarget(explicitTarget);
        if (!InRange(targetObject, AttackRange)) return false;

        context = new ScatterShotContext(
            targetObject,
            transform.position,
            GetDirection(targetObject),
            1 << targetObject.layer,
            MakePayload(system, spec, damageEffect, null, DamageAmount, 0f));
        return true;
    }

    /// <summary>LightBead를 산탄 형태로 발사합니다.</summary>
    public void FireScatterShot(ScatterShotContext context)
    {
        if (context.HitPayload == null || !context.HitPayload.IsValid()) return;

        for (int i = 0; i < ShotCount; i++)
        {
            Vector2 direction = GetShotDirection(context.Direction);
            GameObject projectileObject = Instantiate(projectilePrefab, context.Origin, Quaternion.identity);
            if (projectileObject == null) continue;

            LightBeadProjectile2D projectile = projectileObject.GetComponent<LightBeadProjectile2D>();
            if (projectile == null)
            {
                Debug.LogError($"[{nameof(Wizard)}] {projectilePrefab.name}에 {nameof(LightBeadProjectile2D)}가 없습니다.", projectileObject);
                Destroy(projectileObject);
                continue;
            }

            ProjectileAttackSpawnContext spawnContext = new()
            {
                ownerSystem = abilitySystem,
                sourceSpec = null,
                causer = gameObject,
                ignoreTarget = gameObject,
                lifetime = float.MaxValue,
                wallLayers = 1 << WallLayer,
                damageLayers = context.DamageLayers,
                hitPayload = context.HitPayload,
                direction = direction,
                speed = ProjectileSpeed
            };

            projectile.Setup(spawnContext);
        }

        if (animator != null)
            animator.SetTrigger("attack");
    }

    /// <summary>마법사의 기본 스탯과 크기를 적용합니다.</summary>
    protected override void ApplyStats()
    {
        SetStats("Wizard", MaxHealth, VisualScale);
    }

    /// <summary>산탄 범위 안에서 무작위 탄막 방향을 계산합니다.</summary>
    private static Vector2 GetShotDirection(Vector2 baseDirection)
    {
        float angle = Random.Range(-ScatterAngle * 0.5f, ScatterAngle * 0.5f);
        float rad = angle * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);
        Vector2 dir = baseDirection.normalized;
        return new Vector2(dir.x * cos - dir.y * sin, dir.x * sin + dir.y * cos).normalized;
    }

    /// <summary>산탄 발사 설정이 모두 연결되어 있는지 확인합니다.</summary>
    private bool HasShotData()
    {
        bool isValid = projectilePrefab != null &&
                       attackAbility != null &&
                       damageEffect != null &&
                       abilitySystem != null &&
                       scatterShotRunner != null;

        if (isValid) return true;

        if (!hasLoggedInvalidConfig)
        {
            Debug.LogError($"[{nameof(Wizard)}] 산탄 발사 설정이 비어 있습니다.", this);
            hasLoggedInvalidConfig = true;
        }

        return false;
    }
}
