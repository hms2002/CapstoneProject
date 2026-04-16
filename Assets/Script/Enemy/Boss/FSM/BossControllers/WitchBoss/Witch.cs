using System.Collections;
using System.Collections.Generic;
using CapstoneAudio;
using UnityEngine;
using UnityGAS;
using UnityGAS.Sample;

public class Witch : BossControllerBase, IWitchPatternStateBridge
{
    // 이 클래스의 책임:
    // 마녀 보스 전용 상태, 연출, 패턴 보조 동작을 조율하고 전용 런타임 데이터를 관리한다.

    private const string StaggerImmuneTagResourcePath = "Tags/State.Status.StaggerImmune";
    private const int WallLayer = 30;
    private const int ExtinguishCandleCount = 2;
    private static readonly Vector3 FallbackRetreatLeftOffset = new Vector3(-0.5f, 0.2f, 0f);
    private static readonly Vector3 FallbackRetreatRightOffset = new Vector3(0.5f, 0.2f, 0f);
    private const int FallbackNormal1Count = 3;
    private const float FallbackNormal1Interval = 0.3f;
    private const float FallbackNormal1TileUnitSize = 1.7f;
    private const float FallbackNormal1TileWidthInTiles = 3f;
    private const float FallbackNormal1TileHeightInTiles = 6f;
    private const float FallbackNormal1HitTime = 0.12f;
    private const float FallbackRetreatExplosionDiameter = 6f;
    private const float FallbackRetreatSpeedScale = 1.5f;

    [Header("Pattern")]
    [Tooltip("촛대를 끄는 패턴에 사용할 Fog 프리팹입니다.")]
    [SerializeField] private GameObject fogPrefab;
    [SerializeField] private GameObject candlestickPrefab;
    [SerializeField] private GameObject lightBeadPrefab;
    [SerializeField] private GE_Damage_Spec extinguishDamageEffect;
    [SerializeField] private Transform phaseTransitionCenterPoint;
    [SerializeField] private float extinguishDamage = 1f;
    [SerializeField] private float projectileSpeed = 4f;
    [SerializeField] private bool useRuntimeDefaultPatternsWhenPhasesEmpty = true;
    [SerializeField] private float fallbackCandleSpawnRadius = 6f;

    [Header("Pattern Logic Templates")]
    [SerializeField] private AbilityLogic_WitchNormalAttack1 normalAttack1PatternLogicTemplate;
    [SerializeField] private AbilityLogic_WitchExtinguishCandle extinguishPatternLogicTemplate;
    [SerializeField] private AbilityLogic_WitchRetreatToCandle retreatPatternLogicTemplate;
    [SerializeField] private AbilityLogic_WitchLightAllCandles lightAllCandlesPatternLogicTemplate;

    [Header("Pattern Runtime References")]
    [SerializeField] private Transform extinguishExplosionVisualSocket;

    private BossDialogueRunner dialogueRunner;
    private Coroutine dialogueRoutine;
    private WitchExtinguishPatternState extinguishState;
    private WitchNormalAttack1State normalAttack1State;
    private WitchRetreatToCandleState retreatState;
    private WitchExtinguishPatternExecutor extinguishPatternExecutor;
    private WitchLightAllCandlesPatternExecutor lightAllCandlesPatternExecutor;
    private WitchNormalAttack1PatternExecutor normalAttack1PatternExecutor;
    private WitchRetreatPatternExecutor retreatPatternExecutor;
    private WitchCandleService candleService;
    private AttackTelegraphService telegraphService;
    private AttackTelegraphStyle extinguishWarningStyle;
    private AttackTelegraphStyle mapWideWarningStyle;
    private bool hasAttackTrigger;
    private WitchRuntimeData runtimeData;
    private bool hasLoggedRuntimeDataReady;
    private AbilityDefinition basicAttack2Ability;
    private AbilityDefinition sealedCandleRampageAbility;
    private AbilityDefinition lightAllCandlesAbility;
    private readonly List<Candlestick> runtimeSpawnedCandles = new();
    private WitchShieldController shieldController;
    private WitchShieldVisualController shieldVisualController;
    private CameraPresentationDirector cameraPresentationDirector;
    private GameplayTag staggerImmuneTag;
    private bool hasAppliedStaggerImmuneTag;

    protected override void Awake()
    {
        base.Awake();
        runtimeData = new WitchRuntimeData();
        extinguishPatternExecutor = GetComponent<WitchExtinguishPatternExecutor>();
        if (extinguishPatternExecutor == null)
            extinguishPatternExecutor = gameObject.AddComponent<WitchExtinguishPatternExecutor>();
        lightAllCandlesPatternExecutor = GetComponent<WitchLightAllCandlesPatternExecutor>();
        if (lightAllCandlesPatternExecutor == null)
            lightAllCandlesPatternExecutor = gameObject.AddComponent<WitchLightAllCandlesPatternExecutor>();
        normalAttack1PatternExecutor = GetComponent<WitchNormalAttack1PatternExecutor>();
        if (normalAttack1PatternExecutor == null)
            normalAttack1PatternExecutor = gameObject.AddComponent<WitchNormalAttack1PatternExecutor>();
        retreatPatternExecutor = GetComponent<WitchRetreatPatternExecutor>();
        if (retreatPatternExecutor == null)
            retreatPatternExecutor = gameObject.AddComponent<WitchRetreatPatternExecutor>();
        candleService = GetComponent<WitchCandleService>();
        if (candleService == null)
            candleService = gameObject.AddComponent<WitchCandleService>();
        telegraphService = GetComponent<AttackTelegraphService>();
        extinguishWarningStyle = MakeWarningStyle();
        mapWideWarningStyle = MakeMapWideWarningStyle();
        hasAttackTrigger = CheckAttackTrigger();
        shieldController = GetComponent<WitchShieldController>();
        if (shieldController == null)
            shieldController = gameObject.AddComponent<WitchShieldController>();
        shieldVisualController = GetComponent<WitchShieldVisualController>();
        if (shieldVisualController == null)
            shieldVisualController = gameObject.AddComponent<WitchShieldVisualController>();
        cameraPresentationDirector = GetComponent<CameraPresentationDirector>();
        staggerImmuneTag = Resources.Load<GameplayTag>(StaggerImmuneTagResourcePath);
    }

    protected override void Start()
    {
        EnsureRuntimeCandlesIfNeeded();
        ConfigureRuntimePatternsIfNeeded();
        base.Start();
        LogRuntimeDataReadyIfNeeded();
    }

    protected override void CreateStates()
    {
        base.CreateStates();
        extinguishState = new WitchExtinguishPatternState(this, this);
        normalAttack1State = new WitchNormalAttack1State(this, this);
        retreatState = new WitchRetreatToCandleState(this, this);
    }

    protected override void OnPatternEnd(BossPatternEntry patternEntry, bool forced)
    {
        HideExtinguishWarning();
        if (patternEntry != null && patternEntry.Ability != null && patternEntry.Ability.logic is AbilityLogic_WitchLightAllCandles)
        {
            if (patternEntry.Ability.logic is AbilityLogic_WitchLightAllCandles lightAllCandlesLogic)
                lightAllCandlesLogic.StopChargeLoopFor(this);
            HideMapWideWarning();
            ClearShield();
            DisableStaggerImmuneDuringPhaseTransition();
            if (!IsDead)
            {
                CameraPresentationDirector phaseCameraDirector = GetCameraPresentationDirector();
                if (phaseCameraDirector != null)
                    phaseCameraDirector.RestoreDefaultState();
            }
        }
        ClearNormal1();
    }

