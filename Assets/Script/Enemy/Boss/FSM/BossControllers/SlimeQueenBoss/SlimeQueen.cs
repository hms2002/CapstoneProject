using System.Collections;
using System.Collections.Generic;
using CapstoneAudio;
using CapstonePresentation;
using UnityEngine;
using UnityGAS;

[RequireComponent(typeof(SlimeQueenVanishParticleEffect))]
public sealed class SlimeQueen : SlimeQueenBossBase, ISlimeQueenBodyInflateHost, ISlimeQueenRandomJumpHost
{
    private const float PhaseTwoSplitBlockedSkin = 0.08f;
    private const int PhaseTwoSplitDirectionAttempts = 16;
    private const int PhaseTwoSplitSafeLandingResolveSteps = 8;
    private const int PhaseTwoSplitSafeLandingRadialSteps = 4;
    private const int PhaseTwoSplitSafeLandingAngleSteps = 16;
    private const float PhaseTwoSplitSafeLandingMinRadiusScale = 0.35f;

    private static readonly int IsJumpingHash = Animator.StringToHash("isJumping");
    private static readonly int IsShoutingHash = Animator.StringToHash("isShouting");
    private static readonly int ReadyTriggerHash = Animator.StringToHash("ready");
    private static readonly int IsGiantizationHash = Animator.StringToHash("isGiantization");
    private static readonly int IdleStateHash = Animator.StringToHash("SlimeQueenA_Idle");

    [Header("Slime Queen Runtime")]
    [Tooltip("켜두면 Phase 1 Pattern 1을 런타임 보스 FSM 패턴으로 자동 구성합니다.")]
    [SerializeField] private bool configureRuntimePatternsOnStart = true;

    [Space(8)]

    [Header("Phase 1 - Pattern 1")]
    [Tooltip("패턴 1에서 소환할 중형 슬라임 Knight 프리팹입니다.")]
    [SerializeField] private GameObject knightPrefab;

    [Tooltip("패턴 1에서 소환할 중형 슬라임 Wizard 프리팹입니다.")]
    [SerializeField] private GameObject wizardPrefab;

    [Tooltip("소환 위치 경고 표시에 사용할 AttackTelegraph 스타일입니다.")]
    [SerializeField] private AttackTelegraphStyle summonWarningStyle;

    [Tooltip("소환 경고가 유지되는 시간입니다.")]
    [SerializeField, Min(0.1f)] private float summonWarningSeconds = 1.4f;

    [Tooltip("소환 경고 원의 지름입니다.")]
    [SerializeField, Min(0.1f)] private float summonWarningDiameter = 1.7f;

    [Tooltip("슬라임이 낙하를 시작할 높이입니다.")]
    [SerializeField, Min(0.1f)] private float fallStartHeight = 5f;

    [Tooltip("슬라임 낙하 연출 속도입니다.")]
    [SerializeField, Min(0.1f)] private float fallSpeed = 8f;

    [Tooltip("착지 후 실제 몹이 추적을 시작하기 전까지 멈춰있는 시간입니다.")]
    [SerializeField, Min(0f)] private float postLandingWaitSeconds = 1f;

    [Tooltip("낙하 중 플레이어와 접촉했을 때 적용할 피해량입니다.")]
    [SerializeField, Min(0f)] private float fallingContactDamage = 1f;

    [Tooltip("낙하 접촉 피해에 사용할 GAS Damage Effect입니다.")]
    [SerializeField] private GE_Damage_Spec fallingContactDamageEffect;

    [Tooltip("FSM 패턴 선택 잠금 시간입니다.")]
    [SerializeField, Min(0f)] private float patternSelectionLockSeconds = 0.5f;

    [Tooltip("패턴 종료 후 다음 패턴 선택 전 대기 시간입니다.")]
    [SerializeField, Min(0f)] private float postPatternDelaySeconds = 0.35f;

    [Space(8)]

    [Header("Phase 1 - Pattern 2")]
    [Tooltip("패턴 2의 랜덤 착지 위치를 뽑을 바운더리입니다. 비워두면 씬에서 자동 탐색합니다.")]
    [SerializeField] private SlimeQueenRandomMoveBounds randomMoveBounds;

    [Tooltip("점프 착지 경고 표시에 사용할 AttackTelegraph 스타일입니다.")]
    [SerializeField] private AttackTelegraphStyle jumpWarningStyle;

    [Tooltip("점프 착지 경고 원의 지름입니다.")]
    [SerializeField, Min(0.1f)] private float jumpWarningDiameter = 2.4f;

    [Tooltip("랜덤 위치까지 점프 이동하는 데 걸리는 시간입니다.")]
    [SerializeField, Min(0.1f)] private float jumpDurationSeconds = 1.6f;

    [Tooltip("착지 위치 위로 올라가 체공할 높이입니다.")]
    [SerializeField, Min(0f)] private float jumpArcHeight = 2.5f;

    [Tooltip("착지 피해 판정 원의 지름입니다.")]
    [SerializeField, Min(0.1f)] private float jumpLandingDamageDiameter = 2.4f;

    [Tooltip("착지 시 플레이어에게 적용할 피해량입니다.")]
    [SerializeField, Min(0f)] private float jumpLandingDamage = 1f;

    [Tooltip("착지 피해에 사용할 GAS Damage Effect입니다. 비워두면 패턴 1 낙하 피해 Effect를 사용합니다.")]
    [SerializeField] private GE_Damage_Spec jumpLandingDamageEffect;

    [Space(8)]

    [Header("Phase 1 - Pattern 3")]
    [Tooltip("패턴 3에서 소환할 대형 슬라임 Bishop 프리팹입니다.")]
    [SerializeField] private GameObject bishopPrefab;

    [Tooltip("패턴 3에서 소환할 대형 슬라임 Rook 프리팹입니다.")]
    [SerializeField] private GameObject rookPrefab;

