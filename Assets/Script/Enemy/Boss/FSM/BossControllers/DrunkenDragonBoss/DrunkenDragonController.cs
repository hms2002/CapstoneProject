using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// 취룡 보스 전용 패턴 실행에 필요한 런타임 데이터, 연출 키, 공통 보조 기능을 기존 보스 FSM 위에 제공한다.
/// </summary>
public sealed class DrunkenDragonController : BossControllerBase
{
    [Header("Drunken Dragon")]
    [SerializeField] private Transform arenaCenterPoint;
    [SerializeField] private DrunkenDragonArenaBounds2D arenaBounds;
    [SerializeField] private Transform patternMotionRoot;
    [SerializeField] private Transform patternShadowMotionRoot;
    [SerializeField] private MirroredLocalSocket2D fireBreathMouthSocket;
    [SerializeField] private bool faceTargetDuringCombat = true;

    private DrunkenDragonRuntimeData runtimeData;
    private int faceTargetLockCount;

    public DrunkenDragonRuntimeData RuntimeData
    {
        get
        {
            runtimeData ??= new DrunkenDragonRuntimeData();
            return runtimeData;
        }
    }

    public Vector3 ArenaCenterPosition => arenaCenterPoint != null ? arenaCenterPoint.position : transform.position;
    public Transform BodyVisualRoot => sprite != null ? sprite.transform : transform;
    public Transform PatternMotionRoot => patternMotionRoot != null ? patternMotionRoot : BodyVisualRoot;
    public Transform PatternShadowMotionRoot => patternShadowMotionRoot;
    public DrunkenDragonArenaBounds2D ArenaBounds => ResolveArenaBounds();

    protected override void Awake()
    {
        base.Awake();
        runtimeData = new DrunkenDragonRuntimeData();
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
        bool isReactiveTrigger = triggerName == DrunkenDragonAnimationKeys.Groggy ||
                                 triggerName == DrunkenDragonAnimationKeys.Recover ||
                                 triggerName == DrunkenDragonAnimationKeys.Dead;
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

    /// <summary>
    /// 책임:
    /// 취룡이 그로기 FSM 상태에 들어간 순간 현재 패턴 애니메이션을 그로기 애니메이션으로 전환한다.
    /// </summary>
    protected override void OnGroggyStateEntered()
    {
        SpeakSituation(BossSpeechSituationEnum.GroggyStart);
        PlayPatternTrigger(DrunkenDragonAnimationKeys.Groggy);
    }

    /// <summary>
    /// 책임:
    /// 취룡의 그로기 태그가 사라져 전투로 복귀할 때 회복 애니메이션 진입을 요청한다.
    /// </summary>
    protected override void OnGroggyStateExited()
    {
        PlayPatternTrigger(DrunkenDragonAnimationKeys.Recover);
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
        DrunkenDragonArenaBounds2D bounds = ResolveArenaBounds();
        return bounds != null && bounds.TryGetRandomPoint(out point);
    }

    /// <summary>취룡 전투 공간에서 지정한 재시도 횟수로 무작위 유효 지점을 요청한다.</summary>
    public bool TryGetRandomArenaPoint(int maxAttempts, out Vector2 point)
    {
        point = default;
        DrunkenDragonArenaBounds2D bounds = ResolveArenaBounds();
        return bounds != null && bounds.TryGetRandomPoint(maxAttempts, out point);
    }

    /// <summary>취룡 전투 공간에서 특정 지점과 최소 거리 이상 떨어진 무작위 유효 지점을 요청한다.</summary>
    public bool TryGetRandomArenaPointAwayFrom(Vector2 avoidPoint, float minDistance, int maxAttempts, out Vector2 point)
    {
        point = default;
        DrunkenDragonArenaBounds2D bounds = ResolveArenaBounds();
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

    private DrunkenDragonArenaBounds2D ResolveArenaBounds()
    {
        if (arenaBounds != null)
            return arenaBounds;

        arenaBounds = GetComponentInParent<DrunkenDragonArenaBounds2D>();
        if (arenaBounds != null)
            return arenaBounds;

        arenaBounds = GetComponentInChildren<DrunkenDragonArenaBounds2D>();
        if (arenaBounds != null)
            return arenaBounds;

        arenaBounds = FindAnyObjectByType<DrunkenDragonArenaBounds2D>();
        return arenaBounds;
    }
}
