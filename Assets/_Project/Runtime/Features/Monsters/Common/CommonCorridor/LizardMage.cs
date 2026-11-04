using System.Collections;
using UnityEngine;
using UnityGAS;
using CapstoneAudio;

/// <summary>
/// 책임:
/// - 리자드맨 마법사의 이동 사격 판단과 단발 추적탄 발사 문맥을 소유한다.
/// - 준비/발사/재배치 실행은 Runner에 위임하고 발사 후 대기 시각은 개체별로 소유한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MobAbilityCoordinator))]
[RequireComponent(typeof(LizardMageBurstRunner))]
public sealed class LizardMage : Mob, IMobAttackDecisionSource, IMobProjectileLaneSource
{
    private static readonly SoundRef ShotFireSound = SoundRef.FromKey("sound_lizardMage_shotFire");

    [SerializeField] private AbilityDefinition burstAbility;

    private LizardMageBurstRunner runner;
    private float nextAttackTime;
    private bool isRetreating;
    private int strafeSign;
    private bool hasLoggedInvalidConfig;
    private AbilityLogic_LizardMageBurst Logic => burstAbility != null ? burstAbility.logic as AbilityLogic_LizardMageBurst : null;

    public AbilityLogic_LizardMageBurst BurstLogic => Logic;
    public GameObject ShotProjectilePrefab => Logic != null ? Logic.ProjectilePrefab : null;
    public LayerMask ShotWallLayers => Logic != null ? Logic.WallLayers : 0;
    public bool IsRestingBetweenBursts => CombatHitPause2D.GetActionTime(gameObject) < nextAttackTime;

    // 책임: 리자드 마법사 단발 공격의 타겟, 조준 방향, 사거리, 피해 정보를 보관한다.
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
        strafeSign = GetInstanceID() % 2 == 0 ? 1 : -1;
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
        if (IsRestingBetweenBursts)
            return false;

        GameObject targetObject = Target != null ? Target.gameObject : null;
        AbilityLogic_LizardMageBurst logic = Logic;
        if (!HasRequiredData() || logic == null || !CommonMonsterCombatUtility.InRange(transform, targetObject, logic.AttackRange))
            return false;