    [Tooltip("패턴 3에서 몬스터가 등장할 네 방향 길목 좌표입니다.")]
    [SerializeField] private Vector2[] callSlimeSpawnPositions =
    {
        new Vector2(-22f, 9.5f),
        new Vector2(-22f, -1.5f),
        new Vector2(-26.5f, 4f),
        new Vector2(-17.5f, 4f)
    };

    [Tooltip("패턴 3 시작 시 보스 말풍선에 출력할 문장입니다.")]
    [SerializeField] private string callSlimeSpeechText = "모두 모여라!!";

    [Tooltip("패턴 3 보스 말풍선이 유지되는 시간입니다.")]
    [SerializeField, Min(0f)] private float callSlimeSpeechSeconds = 1.4f;

    [Tooltip("말풍선 출력 후 몬스터 소환까지 기다리는 시간입니다.")]
    [SerializeField, Min(0f)] private float callSlimeSpawnDelaySeconds = 0.35f;

    [Space(8)]

    [Header("Phase 1 - Pattern 4")]
    [Tooltip("패턴 4 몸 부풀림 원형 경고 표시에 사용할 AttackTelegraph 스타일입니다.")]
    [SerializeField] private AttackTelegraphStyle bodyInflateWarningStyle;

    [Tooltip("패턴 4 몸 부풀림 경고 원의 지름입니다.")]
    [SerializeField, Min(0.1f)] private float bodyInflateWarningDiameter = 6f;

    [Tooltip("패턴 4 몸 부풀림 경고가 유지되는 시간입니다.")]
    [SerializeField, Min(0f)] private float bodyInflateWarningSeconds = 1.4f;

    [Tooltip("패턴 4 몸 부풀림 실제 피해 판정 원의 지름입니다.")]
    [SerializeField, Min(0.1f)] private float bodyInflateImpactDiameter = 6f;

    [Tooltip("패턴 4 몸 부풀림이 플레이어에게 주는 피해량입니다. 0이면 피해 없이 넉백만 적용합니다.")]
    [SerializeField, Min(0f)] private float bodyInflateImpactDamage = 0f;

    [Tooltip("패턴 4 몸 부풀림 피해에 사용할 GAS Damage Effect입니다. 비워두면 패턴 1 낙하 피해 Effect를 사용합니다.")]
    [SerializeField] private GE_Damage_Spec bodyInflateImpactDamageEffect;

    [Tooltip("패턴 4 몸 부풀림 넉백에 사용할 GAS Knockback Effect입니다.")]
    [SerializeField] private GE_Knockback_Spec bodyInflateImpactKnockbackEffect;

    [Tooltip("패턴 4 몸 부풀림 넉백 세기입니다.")]
    [SerializeField, Min(0f)] private float bodyInflateImpactKnockbackImpulse = 195f;

    [Space(8)]

    [Header("Phase 2 Split")]
    [Tooltip("1페이즈 사망 후 생성할 2페이즈 근거리 퀸 프리팹입니다.")]
    [SerializeField] private SlimeQueenP2Short phase2ShortPrefab;

    [Tooltip("1페이즈 사망 후 생성할 2페이즈 원거리 퀸 프리팹입니다.")]
    [SerializeField] private SlimeQueenP2Long phase2LongPrefab;

    [Tooltip("분열 착지점 계산에 실패했을 때 사용하는 2페이즈 근거리 퀸 fallback 오프셋입니다.")]
    [SerializeField] private Vector2 phase2ShortSpawnOffset = new Vector2(-1.5f, 0f);

    [Tooltip("분열 착지점 계산에 실패했을 때 사용하는 2페이즈 원거리 퀸 fallback 오프셋입니다.")]
    [SerializeField] private Vector2 phase2LongSpawnOffset = new Vector2(1.5f, 0f);

    [Tooltip("분열 원점에서 각 2페이즈 퀸이 튀어나갈 거리입니다.")]
    [SerializeField, Min(0f)] private float phase2SplitDistance = 1.45f;

    [Tooltip("2페이즈 퀸 분열 착지 연출 시간입니다.")]
    [SerializeField, Min(0.01f)] private float phase2SplitLandingSeconds = 0.6f;

    [Tooltip("2페이즈 퀸 분열 착지 포물선 높이입니다.")]
    [SerializeField, Min(0f)] private float phase2SplitLandingArcHeight = 0.85f;

    [Tooltip("소멸 파티클 후 2페이즈 분열체가 나타나기 전 짧은 지연 시간입니다.")]
    [SerializeField, Min(0f)] private float phase2SplitVanishLeadSeconds = 0.15f;

    [Tooltip("분열 착지점이 벽/장애물과 겹치지 않도록 검사할 레이어입니다. 비워두면 Wall, Default, Non_FightCollision을 사용합니다.")]
    [SerializeField] private LayerMask phase2SplitBlockedLayers;

    [Tooltip("분열 착지점 충돌 검사 반지름입니다.")]
    [SerializeField, Min(0.01f)] private float phase2SplitLandingProbeRadius = 0.45f;

    [Tooltip("1페이즈 소멸 위치에 생성할 초록 파티클 프리팹입니다. 비워두면 임시 런타임 파티클을 사용합니다.")]
    [SerializeField] private GameObject phase2SplitVanishEffectPrefab;

    [Tooltip("소멸 파티클 프리팹 또는 임시 파티클을 제거할 시간입니다.")]
    [SerializeField, Min(0.1f)] private float phase2SplitVanishEffectLifetime = 1.2f;

    [Tooltip("2페이즈 전환 분열 순간에 재생할 사운드/연출입니다.")]
    [SerializeField] private WorldPresentationHook phase2SplitPresentation = new WorldPresentationHook
    {
        sound = SoundRef.FromKey("sound_slimeQueen_Split1"),
        additionalSounds = new[]
        {
            SoundRef.FromKey("sound_slimeQueen_Split2")
        }
    };

