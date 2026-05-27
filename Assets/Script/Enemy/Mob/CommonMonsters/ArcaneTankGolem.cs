using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// - 마도 탱커 골렘의 점프 착지 공격과 후속 4방향 낙석 공격 판단을 소유한다.
/// - 실제 점프 이동, 높이 상태, 경고 표시, 피해 판정은 ArcaneTankGolemSlamRunner에 위임한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MobAbilityCoordinator))]
[RequireComponent(typeof(ArcaneTankGolemSlamRunner))]
public sealed class ArcaneTankGolem : Mob, IMobAttackDecisionSource
{
    [SerializeField] private AbilityDefinition slamAbility;
    [SerializeField, Min(0f)] private float maxHealth = 28f;
    [SerializeField, Range(0.1f, 2f)] private float chaseSpeedScale = 0.65f;

    private ArcaneTankGolemSlamRunner runner;
    private bool hasLoggedInvalidConfig;
    private bool hasSlamFacingLock;
    private AbilityLogic_ArcaneTankGolemSlam Logic => slamAbility != null ? slamAbility.logic as AbilityLogic_ArcaneTankGolemSlam : null;

    public readonly struct SlamContext
    {
        public readonly GameObject Target;
        public readonly Vector2 StartPosition;
        public readonly Vector2 LandingPosition;
        public readonly float LandingWarningSeconds;
        public readonly float JumpSeconds;
        public readonly float LandingDiameter;
        public readonly float JumpVisualHeight;
        public readonly float BodyZHeight;
        public readonly float RockOffsetDistance;
        public readonly float RockDiameter;
        public readonly float RockWarningSeconds;
        public readonly LayerMask TargetLayers;
        public readonly CombatHitPayload HitPayload;

        public SlamContext(
            GameObject target,
            Vector2 startPosition,
            Vector2 landingPosition,
            float landingWarningSeconds,
            float jumpSeconds,
            float landingDiameter,
            float jumpVisualHeight,
            float bodyZHeight,
            float rockOffsetDistance,
            float rockDiameter,
            float rockWarningSeconds,
            LayerMask targetLayers,
            CombatHitPayload hitPayload)
        {
            Target = target;
            StartPosition = startPosition;
            LandingPosition = landingPosition;
            LandingWarningSeconds = landingWarningSeconds;
            JumpSeconds = jumpSeconds;
            LandingDiameter = landingDiameter;
            JumpVisualHeight = jumpVisualHeight;
            BodyZHeight = bodyZHeight;
            RockOffsetDistance = rockOffsetDistance;
            RockDiameter = rockDiameter;
            RockWarningSeconds = rockWarningSeconds;
            TargetLayers = targetLayers;
            HitPayload = hitPayload;
        }
    }

    protected override void Awake()
    {
        base.Awake();
        runner = GetComponent<ArcaneTankGolemSlamRunner>();
        ApplyStats();
        ChaseIntent?.SetSpeedScale(chaseSpeedScale);
    }

    protected override void Start()
    {
        base.Start();
        if (abilitySystem != null && slamAbility != null)
            abilitySystem.GiveAbility(slamAbility);
    }

    public override bool CanUseChaseMovement()
    {
        return base.CanUseChaseMovement() && (runner == null || !runner.IsRunning);
    }

    public bool TryBuildAttackRequest(out MobAttackRequest request)
    {
        request = default;
        GameObject targetObject = Target != null ? Target.gameObject : null;
        AbilityLogic_ArcaneTankGolemSlam logic = Logic;
        if (!HasRequiredData() || logic == null || !CommonMonsterCombatUtility.InRange(transform, targetObject, logic.AttackRange))
            return false;

        request = new MobAttackRequest(slamAbility, targetObject, logic.RecoverSeconds);
        return request.IsValid;
    }

    /// <summary>공격 상태 진입 시 점프 준비 애니메이션을 요청한다.</summary>
    public void OnAttackStateEntered(MobAttackRequest request)
    {
        AcquireSlamFacingLock();
        CommonMonsterCombatUtility.TriggerAnimation(this, CommonMonsterAnimationCue.AttackReady);
    }

