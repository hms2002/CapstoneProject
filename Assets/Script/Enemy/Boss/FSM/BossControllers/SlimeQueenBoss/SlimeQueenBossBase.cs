using System;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 슬라임 여왕 계열 보스가 공유하는 타겟 방향, 이동 차단, 경고 표시 기반 기능입니다.
/// </summary>
public abstract class SlimeQueenBossBase : BossControllerBase, IIntentMovementSource2D
{
    private const string GroggyTagResourcePath = "Tags/State.Status.Groggy";
    private const float DefaultGroggyDurationSeconds = 3f;
    private const int ThinWarningOutlineWallLayer = 30;
    private const int ThinWarningOutlineSampleCount = 48;
    private const float ThinWarningOutlineSkinWidth = 0.03f;
    private const float KnightStyleSlamTravelNormalized = 0.28f;
    private const float KnightStyleSlamHoldEndNormalized = 0.86f;
    private const float KnightStyleSlamPreDropEndNormalized = 0.96f;
    private const float KnightStyleSlamPreDropHeightScale = 0.9f;
    private const float KnightStyleSlamTravelEaseOutPower = 2.2f;
    private const float KnightStyleSlamLandingDropSharpness = 0.22f;
    private const string PlayerLayerName = "Player";
    private const string EnemyActorLayerName = "TEMP_Enemy_LAYER";
    private static readonly CameraShakeHook SlamLandingCameraShake = CameraShakeHook.Create(
        amplitude: 0.22f,
        amplitudeMultiplier: 1f,
        maxAmplitude: 0.45f,
        minIntervalSeconds: 0.04f);

    [Header("Height Presentation")]
    [Tooltip("점프/내려찍기 중 공중 판정 높이로 사용할 바디 Z 높이입니다.")]
    [SerializeField, Min(0f)] private float airborneBodyZHeight = 1f;
    [SerializeField] private bool logHeightCollisionDebug = true;

    [Header("Pattern Afterimage")]
    [Tooltip("점프/돌진 같은 빠른 이동형 패턴 중 본체 잔상을 남길지 여부입니다.")]
    [SerializeField] private bool enablePatternAfterimage = true;

    [Tooltip("잔상 스냅샷을 생성하는 간격입니다.")]
    [SerializeField, Min(0.01f)] private float patternAfterimageIntervalSeconds = 0.045f;

    [Tooltip("각 잔상 스냅샷이 사라질 때까지 걸리는 시간입니다.")]
    [SerializeField, Min(0.01f)] private float patternAfterimageLifetimeSeconds = 0.18f;

    [Tooltip("잔상에 입힐 색과 투명도입니다.")]
    [SerializeField] private Color patternAfterimageColor = new(0.65f, 1f, 0.85f, 0.38f);

    [Header("Body Inflate Presentation")]
    [Tooltip("몸 부풀림 충격 중 Visual에 추가로 곱할 스케일 배율입니다.")]
    [SerializeField, Min(1f)] private float bodyInflateVisualScaleMultiplier = 2f;

    [Tooltip("몸 부풀림 Visual 스케일이 커지는 데 걸리는 시간입니다.")]
    [SerializeField, Min(0.01f)] private float bodyInflateVisualScaleInSeconds = 0.12f;

    [Tooltip("몸 부풀림 Visual 스케일이 원래대로 돌아오는 데 걸리는 시간입니다.")]
    [SerializeField, Min(0.01f)] private float bodyInflateVisualScaleOutSeconds = 0.16f;

    [Tooltip("몸 부풀림 Visual 스케일 변화 단계를 몇 단계로 끊어 보일지 설정합니다. 0이면 부드럽게 보간합니다.")]
    [SerializeField, Min(0)] private int bodyInflateVisualScaleSteps = 5;

    private AttackTelegraphService telegraphService;
    private CombatHeightState2D combatHeightState;
    private CombatHeightPresentation2D combatHeightPresentation;
    private EntityCollisionProfile2D heightCollisionProfile;
    private CombatHeightCollisionBinder2D heightCollisionBinder;
    private SpriteAfterimageEmitter2D patternAfterimageEmitter;
    private Collider2D[] heightCollisionBodyColliders;
    private GameplayTag patternMoveInvulnerableTag;
    private GameplayEffect runtimeGroggyStatusEffect;
    private bool isPatternMoveDamageBlocked;
    private bool hasAppliedPatternMoveInvulnerableTag;
    private bool isPitFallRuntimeLocked;
    private int pitFallTriggerBlockCount;
    private float nextHeightCollisionDebugLogTime;
    private float nextAirborneCollisionDebugLogTime;
    private Transform bodyInflateVisualRoot;
    private Vector3 bodyInflateVisualBaseScale = Vector3.one;
    private bool hasBodyInflateVisualBaseScale;
    private bool isBodyInflateVisualScaling;
    private bool isBodyInflateVisualScaleReleasing;
    private float bodyInflateVisualScaleElapsed;
    private bool isPatternFacingLocked;
    private bool patternFacingLockedFlipX;
    private bool isDeathFacingFrozen;
    private bool deathFacingFrozenFlipX;

