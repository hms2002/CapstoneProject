using UnityEngine;
using UnityGAS;

public sealed class SlimeQueen : BossControllerBase, IIntentMovementSource2D, IDamageReceiver, IBossSplitHealthPresentation
{
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

    [Tooltip("점프 중간 지점에서 올라갈 포물선 높이입니다.")]
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
    [SerializeField, Min(0.1f)] private float bodyInflateWarningDiameter = 4f;

    [Tooltip("패턴 4 몸 부풀림 경고가 유지되는 시간입니다.")]
    [SerializeField, Min(0f)] private float bodyInflateWarningSeconds = 1.4f;

    [Tooltip("패턴 4 몸 부풀림 실제 피해 판정 원의 지름입니다.")]
    [SerializeField, Min(0.1f)] private float bodyInflateImpactDiameter = 4f;

    [Tooltip("패턴 4 몸 부풀림이 플레이어에게 주는 피해량입니다.")]
    [SerializeField, Min(0f)] private float bodyInflateImpactDamage = 1.5f;

    [Tooltip("패턴 4 몸 부풀림 피해에 사용할 GAS Damage Effect입니다. 비워두면 패턴 1 낙하 피해 Effect를 사용합니다.")]
    [SerializeField] private GE_Damage_Spec bodyInflateImpactDamageEffect;

    [Tooltip("패턴 4 몸 부풀림 넉백에 사용할 GAS Knockback Effect입니다.")]
    [SerializeField] private GE_Knockback_Spec bodyInflateImpactKnockbackEffect;

    [Tooltip("패턴 4 몸 부풀림 넉백 세기입니다.")]
    [SerializeField, Min(0f)] private float bodyInflateImpactKnockbackImpulse = 8f;

    [Space(8)]

    [Header("Phase 2")]
    [Tooltip("현재 HP 비율이 이 값 이하가 되면 페이즈 2로 전환합니다.")]
    [SerializeField, Range(0.01f, 1f)] private float phase2EnterHealthRatio = 0.5f;

    [Tooltip("페이즈 2 진입 시 생성할 분신 프리팹입니다. 비워두면 현재 보스 오브젝트를 복제합니다.")]
    [SerializeField] private SlimeQueen phase2TwinPrefab;

    [Tooltip("페이즈 2 분신을 원본 위치 기준으로 생성할 오프셋입니다.")]
    [SerializeField] private Vector2 phase2TwinSpawnOffset = new Vector2(2.5f, 0f);

    [Tooltip("페이즈 2에서 플레이어와 접촉했을 때 적용할 피해량입니다.")]
    [SerializeField, Min(0f)] private float phase2ContactDamage = 1f;

    [Tooltip("페이즈 2 접촉 피해를 다시 적용할 수 있는 최소 간격입니다.")]
    [SerializeField, Min(0f)] private float phase2ContactDamageCooldownSeconds = 1f;

    [Tooltip("페이즈 2 접촉 피해에 사용할 GAS Damage Effect입니다. 비워두면 패턴 1 낙하 피해 Effect를 사용합니다.")]
    [SerializeField] private GE_Damage_Spec phase2ContactDamageEffect;

    [Space(8)]

    [Header("Phase 2 - Pattern 1")]
    [Tooltip("연속 내려찍기 경고 표시에 사용할 AttackTelegraph 스타일입니다.")]
    [SerializeField] private AttackTelegraphStyle phase2SlamWarningStyle;

    [Tooltip("연속 내려찍기 경고 원의 지름입니다.")]
    [SerializeField, Min(0.1f)] private float phase2SlamWarningDiameter = 2.8f;

    [Tooltip("연속 내려찍기 피해 판정 원의 지름입니다.")]
    [SerializeField, Min(0.1f)] private float phase2SlamDamageDiameter = 2.8f;

    [Tooltip("연속 내려찍기 사이의 텀입니다.")]
    [SerializeField, Min(0.1f)] private float phase2SlamIntervalSeconds = 1f;

