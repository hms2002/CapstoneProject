using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 슬라임 여왕 2페이즈 개체가 공유하는 접촉 피해와 향후 패턴 실행 기반입니다.
/// </summary>
[RequireComponent(typeof(SlimeQueenVanishParticleEffect))]
public abstract class SlimeQueenPhaseTwoBase : SlimeQueenBossBase, ISlimeQueenBodyInflateHost
{
    private static readonly int IsDeadHash = Animator.StringToHash("isDead");
    protected static readonly int IsSinkingHash = Animator.StringToHash("isSinking");

    [Header("Phase 2 Contact")]
    [Tooltip("2페이즈 퀸이 플레이어와 접촉했을 때 적용할 피해량입니다.")]
    [SerializeField, Min(0f)] private float contactDamage = 1f;

    [Tooltip("2페이즈 접촉 피해를 다시 적용할 수 있는 최소 간격입니다.")]
    [SerializeField, Min(0f)] private float contactDamageCooldownSeconds = 1f;

    [Tooltip("2페이즈 접촉 피해에 사용할 GAS Damage Effect입니다.")]
    [SerializeField] private GE_Damage_Spec contactDamageEffect;

    [Space(8)]

    [Header("Phase 2 - Repeated Slam")]
    [Tooltip("연속 내려찍기 경고 표시에 사용할 AttackTelegraph 스타일입니다.")]
    [SerializeField] private AttackTelegraphStyle slamWarningStyle;

    [Tooltip("연속 내려찍기 경고 원의 지름입니다.")]
    [SerializeField, Min(0.1f)] private float slamWarningDiameter = 2.8f;

    [Tooltip("연속 내려찍기 피해 판정 원의 지름입니다.")]
    [SerializeField, Min(0.1f)] private float slamDamageDiameter = 2.8f;

    [Tooltip("연속 내려찍기 사이의 텀입니다.")]
    [SerializeField, Min(0.1f)] private float slamIntervalSeconds = 1f;

    [Tooltip("연속 내려찍기를 반복할 횟수입니다.")]
    [SerializeField, Min(1)] private int slamCount = 3;

    [Tooltip("연속 내려찍기에서 착지 위치 위로 올라가 체공할 높이입니다.")]
    [SerializeField, Min(0f)] private float slamArcHeight = 2.8f;

    [Tooltip("연속 내려찍기 착지 시 플레이어에게 적용할 피해량입니다.")]
    [SerializeField, Min(0f)] private float slamDamage = 1f;

    [Tooltip("연속 내려찍기 피해에 사용할 GAS Damage Effect입니다.")]
    [SerializeField] private GE_Damage_Spec slamDamageEffect;

    [Space(8)]

    [Header("Phase 2 - Body Inflate Impact")]
    [Tooltip("몸 부풀림 원형 경고 표시에 사용할 AttackTelegraph 스타일입니다.")]
    [SerializeField] private AttackTelegraphStyle bodyInflateWarningStyle;

    [Tooltip("몸 부풀림 경고 원의 지름입니다.")]
    [SerializeField, Min(0.1f)] private float bodyInflateWarningDiameter = 6f;

    [Tooltip("몸 부풀림 경고가 유지되는 시간입니다.")]
    [SerializeField, Min(0f)] private float bodyInflateWarningSeconds = 1.4f;

    [Tooltip("몸 부풀림 실제 피해 판정 원의 지름입니다.")]
    [SerializeField, Min(0.1f)] private float bodyInflateImpactDiameter = 6f;

    [Tooltip("몸 부풀림이 플레이어에게 주는 피해량입니다. 0이면 피해 없이 넉백만 적용합니다.")]
    [SerializeField, Min(0f)] private float bodyInflateImpactDamage = 0f;

    [Tooltip("몸 부풀림 피해에 사용할 GAS Damage Effect입니다.")]
    [SerializeField] private GE_Damage_Spec bodyInflateImpactDamageEffect;

    [Tooltip("몸 부풀림 넉백에 사용할 GAS Knockback Effect입니다.")]
    [SerializeField] private GE_Knockback_Spec bodyInflateImpactKnockbackEffect;

    [Tooltip("몸 부풀림 넉백 세기입니다.")]
    [SerializeField, Min(0f)] private float bodyInflateImpactKnockbackImpulse = 195f;

    [Space(8)]

    [Header("Phase 2 - Castling")]
    [Tooltip("캐슬링 경고선 표시에 사용할 AttackTelegraph 스타일입니다.")]
    [SerializeField] private AttackTelegraphStyle castlingWarningStyle;

    [Tooltip("캐슬링 경고선이 유지되는 시간입니다.")]
    [SerializeField, Min(0f)] private float castlingWarningSeconds = 1.2f;

    [Tooltip("캐슬링 돌진 속도입니다. 기본값 9는 플레이어 기본 이동속도 3의 3배 기준입니다.")]
    [SerializeField, Min(0.1f)] private float castlingRushSpeed = 9f;

    [Tooltip("캐슬링 경고선의 폭입니다.")]
    [SerializeField, Min(0.05f)] private float castlingWarningWidth = 2.2f;