    public WitchRuntimeData RuntimeData
    {
        get
        {
            if (runtimeData == null)
                runtimeData = new WitchRuntimeData();

            return runtimeData;
        }
    }
    public GameObject LightBeadPrefab => lightBeadPrefab;
    public GE_Damage_Spec ProjectileDamageEffect => extinguishDamageEffect;
    public float ProjectileDamage => extinguishDamage;
    public float ProjectileSpeed => projectileSpeed;
    public bool HasProjectilePatternConfig => lightBeadPrefab != null && extinguishDamageEffect != null;
    public WitchShieldController ShieldController => shieldController;
    public AttackTelegraphService ExtinguishTelegraphService => telegraphService;
    public AttackTelegraphStyle ExtinguishWarningStyle => extinguishWarningStyle;
    public GameObject FogPrefab => fogPrefab;
    public GE_Damage_Spec ExtinguishDamageEffect => extinguishDamageEffect;
    public float ExtinguishDamage => extinguishDamage;
    public Transform ExtinguishExplosionVisualSocket => extinguishExplosionVisualSocket;
    public WitchLightAllCandlesPatternExecutor LightAllCandlesPatternExecutor => lightAllCandlesPatternExecutor;

    public CameraPresentationDirector GetCameraPresentationDirector()
    {
        if (cameraPresentationDirector == null)
            cameraPresentationDirector = GetComponent<CameraPresentationDirector>();

        if (cameraPresentationDirector == null)
            cameraPresentationDirector = FindAnyObjectByType<CameraPresentationDirector>();

        return cameraPresentationDirector;
    }

    protected override void Update()
    {
        base.Update();

        // 스프라이트 반전
        if (Target == null) return;

        if      (transform.position.x > Target.position.x) sprite.flipX = true;
        else if (transform.position.x < Target.position.x) sprite.flipX = false;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        if (extinguishWarningStyle != null) Destroy(extinguishWarningStyle);
        if (mapWideWarningStyle != null)
            Destroy(mapWideWarningStyle);
    }

    public BossState GetExtinguishState()
    {
        return extinguishState;
    }

    public override BossState GetPatternState(BossPatternEntry patternEntry)
    {
        if (IsExtinguishPattern(patternEntry)) return extinguishState;
        if (IsNormal1Pattern(patternEntry)) return normalAttack1State;
        if (IsRetreatPattern(patternEntry)) return retreatState;

        return base.GetPatternState(patternEntry);
    }

    public override bool TryStartDialogue()
    {
        BossDialogueRunner runner = GetDialogueRunner();
        if (runner == null || dialogueRoutine != null) return false;

        dialogueRoutine = StartCoroutine(PlayDialogue(runner));
        return true;
    }

    public override bool IsDialogueActive()
    {
        return dialogueRoutine != null ||
               (DialogueService.Instance != null && DialogueService.Instance.IsPlaying);
    }

    /// <summary>패턴 공용 공격 모션을 재생합니다.</summary>
    public void PlayPatternAttackMotion()
    {
        if (animator != null && hasAttackTrigger)
            animator.SetTrigger("attack");
    }

    /// <summary>촛불 끄기 패턴인지 확인합니다.</summary>
    private bool IsExtinguishPattern(BossPatternEntry patternEntry)
    {
        if (patternEntry == null || patternEntry.Ability == null) return false;

        return patternEntry.Ability.logic is UnityGAS.Sample.AbilityLogic_WitchExtinguishCandle;
    }

    /// <summary>평타1 패턴인지 확인합니다.</summary>
    private bool IsNormal1Pattern(BossPatternEntry patternEntry)
    {
        if (patternEntry == null || patternEntry.Ability == null) return false;

        return patternEntry.Ability.logic is UnityGAS.Sample.AbilityLogic_WitchNormalAttack1;
    }

    /// <summary>촛대로의 피난 패턴인지 확인합니다.</summary>
    private bool IsRetreatPattern(BossPatternEntry patternEntry)
    {
        if (patternEntry == null || patternEntry.Ability == null) return false;

        return patternEntry.Ability.logic is UnityGAS.Sample.AbilityLogic_WitchRetreatToCandle;
    }

    /// <summary>촛불 끄기 패턴을 시작합니다.</summary>
    public bool StartExtinguish(float warningTime)
    {
        return extinguishPatternExecutor != null &&
               extinguishPatternExecutor.TryBeginPattern(warningTime, out _);
    }

    /// <summary>
    /// 책임 :
    /// - FSM state가 촛불 끄기 패턴 시작을 구체 Witch 구현 대신 브리지 계약으로 요청할 수 있게 한다.
    /// - 실행 지속시간을 함께 반환해 state가 내부 계산 메서드에 직접 의존하지 않게 만든다.
    /// </summary>
    public bool TryBeginExtinguishPattern(float warningTimeSeconds, out float resolvedDurationSeconds)
    {
        if (extinguishPatternExecutor == null)
        {
            resolvedDurationSeconds = 0f;
            return false;
        }

        return extinguishPatternExecutor.TryBeginPattern(warningTimeSeconds, out resolvedDurationSeconds);
    }

    /// <summary>평타1 장판 공격을 시작합니다.</summary>
    public bool StartNormal1()
    {
        bool executorSucceeded = normalAttack1PatternExecutor != null && normalAttack1PatternExecutor.TryBeginPattern();
        if (executorSucceeded)
            return true;

        Debug.LogWarning("[Witch] 평타1 executor 경로가 실패하여 inline fallback을 사용합니다.", this);
        return TryBeginNormalAttack1InlineFallback();
    }

    /// <summary>
    /// 책임 :
    /// - FSM state가 평타1 패턴 시작과 대기 시간을 브리지 계약으로 받도록 돕는다.
    /// - 패턴 내부 구현이 바뀌어도 state는 반환된 지속시간만 사용하게 만들어 결합을 줄인다.
    /// </summary>
    public bool TryBeginNormalAttack1Pattern(out float resolvedDurationSeconds)
    {
        resolvedDurationSeconds = 0f;
        if (!StartNormal1())
            return false;

        resolvedDurationSeconds = GetNormal1Time();
        return true;
    }

    /// <summary>촛대로의 피난 패턴을 시작합니다.</summary>
    public bool StartRetreat()
    {
        bool executorSucceeded = retreatPatternExecutor != null && retreatPatternExecutor.TryBeginPattern();
        if (executorSucceeded)
            return true;

        Debug.LogWarning("[Witch] 피난 executor 경로가 실패하여 inline fallback을 사용합니다.", this);
        return TryBeginRetreatInlineFallback();
    }

    /// <summary>
    /// 책임 :
    /// - FSM state가 촛대로의 피난 패턴 시작을 브리지 계약으로 호출하게 한다.
    /// - 이후 피난 패턴 구현이 runner나 다른 실행기로 바뀌어도 state 호출 형태를 유지하게 한다.
    /// </summary>
    public bool TryBeginRetreatPattern()
    {
        return StartRetreat();
    }

    /// <summary>촛불 끄기 패턴을 끝냅니다.</summary>
    public void FinishExtinguish()
    {
        extinguishPatternExecutor?.CompletePattern();
    }