        request = new MobAttackRequest(burstAbility, targetObject, logic.RecoverSeconds);
        return request.IsValid;
    }

    public override bool CanUsePostAttackRecoveryRetreat() => false;

    /// <summary>Only the active runner owns spacing, so chase/recovery cannot add a second movement.</summary>
    public void TickCombatSpacing()
    {
        StopCombatSpacing();
        AbilityLogic_LizardMageBurst logic = Logic;
        if (logic == null || Target == null || externalMovement == null || IsDead ||
            CombatHitPause2D.IsParalyzedOn(gameObject)) return;

        Vector2 toTarget = (Vector2)(Target.position - transform.position);
        float distance = toTarget.magnitude;
        if (distance <= 0.001f) toTarget = Vector2.right;
        else toTarget /= distance;

        if (isRetreating)
            isRetreating = distance < logic.RetreatStopDistance;
        else
            isRetreating = distance < logic.RetreatStartDistance;

        Vector2 tangent = new Vector2(-toTarget.y, toTarget.x) * strafeSign;
        Vector2 direction = isRetreating ? -toTarget : distance > logic.ApproachDistance ? toTarget : tangent;
        float speed = CommonMonsterCombatUtility.ResolvePlayerBaseSpeed(Target) *
            (isRetreating || distance > logic.ApproachDistance ? logic.RetreatSpeedMultiplier : logic.StrafeSpeedMultiplier);
        float duration = Mathf.Max(0.08f, Time.fixedDeltaTime * 2f);
        float probeDistance = speed * duration + 0.03f;
        if (IsRecoveryRetreatDirectionBlocked(direction, probeDistance))
        {
            if (!IsRecoveryRetreatDirectionBlocked(tangent, probeDistance))
                direction = tangent;
            else if (!IsRecoveryRetreatDirectionBlocked(-tangent, probeDistance))
            {
                strafeSign = -strafeSign;
                direction = -tangent;
            }
            else return;
        }
        externalMovement.AddTimedVelocity(direction * speed, duration, 0f, this);
    }

    public void StopCombatSpacing() => externalMovement?.RemoveTimedVelocitiesFromSource(this);

    /// <summary>공격 상태 진입 시 마법 준비 애니메이션을 요청한다.</summary>
    public void OnAttackStateEntered(MobAttackRequest request)
    {
        CommonMonsterCombatUtility.TriggerAnimation(this, CommonMonsterAnimationCue.AttackReady);
    }

    /// <summary>공격 상태가 끝날 때 이 개체가 소유한 재배치 이동을 정리한다.</summary>
    public void OnAttackStateExited(MobAttackRequest request, bool wasCancelled)
    {
        StopCombatSpacing();
    }

    public void CleanupMovingCastPresentation()
    {
        if (animator == null || IsDead) return;
        animator.ResetTrigger("LizardMage_AttackReady");
        animator.ResetTrigger("LizardMage_Attack");
        animator.ResetTrigger("LizardMage_Recover");
        animator.Play("Base Layer.Idle", 0, 0f);
    }

    protected override void OnDeathStarted()
    {
        CommonMonsterCombatUtility.TriggerAnimation(this, CommonMonsterAnimationCue.Die);
        base.OnDeathStarted();
    }

    public bool TryBuildBurstContext(AbilitySystem system, AbilitySpec spec, GameObject explicitTarget, out BurstContext context)
    {
        context = default;
        if (IsRestingBetweenBursts)
            return false;

        GameObject targetObject = explicitTarget != null ? explicitTarget : Target != null ? Target.gameObject : null;
        AbilityLogic_LizardMageBurst logic = Logic;
        if (!HasRequiredData() || logic == null || !CommonMonsterCombatUtility.InRange(transform, targetObject, logic.AttackRange))
            return false;

        Vector2 origin = CommonMonsterCombatUtility.ResolveAimPoint(gameObject);
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
        if (IsRestingBetweenBursts)
            return;

        Vector2 direction = context.WarningDirection;

        AbilityLogic_LizardMageBurst logic = Logic;
        if (logic == null || logic.ProjectilePrefab == null)
            return;

        Vector2 origin = CommonMonsterCombatUtility.ResolveAimPoint(gameObject);
        if (!MobProjectileLaneUtility.IsClear(this, origin, direction, 0.2f)) return;
        GameObject projectileObject = Instantiate(logic.ProjectilePrefab, origin, Quaternion.identity);
        if (projectileObject == null)
            return;

        LightBeadProjectile2D projectile = projectileObject.GetComponent<LightBeadProjectile2D>();
        if (projectile == null)
        {
            Debug.LogError($"[{nameof(LizardMage)}] AL projectilePrefab에 {nameof(LightBeadProjectile2D)}가 없습니다.", projectileObject);
            Destroy(projectileObject);
            return;
        }

        PlayShotFireSound(origin);
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
        projectile.ConfigureLimitedHoming(context.Target != null ? context.Target.transform : null,
            logic.HomingSeconds, logic.HomingTurnDegreesPerSecond, logic.HomingHalfAngle);
        nextAttackTime = CombatHitPause2D.GetActionTime(gameObject) + logic.ShotRestSeconds;
    }

    /// <summary>리자드 메이지의 각 마법탄 발사 타이밍에 사운드를 재생합니다.</summary>
    private void PlayShotFireSound(Vector2 origin)
    {
        SoundPlaybackUtility.Play(
            ShotFireSound,
            instigator: gameObject,
            causer: gameObject,
            target: Target != null ? Target.gameObject : null,
            position: origin,
            sourceObject: this);
    }

    private bool HasRequiredData()
    {
        AbilityLogic_LizardMageBurst logic = Logic;
        bool valid = abilitySystem != null && burstAbility != null && logic != null &&
            logic.ProjectilePrefab != null && logic.DamageEffect != null && runner != null &&
            externalMovement != null && collision != null;
        if (valid)
            return true;

        if (!hasLoggedInvalidConfig)
        {
            Debug.LogError($"[{nameof(LizardMage)}] 이동 추적탄 공격 설정이 비어 있습니다.", this);
            hasLoggedInvalidConfig = true;
        }

        return false;
    }
}

/// <summary>
/// 책임:
/// - 이동하며 조준하고 마지막 0.2초는 방향을 고정한 뒤, 현재 위치에서 추적탄 한 발을 발사한다.
/// - 준비와 발사 후 재배치 동안의 이동/경고 수명을 소유하고 취소 시 즉시 정리한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(LizardMage))]
public sealed partial class LizardMageBurstRunner : MonoBehaviour, IMobPatternRunner, IMobPresentationCleanup
{
    [SerializeField] private LizardMage owner;
    [SerializeField] private MobAbilityCoordinator abilityCoordinator;
    [SerializeField] private MonoBehaviour telegraphService;

    private AttackTelegraphStyle warningStyle;
    private IAttackTelegraphPresenter telegraphPresenter;
    private bool isRunning;
    private bool cancelRequested;

