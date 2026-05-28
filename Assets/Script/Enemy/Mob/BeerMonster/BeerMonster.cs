using System.Collections;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 이 클래스의 책임:
/// 맥주 몬스터 프리팹의 공통 Mob FSM 본체 역할을 담당하고,
/// 사망 시 술 장판을 생성하는 맥주 몬스터 고유 기믹을 제공한다.
/// 원거리 탄막 공격 판단과 탄막 생성 문맥을 소유한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MobAbilityCoordinator))]
[RequireComponent(typeof(BeerMonsterShotRunner))]
public class BeerMonster : Mob, IMobAttackDecisionSource
{
    [Header("Attack")]
    [Tooltip("맥주 몬스터가 사용하는 단발 탄막 AbilityDefinition입니다.")]
    [SerializeField] private AbilityDefinition shotAbility;

    [Header("Death Puddle")]
    [Tooltip("맥주 몬스터가 사망할 때 생성할 술 장판 프리팹입니다.")]
    [SerializeField] private AlcoholPuddleArea alcoholPuddlePrefab;

    [Tooltip("사망 위치 기준 술 장판 생성 위치 보정값입니다.")]
    [SerializeField] private Vector2 puddleSpawnOffset;

    [Tooltip("꺼두면 사망해도 술 장판을 생성하지 않습니다. 디버그/특수 연출용 스위치입니다.")]
    [SerializeField] private bool spawnPuddleOnDeath = true;

    private BeerMonsterShotRunner runner;
    private bool hasLoggedInvalidAttackConfig;
    private bool hasSpawnedDeathPuddle;
    private AbilityLogic_BeerMonsterShot Logic => shotAbility != null ? shotAbility.logic as AbilityLogic_BeerMonsterShot : null;
    public AbilityLogic_BeerMonsterShot ShotLogic => Logic;

    public readonly struct ShotContext
    {
        public readonly GameObject Target;
        public readonly Vector2 Origin;
        public readonly Vector2 Direction;
        public readonly float WarningSeconds;
        public readonly float WarningWidth;
        public readonly float TelegraphRange;
        public readonly float ProjectileSpeed;
        public readonly float ProjectileLifetime;
        public readonly LayerMask WallLayers;
        public readonly LayerMask TargetLayers;
        public readonly CombatHitPayload HitPayload;

        public ShotContext(
            GameObject target,
            Vector2 origin,
            Vector2 direction,
            float warningSeconds,
            float warningWidth,
            float telegraphRange,
            float projectileSpeed,
            float projectileLifetime,
            LayerMask wallLayers,
            LayerMask targetLayers,
            CombatHitPayload hitPayload)
        {
            Target = target;
            Origin = origin;
            Direction = direction;
            WarningSeconds = warningSeconds;
            WarningWidth = warningWidth;
            TelegraphRange = telegraphRange;
            ProjectileSpeed = projectileSpeed;
            ProjectileLifetime = projectileLifetime;
            WallLayers = wallLayers;
            TargetLayers = targetLayers;
            HitPayload = hitPayload;
        }
    }

    protected override void Awake()
    {
        base.Awake();
        runner = GetComponent<BeerMonsterShotRunner>();
    }

    protected override void Start()
    {
        base.Start();
        if (abilitySystem != null && shotAbility != null)
            abilitySystem.GiveAbility(shotAbility);
    }

    public override bool CanUseChaseMovement()
    {
        return base.CanUseChaseMovement() && (runner == null || !runner.IsRunning);
    }

    public bool TryBuildAttackRequest(out MobAttackRequest request)
    {
        request = default;
        GameObject targetObject = Target != null ? Target.gameObject : null;
        AbilityLogic_BeerMonsterShot logic = Logic;
        if (!HasRequiredAttackData() || logic == null || !CommonMonsterCombatUtility.InRange(transform, targetObject, logic.AttackRange))
            return false;

        request = new MobAttackRequest(shotAbility, targetObject, logic.RecoverSeconds);
        return request.IsValid;
    }

    /// <summary>공격 상태 진입 시 탄막 조준 준비 애니메이션을 요청한다.</summary>
    public void OnAttackStateEntered(MobAttackRequest request)
    {
        CommonMonsterCombatUtility.TriggerAnimation(this, CommonMonsterAnimationCue.AttackReady);
    }