    private SpeechBubbleComponent speechBubble;
    private SlimeQueenVanishParticleEffect phaseOneVanishEffect;
    private Coroutine callSlimeSpeechAnimationRoutine;
    private readonly List<AttackTelegraphView> bodyInflateWarningViews = new List<AttackTelegraphView>();
    private readonly RaycastHit2D[] phase2SplitRaycastHits = new RaycastHit2D[8];
    private readonly Collider2D[] phase2SplitOverlapHits = new Collider2D[12];
    private bool runtimePatternsConfigured;
    private bool hasSpawnedPhaseTwoQueens;

    public float SummonWarningSeconds => summonWarningSeconds;

    public float SummonWarningDiameter => summonWarningDiameter;

    public float FallStartHeight => fallStartHeight;

    public float FallSpeed => fallSpeed;

    public float PostLandingWaitSeconds => postLandingWaitSeconds;

    public float FallContactRadius => Mathf.Max(0.1f, summonWarningDiameter * 0.5f);

    public float JumpDurationSeconds => jumpDurationSeconds;

    public float JumpArcHeight => jumpArcHeight;

    public float CallSlimeSpawnDelaySeconds => callSlimeSpawnDelaySeconds;

    public float CallSlimeSpeechSeconds => callSlimeSpeechSeconds;

    public float BodyInflateWarningSeconds => bodyInflateWarningSeconds;

    public void BeginRandomJumpAnimation()
    {
        SetAnimatorBool(IsJumpingHash, true);
    }

    public void EndRandomJumpAnimation()
    {
        SetAnimatorBool(IsJumpingHash, false);
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
        BeginBodyInflateVisualScale();
        SetAnimatorBool(IsGiantizationHash, true);
    }

    public void EndBodyInflateImpactAnimation()
    {
        EndBodyInflateVisualScale();
        SetAnimatorBool(IsGiantizationHash, false);
    }

    protected override void Awake()
    {
        base.Awake();
        speechBubble = GetComponent<SpeechBubbleComponent>();
        EnsurePhaseOneVanishEffect();
    }

    protected override void Start()
    {
        if (configureRuntimePatternsOnStart || ConfiguredPhaseCount == 0)
            ConfigureRuntimePatternsIfNeeded();

        base.Start();
    }

    private void OnValidate()
    {
        phase2SplitDistance = Mathf.Max(0f, phase2SplitDistance);
        phase2SplitLandingSeconds = Mathf.Max(0.01f, phase2SplitLandingSeconds);
        phase2SplitLandingArcHeight = Mathf.Max(0f, phase2SplitLandingArcHeight);
        phase2SplitVanishLeadSeconds = Mathf.Max(0f, phase2SplitVanishLeadSeconds);
        phase2SplitLandingProbeRadius = Mathf.Max(0.01f, phase2SplitLandingProbeRadius);
        phase2SplitVanishEffectLifetime = Mathf.Max(0.1f, phase2SplitVanishEffectLifetime);
    }

    protected override void OnDestroy()
    {
        CleanupBodyInflatePresentation();
        base.OnDestroy();
    }

    private void OnDisable()
    {
        CleanupBodyInflatePresentation();
    }

    protected override void OnPatternEnd(BossPatternEntry patternEntry, bool forced)
    {
        CleanupBodyInflatePresentation();
        if (forced)
            ResetPatternAnimatorStateForInterrupt();

        base.OnPatternEnd(patternEntry, forced);
    }

    protected override void ResetPatternAnimatorStateForInterrupt()
    {
        StopCallSlimeSpeechAnimation();
        EndBodyInflateVisualScale(resetImmediately: true);
        SetAnimatorBoolIfExists(IsJumpingHash, false);
        SetAnimatorBoolIfExists(IsShoutingHash, false);
        ResetAnimatorTriggerIfExists(ReadyTriggerHash);
        SetAnimatorBoolIfExists(IsGiantizationHash, false);
        PlayAnimatorStateIfExists(IdleStateHash);
    }

    /// <summary>소환 위치 경고 원을 AttackTelegraph로 표시합니다.</summary>
    public void ShowSummonWarning(Vector3 landingPosition)
    {
        AttackTelegraphService service = GetTelegraphService();
        if (service == null)
            return;

        AttackTelegraphSpec spec = WithThinWarningOutline(AttackTelegraphSpec.CreateCircle(
            landingPosition,
            summonWarningDiameter,
            summonWarningSeconds,
            summonWarningStyle));

        service.SpawnDetachedView(spec);
    }

    /// <summary>패턴 2의 랜덤 점프 착지 경고 원을 표시합니다.</summary>
    public void ShowJumpWarning(Vector3 landingPosition)
    {
        AttackTelegraphService service = GetTelegraphService();
        if (service == null)
            return;

        AttackTelegraphSpec spec = WithThinWarningOutline(AttackTelegraphSpec.CreateCircle(
            landingPosition,
            jumpWarningDiameter,
            jumpDurationSeconds,
            jumpWarningStyle));

        service.SpawnDetachedView(spec);
    }

    /// <summary>Knight와 Wizard 중 현재 사용 가능한 중형 슬라임 프리팹을 무작위로 반환합니다.</summary>
    public GameObject GetRandomMediumSlimePrefab()
    {
        if (knightPrefab == null && wizardPrefab == null)
            return null;

        if (knightPrefab == null)
            return wizardPrefab;

        if (wizardPrefab == null)
            return knightPrefab;

        return Random.value < 0.5f ? knightPrefab : wizardPrefab;
    }

    /// <summary>Bishop과 Rook 중 현재 사용 가능한 대형 슬라임 프리팹을 무작위로 반환합니다.</summary>
    public GameObject GetRandomLargeSlimePrefab()
    {
        if (bishopPrefab == null && rookPrefab == null)
            return null;

        if (bishopPrefab == null)
            return rookPrefab;

        if (rookPrefab == null)
            return bishopPrefab;

        return Random.value < 0.5f ? bishopPrefab : rookPrefab;
    }