    /// <summary>
    /// 책임 :
    /// - FSM state가 촛불 끄기 패턴 완료 시 필요한 연출/판정 정리를 브리지 계약으로 위임하게 한다.
    /// - state가 선택 데이터나 경고 정리 절차를 직접 알지 않도록 완료 책임을 Witch 내부에 둔다.
    /// </summary>
    public void CompleteExtinguishPattern()
    {
        FinishExtinguish();
    }

    public void HideExtinguishWarning()
    {
        extinguishPatternExecutor?.CancelPattern();
    }

    /// <summary>
    /// 책임 :
    /// - FSM state가 촛불 끄기 패턴 취소 시 경고와 선택 상태 정리를 브리지 계약으로 요청하게 한다.
    /// - 취소 정리 구현이 바뀌어도 state가 구체 절차를 직접 알지 않게 만든다.
    /// </summary>
    public void CancelExtinguishPattern()
    {
        extinguishPatternExecutor?.CancelPattern();
    }

    /// <summary>마녀 보호막을 지정 단계수로 활성화합니다.</summary>
    public void ActivateShield(int stageCount = 4)
    {
        shieldController?.ActivateShield(stageCount);
    }

    /// <summary>마녀 보호막 전용 타격을 적용합니다.</summary>
    public bool TryApplyShieldHit(int amount = 1)
    {
        return shieldController != null && shieldController.TryApplyShieldHit(amount);
    }

    /// <summary>마녀 보호막을 깨뜨립니다.</summary>
    public void BreakShield()
    {
        shieldController?.BreakShield();
    }

    /// <summary>마녀 보호막을 조용히 정리합니다.</summary>
    public void ClearShield()
    {
        shieldController?.ClearShield();
    }

    /// <summary>
    /// 책임 :
    /// - 촛불을 켜라 패턴 동안만 사용할 stagger 면역 태그를 중복 없이 부여한다.
    /// - HP 무적과 별도로 stagger buildup 차단 의도를 분리해 유지한다.
    /// </summary>
    public void EnableStaggerImmuneDuringPhaseTransition()
    {
        if (hasAppliedStaggerImmuneTag || staggerImmuneTag == null || TagSystem == null)
            return;

        TagSystem.AddTag(staggerImmuneTag, 1);
        hasAppliedStaggerImmuneTag = true;
    }

    /// <summary>
    /// 책임 :
    /// - 촛불을 켜라 패턴이 끝날 때 이 보스가 직접 부여한 stagger 면역 태그만 회수한다.
    /// - 강제 종료나 보호막 파괴 종료에서도 태그가 남지 않게 정리한다.
    /// </summary>
    public void DisableStaggerImmuneDuringPhaseTransition()
    {
        if (!hasAppliedStaggerImmuneTag || staggerImmuneTag == null || TagSystem == null)
            return;

        TagSystem.RemoveTag(staggerImmuneTag, 1);
        hasAppliedStaggerImmuneTag = false;
    }

    /// <summary>평타1 장판을 모두 지웁니다.</summary>
    public void ClearNormal1()
    {
        runtimeData.ClearNormal1Tiles();
    }

    /// <summary>평타1 전체 시간을 돌려줍니다.</summary>
    public float GetNormal1Time()
    {
        return GetNormal1StartTimeValue() + ((GetNormal1CountValue() - 1) * GetNormal1IntervalValue()) + GetNormal1HitTimeValue();
    }

    /// <summary>대화 State 사용 여부를 정합니다.</summary>
    protected override bool CanUseDialogue()
    {
        return GetDialogueRunner() != null && FindAnyObjectByType<BossEncounterDirector>() == null;
    }

    /// <summary>대화를 실행합니다.</summary>
    private IEnumerator PlayDialogue(BossDialogueRunner runner)
    {
        yield return runner.PlayDialogueRoutine();
        dialogueRoutine = null;
    }

    /// <summary>대화 러너를 구합니다.</summary>
    private BossDialogueRunner GetDialogueRunner()
    {
        if (dialogueRunner == null) dialogueRunner = FindAnyObjectByType<BossDialogueRunner>();

        return dialogueRunner;
    }

    /// <summary>가장 가까운 촛대를 구합니다.</summary>
    public Candlestick GetNearestCandle()
    {
        return candleService != null ? candleService.GetNearestAvailableCandle() : null;
    }

    /// <summary>평타1 패턴이 사용할 조준 방향을 반환합니다.</summary>
    internal Vector2 GetAimDirectionValue()
    {
        Vector2 toTarget = Target.position - transform.position;
        if (toTarget.sqrMagnitude <= 0.0001f) return Vector2.right;

        return toTarget.normalized;
    }

    /// <summary>평타1 패턴이 사용할 장판 위치를 반환합니다.</summary>
    internal Vector3 GetNormal1TilePointValue(Vector2 aimDir, int index)
    {
        float forwardSize = GetNormal1TileSizeValue().x;
        float distance = (forwardSize * 0.5f) + (forwardSize * index);
        Vector3 offset = new Vector3(aimDir.x, aimDir.y, 0f) * distance;
        return transform.position + offset;
    }

    /// <summary>평타1 패턴이 사용할 장판 크기를 반환합니다.</summary>
    internal Vector2 GetNormal1TileSizeValue()
    {
        return new Vector2(
            GetNormal1TileUnitSizeValue() * GetNormal1TileWidthInTilesValue(),
            GetNormal1TileUnitSizeValue() * GetNormal1TileHeightInTilesValue());
    }

    /// <summary>평타1 executor가 실패했을 때 기존 인라인 경로로 장판 공격을 복구합니다.</summary>
    private bool TryBeginNormalAttack1InlineFallback()
    {
        if (abilitySystem == null || Target == null)
        {
            Debug.LogWarning(
                $"[Witch] 평타1 inline fallback 실패: abilitySystem={(abilitySystem != null)}, target={(Target != null)}",
                this);
            return false;
        }

        WitchNormalAttack1Tile tilePrefab = ResolveNormal1TilePrefabValue();
        GE_Damage_Spec damageEffect = ResolveNormal1DamageEffectValue();
        if (tilePrefab == null || damageEffect == null)
        {
            Debug.LogWarning(
                $"[Witch] 평타1 inline fallback 실패: tilePrefab={(tilePrefab != null)}, damageEffect={(damageEffect != null)}",
                this);
            return false;
        }

        Vector2 aimDir = GetAimDirectionValue();
        if (aimDir == Vector2.zero)
        {
            Debug.LogWarning("[Witch] 평타1 inline fallback 실패: aimDir가 zero입니다.", this);
            return false;
        }

        runtimeData.ClearNormal1Tiles();
        PlayPatternAttackMotion();

        float angle = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg;
        Vector2 tileSize = GetNormal1TileSizeValue();
        CombatHitPayload payload = MakeNormal1PayloadValue();
        int tileCount = GetNormal1CountValue();
        float intervalSeconds = GetNormal1IntervalValue();
        float startTime = GetNormal1StartTimeValue();

        for (int i = 0; i < tileCount; i++)
        {
            WitchNormalAttack1Tile tile = Instantiate(
                tilePrefab,
                GetNormal1TilePointValue(aimDir, i),
                Quaternion.Euler(0f, 0f, angle));

            runtimeData.AddNormal1Tile(tile);
            tile.Play(
                Target.gameObject,
                payload,
                tileSize,
                angle,
                i * intervalSeconds,
                startTime + (i * intervalSeconds));
        }

        Debug.Log($"[Witch] 평타1 inline fallback 실행 성공: tileCount={tileCount}, interval={intervalSeconds}", this);
        return true;
    }