    public bool IsPatternMoveDamageBlocked => isPatternMoveDamageBlocked;

    public bool CanTriggerPitFall => !isPitFallRuntimeLocked && pitFallTriggerBlockCount <= 0;

    protected override void Awake()
    {
        base.Awake();
        telegraphService = GetComponent<AttackTelegraphService>();
        combatHeightState = GetComponent<CombatHeightState2D>();
        combatHeightPresentation = GetComponent<CombatHeightPresentation2D>();
        EnsureCombatHeightCollisionBinding();
        patternMoveInvulnerableTag = Resources.Load<GameplayTag>("Tags/State.Invulnerable");
        EnsureGroggyGauge();
    }

    protected override void Update()
    {
        if (isPitFallRuntimeLocked)
        {
            if (movementMotor != null)
                movementMotor.StopAllMotion();

            return;
        }

        base.Update();
        FaceCurrentTarget();
    }

    protected virtual void LateUpdate()
    {
        TickBodyInflateVisualScale();
    }

    protected override void OnDestroy()
    {
        StopPatternAfterimage(clearGhosts: true);
        EndPatternFacingLock();
        CleanupAllTelegraphs();
        ClearCombatHeightPresentation();

        if (runtimeGroggyStatusEffect != null)
        {
            Destroy(runtimeGroggyStatusEffect);
            runtimeGroggyStatusEffect = null;
        }

        base.OnDestroy();
    }

    /// <summary>사망 시작 시 남아 있는 공통/분리형 공격 예고를 즉시 정리합니다.</summary>
    protected override void OnDeathStarted()
    {
        FreezeFacingForDeath();
        EndPatternFacingLock();
        CleanupAllTelegraphs();
        base.OnDeathStarted();
    }