    public bool IsRunning => isRunning;

    private void Awake()
    {
        if (owner == null)
            owner = GetComponent<LizardMage>();
        if (abilityCoordinator == null)
            abilityCoordinator = GetComponent<MobAbilityCoordinator>();
        telegraphPresenter = AttackTelegraphPresenterResolver.Resolve(telegraphService, this);
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
        isRunning = false;
        abilityCoordinator?.EndRunner(this);
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
            float warningSeconds = Mathf.Max(0.8f,
                CombatTimingService.ScaleSeconds(system, context.WarningSeconds, CombatTimingSlot.AttackWarning));
            ShowWarning(context, warningSeconds);
            float trackingSeconds = Mathf.Max(0f, warningSeconds - 0.2f);
            float elapsed = 0f;
            while (elapsed < warningSeconds)
            {
                if (ShouldStop(spec, context.Target)) yield break;
                if (CombatHitPause2D.IsParalyzedOn(owner.gameObject))
                {
                    owner.StopCombatSpacing();
                    yield return null;
                    continue;
                }
                owner.TickCombatSpacing();
                if (elapsed < trackingSeconds && owner.TryBuildBurstContext(system, spec, initialTarget, out LizardMage.BurstContext trackedContext))
                {
                    context = trackedContext;
                }
                UpdateWarning(context, warningSeconds);
                elapsed += CombatHitPause2D.GetActionDeltaTime(owner.gameObject);
                yield return null;
            }
            if (ShouldStop(spec, context.Target)) yield break;

            HideWarning();
            CommonMonsterCombatUtility.TriggerAnimation(owner, CommonMonsterAnimationCue.Attack);
            owner.FireProjectile(context);
            // A deterministic, attack-speed-independent rest replaces the old random burst budget.
            // Keep spacing active during it instead of returning to the approach-only Chase state.
            elapsed = 0f;
            bool recoveryRequested = false;
            while (elapsed < owner.BurstLogic.ShotRestSeconds)
            {
                if (ShouldStop(spec, context.Target)) yield break;
                if (CombatHitPause2D.IsParalyzedOn(owner.gameObject))
                {
                    owner.StopCombatSpacing();
                    yield return null;
                    continue;
                }
                owner.TickCombatSpacing();
                if (!recoveryRequested && elapsed >= 0.2f)
                {
                    CommonMonsterCombatUtility.TriggerAnimation(owner, CommonMonsterAnimationCue.Recover);
                    recoveryRequested = true;
                }
                elapsed += CombatHitPause2D.GetActionDeltaTime(owner.gameObject);
                yield return null;
            }
        }
        finally
        {
            CleanupPresentation();
            owner.StopCombatSpacing();
            isRunning = false;
            abilityCoordinator?.EndRunner(this);
        }
    }

    public void Cancel()
    {
        cancelRequested = true;
        owner?.StopCombatSpacing();
        CleanupPresentation();
    }

    public void CleanupPresentation()
    {
        HideWarning();
        if (isRunning) owner?.CleanupMovingCastPresentation();
    }

    private void ShowWarning(LizardMage.BurstContext context, float warningSeconds)
    {
        if (telegraphPresenter == null)
            return;

        telegraphPresenter.Show(CreateWarningSpec(context, warningSeconds));
    }

    private void UpdateWarning(LizardMage.BurstContext context, float warningSeconds)
    {
        if (telegraphPresenter == null)
            return;

        telegraphPresenter.UpdateCurrentGeometry(CreateWarningSpec(context, warningSeconds));
    }

    /// <summary>
    /// 책임:
    /// - 리자드맨 마법사 조준선용 선형 텔레그래프 Spec을 만들고, 벽 기준 clipping 정보를 함께 담는다.
    /// </summary>
    private AttackTelegraphSpec CreateWarningSpec(LizardMage.BurstContext context, float warningSeconds)
    {
        Vector2 start = CommonMonsterCombatUtility.ResolveAimPoint(owner.gameObject, CombatAimPointKind.ProjectileTarget);
        Vector2 warningDirection = context.WarningDirection.normalized;
        Vector2 end = start + warningDirection * context.TelegraphRange;

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
        telegraphPresenter?.HideCurrent();
    }

    private bool ShouldStop(AbilitySpec spec, GameObject targetObject) =>
        cancelRequested || owner == null || owner.IsDead || targetObject == null ||
        (abilityCoordinator != null && abilityCoordinator.IsAbilityExecutionSuppressed) ||
        (spec != null && spec.Token != null && spec.Token.IsCancelled);

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