    /// <summary>피난 executor가 실패했을 때 기존 인라인 경로로 강화 해골 소환을 복구합니다.</summary>
    private bool TryBeginRetreatInlineFallback()
    {
        DeadsSkeleton skeletonPrefab = ResolveRetreatSkeletonPrefabValue();
        if (skeletonPrefab == null)
        {
            Debug.LogWarning("[Witch] 피난 inline fallback 실패: skeletonPrefab이 없습니다.", this);
            return false;
        }

        PlayPatternAttackMotion();
        bool spawnedLeft = SpawnRetreatSkeletonInlineFallback(skeletonPrefab, ResolveRetreatLeftOffsetValue());
        bool spawnedRight = SpawnRetreatSkeletonInlineFallback(skeletonPrefab, ResolveRetreatRightOffsetValue());
        Debug.Log($"[Witch] 피난 inline fallback 실행 결과: left={spawnedLeft}, right={spawnedRight}", this);
        return spawnedLeft || spawnedRight;
    }

    /// <summary>기존 피난 패턴 방식으로 강화 해골 하나를 소환합니다.</summary>
    private bool SpawnRetreatSkeletonInlineFallback(DeadsSkeleton skeletonPrefab, Vector3 localOffset)
    {
        if (skeletonPrefab == null)
        {
            Debug.LogWarning("[Witch] 피난 inline fallback 소환 실패: skeletonPrefab이 null입니다.", this);
            return false;
        }

        DeadsSkeleton skeleton = Instantiate(
            skeletonPrefab,
            transform.TransformPoint(localOffset),
            Quaternion.identity);

        if (skeleton == null)
        {
            Debug.LogWarning("[Witch] 피난 inline fallback 소환 실패: Instantiate 결과가 null입니다.", this);
            return false;
        }

        skeleton.SetBoost(Target, ResolveRetreatExplosionDiameterValue(), ResolveRetreatSpeedScaleValue(), true);
        return true;
    }

    /// <summary>촛대 중심 위치를 구합니다.</summary>
    public Vector3 GetCandleCenter(Candlestick candle)
    {
        return candleService != null ? candleService.GetCandleCenter(candle) : transform.position;
    }

    /// <summary>현재 봉인된 촛대 수를 구합니다.</summary>
    public int GetSealedCandleCount()
    {
        return candleService != null ? candleService.GetSealedCandleCount() : 0;
    }

    /// <summary>봉인된 촛대가 하나라도 있는지 확인합니다.</summary>
    public bool HasAnySealedCandles()
    {
        return candleService != null && candleService.HasAnySealedCandles();
    }

    /// <summary>모든 촛대를 봉인 상태로 만듭니다.</summary>
    public void SealAllCandles()
    {
        candleService?.SealAllCandles();
    }

    /// <summary>현재 봉인된 촛대를 버퍼에 수집합니다.</summary>
    public void CollectSealedCandles(List<Candlestick> buffer)
    {
        candleService?.CollectSealedCandles(buffer);
    }

    /// <summary>테스트나 예외 상황에서 가장 가까운 미봉인 촛대 하나를 즉시 봉인합니다.</summary>
    public Candlestick SealNearestAvailableCandle()
    {
        return candleService != null ? candleService.SealNearestAvailableCandle() : null;
    }

    /// <summary>대상 방향이나 현재 바라보는 방향을 기반으로 투사체 방향을 구합니다.</summary>
    public Vector2 GetDirectionToTargetOrFacing(Transform targetTransform, Vector3? fromPosition = null)
    {
        Vector3 origin = fromPosition ?? transform.position;
        if (targetTransform != null)
        {
            Vector2 toTarget = (Vector2)(targetTransform.position - origin);
            if (toTarget.sqrMagnitude > 0.0001f)
                return toTarget.normalized;
        }

        return sprite != null && sprite.flipX ? Vector2.left : Vector2.right;
    }

    /// <summary>그로기 상태 효과를 보스 자신에게 적용합니다.</summary>
    public void ApplyGroggyStatus()
    {
        GameplayEffect resolvedGroggyStatusEffect = ResolveLightAllCandlesGroggyStatusEffect();
        if (abilitySystem == null || resolvedGroggyStatusEffect == null)
            return;

        abilitySystem.EffectRunner.ApplyEffect(resolvedGroggyStatusEffect, gameObject, gameObject);
    }

    /// <summary>페이즈 전환 패턴의 중앙 위치를 구합니다.</summary>
    public Vector3 GetPhaseTransitionCenter()
    {
        if (phaseTransitionCenterPoint != null)
            return phaseTransitionCenterPoint.position;

        return candleService != null ? candleService.GetCandlesCenter() : transform.position;
    }

    /// <summary>지정한 중심 기준으로 촛대들을 모두 덮는 반경을 구합니다.</summary>
    public float GetArenaRadiusFromCandles(Vector3 center, float fallbackRadius = 6f)
    {
        return candleService != null ? candleService.GetArenaRadiusFromCandles(center, fallbackRadius) : fallbackRadius;
    }

    /// <summary>중앙 위치로 보스를 이동시킵니다.</summary>
    public void MoveToPhaseTransitionCenter(float duration)
    {
        if (duration <= 0f)
            return;

        AbilityMotionController2D motion = GetComponent<AbilityMotionController2D>();
        if (motion == null)
            return;

        Vector2 start = transform.position;
        Vector2 destination = GetPhaseTransitionCenter();
        Vector2 delta = destination - start;
        float distance = delta.magnitude;
        if (distance <= 0.001f)
            return;

        motion.StartLunge(start, delta.normalized, distance, duration);
    }

    /// <summary>벽 레이어를 기준으로 맵 전체 피해 경고를 표시합니다.</summary>
    public void ShowMapWideWarning(Vector3 center, float warningTime)
    {
        if (telegraphService == null)
            return;

        AttackTelegraphStyle warningStyle = ResolveMapWideWarningStyle();
        ResolveArenaRectangle(center, out Vector3 rectCenter, out Vector2 rectSize);
        AttackTelegraphSpec spec = AttackTelegraphSpec.CreateRectangle(
            rectCenter,
            rectSize,
            0f,
            Mathf.Max(0f, warningTime),
            warningStyle);

        telegraphService.Show(spec);
    }

    /// <summary>맵 전체 피해 경고 텔레그래프를 즉시 숨깁니다.</summary>
    public void HideMapWideWarning()
    {
        if (telegraphService == null)
            return;

        telegraphService.HideCurrent();
    }

    /// <summary>현재 타깃에게 맵 전체 피해를 적용합니다.</summary>
    public bool ApplyMapWideDamage(GameObject explicitTarget = null)
    {
        if (abilitySystem == null || extinguishDamageEffect == null)
            return false;

        GameObject targetObject = explicitTarget != null
            ? explicitTarget
            : Target != null ? Target.gameObject : null;
        if (targetObject == null)
            return false;

        CombatDamageSnapshot snapshot = new CombatDamageSnapshot(
            finalHpDamage: extinguishDamage,
            finalStaggerBuildUp: 0f,
            finalKnockbackImpulse: 0f,
            elementBuildUps: null,
            isCriticalHit: false);

        CombatHitPayload payload = CombatHitPayload.FromSnapshot(
            sourceSystem: abilitySystem,
            sourceSpec: null,
            damageEffect: extinguishDamageEffect,
            knockbackEffect: null,
            snapshot: snapshot,
            hitConfirmedTag: null,
            causer: gameObject);

        return CombatHitPayloadApplier.Apply(targetObject, payload, targetObject.transform.position);
    }