    /// <summary>공격 상태 종료 시 취소가 아니라면 회복 애니메이션을 요청한다.</summary>
    public void OnAttackStateExited(MobAttackRequest request, bool wasCancelled)
    {
        ReleaseSlamFacingLock();
        if (!wasCancelled && !IsDead)
            CommonMonsterCombatUtility.TriggerAnimation(this, CommonMonsterAnimationCue.Recover);
    }

    protected override void OnDeathStarted()
    {
        ReleaseSlamFacingLock();
        CommonMonsterCombatUtility.TriggerAnimation(this, CommonMonsterAnimationCue.Die);
        base.OnDeathStarted();
    }

    /// <summary>
    /// 책임:
    /// - 점프 준비부터 착지 종료까지 골렘의 자동 좌우 반전을 잠가 공격 방향 연출을 고정한다.
    /// - 공격 상태 진입/정리 경로가 중복 호출되어도 lock이 누적되지 않도록 보호한다.
    /// </summary>
    private void AcquireSlamFacingLock()
    {
        if (hasSlamFacingLock)
            return;

        PushFacingLock();
        hasSlamFacingLock = true;
    }

    /// <summary>
    /// 책임:
    /// - 점프 착지 패턴의 자동 좌우 반전 잠금을 해제한다.
    /// - LandEnd, 취소, 사망, 공격 상태 종료 중 어느 경로에서든 안전하게 호출될 수 있다.
    /// </summary>
    public void ReleaseSlamFacingLock()
    {
        if (!hasSlamFacingLock)
            return;

        PopFacingLock();
        hasSlamFacingLock = false;
    }

    public bool TryBuildSlamContext(AbilitySystem system, AbilitySpec spec, GameObject explicitTarget, out SlamContext context)
    {
        context = default;
        GameObject targetObject = explicitTarget != null ? explicitTarget : Target != null ? Target.gameObject : null;
        AbilityLogic_ArcaneTankGolemSlam logic = Logic;
        if (!HasRequiredData() || logic == null || !CommonMonsterCombatUtility.InRange(transform, targetObject, logic.AttackRange))
            return false;

        CombatHitPayload payload = CommonMonsterCombatUtility.BuildPayload(
            system != null ? system : abilitySystem,
            spec,
            logic.DamageEffect,
            logic.KnockbackEffect,
            gameObject,
            logic.DamageAmount,
            logic.KnockbackImpulse);

        context = new SlamContext(
            targetObject,
            transform.position,
            targetObject.transform.position,
            logic.LandingWarningSeconds,
            logic.JumpSeconds,
            logic.LandingDiameter,
            logic.JumpVisualHeight,
            logic.BodyZHeight,
            logic.RockOffsetDistance,
            logic.RockDiameter,
            logic.RockWarningSeconds,
            logic.TargetLayers,
            payload);
        return true;
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
        AbilityLogic_ArcaneTankGolemSlam logic = Logic;
        bool valid = abilitySystem != null && slamAbility != null && logic != null && logic.DamageEffect != null && runner != null;
        if (valid)
            return true;

        if (!hasLoggedInvalidConfig)
        {
            Debug.LogError($"[{nameof(ArcaneTankGolem)}] 점프 내려찍기 설정이 비어 있습니다.", this);
            hasLoggedInvalidConfig = true;
        }

        return false;
    }
}

/// <summary>
/// 책임:
/// - 마도 탱커 골렘의 착지 위치 경고, 점프 이동, 착지 피해, 4방향 낙석 경고/피해를 순차 실행한다.
/// - 패턴 도중 취소되면 높이 상태와 모든 경고 표시를 정리한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ArcaneTankGolem))]
public sealed partial class ArcaneTankGolemSlamRunner : MonoBehaviour, IMobPatternRunner, IMobPresentationCleanup
{
    [SerializeField] private ArcaneTankGolem owner;
    [SerializeField] private MobAbilityCoordinator abilityCoordinator;
    [SerializeField] private AttackTelegraphService telegraphService;

