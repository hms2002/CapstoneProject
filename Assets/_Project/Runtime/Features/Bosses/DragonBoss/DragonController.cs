using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// 취룡 보스 전용 패턴 실행에 필요한 런타임 데이터, 연출 키, 공통 보조 기능을 기존 보스 FSM 위에 제공한다.
/// </summary>
public sealed class DragonController : BossControllerBase
{
    [Header("Dragon")]
    [SerializeField] private Transform arenaCenterPoint;
    [SerializeField] private DragonArenaBounds2D arenaBounds;
    [SerializeField] private Transform patternMotionRoot;
    [SerializeField] private Transform patternShadowMotionRoot;
    [SerializeField] private MirroredLocalSocket2D fireBreathMouthSocket;
    [SerializeField] private bool faceTargetDuringCombat = true;

    [Header("Jump Afterimage")]
    [Tooltip("점프형 패턴 중 본체 잔상을 남길지 여부입니다.")]
    [SerializeField] private bool enableJumpAfterimage = true;
    [Tooltip("점프 잔상 스냅샷을 생성하는 간격입니다.")]
    [SerializeField, Min(0.01f)] private float jumpAfterimageEmissionInterval = 0.045f;
    [Tooltip("각 점프 잔상 스냅샷이 사라질 때까지 걸리는 시간입니다.")]
    [SerializeField, Min(0.01f)] private float jumpAfterimageLifetimeSeconds = 0.18f;
    [Tooltip("점프 잔상에 입힐 색과 투명도입니다.")]
    [SerializeField] private Color jumpAfterimageColor = new(1f, 0.72f, 0.45f, 0.36f);

    [Header("Puddle Reactive Pattern Weight")]
    [SerializeField] private bool scaleFireBreathWeightByPuddles = true;
    [SerializeField, Min(0)] private int fireBreathMinimumPuddlesToUse = 4;
    [SerializeField, Min(0.01f)] private float fireBreathMinimumPuddleWeightMultiplier = 1f;
    [SerializeField, Min(1)] private int fireBreathPuddlesForMaxWeight = 5;
    [SerializeField, Min(0.01f)] private float fireBreathMaxPuddleWeightMultiplier = 10f;

    [Header("Demo Pattern")]
    [SerializeField] private bool forceFirstSlamPatternForDemo = true;

    private DragonRuntimeData runtimeData;
    private int faceTargetLockCount;
    private SpriteAfterimageEmitter2D jumpAfterimageEmitter;
    private bool hasForcedFirstSlamPatternForDemo;

    public DragonRuntimeData RuntimeData
    {
        get
        {
            runtimeData ??= new DragonRuntimeData();
            return runtimeData;
        }
    }

    public Vector3 ArenaCenterPosition => arenaCenterPoint != null ? arenaCenterPoint.position : transform.position;
    public Transform BodyVisualRoot => sprite != null ? sprite.transform : transform;
    public Transform PatternMotionRoot => patternMotionRoot != null ? patternMotionRoot : BodyVisualRoot;
    public Transform PatternShadowMotionRoot => patternShadowMotionRoot;
    public DragonArenaBounds2D ArenaBounds => ResolveArenaBounds();

    protected override void Awake()
    {
        base.Awake();
        runtimeData = new DragonRuntimeData();
        hasForcedFirstSlamPatternForDemo = false;
    }

    protected override void Update()
    {
        base.Update();

        if (faceTargetDuringCombat && faceTargetLockCount <= 0 && CanAutoFaceTarget())
            FaceCurrentTarget();
    }

    protected override void OnPatternEnd(BossPatternEntry patternEntry, bool forced)
    {
        RuntimeData.ResetPatternCounters();
        StopJumpAfterimage(clearGhosts: forced);
    }

    public override BossPatternEntry SelectNextPattern()
    {
        if (forceFirstSlamPatternForDemo && !hasForcedFirstSlamPatternForDemo)
        {
            BossPatternEntry forcedPattern = TrySelectFirstSlamPatternForDemo();
            if (forcedPattern != null)
            {
                hasForcedFirstSlamPatternForDemo = true;
                return forcedPattern;
            }
        }

        return base.SelectNextPattern();
    }