    /// <summary>공격 상태 종료 시 취소가 아니라면 탄막 회복 애니메이션을 요청한다.</summary>
    public void OnAttackStateExited(MobAttackRequest request, bool wasCancelled)
    {
        if (!wasCancelled && !IsDead)
            CommonMonsterCombatUtility.TriggerAnimation(this, CommonMonsterAnimationCue.Recover);
    }

    protected override void OnDeathStarted()
    {
        CommonMonsterCombatUtility.TriggerAnimation(this, CommonMonsterAnimationCue.Die);
        SpawnDeathPuddle();
        base.OnDeathStarted();
    }

    public bool TryBuildShotContext(AbilitySystem system, AbilitySpec spec, GameObject explicitTarget, out ShotContext context)
    {
        context = default;
        GameObject targetObject = explicitTarget != null ? explicitTarget : Target != null ? Target.gameObject : null;
        AbilityLogic_BeerMonsterShot logic = Logic;
        if (!HasRequiredAttackData() || logic == null || !CommonMonsterCombatUtility.InRange(transform, targetObject, logic.AttackRange))
            return false;

        Vector2 origin = CommonMonsterCombatUtility.ResolveAimPoint(gameObject, CombatAimPointKind.ProjectileTarget);
        Vector2 direction = CommonMonsterCombatUtility.DirectionToAimPoint(origin, targetObject, sprite != null && sprite.flipX);
        float speed = CommonMonsterCombatUtility.ResolvePlayerBaseSpeed(Target) * logic.ProjectileSpeedMultiplier;
        CombatHitPayload payload = CommonMonsterCombatUtility.BuildPayload(
            system != null ? system : abilitySystem,
            spec,
            logic.DamageEffect,
            null,
            gameObject,
            logic.DamageAmount,
            0f);

        context = new ShotContext(
            targetObject,
            origin,
            direction,
            logic.WarningSeconds,
            logic.WarningWidth,
            logic.AttackRange,
            speed,
            logic.ProjectileLifetime,
            logic.WallLayers,
            logic.TargetLayers,
            payload);
        return true;
    }

    public void FireProjectile(ShotContext context)
    {
        AbilityLogic_BeerMonsterShot logic = Logic;
        if (logic == null || logic.ProjectilePrefab == null)
            return;

        Quaternion rotation = logic.AlignProjectileRotationToDirection
            ? Quaternion.Euler(0f, 0f, Mathf.Atan2(context.Direction.y, context.Direction.x) * Mathf.Rad2Deg + logic.ProjectileRotationOffsetDegrees)
            : logic.ProjectilePrefab.transform.rotation;

        GameObject projectileObject = Instantiate(logic.ProjectilePrefab, context.Origin, rotation);
        if (projectileObject == null)
            return;

        LightBeadProjectile2D projectile = projectileObject.GetComponent<LightBeadProjectile2D>();
        if (projectile == null)
        {
            Debug.LogError($"[{nameof(BeerMonster)}] AL projectilePrefab에 {nameof(LightBeadProjectile2D)}가 없습니다.", projectileObject);
            Destroy(projectileObject);
            return;
        }

        projectile.Setup(new ProjectileAttackSpawnContext
        {
            ownerSystem = abilitySystem,
            sourceSpec = null,
            causer = gameObject,
            ignoreTarget = gameObject,
            lifetime = context.ProjectileLifetime,
            wallLayers = context.WallLayers,
            damageLayers = context.TargetLayers,
            hitPayload = context.HitPayload,
            direction = context.Direction,
            speed = context.ProjectileSpeed
        });
    }

    /// <summary>
    /// 책임:
    /// 사망 처리가 여러 경로에서 진입하더라도 술 장판 생성은 한 번만 수행한다.
    /// </summary>
    private void SpawnDeathPuddle()
    {
        if (!spawnPuddleOnDeath || hasSpawnedDeathPuddle)
            return;

        hasSpawnedDeathPuddle = true;

        if (alcoholPuddlePrefab == null)
        {
            Debug.LogWarning($"{nameof(BeerMonster)}: 사망 시 생성할 술 장판 프리팹이 비어 있습니다.", this);
            return;
        }

        Vector3 spawnPosition = transform.position + new Vector3(puddleSpawnOffset.x, puddleSpawnOffset.y, 0f);
        Instantiate(alcoholPuddlePrefab, spawnPosition, Quaternion.identity);
    }