    [Tooltip("캐슬링 돌진 중 플레이어 충돌을 검사할 원형 반지름입니다.")]
    [SerializeField, Min(0.05f)] private float castlingHitRadius = 0.75f;

    [Tooltip("캐슬링 돌진 중 플레이어와 충돌했을 때 주는 피해량입니다.")]
    [SerializeField, Min(0f)] private float castlingDamage = 1f;

    [Tooltip("캐슬링 피해에 사용할 GAS Damage Effect입니다. 비우면 2페이즈 접촉 피해 Effect를 사용합니다.")]
    [SerializeField] private GE_Damage_Spec castlingDamageEffect;

    [Space(8)]

    [Header("Phase 2 Defeated Presentation")]
    [Tooltip("2페이즈 보스가 체력 0으로 쓰러진 채 남아 있을 때 SpriteRenderer 색에 곱할 밝기입니다.")]
    [SerializeField, Range(0f, 1f)] private float defeatedRendererBrightness = 0.5f;

    private float nextContactDamageTime;
    private bool isPassiveContactDamageBlocked;
    private bool isJointPatternLocked;
    private bool isDrainControlLocked;
    private bool isSplitLandingControlLocked;
    private bool splitLandingMovementMotorWasEnabled;
    private Coroutine splitLandingRoutine;
    private bool? hasIsDeadParameter;
    private SpeechBubbleComponent speechBubble;
    private SlimeQueenVanishParticleEffect finaleVanishEffect;
    private readonly List<AttackTelegraphView> bodyInflateWarningViews = new List<AttackTelegraphView>();
    private readonly List<AttackTelegraphView> castlingWarningViews = new List<AttackTelegraphView>();
    private SpriteRenderer[] phaseTwoSpriteRenderers;
    private Color[] phaseTwoSpriteBaseColors;

    public int Phase2SlamCount => Mathf.Max(1, slamCount);

    public float Phase2SlamIntervalSeconds => Mathf.Max(0.1f, slamIntervalSeconds);

    public float BodyInflateWarningSeconds => bodyInflateWarningSeconds;

    public float CastlingWarningSeconds => Mathf.Max(0f, castlingWarningSeconds);

    public float CastlingRushSpeed => Mathf.Max(0.1f, castlingRushSpeed);

    public readonly struct CastlingContext
    {
        public readonly SlimeQueenP2Short ShortQueen;
        public readonly SlimeQueenP2Long LongQueen;
        public readonly Vector3 ShortStartPosition;
        public readonly Vector3 LongStartPosition;
        public readonly float Distance;

        public bool IsValid => ShortQueen != null && LongQueen != null && Distance > 0.05f;

        public CastlingContext(
            SlimeQueenP2Short shortQueen,
            SlimeQueenP2Long longQueen,
            Vector3 shortStartPosition,
            Vector3 longStartPosition)
        {
            ShortQueen = shortQueen;
            LongQueen = longQueen;
            ShortStartPosition = shortStartPosition;
            LongStartPosition = longStartPosition;
            Distance = Vector3.Distance(shortStartPosition, longStartPosition);
        }
    }

    protected override void Awake()
    {
        base.Awake();
        EnsureFinaleVanishEffect();
        CachePhaseTwoDefeatedTintTargets();
    }

    /// <summary>패턴 피해가 우선 적용되어야 하는 동안 상시 접촉 피해를 막습니다.</summary>
    public void SetPassiveContactDamageBlocked(bool isBlocked)
    {
        isPassiveContactDamageBlocked = isBlocked;
    }

    /// <summary>배수구에 끌려가는 동안 현재 패턴, 기본 이동, 상시 접촉 피해를 잠급니다.</summary>
    public void BeginDrainControlLock()
    {
        if (isDrainControlLocked)
            return;

        isDrainControlLocked = true;
        CancelCastlingForDrain();
        SetPitFallRuntimeLock(true);
        SetPassiveContactDamageBlocked(true);
    }

    /// <summary>배수구에서 올라온 뒤 배수구 전용 잠금을 해제합니다.</summary>
    public void EndDrainControlLock()
    {
        if (!isDrainControlLocked)
            return;

        EndDrainSinkAnimation();
        SetPassiveContactDamageBlocked(false);
        SetPatternMoveDamageBlocked(false);
        SetPitFallRuntimeLock(false);
        isDrainControlLocked = false;
    }

    /// <summary>1페이즈 분열 직후 잡몹 분열과 같은 포물선 착지 연출을 시작합니다.</summary>
    public void BeginPhaseTwoSplitLanding(Vector3 startPosition, Vector3 landingPosition, float durationSeconds, float arcHeight)
    {
        if (splitLandingRoutine != null)
            StopCoroutine(splitLandingRoutine);

        splitLandingRoutine = StartCoroutine(PhaseTwoSplitLandingRoutine(
            startPosition,
            landingPosition,
            Mathf.Max(0.01f, durationSeconds),
            Mathf.Max(0f, arcHeight)));
    }

