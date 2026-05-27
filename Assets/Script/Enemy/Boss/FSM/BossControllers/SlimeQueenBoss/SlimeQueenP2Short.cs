using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 슬라임 여왕 2페이즈 근거리 퀸 컨트롤러입니다.
/// </summary>
public sealed class SlimeQueenP2Short : SlimeQueenPhaseTwoBase
{
    private static readonly int IsRushingHash = Animator.StringToHash("isRushing");
    private static readonly int IsSinkingHash = Animator.StringToHash("isSinking");
    private static readonly int ReadyTriggerHash = Animator.StringToHash("ready");
    private static readonly int IsGiantizationHash = Animator.StringToHash("isGiantization");
    private static readonly int IdleStateHash = Animator.StringToHash("SlimeQueenB_Idle");
    private const string ToxicRushPitFallSlamSpeechText = "아야야… 감히 피하다니!";
    private const float ToxicRushPitFallSlamSpeechSeconds = 1.2f;
    private const float ToxicRushWallSearchDistance = 1000f;

    [Header("Phase 2 Short - Toxic Rush")]
    [Tooltip("독성 돌진 경고선 표시에 사용할 AttackTelegraph 스타일입니다.")]
    [SerializeField] private AttackTelegraphStyle toxicRushWarningStyle;

    [Tooltip("독성 돌진 경고선이 유지되는 시간입니다.")]
    [SerializeField, Min(0f)] private float toxicRushWarningSeconds = 1.4f;

    [Tooltip("독성 돌진에서 조준과 돌진을 반복할 횟수입니다.")]
    [SerializeField, Min(1)] private int toxicRushRepeatCount = 3;

    [Tooltip("독성 돌진 이동 속도입니다.")]
    [SerializeField, Min(0.1f)] private float toxicRushSpeed = 9f;

    [Tooltip("벽 검출에 실패했을 때 독성 돌진이 무한 지속되지 않게 막는 안전 이동 거리입니다.")]
    [SerializeField, Min(0.1f)] private float toxicRushDistance = 7f;

    [Tooltip("독성 돌진 경고선의 폭입니다.")]
    [SerializeField, Min(0.05f)] private float toxicRushWarningWidth = 2.2f;

    [Tooltip("독성 돌진이 충돌 종료 조건으로 검사할 벽 레이어입니다.")]
    [SerializeField] private LayerMask toxicRushWallLayers = 1 << 30;

    [Tooltip("벽과 겹치지 않게 돌진 종료점을 안쪽으로 당길 거리입니다.")]
    [SerializeField, Min(0f)] private float toxicRushWallStopPadding = 0.1f;

    [Tooltip("각 독성 돌진 사이의 짧은 대기 시간입니다.")]
    [SerializeField, Min(0f)] private float toxicRushIntervalSeconds = 0.15f;

    [Space(8)]

    [Header("Phase 2 Short - Poison Cloud Trail")]
    [Tooltip("독성 돌진 경로에 남길 독구름 프리팹입니다.")]
    [SerializeField] private PoisonCloudArea poisonCloudPrefab;

    [Tooltip("독구름을 경로에 배치하는 간격입니다.")]
    [SerializeField, Min(0.05f)] private float poisonCloudSpawnSpacing = 1.4f;

    [Tooltip("생성된 독구름 피해 판정 반지름입니다.")]
    [SerializeField, Min(0.05f)] private float poisonCloudRadius = 0.75f;

    [Tooltip("생성된 독구름이 피해를 줄 수 있는 활성 시간입니다.")]
    [SerializeField, Min(0f)] private float poisonCloudActiveSeconds = 4f;

    [Tooltip("활성 시간이 끝난 뒤 피해 없이 투명해지는 시간입니다.")]
    [SerializeField, Min(0f)] private float poisonCloudFadeSeconds = 1f;

    [Tooltip("독구름이 플레이어에게 주는 피해량입니다.")]
    [SerializeField, Min(0f)] private float poisonCloudDamage = 1f;

