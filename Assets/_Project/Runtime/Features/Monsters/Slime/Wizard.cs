using UnityEngine;
using UnityGAS;
using CapstoneAudio;

/// <summary>
/// 책임:
/// - Slime 계열 Wizard의 산탄 공격 판단, 투사체 생성 문맥, 사망 시 분열 규칙을 소유한다.
/// - 공격 준비/실행 흐름은 WizardScatterShotRunner에 위임하고 실제 발사만 담당한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MobAbilityCoordinator))]
[RequireComponent(typeof(WizardScatterShotRunner))]
public class Wizard : Slime
{
    private static readonly SoundRef ShotFireSound = SoundRef.FromKey("sound_wizard_shotFire");

    private const string AttackPrepareTriggerName = "attackPrepare";
    private const string AttackTriggerName = "attack";
    private const string DieTriggerName = "die";
    private const int WallLayer = 30;
    private const float AttackRange = 6.5f;
    private const float AttackPrepareSeconds = 0.35f;
    private const float MaxHealth = 6f;
    private const float VisualScale = 0.85f;
    private const float ChaseSpeedMultiplier = 1f;
    private const float ProjectileSpeed = 5.5f;
    private const float ScatterAngle = 24f;
    private const float SplitSpread = 0.55f;
    private const float AttackRecoverSeconds = 0.25f;
    private const int ShotCount = 4;

    [SerializeField] private GameObject splitPrefab;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private AbilityDefinition attackAbility;
    [SerializeField] private GE_Damage_Spec damageEffect;
    [SerializeField] private float shotDamageAmount = 1f;
    [SerializeField, Min(0)] private int splitCount = 4;

    private WizardScatterShotRunner scatterShotRunner;
    private bool hasAttackPrepareTrigger;
    private bool hasAttackTrigger;
    private bool hasDieTrigger;
    private bool hasLoggedInvalidConfig;

    public readonly struct ScatterShotContext
    {
        public readonly GameObject Target;
        public readonly Vector2 Origin;
        public readonly Vector2 Direction;
        public readonly LayerMask DamageLayers;
        public readonly float PrepareSeconds;
        public readonly float TelegraphRange;
        public readonly float TelegraphAngle;
        public readonly CombatHitPayload HitPayload;

        public ScatterShotContext(
            GameObject target,
            Vector2 origin,
            Vector2 direction,
            LayerMask damageLayers,
            float prepareSeconds,
            float telegraphRange,
            float telegraphAngle,
            CombatHitPayload hitPayload)
        {
            Target = target;
            Origin = origin;
            Direction = direction;
            DamageLayers = damageLayers;
            PrepareSeconds = prepareSeconds;
            TelegraphRange = telegraphRange;
            TelegraphAngle = telegraphAngle;
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

        CacheAnimatorParameters();
        ApplyStats();
    }

    protected override void Start()
    {
        base.Start();
        GiveAbility(attackAbility);
    }

    public override bool CanUseChaseMovement()
    {
        if (!base.CanUseChaseMovement()) return false;
        UpdateSpeed(ChaseSpeedMultiplier);

        if (!CanMove()) return false;

        return scatterShotRunner == null || !scatterShotRunner.IsRunning;
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
    /// - Wizard 산탄 공격 준비 시작을 Animator trigger로 전달한다.
    /// - 실제 투사체 생성 전 준비 동작 표현만 담당한다.
    /// </summary>
    public void PlayAttackPrepareAnimation()
    {
        SetAnimatorTriggerIfAvailable(AttackPrepareTriggerName, hasAttackPrepareTrigger);
    }

    /// <summary>
    /// 책임:
    /// - Wizard 산탄 투사체 발사 타이밍을 Animator trigger로 전달한다.
    /// - 발사 판정과 시전 애니메이션 시작점을 맞춘다.
    /// </summary>
    public void PlayAttackAnimation()
    {
        SetAnimatorTriggerIfAvailable(AttackTriggerName, hasAttackTrigger);
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
            AttackPrepareSeconds,
            AttackRange,
            ScatterAngle,
            MakePayload(system, spec, damageEffect, null, shotDamageAmount, 0f));
        return true;
    }

    /// <summary>LightBead를 산탄 형태로 발사합니다.</summary>
    public void FireScatterShot(ScatterShotContext context)
    {
        if (context.HitPayload == null || !context.HitPayload.IsValid()) return;

        PlayShotFireSound(context.Origin);
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
    }

    /// <summary>Wizard 산탄 탄막 묶음 발사 타이밍에 사운드를 한 번 재생합니다.</summary>
    private void PlayShotFireSound(Vector2 origin)
    {
        SoundPlaybackUtility.Play(
            ShotFireSound,
            instigator: gameObject,
            causer: gameObject,
            target: target != null ? target.gameObject : null,
            position: origin,
            sourceObject: this);
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

    /// <summary>Animator Controller에 Wizard 전용 트리거가 있는지 캐시합니다.</summary>
    private void CacheAnimatorParameters()
    {
        hasAttackPrepareTrigger = HasAnimatorParameter(AttackPrepareTriggerName, AnimatorControllerParameterType.Trigger);
        hasAttackTrigger = HasAnimatorParameter(AttackTriggerName, AnimatorControllerParameterType.Trigger);
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