    /// <summary>배수구 안에 잠긴 상태의 보스별 애니메이션 진입 훅입니다.</summary>
    public virtual void BeginDrainSinkAnimation()
    {
        SetAnimatorBoolIfExists(IsSinkingHash, true);
    }

    /// <summary>배수구에서 올라올 때 보스별 애니메이션 상태를 정리하는 훅입니다.</summary>
    public virtual void EndDrainSinkAnimation()
    {
        SetAnimatorBoolIfExists(IsSinkingHash, false);
    }

    protected override void Update()
    {
        if (isSplitLandingControlLocked)
            return;

        if (isDrainControlLocked)
            return;

        if (isJointPatternLocked)
        {
            if (movementMotor != null)
                movementMotor.StopAllMotion();

            return;
        }

        base.Update();
    }

    protected override void LateUpdate()
    {
        base.LateUpdate();

        if (IsDead || HasDeadTag() || CurrentHealthValue <= 0f)
            ApplyPhaseTwoDefeatedTint();
    }

    protected override void OnDestroy()
    {
        ForceCleanupPhaseTwoSplitLanding();
        CleanupBodyInflatePresentation();
        ForceCleanupCastlingPattern();
        base.OnDestroy();
    }

    protected virtual void OnDisable()
    {
        ForceCleanupPhaseTwoSplitLanding();
        CleanupBodyInflatePresentation();
        ForceCleanupCastlingPattern();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryApplyContactDamage(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryApplyContactDamage(other);
    }

    protected override void OnDeathStarted()
    {
        SetPhaseTwoDeathAnimation(true);
        base.OnDeathStarted();
        ApplyPhaseTwoDefeatedTint();
    }

    protected override void PlayDeathAnimation()
    {
        SetPhaseTwoDeathAnimation(true);
        ApplyPhaseTwoDefeatedTint();
    }

    protected override void DestroyAfterDelay()
    {
        if (!BossEncounterEndDirector.SuppressesAutomaticRewardReady(this))
            base.DestroyAfterDelay();
    }

    /// <summary>패턴이 설정된 2페이즈 보스만 일반 패턴 루프를 사용하고, 비어 있으면 대기 상태를 사용합니다.</summary>
    protected override BossCombatIdleState CreateCombatIdleState()
    {
        if (ConfiguredPhaseCount > 0)
            return base.CreateCombatIdleState();

        return new PhaseTwoWaitingState(this);
    }

    /// <summary>현재 2페이즈 근거리/원거리 퀸이 캐슬링을 시작할 수 있는지 확인합니다.</summary>
    public bool CanStartCastlingPattern(out string reason)
    {
        if (!TryResolveCastlingPair(out SlimeQueenP2Short shortQueen, out SlimeQueenP2Long longQueen))
        {
            reason = "캐슬링 파트너를 찾지 못했습니다.";
            return false;
        }

        return CanUseCastlingPair(shortQueen, longQueen, null, out reason);
    }

    /// <summary>캐슬링 시작 순간의 두 슬라임 위치를 저장하고 합동 패턴 잠금을 겁니다.</summary>
    public bool TryBeginCastlingPattern(out CastlingContext context)
    {
        context = default;
        if (!TryResolveCastlingPair(out SlimeQueenP2Short shortQueen, out SlimeQueenP2Long longQueen))
            return false;

        if (!CanUseCastlingPair(shortQueen, longQueen, this, out _))
            return false;

        Vector3 shortStartPosition = shortQueen.transform.position;
        Vector3 longStartPosition = longQueen.transform.position;
        context = new CastlingContext(shortQueen, longQueen, shortStartPosition, longStartPosition);
        if (!context.IsValid)
            return false;

        shortQueen.SetCastlingRuntimeLock(true);
        longQueen.SetCastlingRuntimeLock(true);
        shortQueen.BeginPatternFacingLockTowards(longStartPosition);
        longQueen.BeginPatternFacingLockTowards(shortStartPosition);
        shortQueen.SetPatternMoveDamageBlocked(true);
        longQueen.SetPatternMoveDamageBlocked(true);
        shortQueen.SetPassiveContactDamageBlocked(true);
        longQueen.SetPassiveContactDamageBlocked(true);
        return true;
    }

    /// <summary>캐슬링 진행 중 두 슬라임이 계속 유효한 상태인지 확인합니다.</summary>
    public bool CanContinueCastlingPattern(CastlingContext context, out string reason)
    {
        if (!context.IsValid)
        {
            reason = "캐슬링 컨텍스트가 유효하지 않습니다.";
            return false;
        }

        if (!IsCastlingParticipantAlive(context.ShortQueen, out reason))
            return false;

        if (!IsCastlingParticipantAlive(context.LongQueen, out reason))
            return false;

        reason = null;
        return true;
    }

    /// <summary>캐슬링 종료/취소 시 두 슬라임의 합동 패턴 잠금과 임시 표시를 정리합니다.</summary>
    public void EndCastlingPattern(CastlingContext context)
    {
        if (context.ShortQueen != null)
            context.ShortQueen.ForceCleanupCastlingPattern();

        if (context.LongQueen != null)
            context.LongQueen.ForceCleanupCastlingPattern();
    }

    /// <summary>씬 정리나 강제 취소에서 캐슬링 임시 상태를 제거합니다.</summary>
    public void ForceCleanupCastlingPattern()
    {
        CleanupCastlingPresentation();
        EndPatternFacingLock();
        SetPatternMoveDamageBlocked(false);
        SetPassiveContactDamageBlocked(false);
        SetCastlingRuntimeLock(false);
    }

    /// <summary>씬의 2페이즈 근거리/원거리 퀸 한 쌍을 찾습니다.</summary>
    public bool TryResolveCastlingPair(out SlimeQueenP2Short shortQueen, out SlimeQueenP2Long longQueen)
    {
        shortQueen = this as SlimeQueenP2Short;
        longQueen = this as SlimeQueenP2Long;

        if (shortQueen == null)
            shortQueen = FindActivePhaseTwo<SlimeQueenP2Short>();

        if (longQueen == null)
            longQueen = FindActivePhaseTwo<SlimeQueenP2Long>();

        return shortQueen != null && longQueen != null;
    }

    /// <summary>발동 순간의 두 슬라임 위치 사이에 캐슬링 경고선을 표시합니다.</summary>
    public void ShowCastlingWarning(CastlingContext context)
    {
        ClearCastlingWarnings();
        if (!context.IsValid)
            return;

        AttackTelegraphService service = GetTelegraphService();
        if (service == null)
            return;

        Vector2 direction = context.LongStartPosition - context.ShortStartPosition;
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        Vector2 center = ((Vector2)context.ShortStartPosition + (Vector2)context.LongStartPosition) * 0.5f;
        float length = direction.magnitude;
        float rotationDegrees = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        AttackTelegraphSpec spec = WithThinWarningOutline(AttackTelegraphSpec.CreateRectangle(
            center,
            new Vector2(length, Mathf.Max(0.05f, castlingWarningWidth)),
            rotationDegrees,
            CastlingWarningSeconds,
            castlingWarningStyle));

        AttackTelegraphView view = service.SpawnDetachedView(spec);
        if (view != null)
            castlingWarningViews.Add(view);
    }

    /// <summary>캐슬링 경고 표시를 즉시 제거합니다.</summary>
    public void ClearCastlingWarnings()
    {
        ClearViews(castlingWarningViews);
    }

    /// <summary>캐슬링 경고 표시를 정리합니다.</summary>
    public void CleanupCastlingPresentation()
    {
        ClearCastlingWarnings();
    }

    /// <summary>캐슬링 경고 중 해당 2페이즈 슬라임의 말풍선 대사를 출력합니다.</summary>
    public bool TryShowCastlingSpeech(string text, float duration)
    {
        return TryShowPhaseTwoSpeech(text, Mathf.Max(0.1f, duration), null);
    }

    /// <summary>2페이즈 보스 공용 말풍선 대사를 출력합니다.</summary>
    public bool TryShowPhaseTwoSpeech(string text, float duration, Action onHidden = null)
    {
        return TryShowPhaseTwoSpeech(text, duration, onHidden, Vector3.zero);
    }

    /// <summary>2페이즈 보스 공용 말풍선 대사를 추가 오프셋과 함께 출력합니다.</summary>
    public bool TryShowPhaseTwoSpeech(string text, float duration, Action onHidden, Vector3 bubbleOffsetDelta)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        if (speechBubble == null)
            speechBubble = GetComponent<SpeechBubbleComponent>();

        if (speechBubble == null)
            speechBubble = GetComponentInChildren<SpeechBubbleComponent>(includeInactive: true);

        if (speechBubble == null)
        {
            Debug.Log($"SlimeQueen Phase Two Speech: {text}", this);
            return false;
        }

        speechBubble.SpeakWithOffsetDelta(text, Mathf.Max(0.1f, duration), null, onHidden, bubbleOffsetDelta);
        return true;
    }