    private BossPatternEntry TrySelectFirstSlamPatternForDemo()
    {
        if (Target == null)
            TryRefreshTarget(logWarning: false);

        if (CombatTargetDeathUtility.IsPlayerDeathSequenceRunning(Target))
            return null;

        Blackboard?.Tick(0f, Target, CurrentHealthRatio);
        BossPhaseConfig currentPhase = GetCurrentPhase();
        System.Collections.Generic.IReadOnlyList<BossPatternEntry> patterns = currentPhase != null ? currentPhase.Patterns : null;
        if (patterns == null)
            return null;

        for (int i = 0; i < patterns.Count; i++)
        {
            BossPatternEntry pattern = patterns[i];
            if (!IsSlamPattern(pattern))
                continue;

            BossPatternEvalResult result = EvaluatePattern(pattern);
            if (result.CanUse)
                return pattern;
        }

        return null;
    }

    /// <summary>
    /// 책임:
    /// 취룡 전용 환경 요소를 기준으로 공통 보스 패턴 평가 결과의 가중치만 보정한다.
    /// </summary>
    protected override BossPatternEvalResult AdjustPatternEval(BossPatternEntry patternEntry, BossPatternEvalResult result)
    {
        if (!result.CanUse || !scaleFireBreathWeightByPuddles || !IsFireBreathPattern(patternEntry))
            return result;

        int activePuddleCount = CountActiveAlcoholOrFirePuddles();
        if (activePuddleCount < fireBreathMinimumPuddlesToUse)
            return BossPatternEvalResult.HardFail(
                $"Dragon fire breath blocked because active puddles are below requirement: {activePuddleCount}/{fireBreathMinimumPuddlesToUse}.");

        float multiplier = ResolveFireBreathPuddleWeightMultiplier(activePuddleCount);

        return new BossPatternEvalResult(
            result.State,
            result.WeightMultiplier * multiplier,
            $"Dragon fire breath weight scaled by active puddles: {activePuddleCount}.");
    }

    private float ResolveFireBreathPuddleWeightMultiplier(int activePuddleCount)
    {
        if (activePuddleCount < Mathf.Max(1, fireBreathPuddlesForMaxWeight))
            return Mathf.Max(0.01f, fireBreathMinimumPuddleWeightMultiplier);

        return Mathf.Max(0.01f, fireBreathMaxPuddleWeightMultiplier);
    }

    /// <summary>취룡의 현재 타겟 또는 바라보는 방향을 기준으로 패턴 방향을 구한다.</summary>
    public Vector2 GetDirectionToTargetOrFacing(Vector3? fromPosition = null)
    {
        Vector3 origin = fromPosition ?? transform.position;
        if (Target != null)
        {
            Vector2 toTarget = (Vector2)(Target.position - origin);
            if (toTarget.sqrMagnitude > 0.0001f)
                return toTarget.normalized;
        }

        return sprite != null && sprite.flipX ? Vector2.left : Vector2.right;
    }

    /// <summary>취룡 패턴용 Animator trigger를 안전하게 호출한다.</summary>
    public void PlayPatternTrigger(string triggerName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(triggerName))
            return;

        if (ShouldSuppressPatternTrigger(triggerName))
            return;

