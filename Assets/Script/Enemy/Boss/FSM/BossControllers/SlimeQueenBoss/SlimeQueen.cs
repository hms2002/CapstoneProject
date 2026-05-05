using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

public sealed class SlimeQueen : BossControllerBase, IIntentMovementSource2D
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
    [Tooltip("패턴 4의 4방향 경고선 표시에 사용할 AttackTelegraph 스타일입니다.")]
    [SerializeField] private AttackTelegraphStyle bishopLineWarningStyle;

    [Tooltip("패턴 4의 물기둥 폭발 표시에 사용할 AttackTelegraph 스타일입니다.")]
    [SerializeField] private AttackTelegraphStyle bishopLineBlastStyle;

    [Tooltip("패턴 4 경고선이 유지되는 시간입니다.")]
    [SerializeField, Min(0f)] private float bishopLineWarningSeconds = 1.4f;

    [Tooltip("패턴 4 경고선의 두께입니다.")]
    [SerializeField, Min(0.05f)] private float bishopLineWarningWidth = 0.35f;

    [Tooltip("패턴 4 물기둥이 배치되는 간격입니다.")]
    [SerializeField, Min(0.1f)] private float bishopLineBlastInterval = 1.2f;

    [Tooltip("패턴 4 물기둥 피해 원의 지름입니다.")]
    [SerializeField, Min(0.1f)] private float bishopLineBlastDiameter = 1.25f;

    [Tooltip("패턴 4 물기둥 연출 표시 시간입니다.")]
    [SerializeField, Min(0f)] private float bishopLineBlastViewSeconds = 0.2f;

    [Tooltip("패턴 4 물기둥이 플레이어에게 주는 피해량입니다.")]
    [SerializeField, Min(0f)] private float bishopLineBlastDamage = 1.5f;

    [Tooltip("패턴 4 물기둥 피해에 사용할 GAS Damage Effect입니다. 비워두면 패턴 1 낙하 피해 Effect를 사용합니다.")]
    [SerializeField] private GE_Damage_Spec bishopLineBlastDamageEffect;

    [Tooltip("패턴 4 경고선이 벽까지 뻗을 때 검사할 레이어입니다.")]
    [SerializeField] private LayerMask bishopLineObstacleLayers = 1 << 30;

    [Tooltip("패턴 4 경고선 벽 검출 최대 거리입니다.")]
    [SerializeField, Min(0.1f)] private float bishopLineRaycastDistance = 64f;

    [Tooltip("패턴 4에서 벽을 찾지 못했을 때 사용할 경고선 길이입니다.")]
    [SerializeField, Min(0.1f)] private float bishopLineFallbackLength = 8f;

    private AttackTelegraphService telegraphService;
    private SpeechBubbleComponent speechBubble;
    private readonly List<Vector3> bishopLineBlastPoints = new List<Vector3>();
    private bool runtimePatternsConfigured;

    public float SummonWarningSeconds => summonWarningSeconds;

    public float SummonWarningDiameter => summonWarningDiameter;

    public float FallStartHeight => fallStartHeight;

    public float FallSpeed => fallSpeed;

    public float PostLandingWaitSeconds => postLandingWaitSeconds;

    public float FallContactRadius => Mathf.Max(0.1f, summonWarningDiameter * 0.5f);

    public float JumpDurationSeconds => jumpDurationSeconds;

    public float JumpArcHeight => jumpArcHeight;

    public float CallSlimeSpawnDelaySeconds => callSlimeSpawnDelaySeconds;

    public float BishopLineWarningSeconds => bishopLineWarningSeconds;

    public float BishopLineBlastViewSeconds => bishopLineBlastViewSeconds;

    protected override void Awake()
    {
        base.Awake();
        telegraphService = GetComponent<AttackTelegraphService>();
        speechBubble = GetComponent<SpeechBubbleComponent>();
    }

    protected override void Start()
    {
        if (configureRuntimePatternsOnStart || ConfiguredPhaseCount == 0)
            ConfigureRuntimePatternsIfNeeded();

        base.Start();
    }

    protected override void Update()
    {
        base.Update();
        FaceCurrentTarget();
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

    /// <summary>패턴 4의 4방향 Bishop 경고선을 표시합니다.</summary>
    public void ShowBishopLineWarnings()
    {
        Vector2 upRight = new Vector2(1f, 1f).normalized;
        Vector2 upLeft = new Vector2(-1f, 1f).normalized;
        Vector2 downRight = new Vector2(1f, -1f).normalized;
        Vector2 downLeft = new Vector2(-1f, -1f).normalized;

        ShowBishopLineWarning(upRight);
        ShowBishopLineWarning(upLeft);
        ShowBishopLineWarning(downRight);
        ShowBishopLineWarning(downLeft);
    }

    /// <summary>패턴 4의 물기둥 폭발 표시와 피해 판정을 실행합니다.</summary>
    public void FireBishopLineBlasts(AbilitySpec sourceSpec)
    {
        bishopLineBlastPoints.Clear();
        bishopLineBlastPoints.Add(transform.position);

        Vector2 upRight = new Vector2(1f, 1f).normalized;
        Vector2 upLeft = new Vector2(-1f, 1f).normalized;
        Vector2 downRight = new Vector2(1f, -1f).normalized;
        Vector2 downLeft = new Vector2(-1f, -1f).normalized;

        FillBishopLineBlastPoints(upRight);
        FillBishopLineBlastPoints(upLeft);
        FillBishopLineBlastPoints(downRight);
        FillBishopLineBlastPoints(downLeft);
        ShowBishopLineBlastViews();
        ApplyBishopLineBlastDamage(sourceSpec);
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

    /// <summary>SlimeQueen Phase 1 기본 패턴 구성을 런타임으로 생성합니다.</summary>
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

        BossPatternEntry bishopLineBlast = CreatePattern<AbilityLogic_SlimeQueenBishopLineBlast>(
            "SlimeQueen_BishopLineBlast",
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
                bishopLineBlast)
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

    /// <summary>패턴 4의 단일 방향 경고선을 표시합니다.</summary>
    private void ShowBishopLineWarning(Vector2 direction)
    {
        AttackTelegraphService service = GetTelegraphService();
        if (service == null)
            return;

        float distance = GetBishopLineDistance(transform.position, direction);
        if (distance <= 0f)
            return;

        Vector3 center = transform.position + (Vector3)(direction * distance * 0.5f);
        float angleDeg = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        AttackTelegraphSpec spec = AttackTelegraphSpec.CreateRectangle(
            center,
            new Vector2(distance, bishopLineWarningWidth),
            angleDeg,
            bishopLineWarningSeconds,
            bishopLineWarningStyle);

        service.SpawnDetachedView(spec);
    }

    /// <summary>패턴 4의 단일 방향 물기둥 위치들을 채웁니다.</summary>
    private void FillBishopLineBlastPoints(Vector2 direction)
    {
        float distance = GetBishopLineDistance(transform.position, direction);
        float interval = Mathf.Max(0.1f, bishopLineBlastInterval);

        for (float offset = interval; offset <= distance + 0.001f; offset += interval)
            bishopLineBlastPoints.Add(transform.position + (Vector3)(direction * offset));
    }

    /// <summary>패턴 4 물기둥 위치에 원형 폭발 표시를 생성합니다.</summary>
    private void ShowBishopLineBlastViews()
    {
        AttackTelegraphService service = GetTelegraphService();
        if (service == null)
            return;

        for (int i = 0; i < bishopLineBlastPoints.Count; i++)
        {
            AttackTelegraphSpec spec = AttackTelegraphSpec.CreateCircle(
                bishopLineBlastPoints[i],
                bishopLineBlastDiameter,
                bishopLineBlastViewSeconds,
                bishopLineBlastStyle);

            service.SpawnDetachedView(spec);
        }
    }

    /// <summary>패턴 4 물기둥 범위 안의 현재 타겟에게 GAS Damage Effect를 적용합니다.</summary>
    private void ApplyBishopLineBlastDamage(AbilitySpec sourceSpec)
    {
        if (bishopLineBlastDamage <= 0f || CurrentTarget == null)
            return;

        GE_Damage_Spec damageEffect = bishopLineBlastDamageEffect != null
            ? bishopLineBlastDamageEffect
            : fallingContactDamageEffect;

        if (damageEffect == null)
            return;

        float radius = Mathf.Max(0.1f, bishopLineBlastDiameter * 0.5f);
        float sqrRadius = radius * radius;
        Vector2 targetPosition = CurrentTarget.position;

        for (int i = 0; i < bishopLineBlastPoints.Count; i++)
        {
            Vector2 toTarget = targetPosition - (Vector2)bishopLineBlastPoints[i];
            if (toTarget.sqrMagnitude > sqrRadius)
                continue;

            CombatDamageAction.ApplyDamageAndEmitHit(
                AbilitySystem,
                sourceSpec,
                damageEffect,
                null,
                CurrentTarget.gameObject,
                bishopLineBlastDamage,
                0f,
                null,
                0f,
                null,
                bishopLineBlastPoints[i],
                gameObject);
            return;
        }
    }

    /// <summary>패턴 4 경고선이 지정 방향으로 뻗을 거리를 계산합니다.</summary>
    private float GetBishopLineDistance(Vector2 center, Vector2 direction)
    {
        RaycastHit2D hit = Physics2D.Raycast(center, direction, bishopLineRaycastDistance, bishopLineObstacleLayers);
        if (hit.collider != null)
            return Mathf.Max(bishopLineBlastDiameter * 0.5f, hit.distance);

        return bishopLineFallbackLength;
    }

    /// <summary>패턴 2 바운더리 참조를 인스펙터 또는 씬 자동 탐색으로 해결합니다.</summary>
    private SlimeQueenRandomMoveBounds ResolveRandomMoveBounds()
    {
        if (randomMoveBounds == null)
            randomMoveBounds = FindAnyObjectByType<SlimeQueenRandomMoveBounds>();

        return randomMoveBounds;
    }
}