    /// <summary>평타1 공격 payload를 만듭니다.</summary>
    internal CombatHitPayload MakeNormal1PayloadValue()
    {
        CombatDamageSnapshot snapshot = new CombatDamageSnapshot(
            finalHpDamage: ResolveNormal1DamageAmountValue(),
            finalStaggerBuildUp: 0f,
            finalKnockbackImpulse: 0f,
            elementBuildUps: null,
            isCriticalHit: false);

        return CombatHitPayload.FromSnapshot(
            sourceSystem: abilitySystem,
            sourceSpec: null,
            damageEffect: ResolveNormal1DamageEffectValue(),
            knockbackEffect: null,
            snapshot: snapshot,
            hitConfirmedTag: null,
            causer: gameObject);
    }

    /// <summary>Fog 생성 위치를 구합니다.</summary>
    internal Vector3 GetFogSpawnPosition(Vector3 center)
    {
        Vector2 fogOffset = GetFogOffset();
        return center - new Vector3(fogOffset.x, fogOffset.y, 0f);
    }

    /// <summary>Fog 반지름을 구합니다.</summary>
    internal float GetFogRadiusValue()
    {
        if (fogPrefab == null) return 0f;

        CircleCollider2D fogCollider = fogPrefab.GetComponent<CircleCollider2D>();
        if (fogCollider == null) return 0f;

        Vector3 scale = fogPrefab.transform.localScale;
        float xRadius = fogCollider.radius * Mathf.Abs(scale.x);
        float yRadius = fogCollider.radius * Mathf.Abs(scale.y);
        return Mathf.Max(xRadius, yRadius);
    }

    /// <summary>촛불 끄기 패턴의 실제 공격 반경을 반환합니다.</summary>
    internal float GetExtinguishAttackRadiusValue()
    {
        return GetFogRadiusValue() * Mathf.Max(0f, ResolveExtinguishAttackRadiusMultiplierValue());
    }

    /// <summary>Fog 오프셋을 구합니다.</summary>
    private Vector2 GetFogOffset()
    {
        if (fogPrefab == null) return Vector2.zero;

        CircleCollider2D fogCollider = fogPrefab.GetComponent<CircleCollider2D>();
        if (fogCollider == null) return Vector2.zero;

        Vector3 scale = Vector3.Scale(fogPrefab.transform.localScale, ResolveExtinguishFogSpawnScaleMultiplierValue());
        return new Vector2(
            fogCollider.offset.x * scale.x,
            fogCollider.offset.y * scale.y);
    }

    /// <summary>평타1 타격 시작 시점을 구합니다.</summary>
    internal float GetNormal1StartTimeValue()
    {
        return GetNormal1CountValue() * GetNormal1IntervalValue();
    }

    /// <summary>현재 50% 패턴이 참조하는 logic asset을 반환합니다.</summary>
    private AbilityLogic_WitchLightAllCandles GetLightAllCandlesLogicConfig()
    {
        AbilityLogic_WitchLightAllCandles currentLogic = PatternRuntime != null && PatternRuntime.CurrentPattern != null && PatternRuntime.CurrentPattern.Ability != null
            ? PatternRuntime.CurrentPattern.Ability.logic as AbilityLogic_WitchLightAllCandles
            : null;
        if (currentLogic != null)
            return currentLogic;

        AbilityLogic_WitchLightAllCandles reservedLogic = PatternRuntime != null && PatternRuntime.ReservedPattern != null && PatternRuntime.ReservedPattern.Ability != null
            ? PatternRuntime.ReservedPattern.Ability.logic as AbilityLogic_WitchLightAllCandles
            : null;
        return reservedLogic != null ? reservedLogic : lightAllCandlesPatternLogicTemplate;
    }

    /// <summary>50% 패턴이 사용할 그로기 상태 효과를 반환합니다.</summary>
    private GameplayEffect ResolveLightAllCandlesGroggyStatusEffect()
    {
        AbilityLogic_WitchLightAllCandles config = GetLightAllCandlesLogicConfig();
        return config != null ? config.GroggyStatusEffect : null;
    }

    /// <summary>50% 패턴이 사용할 맵 전체 경고 스타일을 반환합니다.</summary>
    private AttackTelegraphStyle ResolveMapWideWarningStyle()
    {
        AbilityLogic_WitchLightAllCandles config = GetLightAllCandlesLogicConfig();
        if (config != null && config.MapWideWarningStyleAsset != null)
            return config.MapWideWarningStyleAsset;

        return mapWideWarningStyle;
    }

    /// <summary>현재 촛대로의 피난 패턴이 참조하는 logic asset을 반환합니다.</summary>
    private AbilityLogic_WitchRetreatToCandle GetRetreatLogicConfig()
    {
        AbilityLogic_WitchRetreatToCandle currentLogic = PatternRuntime != null && PatternRuntime.CurrentPattern != null && PatternRuntime.CurrentPattern.Ability != null
            ? PatternRuntime.CurrentPattern.Ability.logic as AbilityLogic_WitchRetreatToCandle
            : null;
        if (currentLogic != null && currentLogic.SkeletonPrefab != null)
            return currentLogic;

        AbilityLogic_WitchRetreatToCandle reservedLogic = PatternRuntime != null && PatternRuntime.ReservedPattern != null && PatternRuntime.ReservedPattern.Ability != null
            ? PatternRuntime.ReservedPattern.Ability.logic as AbilityLogic_WitchRetreatToCandle
            : null;
        if (reservedLogic != null && reservedLogic.SkeletonPrefab != null)
            return reservedLogic;

        AbilityLogic_WitchRetreatToCandle phaseLogic = FindConfiguredRetreatLogic();
        if (phaseLogic != null && phaseLogic.SkeletonPrefab != null)
            return phaseLogic;

        return retreatPatternLogicTemplate;
    }

    /// <summary>촛대로의 피난 패턴이 사용할 해골 프리팹을 반환합니다.</summary>
    internal DeadsSkeleton ResolveRetreatSkeletonPrefabValue()
    {
        AbilityLogic_WitchRetreatToCandle config = GetRetreatLogicConfig();
        return config != null ? config.SkeletonPrefab : null;
    }

    /// <summary>촛대로의 피난 패턴이 사용할 좌측 소환 오프셋을 반환합니다.</summary>
    internal Vector3 ResolveRetreatLeftOffsetValue()
    {
        AbilityLogic_WitchRetreatToCandle config = GetRetreatLogicConfig();
        return config != null ? config.LeftOffset : FallbackRetreatLeftOffset;
    }

    /// <summary>촛대로의 피난 패턴이 사용할 우측 소환 오프셋을 반환합니다.</summary>
    internal Vector3 ResolveRetreatRightOffsetValue()
    {
        AbilityLogic_WitchRetreatToCandle config = GetRetreatLogicConfig();
        return config != null ? config.RightOffset : FallbackRetreatRightOffset;
    }