    [Tooltip("독구름 반복 피해 간격입니다.")]
    [SerializeField, Min(0.05f)] private float poisonCloudDamageIntervalSeconds = 1f;

    [Tooltip("독구름 피해에 사용할 GAS Damage Effect입니다. 비우면 프리팹 기본값을 사용합니다.")]
    [SerializeField] private GE_Damage_Spec poisonCloudDamageEffect;

    private readonly List<AttackTelegraphView> toxicRushWarningViews = new List<AttackTelegraphView>();
    private Coroutine toxicRushPitFallSlamRoutine;
    private Vector3 lastPoisonCloudSpawnPosition;
    private bool hasLastPoisonCloudSpawnPosition;
    private bool isToxicRushActive;
    private bool shouldRunToxicRushPitFallSlam;
    private bool isToxicRushPitFallSlamLocked;

    public int ToxicRushRepeatCount => Mathf.Max(1, toxicRushRepeatCount);
    public float ToxicRushWarningSeconds => Mathf.Max(0f, toxicRushWarningSeconds);
    public float ToxicRushSpeed => Mathf.Max(0.1f, toxicRushSpeed);
    public float ToxicRushIntervalSeconds => Mathf.Max(0f, toxicRushIntervalSeconds);

    public void BeginToxicRushAnimation()
    {
        isToxicRushActive = true;
        SetAnimatorBool(IsRushingHash, true);
    }

    public void EndToxicRushAnimation()
    {
        isToxicRushActive = false;
        SetAnimatorBool(IsRushingHash, false);
    }

    public override void BeginDrainSinkAnimation()
    {
        SetAnimatorBool(IsSinkingHash, true);
    }

    public override void EndDrainSinkAnimation()
    {
        SetAnimatorBool(IsSinkingHash, false);
    }

    public void TriggerBodyInflateReadyAnimation()
    {
        if (animator == null)
            return;

        animator.ResetTrigger(ReadyTriggerHash);
        animator.SetTrigger(ReadyTriggerHash);
    }

    public void ResetBodyInflateReadyAnimation()
    {
        if (animator == null)
            return;

        animator.ResetTrigger(ReadyTriggerHash);
    }

    public void BeginBodyInflateImpactAnimation()
    {
        SetAnimatorBool(IsGiantizationHash, true);
    }

    public void EndBodyInflateImpactAnimation()
    {
        SetAnimatorBool(IsGiantizationHash, false);
    }

    protected override void ResetPatternAnimatorStateForInterrupt()
    {
        isToxicRushActive = false;
        SetAnimatorBoolIfExists(IsRushingHash, false);
        ResetAnimatorTriggerIfExists(ReadyTriggerHash);
        SetAnimatorBoolIfExists(IsGiantizationHash, false);

        if (!HasGroggyTag())
            SetAnimatorBoolIfExists(IsSinkingHash, false);

        PlayAnimatorStateIfExists(IdleStateHash);
    }

    public readonly struct ToxicRushSegment
    {
        public readonly Vector3 Start;
        public readonly Vector3 End;
        public readonly Vector2 Direction;
        public readonly float Length;
        public readonly float RotationDegrees;

        public Vector3 Center => (Start + End) * 0.5f;
        public bool IsValid => Length > 0.05f;

        public ToxicRushSegment(Vector3 start, Vector3 end, Vector2 direction)
        {
            Start = start;
            End = end;
            Direction = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.right;
            Length = Vector3.Distance(start, end);
            RotationDegrees = Mathf.Atan2(Direction.y, Direction.x) * Mathf.Rad2Deg;
        }
    }

    protected override void OnDestroy()
    {
        StopToxicRushPitFallSlam();
        CleanupToxicRushPresentation();
        base.OnDestroy();
    }

    protected override void OnDisable()
    {
        StopToxicRushPitFallSlam();
        base.OnDisable();
    }

