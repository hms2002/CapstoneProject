using System.Collections;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// - 리자드맨 마법사의 연속 탄막 공격 판단과 발사 문맥을 소유한다.
/// - 경고 후 3회 발사 시퀀스는 LizardMageBurstRunner에 위임한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MobAbilityCoordinator))]
[RequireComponent(typeof(LizardMageBurstRunner))]
public sealed class LizardMage : Mob, IMobAttackDecisionSource
{
    [SerializeField] private AbilityDefinition burstAbility;
    [SerializeField, Min(0f)] private float maxHealth = 7f;

    private LizardMageBurstRunner runner;
    private bool hasLoggedInvalidConfig;
    private AbilityLogic_LizardMageBurst Logic => burstAbility != null ? burstAbility.logic as AbilityLogic_LizardMageBurst : null;

    public AbilityLogic_LizardMageBurst BurstLogic => Logic;
    public int ShotCount => Logic != null ? Logic.ShotCount : 0;
    public float ShotInterval => Logic != null ? Logic.ShotInterval : 0f;

    public readonly struct BurstContext
    {
        public readonly GameObject Target;
        public readonly Vector2 Origin;
        public readonly Vector2 WarningDirection;
        public readonly float WarningSeconds;
        public readonly float WarningWidth;
        public readonly float TelegraphRange;
        public readonly float ProjectileSpeed;
        public readonly float ProjectileLifetime;
        public readonly LayerMask WallLayers;
        public readonly LayerMask TargetLayers;
        public readonly CombatHitPayload HitPayload;

        public BurstContext(
            GameObject target,
            Vector2 origin,
            Vector2 warningDirection,
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
            WarningDirection = warningDirection;
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
        runner = GetComponent<LizardMageBurstRunner>();
        ApplyStats();
    }

    protected override void Start()
    {
        base.Start();
        if (abilitySystem != null && burstAbility != null)
            abilitySystem.GiveAbility(burstAbility);
    }

    public override bool CanUseChaseMovement()
    {
        return base.CanUseChaseMovement() && (runner == null || !runner.IsRunning);
    }

    public bool TryBuildAttackRequest(out MobAttackRequest request)
    {
        request = default;
        GameObject targetObject = Target != null ? Target.gameObject : null;
        AbilityLogic_LizardMageBurst logic = Logic;
        if (!HasRequiredData() || logic == null || !CommonMonsterCombatUtility.InRange(transform, targetObject, logic.AttackRange))
            return false;

        request = new MobAttackRequest(burstAbility, targetObject, logic.RecoverSeconds);
        return request.IsValid;
    }

    /// <summary>공격 상태 진입 시 마법 준비 애니메이션을 요청한다.</summary>
    public void OnAttackStateEntered(MobAttackRequest request)
    {
        CommonMonsterCombatUtility.TriggerAnimation(this, CommonMonsterAnimationCue.AttackReady);
    }

    /// <summary>공격 상태 종료 시 취소가 아니라면 회복 애니메이션을 요청한다.</summary>
    public void OnAttackStateExited(MobAttackRequest request, bool wasCancelled)
    {
        if (!wasCancelled && !IsDead)
            CommonMonsterCombatUtility.TriggerAnimation(this, CommonMonsterAnimationCue.Recover);
    }

    protected override void OnDeathStarted()
    {
        CommonMonsterCombatUtility.TriggerAnimation(this, CommonMonsterAnimationCue.Die);
        base.OnDeathStarted();
    }

    public bool TryBuildBurstContext(AbilitySystem system, AbilitySpec spec, GameObject explicitTarget, out BurstContext context)
    {
        context = default;
        GameObject targetObject = explicitTarget != null ? explicitTarget : Target != null ? Target.gameObject : null;
        AbilityLogic_LizardMageBurst logic = Logic;
        if (!HasRequiredData() || logic == null || !CommonMonsterCombatUtility.InRange(transform, targetObject, logic.AttackRange))
            return false;

        Vector2 origin = transform.position;
        Vector2 direction = CommonMonsterCombatUtility.DirectionToAimPoint(origin, targetObject, sprite != null && sprite.flipX);
        float speed = CommonMonsterCombatUtility.ResolvePlayerBaseSpeed(target) * logic.ProjectileSpeedMultiplier;
        CombatHitPayload payload = CommonMonsterCombatUtility.BuildPayload(
            system != null ? system : abilitySystem,
            spec,
            logic.DamageEffect,
            null,
            gameObject,
            logic.DamageAmount,
            0f);

        context = new BurstContext(
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

    public void FireProjectile(BurstContext context)
    {
        Vector2 direction = context.Target != null
            ? CommonMonsterCombatUtility.DirectionToAimPoint(context.Origin, context.Target, sprite != null && sprite.flipX)
            : context.WarningDirection;

        AbilityLogic_LizardMageBurst logic = Logic;
        if (logic == null || logic.ProjectilePrefab == null)
            return;

        GameObject projectileObject = Instantiate(logic.ProjectilePrefab, transform.position, Quaternion.identity);
        if (projectileObject == null)
            return;

        LightBeadProjectile2D projectile = projectileObject.GetComponent<LightBeadProjectile2D>();
        if (projectile == null)
        {
            Debug.LogError($"[{nameof(LizardMage)}] AL projectilePrefab에 {nameof(LightBeadProjectile2D)}가 없습니다.", projectileObject);
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
            direction = direction,
            speed = context.ProjectileSpeed
        });
    }

    private void ApplyStats()
    {
        if (attributeSet == null)
            return;

        attributeSet.TrySetBaseValue(maxHealthDef, maxHealth, this);
        attributeSet.TrySetBaseValue(healthDef, maxHealth, this);
    }

    private bool HasRequiredData()
    {
        AbilityLogic_LizardMageBurst logic = Logic;
        bool valid = abilitySystem != null && burstAbility != null && logic != null && logic.ProjectilePrefab != null && logic.DamageEffect != null && runner != null;
        if (valid)
            return true;

        if (!hasLoggedInvalidConfig)
        {
            Debug.LogError($"[{nameof(LizardMage)}] 연속 탄막 공격 설정이 비어 있습니다.", this);
            hasLoggedInvalidConfig = true;
        }

        return false;
    }
}

/// <summary>
/// 책임:
/// - 리자드맨 마법사의 고정 경고선을 표시한 뒤, 발사 시점마다 플레이어 방향을 다시 계산해 3발을 발사한다.
/// - 경고와 투사체 생성 생명주기를 몬스터 본체에서 분리한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(LizardMage))]
public sealed partial class LizardMageBurstRunner : MonoBehaviour, IMobPatternRunner, IMobPresentationCleanup
{
    [SerializeField] private LizardMage owner;
    [SerializeField] private MobAbilityCoordinator abilityCoordinator;
    [SerializeField] private AttackTelegraphService telegraphService;

    private AttackTelegraphStyle warningStyle;
    private bool isRunning;
    private bool cancelRequested;

    public bool IsRunning => isRunning;

    private void Awake()
    {
        if (owner == null)
            owner = GetComponent<LizardMage>();
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
        if (!owner.TryBuildBurstContext(system, spec, initialTarget, out LizardMage.BurstContext context)) yield break;
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

            if (owner.TryBuildBurstContext(system, spec, initialTarget, out LizardMage.BurstContext finalContext))
            {
                context = finalContext;
                UpdateWarning(context, warningSeconds);
            }

            HideWarning();
            CommonMonsterCombatUtility.TriggerAnimation(owner, CommonMonsterAnimationCue.Attack);
            for (int i = 0; i < owner.ShotCount; i++)
            {
                if (cancelRequested || owner.IsDead || IsCancelled(spec))
                    yield break;

                owner.FireProjectile(context);
                if (i < owner.ShotCount - 1 && owner.ShotInterval > 0f)
                    yield return AbilityTasks.WaitCombatDelay(system, spec, owner.ShotInterval, CombatTimingSlot.AttackInterval);
            }
        }
        finally
        {
            HideWarning();
            cancelRequested = false;
            isRunning = false;
            abilityCoordinator?.EndRunner(this);
        }
    }

    /// <summary>
    /// 책임:
    /// - 발사 직전까지 플레이어 위치를 다시 읽어 리자드맨 마법사의 경고선을 갱신한다.
    /// - 조준 중 방향 전환과 wall clipping이 실제 발사 방향과 어긋나지 않게 한다.
    /// </summary>
    private IEnumerator TrackWarningUntilFire(
        AbilitySystem system,
        AbilitySpec spec,
        GameObject initialTarget,
        LizardMage.BurstContext context,
        float warningSeconds)
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0f, warningSeconds);