    [Tooltip("연속 내려찍기를 반복할 횟수입니다.")]
    [SerializeField, Min(1)] private int phase2SlamCount = 3;

    [Tooltip("연속 내려찍기 점프 중간 지점에서 올라갈 포물선 높이입니다.")]
    [SerializeField, Min(0f)] private float phase2SlamArcHeight = 2.8f;

    [Tooltip("연속 내려찍기 착지 시 플레이어에게 적용할 피해량입니다.")]
    [SerializeField, Min(0f)] private float phase2SlamDamage = 1f;

    [Tooltip("연속 내려찍기 피해에 사용할 GAS Damage Effect입니다. 비워두면 패턴 1 낙하 피해 Effect를 사용합니다.")]
    [SerializeField] private GE_Damage_Spec phase2SlamDamageEffect;

    private AttackTelegraphService telegraphService;
    private SpeechBubbleComponent speechBubble;
    private bool runtimePatternsConfigured;
    private bool isPhase2Active;
    private bool isPatternMoveDamageBlocked;
    private bool hasAppliedPatternMoveInvulnerableTag;
    private bool hasSpawnedPhase2Twin;
    private bool isPhase2Twin;
    private float nextPhase2ContactDamageTime;
    private GameplayTag patternMoveInvulnerableTag;
    private SlimeQueen phase2Original;
    private SlimeQueen phase2Twin;

    public float SummonWarningSeconds => summonWarningSeconds;

    public float SummonWarningDiameter => summonWarningDiameter;

    public float FallStartHeight => fallStartHeight;

    public float FallSpeed => fallSpeed;

    public float PostLandingWaitSeconds => postLandingWaitSeconds;

    public float FallContactRadius => Mathf.Max(0.1f, summonWarningDiameter * 0.5f);

    public float JumpDurationSeconds => jumpDurationSeconds;

    public float JumpArcHeight => jumpArcHeight;

    public float CallSlimeSpawnDelaySeconds => callSlimeSpawnDelaySeconds;

    public float BodyInflateWarningSeconds => bodyInflateWarningSeconds;

    public int Phase2SlamCount => Mathf.Max(1, phase2SlamCount);

    public float Phase2SlamIntervalSeconds => Mathf.Max(0.1f, phase2SlamIntervalSeconds);

    /// <summary>보스 HUD가 페이즈 2 분리 체력 표시 여부를 읽습니다.</summary>
    public bool ShowSplitHealthPresentation => isPhase2Active && !isPhase2Twin && phase2Twin != null;

    /// <summary>보스 HUD의 왼쪽 분리 체력 라벨입니다.</summary>
    public string SplitHealthLeftLabel => "본체";

    /// <summary>보스 HUD의 오른쪽 분리 체력 라벨입니다.</summary>
    public string SplitHealthRightLabel => "분신";

    protected override void Awake()
    {
        base.Awake();
        telegraphService = GetComponent<AttackTelegraphService>();
        speechBubble = GetComponent<SpeechBubbleComponent>();
        patternMoveInvulnerableTag = Resources.Load<GameplayTag>("Tags/State.Invulnerable");
    }

    protected override void Start()
    {
        if (configureRuntimePatternsOnStart || ConfiguredPhaseCount == 0)
            ConfigureRuntimePatternsIfNeeded();

        base.Start();
        SetPhase2Active(Blackboard != null && Blackboard.CurrentPhaseIndex >= 1);

        if (isPhase2Twin && phase2Original != null && BossHudController.Instance != null)
        {
            BossHudController.Instance.UnbindBoss(this);
            BossHudController.Instance.BindBoss(phase2Original);
        }
    }