    private bool HasRequiredAttackData()
    {
        AbilityLogic_BeerMonsterShot logic = Logic;
        bool valid = abilitySystem != null &&
                     shotAbility != null &&
                     logic != null &&
                     logic.ProjectilePrefab != null &&
                     logic.DamageEffect != null &&
                     runner != null;
        if (valid)
            return true;

        if (!hasLoggedInvalidAttackConfig)
        {
            Debug.LogError($"[{nameof(BeerMonster)}] 탄막 공격 설정이 비어 있습니다.", this);
            hasLoggedInvalidAttackConfig = true;
        }

        return false;
    }
}

/// <summary>
/// 책임:
/// - 맥주 몬스터의 추적 실선 경고를 표시하고 발사 직전 방향으로 한 발을 발사한다.
/// - GoblinGunner와 같은 탄막 메커니즘을 BeerMonster 전용 문맥으로 실행한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BeerMonster))]
public sealed partial class BeerMonsterShotRunner : MonoBehaviour, IMobPatternRunner, IMobPresentationCleanup
{
    [SerializeField] private BeerMonster owner;
    [SerializeField] private MobAbilityCoordinator abilityCoordinator;
    [SerializeField] private AttackTelegraphService telegraphService;

    private AttackTelegraphStyle warningStyle;
    private bool isRunning;
    private bool cancelRequested;
    private float nextWallClipProbeLogTime;

    public bool IsRunning => isRunning;

    private void Awake()
    {
        if (owner == null)
            owner = GetComponent<BeerMonster>();
        if (abilityCoordinator == null)
            abilityCoordinator = GetComponent<MobAbilityCoordinator>();
        if (telegraphService == null)
            telegraphService = GetComponent<AttackTelegraphService>();
        warningStyle = CreateWarningStyle();
    }

    private void OnDestroy()
    {
        if (warningStyle != null)
            Destroy(warningStyle);
    }

    private void OnDisable()
    {
        Cancel();
    }

    public IEnumerator Run(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        if (owner == null) yield break;
        if (!owner.TryBuildShotContext(system, spec, initialTarget, out BeerMonster.ShotContext context)) yield break;
        if (abilityCoordinator != null && !abilityCoordinator.TryBeginRunner(this)) yield break;

        isRunning = true;
        cancelRequested = false;

        try
        {
            float warningSeconds = CombatTimingService.ScaleSeconds(system, context.WarningSeconds, CombatTimingSlot.AttackWarning);
            ShowWarning(context, warningSeconds);
            if (warningSeconds > 0f)
                yield return TrackWarningUntilFire(system, spec, initialTarget, context, warningSeconds);

            if (cancelRequested || owner.IsDead || IsCancelled(spec))
                yield break;

            if (owner.TryBuildShotContext(system, spec, initialTarget, out BeerMonster.ShotContext finalContext))
            {
                context = finalContext;
                UpdateWarning(context, warningSeconds);
            }

            HideWarning();
            CommonMonsterCombatUtility.TriggerAnimation(owner, CommonMonsterAnimationCue.Attack);
            owner.FireProjectile(context);
        }
        finally
        {
            HideWarning();
            cancelRequested = false;
            isRunning = false;
            abilityCoordinator?.EndRunner(this);
        }
    }