        while (elapsed < duration)
        {
            if (cancelRequested || owner.IsDead || IsCancelled(spec))
                yield break;

            if (owner.TryBuildBurstContext(system, spec, initialTarget, out LizardMage.BurstContext trackedContext))
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

    private void ShowWarning(LizardMage.BurstContext context, float warningSeconds)
    {
        if (telegraphService == null)
            return;

        telegraphService.Show(CreateWarningSpec(context, warningSeconds));
    }

    private void UpdateWarning(LizardMage.BurstContext context, float warningSeconds)
    {
        if (telegraphService == null)
            return;

        telegraphService.UpdateCurrentGeometry(CreateWarningSpec(context, warningSeconds));
    }

    /// <summary>
    /// 책임:
    /// - 리자드맨 마법사 조준선용 선형 텔레그래프 Spec을 만들고, 벽 기준 clipping 정보를 함께 담는다.
    /// </summary>
    private AttackTelegraphSpec CreateWarningSpec(LizardMage.BurstContext context, float warningSeconds)
    {
        Vector2 start = CommonMonsterCombatUtility.ResolveAimPoint(owner.gameObject, CombatAimPointKind.ProjectileTarget);
        Vector2 end = context.Target != null
            ? CommonMonsterCombatUtility.ResolveAimPoint(context.Target, CombatAimPointKind.ProjectileTarget)
            : start + context.WarningDirection.normalized * context.TelegraphRange;

        AttackTelegraphSpec spec = AttackTelegraphSpec.CreateLine(
            start,
            end,
            context.WarningWidth,
            warningSeconds,
            warningStyle);
        return spec.WithWallClipping(context.WallLayers, 48, 0.03f);
    }

    private void HideWarning()
    {
        telegraphService?.HideCurrent();
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