    private readonly List<AttackTelegraphView> detachedWarnings = new();
    private readonly List<GameObject> spawnedRockFallVisuals = new();
    private AttackTelegraphStyle landingWarningStyle;
    private AttackTelegraphStyle rockWarningStyle;
    private CombatHeightState2D heightState;
    private bool isRunning;
    private bool cancelRequested;

    public bool IsRunning => isRunning;

    private void Awake()
    {
        if (owner == null)
            owner = GetComponent<ArcaneTankGolem>();
        if (abilityCoordinator == null)
            abilityCoordinator = GetComponent<MobAbilityCoordinator>();
        if (telegraphService == null)
            telegraphService = GetComponent<AttackTelegraphService>();
        heightState = GetComponent<CombatHeightState2D>();
        if (heightState == null)
            heightState = gameObject.AddComponent<CombatHeightState2D>();
        landingWarningStyle = CreateWarningStyle(new Color(0.58f, 0.08f, 1f, 1f));
        rockWarningStyle = CreateWarningStyle(new Color(0.35f, 0.65f, 1f, 1f));
    }

    private void OnDestroy()
    {
        if (landingWarningStyle != null)
            Destroy(landingWarningStyle);
        if (rockWarningStyle != null)
            Destroy(rockWarningStyle);
    }

    private void OnDisable()
    {
        Cancel();
    }

    public IEnumerator Run(
        AbilitySystem system,
        AbilitySpec spec,
        GameObject initialTarget,
        float landingImpactDelay,
        GameObject landingImpactEffectPrefab,
        Vector3 landingImpactEffectOffset,
        float landingImpactEffectScale,
        float landingImpactEffectFallbackLifetime,
        GameObject rockFallVisualPrefab,
        Vector3 rockFallVisualOffset,
        float rockFallSpawnHeight,
        float rockFallSeconds)
    {
        if (owner == null) yield break;
        if (!owner.TryBuildSlamContext(system, spec, initialTarget, out ArcaneTankGolem.SlamContext context)) yield break;
        if (abilityCoordinator != null && !abilityCoordinator.TryBeginRunner(this)) yield break;

        isRunning = true;
        cancelRequested = false;
        bool didEnterLand = false;

        try
        {
            float landingWarningSeconds = CombatTimingService.ScaleSeconds(system, context.LandingWarningSeconds, CombatTimingSlot.AttackWarning);
            ShowLandingWarning(context, landingWarningSeconds);
            if (landingWarningSeconds > 0f)
                yield return AbilityTasks.WaitDelay(system, spec, landingWarningSeconds);

            HideCurrentWarning();
            if (cancelRequested || owner.IsDead || IsCancelled(spec))
                yield break;

            CommonMonsterCombatUtility.TriggerAnimation(owner, CommonMonsterAnimationCue.Jump);
            yield return JumpToLanding(context, spec);
            if (cancelRequested || owner.IsDead || IsCancelled(spec))
                yield break;

            CommonMonsterCombatUtility.TriggerAnimation(owner, CommonMonsterAnimationCue.Land);
            didEnterLand = true;
            if (landingImpactDelay > 0f)
                yield return AbilityTasks.WaitDelay(system, spec, landingImpactDelay);

            if (cancelRequested || owner.IsDead || IsCancelled(spec))
                yield break;

            SpawnLandingImpactEffect(
                context.LandingPosition,
                landingImpactEffectPrefab,
                landingImpactEffectOffset,
                landingImpactEffectScale,
                landingImpactEffectFallbackLifetime);
            CommonMonsterCombatUtility.TryApplyCircleDamage(
                context.LandingPosition,
                context.LandingDiameter,
                context.TargetLayers,
                owner.gameObject,
                context.HitPayload);

            Vector2[] rockPositions = BuildRockPositions(context.LandingPosition, context.RockOffsetDistance);
            float rockWarningSeconds = CombatTimingService.ScaleSeconds(system, context.RockWarningSeconds, CombatTimingSlot.AttackWarning);
            ShowRockWarnings(rockPositions, context, rockWarningSeconds);
            if (rockWarningSeconds > 0f)
                yield return AbilityTasks.WaitDelay(system, spec, rockWarningSeconds);

            if (cancelRequested || owner.IsDead || IsCancelled(spec))
                yield break;

            HideDetachedWarnings();
            yield return PlayRockFallVisuals(
                rockPositions,
                rockFallVisualPrefab,
                rockFallVisualOffset,
                rockFallSpawnHeight,
                rockFallSeconds,
                spec);

            if (cancelRequested || owner.IsDead || IsCancelled(spec))
                yield break;

            for (int i = 0; i < rockPositions.Length; i++)
            {
                CommonMonsterCombatUtility.TryApplyCircleDamage(
                    rockPositions[i],
                    context.RockDiameter,
                    context.TargetLayers,
                    owner.gameObject,
                    context.HitPayload);
            }
        }
        finally
        {
            if (didEnterLand && owner != null && !owner.IsDead)
                CommonMonsterCombatUtility.TriggerAnimation(owner, CommonMonsterAnimationCue.LandEnd);

            owner?.ReleaseSlamFacingLock();
            HideCurrentWarning();
            HideDetachedWarnings();
            heightState?.SetGrounded();
            cancelRequested = false;
            isRunning = false;
            abilityCoordinator?.EndRunner(this);
        }
    }