    /// <summary>최종 패배 연출에서 이 2페이즈 보스를 소멸시킵니다.</summary>
    public void PlayFinaleVanishAndDestroy()
    {
        if (speechBubble == null)
            speechBubble = GetComponentInChildren<SpeechBubbleComponent>(includeInactive: true);

        speechBubble?.HideActive();
        CleanupBodyInflatePresentation();
        CleanupCastlingPresentation();
        PlayFinaleVanishEffect(transform.position);
        SetPhaseTwoRenderersVisible(false);
        Destroy(gameObject);
    }

    /// <summary>캐슬링 진행도에 맞춰 보스 위치를 선형 이동시킵니다.</summary>
    public void SetCastlingPose(Vector3 startPosition, Vector3 destination, float normalizedTime)
    {
        if (movementMotor != null)
            movementMotor.StopAllMotion();

        float clampedTime = Mathf.Clamp01(normalizedTime);
        transform.position = Vector3.Lerp(startPosition, destination, clampedTime);
        FaceCastlingDestination(destination);
    }

    /// <summary>캐슬링 종료 위치로 보스 좌표를 확정합니다.</summary>
    public void SnapToCastlingDestination(Vector3 destination)
    {
        if (movementMotor != null)
            movementMotor.StopAllMotion();

        transform.position = destination;
        FaceCastlingDestination(destination);
    }