    /// <summary>선택된 중형 슬라임의 낙하 연출 오브젝트를 생성합니다.</summary>
    public SlimeQueenFallingSummon SpawnFallingMediumSlime(
        GameObject summonPrefab,
        AbilitySpec sourceSpec,
        Vector3 landingPosition,
        WorldPresentationHook landingPresentation = default,
        Object presentationSourceObject = null)
    {
        if (summonPrefab == null)
            return null;

        SpriteRenderer sourceRenderer = summonPrefab.GetComponentInChildren<SpriteRenderer>(true);
        Vector3 startPosition = landingPosition + Vector3.up * fallStartHeight;

        return SlimeQueenFallingSummon.Create(
            this,
            sourceSpec,
            summonPrefab,
            sourceRenderer,
            startPosition,
            landingPosition,
            fallSpeed,
            postLandingWaitSeconds,
            FallContactRadius,
            CurrentTarget,
            landingPresentation,
            presentationSourceObject);
    }

    /// <summary>패턴 3 호출 대사를 보스 말풍선으로 출력합니다.</summary>
    public void ShowCallSlimeSpeech()
    {
        if (string.IsNullOrWhiteSpace(callSlimeSpeechText))
        {
            StopCallSlimeSpeechAnimation();
            return;
        }

        BeginCallSlimeSpeechAnimation();

        if (speechBubble == null)
            speechBubble = GetComponent<SpeechBubbleComponent>();

        if (speechBubble != null)
        {
            speechBubble.Speak(callSlimeSpeechText, callSlimeSpeechSeconds);
            return;
        }

        Debug.Log($"SlimeQueen: {callSlimeSpeechText}", this);
    }

    private void BeginCallSlimeSpeechAnimation()
    {
        if (callSlimeSpeechAnimationRoutine != null)
            StopCoroutine(callSlimeSpeechAnimationRoutine);

        SetAnimatorBool(IsShoutingHash, true);

        if (callSlimeSpeechSeconds <= 0f)
        {
            SetAnimatorBool(IsShoutingHash, false);
            callSlimeSpeechAnimationRoutine = null;
            return;
        }

        callSlimeSpeechAnimationRoutine = StartCoroutine(EndCallSlimeSpeechAnimationAfterDelay(callSlimeSpeechSeconds));
    }

    private IEnumerator EndCallSlimeSpeechAnimationAfterDelay(float duration)
    {
        yield return new WaitForSeconds(duration);

        SetAnimatorBool(IsShoutingHash, false);
        callSlimeSpeechAnimationRoutine = null;
    }

    private void StopCallSlimeSpeechAnimation()
    {
        if (callSlimeSpeechAnimationRoutine != null)
        {
            StopCoroutine(callSlimeSpeechAnimationRoutine);
            callSlimeSpeechAnimationRoutine = null;
        }

        SetAnimatorBool(IsShoutingHash, false);
    }

    /// <summary>낙하 중형 슬라임의 플레이어 접촉 피해를 GAS Damage Effect로 적용합니다.</summary>
    public void ApplyFallingSummonDamage(AbilitySpec sourceSpec, GameObject hitTarget, Vector3 hitWorldPosition)
    {
        if (fallingContactDamage <= 0f || fallingContactDamageEffect == null || hitTarget == null)
            return;

        CombatDamageAction.ApplyDamageAndEmitHit(
            AbilitySystem,
            sourceSpec,
            fallingContactDamageEffect,
            null,
            hitTarget,
            fallingContactDamage,
            0f,
            0f,
            null,
            hitWorldPosition,
            gameObject);
    }

    /// <summary>패턴 2의 랜덤 착지 위치를 바운더리에서 가져옵니다.</summary>
    public bool TryGetRandomJumpLandingPosition(out Vector3 landingPosition)
    {
        SlimeQueenRandomMoveBounds bounds = ResolveRandomMoveBounds();
        if (bounds == null)
        {
            landingPosition = transform.position;
            return false;
        }

        return bounds.TryGetRandomPoint(transform.position.z, out landingPosition);
    }

    /// <summary>패턴 3에서 서로 다른 중형/대형 슬라임 소환 좌표를 뽑습니다.</summary>
    public bool TryGetCallSlimeSpawnPositions(out Vector3 mediumSpawnPosition, out Vector3 largeSpawnPosition)
    {
        if (callSlimeSpawnPositions == null || callSlimeSpawnPositions.Length < 2)
        {
            mediumSpawnPosition = transform.position;
            largeSpawnPosition = transform.position;
            return false;
        }

        int mediumIndex = Random.Range(0, callSlimeSpawnPositions.Length);
        int largeIndex = Random.Range(0, callSlimeSpawnPositions.Length - 1);
        if (largeIndex >= mediumIndex)
            largeIndex++;

        Vector2 mediumPosition = callSlimeSpawnPositions[mediumIndex];
        Vector2 largePosition = callSlimeSpawnPositions[largeIndex];
        mediumSpawnPosition = new Vector3(mediumPosition.x, mediumPosition.y, transform.position.z);
        largeSpawnPosition = new Vector3(largePosition.x, largePosition.y, transform.position.z);
        return true;
    }

    /// <summary>패턴 4 몸 부풀림 원형 경고를 보스 위치에 표시합니다.</summary>
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