    /// <summary>보스가 기본 의도 이동을 하지 않도록 빈 이동값을 제공합니다.</summary>
    public IntentMovementData GetIntent()
    {
        return IntentMovementData.None;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        LogAirborneCollision("enter", collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        LogAirborneCollision("stay", collision);
    }

    /// <summary>이동형 패턴 중 보스 피격과 접촉 피해를 임시로 막습니다.</summary>
    public void SetPatternMoveDamageBlocked(bool isBlocked)
    {
        if (isPatternMoveDamageBlocked == isBlocked)
            return;

        isPatternMoveDamageBlocked = isBlocked;

        if (isBlocked)
        {
            if (!hasAppliedPatternMoveInvulnerableTag && TryAddStateTag(patternMoveInvulnerableTag))
                hasAppliedPatternMoveInvulnerableTag = true;

            return;
        }

        if (hasAppliedPatternMoveInvulnerableTag && TryRemoveStateTag(patternMoveInvulnerableTag))
            hasAppliedPatternMoveInvulnerableTag = false;
    }

    /// <summary>공중 이동처럼 구덩이 판정을 받으면 안 되는 구간을 시작합니다.</summary>
    public void PushPitFallTriggerBlock()
    {
        pitFallTriggerBlockCount++;
    }

    /// <summary>구덩이 판정 차단 구간을 종료합니다.</summary>
    public void PopPitFallTriggerBlock()
    {
        if (pitFallTriggerBlockCount <= 0)
        {
            pitFallTriggerBlockCount = 0;
            return;
        }

        pitFallTriggerBlockCount--;
    }

    /// <summary>구덩이 낙하 연출 중 기존 패턴과 기본 추적 갱신을 멈춥니다.</summary>
    public void SetPitFallRuntimeLock(bool isLocked)
    {
        if (isPitFallRuntimeLocked == isLocked)
            return;

        isPitFallRuntimeLocked = isLocked;

        if (!isLocked)
            return;

        AbortCurrentPattern();

        if (movementMotor != null)
            movementMotor.StopAllMotion();
    }

    /// <summary>HoleTrap 공통 낙하 파이프라인에서 보스별 시작 후처리를 받을 수 있게 합니다.</summary>
    public void NotifyPitFallStarted(PitFallContext context)
    {
        OnPitFallStarted(context);
    }

    /// <summary>HoleTrap 공통 낙하 파이프라인에서 보스별 완료 후처리를 받을 수 있게 합니다.</summary>
    public void NotifyPitFallCompleted(PitFallContext context)
    {
        OnPitFallCompleted(context);
    }

    /// <summary>현재 타겟 방향에 맞춰 보스 스프라이트 방향을 갱신합니다.</summary>
    public void FaceCurrentTarget()
    {
        if (sprite == null)
            return;

        if (ShouldBlockFacingUpdate())
        {
            ApplyDeathFacingFreeze();
            return;
        }

        if (isPatternFacingLocked)
        {
            ApplyPatternFacingLock();
            return;
        }

        if (CurrentTarget == null)
            return;

        SetFacingByWorldX(CurrentTarget.position.x);
    }

    /// <summary>이동형 패턴 준비부터 종료까지 현재/지정 타겟 기준 flipX를 고정합니다.</summary>
    public void BeginPatternFacingLock(GameObject targetOverride = null)
    {
        if (targetOverride != null)
        {
            BeginPatternFacingLockTowards(targetOverride.transform.position);
            return;
        }

        if (CurrentTarget != null)
        {
            BeginPatternFacingLockTowards(CurrentTarget.position);
            return;
        }

        if (sprite == null)
            return;

        patternFacingLockedFlipX = sprite.flipX;
        isPatternFacingLocked = true;
        ApplyPatternFacingLock();
    }

    /// <summary>이동형 패턴 준비부터 종료까지 지정 월드 좌표 방향으로 flipX를 고정합니다.</summary>
    public void BeginPatternFacingLockTowards(Vector3 targetPosition)
    {
        if (sprite == null)
            return;

        SetFacingByWorldX(targetPosition.x);
        patternFacingLockedFlipX = sprite.flipX;
        isPatternFacingLocked = true;
        ApplyPatternFacingLock();
    }

    /// <summary>이동형 패턴이 끝난 뒤 flipX 고정을 해제합니다.</summary>
    public void EndPatternFacingLock()
    {
        isPatternFacingLocked = false;
    }

    protected bool IsPatternFacingLocked => isPatternFacingLocked;

    protected bool ShouldBlockFacingUpdate()
    {
        return isDeathFacingFrozen || IsDead || HasDeadTag() || CurrentHealthValue <= 0f;
    }

    protected void ApplyPatternFacingLock()
    {
        if (sprite != null)
            sprite.flipX = patternFacingLockedFlipX;
    }

    private void FreezeFacingForDeath()
    {
        if (sprite == null)
            return;

        deathFacingFrozenFlipX = sprite.flipX;
        isDeathFacingFrozen = true;
        ApplyDeathFacingFreeze();
    }

    private void ApplyDeathFacingFreeze()
    {
        if (sprite != null && isDeathFacingFrozen)
            sprite.flipX = deathFacingFrozenFlipX;
    }

    private void SetFacingByWorldX(float targetX)
    {
        if (sprite == null)
            return;

        if (transform.position.x > targetX)
            sprite.flipX = true;
        else if (transform.position.x < targetX)
            sprite.flipX = false;
    }

    /// <summary>
    /// 책임:
    /// - 슬라임 퀸 계열의 빠른 이동형 패턴 중 Visual 기준 잔상 방출을 시작한다.
    /// - 개별 패턴 로직이 SpriteRenderer 복제 방식에 의존하지 않도록 공통 시작점만 제공한다.
    /// </summary>
    public void BeginPatternAfterimage()
    {
        if (!enablePatternAfterimage || !isActiveAndEnabled)
            return;

        SpriteAfterimageEmitter2D emitter = ResolvePatternAfterimageEmitter();
        if (emitter == null)
            return;

        Transform sourceRoot = sprite != null ? sprite.transform : transform;
        emitter.Begin(
            sourceRoot,
            patternAfterimageIntervalSeconds,
            patternAfterimageLifetimeSeconds,
            patternAfterimageColor);
    }

    /// <summary>
    /// 책임:
    /// - 슬라임 퀸 계열 이동형 패턴의 잔상 생성을 멈춘다.
    /// - 일반 종료에서는 남은 잔상이 자연 소멸하고, 씬 정리 같은 강제 상황에서는 즉시 제거할 수 있다.
    /// </summary>
    public void StopPatternAfterimage(bool clearGhosts = false)
    {
        if (patternAfterimageEmitter == null)
            return;

        patternAfterimageEmitter.StopEmission();
        if (clearGhosts)
            patternAfterimageEmitter.ClearSpawnedGhosts();
    }

    /// <summary>패턴 종료 시 이동형 패턴 피해 차단 상태를 정리합니다.</summary>
    protected override void OnPatternEnd(BossPatternEntry patternEntry, bool forced)
    {
        StopPatternAfterimage(clearGhosts: forced);
        EndPatternFacingLock();
        EndBodyInflateVisualScale(resetImmediately: forced);
        SetPatternMoveDamageBlocked(false);
        pitFallTriggerBlockCount = 0;
        CleanupAllTelegraphs();
        ClearCombatHeightPresentation();
    }

    /// <summary>AttackTelegraphService가 소유한 모든 경고 표시를 회수합니다.</summary>
    protected void CleanupAllTelegraphs()
    {
        GetTelegraphService()?.ClearAll();
    }

    /// <summary>
    /// 책임:
    /// - 슬라임 퀸 계열 경고 telegraph를 Rook 경고와 같은 mesh/LineRenderer 기반 얇은 외곽선 경로로 렌더링하게 한다.
    /// - 실제 공격 판정은 건드리지 않고, 표시용 wall clipping 옵션만 공통으로 부여한다.
    /// </summary>
    protected static AttackTelegraphSpec WithThinWarningOutline(AttackTelegraphSpec spec)
    {
        LayerMask wallLayers = default;
        wallLayers.value = 1 << ThinWarningOutlineWallLayer;
        return spec.WithWallClipping(
            wallLayers,
            ThinWarningOutlineSampleCount,
            ThinWarningOutlineSkinWidth);
    }

    /// <summary>
    /// 책임:
    /// - 점프/내려찍기 중 root와 collider는 바닥 좌표에 두고, visual 높이는 CombatHeightState2D로 분리한다.
    /// - 높이 프레젠테이션 컴포넌트가 아직 없는 프리팹은 root 높이 이동으로 fallback해 authoring 전에도 연출을 유지한다.
    /// </summary>
    protected void ApplyGroundedMotionPose(Vector3 groundPosition, float visualHeight)
    {
        if (movementMotor != null)
            movementMotor.StopAllMotion();

        float safeVisualHeight = Mathf.Max(0f, visualHeight);
        if (!CanUseCombatHeightPresentation())
        {
            transform.position = groundPosition + Vector3.up * safeVisualHeight;
            return;
        }

        transform.position = groundPosition;
        EnsureCombatHeightState()?.SetAirborne(safeVisualHeight, airborneBodyZHeight);
        LogHeightCollisionStateThrottled("airborne pose");
    }

    /// <summary>Knight 슬라임처럼 착지 위치 위로 빠르게 올라가 체공한 뒤 마지막에 급강하하는 자세를 적용합니다.</summary>
    protected void ApplyKnightStyleSlamPose(Vector3 startPosition, Vector3 landingPosition, float normalizedTime, float visualHeight)
    {
        float clampedTime = Mathf.Clamp01(normalizedTime);
        float groundProgress = ResolveKnightStyleSlamGroundProgress(clampedTime);
        Vector3 groundPosition = Vector3.Lerp(startPosition, landingPosition, groundProgress);
        groundPosition.z = landingPosition.z;

        float height = ResolveKnightStyleSlamVisualHeight(clampedTime, visualHeight);
        ApplyGroundedMotionPose(groundPosition, height);
    }

    /// <summary>점프/내려찍기 종료 시 root를 착지 좌표에 고정하고 visual 높이를 지상 상태로 되돌립니다.</summary>
    protected void SnapToGroundedMotionLanding(Vector3 landingPosition)
    {
        if (movementMotor != null)
            movementMotor.StopAllMotion();

        transform.position = landingPosition;
        ClearCombatHeightPresentation();
    }

    /// <summary>슬라임 여왕 계열 내려찍기 착지에 쓰는 드래곤 내려찍기와 같은 세기의 카메라 흔들림입니다.</summary>
    protected void PlayLightSlamLandingCameraShake(string debugReason)
    {
        SlamLandingCameraShake.TryPlay(
            gameObject,
            Vector3.down,
            debugReason: debugReason);
    }

    /// <summary>남아 있을 수 있는 가짜 높이를 지상 상태로 정리합니다.</summary>
    protected void ClearCombatHeightPresentation()
    {
        CombatHeightState2D heightState = combatHeightState != null
            ? combatHeightState
            : GetComponent<CombatHeightState2D>();

        if (heightState != null)
        {
            combatHeightState = heightState;
            heightState.SetGrounded();
            SnapCombatHeightPresentationToState();
            LogHeightCollisionState("grounded cleanup");
        }
    }

    /// <summary>CombatHeightState2D가 가진 현재 높이를 smoothing 없이 즉시 렌더 위치에 반영합니다.</summary>
    protected void SnapCombatHeightPresentationToState()
    {
        CombatHeightPresentation2D heightPresentation = GetComponent<CombatHeightPresentation2D>();
        heightPresentation?.SnapToCurrentState();
    }

    private static float ResolveKnightStyleSlamGroundProgress(float normalizedTime)
    {
        if (normalizedTime <= 0f)
            return 0f;

        if (normalizedTime >= KnightStyleSlamTravelNormalized)
            return 1f;

        float travelProgress = Mathf.Clamp01(normalizedTime / KnightStyleSlamTravelNormalized);
        return 1f - Mathf.Pow(1f - travelProgress, KnightStyleSlamTravelEaseOutPower);
    }

    private static float ResolveKnightStyleSlamVisualHeight(float normalizedTime, float maxHeight)
    {
        float safeHeight = Mathf.Max(0f, maxHeight);
        if (safeHeight <= 0f)
            return 0f;

        if (normalizedTime <= KnightStyleSlamTravelNormalized)
        {
            float travelProgress = Mathf.Clamp01(normalizedTime / KnightStyleSlamTravelNormalized);
            float easedProgress = 1f - Mathf.Pow(1f - travelProgress, KnightStyleSlamTravelEaseOutPower);
            return Mathf.Lerp(0f, safeHeight, easedProgress);
        }

        if (normalizedTime <= KnightStyleSlamHoldEndNormalized)
            return safeHeight;

        if (normalizedTime <= KnightStyleSlamPreDropEndNormalized)
        {
            float preDropProgress = Mathf.InverseLerp(
                KnightStyleSlamHoldEndNormalized,
                KnightStyleSlamPreDropEndNormalized,
                normalizedTime);
            return Mathf.Lerp(safeHeight, safeHeight * KnightStyleSlamPreDropHeightScale, preDropProgress);
        }

        float dropProgress = Mathf.InverseLerp(KnightStyleSlamPreDropEndNormalized, 1f, normalizedTime);
        float easedDrop = Mathf.Pow(Mathf.Clamp01(dropProgress), KnightStyleSlamLandingDropSharpness);
        return Mathf.Lerp(safeHeight * KnightStyleSlamPreDropHeightScale, 0f, easedDrop);
    }

    /// <summary>
    /// 책임:
    /// - 몸 부풀림 충격 연출 중 Visual 스케일을 일시적으로 키워 Animator 트리거만으로 부족한 팽창감을 보강한다.
    /// - 실제 collider/피해 범위는 변경하지 않고 화면에 보이는 Visual root만 다룬다.
    /// </summary>
    protected void BeginBodyInflateVisualScale()
    {
        Transform visualRoot = ResolveBodyInflateVisualRoot();
        if (visualRoot == null)
            return;

        CaptureBodyInflateVisualBaseScale(visualRoot);
        isBodyInflateVisualScaling = true;
        isBodyInflateVisualScaleReleasing = false;
        bodyInflateVisualScaleElapsed = 0f;
    }

    /// <summary>몸 부풀림 Visual 스케일 보강을 종료하고 원래 크기로 복구합니다.</summary>
    protected void EndBodyInflateVisualScale(bool resetImmediately = false)
    {
        Transform visualRoot = ResolveBodyInflateVisualRoot();
        if (visualRoot == null || !hasBodyInflateVisualBaseScale)
            return;

        if (!isBodyInflateVisualScaling && !isBodyInflateVisualScaleReleasing)
            return;

        if (resetImmediately)
        {
            visualRoot.localScale = bodyInflateVisualBaseScale;
            isBodyInflateVisualScaling = false;
            isBodyInflateVisualScaleReleasing = false;
            bodyInflateVisualScaleElapsed = 0f;
            return;
        }

        isBodyInflateVisualScaling = false;
        isBodyInflateVisualScaleReleasing = true;
        bodyInflateVisualScaleElapsed = 0f;
    }

    private void TickBodyInflateVisualScale()
    {
        Transform visualRoot = ResolveBodyInflateVisualRoot();
        if (visualRoot == null || !hasBodyInflateVisualBaseScale)
            return;

        if (!isBodyInflateVisualScaling && !isBodyInflateVisualScaleReleasing)
            return;

        float duration = isBodyInflateVisualScaling
            ? bodyInflateVisualScaleInSeconds
            : bodyInflateVisualScaleOutSeconds;
        bodyInflateVisualScaleElapsed += Time.deltaTime;

        float progress = Mathf.Clamp01(bodyInflateVisualScaleElapsed / Mathf.Max(0.01f, duration));
        float steppedProgress = QuantizeBodyInflateScaleProgress(progress);
        float eased = Mathf.SmoothStep(0f, 1f, steppedProgress);
        Vector3 inflatedScale = bodyInflateVisualBaseScale * Mathf.Max(1f, bodyInflateVisualScaleMultiplier);
        visualRoot.localScale = isBodyInflateVisualScaling
            ? Vector3.LerpUnclamped(bodyInflateVisualBaseScale, inflatedScale, eased)
            : Vector3.LerpUnclamped(inflatedScale, bodyInflateVisualBaseScale, eased);

        if (progress < 1f)
            return;

        if (isBodyInflateVisualScaleReleasing)
        {
            visualRoot.localScale = bodyInflateVisualBaseScale;
            isBodyInflateVisualScaling = false;
            isBodyInflateVisualScaleReleasing = false;
        }
    }

    private Transform ResolveBodyInflateVisualRoot()
    {
        if (bodyInflateVisualRoot != null)
            return bodyInflateVisualRoot;

        bodyInflateVisualRoot = sprite != null ? sprite.transform : transform;
        return bodyInflateVisualRoot;
    }

    private void CaptureBodyInflateVisualBaseScale(Transform visualRoot)
    {
        if (visualRoot == null || hasBodyInflateVisualBaseScale)
            return;

        bodyInflateVisualBaseScale = visualRoot.localScale;
        hasBodyInflateVisualBaseScale = true;
    }

    private float QuantizeBodyInflateScaleProgress(float progress)
    {
        int steps = Mathf.Max(0, bodyInflateVisualScaleSteps);
        if (steps <= 1)
            return progress;

        return Mathf.Clamp01(Mathf.Ceil(progress * steps) / steps);
    }

    private SpriteAfterimageEmitter2D ResolvePatternAfterimageEmitter()
    {
        if (patternAfterimageEmitter != null)
            return patternAfterimageEmitter;

        if (!TryGetComponent(out patternAfterimageEmitter))
            patternAfterimageEmitter = gameObject.AddComponent<SpriteAfterimageEmitter2D>();

        return patternAfterimageEmitter;
    }

    private bool CanUseCombatHeightPresentation()
    {
        if (combatHeightPresentation == null)
            combatHeightPresentation = GetComponent<CombatHeightPresentation2D>();

        return combatHeightPresentation != null;
    }

    private CombatHeightState2D EnsureCombatHeightState()
    {
        if (combatHeightState != null)
            return combatHeightState;

        combatHeightState = GetComponent<CombatHeightState2D>();
        if (combatHeightState != null)
            return combatHeightState;

        combatHeightState = gameObject.AddComponent<CombatHeightState2D>();
        return combatHeightState;
    }

    /// <summary>
    /// 슬라임 여왕이 기존 CombatHeight/EntityCollisionProfile 경로로 공중 actor 통과를 보장하도록 런타임 연결을 보정합니다.
    /// </summary>
    private void EnsureCombatHeightCollisionBinding()
    {
        CombatHeightState2D heightState = EnsureCombatHeightState();

        heightCollisionProfile = GetComponent<EntityCollisionProfile2D>();
        if (heightCollisionProfile == null)
            heightCollisionProfile = gameObject.AddComponent<EntityCollisionProfile2D>();

        LayerMask emptyMask = default;
        heightCollisionBodyColliders = CollectBodyColliders();
        heightCollisionProfile.Configure(
            heightCollisionBodyColliders,
            emptyMask,
            ResolveActorLayerMask(),
            EntityCollisionProfile2D.BodyCollisionMode.Normal,
            applyImmediately: false);

        heightCollisionBinder = GetComponent<CombatHeightCollisionBinder2D>();
        if (heightCollisionBinder == null)
            heightCollisionBinder = gameObject.AddComponent<CombatHeightCollisionBinder2D>();

        heightCollisionBinder.Configure(
            heightState,
            heightCollisionProfile,
            EntityCollisionProfile2D.BodyCollisionMode.Normal,
            EntityCollisionProfile2D.BodyCollisionMode.Disabled,
            restoreDefaultOnGrounded: true);

        LogHeightCollisionState("binding ensured");
    }

    private Collider2D[] CollectBodyColliders()
    {
        Collider2D[] candidates = GetComponentsInChildren<Collider2D>(true);
        int bodyCount = 0;
        for (int i = 0; i < candidates.Length; i++)
        {
            Collider2D candidate = candidates[i];
            if (candidate != null && !candidate.isTrigger)
                bodyCount++;
        }

        Collider2D[] bodyColliders = new Collider2D[bodyCount];
        int writeIndex = 0;
        for (int i = 0; i < candidates.Length; i++)
        {
            Collider2D candidate = candidates[i];
            if (candidate == null || candidate.isTrigger)
                continue;

            bodyColliders[writeIndex] = candidate;
            writeIndex++;
        }

        return bodyColliders;
    }

    private static LayerMask ResolveActorLayerMask()
    {
        LayerMask actorLayers = default;
        actorLayers.value = ResolveLayerBit(PlayerLayerName) | ResolveLayerBit(EnemyActorLayerName);
        return actorLayers;
    }

    private static int ResolveLayerBit(string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        return layer >= 0 ? 1 << layer : 0;
    }

    private void LogHeightCollisionStateThrottled(string reason)
    {
        if (!logHeightCollisionDebug || Time.time < nextHeightCollisionDebugLogTime)
            return;

        nextHeightCollisionDebugLogTime = Time.time + 0.5f;
        LogHeightCollisionState(reason);
    }

    private void LogHeightCollisionState(string reason)
    {
        if (!logHeightCollisionDebug)
            return;

        CombatHeightState2D heightState = combatHeightState != null
            ? combatHeightState
            : GetComponent<CombatHeightState2D>();

        EntityCollisionProfile2D collisionProfile = heightCollisionProfile != null
            ? heightCollisionProfile
            : GetComponent<EntityCollisionProfile2D>();

        Collider2D[] bodyColliders = heightCollisionBodyColliders;
        if (bodyColliders == null || bodyColliders.Length == 0)
            bodyColliders = CollectBodyColliders();

        string colliderSummary = BuildColliderDebugSummary(bodyColliders);
        int playerLayer = LayerMask.NameToLayer(PlayerLayerName);
        int enemyLayer = LayerMask.NameToLayer(EnemyActorLayerName);

        Debug.Log(
            $"[SlimeQueenHeightCollision] {name}: {reason}. " +
            $"heightMode={(heightState != null ? heightState.Mode.ToString() : "null")}, " +
            $"visualHeight={(heightState != null ? heightState.VisualHeight : -1f):0.00}, " +
            $"zMin={(heightState != null ? heightState.ZMin : -1f):0.00}, " +
            $"zMax={(heightState != null ? heightState.ZMax : -1f):0.00}, " +
            $"collisionMode={(collisionProfile != null ? collisionProfile.CurrentMode.ToString() : "null")}, " +
            $"playerLayer={playerLayer}, enemyLayer={enemyLayer}, " +
            $"bodyCount={(bodyColliders != null ? bodyColliders.Length : 0)}, bodies={colliderSummary}",
            this);
    }

    private static string BuildColliderDebugSummary(Collider2D[] bodyColliders)
    {
        if (bodyColliders == null || bodyColliders.Length == 0)
            return "none";

        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        for (int i = 0; i < bodyColliders.Length; i++)
        {
            Collider2D bodyCollider = bodyColliders[i];
            if (i > 0)
                builder.Append(" | ");

            if (bodyCollider == null)
            {
                builder.Append("null");
                continue;
            }

            builder
                .Append(bodyCollider.name)
                .Append("(enabled=")
                .Append(bodyCollider.enabled)
                .Append(", layer=")
                .Append(LayerMask.LayerToName(bodyCollider.gameObject.layer))
                .Append("/")
                .Append(bodyCollider.gameObject.layer)
                .Append(", trigger=")
                .Append(bodyCollider.isTrigger)
                .Append(", exclude=")
                .Append(bodyCollider.excludeLayers.value)
                .Append(")");
        }

        return builder.ToString();
    }

    private void LogAirborneCollision(string phase, Collision2D collision)
    {
        if (!logHeightCollisionDebug || collision == null)
            return;

        CombatHeightState2D heightState = combatHeightState != null
            ? combatHeightState
            : GetComponent<CombatHeightState2D>();

        if (heightState == null || !heightState.IsAirborne)
            return;

        if (Time.time < nextAirborneCollisionDebugLogTime)
            return;

        nextAirborneCollisionDebugLogTime = Time.time + 0.25f;

        Collider2D ownCollider = collision.collider;
        Collider2D otherCollider = collision.otherCollider;
        Transform otherRoot = otherCollider != null ? otherCollider.transform.root : null;
        Rigidbody2D otherRigidbody = collision.rigidbody;

        Debug.Log(
            $"[SlimeQueenAirborneCollision] {name}: {phase}. " +
            $"heightMode={heightState.Mode}, collisionMode={(heightCollisionProfile != null ? heightCollisionProfile.CurrentMode.ToString() : "null")}, " +
            $"own={FormatCollisionCollider(ownCollider)}, " +
            $"other={FormatCollisionCollider(otherCollider)}, " +
            $"otherRoot={(otherRoot != null ? otherRoot.name : "null")}, " +
            $"otherRootLayer={(otherRoot != null ? LayerMask.LayerToName(otherRoot.gameObject.layer) : "null")}/{(otherRoot != null ? otherRoot.gameObject.layer : -1)}, " +
            $"otherTag={(otherRoot != null ? otherRoot.tag : "null")}, " +
            $"otherRigidbody={(otherRigidbody != null ? otherRigidbody.name : "null")}, " +
            $"contacts={collision.contactCount}",
            this);
    }

    private static string FormatCollisionCollider(Collider2D targetCollider)
    {
        if (targetCollider == null)
            return "null";

        GameObject targetObject = targetCollider.gameObject;
        return $"{targetCollider.name}(layer={LayerMask.LayerToName(targetObject.layer)}/{targetObject.layer}, " +
            $"trigger={targetCollider.isTrigger}, enabled={targetCollider.enabled}, exclude={targetCollider.excludeLayers.value})";
    }

    /// <summary>슬라임 여왕 계열 보스가 그로기 진입 시 패턴 애니메이션 잔여 상태를 정리합니다.</summary>
    protected override void OnGroggyStateEntered()
    {
        ResetPatternAnimatorStateForInterrupt();
    }

    /// <summary>그로기/강제 취소처럼 패턴이 중단될 때 보스별 Animator 파라미터를 Idle 조건으로 되돌립니다.</summary>
    protected virtual void ResetPatternAnimatorStateForInterrupt()
    {
    }

    protected void SetAnimatorBoolIfExists(int parameterHash, bool value)
    {
        if (animator == null || !HasAnimatorParameter(parameterHash, AnimatorControllerParameterType.Bool))
            return;

        animator.SetBool(parameterHash, value);
    }

    protected void ResetAnimatorTriggerIfExists(int parameterHash)
    {
        if (animator == null || !HasAnimatorParameter(parameterHash, AnimatorControllerParameterType.Trigger))
            return;

        animator.ResetTrigger(parameterHash);
    }

    protected void PlayAnimatorStateIfExists(int stateHash)
    {
        if (animator == null || !animator.HasState(0, stateHash))
            return;

        animator.Play(stateHash, 0, 0f);
        animator.Update(0f);
    }

    protected bool HasAnimatorParameter(int parameterHash, AnimatorControllerParameterType parameterType)
    {
        if (animator == null)
            return false;

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.nameHash == parameterHash && parameter.type == parameterType)
                return true;
        }