    protected override void Update()
    {
        base.Update();
        FaceCurrentTarget();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryApplyPhase2ContactDamage(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryApplyPhase2ContactDamage(other);
    }

    /// <summary>페이즈 전환 시 SlimeQueen 전용 페이즈 상태를 먼저 갱신합니다.</summary>
    protected override void OnPhaseChanged(int previousPhaseIndex, int nextPhaseIndex)
    {
        SetPhase2Active(nextPhaseIndex >= 1);
        base.OnPhaseChanged(previousPhaseIndex, nextPhaseIndex);
    }

    /// <summary>분신으로 피격된 피해를 원본 체력으로 전달합니다.</summary>
    public bool TryApplyDamage(DamageRequest request)
    {
        if (!isPhase2Twin || phase2Original == null || phase2Original.IsDead)
            return false;

        return phase2Original.TryApplySharedPhase2Damage(request, gameObject);
    }

    /// <summary>보스가 기본 의도 이동을 하지 않도록 빈 이동값을 제공합니다.</summary>
    public IntentMovementData GetIntent()
    {
        return IntentMovementData.None;
    }

    /// <summary>소환 위치 경고 원을 AttackTelegraph로 표시합니다.</summary>
    public void ShowSummonWarning(Vector3 landingPosition)
    {
        AttackTelegraphService service = GetTelegraphService();
        if (service == null)
            return;

        AttackTelegraphSpec spec = AttackTelegraphSpec.CreateCircle(
            landingPosition,
            summonWarningDiameter,
            summonWarningSeconds,
            summonWarningStyle);

        service.SpawnDetachedView(spec);
    }

    /// <summary>페이즈 2 연속 내려찍기 경고 원을 표시합니다.</summary>
    public void ShowPhase2SlamWarning(Vector3 landingPosition)
    {
        AttackTelegraphService service = GetTelegraphService();
        if (service == null)
            return;

        AttackTelegraphSpec spec = AttackTelegraphSpec.CreateCircle(
            landingPosition,
            phase2SlamWarningDiameter,
            Phase2SlamIntervalSeconds,
            phase2SlamWarningStyle);

        service.SpawnDetachedView(spec);
    }

    /// <summary>패턴 2의 랜덤 점프 착지 경고 원을 표시합니다.</summary>
    public void ShowJumpWarning(Vector3 landingPosition)
    {
        AttackTelegraphService service = GetTelegraphService();
        if (service == null)
            return;

        AttackTelegraphSpec spec = AttackTelegraphSpec.CreateCircle(
            landingPosition,
            jumpWarningDiameter,
            jumpDurationSeconds,
            jumpWarningStyle);

        service.SpawnDetachedView(spec);
    }

    /// <summary>이동형 패턴 중 보스 피격과 페이즈2 접촉 피해를 임시로 막습니다.</summary>
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
    public SlimeQueenFallingSummon SpawnFallingMediumSlime(GameObject summonPrefab, AbilitySpec sourceSpec, Vector3 landingPosition)
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
            CurrentTarget);
    }

    /// <summary>패턴 3 호출 대사를 보스 말풍선으로 출력합니다.</summary>
    public void ShowCallSlimeSpeech()
    {
        if (string.IsNullOrWhiteSpace(callSlimeSpeechText))
            return;

        if (speechBubble == null)
            speechBubble = GetComponent<SpeechBubbleComponent>();

        if (speechBubble != null)
        {
            speechBubble.Speak(callSlimeSpeechText, callSlimeSpeechSeconds);
            return;
        }

        Debug.Log($"SlimeQueen: {callSlimeSpeechText}", this);
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
            null,
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
        AttackTelegraphService service = GetTelegraphService();
        if (service == null)
            return;

        AttackTelegraphSpec spec = AttackTelegraphSpec.CreateCircle(
            transform.position,
            bodyInflateWarningDiameter,
            bodyInflateWarningSeconds,
            bodyInflateWarningStyle);

        service.SpawnDetachedView(spec);
    }

    /// <summary>패턴 4 몸 부풀림 범위 안의 플레이어에게 피해와 넉백을 적용합니다.</summary>
    public void ApplyBodyInflateImpact(AbilitySpec sourceSpec)
    {
        if (bodyInflateImpactDamage <= 0f || CurrentTarget == null)
            return;

        GE_Damage_Spec damageEffect = bodyInflateImpactDamageEffect != null
            ? bodyInflateImpactDamageEffect
            : fallingContactDamageEffect;

        if (damageEffect == null)
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
            CombatDamageAction.ApplyDamageAndEmitHit(
                AbilitySystem,
                sourceSpec,
                damageEffect,
                bodyInflateImpactKnockbackEffect,
                contactTarget,
                bodyInflateImpactDamage,
                0f,
                null,
                bodyInflateImpactKnockbackImpulse,
                null,
                hitWorldPosition,
                gameObject);
            return;
        }
    }

    /// <summary>점프 포물선 진행도에 맞춰 보스 위치를 이동시킵니다.</summary>
    public void SetJumpPose(Vector3 startPosition, Vector3 landingPosition, float normalizedTime)
    {
        float clampedTime = Mathf.Clamp01(normalizedTime);
        Vector3 groundPosition = Vector3.Lerp(startPosition, landingPosition, clampedTime);
        float arcOffset = Mathf.Sin(clampedTime * Mathf.PI) * jumpArcHeight;

        if (movementMotor != null)
            movementMotor.StopAllMotion();

        transform.position = groundPosition + Vector3.up * arcOffset;
    }

    /// <summary>점프 종료 위치로 보스 좌표를 확정합니다.</summary>
    public void SnapToJumpLanding(Vector3 landingPosition)
    {
        if (movementMotor != null)
            movementMotor.StopAllMotion();

        transform.position = landingPosition;
    }

    /// <summary>패턴 2 착지 범위 안의 현재 타겟에게 GAS Damage Effect를 적용합니다.</summary>
    public void ApplyJumpLandingDamage(AbilitySpec sourceSpec, Vector3 landingPosition)
    {
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
            null,
            0f,
            null,
            landingPosition,
            gameObject);
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

    /// <summary>페이즈 2 내려찍기 포물선 진행도에 맞춰 보스 위치를 이동시킵니다.</summary>
    public void SetPhase2SlamPose(Vector3 startPosition, Vector3 landingPosition, float normalizedTime)
    {
        float clampedTime = Mathf.Clamp01(normalizedTime);
        Vector3 groundPosition = Vector3.Lerp(startPosition, landingPosition, clampedTime);
        float arcOffset = Mathf.Sin(clampedTime * Mathf.PI) * phase2SlamArcHeight;

        if (movementMotor != null)
            movementMotor.StopAllMotion();

        transform.position = groundPosition + Vector3.up * arcOffset;
    }

    /// <summary>페이즈 2 내려찍기 종료 위치로 보스 좌표를 확정합니다.</summary>
    public void SnapToPhase2SlamLanding(Vector3 landingPosition)
    {
        if (movementMotor != null)
            movementMotor.StopAllMotion();

        transform.position = landingPosition;
    }

    /// <summary>페이즈 2 내려찍기 범위 안의 현재 타겟에게 GAS Damage Effect를 적용합니다.</summary>
    public void ApplyPhase2SlamDamage(AbilitySpec sourceSpec, Vector3 landingPosition)
    {
        if (phase2SlamDamage <= 0f || CurrentTarget == null)
            return;

        float damageRadius = Mathf.Max(0.1f, phase2SlamDamageDiameter * 0.5f);
        float sqrDistance = ((Vector2)(CurrentTarget.position - landingPosition)).sqrMagnitude;
        if (sqrDistance > damageRadius * damageRadius)
            return;

        GE_Damage_Spec damageEffect = phase2SlamDamageEffect != null
            ? phase2SlamDamageEffect
            : fallingContactDamageEffect;

        if (damageEffect == null)
            return;

        CombatDamageAction.ApplyDamageAndEmitHit(
            AbilitySystem,
            sourceSpec,
            damageEffect,
            null,
            CurrentTarget.gameObject,
            phase2SlamDamage,
            0f,
            null,
            0f,
            null,
            landingPosition,
            gameObject);
    }

    /// <summary>현재 타겟 방향에 맞춰 보스 스프라이트 방향을 갱신합니다.</summary>
    public void FaceCurrentTarget()
    {
        if (sprite == null || CurrentTarget == null)
            return;

        if (transform.position.x > CurrentTarget.position.x)
            sprite.flipX = true;
        else if (transform.position.x < CurrentTarget.position.x)
            sprite.flipX = false;
    }

    /// <summary>패턴 종료 시 이동형 패턴 피해 차단 상태를 정리합니다.</summary>
    protected override void OnPatternEnd(BossPatternEntry patternEntry, bool forced)
    {
        SetPatternMoveDamageBlocked(false);
    }

    /// <summary>사망 시작 시 원본이 보유한 분신을 함께 정리합니다.</summary>
    protected override void OnDeathStarted()
    {
        if (!isPhase2Twin)
            DestroyPhase2Twin();

        base.OnDeathStarted();
    }

    protected override void OnDestroy()
    {
        if (!isPhase2Twin)
            DestroyPhase2Twin();
        else if (phase2Original != null && phase2Original.phase2Twin == this)
            phase2Original.phase2Twin = null;

        base.OnDestroy();
    }

    /// <summary>분신은 보상/포탈 생성 없이 제거하고 원본만 보스 사망 보상을 처리합니다.</summary>
    protected override void DestroyAfterDelay()
    {
        if (isPhase2Twin)
        {
            Destroy(gameObject);
            return;
        }

        base.DestroyAfterDelay();
    }

    /// <summary>분신은 체력 비율과 무관하게 페이즈 2 패턴만 평가하게 합니다.</summary>
    protected override int EvaluatePhaseIndexByHealthRatio(float hpRatio)
    {
        if (isPhase2Twin)
            return 1;

        return base.EvaluatePhaseIndexByHealthRatio(hpRatio);
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

        BossPatternEntry repeatedSlam = CreatePattern<AbilityLogic_SlimeQueenRepeatedSlam>(
            "SlimeQueen_RepeatedSlam",
            weight: 100,
            maxConsecutive: 0,
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
                bodyInflateImpact),
            BossPhaseConfig.CreateRuntime(
                "Slime Queen Phase 2",
                phase2EnterHealthRatio,
                0.25f,
                0.5f,
                repeatedSlam)
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

    /// <summary>AttackTelegraphService 참조를 반환합니다.</summary>
    private AttackTelegraphService GetTelegraphService()
    {
        if (telegraphService == null)
            telegraphService = GetComponent<AttackTelegraphService>();

        return telegraphService;
    }

    /// <summary>페이즈 2 접촉 피해 활성 상태를 갱신합니다.</summary>
    private void SetPhase2Active(bool active)
    {
        if (isPhase2Active == active)
            return;

        isPhase2Active = active;
        nextPhase2ContactDamageTime = 0f;

        if (isPhase2Active)
        {
            Debug.Log("[BossFSM] SlimeQueen: 페이즈 2에 진입합니다.", this);

            if (!isPhase2Twin)
                SpawnPhase2TwinIfNeeded();
        }
    }

    /// <summary>원본 보스가 페이즈 2 분신을 한 번만 생성합니다.</summary>
    private void SpawnPhase2TwinIfNeeded()
    {
        if (hasSpawnedPhase2Twin || phase2Twin != null || IsDead)
            return;

        hasSpawnedPhase2Twin = true;

        SlimeQueen sourcePrefab = phase2TwinPrefab != null ? phase2TwinPrefab : this;
        SlimeQueen twin = Instantiate(sourcePrefab, ResolvePhase2TwinSpawnPosition(), transform.rotation);
        if (twin == null)
            return;

        twin.ConfigureAsPhase2Twin(this, CurrentTarget);
        phase2Twin = twin;
    }

    /// <summary>분신 런타임 역할과 타겟을 원본 기준으로 초기화합니다.</summary>
    private void ConfigureAsPhase2Twin(SlimeQueen originalBoss, Transform sharedTarget)
    {
        isPhase2Twin = true;
        phase2Original = originalBoss;
        phase2Twin = null;
        hasSpawnedPhase2Twin = true;
        SetCombatTarget(sharedTarget);
    }

    /// <summary>페이즈 2 분신 생성 위치를 계산합니다.</summary>
    private Vector3 ResolvePhase2TwinSpawnPosition()
    {
        Vector3 spawnOffset = new Vector3(phase2TwinSpawnOffset.x, phase2TwinSpawnOffset.y, 0f);
        return transform.position + spawnOffset;
    }

    /// <summary>분신에게 들어온 HP 피해를 원본 체력 Attribute에 적용합니다.</summary>
    private bool TryApplySharedPhase2Damage(DamageRequest request, Object damageSource)
    {
        if (IsDead)
            return true;

        float hpDamage = Mathf.Max(0f, request.HpDamage);
        if (hpDamage <= 0f)
            return true;

        bool applied = TryModifyCurrentHealthValue(-hpDamage, damageSource != null ? damageSource : this);
        if (!applied)
            Debug.LogWarning("[BossFSM] SlimeQueen: 분신 피해를 원본 체력에 적용하지 못했습니다.", this);

        return true;
    }

    /// <summary>원본 보스가 보유 중인 페이즈 2 분신 오브젝트를 제거합니다.</summary>
    private void DestroyPhase2Twin()
    {
        if (phase2Twin == null)
            return;

        SlimeQueen twin = phase2Twin;
        phase2Twin = null;

        if (twin != null)
            Destroy(twin.gameObject);
    }

    /// <summary>페이즈 2에서 플레이어와 접촉 중이면 GAS 피해를 적용합니다.</summary>
    private void TryApplyPhase2ContactDamage(Collider2D other)
    {
        if (!isPhase2Active || isPatternMoveDamageBlocked || IsDead || other == null)
            return;

        if (phase2ContactDamage <= 0f || Time.time < nextPhase2ContactDamageTime)
            return;

        if (!HasPlayerTagInHierarchy(other.transform))
            return;

        GameObject contactTarget = CombatTargetResolver2D.ResolveDamageTarget(other);
        if (contactTarget == null || !contactTarget.CompareTag("Player"))
            return;

        GE_Damage_Spec damageEffect = phase2ContactDamageEffect != null
            ? phase2ContactDamageEffect
            : fallingContactDamageEffect;

        if (damageEffect == null)
            return;

        Vector3 hitWorldPosition = other.ClosestPoint(transform.position);
        CombatDamageAction.ApplyDamageAndEmitHit(
            AbilitySystem,
            null,
            damageEffect,
            null,
            contactTarget,
            phase2ContactDamage,
            0f,
            null,
            0f,
            null,
            hitWorldPosition,
            gameObject);

        nextPhase2ContactDamageTime = Time.time + Mathf.Max(0f, phase2ContactDamageCooldownSeconds);
    }

    /// <summary>충돌한 콜라이더의 계층에 Player 태그가 있는지 확인합니다.</summary>
    private bool HasPlayerTagInHierarchy(Transform hitTransform)
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

    /// <summary>패턴 2 바운더리 참조를 인스펙터 또는 씬 자동 탐색으로 해결합니다.</summary>
    private SlimeQueenRandomMoveBounds ResolveRandomMoveBounds()
    {
        if (randomMoveBounds == null)
            randomMoveBounds = FindAnyObjectByType<SlimeQueenRandomMoveBounds>();

        return randomMoveBounds;
    }
}