    /// <summary>패턴 4 몸 부풀림 범위 안의 플레이어에게 피해와 넉백을 적용합니다.</summary>
    public void ApplyBodyInflateImpact(AbilitySpec sourceSpec)
    {
        bool hasDamage = bodyInflateImpactDamage > 0f;
        bool hasKnockback = bodyInflateImpactKnockbackImpulse > 0f && bodyInflateImpactKnockbackEffect != null;

        if ((!hasDamage && !hasKnockback) || CurrentTarget == null)
            return;

        GE_Damage_Spec damageEffect = null;
        if (hasDamage)
        {
            damageEffect = bodyInflateImpactDamageEffect != null
                ? bodyInflateImpactDamageEffect
                : fallingContactDamageEffect;

            if (damageEffect == null)
                return;
        }

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
                damageEffect,
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

    /// <summary>착지 위치 위로 빠르게 올라가 체공한 뒤 급강하하는 자세를 적용합니다.</summary>
    public void SetJumpPose(Vector3 startPosition, Vector3 landingPosition, float normalizedTime)
    {
        ApplyKnightStyleSlamPose(startPosition, landingPosition, normalizedTime, jumpArcHeight);
    }

    /// <summary>점프 종료 위치로 보스 좌표를 확정합니다.</summary>
    public void SnapToJumpLanding(Vector3 landingPosition)
    {
        SnapToGroundedMotionLanding(landingPosition);
        EndRandomJumpAnimation();
    }

    private void SetAnimatorBool(int parameterHash, bool value)
    {
        if (animator == null)
            return;

        animator.SetBool(parameterHash, value);
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

    /// <summary>패턴 2 착지 범위 안의 현재 타겟에게 GAS Damage Effect를 적용합니다.</summary>
    public void ApplyJumpLandingDamage(AbilitySpec sourceSpec, Vector3 landingPosition)
    {
        PlayLightSlamLandingCameraShake("SlimeQueen.JumpLanding");

        if (jumpLandingDamage <= 0f || CurrentTarget == null)
            return;

        float damageRadius = Mathf.Max(0.1f, jumpLandingDamageDiameter * 0.5f);
        float sqrDistance = ((Vector2)(CurrentTarget.position - landingPosition)).sqrMagnitude;
        if (sqrDistance > damageRadius * damageRadius)
            return;

        GE_Damage_Spec damageEffect = jumpLandingDamageEffect != null
            ? jumpLandingDamageEffect
            : fallingContactDamageEffect;

        if (damageEffect == null)
            return;

        CombatDamageAction.ApplyDamageAndEmitHit(
            AbilitySystem,
            sourceSpec,
            damageEffect,
            null,
            CurrentTarget.gameObject,
            jumpLandingDamage,
            0f,
            0f,
            null,
            landingPosition,
            gameObject);
    }

    /// <summary>1페이즈 사망 애니메이션 이후 2페이즈 근거리/원거리 퀸을 생성합니다.</summary>
    protected override void DestroyAfterDelay()
    {
        StartCoroutine(WaitForDeathAnimationAndSplit());
    }

    /// <summary>사망 애니메이션 대기 후 2페이즈 프리팹을 생성하고 1페이즈 본체를 제거합니다.</summary>
    private IEnumerator WaitForDeathAnimationAndSplit()
    {
        float elapsedSeconds = 0f;
        float waitSeconds = ResolveDeathSplitDelay();

        while (elapsedSeconds < waitSeconds)
        {
            elapsedSeconds += Time.deltaTime;
            yield return null;
        }

        Vector3 vanishPosition = transform.position;
        Vector3 splitOrigin = ResolvePhaseTwoSplitOrigin();
        PlayPhaseTwoSplitVanishEffect(vanishPosition);
        SetPhaseOneVisualsVisible(false);

        elapsedSeconds = 0f;
        while (elapsedSeconds < phase2SplitVanishLeadSeconds)
        {
            elapsedSeconds += Time.deltaTime;
            yield return null;
        }

        SpawnPhaseTwoQueens(splitOrigin);
        Destroy(gameObject);
    }

    /// <summary>설정된 근거리/원거리 2페이즈 퀸 프리팹을 각각 생성합니다.</summary>
    private void SpawnPhaseTwoQueens(Vector3 splitOrigin)
    {
        if (hasSpawnedPhaseTwoQueens)
            return;

        hasSpawnedPhaseTwoQueens = true;
        ResolvePhaseTwoSplitLandingPair(splitOrigin, out Vector3 shortLandingPosition, out Vector3 longLandingPosition);
        SpawnPhaseTwoQueen(phase2ShortPrefab, splitOrigin, shortLandingPosition, "SlimeQueenP2Short");
        SpawnPhaseTwoQueen(phase2LongPrefab, splitOrigin, longLandingPosition, "SlimeQueenP2Long");
    }

    /// <summary>2페이즈 퀸 프리팹 하나를 분열 원점에 생성하고 착지 연출을 시작합니다.</summary>
    private TQueen SpawnPhaseTwoQueen<TQueen>(TQueen prefab, Vector3 splitOrigin, Vector3 landingPosition, string fallbackName)
        where TQueen : SlimeQueenPhaseTwoBase
    {
        if (prefab == null)
        {
            Debug.LogWarning($"[BossFSM] SlimeQueen: {fallbackName} 프리팹이 비어 있어 2페이즈 개체를 생성하지 못했습니다.", this);
            return null;
        }

        TQueen spawnedQueen = Instantiate(prefab, splitOrigin, transform.rotation);
        spawnedQueen.name = fallbackName;
        spawnedQueen.SetCombatTarget(CurrentTarget);
        spawnedQueen.BeginPhaseTwoSplitLanding(
            splitOrigin,
            landingPosition,
            phase2SplitLandingSeconds,
            phase2SplitLandingArcHeight);
        return spawnedQueen;
    }

    /// <summary>분열 연출 원점을 1페이즈 보스 root 위치로 결정합니다.</summary>
    private Vector3 ResolvePhaseTwoSplitOrigin()
    {
        return transform.position;
    }

    /// <summary>두 2페이즈 퀸이 서로 반대 방향으로, 구덩이/배수구를 피해서 착지할 좌표를 계산합니다.</summary>
    private void ResolvePhaseTwoSplitLandingPair(Vector3 splitOrigin, out Vector3 shortLandingPosition, out Vector3 longLandingPosition)
    {
        float splitDistance = Mathf.Max(0f, phase2SplitDistance);
        if (splitDistance <= 0f)
        {
            ResolveLegacyPhaseTwoSplitOffsets(splitOrigin, out shortLandingPosition, out longLandingPosition);
            return;
        }

        float bestScore = -1f;
        Vector3 bestShortLanding = splitOrigin;
        Vector3 bestLongLanding = splitOrigin;

        for (int i = 0; i < PhaseTwoSplitDirectionAttempts; i++)
        {
            Vector2 direction = CreateRandomPhaseTwoSplitDirection();
            Vector3 shortCandidate = ResolvePhaseTwoSplitLandingPosition(splitOrigin, direction, splitDistance);
            Vector3 longCandidate = ResolvePhaseTwoSplitLandingPosition(splitOrigin, -direction, splitDistance);
            float shortDistance = Vector2.Distance(splitOrigin, shortCandidate);
            float longDistance = Vector2.Distance(splitOrigin, longCandidate);
            float separation = Vector2.Distance(shortCandidate, longCandidate);
            float score = shortDistance + longDistance + separation;

            if (score > bestScore)
            {
                bestScore = score;
                bestShortLanding = shortCandidate;
                bestLongLanding = longCandidate;
            }

            bool enoughSplit = separation >= Mathf.Max(0.8f, splitDistance * 1.25f);
            bool enoughDistance = shortDistance >= splitDistance * 0.45f && longDistance >= splitDistance * 0.45f;
            if (enoughSplit && enoughDistance)
            {
                shortLandingPosition = shortCandidate;
                longLandingPosition = longCandidate;
                return;
            }
        }

        if (bestScore > 0f)
        {
            shortLandingPosition = bestShortLanding;
            longLandingPosition = bestLongLanding;
            return;
        }

        ResolveLegacyPhaseTwoSplitOffsets(splitOrigin, out shortLandingPosition, out longLandingPosition);
    }

    /// <summary>기존 serialized 오프셋을 최후 fallback 후보로 사용하되 착지 안전 검사를 통과시킵니다.</summary>
    private void ResolveLegacyPhaseTwoSplitOffsets(Vector3 splitOrigin, out Vector3 shortLandingPosition, out Vector3 longLandingPosition)
    {
        LayerMask blockedLayers = ResolvePhaseTwoSplitBlockedLayers();
        Vector3 shortCandidate = ClampPhaseTwoSplitPointToBounds(splitOrigin + new Vector3(phase2ShortSpawnOffset.x, phase2ShortSpawnOffset.y, 0f));
        Vector3 longCandidate = ClampPhaseTwoSplitPointToBounds(splitOrigin + new Vector3(phase2LongSpawnOffset.x, phase2LongSpawnOffset.y, 0f));
        shortLandingPosition = ResolveSafePhaseTwoSplitLanding(splitOrigin, shortCandidate, blockedLayers);
        longLandingPosition = ResolveSafePhaseTwoSplitLanding(splitOrigin, longCandidate, blockedLayers);
    }

    /// <summary>분열 방향을 랜덤 각도로 생성합니다.</summary>
    private static Vector2 CreateRandomPhaseTwoSplitDirection()
    {
        float angleRadians = Random.Range(0f, Mathf.PI * 2f);
        return new Vector2(Mathf.Cos(angleRadians), Mathf.Sin(angleRadians));
    }

    /// <summary>한 방향의 착지 후보를 장애물/기믹 충돌을 피해 보정합니다.</summary>
    private Vector3 ResolvePhaseTwoSplitLandingPosition(Vector3 splitOrigin, Vector2 direction, float splitDistance)
    {
        if (direction.sqrMagnitude <= 0.0001f || splitDistance <= 0f)
            return splitOrigin;

        LayerMask blockedLayers = ResolvePhaseTwoSplitBlockedLayers();
        Vector2 normalizedDirection = direction.normalized;
        Vector2 start = splitOrigin;
        float safeDistance = splitDistance;

        if (blockedLayers.value != 0)
        {
            RaycastHit2D blockerHit = FindNearestPhaseTwoSplitBlocker(start, normalizedDirection, splitDistance, blockedLayers);
            if (blockerHit.collider != null)
                safeDistance = Mathf.Max(0f, blockerHit.distance - PhaseTwoSplitBlockedSkin);
        }

        Vector3 candidate = splitOrigin + (Vector3)(normalizedDirection * safeDistance);
        candidate = ClampPhaseTwoSplitPointToBounds(candidate);
        return ResolveSafePhaseTwoSplitLanding(splitOrigin, candidate, blockedLayers);
    }

    /// <summary>후보 착지점이 구덩이/배수구/벽과 겹치면 되돌림과 주변 탐색으로 안전 좌표를 찾습니다.</summary>
    private Vector3 ResolveSafePhaseTwoSplitLanding(Vector3 splitOrigin, Vector3 candidate, LayerMask blockedLayers)
    {
        if (IsPhaseTwoSplitLandingSafe(candidate, blockedLayers))
            return candidate;

        for (int i = 1; i <= PhaseTwoSplitSafeLandingResolveSteps; i++)
        {
            float t = 1f - (float)i / PhaseTwoSplitSafeLandingResolveSteps;
            Vector3 fallback = Vector3.Lerp(splitOrigin, candidate, t);
            if (IsPhaseTwoSplitLandingSafe(fallback, blockedLayers))
                return fallback;
        }

        if (TryFindNearbySafePhaseTwoSplitLanding(splitOrigin, candidate, blockedLayers, out Vector3 nearbySafeLanding))
            return nearbySafeLanding;

        Vector3 clampedOrigin = ClampPhaseTwoSplitPointToBounds(splitOrigin);
        return IsPhaseTwoSplitLandingSafe(clampedOrigin, blockedLayers) ? clampedOrigin : splitOrigin;
    }

    /// <summary>분열 원점 주변을 원형으로 훑어 기존 후보와 가까운 안전 착지점을 찾습니다.</summary>
    private bool TryFindNearbySafePhaseTwoSplitLanding(Vector3 splitOrigin, Vector3 preferredCandidate, LayerMask blockedLayers, out Vector3 safeLanding)
    {
        safeLanding = splitOrigin;

        Vector2 preferredOffset = preferredCandidate - splitOrigin;
        float preferredDistance = preferredOffset.magnitude;
        float maxRadius = Mathf.Max(phase2SplitLandingProbeRadius * 2f, preferredDistance, phase2SplitDistance);
        if (maxRadius <= 0.0001f)
            return false;

        float minRadius = Mathf.Max(phase2SplitLandingProbeRadius * 1.25f, maxRadius * PhaseTwoSplitSafeLandingMinRadiusScale);
        float baseAngle = preferredOffset.sqrMagnitude > 0.0001f
            ? Mathf.Atan2(preferredOffset.y, preferredOffset.x)
            : 0f;

        for (int radialIndex = 0; radialIndex < PhaseTwoSplitSafeLandingRadialSteps; radialIndex++)
        {
            float radialT = PhaseTwoSplitSafeLandingRadialSteps <= 1
                ? 1f
                : (float)radialIndex / (PhaseTwoSplitSafeLandingRadialSteps - 1);
            float radius = Mathf.Lerp(maxRadius, minRadius, radialT);

            for (int angleIndex = 0; angleIndex < PhaseTwoSplitSafeLandingAngleSteps; angleIndex++)
            {
                int signedStep = angleIndex == 0
                    ? 0
                    : ((angleIndex + 1) / 2) * (angleIndex % 2 == 1 ? 1 : -1);
                float angle = baseAngle + signedStep * (Mathf.PI * 2f / PhaseTwoSplitSafeLandingAngleSteps);
                Vector3 candidate = splitOrigin + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
                candidate.z = splitOrigin.z;
                candidate = ClampPhaseTwoSplitPointToBounds(candidate);

                if (!IsPhaseTwoSplitLandingSafe(candidate, blockedLayers))
                    continue;

                safeLanding = candidate;
                return true;
            }
        }

        return false;
    }

    /// <summary>분열 착지점이 바운더리 밖으로 나가지 않도록 보정합니다.</summary>
    private Vector3 ClampPhaseTwoSplitPointToBounds(Vector3 point)
    {
        SlimeQueenRandomMoveBounds bounds = ResolveRandomMoveBounds();
        if (bounds != null && bounds.TryClampPoint(point, out Vector3 clampedPoint))
            return clampedPoint;

        return point;
    }

    /// <summary>착지점에 장애물 또는 구덩이/배수구 기믹이 없는지 확인합니다.</summary>
    private bool IsPhaseTwoSplitLandingSafe(Vector3 position, LayerMask blockedLayers)
    {
        return !IsPhaseTwoSplitLandingBlocked(position, blockedLayers) &&
               !IsPhaseTwoSplitLandingOnHazard(position);
    }

    /// <summary>분열 착지점 검사에 사용할 충돌체 레이어를 가져옵니다.</summary>
    private LayerMask ResolvePhaseTwoSplitBlockedLayers()
    {
        if (phase2SplitBlockedLayers.value != 0)
            return phase2SplitBlockedLayers;

        int mask = 0;
        int wallLayer = LayerMask.NameToLayer("Wall");
        int defaultLayer = LayerMask.NameToLayer("Default");
        int nonFightCollisionLayer = LayerMask.NameToLayer("Non_FightCollision");

        if (wallLayer >= 0)
            mask |= 1 << wallLayer;
        if (defaultLayer >= 0)
            mask |= 1 << defaultLayer;
        if (nonFightCollisionLayer >= 0)
            mask |= 1 << nonFightCollisionLayer;

        return mask;
    }

    /// <summary>분열 이동 경로에서 가장 가까운 non-trigger 장애물을 찾습니다.</summary>
    private RaycastHit2D FindNearestPhaseTwoSplitBlocker(Vector2 start, Vector2 direction, float distance, LayerMask blockedLayers)
    {
        ContactFilter2D filter = new()
        {
            useLayerMask = true,
            layerMask = blockedLayers,
            useTriggers = false
        };

        int hitCount = Physics2D.Raycast(start, direction, filter, phase2SplitRaycastHits, distance);
        RaycastHit2D nearestHit = default;
        float nearestDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = phase2SplitRaycastHits[i];
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
                continue;

            if (hit.distance < nearestDistance)
            {
                nearestHit = hit;
                nearestDistance = hit.distance;
            }
        }

        return nearestHit;
    }

    /// <summary>착지점이 non-trigger 장애물과 겹치는지 검사합니다.</summary>
    private bool IsPhaseTwoSplitLandingBlocked(Vector3 position, LayerMask blockedLayers)
    {
        if (blockedLayers.value == 0)
            return false;

        ContactFilter2D filter = new()
        {
            useLayerMask = true,
            layerMask = blockedLayers,
            useTriggers = false
        };

        int hitCount = Physics2D.OverlapCircle(position, phase2SplitLandingProbeRadius, filter, phase2SplitOverlapHits);
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = phase2SplitOverlapHits[i];
            if (hit != null && !hit.transform.IsChildOf(transform))
                return true;
        }

        return false;
    }