    public void Cancel()
    {
        cancelRequested = true;
        HideCurrentWarning();
        HideDetachedWarnings();
        DestroySpawnedRockFallVisuals();
        heightState?.SetGrounded();
    }

    public void CleanupPresentation()
    {
        HideCurrentWarning();
        HideDetachedWarnings();
        DestroySpawnedRockFallVisuals();
    }

    private void SpawnLandingImpactEffect(
        Vector2 landingPosition,
        GameObject effectPrefab,
        Vector3 effectOffset,
        float effectScale,
        float fallbackLifetime)
    {
        if (effectPrefab == null)
            return;

        Vector3 spawnPosition = new Vector3(landingPosition.x, landingPosition.y, transform.position.z) + effectOffset;
        GameObject instance = Instantiate(effectPrefab, spawnPosition, effectPrefab.transform.rotation);
        if (instance == null)
            return;

        instance.transform.localScale = effectPrefab.transform.localScale * Mathf.Max(0.01f, effectScale);

        ParticleSystem particle = instance.GetComponentInChildren<ParticleSystem>();
        if (particle != null)
            Destroy(instance, particle.main.duration + particle.main.startLifetime.constantMax);
        else
            Destroy(instance, Mathf.Max(0.01f, fallbackLifetime));
    }

    private IEnumerator JumpToLanding(ArcaneTankGolem.SlamContext context, AbilitySpec spec)
    {
        Vector3 start = context.StartPosition;
        Vector3 end = context.LandingPosition;
        float duration = Mathf.Max(0.01f, context.JumpSeconds);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (cancelRequested || owner.IsDead || IsCancelled(spec))
                yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.position = Vector3.Lerp(start, end, t);
            float height = Mathf.Sin(t * Mathf.PI) * context.JumpVisualHeight;
            heightState?.SetAirborne(height, context.BodyZHeight);
            yield return null;
        }