        return false;
    }

    /// <summary>AttackTelegraphService 참조를 반환합니다.</summary>
    protected AttackTelegraphService GetTelegraphService()
    {
        if (telegraphService == null)
            telegraphService = GetComponent<AttackTelegraphService>();

        return telegraphService;
    }

    /// <summary>슬라임 여왕 계열 보스가 다른 보스처럼 공용 스태거/그로기 게이지를 사용하도록 보장합니다.</summary>
    private void EnsureGroggyGauge()
    {
        StaggerGaugeSystem staggerGauge = GetComponent<StaggerGaugeSystem>();
        if (staggerGauge == null)
            staggerGauge = gameObject.AddComponent<StaggerGaugeSystem>();

        if (staggerGauge.currentGaugeAttribute == null)
            staggerGauge.currentGaugeAttribute = FindAttributeDefinition("Stagger", "StaggerBaseAttribute");

        if (staggerGauge.maxGaugeAttribute == null)
            staggerGauge.maxGaugeAttribute = FindAttributeDefinition("MaxStaggerAttribute");

        if (staggerGauge.resistancePercentAttribute == null)
            staggerGauge.resistancePercentAttribute = FindAttributeDefinition("StaggerResistanceAttribute");

        if (staggerGauge.staggeredEffect == null)
            staggerGauge.staggeredEffect = ResolveRuntimeGroggyStatusEffect();

        staggerGauge.allowOverflow = false;
    }

    private GameplayEffect ResolveRuntimeGroggyStatusEffect()
    {
        if (runtimeGroggyStatusEffect != null)
            return runtimeGroggyStatusEffect;

        GameplayTag groggyTag = Resources.Load<GameplayTag>(GroggyTagResourcePath);
        if (groggyTag == null)
            return null;

        GE_StatusOnlyDuration groggyEffect = ScriptableObject.CreateInstance<GE_StatusOnlyDuration>();
        groggyEffect.name = "GE_SlimeQueen_RuntimeGroggyStatus";
        groggyEffect.effectName = "Groggy";
        groggyEffect.duration = DefaultGroggyDurationSeconds;
        groggyEffect.canStack = false;
        groggyEffect.maxStacks = 1;
        groggyEffect.grantedTags.Add(groggyTag);

        runtimeGroggyStatusEffect = groggyEffect;
        return runtimeGroggyStatusEffect;
    }

    private AttributeDefinition FindAttributeDefinition(params string[] attributeNames)
    {
        if (AttributeSet == null || attributeNames == null || attributeNames.Length == 0)
            return null;

        foreach (AttributeDefinition definition in AttributeSet.EnumerateDefinitions())
        {
            if (definition == null)
                continue;

            for (int i = 0; i < attributeNames.Length; i++)
            {
                string attributeName = attributeNames[i];
                if (string.IsNullOrWhiteSpace(attributeName))
                    continue;

                if (string.Equals(definition.attributeName, attributeName, StringComparison.Ordinal) ||
                    string.Equals(definition.name, attributeName, StringComparison.Ordinal))
                    return definition;
            }
        }

        return null;
    }

    /// <summary>충돌한 콜라이더의 계층에 Player 태그가 있는지 확인합니다.</summary>
    protected bool HasPlayerTagInHierarchy(Transform hitTransform)
    {
        Transform current = hitTransform;
        while (current != null)
        {
            if (current.CompareTag("Player"))
                return true;

            current = current.parent;
        }

        return false;
    }

    /// <summary>구덩이 낙하 시작 시 보스별 특수 상태를 기록하는 선택적 훅입니다.</summary>
    protected virtual void OnPitFallStarted(PitFallContext context)
    {
    }

    /// <summary>구덩이 낙하 완료 후 보스별 후속 패턴을 실행하는 선택적 훅입니다.</summary>
    protected virtual void OnPitFallCompleted(PitFallContext context)
    {
    }
}