    /// <summary>촛대로의 피난 패턴이 사용할 해골 자폭 반경을 반환합니다.</summary>
    internal float ResolveRetreatExplosionDiameterValue()
    {
        AbilityLogic_WitchRetreatToCandle config = GetRetreatLogicConfig();
        return config != null ? Mathf.Max(0f, config.SkeletonExplosionDiameter) : FallbackRetreatExplosionDiameter;
    }

    /// <summary>촛대로의 피난 패턴이 사용할 해골 돌진 속도 배율을 반환합니다.</summary>
    internal float ResolveRetreatSpeedScaleValue()
    {
        AbilityLogic_WitchRetreatToCandle config = GetRetreatLogicConfig();
        return config != null ? Mathf.Max(0f, config.SkeletonSpeedScale) : FallbackRetreatSpeedScale;
    }

    /// <summary>현재 촛불 끄기 패턴이 참조하는 logic asset을 반환합니다.</summary>
    private AbilityLogic_WitchExtinguishCandle GetExtinguishLogicConfig()
    {
        AbilityLogic_WitchExtinguishCandle currentLogic = PatternRuntime != null && PatternRuntime.CurrentPattern != null && PatternRuntime.CurrentPattern.Ability != null
            ? PatternRuntime.CurrentPattern.Ability.logic as AbilityLogic_WitchExtinguishCandle
            : null;
        if (currentLogic != null)
            return currentLogic;

        AbilityLogic_WitchExtinguishCandle reservedLogic = PatternRuntime != null && PatternRuntime.ReservedPattern != null && PatternRuntime.ReservedPattern.Ability != null
            ? PatternRuntime.ReservedPattern.Ability.logic as AbilityLogic_WitchExtinguishCandle
            : null;
        return reservedLogic != null ? reservedLogic : extinguishPatternLogicTemplate;
    }

    /// <summary>촛불 끄기 패턴의 폭발 비주얼 프리팹을 반환합니다.</summary>
    internal GameObject ResolveExtinguishExplosionVisualPrefabValue()
    {
        AbilityLogic_WitchExtinguishCandle config = GetExtinguishLogicConfig();
        return config != null ? config.ExplosionVisualPrefab : null;
    }

    /// <summary>촛불 끄기 패턴의 폭발 파티클 프리팹을 반환합니다.</summary>
    internal GameObject ResolveExtinguishExplosionParticlePrefabValue()
    {
        AbilityLogic_WitchExtinguishCandle config = GetExtinguishLogicConfig();
        return config != null ? config.ExplosionParticlePrefab : null;
    }

    /// <summary>촛불 끄기 패턴의 폭발 비주얼 오프셋을 반환합니다.</summary>
    internal Vector3 ResolveExtinguishExplosionVisualOffsetValue()
    {
        AbilityLogic_WitchExtinguishCandle config = GetExtinguishLogicConfig();
        return config != null ? config.ExplosionVisualOffset : Vector3.zero;
    }

    /// <summary>촛불 끄기 패턴의 폭발 비주얼 배율을 반환합니다.</summary>
    internal Vector3 ResolveExtinguishExplosionVisualScaleValue()
    {
        AbilityLogic_WitchExtinguishCandle config = GetExtinguishLogicConfig();
        return config != null ? config.ExplosionVisualScale : Vector3.one;
    }

    /// <summary>촛불 끄기 패턴의 폭발 파티클 오프셋을 반환합니다.</summary>
    internal Vector3 ResolveExtinguishExplosionParticleOffsetValue()
    {
        AbilityLogic_WitchExtinguishCandle config = GetExtinguishLogicConfig();
        return config != null ? config.ExplosionParticleOffset : Vector3.zero;
    }

    /// <summary>촛불 끄기 패턴의 폭발 파티클 배율을 반환합니다.</summary>
    internal Vector3 ResolveExtinguishExplosionParticleScaleValue()
    {
        AbilityLogic_WitchExtinguishCandle config = GetExtinguishLogicConfig();
        return config != null ? config.ExplosionParticleScale : Vector3.one;
    }

    /// <summary>촛불 끄기 패턴의 폭발 사운드를 반환합니다.</summary>
    internal SoundRef ResolveExtinguishExplosionSoundValue()
    {
        AbilityLogic_WitchExtinguishCandle config = GetExtinguishLogicConfig();
        return config != null ? config.ExplosionSound : default;
    }

    internal CapstonePresentation.WorldPresentationHook ResolveExtinguishExplosionPresentationValue()
    {
        AbilityLogic_WitchExtinguishCandle config = GetExtinguishLogicConfig();
        return config != null ? config.GetExplosionPresentation() : default;
    }

    /// <summary>촛불 끄기 패턴의 폭발 카메라 셰이크를 반환합니다.</summary>
    internal CameraShakeHook ResolveExtinguishExplosionCameraShakeValue()
    {
        AbilityLogic_WitchExtinguishCandle config = GetExtinguishLogicConfig();
        return config != null ? config.ExplosionCameraShake : default;
    }

    /// <summary>촛불 끄기 패턴의 Fog 배율 보정을 반환합니다.</summary>
    internal Vector3 ResolveExtinguishFogSpawnScaleMultiplierValue()
    {
        AbilityLogic_WitchExtinguishCandle config = GetExtinguishLogicConfig();
        return config != null ? config.FogSpawnScaleMultiplier : Vector3.one;
    }

    internal CapstonePresentation.SpawnedPresentationHook ResolveExtinguishFogPresentationValue()
    {
        return new CapstonePresentation.SpawnedPresentationHook
        {
            prefab = fogPrefab,
            localOffset = Vector3.zero,
            rotationOffsetZ = 0f,
            scaleMultiplier = ResolveExtinguishFogSpawnScaleMultiplierValue(),
            lifetimeMode = CapstonePresentation.PresentationLifetimeMode.ManualRelease,
            lifetimeOverrideSeconds = 0f,
            useUnscaledTime = false
        };
    }

    /// <summary>촛불 끄기 패턴의 실제 공격 반경 배율을 반환합니다.</summary>
    internal float ResolveExtinguishAttackRadiusMultiplierValue()
    {
        AbilityLogic_WitchExtinguishCandle config = GetExtinguishLogicConfig();
        return config != null ? Mathf.Max(0f, config.AttackRadiusMultiplier) : 6f;
    }

    /// <summary>현재 평타1 패턴이 참조하는 logic asset을 반환합니다.</summary>
    private AbilityLogic_WitchNormalAttack1 GetNormal1LogicConfig()
    {
        AbilityLogic_WitchNormalAttack1 currentLogic = PatternRuntime != null && PatternRuntime.CurrentPattern != null && PatternRuntime.CurrentPattern.Ability != null
            ? PatternRuntime.CurrentPattern.Ability.logic as AbilityLogic_WitchNormalAttack1
            : null;
        if (currentLogic != null && currentLogic.TilePrefab != null && currentLogic.DamageEffect != null)
            return currentLogic;

        AbilityLogic_WitchNormalAttack1 reservedLogic = PatternRuntime != null && PatternRuntime.ReservedPattern != null && PatternRuntime.ReservedPattern.Ability != null
            ? PatternRuntime.ReservedPattern.Ability.logic as AbilityLogic_WitchNormalAttack1
            : null;
        if (reservedLogic != null && reservedLogic.TilePrefab != null && reservedLogic.DamageEffect != null)
            return reservedLogic;

        AbilityLogic_WitchNormalAttack1 phaseLogic = FindConfiguredNormal1Logic();
        if (phaseLogic != null && phaseLogic.TilePrefab != null && phaseLogic.DamageEffect != null)
            return phaseLogic;

        return normalAttack1PatternLogicTemplate;
    }