    /// <summary>캐슬링 돌진 중 플레이어와 겹치면 한 패턴당 한 번 피해를 적용합니다.</summary>
    public bool TryApplyCastlingDamage(AbilitySystem sourceSystem, AbilitySpec sourceSpec, HashSet<GameObject> damagedTargets)
    {
        GE_Damage_Spec damageEffect = ResolveCastlingDamageEffect();
        if (castlingDamage <= 0f || damageEffect == null)
            return false;

        float radius = Mathf.Max(0.05f, castlingHitRadius);
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hitCollider = hits[i];
            if (hitCollider == null || !HasPlayerTagInHierarchy(hitCollider.transform))
                continue;

            GameObject damageTarget = CombatTargetResolver2D.ResolveDamageTarget(hitCollider);
            if (damageTarget == null || !damageTarget.CompareTag("Player"))
                continue;

            if (damagedTargets != null && damagedTargets.Contains(damageTarget))
                continue;

            Vector3 hitWorldPosition = hitCollider.ClosestPoint(transform.position);
            CombatDamageAction.ApplyDamageAndEmitHit(
                sourceSystem != null ? sourceSystem : AbilitySystem,
                sourceSpec,
                damageEffect,
                null,
                damageTarget,
                castlingDamage,
                0f,
                0f,
                null,
                hitWorldPosition,
                gameObject);

            damagedTargets?.Add(damageTarget);
            return true;
        }