        transform.position = end;
        heightState?.SetGrounded();
    }

    private void ShowLandingWarning(ArcaneTankGolem.SlamContext context, float warningSeconds)
    {
        telegraphService?.Show(AttackTelegraphSpec.CreateCircle(
            context.LandingPosition,
            context.LandingDiameter,
            warningSeconds,
            landingWarningStyle));
    }

    private void ShowRockWarnings(Vector2[] centers, ArcaneTankGolem.SlamContext context, float warningSeconds)
    {
        if (telegraphService == null)
            return;

        for (int i = 0; i < centers.Length; i++)
        {
            AttackTelegraphView view = telegraphService.SpawnDetachedView(AttackTelegraphSpec.CreateCircle(
                centers[i],
                context.RockDiameter,
                warningSeconds,
                rockWarningStyle));
            if (view != null)
                detachedWarnings.Add(view);
        }
    }

    private void HideCurrentWarning()
    {
        telegraphService?.HideCurrent();
    }

    private void HideDetachedWarnings()
    {
        for (int i = 0; i < detachedWarnings.Count; i++)
        {
            if (detachedWarnings[i] != null)
                detachedWarnings[i].HideImmediate();
        }

        detachedWarnings.Clear();
    }

    private IEnumerator PlayRockFallVisuals(
        Vector2[] centers,
        GameObject visualPrefab,
        Vector3 visualOffset,
        float spawnHeight,
        float fallSeconds,
        AbilitySpec spec)
    {
        if (visualPrefab == null || centers == null)
            yield break;

        var visuals = new List<RockFallVisualInstance>(centers.Length);
        for (int i = 0; i < centers.Length; i++)
        {
            Vector3 landingPosition = new Vector3(centers[i].x, centers[i].y, transform.position.z) + visualOffset;
            Vector3 startPosition = landingPosition + Vector3.up * Mathf.Max(0f, spawnHeight);
            GameObject instance = Instantiate(visualPrefab, startPosition, Quaternion.identity);
            spawnedRockFallVisuals.Add(instance);
            visuals.Add(new RockFallVisualInstance(instance.transform, startPosition, landingPosition));
        }

        float duration = Mathf.Max(0.01f, fallSeconds);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (cancelRequested || owner.IsDead || IsCancelled(spec))
                yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t;
            for (int i = 0; i < visuals.Count; i++)
            {
                RockFallVisualInstance visual = visuals[i];
                if (visual.Transform != null)
                    visual.Transform.position = Vector3.Lerp(visual.StartPosition, visual.LandingPosition, eased);
            }

            yield return null;
        }

        for (int i = 0; i < visuals.Count; i++)
        {
            RockFallVisualInstance visual = visuals[i];
            if (visual.Transform != null)
                visual.Transform.position = visual.LandingPosition;
        }

        DestroySpawnedRockFallVisuals();
    }

    private void DestroySpawnedRockFallVisuals()
    {
        for (int i = 0; i < spawnedRockFallVisuals.Count; i++)
        {
            if (spawnedRockFallVisuals[i] != null)
                Destroy(spawnedRockFallVisuals[i]);
        }

        spawnedRockFallVisuals.Clear();
    }

    private static Vector2[] BuildRockPositions(Vector2 center, float offsetDistance)
    {
        float distance = Mathf.Max(0f, offsetDistance);
        return new[]
        {
            center + Vector2.up * distance,
            center + Vector2.right * distance,
            center + Vector2.down * distance,
            center + Vector2.left * distance
        };
    }

    private static bool IsCancelled(AbilitySpec spec)
    {
        return spec != null && spec.Token != null && spec.Token.IsCancelled;
    }

    private static AttackTelegraphStyle CreateWarningStyle(Color accent)
    {
        AttackTelegraphStyle style = ScriptableObject.CreateInstance<AttackTelegraphStyle>();
        AttackTelegraphStyleUtility.ApplyDangerAreaColors(style);
        style.progressCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        style.blinkStartNormalized = 0.7f;
        style.blinkFrequency = 5f;
        style.blinkAlphaMin = 0.45f;
        style.scaleFillWithProgress = false;
        style.fillScaleStart = 1f;
        style.fillScaleEnd = 1f;
        return style;
    }

    /// <summary>
    /// 책임:
    /// - 낙석 비주얼 하나의 시작/착지 위치를 코루틴 동안 안전하게 보관한다.
    /// </summary>
    private readonly struct RockFallVisualInstance
    {
        public readonly Transform Transform;
        public readonly Vector3 StartPosition;
        public readonly Vector3 LandingPosition;

        public RockFallVisualInstance(Transform transform, Vector3 startPosition, Vector3 landingPosition)
        {
            Transform = transform;
            StartPosition = startPosition;
            LandingPosition = landingPosition;
        }
    }
}