    private IEnumerator TrackWarningUntilFire(
        AbilitySystem system,
        AbilitySpec spec,
        GameObject initialTarget,
        BeerMonster.ShotContext context,
        float warningSeconds)
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0f, warningSeconds);

        while (elapsed < duration)
        {
            if (cancelRequested || owner.IsDead || IsCancelled(spec))
                yield break;

            if (owner.TryBuildShotContext(system, spec, initialTarget, out BeerMonster.ShotContext trackedContext))
            {
                context = trackedContext;
                UpdateWarning(context, warningSeconds);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    public void Cancel()
    {
        cancelRequested = true;
        HideWarning();
    }

    public void CleanupPresentation()
    {
        HideWarning();
    }

    private void ShowWarning(BeerMonster.ShotContext context, float warningSeconds)
    {
        if (telegraphService == null)
            return;

        telegraphService.Show(CreateWarningSpec(context, warningSeconds));
    }

    private void UpdateWarning(BeerMonster.ShotContext context, float warningSeconds)
    {
        if (telegraphService == null)
            return;

        telegraphService.UpdateCurrentGeometry(CreateWarningSpec(context, warningSeconds));
    }

    private AttackTelegraphSpec CreateWarningSpec(BeerMonster.ShotContext context, float warningSeconds)
    {
        LogWallClipProbe(context);

        Vector2 start = CommonMonsterCombatUtility.ResolveAimPoint(owner.gameObject, CombatAimPointKind.ProjectileTarget);
        Vector2 end = context.Target != null
            ? CommonMonsterCombatUtility.ResolveAimPoint(context.Target, CombatAimPointKind.ProjectileTarget)
            : start + context.Direction.normalized * context.TelegraphRange;

        AttackTelegraphSpec spec = AttackTelegraphSpec.CreateLine(
            start,
            end,
            context.WarningWidth,
            warningSeconds,
            warningStyle);
        return spec.WithWallClipping(context.WallLayers, 48, 0.03f);
    }

    private void LogWallClipProbe(BeerMonster.ShotContext context)
    {
        AbilityLogic_BeerMonsterShot logic = ResolveLogic();
        if (logic == null || !logic.LogWallClipProbe || Time.time < nextWallClipProbeLogTime)
            return;

        nextWallClipProbeLogTime = Time.time + logic.WallClipProbeLogInterval;

        Vector2 start = CommonMonsterCombatUtility.ResolveAimPoint(owner.gameObject, CombatAimPointKind.ProjectileTarget);
        Vector2 end = context.Target != null
            ? CommonMonsterCombatUtility.ResolveAimPoint(context.Target, CombatAimPointKind.ProjectileTarget)
            : start + context.Direction.normalized * context.TelegraphRange;
        Vector2 delta = end - start;
        Vector2 direction = delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector2.right;
        float range = Mathf.Max(0.01f, delta.magnitude);
        RaycastHit2D hit = Physics2D.Raycast(start, direction, range, context.WallLayers);
        if (hit.collider == null)
        {
            Debug.Log(
                $"[BeerMonsterWallClipProbe] no wall hit. origin={start}, dir={direction}, range={range:0.00}, wallMask={context.WallLayers.value}",
                this);
            return;
        }

        Debug.Log(
            $"[BeerMonsterWallClipProbe] wall hit. collider={hit.collider.name}, layer={LayerMask.LayerToName(hit.collider.gameObject.layer)}({hit.collider.gameObject.layer}), trigger={hit.collider.isTrigger}, distance={hit.distance:0.00}, point={hit.point}, wallMask={context.WallLayers.value}",
            hit.collider);
    }

    private void HideWarning()
    {
        telegraphService?.HideCurrent();
    }

    /// <summary>Runner가 직접 데이터를 소유하지 않고 현재 AD에 연결된 맥주 몬스터 사격 AL 데이터를 조회한다.</summary>
    private AbilityLogic_BeerMonsterShot ResolveLogic()
    {
        return owner != null ? owner.ShotLogic : null;
    }

    private static bool IsCancelled(AbilitySpec spec)
    {
        return spec != null && spec.Token != null && spec.Token.IsCancelled;
    }

    private static AttackTelegraphStyle CreateWarningStyle()
    {
        AttackTelegraphStyle style = ScriptableObject.CreateInstance<AttackTelegraphStyle>();
        AttackTelegraphStyleUtility.ApplyDangerLineColors(style);
        style.progressCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        style.blinkStartNormalized = 0.72f;
        style.blinkFrequency = 5f;
        style.blinkAlphaMin = 0.45f;
        style.scaleFillWithProgress = false;
        style.fillScaleStart = 1f;
        style.fillScaleEnd = 1f;
        return style;
    }
}
