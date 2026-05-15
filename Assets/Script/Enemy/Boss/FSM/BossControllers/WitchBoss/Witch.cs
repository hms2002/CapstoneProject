using System.Collections;
using System.Collections.Generic;
using CapstoneAudio;
using CapstonePresentation;
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

    [Header("Pattern")]
    [Tooltip("촛대를 끄는 패턴에 사용할 Fog 프리팹입니다.")]
    [SerializeField] private GameObject fogPrefab;
    [SerializeField] private GameObject candlestickPrefab;
    [SerializeField] private GameObject lightBeadPrefab;
    [SerializeField] private Transform phaseTransitionCenterPoint;
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
    private bool hasAttackTrigger;
    private WitchRuntimeData runtimeData;
    private bool hasLoggedRuntimeDataReady;
    private AbilityDefinition basicAttack2Ability;
    private AbilityDefinition sealedCandleRampageAbility;
    private AbilityDefinition lightAllCandlesAbility;
    private readonly List<Candlestick> runtimeSpawnedCandles = new();
    private readonly List<LightBeadProjectile2D> activeRampageProjectiles = new();
    private readonly List<DeadsSkeleton> activeRetreatSummons = new();
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
        if (ShouldClearRampageProjectilesOnPatternEnd(forced))
            ClearActiveRampageProjectiles();
        if (forced)
            ClearActiveRetreatSummons();

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
    public float ProjectileSpeed => projectileSpeed;
    public bool HasProjectilePatternConfig => lightBeadPrefab != null;
    public WitchShieldController ShieldController => shieldController;
    public AttackTelegraphService ExtinguishTelegraphService => telegraphService;
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
        ClearActiveRetreatSummons();
        ClearActiveRampageProjectiles();
        base.OnDestroy();
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

    /// <summary>촛대 폭주 패턴인지 확인합니다.</summary>
    private bool IsSealedCandleRampagePattern(BossPatternEntry patternEntry)
    {
        if (patternEntry == null || patternEntry.Ability == null)
            return false;

        return patternEntry.Ability.logic is AbilityLogic_WitchSealedCandleRampage;
    }

    /// <summary>
    /// 책임 :
    /// - 촛대 폭주 탄막은 일반 패턴 전환에서는 유지하고, 전투가 강하게 끊기는 사망/그로기 상황에서만 회수 여부를 판정한다.
    /// - 패턴 종료 훅이 다양한 이유로 호출돼도 탄막 회수 타이밍을 한 곳에서 일관되게 유지한다.
    /// </summary>
    private bool ShouldClearRampageProjectilesOnPatternEnd(bool forced)
    {
        if (IsDead)
            return true;

        if (!forced)
            return false;

        return HasDeadTag() || HasGroggyTag();
    }

    /// <summary>촛불 끄기 패턴을 시작합니다.</summary>
    public bool StartExtinguish(AbilityLogic_WitchExtinguishCandle logic, float warningTime)
    {
        if (!TryBuildExtinguishPatternContext(logic, warningTime, out WitchExtinguishPatternExecutor.PatternContext context, out _))
            return false;

        return extinguishPatternExecutor != null &&
               extinguishPatternExecutor.TryBeginPattern(context, out _);
    }

    /// <summary>
    /// 책임 :
    /// - FSM state가 촛불 끄기 패턴 시작을 구체 Witch 구현 대신 브리지 계약으로 요청할 수 있게 한다.
    /// - 실행 지속시간을 함께 반환해 state가 내부 계산 메서드에 직접 의존하지 않게 만든다.
    /// </summary>
    public bool TryBeginExtinguishPattern(AbilityLogic_WitchExtinguishCandle logic, float warningTimeSeconds, out float resolvedDurationSeconds)
    {
        if (!TryBuildExtinguishPatternContext(logic, warningTimeSeconds, out WitchExtinguishPatternExecutor.PatternContext context, out resolvedDurationSeconds))
            return false;

        if (extinguishPatternExecutor == null)
            return false;

        return extinguishPatternExecutor.TryBeginPattern(context, out resolvedDurationSeconds);
    }

    /// <summary>평타1 장판 공격을 시작합니다.</summary>
    public bool StartNormal1(AbilityLogic_WitchNormalAttack1 logic)
    {
        if (!TryBuildNormalAttack1PatternContext(logic, out WitchNormalAttack1PatternExecutor.PatternContext context, out _))
            return false;

        bool executorSucceeded = normalAttack1PatternExecutor != null && normalAttack1PatternExecutor.TryBeginPattern(context);
        if (executorSucceeded)
            return true;

        Debug.LogWarning("[Witch] 평타1 executor 경로가 실패하여 inline fallback을 사용합니다.", this);
        return TryBeginNormalAttack1InlineFallback(context);
    }

    /// <summary>
    /// 책임 :
    /// - FSM state가 평타1 패턴 시작과 대기 시간을 브리지 계약으로 받도록 돕는다.
    /// - 패턴 내부 구현이 바뀌어도 state는 반환된 지속시간만 사용하게 만들어 결합을 줄인다.
    /// </summary>
    public bool TryBeginNormalAttack1Pattern(AbilityLogic_WitchNormalAttack1 logic, out float resolvedDurationSeconds)
    {
        if (!TryBuildNormalAttack1PatternContext(logic, out _, out resolvedDurationSeconds))
            return false;

        return StartNormal1(logic);
    }

    /// <summary>촛대로의 피난 패턴을 시작합니다.</summary>
    public bool StartRetreat(AbilityLogic_WitchRetreatToCandle logic)
    {
        if (!TryBuildRetreatPatternContext(logic, out WitchRetreatPatternExecutor.PatternContext context))
            return false;

        bool executorSucceeded = retreatPatternExecutor != null && retreatPatternExecutor.TryBeginPattern(context);
        if (executorSucceeded)
            return true;

        Debug.LogWarning("[Witch] 피난 executor 경로가 실패하여 inline fallback을 사용합니다.", this);
        return TryBeginRetreatInlineFallback(context);
    }

    /// <summary>
    /// 책임 :
    /// - FSM state가 촛대로의 피난 패턴 시작을 브리지 계약으로 호출하게 한다.
    /// - 이후 피난 패턴 구현이 runner나 다른 실행기로 바뀌어도 state 호출 형태를 유지하게 한다.
    /// </summary>
    public bool TryBeginRetreatPattern(AbilityLogic_WitchRetreatToCandle logic)
    {
        return StartRetreat(logic);
    }

    /// <summary>촛불 끄기 패턴을 끝냅니다.</summary>
    public void FinishExtinguish()
    {
        if (TryBuildExtinguishPatternContext(null, 0f, out WitchExtinguishPatternExecutor.PatternContext context, out _))
            extinguishPatternExecutor?.CompletePattern(context);
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
    /// - 촛대 폭주 패턴이 생성한 투사체를 등록해 보스 사망/강제 종료 시 한 번에 회수할 수 있게 한다.
    /// - 이미 파괴된 투사체 참조는 정리하면서 중복 등록을 막는다.
    /// </summary>
    public void RegisterRampageProjectile(LightBeadProjectile2D projectile)
    {
        if (projectile == null)
            return;

        CleanupNullRampageProjectiles();
        if (activeRampageProjectiles.Contains(projectile))
            return;

        activeRampageProjectiles.Add(projectile);
    }

    /// <summary>
    /// 책임 :
    /// - 촛대 폭주 투사체가 자연 소멸하거나 적중했을 때 등록 목록에서 빠지게 한다.
    /// - 회수 목록에 이미 없는 투사체는 조용히 무시한다.
    /// </summary>
    public void UnregisterRampageProjectile(LightBeadProjectile2D projectile)
    {
        if (projectile == null)
            return;

        activeRampageProjectiles.Remove(projectile);
    }

    /// <summary>
    /// 책임 :
    /// - 피난 패턴이 소환한 강화 해골을 등록해 보스 사망/강제 종료 때 Die 경로로 정리할 수 있게 한다.
    /// - 이미 사라진 참조를 정리하면서 중복 등록을 막아 목록을 안정적으로 유지한다.
    /// </summary>
    public void RegisterRetreatSummon(DeadsSkeleton skeleton)
    {
        if (skeleton == null)
            return;

        CleanupNullRetreatSummons();
        if (activeRetreatSummons.Contains(skeleton))
            return;

        activeRetreatSummons.Add(skeleton);
    }

    /// <summary>
    /// 책임 :
    /// - 보스 사망/패턴 강제 종료 시 남아 있는 촛대 폭주 투사체를 즉시 제거한다.
    /// - 다음 패턴이나 연출 중 lingering projectile이 플레이어를 계속 위협하지 않게 정리한다.
    /// </summary>
    public void ClearActiveRampageProjectiles()
    {
        for (int i = activeRampageProjectiles.Count - 1; i >= 0; i--)
        {
            LightBeadProjectile2D projectile = activeRampageProjectiles[i];
            if (projectile != null)
                CombatEntityCleanupUtil.Cleanup(projectile.gameObject, gameObject);
        }

        activeRampageProjectiles.Clear();
    }

    /// <summary>
    /// 책임 :
    /// - 보스 사망/강제 종료 시 피난 패턴이 남긴 강화 해골들에게 Die 경로를 요청한다.
    /// - 해골이 가진 드롭, 사망 연출, 내부 패턴 정리가 Destroy 대신 공통 사망 흐름을 타게 만든다.
    /// </summary>
    public void ClearActiveRetreatSummons()
    {
        for (int i = activeRetreatSummons.Count - 1; i >= 0; i--)
        {
            DeadsSkeleton skeleton = activeRetreatSummons[i];
            if (skeleton != null)
                CombatEntityCleanupUtil.Cleanup(skeleton.gameObject, gameObject);
        }

        activeRetreatSummons.Clear();
    }

    private void CleanupNullRampageProjectiles()
    {
        for (int i = activeRampageProjectiles.Count - 1; i >= 0; i--)
        {
            if (activeRampageProjectiles[i] == null)
                activeRampageProjectiles.RemoveAt(i);
        }
    }

    private void CleanupNullRetreatSummons()
    {
        for (int i = activeRetreatSummons.Count - 1; i >= 0; i--)
        {
            if (activeRetreatSummons[i] == null)
                activeRetreatSummons.RemoveAt(i);
        }
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

        if (!TryAddStateTag(staggerImmuneTag, 1))
            return;

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

        if (!TryRemoveStateTag(staggerImmuneTag, 1))
            return;

        hasAppliedStaggerImmuneTag = false;
    }

    /// <summary>평타1 장판을 모두 지웁니다.</summary>
    public void ClearNormal1()
    {
        RuntimeData.ClearNormal1Tiles();
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
    internal Vector3 GetNormal1TilePoint(Vector2 aimDir, int index, Vector2 tileSize)
    {
        float forwardSize = tileSize.x;
        float distance = (forwardSize * 0.5f) + (forwardSize * index);
        Vector3 offset = new Vector3(aimDir.x, aimDir.y, 0f) * distance;
        return transform.position + offset;
    }

    /// <summary>평타1 executor가 실패했을 때 기존 인라인 경로로 장판 공격을 복구합니다.</summary>
    private bool TryBeginNormalAttack1InlineFallback(in WitchNormalAttack1PatternExecutor.PatternContext context)
    {
        if (abilitySystem == null || Target == null)
        {
            Debug.LogWarning(
                $"[Witch] 평타1 inline fallback 실패: abilitySystem={(abilitySystem != null)}, target={(Target != null)}",
                this);
            return false;
        }

        if (context.TilePrefab == null || context.DamageEffect == null)
        {
            Debug.LogWarning(
                $"[Witch] 평타1 inline fallback 실패: tilePrefab={(context.TilePrefab != null)}, damageEffect={(context.DamageEffect != null)}",
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
        CombatHitPayload payload = MakeNormal1Payload(context.DamageEffect, context.DamageAmount);
        float startTime = context.TileCount * context.IntervalSeconds;

        for (int i = 0; i < context.TileCount; i++)
        {
            WitchNormalAttack1Tile tile = Instantiate(
                context.TilePrefab,
                GetNormal1TilePoint(aimDir, i, context.TileSize),
                Quaternion.Euler(0f, 0f, angle));

            runtimeData.AddNormal1Tile(tile);
            tile.Play(
                Target.gameObject,
                payload,
                context.TileSize,
                angle,
                i * context.IntervalSeconds,
                startTime + (i * context.IntervalSeconds),
                context.WarningTelegraphStyle,
                context.HitTelegraphStyle);
        }

        Debug.Log($"[Witch] 평타1 inline fallback 실행 성공: tileCount={context.TileCount}, interval={context.IntervalSeconds}", this);
        return true;
    }

    /// <summary>피난 executor가 실패했을 때 기존 인라인 경로로 강화 해골 소환을 복구합니다.</summary>
    private bool TryBeginRetreatInlineFallback(in WitchRetreatPatternExecutor.PatternContext context)
    {
        if (context.SkeletonPrefab == null)
        {
            Debug.LogWarning("[Witch] 피난 inline fallback 실패: skeletonPrefab이 없습니다.", this);
            return false;
        }

        PlayPatternAttackMotion();
        bool spawnedLeft = SpawnRetreatSkeletonInlineFallback(context.SkeletonPrefab, context.LeftOffset, context.ExplosionDiameter, context.SpeedScale);
        bool spawnedRight = SpawnRetreatSkeletonInlineFallback(context.SkeletonPrefab, context.RightOffset, context.ExplosionDiameter, context.SpeedScale);
        Debug.Log($"[Witch] 피난 inline fallback 실행 결과: left={spawnedLeft}, right={spawnedRight}", this);
        return spawnedLeft || spawnedRight;
    }

    /// <summary>기존 피난 패턴 방식으로 강화 해골 하나를 소환합니다.</summary>
    private bool SpawnRetreatSkeletonInlineFallback(DeadsSkeleton skeletonPrefab, Vector3 localOffset, float explosionDiameter, float speedScale)
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

        skeleton.SetBoost(Target, explosionDiameter, speedScale, true);
        RegisterRetreatSummon(skeleton);
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
    public void ApplyGroggyStatus(GameplayEffect groggyStatusEffect)
    {
        TryApplySelfEffect(groggyStatusEffect);
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
    public void ShowMapWideWarning(Vector3 center, float warningTime, AttackTelegraphStyle warningStyle)
    {
        if (telegraphService == null || warningStyle == null)
            return;

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
    public bool ApplyMapWideDamage(GE_Damage_Spec damageEffect, float damageAmount, GameObject explicitTarget = null)
    {
        if (abilitySystem == null || damageEffect == null)
            return false;

        GameObject targetObject = explicitTarget != null
            ? explicitTarget
            : Target != null ? Target.gameObject : null;
        if (targetObject == null)
            return false;

        CombatDamageSnapshot snapshot = new CombatDamageSnapshot(
            finalHpDamage: damageAmount,
            finalStaggerBuildUp: 0f,
            finalKnockbackImpulse: 0f,
            isCriticalHit: false);

        CombatHitPayload payload = CombatHitPayload.FromSnapshot(
            sourceSystem: abilitySystem,
            sourceSpec: null,
            damageEffect: damageEffect,
            knockbackEffect: null,
            snapshot: snapshot,
            hitConfirmedTag: null,
            causer: gameObject);

        return CombatHitPayloadApplier.Apply(targetObject, payload, targetObject.transform.position);
    }

    /// <summary>평타1 공격 payload를 만듭니다.</summary>
    internal CombatHitPayload MakeNormal1Payload(GE_Damage_Spec damageEffect, float damageAmount)
    {
        CombatDamageSnapshot snapshot = new CombatDamageSnapshot(
            finalHpDamage: damageAmount,
            finalStaggerBuildUp: 0f,
            finalKnockbackImpulse: 0f,
            isCriticalHit: false);

        return CombatHitPayload.FromSnapshot(
            sourceSystem: abilitySystem,
            sourceSpec: null,
            damageEffect: damageEffect,
            knockbackEffect: null,
            snapshot: snapshot,
            hitConfirmedTag: null,
            causer: gameObject);
    }

    /// <summary>촛불 끄기 패턴 실행에 필요한 문맥과 총 지속시간을 구성합니다.</summary>
    private bool TryBuildExtinguishPatternContext(
        AbilityLogic_WitchExtinguishCandle logic,
        float warningTimeSeconds,
        out WitchExtinguishPatternExecutor.PatternContext context,
        out float resolvedDurationSeconds)
    {
        context = default;
        resolvedDurationSeconds = Mathf.Max(0f, warningTimeSeconds);

        AbilityLogic_WitchExtinguishCandle config = logic != null ? logic : extinguishPatternLogicTemplate;
        if (fogPrefab == null || config == null)
        {
            Debug.LogWarning(
                $"[Witch] 촛불 끄기 문맥 구성 실패: fogPrefab={(fogPrefab != null)}, logic={(config != null)}",
                this);
            return false;
        }

        GE_Damage_Spec damageEffect = config.DamageEffect;
        AttackTelegraphStyle warningStyle = config.WarningTelegraphStyle;
        if (warningStyle == null && extinguishPatternLogicTemplate != null && config != extinguishPatternLogicTemplate)
        {
            warningStyle = extinguishPatternLogicTemplate.WarningTelegraphStyle;
            Debug.LogWarning(
                "[Witch] 촛불 끄기 warningStyle이 비어 있어 pattern logic template의 스타일로 fallback합니다.",
                this);
        }

        if (damageEffect == null || warningStyle == null)
        {
            Debug.LogWarning(
                $"[Witch] 촛불 끄기 문맥 구성 실패: damageEffect={(damageEffect != null)}, warningStyle={(warningStyle != null)}",
                this);
            return false;
        }

        context = new WitchExtinguishPatternExecutor.PatternContext(
            resolvedDurationSeconds,
            warningStyle,
            damageEffect,
            config.DamageAmount,
            new SpawnedPresentationHook
            {
                prefab = fogPrefab,
                localOffset = Vector3.zero,
                rotationOffsetZ = 0f,
                scaleMultiplier = config.FogSpawnScaleMultiplier,
                lifetimeMode = PresentationLifetimeMode.ManualRelease,
                lifetimeOverrideSeconds = 0f,
                useUnscaledTime = false
            },
            Mathf.Max(0f, config.AttackRadiusMultiplier),
            config.GetExplosionPresentation());
        return true;
    }

    /// <summary>피난 패턴 실행에 필요한 문맥을 구성합니다.</summary>
    private bool TryBuildRetreatPatternContext(
        AbilityLogic_WitchRetreatToCandle logic,
        out WitchRetreatPatternExecutor.PatternContext context)
    {
        context = default;

        AbilityLogic_WitchRetreatToCandle config = logic != null ? logic : retreatPatternLogicTemplate;
        if (config == null || config.SkeletonPrefab == null)
            return false;

        context = new WitchRetreatPatternExecutor.PatternContext(
            config.SkeletonPrefab,
            config.LeftOffset,
            config.RightOffset,
            Mathf.Max(0f, config.SkeletonExplosionDiameter),
            Mathf.Max(0f, config.SkeletonSpeedScale));
        return true;
    }

    /// <summary>평타1 패턴 실행에 필요한 문맥과 총 지속시간을 구성합니다.</summary>
    private bool TryBuildNormalAttack1PatternContext(
        AbilityLogic_WitchNormalAttack1 logic,
        out WitchNormalAttack1PatternExecutor.PatternContext context,
        out float resolvedDurationSeconds)
    {
        context = default;
        resolvedDurationSeconds = 0f;

        AbilityLogic_WitchNormalAttack1 config = logic != null ? logic : normalAttack1PatternLogicTemplate;
        if (config == null)
            return false;

        WitchNormalAttack1Tile tilePrefab = config.TilePrefab;
        GE_Damage_Spec damageEffect = config.DamageEffect;
        if (tilePrefab == null || damageEffect == null)
            return false;

        int tileCount = Mathf.Max(1, config.TileCount);
        float intervalSeconds = Mathf.Max(0f, config.IntervalSeconds);
        float hitDurationSeconds = Mathf.Max(0f, config.HitDurationSeconds);
        float tileUnitSize = Mathf.Max(0.01f, config.TileUnitSize);
        float tileWidthInTiles = Mathf.Max(0.1f, config.TileWidthInTiles);
        float tileHeightInTiles = Mathf.Max(0.1f, config.TileHeightInTiles);
        Vector2 tileSize = new Vector2(tileUnitSize * tileWidthInTiles, tileUnitSize * tileHeightInTiles);

        context = new WitchNormalAttack1PatternExecutor.PatternContext(
            tilePrefab,
            damageEffect,
            config.DamageAmount,
            tileCount,
            intervalSeconds,
            hitDurationSeconds,
            tileSize,
            config.WarningTelegraphStyle,
            config.HitTelegraphStyle);

        float startTime = tileCount * intervalSeconds;
        resolvedDurationSeconds = startTime + ((tileCount - 1) * intervalSeconds) + hitDurationSeconds;
        return true;
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

        TryRegisterAbility(definition);
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