    /// <summary>착지점이 구덩이 또는 배수구 근처인지 검사합니다.</summary>
    private bool IsPhaseTwoSplitLandingOnHazard(Vector3 position)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(position, phase2SplitLandingProbeRadius);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit.transform.IsChildOf(transform))
                continue;

            if (hit.GetComponent<HoleTrap>() != null || hit.GetComponentInParent<HoleTrap>() != null)
                return true;

            if (hit.GetComponent<DrainPipe>() != null || hit.GetComponentInParent<DrainPipe>() != null)
                return true;
        }

        DrainPipe[] drainPipes = FindObjectsByType<DrainPipe>(FindObjectsInactive.Exclude);
        for (int i = 0; i < drainPipes.Length; i++)
        {
            DrainPipe drainPipe = drainPipes[i];
            if (drainPipe != null && drainPipe.ContainsActivePhaseTwoBossSuctionPoint(position, phase2SplitLandingProbeRadius))
                return true;
        }

        return false;
    }

    /// <summary>1페이즈 사망 위치에 소멸 파티클을 출력합니다.</summary>
    private void PlayPhaseTwoSplitVanishEffect(Vector3 position)
    {
        SlimeQueenPresentationAudioUtility.PlayPresentation(
            phase2SplitPresentation,
            gameObject,
            position,
            this,
            CurrentTarget != null ? CurrentTarget.gameObject : null);

        if (phase2SplitVanishEffectPrefab != null)
        {
            GameObject effect = Instantiate(phase2SplitVanishEffectPrefab, position, Quaternion.identity);
            Destroy(effect, phase2SplitVanishEffectLifetime);
            return;
        }

        EnsurePhaseOneVanishEffect();
        if (phaseOneVanishEffect != null)
        {
            phaseOneVanishEffect.SpawnOneShot(position, sprite);
            return;
        }

        CreateFallbackPhaseTwoSplitVanishEffect(position);
    }

    private void EnsurePhaseOneVanishEffect()
    {
        if (phaseOneVanishEffect == null)
            phaseOneVanishEffect = GetComponent<SlimeQueenVanishParticleEffect>();

        if (phaseOneVanishEffect == null)
            phaseOneVanishEffect = gameObject.AddComponent<SlimeQueenVanishParticleEffect>();
    }

    /// <summary>별도 프리팹이 없을 때 사용하는 임시 초록 소멸 파티클입니다.</summary>
    private void CreateFallbackPhaseTwoSplitVanishEffect(Vector3 position)
    {
        GameObject effect = new GameObject("SlimeQueenPhaseTwoSplitVanish");
        effect.transform.position = position;

        ParticleSystem particles = effect.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.duration = 0.45f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.55f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 2.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.26f, 1f, 0.34f, 0.9f),
            new Color(0.03f, 0.65f, 0.12f, 0.35f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)42) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.12f;

        ParticleSystemRenderer particleRenderer = effect.GetComponent<ParticleSystemRenderer>();
        if (particleRenderer != null && sprite != null)
        {
            particleRenderer.sortingLayerID = sprite.sortingLayerID;
            particleRenderer.sortingOrder = sprite.sortingOrder + 3;
        }

        Destroy(effect, phase2SplitVanishEffectLifetime);
    }

    /// <summary>소멸 연출 이후 1페이즈 본체 표시를 숨깁니다.</summary>
    private void SetPhaseOneVisualsVisible(bool isVisible)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = isVisible;
        }
    }

    /// <summary>사망 애니메이션 클립 길이를 기준으로 분열 생성 대기 시간을 계산합니다.</summary>
    private float ResolveDeathSplitDelay()
    {
        float clipLength = FindAnimationClipLength($"{EnemyName}_Die", "die");
        return clipLength > 0f ? clipLength + 0.05f : 0.35f;
    }

    /// <summary>SlimeQueen 기본 페이즈와 패턴 구성을 런타임으로 생성합니다.</summary>
    private void ConfigureRuntimePatternsIfNeeded()
    {
        if (runtimePatternsConfigured)
            return;

        runtimePatternsConfigured = true;

        BossPatternEntry dropMediumSlime = CreatePattern<AbilityLogic_SlimeQueenDropMediumSlime>(
            "SlimeQueen_DropMediumSlime",
            weight: 100,
            maxConsecutive: 0,
            lockTime: patternSelectionLockSeconds,
            postDelay: postPatternDelaySeconds,
            minDistance: 0f,
            maxDistance: 999f);

        BossPatternEntry randomJump = CreatePattern<AbilityLogic_SlimeQueenRandomJump>(
            "SlimeQueen_RandomJump",
            weight: 100,
            maxConsecutive: 1,
            lockTime: patternSelectionLockSeconds,
            postDelay: postPatternDelaySeconds,
            minDistance: 0f,
            maxDistance: 999f);

        BossPatternEntry callSlimes = CreatePattern<AbilityLogic_SlimeQueenCallSlimes>(
            "SlimeQueen_CallSlimes",
            weight: 100,
            maxConsecutive: 1,
            lockTime: patternSelectionLockSeconds,
            postDelay: postPatternDelaySeconds,
            minDistance: 0f,
            maxDistance: 999f);

        BossPatternEntry bodyInflateImpact = CreatePattern<AbilityLogic_SlimeQueenBodyInflateImpact>(
            "SlimeQueen_BodyInflateImpact",
            weight: 100,
            maxConsecutive: 1,
            lockTime: patternSelectionLockSeconds,
            postDelay: postPatternDelaySeconds,
            minDistance: 0f,
            maxDistance: 999f);

        SetRuntimePhases(new[]
        {
            BossPhaseConfig.CreateRuntime(
                "Slime Queen Phase 1",
                1f,
                0.4f,
                0.8f,
                dropMediumSlime,
                randomJump,
                callSlimes,
                bodyInflateImpact)
        });
    }

    /// <summary>보스 FSM이 실행할 런타임 GAS 패턴 엔트리를 생성합니다.</summary>
    private BossPatternEntry CreatePattern<TLogic>(
        string abilityName,
        int weight,
        int maxConsecutive,
        float lockTime,
        float postDelay,
        float minDistance,
        float maxDistance)
        where TLogic : AbilityLogic
    {
        TLogic logic = ScriptableObject.CreateInstance<TLogic>();
        logic.name = $"AL_{abilityName}";

        AbilityDefinition ability = ScriptableObject.CreateInstance<AbilityDefinition>();
        ability.name = $"AD_{abilityName}";
        ability.abilityName = abilityName;
        ability.castTime = 0f;
        ability.recoveryTime = 0f;
        ability.canCastWhileMoving = true;
        ability.interruptible = true;
        ability.executionPolicy = AbilityDefinition.ExecutionPolicy.ExclusiveQueued;
        ability.logic = logic;

        return BossPatternEntry.CreateRuntime(
            ability,
            weight,
            maxConsecutive,
            0,
            lockTime,
            postDelay,
            minDistance,
            maxDistance,
            0f,
            1f);
    }

    /// <summary>패턴 2 바운더리 참조를 인스펙터 또는 씬 자동 탐색으로 해결합니다.</summary>
    private SlimeQueenRandomMoveBounds ResolveRandomMoveBounds()
    {
        if (randomMoveBounds == null)
            randomMoveBounds = FindAnyObjectByType<SlimeQueenRandomMoveBounds>();

        return randomMoveBounds;
    }
}