        animator.SetTrigger(triggerName);
    }

    /// <summary>
    /// 책임:
    /// 취소된 패턴의 finally가 idle 트리거를 뒤늦게 호출해 그로기/사망 애니메이션을 덮어쓰지 못하게 막는다.
    /// </summary>
    private bool ShouldSuppressPatternTrigger(string triggerName)
    {
        bool isReactiveTrigger = triggerName == DragonAnimationKeys.Groggy ||
                                 triggerName == DragonAnimationKeys.Recover ||
                                 triggerName == DragonAnimationKeys.Dead;
        if (isReactiveTrigger)
            return false;

        return HasGroggyTag() || HasDeadTag();
    }

    /// <summary>
    /// 책임:
    /// 취룡이 그로기/사망/패턴 방향 고정 중일 때 자동 좌우 반전이 상태 연출을 흔들지 않도록 허용 여부를 판단한다.
    /// </summary>
    private bool CanAutoFaceTarget()
    {
        return !HasGroggyTag() && !HasDeadTag();
    }

    private static bool IsFireBreathPattern(BossPatternEntry patternEntry)
    {
        return patternEntry != null &&
               patternEntry.Ability != null &&
               patternEntry.Ability.logic is AbilityLogic_DragonFireBreath;
    }

    private static bool IsSlamPattern(BossPatternEntry patternEntry)
    {
        return patternEntry != null &&
               patternEntry.Ability != null &&
               patternEntry.Ability.logic is AbilityLogic_DragonSlam;
    }

    private static int CountActiveAlcoholOrFirePuddles()
    {
        PuddleManager manager = PuddleManager.ResolveForScene();
        System.Collections.Generic.IReadOnlyList<PuddleAreaBase> puddles = manager != null ? manager.Puddles : null;
        if (puddles == null || puddles.Count == 0)
            return 0;

        int count = 0;
        for (int i = 0; i < puddles.Count; i++)
        {
            PuddleAreaBase puddle = puddles[i];
            if (puddle == null || !puddle.IsGroundActive)
                continue;

            if (puddle.ElementType == PuddleElementType.Alcohol ||
                puddle.ElementType == PuddleElementType.Fire)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// 책임:
    /// 취룡이 그로기 FSM 상태에 들어간 순간 현재 패턴 애니메이션을 그로기 애니메이션으로 전환한다.
    /// </summary>
    protected override void OnGroggyStateEntered()
    {
        SpeakSituation(BossSpeechSituationEnum.GroggyStart);
        PlayPatternTrigger(DragonAnimationKeys.Groggy);
    }

    /// <summary>
    /// 책임:
    /// 취룡의 그로기 태그가 사라져 전투로 복귀할 때 회복 애니메이션 진입을 요청한다.
    /// </summary>
    protected override void OnGroggyStateExited()
    {
        PlayPatternTrigger(DragonAnimationKeys.Recover);
    }

    /// <summary>화염 방사 연출/판정이 시작될 입 위치를 계산한다.</summary>
    public Vector2 ResolveFireBreathMouthPosition(Vector2 direction, float fallbackForwardOffset)
    {
        if (fireBreathMouthSocket != null)
            return fireBreathMouthSocket.WorldPosition;

        return (Vector2)transform.position + (direction.normalized * fallbackForwardOffset);
    }

    /// <summary>취룡 전투 공간에서 무작위 유효 지점을 요청한다.</summary>
    public bool TryGetRandomArenaPoint(out Vector2 point)
    {
        point = default;
        DragonArenaBounds2D bounds = ResolveArenaBounds();
        return bounds != null && bounds.TryGetRandomPoint(out point);
    }

    /// <summary>취룡 전투 공간에서 지정한 재시도 횟수로 무작위 유효 지점을 요청한다.</summary>
    public bool TryGetRandomArenaPoint(int maxAttempts, out Vector2 point)
    {
        point = default;
        DragonArenaBounds2D bounds = ResolveArenaBounds();
        return bounds != null && bounds.TryGetRandomPoint(maxAttempts, out point);
    }

    /// <summary>취룡 전투 공간에서 특정 지점과 최소 거리 이상 떨어진 무작위 유효 지점을 요청한다.</summary>
    public bool TryGetRandomArenaPointAwayFrom(Vector2 avoidPoint, float minDistance, int maxAttempts, out Vector2 point)
    {
        point = default;
        DragonArenaBounds2D bounds = ResolveArenaBounds();
        return bounds != null && bounds.TryGetRandomPointAwayFrom(avoidPoint, minDistance, maxAttempts, out point);
    }

    /// <summary>패턴 연출 중 자동 좌우 반전이 공격 방향을 덮어쓰지 못하도록 잠근다.</summary>
    public void PushFaceTargetLock()
    {
        faceTargetLockCount++;
    }

    /// <summary>패턴 연출이 끝났을 때 자동 좌우 반전 잠금을 해제한다.</summary>
    public void PopFaceTargetLock()
    {
        faceTargetLockCount = Mathf.Max(0, faceTargetLockCount - 1);
    }

    /// <summary>패턴이 확정한 공격 방향과 스프라이트 좌우 반전을 즉시 동기화한다.</summary>
    public void FacePatternDirection(Vector2 direction)
    {
        if (sprite == null || Mathf.Abs(direction.x) <= 0.0001f)
            return;

        sprite.flipX = direction.x < 0f;
    }

    /// <summary>
    /// 책임:
    /// 취룡 점프형 패턴에서 Visual 기준 잔상 방출을 시작한다.
    /// </summary>
    public void BeginJumpAfterimage()
    {
        if (!enableJumpAfterimage || !isActiveAndEnabled)
            return;

        SpriteAfterimageEmitter2D emitter = ResolveJumpAfterimageEmitter();
        if (emitter == null)
            return;

        Transform sourceRoot = BodyVisualRoot != null ? BodyVisualRoot : transform;
        emitter.Begin(
            sourceRoot,
            jumpAfterimageEmissionInterval,
            jumpAfterimageLifetimeSeconds,
            jumpAfterimageColor);
    }

    /// <summary>
    /// 책임:
    /// 취룡 점프형 패턴의 잔상 생성을 멈추고, 강제 종료 상황에서는 이미 생성된 잔상까지 정리한다.
    /// </summary>
    public void StopJumpAfterimage(bool clearGhosts = false)
    {
        if (jumpAfterimageEmitter == null)
            return;

        jumpAfterimageEmitter.StopEmission();
        if (clearGhosts)
            jumpAfterimageEmitter.ClearSpawnedGhosts();
    }

    /// <summary>취룡 보스가 흡수 패턴을 시작할 때 이전 결과를 정리한다.</summary>
    public void BeginAbsorbPatternTracking()
    {
        RuntimeData.BeginAbsorbPattern();
    }

    /// <summary>장판 탄막이 보스에게 도착했을 때 흡수 결과를 기록한다.</summary>
    public void RecordAbsorbedPuddleProjectile(PuddleElementType elementType)
    {
        if (elementType == PuddleElementType.Alcohol)
        {
            RuntimeData.RecordAlcoholProjectileAbsorbed();
            return;
        }

        if (elementType == PuddleElementType.Fire)
            RuntimeData.RecordFireProjectileAbsorbed();
    }

    /// <summary>술 탄막 흡수 보상처럼 취룡의 스태거 누적치를 최대 게이지 비율 기준으로 회복한다.</summary>
    public float RecoverStaggerBuildUpByMaxRatio(float ratio)
    {
        StaggerGaugeSystem staggerGaugeSystem = GetComponent<StaggerGaugeSystem>();
        return staggerGaugeSystem != null
            ? staggerGaugeSystem.ReduceBuildUpByMaxRatio(ratio)
            : 0f;
    }

    /// <summary>불 탄막 흡수 페널티처럼 취룡의 스태거 누적치를 최대 게이지 비율 기준으로 증가시킨다.</summary>
    public float AddStaggerBuildUpByMaxRatio(float ratio)
    {
        StaggerGaugeSystem staggerGaugeSystem = GetComponent<StaggerGaugeSystem>();
        if (staggerGaugeSystem == null)
            return 0f;

        AttributeSet attributes = GetComponent<AttributeSet>();
        if (attributes == null || staggerGaugeSystem.currentGaugeAttribute == null || staggerGaugeSystem.maxGaugeAttribute == null)
            return 0f;

        float before = attributes.GetAttributeValue(staggerGaugeSystem.currentGaugeAttribute);
        float max = Mathf.Max(0f, attributes.GetAttributeValue(staggerGaugeSystem.maxGaugeAttribute));
        if (max <= 0f)
            return 0f;

        staggerGaugeSystem.AddBuildUp(max * Mathf.Clamp01(ratio), gameObject, gameObject);
        float after = attributes.GetAttributeValue(staggerGaugeSystem.currentGaugeAttribute);
        return Mathf.Max(0f, after - before);
    }

    private void FaceCurrentTarget()
    {
        if (Target == null || sprite == null)
            return;

        if (transform.position.x > Target.position.x)
            sprite.flipX = true;
        else if (transform.position.x < Target.position.x)
            sprite.flipX = false;
    }

    private SpriteAfterimageEmitter2D ResolveJumpAfterimageEmitter()
    {
        if (jumpAfterimageEmitter != null)
            return jumpAfterimageEmitter;

        if (!TryGetComponent(out jumpAfterimageEmitter))
            jumpAfterimageEmitter = gameObject.AddComponent<SpriteAfterimageEmitter2D>();

        return jumpAfterimageEmitter;
    }

    private DragonArenaBounds2D ResolveArenaBounds()
    {
        if (arenaBounds != null)
            return arenaBounds;

        arenaBounds = GetComponentInParent<DragonArenaBounds2D>();
        if (arenaBounds != null)
            return arenaBounds;

        arenaBounds = GetComponentInChildren<DragonArenaBounds2D>();
        if (arenaBounds != null)
            return arenaBounds;

        arenaBounds = FindAnyObjectByType<DragonArenaBounds2D>();
        return arenaBounds;
    }
}