    /// <summary>현재 타겟 방향을 기준으로 벽에 닿을 때까지 이어지는 독성 돌진 경로를 계산합니다.</summary>
    public bool TryBuildToxicRushSegment(GameObject explicitTarget, out ToxicRushSegment segment)
    {
        Transform targetTransform = explicitTarget != null ? explicitTarget.transform : CurrentTarget;
        if (targetTransform == null)
        {
            segment = default;
            return false;
        }

        Vector3 startPosition = transform.position;
        Vector3 targetPosition = targetTransform.position;
        targetPosition.z = startPosition.z;

        Vector2 direction = targetPosition - startPosition;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = sprite != null && sprite.flipX ? Vector2.left : Vector2.right;
        else
            direction.Normalize();

        float rushDistance = ResolveWallTerminatedRushDistance(startPosition, direction);
        Vector3 endPosition = startPosition + (Vector3)(direction * rushDistance);
        endPosition.z = startPosition.z;

        segment = new ToxicRushSegment(startPosition, endPosition, direction);
        return segment.IsValid;
    }

    /// <summary>독성 돌진 경고선을 표시하고 런타임 정리 목록에 보관합니다.</summary>
    public void ShowToxicRushWarning(ToxicRushSegment segment)
    {
        if (!segment.IsValid)
            return;

        AttackTelegraphService service = GetTelegraphService();
        if (service == null)
            return;

        AttackTelegraphSpec spec = WithThinWarningOutline(AttackTelegraphSpec.CreateRectangle(
            segment.Center,
            new Vector2(segment.Length, Mathf.Max(0.05f, toxicRushWarningWidth)),
            segment.RotationDegrees,
            ToxicRushWarningSeconds,
            toxicRushWarningStyle));

        AttackTelegraphView view = service.SpawnDetachedView(spec);
        if (view != null)
            toxicRushWarningViews.Add(view);
    }

    /// <summary>독성 돌진 경고선을 즉시 제거합니다.</summary>
    public void ClearToxicRushWarnings()
    {
        ClearViews(toxicRushWarningViews);
    }

    /// <summary>독성 돌진 진행 거리만큼 보스를 이동시키고 독구름 트레일을 갱신합니다.</summary>
    public void SetToxicRushPose(ToxicRushSegment segment, float traveledDistance)
    {
        if (!segment.IsValid)
            return;

        if (movementMotor != null)
            movementMotor.StopAllMotion();

        float clampedDistance = Mathf.Clamp(traveledDistance, 0f, segment.Length);
        transform.position = segment.Start + (Vector3)(segment.Direction * clampedDistance);
        SpawnPoisonCloudTrailIfNeeded(transform.position);
    }

    /// <summary>독성 돌진 시작점에 독구름을 배치하고 트레일 기록을 초기화합니다.</summary>
    public void BeginToxicRushTrail(Vector3 startPosition)
    {
        hasLastPoisonCloudSpawnPosition = false;
        SpawnPoisonCloudAt(startPosition);
    }

    /// <summary>독성 돌진 종료 위치를 확정하고 트레일 기록을 비웁니다.</summary>
    public void FinishToxicRushSegment(ToxicRushSegment segment)
    {
        if (movementMotor != null)
            movementMotor.StopAllMotion();

        if (segment.IsValid)
        {
            transform.position = segment.End;
            SpawnPoisonCloudTrailIfNeeded(segment.End);
        }

        hasLastPoisonCloudSpawnPosition = false;
    }

    /// <summary>플레이어 충돌로 독성 돌진이 중단된 현재 위치를 종료점으로 정리합니다.</summary>
    public void FinishToxicRushAtCurrentPosition()
    {
        if (movementMotor != null)
            movementMotor.StopAllMotion();

        SpawnPoisonCloudTrailIfNeeded(transform.position);
        hasLastPoisonCloudSpawnPosition = false;
    }