        return false;
    }

    /// <summary>페이즈 2 연속 내려찍기 경고 원을 표시합니다.</summary>
    public void ShowPhase2SlamWarning(Vector3 landingPosition)
    {
        AttackTelegraphService service = GetTelegraphService();
        if (service == null)
            return;

        AttackTelegraphSpec spec = WithThinWarningOutline(AttackTelegraphSpec.CreateCircle(
            landingPosition,
            slamWarningDiameter,
            Phase2SlamIntervalSeconds,
            slamWarningStyle));

        service.SpawnDetachedView(spec);
    }

    /// <summary>페이즈 2 내려찍기 착지 위치를 현재 타겟 위치로 계산합니다.</summary>
    public bool TryGetPhase2SlamLandingPosition(GameObject explicitTarget, out Vector3 landingPosition)
    {
        Transform targetTransform = explicitTarget != null ? explicitTarget.transform : CurrentTarget;
        if (targetTransform == null)
        {
            landingPosition = transform.position;
            return false;
        }

        landingPosition = targetTransform.position;
        landingPosition.z = transform.position.z;
        return true;
    }

    /// <summary>페이즈 2 내려찍기에서 착지 위치 위로 빠르게 올라가 체공한 뒤 급강하하는 자세를 적용합니다.</summary>
    public void SetPhase2SlamPose(Vector3 startPosition, Vector3 landingPosition, float normalizedTime)
    {
        ApplyKnightStyleSlamPose(startPosition, landingPosition, normalizedTime, slamArcHeight);
    }

    /// <summary>구덩이에서 복귀할 때 시작부터 공중에 있는 상태로 내려오는 낙하 자세를 적용합니다.</summary>
    public void SetPhase2PitFallReturnPose(Vector3 landingPosition, float normalizedTime, float startVisualHeight)
    {
        float clampedTime = Mathf.Clamp01(normalizedTime);
        float safeHeight = Mathf.Max(0f, startVisualHeight);
        float easedDrop = Mathf.Pow(clampedTime, 2.25f);
        float visualHeight = Mathf.Lerp(safeHeight, 0f, easedDrop);
        ApplyGroundedMotionPose(landingPosition, visualHeight);
        SnapCombatHeightPresentationToState();
    }

    /// <summary>페이즈 2 내려찍기 종료 위치로 보스 좌표를 확정합니다.</summary>
    public void SnapToPhase2SlamLanding(Vector3 landingPosition)
    {
        SnapToGroundedMotionLanding(landingPosition);
    }

    /// <summary>페이즈 2 내려찍기 범위 안의 현재 타겟에게 GAS Damage Effect를 적용합니다.</summary>
    public void ApplyPhase2SlamDamage(AbilitySpec sourceSpec, Vector3 landingPosition)
    {
        PlayLightSlamLandingCameraShake($"{GetType().Name}.Phase2SlamLanding");

        if (slamDamage <= 0f || CurrentTarget == null || slamDamageEffect == null)
            return;

        float damageRadius = Mathf.Max(0.1f, slamDamageDiameter * 0.5f);
        float sqrDistance = ((Vector2)(CurrentTarget.position - landingPosition)).sqrMagnitude;
        if (sqrDistance > damageRadius * damageRadius)
            return;

        CombatDamageAction.ApplyDamageAndEmitHit(
            AbilitySystem,
            sourceSpec,
            slamDamageEffect,
            null,
            CurrentTarget.gameObject,
            slamDamage,
            0f,
            0f,
            null,
            landingPosition,
            gameObject);
    }

    /// <summary>몸 부풀림 원형 경고를 보스 위치에 표시합니다.</summary>
    public void ShowBodyInflateWarning()
    {
        CleanupBodyInflatePresentation();

        AttackTelegraphService service = GetTelegraphService();
        if (service == null)
            return;

        AttackTelegraphSpec spec = WithThinWarningOutline(AttackTelegraphSpec.CreateCircle(
            transform.position,
            bodyInflateWarningDiameter,
            bodyInflateWarningSeconds,
            bodyInflateWarningStyle));

        AttackTelegraphView view = service.SpawnDetachedView(spec);
        if (view != null)
            bodyInflateWarningViews.Add(view);
    }

    public void CleanupBodyInflatePresentation()
    {
        ClearViews(bodyInflateWarningViews);
    }

    /// <summary>몸 부풀림 범위 안의 플레이어에게 피해와 넉백을 적용합니다.</summary>
    public void ApplyBodyInflateImpact(AbilitySpec sourceSpec)
    {
        bool hasDamage = bodyInflateImpactDamage > 0f;
        bool hasKnockback = bodyInflateImpactKnockbackImpulse > 0f && bodyInflateImpactKnockbackEffect != null;

        if ((!hasDamage && !hasKnockback) || CurrentTarget == null)
            return;

        if (hasDamage && bodyInflateImpactDamageEffect == null)
            return;

        float radius = Mathf.Max(0.1f, bodyInflateImpactDiameter * 0.5f);
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius);

        for (int i = 0; i < hits.Length; i++)
        {
            if (!HasPlayerTagInHierarchy(hits[i].transform))
                continue;

            GameObject contactTarget = CombatTargetResolver2D.ResolveDamageTarget(hits[i]);
            if (contactTarget == null || !contactTarget.CompareTag("Player"))
                continue;

            Vector3 hitWorldPosition = hits[i].ClosestPoint(transform.position);
            if (!hasDamage)
            {
                ApplyBodyInflateKnockbackOnly(sourceSpec, contactTarget);
                return;
            }

            CombatDamageAction.ApplyDamageAndEmitHit(
                AbilitySystem,
                sourceSpec,
                bodyInflateImpactDamageEffect,
                bodyInflateImpactKnockbackEffect,
                contactTarget,
                bodyInflateImpactDamage,
                0f,
                hasKnockback ? bodyInflateImpactKnockbackImpulse : 0f,
                null,
                hitWorldPosition,
                gameObject);
            return;
        }
    }

    private void ApplyBodyInflateKnockbackOnly(AbilitySpec sourceSpec, GameObject contactTarget)
    {
        if (AbilitySystem == null || AbilitySystem.EffectRunner == null)
            return;

        if (bodyInflateImpactKnockbackEffect == null || bodyInflateImpactKnockbackImpulse <= 0f || contactTarget == null)
            return;

        var knockbackSpec = AbilitySystem.MakeSpec(
            bodyInflateImpactKnockbackEffect,
            causer: gameObject,
            sourceObject: sourceSpec != null ? sourceSpec.Definition : null);

        if (bodyInflateImpactKnockbackEffect.knockbackKey != null)
        {
            knockbackSpec.SetSetByCallerMagnitude(
                bodyInflateImpactKnockbackEffect.knockbackKey,
                bodyInflateImpactKnockbackImpulse);
        }

        AbilitySystem.EffectRunner.ApplyEffectSpec(knockbackSpec, contactTarget);
    }

    /// <summary>캐슬링 관련 임시 상태를 패턴 종료 시 항상 정리합니다.</summary>
    protected override void OnPatternEnd(BossPatternEntry patternEntry, bool forced)
    {
        CleanupBodyInflatePresentation();
        ForceCleanupCastlingPattern();
        if (forced)
            ResetPatternAnimatorStateForInterrupt();

        base.OnPatternEnd(patternEntry, forced);
    }

    private static T FindActivePhaseTwo<T>() where T : SlimeQueenPhaseTwoBase
    {
        T[] candidates = FindObjectsByType<T>(FindObjectsInactive.Exclude);
        for (int i = 0; i < candidates.Length; i++)
        {
            T candidate = candidates[i];
            if (candidate != null && candidate.isActiveAndEnabled && candidate.gameObject.activeInHierarchy)
                return candidate;
        }

        return null;
    }

    private bool CanUseCastlingPair(
        SlimeQueenP2Short shortQueen,
        SlimeQueenP2Long longQueen,
        SlimeQueenPhaseTwoBase activeAbilityOwner,
        out string reason)
    {
        if (!IsCastlingParticipantAvailable(shortQueen, activeAbilityOwner, out reason))
            return false;

        if (!IsCastlingParticipantAvailable(longQueen, activeAbilityOwner, out reason))
            return false;

        if (Vector3.Distance(shortQueen.transform.position, longQueen.transform.position) <= 0.05f)
        {
            reason = "캐슬링 위치 간격이 너무 짧습니다.";
            return false;
        }

        reason = null;
        return true;
    }

    private static bool IsCastlingParticipantAvailable(
        SlimeQueenPhaseTwoBase participant,
        SlimeQueenPhaseTwoBase activeAbilityOwner,
        out string reason)
    {
        if (!IsCastlingParticipantAlive(participant, out reason))
            return false;

        if (!participant.IsCombatActive)
        {
            reason = $"{participant.name}: 전투 비활성 상태입니다.";
            return false;
        }

        if (participant.isJointPatternLocked)
        {
            reason = $"{participant.name}: 합동 패턴 잠금 중입니다.";
            return false;
        }

        if (participant.isDrainControlLocked || !participant.CanTriggerPitFall)
        {
            reason = $"{participant.name}: 구덩이/배수구 또는 공중 이동 처리 중입니다.";
            return false;
        }

        if (participant != activeAbilityOwner && participant.IsAbilityExecutionBusy)
        {
            reason = $"{participant.name}: 다른 패턴 실행 중입니다.";
            return false;
        }

        reason = null;
        return true;
    }

    private static bool IsCastlingParticipantAlive(SlimeQueenPhaseTwoBase participant, out string reason)
    {
        if (participant == null)
        {
            reason = "캐슬링 참여자가 없습니다.";
            return false;
        }

        if (!participant.isActiveAndEnabled || !participant.gameObject.activeInHierarchy)
        {
            reason = $"{participant.name}: 비활성 상태입니다.";
            return false;
        }

        if (participant.IsDead || participant.HasDeadTag() || participant.CurrentHealthValue <= 0f)
        {
            reason = $"{participant.name}: 사망 상태입니다.";
            return false;
        }

        if (participant.HasGroggyTag())
        {
            reason = $"{participant.name}: 무력화 상태입니다.";
            return false;
        }

        reason = null;
        return true;
    }

    private void CancelCastlingForDrain()
    {
        if (!isJointPatternLocked)
        {
            ForceCleanupCastlingPattern();
            return;
        }

        if (!TryResolveCastlingPair(out SlimeQueenP2Short shortQueen, out SlimeQueenP2Long longQueen))
        {
            ForceCleanupCastlingPattern();
            return;
        }

        shortQueen.AbortCurrentPattern();
        longQueen.AbortCurrentPattern();
        shortQueen.ForceCleanupCastlingPattern();
        longQueen.ForceCleanupCastlingPattern();
    }

    private void SetCastlingRuntimeLock(bool isLocked)
    {
        isJointPatternLocked = isLocked;

        if (isLocked && movementMotor != null)
            movementMotor.StopAllMotion();
    }

    private IEnumerator PhaseTwoSplitLandingRoutine(
        Vector3 startPosition,
        Vector3 landingPosition,
        float durationSeconds,
        float arcHeight)
    {
        BeginPhaseTwoSplitLandingLock();

        SlimeSplitLandingMotion2D landingMotion = GetComponent<SlimeSplitLandingMotion2D>();
        if (landingMotion == null)
            landingMotion = gameObject.AddComponent<SlimeSplitLandingMotion2D>();

        landingMotion.Begin(startPosition, landingPosition, durationSeconds, arcHeight);

        float elapsedSeconds = 0f;
        while (elapsedSeconds < durationSeconds)
        {
            elapsedSeconds += Time.deltaTime;
            yield return null;
        }

        transform.position = landingPosition;
        EndPhaseTwoSplitLandingLock();
        splitLandingRoutine = null;
    }

    private void BeginPhaseTwoSplitLandingLock()
    {
        if (isSplitLandingControlLocked)
            return;

        isSplitLandingControlLocked = true;
        splitLandingMovementMotorWasEnabled = movementMotor == null || movementMotor.enabled;

        SetPitFallRuntimeLock(true);
        SetPatternMoveDamageBlocked(true);
        SetPassiveContactDamageBlocked(true);

        if (movementMotor != null)
        {
            movementMotor.StopAllMotion();
            movementMotor.enabled = false;
        }

        if (rigid2D != null)
        {
            rigid2D.linearVelocity = Vector2.zero;
            rigid2D.angularVelocity = 0f;
        }
    }

    private void EndPhaseTwoSplitLandingLock()
    {
        if (!isSplitLandingControlLocked)
            return;

        if (rigid2D != null)
        {
            rigid2D.linearVelocity = Vector2.zero;
            rigid2D.angularVelocity = 0f;
        }

        if (movementMotor != null)
            movementMotor.enabled = splitLandingMovementMotorWasEnabled;

        SetPassiveContactDamageBlocked(false);
        SetPatternMoveDamageBlocked(false);
        SetPitFallRuntimeLock(false);
        isSplitLandingControlLocked = false;
    }

    private void ForceCleanupPhaseTwoSplitLanding()
    {
        if (splitLandingRoutine != null)
        {
            StopCoroutine(splitLandingRoutine);
            splitLandingRoutine = null;
        }

        EndPhaseTwoSplitLandingLock();
    }

    private void SetPhaseTwoDeathAnimation(bool value)
    {
        if (animator == null)
            return;

        if (!hasIsDeadParameter.HasValue)
            hasIsDeadParameter = HasAnimatorBoolParameter(IsDeadHash);

        if (hasIsDeadParameter.Value)
            animator.SetBool(IsDeadHash, value);
    }

    private bool HasAnimatorBoolParameter(int parameterHash)
    {
        if (animator == null)
            return false;

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.nameHash == parameterHash && parameter.type == AnimatorControllerParameterType.Bool)
                return true;
        }

        return false;
    }

    /// <summary>2페이즈 패배 색상 고정에 사용할 SpriteRenderer와 원본 색을 캐싱합니다.</summary>
    private void CachePhaseTwoDefeatedTintTargets()
    {
        phaseTwoSpriteRenderers = GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
        phaseTwoSpriteBaseColors = new Color[phaseTwoSpriteRenderers.Length];

        for (int i = 0; i < phaseTwoSpriteRenderers.Length; i++)
        {
            SpriteRenderer targetRenderer = phaseTwoSpriteRenderers[i];
            phaseTwoSpriteBaseColors[i] = targetRenderer != null ? targetRenderer.color : Color.white;
        }
    }

    /// <summary>체력 0으로 쓰러진 2페이즈 보스 비주얼을 원본 대비 어둡게 유지합니다.</summary>
    private void ApplyPhaseTwoDefeatedTint()
    {
        if (phaseTwoSpriteRenderers == null || phaseTwoSpriteBaseColors == null)
            CachePhaseTwoDefeatedTintTargets();

        float brightness = Mathf.Clamp01(defeatedRendererBrightness);
        for (int i = 0; i < phaseTwoSpriteRenderers.Length; i++)
        {
            SpriteRenderer targetRenderer = phaseTwoSpriteRenderers[i];
            if (targetRenderer == null)
                continue;

            Color baseColor = i < phaseTwoSpriteBaseColors.Length ? phaseTwoSpriteBaseColors[i] : Color.white;
            targetRenderer.color = new Color(
                baseColor.r * brightness,
                baseColor.g * brightness,
                baseColor.b * brightness,
                baseColor.a);
        }

    }

    private void SetPhaseTwoRenderersVisible(bool visible)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer phaseTwoRenderer = renderers[i];
            if (phaseTwoRenderer != null)
                phaseTwoRenderer.enabled = visible;
        }
    }

    private void PlayFinaleVanishEffect(Vector3 position)
    {
        EnsureFinaleVanishEffect();
        finaleVanishEffect?.SpawnOneShot(position, sprite);
    }

    private void EnsureFinaleVanishEffect()
    {
        if (finaleVanishEffect == null)
            finaleVanishEffect = GetComponent<SlimeQueenVanishParticleEffect>();

        if (finaleVanishEffect == null)
            finaleVanishEffect = gameObject.AddComponent<SlimeQueenVanishParticleEffect>();
    }

    private void FaceCastlingDestination(Vector3 destination)
    {
        if (sprite == null)
            return;

        if (ShouldBlockFacingUpdate())
            return;

        if (IsPatternFacingLocked)
        {
            ApplyPatternFacingLock();
            return;
        }

        if (transform.position.x > destination.x)
            sprite.flipX = true;
        else if (transform.position.x < destination.x)
            sprite.flipX = false;
    }

    private GE_Damage_Spec ResolveCastlingDamageEffect()
    {
        return castlingDamageEffect != null ? castlingDamageEffect : contactDamageEffect;
    }

    private static void ClearViews(List<AttackTelegraphView> views)
    {
        if (views == null)
            return;

        for (int i = 0; i < views.Count; i++)
        {
            AttackTelegraphView view = views[i];
            if (view != null)
            {
                view.HideImmediate();
                Destroy(view.gameObject);
            }
        }

        views.Clear();
    }

    /// <summary>플레이어와 접촉 중이면 GAS 피해를 적용합니다.</summary>
    private void TryApplyContactDamage(Collider2D other)
    {
        if (IsPatternMoveDamageBlocked || isPassiveContactDamageBlocked || IsDead || other == null)
            return;

        if (contactDamage <= 0f || contactDamageEffect == null || Time.time < nextContactDamageTime)
            return;

        if (!HasPlayerTagInHierarchy(other.transform))
            return;

        GameObject contactTarget = CombatTargetResolver2D.ResolveDamageTarget(other);
        if (contactTarget == null || !contactTarget.CompareTag("Player"))
            return;

        Vector3 hitWorldPosition = other.ClosestPoint(transform.position);
        CombatDamageAction.ApplyDamageAndEmitHit(
            AbilitySystem,
            null,
            contactDamageEffect,
            null,
            contactTarget,
            contactDamage,
            0f,
            0f,
            null,
            hitWorldPosition,
            gameObject);

        nextContactDamageTime = Time.time + Mathf.Max(0f, contactDamageCooldownSeconds);
    }

    private sealed class PhaseTwoWaitingState : BossCombatIdleState
    {
        public PhaseTwoWaitingState(BossControllerBase boss) : base(boss)
        {
        }

        public override void OnEnter()
        {
            LogState("2페이즈 패턴 구현 대기 상태입니다.");
        }

        public override void OnUpdate()
        {
        }
    }
}