    /// <summary>현재 페이즈 구성에서 평타1 logic asset을 찾아 반환합니다.</summary>
    private AbilityLogic_WitchNormalAttack1 FindConfiguredNormal1Logic()
    {
        BossPhaseConfig currentPhase = GetCurrentPhase();
        if (currentPhase == null || currentPhase.Patterns == null)
            return null;

        for (int i = 0; i < currentPhase.Patterns.Count; i++)
        {
            BossPatternEntry pattern = currentPhase.Patterns[i];
            AbilityLogic_WitchNormalAttack1 logic = pattern != null && pattern.Ability != null
                ? pattern.Ability.logic as AbilityLogic_WitchNormalAttack1
                : null;

            if (logic != null)
                return logic;
        }

        return null;
    }

    /// <summary>현재 페이즈 구성에서 촛대로의 피난 logic asset을 찾아 반환합니다.</summary>
    private AbilityLogic_WitchRetreatToCandle FindConfiguredRetreatLogic()
    {
        BossPhaseConfig currentPhase = GetCurrentPhase();
        if (currentPhase == null || currentPhase.Patterns == null)
            return null;

        for (int i = 0; i < currentPhase.Patterns.Count; i++)
        {
            BossPatternEntry pattern = currentPhase.Patterns[i];
            AbilityLogic_WitchRetreatToCandle logic = pattern != null && pattern.Ability != null
                ? pattern.Ability.logic as AbilityLogic_WitchRetreatToCandle
                : null;

            if (logic != null)
                return logic;
        }

        return null;
    }

    /// <summary>평타1 타일 개수를 반환합니다.</summary>
    internal int GetNormal1CountValue()
    {
        AbilityLogic_WitchNormalAttack1 config = GetNormal1LogicConfig();
        return config != null ? Mathf.Max(1, config.TileCount) : FallbackNormal1Count;
    }

    /// <summary>평타1 타일 발사 간격을 반환합니다.</summary>
    internal float GetNormal1IntervalValue()
    {
        AbilityLogic_WitchNormalAttack1 config = GetNormal1LogicConfig();
        return config != null ? Mathf.Max(0f, config.IntervalSeconds) : FallbackNormal1Interval;
    }

    /// <summary>평타1 타일 단위 크기를 반환합니다.</summary>
    internal float GetNormal1TileUnitSizeValue()
    {
        AbilityLogic_WitchNormalAttack1 config = GetNormal1LogicConfig();
        return config != null ? Mathf.Max(0.01f, config.TileUnitSize) : FallbackNormal1TileUnitSize;
    }

    /// <summary>평타1 타일 가로 배율을 반환합니다.</summary>
    internal float GetNormal1TileWidthInTilesValue()
    {
        AbilityLogic_WitchNormalAttack1 config = GetNormal1LogicConfig();
        return config != null ? Mathf.Max(0.1f, config.TileWidthInTiles) : FallbackNormal1TileWidthInTiles;
    }

    /// <summary>평타1 타일 세로 배율을 반환합니다.</summary>
    internal float GetNormal1TileHeightInTilesValue()
    {
        AbilityLogic_WitchNormalAttack1 config = GetNormal1LogicConfig();
        return config != null ? Mathf.Max(0.1f, config.TileHeightInTiles) : FallbackNormal1TileHeightInTiles;
    }

    /// <summary>평타1 타격 유지 시간을 반환합니다.</summary>
    internal float GetNormal1HitTimeValue()
    {
        AbilityLogic_WitchNormalAttack1 config = GetNormal1LogicConfig();
        return config != null ? Mathf.Max(0f, config.HitDurationSeconds) : FallbackNormal1HitTime;
    }

    /// <summary>평타1 타일 프리팹을 반환합니다.</summary>
    internal WitchNormalAttack1Tile ResolveNormal1TilePrefabValue()
    {
        AbilityLogic_WitchNormalAttack1 config = GetNormal1LogicConfig();
        return config != null ? config.TilePrefab : null;
    }

    /// <summary>평타1 피해 이펙트를 반환합니다.</summary>
    internal GE_Damage_Spec ResolveNormal1DamageEffectValue()
    {
        AbilityLogic_WitchNormalAttack1 config = GetNormal1LogicConfig();
        return config != null ? config.DamageEffect : null;
    }

    /// <summary>평타1 피해량을 반환합니다.</summary>
    internal float ResolveNormal1DamageAmountValue()
    {
        AbilityLogic_WitchNormalAttack1 config = GetNormal1LogicConfig();
        return config != null ? config.DamageAmount : 1f;
    }

    /// <summary>attack 트리거 유무를 확인합니다.</summary>
    private bool CheckAttackTrigger()
    {
        if (animator == null) return false;

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.type == AnimatorControllerParameterType.Trigger &&
                parameter.name == "attack")
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>경고 스타일을 만듭니다.</summary>
    private AttackTelegraphStyle MakeWarningStyle()
    {
        AttackTelegraphStyle style = ScriptableObject.CreateInstance<AttackTelegraphStyle>();
        style.fillColorStart = new Color(1f, 0f, 0f, 0.35f);
        style.fillColorEnd = new Color(1f, 0f, 0f, 0.35f);
        style.borderColorStart = new Color(1f, 0f, 0f, 1f);
        style.borderColorEnd = new Color(1f, 0f, 0f, 1f);
        style.progressCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        style.blinkStartNormalized = 1f;
        style.blinkFrequency = 0f;
        style.blinkAlphaMin = 1f;
        style.scaleFillWithProgress = false;
        style.fillScaleStart = 1f;
        style.fillScaleEnd = 1f;
        return style;
    }

    /// <summary>맵 전체 직사각 경고에 사용할 스타일을 만듭니다.</summary>
    private AttackTelegraphStyle MakeMapWideWarningStyle()
    {
        AttackTelegraphStyle style = ScriptableObject.CreateInstance<AttackTelegraphStyle>();
        style.fillColorStart = new Color(1f, 0.1f, 0.1f, 0.08f);
        style.fillColorEnd = new Color(1f, 0.1f, 0.1f, 0.26f);
        style.borderColorStart = new Color(1f, 0.4f, 0.4f, 0.75f);
        style.borderColorEnd = new Color(1f, 0.15f, 0.15f, 1f);
        style.progressCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        style.blinkStartNormalized = 0.82f;
        style.blinkFrequency = 9f;
        style.blinkAlphaMin = 0.4f;
        style.scaleFillWithProgress = true;
        style.fillScaleStart = 0f;
        style.fillScaleEnd = 1f;
        return style;
    }

    /// <summary>중앙 기준으로 벽까지 raycast해 맵 직사각 범위를 구합니다.</summary>
    private void ResolveArenaRectangle(Vector3 probeCenter, out Vector3 rectCenter, out Vector2 rectSize)
    {
        const float fallbackHalfExtent = 6f;
        LayerMask wallMask = 1 << WallLayer;

        float leftDistance = GetWallDistance(probeCenter, Vector2.left, wallMask, fallbackHalfExtent);
        float rightDistance = GetWallDistance(probeCenter, Vector2.right, wallMask, fallbackHalfExtent);
        float downDistance = GetWallDistance(probeCenter, Vector2.down, wallMask, fallbackHalfExtent);
        float upDistance = GetWallDistance(probeCenter, Vector2.up, wallMask, fallbackHalfExtent);

        rectCenter = new Vector3(
            probeCenter.x + ((rightDistance - leftDistance) * 0.5f),
            probeCenter.y + ((upDistance - downDistance) * 0.5f),
            probeCenter.z);
        rectSize = new Vector2(
            Mathf.Max(0.5f, leftDistance + rightDistance),
            Mathf.Max(0.5f, downDistance + upDistance));
    }