    /// <summary>독성 돌진 중 플레이어와 겹쳤는지 확인합니다.</summary>
    public bool HasToxicRushHitPlayer()
    {
        float hitRadius = Mathf.Max(0.05f, toxicRushWarningWidth * 0.5f);
        if (CurrentTarget != null)
        {
            float sqrDistance = ((Vector2)(CurrentTarget.position - transform.position)).sqrMagnitude;
            if (sqrDistance <= hitRadius * hitRadius)
                return true;
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, hitRadius);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || !HasPlayerTagInHierarchy(hit.transform))
                continue;

            GameObject damageTarget = CombatTargetResolver2D.ResolveDamageTarget(hit);
            if (damageTarget == null)
                return true;

            if (damageTarget.CompareTag("Player"))
                return true;
        }

        return false;
    }

    /// <summary>독성 돌진이 남긴 경고 표시를 정리합니다.</summary>
    public void CleanupToxicRushPresentation()
    {
        ClearToxicRushWarnings();
    }

    private void SetAnimatorBool(int parameterHash, bool value)
    {
        if (animator == null)
            return;

        animator.SetBool(parameterHash, value);
    }

    protected override void OnPitFallStarted(PitFallContext context)
    {
        if (isToxicRushActive)
            shouldRunToxicRushPitFallSlam = true;
    }

    protected override void OnPitFallCompleted(PitFallContext context)
    {
        if (!shouldRunToxicRushPitFallSlam)
            return;

        shouldRunToxicRushPitFallSlam = false;
        if (!CanStartToxicRushPitFallSlam())
            return;

        StartToxicRushPitFallSlam(context.RespawnPosition);
    }

    /// <summary>독성 돌진의 벽 충돌 종료 지점을 긴 거리 cast로 계산합니다.</summary>
    private float ResolveWallTerminatedRushDistance(Vector3 startPosition, Vector2 direction)
    {
        float maxSearchDistance = Mathf.Max(ToxicRushWallSearchDistance, toxicRushDistance);
        if (toxicRushWallLayers.value == 0)
            return maxSearchDistance;

        RaycastHit2D hit = Physics2D.CircleCast(
            startPosition,
            Mathf.Max(0.05f, toxicRushWarningWidth * 0.5f),
            direction,
            maxSearchDistance,
            toxicRushWallLayers.value);

        if (hit.collider != null)
            return Mathf.Max(0.05f, hit.distance - toxicRushWallStopPadding);

        return maxSearchDistance;
    }

    private void StartToxicRushPitFallSlam(Vector3 landingPosition)
    {
        if (toxicRushPitFallSlamRoutine != null)
            StopCoroutine(toxicRushPitFallSlamRoutine);

        toxicRushPitFallSlamRoutine = StartCoroutine(ToxicRushPitFallSlamRoutine(landingPosition));
    }

    private void StopToxicRushPitFallSlam()
    {
        if (toxicRushPitFallSlamRoutine != null)
        {
            StopCoroutine(toxicRushPitFallSlamRoutine);
            toxicRushPitFallSlamRoutine = null;
        }

        EndToxicRushPitFallSlamLock();
        shouldRunToxicRushPitFallSlam = false;
    }

    private IEnumerator ToxicRushPitFallSlamRoutine(Vector3 landingPosition)
    {
        BeginToxicRushPitFallSlamLock();
        landingPosition.z = transform.position.z;

        try
        {
            TryShowPhaseTwoSpeech(ToxicRushPitFallSlamSpeechText, ToxicRushPitFallSlamSpeechSeconds);
            ShowPhase2SlamWarning(landingPosition);

            Vector3 startPosition = transform.position;
            startPosition.z = landingPosition.z;

            float elapsedSeconds = 0f;
            while (elapsedSeconds < Phase2SlamIntervalSeconds)
            {
                if (!CanContinueToxicRushPitFallSlam())
                    yield break;

                elapsedSeconds += Time.deltaTime;
                float normalizedTime = Mathf.Clamp01(elapsedSeconds / Phase2SlamIntervalSeconds);
                SetPhase2SlamPose(startPosition, landingPosition, normalizedTime);
                yield return null;
            }

            if (!CanContinueToxicRushPitFallSlam())
                yield break;

            SnapToPhase2SlamLanding(landingPosition);
            ApplyPhase2SlamDamage(null, landingPosition);
            FaceCurrentTarget();
        }
        finally
        {
            ClearCombatHeightPresentation();
            EndToxicRushPitFallSlamLock();
            toxicRushPitFallSlamRoutine = null;
        }
    }

    private bool CanStartToxicRushPitFallSlam()
    {
        return isActiveAndEnabled &&
               gameObject.activeInHierarchy &&
               !IsDead &&
               !HasDeadTag() &&
               !HasGroggyTag() &&
               CurrentHealthValue > 0f;
    }

    private bool CanContinueToxicRushPitFallSlam()
    {
        return CanStartToxicRushPitFallSlam();
    }

    private void BeginToxicRushPitFallSlamLock()
    {
        if (isToxicRushPitFallSlamLocked)
            return;

        isToxicRushPitFallSlamLocked = true;
        SetPitFallRuntimeLock(true);
        SetPassiveContactDamageBlocked(true);
        SetPatternMoveDamageBlocked(true);
        PushPitFallTriggerBlock();
    }

    private void EndToxicRushPitFallSlamLock()
    {
        if (!isToxicRushPitFallSlamLocked)
            return;

        PopPitFallTriggerBlock();
        SetPatternMoveDamageBlocked(false);
        SetPassiveContactDamageBlocked(false);
        SetPitFallRuntimeLock(false);
        isToxicRushPitFallSlamLocked = false;
    }

    /// <summary>이전 독구름 위치에서 현재 위치까지 설정 간격에 맞춰 독구름을 배치합니다.</summary>
    private void SpawnPoisonCloudTrailIfNeeded(Vector3 currentPosition)
    {
        if (!hasLastPoisonCloudSpawnPosition)
        {
            SpawnPoisonCloudAt(currentPosition);
            return;
        }

        float spacing = Mathf.Max(0.05f, poisonCloudSpawnSpacing);
        Vector3 toCurrent = currentPosition - lastPoisonCloudSpawnPosition;
        float distance = toCurrent.magnitude;
        if (distance < spacing)
            return;

        Vector3 direction = toCurrent / distance;
        Vector3 spawnPosition = lastPoisonCloudSpawnPosition;

        while (distance >= spacing)
        {
            spawnPosition += direction * spacing;
            SpawnPoisonCloudAt(spawnPosition);
            distance -= spacing;
        }
    }

    /// <summary>지정 위치에 독구름 프리팹을 생성하고 패턴 수치를 적용합니다.</summary>
    private void SpawnPoisonCloudAt(Vector3 spawnPosition)
    {
        if (poisonCloudPrefab == null)
            return;

        PoisonCloudArea poisonCloud = Instantiate(poisonCloudPrefab, spawnPosition, Quaternion.identity);
        poisonCloud.Initialize(
            poisonCloudRadius,
            poisonCloudActiveSeconds,
            poisonCloudFadeSeconds,
            poisonCloudDamage,
            poisonCloudDamageIntervalSeconds,
            poisonCloudDamageEffect);

        lastPoisonCloudSpawnPosition = spawnPosition;
        hasLastPoisonCloudSpawnPosition = true;
    }

    /// <summary>생성된 텔레그래프 뷰 목록을 제거합니다.</summary>
    private static void ClearViews(List<AttackTelegraphView> views)
    {
        if (views == null)
            return;

        for (int i = 0; i < views.Count; i++)
        {
            AttackTelegraphView view = views[i];
            if (view != null)
                Destroy(view.gameObject);
        }

        views.Clear();
    }
}