    /// <summary>지정 방향으로 가장 가까운 벽까지의 거리를 구합니다.</summary>
    private float GetWallDistance(Vector3 origin, Vector2 direction, LayerMask wallMask, float fallbackDistance)
    {
        RaycastHit2D hit = Physics2D.Raycast(origin, direction, fallbackDistance * 4f, wallMask);
        return hit.collider != null
            ? Mathf.Max(0.25f, hit.distance)
            : fallbackDistance;
    }

    /// <summary>씬에 촛대가 하나도 없을 때 테스트용 기본 촛대를 생성합니다.</summary>
    private void EnsureRuntimeCandlesIfNeeded()
    {
        if (candlestickPrefab == null || Candlestick.Instances.Count > 0)
            return;

        float halfExtent = fallbackCandleSpawnRadius / Mathf.Sqrt(2f);
        Vector2[] offsets =
        {
            new Vector2(-halfExtent, halfExtent),
            new Vector2(halfExtent, halfExtent),
            new Vector2(-halfExtent, -halfExtent),
            new Vector2(halfExtent, -halfExtent)
        };

        for (int i = 0; i < offsets.Length; i++)
        {
            Vector3 spawnPosition = transform.position + (Vector3)offsets[i];
            GameObject candleObject = Instantiate(candlestickPrefab, spawnPosition, Quaternion.identity);
            Candlestick candle = candleObject.GetComponent<Candlestick>();
            if (candle != null)
                runtimeSpawnedCandles.Add(candle);
        }
    }

    /// <summary>기본 phase 자산이 비어 있으면 마녀 기본 패턴 구성을 런타임에 만든다.</summary>
    private void ConfigureRuntimePatternsIfNeeded()
    {
        if (!useRuntimeDefaultPatternsWhenPhasesEmpty || ConfiguredPhaseCount > 0 || abilitySystem == null)
            return;

        basicAttack2Ability = CreateRuntimeAbility(
            "AD_Witch_BasicAttack2_Runtime",
            ScriptableObject.CreateInstance<AbilityLogic_WitchBasicAttack2>(),
            castTime: 0f,
            recoveryTime: 0.55f);

        sealedCandleRampageAbility = CreateRuntimeAbility(
            "AD_Witch_SealedCandleRampage_Runtime",
            ScriptableObject.CreateInstance<AbilityLogic_WitchSealedCandleRampage>(),
            castTime: 0f,
            recoveryTime: 0.85f);

        lightAllCandlesAbility = CreateRuntimeAbility(
            "AD_Witch_LightAllCandles_Runtime",
            lightAllCandlesPatternLogicTemplate != null
                ? Instantiate(lightAllCandlesPatternLogicTemplate)
                : ScriptableObject.CreateInstance<AbilityLogic_WitchLightAllCandles>(),
            castTime: 0f,
            recoveryTime: 0.2f);

        BossPatternEntry phase1BasicAttack = BossPatternEntry.CreateRuntime(
            basicAttack2Ability,
            runtimeSelectionWeight: 100,
            runtimeMaxConsecutiveUseCount: 2,
            runtimeMaxUseCount: 0,
            runtimeSelectionLockTime: 0.2f,
            runtimeMinDistanceToTarget: 0f,
            runtimeMaxDistanceToTarget: 99f,
            runtimeMinHpRatio: 0.5f,
            runtimeMaxHpRatio: 1f);

        BossPatternEntry phase2BasicAttack = BossPatternEntry.CreateRuntime(
            basicAttack2Ability,
            runtimeSelectionWeight: 100,
            runtimeMaxConsecutiveUseCount: 2,
            runtimeMaxUseCount: 0,
            runtimeSelectionLockTime: 0.2f,
            runtimeMinDistanceToTarget: 0f,
            runtimeMaxDistanceToTarget: 99f,
            runtimeMinHpRatio: 0f,
            runtimeMaxHpRatio: 0.5f);

        BossPatternEntry rampagePattern = BossPatternEntry.CreateRuntime(
            sealedCandleRampageAbility,
            runtimeSelectionWeight: 140,
            runtimeMaxConsecutiveUseCount: 1,
            runtimeMaxUseCount: 0,
            runtimeSelectionLockTime: 0.6f,
            runtimeMinDistanceToTarget: 0f,
            runtimeMaxDistanceToTarget: 99f,
            runtimeMinHpRatio: 0f,
            runtimeMaxHpRatio: 1f,
            WitchSealedCandlesCondition.CreateRuntime());

        BossPatternEntry lightAllCandlesPattern = BossPatternEntry.CreateRuntime(
            lightAllCandlesAbility,
            runtimeSelectionWeight: 3000,
            runtimeMaxConsecutiveUseCount: 1,
            runtimeMaxUseCount: 1,
            runtimeSelectionLockTime: 0f,
            runtimeMinDistanceToTarget: 0f,
            runtimeMaxDistanceToTarget: 99f,
            runtimeMinHpRatio: 0f,
            runtimeMaxHpRatio: 0.5f);

        SetRuntimePhases(new[]
        {
            BossPhaseConfig.CreateRuntime("Phase 1", 1f, 0.3f, 0.55f, phase1BasicAttack),
            BossPhaseConfig.CreateRuntime("Phase 2", 0.5f, 0.25f, 0.5f, lightAllCandlesPattern, rampagePattern, phase2BasicAttack)
        });
    }

    private AbilityDefinition CreateRuntimeAbility(
        string runtimeAbilityName,
        AbilityLogic runtimeLogic,
        float castTime,
        float recoveryTime,
        List<GameplayTag> runtimeGrantedTags = null)
    {
        AbilityDefinition definition = ScriptableObject.CreateInstance<AbilityDefinition>();
        definition.name = runtimeAbilityName;
        definition.abilityName = runtimeAbilityName;
        definition.castTime = castTime;
        definition.recoveryTime = recoveryTime;
        definition.animationChannel = AbilityDefinition.AnimationChannel.Player;
        definition.animationTrigger = "attack";
        definition.animationTriggerHash = 0;
        definition.logic = runtimeLogic;
        definition.executionPolicy = AbilityDefinition.ExecutionPolicy.ExclusiveQueued;

        if (runtimeGrantedTags != null && runtimeGrantedTags.Count > 0)
            definition.grantedTagsWhileActive = new List<GameplayTag>(runtimeGrantedTags);

        abilitySystem.GiveAbility(definition);
        return definition;
    }

    /// <summary>마녀 전용 런타임 데이터가 준비되었음을 한 번만 기록합니다.</summary>
    private void LogRuntimeDataReadyIfNeeded()
    {
        if (hasLoggedRuntimeDataReady)
            return;

        hasLoggedRuntimeDataReady = true;
        Debug.Log(
            $"[BossFSM] {name}: WitchRuntimeData 준비 완료. sealedCandles={GetSealedCandleCount()}, hasSelection={RuntimeData.HasActiveExtinguishSelection}",
            this);
    }
}
