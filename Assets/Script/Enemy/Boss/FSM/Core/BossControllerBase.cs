using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

public abstract class BossControllerBase : Enemy, IBossAbilityStateBridge
{
    // 이 클래스의 책임:
    // Enemy의 공통 전투/사망 처리 위에 보스 전용 전투 상태, 페이즈, 반응 전환을 조율한다.

    private const float TargetRefreshRetryIntervalSeconds = 0.25f;

    [Header("Encounter")]
    [SerializeField] private bool startCombatOnStart = true;

    [Header("Target")]
    [Tooltip("기본 전투 타겟입니다. 필요 시 런타임에 교체할 수 있습니다.")]
    [SerializeField] private Transform initialTarget;

    [Space(8)]

    [Header("Health References")]
    [Tooltip("현재 체력 Attribute입니다.")]
    [SerializeField] private AttributeDefinition healthAttribute;

    [Tooltip("최대 체력 Attribute입니다.")]
    [SerializeField] private AttributeDefinition maxHealthAttribute;

    [Space(8)]

    [Header("HUD")]
    [Tooltip("이 보스가 HUD 등록 시 요청할 체력바 테마입니다. 비워두면 HUD 슬롯 프리팹 기본 프레임을 사용합니다.")]
    [SerializeField] private BossHudHealthBarTheme hudHealthBarTheme;

    [Space(8)]

    [Header("Reactive Tags")]
    [Tooltip("이 태그가 있으면 DeadState로 전환합니다.")]
    [SerializeField] private GameplayTag deadTag;

    [Tooltip("이 태그가 있으면 GroggyState로 전환합니다.")]
    [SerializeField] private GameplayTag groggyTag;

    [Space(8)]

    [Header("Phase Data")]
    [Tooltip("페이즈 순서대로 배치합니다. 예: Phase1(1.0), Phase2(0.65), Phase3(0.3)")]
    [SerializeField] private List<BossPhaseConfig> phases = new();

    private BossBlackboard blackboard;
    private BossPatternRuntimeState patternRuntime;
    private BossStateMachine stateMachine;

    private BossSpawnState spawnState;
    private BossEncounterIntroState encounterIntroState;
    private BossCombatIdleState combatIdleState;
    private BossPatternSelectState patternSelectState;
    private BossPatternExecuteState patternExecuteState;
    private BossGroggyState groggyState;
    private BossDeadState deadState;

    // 보스 전용 드롭 처리의 책임을 이 컨트롤러에서 맡기 위한 참조입니다.
    private BossEncounterDirector encounterDirector;
    private BossTalkManager bossTalkManager;
    private BossDeathPresentation deathPresentation;
    private BossSpeechController speechController;

    private bool combatActive;
    private bool hasCombatOverride;
    private bool encounterIntroFinished;
    private bool hasInitializedBossRuntime;
    private float nextTargetRefreshTime;

    public AbilitySystem AbilitySystem => abilitySystem;
    public TagSystem TagSystem => tagSystem;
    public AttributeSet AttributeSet => attributeSet;
    public BossBlackboard Blackboard => blackboard;
    public BossPatternRuntimeState PatternRuntime => patternRuntime;
    public BossStateMachine StateMachine => stateMachine;
    public Transform CurrentTarget => Target;
    public override Transform Target => target;
    protected int ConfiguredPhaseCount => phases != null ? phases.Count : 0;
    protected IReadOnlyList<BossPhaseConfig> ConfiguredPhases => phases;
    public float CurrentHealthRatio => GetCurrentHpRatio();
    public float CurrentHealthValue => GetCurrentHealthValue();
    public float MaxHealthValue => GetCurrentMaxHealthValue();
    public bool IsAbilityExecutionBusy => abilitySystem != null && abilitySystem.IsBusy;
    /// <summary>
    /// 책임 :
    /// - 보스가 그로기처럼 전역 제압 상태에 들어가 능력 실행 커밋이 막혀야 하는지를 공통 bridge 계약으로 노출한다.
    /// - 몬스터/보스가 같은 AI-ASC 상호작용 규칙을 공유해도 각자 기존 reactive tag 구조를 유지하게 돕는다.
    /// </summary>
    public bool IsAbilityExecutionSuppressed => HasGroggyTag();

    protected override void Awake()
    {
        base.Awake();

        CacheComponents();
        speechController = GetComponent<BossSpeechController>();
        ResolveDeathPresentation();

        blackboard = CreateBlackboard();
        patternRuntime = CreatePatternRuntimeState();
        stateMachine = new BossStateMachine(blackboard);
        CreateStates();
        hasInitializedBossRuntime = true;
    }

    protected override void Start()
    {
        base.Start();

        RegisterConfiguredPatternAbilities();

        if (initialTarget != null)
            SetTarget(initialTarget);

        blackboard.SetPhaseIndex(EvaluatePhaseIndexByHealthRatio(GetCurrentHpRatio()));
        stateMachine.ChangeState(spawnState);
        if (hasCombatOverride)
        {
            SyncBossHudRegistration();
        }
        else
        {
            SetCombatActive(startCombatOnStart);
        }
    }

    protected virtual void Update()
    {
        bool canTickStateMachine = combatActive ||
                                   (stateMachine != null && stateMachine.CurrentState == encounterIntroState);
        if (!canTickStateMachine) return;

        EnsureCombatTarget();
        blackboard.Tick(Time.deltaTime, Target, GetCurrentHpRatio());

        EvaluatePhaseChange();
        EvaluateReactiveStateTransitions();

        stateMachine.Update();
    }

    protected virtual void CacheComponents()
    {
        if (abilitySystem == null) abilitySystem = GetComponent<AbilitySystem>();
        if (tagSystem == null) tagSystem = GetComponent<TagSystem>();
        if (attributeSet == null) attributeSet = GetComponent<AttributeSet>();
    }

    public void SetCombatTarget(Transform newTarget)
    {
        SetTarget(newTarget);
    }

    public bool IsCombatActive => combatActive;

    public BossHudHealthBarTheme HudHealthBarTheme => hudHealthBarTheme;

    public void SetCombatActive(bool isActive)
    {
        combatActive = isActive;
        hasCombatOverride = true;

        if (!isActive && hasInitializedBossRuntime)
            AbortCurrentPattern();

        RunProgressCoordinator coordinator = RunProgressCoordinator.EnsureInstance();
        if (isActive)
            coordinator?.NotifyBossCombatStarted(this);
        else
            coordinator?.NotifyBossCombatEnded(this);

        SyncBossHudRegistration();
    }

    public void BeginCombatEncounter(Transform combatTarget = null)
    {
        if (combatTarget != null)
            SetTarget(combatTarget);
        else if (Target == null)
            RefreshTarget();

        SetCombatActive(true);
    }

    public bool SpeakSituation(BossSpeechSituationEnum situation, float duration = 2f)
    {
        ResolveSpeechController();
        return speechController != null && speechController.TrySpeakSituation(situation, duration);
    }

    public void ChangeState(BossState nextState)
    {
        stateMachine.ChangeState(nextState);
    }

    public BossState GetCombatIdleState()
    {
        return combatIdleState;
    }

    public BossState GetEncounterIntroState()
    {
        return encounterIntroState;
    }

    public BossState GetPatternSelectState()
    {
        return patternSelectState;
    }

    public BossState GetPatternExecuteState()
    {
        return patternExecuteState;
    }

    /// <summary>선택된 패턴에 맞는 실행 상태를 돌려줍니다.</summary>
    public virtual BossState GetPatternState(BossPatternEntry patternEntry)
    {
        return patternExecuteState;
    }

    public BossState GetGroggyState()
    {
        return groggyState;
    }

    public BossState GetDeadState()
    {
        return deadState;
    }

    public BossPhaseConfig GetCurrentPhase()
    {
        if (phases == null || phases.Count == 0)
            return null;

        int phaseIndex = Mathf.Clamp(blackboard.CurrentPhaseIndex, 0, phases.Count - 1);
        return phases[phaseIndex];
    }

    public float GetCurrentPhaseThinkDelay()
    {
        BossPhaseConfig currentPhase = GetCurrentPhase();
        if (currentPhase == null)
            return 0.2f;

        return Random.Range(currentPhase.ThinkDelayMin, currentPhase.ThinkDelayMax);
    }

    public virtual BossPatternEntry SelectNextPattern()
    {
        EnsureCombatTarget(forceRefresh: true);
        if (CombatTargetDeathUtility.IsPlayerDeathSequenceRunning(Target))
            return null;

        blackboard.Tick(0f, Target, GetCurrentHpRatio());

        BossPatternEntry followUpPattern = TrySelectQueuedFollowUpPattern();
        if (followUpPattern != null)
            return followUpPattern;

        return BossPatternSelector.Select(this, blackboard, GetCurrentPhase());
    }

    /// <summary>
    /// 책임:
    /// 보스가 Start 시점에 플레이어를 찾지 못했더라도 전투 루프 중 필요한 순간에 타겟을 재획득한다.
    /// </summary>
    private void EnsureCombatTarget(bool forceRefresh = false)
    {
        if (Target != null)
            return;

        if (!forceRefresh && Time.time < nextTargetRefreshTime)
            return;

        nextTargetRefreshTime = Time.time + TargetRefreshRetryIntervalSeconds;
        TryRefreshTarget(logWarning: false);
    }

    /// <summary>패턴 평가 결과를 반환합니다.</summary>
    public virtual BossPatternEvalResult EvaluatePattern(BossPatternEntry patternEntry)
    {
        if (patternEntry == null) return BossPatternEvalResult.HardFail("패턴이 없습니다.");

        BossPatternEvalContext context = new BossPatternEvalContext(this, blackboard, patternRuntime);
        BossPatternEvalResult result = patternEntry.Evaluate(context);
        return AdjustPatternEval(patternEntry, result);
    }

    public bool TryStartPattern(BossPatternEntry patternEntry)
    {
        if (patternEntry == null || abilitySystem == null) return false;

        if (CombatTargetDeathUtility.IsPlayerDeathSequenceRunning(Target))
            return false;

        if (abilitySystem.IsBusy) return false;

        BossPatternEvalResult result = patternRuntime != null &&
                                       patternRuntime.ReservedPattern == patternEntry &&
                                       patternRuntime.ReservedPatternIsForcedFollowUp
            ? EvaluateForcedFollowUpPattern(patternEntry)
            : EvaluatePattern(patternEntry);
        if (!result.CanUse) return false;

        GameObject targetObject = Target != null ? Target.gameObject : null;
        bool isActivated = abilitySystem.TryActivateAbility(patternEntry.Ability, targetObject);

        if (!isActivated)
            return false;

        patternRuntime.BeginPattern(patternEntry);
        return true;
    }

    /// <summary>
    /// 책임 :
    /// - FSM state가 AbilitySystem 구체 구현을 직접 모르고도 능력 시작을 요청할 수 있게 한다.
    /// - 명시 타깃이 없으면 현재 보스 타깃을 기본값으로 사용해 공통 실행 문맥을 맞춘다.
    /// </summary>
    public bool TryStartAbility(AbilityDefinition ability, GameObject explicitTarget = null)
    {
        if (abilitySystem == null || ability == null)
            return false;

        Transform targetTransform = explicitTarget != null
            ? explicitTarget.transform
            : Target;
        if (CombatTargetDeathUtility.IsPlayerDeathSequenceRunning(targetTransform))
            return false;

        GameObject targetObject = explicitTarget != null
            ? explicitTarget
            : Target != null ? Target.gameObject : null;

        return abilitySystem.TryActivateAbility(ability, targetObject);
    }

    /// <summary>
    /// 책임 :
    /// - FSM state가 현재 실행 중인 능력을 취소할 때 casting/execution 세부 API를 직접 몰라도 되게 한다.
    /// - 취소 정책(force 여부)은 state가 전달하고, 실제 ASC 호출은 컨트롤러가 책임진다.
    /// </summary>
    public void CancelActiveAbility(bool force)
    {
        if (abilitySystem == null)
            return;

        if (abilitySystem.IsCasting)
            abilitySystem.CancelCasting(force);

        if (abilitySystem.IsExecuting)
            abilitySystem.CancelExecution(force);
    }

    /// <summary>
    /// 책임 :
    /// - FSM state가 TagSystem 구현 세부사항 대신 공통 브리지를 통해 상태 태그를 조회하게 한다.
    /// - 상태 전환 조건이 태그 시스템 교체에 덜 민감하도록 조회 경로를 한 곳으로 모은다.
    /// </summary>
    public bool HasStateTag(GameplayTag tag)
    {
        return tag != null && tagSystem != null && tagSystem.HasTag(tag);
    }

    /// <summary>
    /// 책임 :
    /// - 보스 파생 구현이 TagSystem 구체 API를 직접 모르고도 상태 태그를 추가하게 한다.
    /// - 보호막/면역처럼 보스 전용 규칙이 태그 표현을 쓰더라도 공통 컨트롤러가 태그 적용 책임을 가진다.
    /// </summary>
    protected bool TryAddStateTag(GameplayTag tag, int count = 1)
    {
        if (tagSystem == null || tag == null || count <= 0)
            return false;

        tagSystem.AddTag(tag, count);
        return true;
    }

    /// <summary>
    /// 책임 :
    /// - 보스 파생 구현이 TagSystem 구체 API를 직접 모르고도 상태 태그를 회수하게 한다.
    /// - 전투 종료/패턴 종료 시 보스 전용 태그 정리 경로를 공통 컨트롤러로 모은다.
    /// </summary>
    protected bool TryRemoveStateTag(GameplayTag tag, int count = 1)
    {
        if (tagSystem == null || tag == null || count <= 0)
            return false;

        tagSystem.RemoveTag(tag, count);
        return true;
    }

    /// <summary>
    /// 책임 :
    /// - 보스 파생 구현이 EffectRunner 구체 경로 대신 공통 컨트롤러를 통해 자기 자신에게 GE를 적용하게 한다.
    /// - 그로기/보호막 같은 보스 전용 반응 효과의 적용 실패 여부를 한 곳에서 판정하게 한다.
    /// </summary>
    protected bool TryApplySelfEffect(GameplayEffect effect, GameObject sourceObject = null)
    {
        if (effectRunner == null || effect == null)
            return false;

        GameObject source = sourceObject != null ? sourceObject : gameObject;
        effectRunner.ApplyEffect(effect, source, gameObject);
        return true;
    }

    /// <summary>
    /// 책임 :
    /// - 보스가 런타임/설정 phase에서 쓰는 ability 등록을 공통 컨트롤러를 통해 수행하게 한다.
    /// - 파생 구현이 AbilitySystem.GiveAbility 세부 호출을 직접 알지 않도록 등록 표면을 통일한다.
    /// </summary>
    protected bool TryRegisterAbility(AbilityDefinition ability)
    {
        if (abilitySystem == null || ability == null)
            return false;

        abilitySystem.GiveAbility(ability);
        return true;
    }

    public void FinishCurrentPattern()
    {
        BossPatternEntry finishedPattern = patternRuntime != null ? patternRuntime.CurrentPattern : null;
        OnPatternEnd(finishedPattern, false);
        QueueFollowUpPattern(finishedPattern);
        patternRuntime?.EndPattern(finishedPattern);
    }

    public void AbortCurrentPattern()
    {
        if (!hasInitializedBossRuntime)
            return;

        BossPatternEntry activePattern = patternRuntime != null ? patternRuntime.CurrentPattern ?? patternRuntime.ReservedPattern : null;
        CancelActiveAbility(true);

        OnPatternEnd(activePattern, true);
        patternRuntime?.ClearPatternContext();
    }

    /// <summary>
    /// 책임 :
    /// - GroggyState가 보스 구체 타입을 몰라도 그로기 진입 연출을 요청할 수 있게 한다.
    /// - 패턴 cleanup과 상태 연출을 분리해 보스별 애니메이션 정책을 파생 컨트롤러에 맡긴다.
    /// </summary>
    public void NotifyGroggyStateEntered()
    {
        OnGroggyStateEntered();
    }

    /// <summary>
    /// 책임 :
    /// - GroggyState가 보스 구체 타입을 몰라도 그로기 종료 연출을 요청할 수 있게 한다.
    /// - 그로기 회복/복귀 애니메이션이 필요한 보스만 파생 구현에서 선택적으로 처리하게 한다.
    /// </summary>
    public void NotifyGroggyStateExited()
    {
        OnGroggyStateExited();
    }

    protected override void OnEnemyAttributeChanged(AttributeDefinition attribute, float oldValue, float newValue)
    {
        base.OnEnemyAttributeChanged(attribute, oldValue, newValue);

        AttributeDefinition deathHealthAttribute = ResolveHealthAttribute();
        if (attribute == deathHealthAttribute && newValue <= 0f && oldValue > 0f)
            Die();
    }

    protected override void OnDeathStarted()
    {
        if (stateMachine != null && deadState != null && stateMachine.CurrentState != deadState)
            ChangeState(deadState);

        combatActive = false;
        hasCombatOverride = true;
        if (hasInitializedBossRuntime)
            AbortCurrentPattern();

        CleanupStatusPresentationForDeath();

        RunProgressCoordinator.EnsureInstance()?.NotifyBossCombatEnded(this);
        BossHudController.Instance?.MarkBossDefeated(this);
        RunProgressCoordinator.EnsureInstance()?.NotifyBossDefeated(this);
        ResolveDeathPresentation();
        deathPresentation?.NotifyDeathStarted();
    }

    private void CleanupStatusPresentationForDeath()
    {
        TryEndGroggyStateImmediately();
        effectRunner?.ClearAllActiveEffects();
        GetComponent<ElementGaugeSystem>()?.ClearAll();
        GetComponent<MonsterElementGaugeViewInstaller>()?.Uninstall();
    }

    protected override void OnDestroy()
    {
        RunProgressCoordinator.Instance?.NotifyBossCombatEnded(this);
        SyncBossHudRegistration(forceUnbind: true);
        base.OnDestroy();
    }

    protected override void DestroyAfterDelay()
    {
        ResolveDeathPresentation();
        if (deathPresentation != null && deathPresentation.TryBeginDeathSequence())
            return;

        if (!BossEncounterEndDirector.SuppressesAutomaticRewardReady(this))
            RunProgressCoordinator.EnsureInstance()?.NotifyBossRewardsReady(this);

        base.DestroyAfterDelay();
    }

    protected override void PlayDeathAnimation()
    {
        ResolveDeathPresentation();
        if (deathPresentation != null && deathPresentation.ShouldDeferDeathAnimation)
            return;

        base.PlayDeathAnimation();
    }

    public bool HasDeadTag()
    {
        return HasStateTag(deadTag);
    }

    public bool HasGroggyTag()
    {
        return HasStateTag(groggyTag);
    }

    protected bool TryEndGroggyStateImmediately()
    {
        if (groggyTag == null || tagSystem == null || !tagSystem.HasTag(groggyTag))
            return false;

        bool ended = false;
        if (effectRunner != null)
        {
            ended = effectRunner.ReduceRemainingTimeByGrantedTag(
                gameObject,
                groggyTag,
                float.MaxValue) > 0;
        }

        int remainingExplicitCount = tagSystem.GetExplicitTagCount(groggyTag);
        if (remainingExplicitCount > 0)
        {
            tagSystem.RemoveTag(groggyTag, remainingExplicitCount);
            ended = true;
        }

        return ended;
    }

    protected virtual void OnEnterSpawn()
    {
        if (CanEnterEncounterIntroState() && encounterIntroState != null)
        {
            ChangeState(encounterIntroState);
            return;
        }

        ChangeState(combatIdleState);
    }

    protected virtual void OnPhaseChanged(int previousPhaseIndex, int nextPhaseIndex)
    {
        AbortCurrentPattern();
        ChangeState(combatIdleState);
    }

    /// <summary>
    /// 책임 :
    /// - 보스별 그로기 진입 연출 hook을 제공한다.
    /// - 기본 보스는 별도 애니메이션이 없어도 FSM 동작을 유지하도록 비워둔다.
    /// </summary>
    protected virtual void OnGroggyStateEntered()
    {
    }

    /// <summary>
    /// 책임 :
    /// - 보스별 그로기 종료 연출 hook을 제공한다.
    /// - 기본 보스는 별도 회복 애니메이션이 없어도 FSM 동작을 유지하도록 비워둔다.
    /// </summary>
    protected virtual void OnGroggyStateExited()
    {
    }

    protected virtual int EvaluatePhaseIndexByHealthRatio(float hpRatio)
    {
        if (phases == null || phases.Count == 0)
            return 0;

        int phaseIndex = 0;

        for (int i = 0; i < phases.Count; i++)
        {
            BossPhaseConfig phaseConfig = phases[i];
            if (phaseConfig == null)
                continue;

            if (hpRatio <= phaseConfig.EnterHpRatioBelowOrEqual)
                phaseIndex = i;
        }

        return phaseIndex;
    }

    private void EvaluatePhaseChange()
    {
        int nextPhaseIndex = EvaluatePhaseIndexByHealthRatio(blackboard.CurrentHpRatio);
        if (nextPhaseIndex == blackboard.CurrentPhaseIndex)
            return;

        int previousPhaseIndex = blackboard.CurrentPhaseIndex;
        blackboard.SetPhaseIndex(nextPhaseIndex);
        OnPhaseChanged(previousPhaseIndex, nextPhaseIndex);
    }

    private void EvaluateReactiveStateTransitions()
    {
        if (HasDeadTag())
        {
            if (StateMachine.CurrentState != deadState)
                ChangeState(deadState);

            return;
        }

        if (HasGroggyTag())
        {
            if (StateMachine.CurrentState != groggyState)
                ChangeState(groggyState);
        }
    }

    private float GetCurrentHpRatio()
    {
        float currentHealth = GetCurrentHealthValue();
        float maxHealth = GetCurrentMaxHealthValue();

        if (maxHealth <= 0f)
            return 0f;

        return currentHealth / maxHealth;
    }

    /// <summary>
    /// 책임 :
    /// - 보스 전투 활성 상태에 따라 HUD 등록/해제를 한 곳에서 일관되게 수행한다.
    /// - HUD가 보스를 탐색하지 않아도 현재 전투 중인 보스를 정확히 가리키도록 연결한다.
    /// </summary>
    private void SyncBossHudRegistration(bool forceUnbind = false)
    {
        if (BossHudController.Instance == null)
        {
            return;
        }

        if (forceUnbind || !combatActive)
        {
            BossHudController.Instance.UnbindBoss(this);
            return;
        }

        BossHudController.Instance.RegisterBoss(this, hudHealthBarTheme);
    }

    /// <summary>
    /// 책임 :
    /// - 보스 UI/HUD 같은 외부 표시 계층이 현재 체력 값을 안전하게 읽을 수 있게 제공한다.
    /// - 보스 내부 Attribute 참조 해석은 컨트롤러 안에 가두고, 외부는 숫자만 소비하게 만든다.
    /// </summary>
    private float GetCurrentHealthValue()
    {
        AttributeDefinition currentHealthAttribute = ResolveHealthAttribute();
        if (attributeSet == null || currentHealthAttribute == null)
            return 0f;

        return attributeSet.GetAttributeValue(currentHealthAttribute);
    }

    /// <summary>파생 보스가 현재 체력 Attribute를 공통 해석 규칙으로 수정하게 합니다.</summary>
    protected bool TryModifyCurrentHealthValue(float amount, Object source)
    {
        AttributeDefinition currentHealthAttribute = ResolveHealthAttribute();
        if (attributeSet == null || currentHealthAttribute == null)
            return false;

        return attributeSet.TryModifyAttributeValue(currentHealthAttribute, amount, source != null ? source : this);
    }

    protected bool TrySetCurrentHealthValue(float value, Object source)
    {
        AttributeDefinition currentHealthAttribute = ResolveHealthAttribute();
        if (attributeSet == null || currentHealthAttribute == null)
            return false;

        return attributeSet.TrySetCurrentValue(currentHealthAttribute, value, source != null ? source : this);
    }

    protected bool IsCurrentHealthAttribute(AttributeDefinition attribute)
    {
        AttributeDefinition currentHealthAttribute = ResolveHealthAttribute();
        return attribute != null && currentHealthAttribute != null && attribute == currentHealthAttribute;
    }

    /// <summary>
    /// 책임 :
    /// - 보스 UI/HUD 같은 외부 표시 계층이 최대 체력 값을 안전하게 읽을 수 있게 제공한다.
    /// - Health/MaxHealth Attribute 정의 fallback 규칙을 보스 컨트롤러 내부에 유지한다.
    /// </summary>
    private float GetCurrentMaxHealthValue()
    {
        AttributeDefinition currentMaxHealthAttribute = ResolveMaxHealthAttribute();
        if (attributeSet == null || currentMaxHealthAttribute == null)
            return 0f;

        return attributeSet.GetAttributeValue(currentMaxHealthAttribute);
    }

    private AttributeDefinition ResolveHealthAttribute()
    {
        return healthAttribute != null ? healthAttribute : healthDef;
    }

    private AttributeDefinition ResolveMaxHealthAttribute()
    {
        return maxHealthAttribute != null ? maxHealthAttribute : maxHealthDef;
    }

    public void NotifySpawnFinished()
    {
        OnEnterSpawn();
    }

    /// <summary>인트로 State를 쓸지 확인합니다.</summary>
    public bool CanEnterEncounterIntroState()
    {
        return !encounterIntroFinished && CanUseEncounterIntro();
    }

    /// <summary>인트로 시퀀스를 시작합니다.</summary>
    public virtual bool TryStartEncounterIntro()
    {
        if (encounterDirector == null) encounterDirector = FindAnyObjectByType<BossEncounterDirector>();
        if (encounterDirector != null)
        {
            if (!encounterDirector.IsSequenceRunning)
                encounterDirector.BeginSequence();

            return encounterDirector.IsSequenceRunning;
        }

        if (bossTalkManager == null) bossTalkManager = FindAnyObjectByType<BossTalkManager>();
        if (bossTalkManager != null)
        {
            if (!bossTalkManager.IsSequenceRunning)
                bossTalkManager.BeginEncounterSequence();

            return bossTalkManager.IsSequenceRunning;
        }

        return TryStartDialogue();
    }

    /// <summary>인트로 시퀀스 진행 여부를 확인합니다.</summary>
    public virtual bool IsEncounterIntroActive()
    {
        if (encounterDirector == null) encounterDirector = FindAnyObjectByType<BossEncounterDirector>();
        if (encounterDirector != null) return encounterDirector.IsSequenceRunning;

        if (bossTalkManager == null) bossTalkManager = FindAnyObjectByType<BossTalkManager>();
        if (bossTalkManager != null) return bossTalkManager.IsSequenceRunning;

        return IsDialogueActive();
    }

    /// <summary>인트로 종료를 기록합니다.</summary>
    public void FinishEncounterIntro()
    {
        encounterIntroFinished = true;
    }

    /// <summary>보스 대화를 시작합니다.</summary>
    public virtual bool TryStartDialogue()
    {
        return false;
    }

    /// <summary>보스 대화가 진행 중인지 확인합니다.</summary>
    public virtual bool IsDialogueActive()
    {
        return DialogueService.Instance != null && DialogueService.Instance.IsPlaying;
    }

    /// <summary>보스 대화 종료를 기록합니다.</summary>
    public void FinishDialogue()
    {
        FinishEncounterIntro();
    }

    protected virtual void CreateStates()
    {
        spawnState = CreateSpawnState();
        encounterIntroState = CreateEncounterIntroState();
        combatIdleState = CreateCombatIdleState();
        patternSelectState = CreatePatternSelectState();
        patternExecuteState = CreatePatternExecuteState();
        groggyState = CreateGroggyState();
        deadState = CreateDeadState();
    }

    protected virtual BossBlackboard CreateBlackboard()
    {
        return new BossBlackboard(transform);
    }

    protected virtual BossPatternRuntimeState CreatePatternRuntimeState()
    {
        return new BossPatternRuntimeState();
    }

    /// <summary>
    /// 책임 :
    /// - 런타임 기본 패턴 구성을 사용할 때 보스의 phase 리스트를 안전하게 교체한다.
    /// - 공통 FSM은 phase 데이터를 소비만 하고, 실제 phase 구성 책임은 보스 구현체에 둔다.
    /// </summary>
    protected void SetRuntimePhases(IEnumerable<BossPhaseConfig> runtimePhases)
    {
        phases = runtimePhases != null
            ? new List<BossPhaseConfig>(runtimePhases)
            : new List<BossPhaseConfig>();
    }

    /// <summary>
    /// 책임 :
    /// - 현재 phase 설정이 참조하는 모든 패턴 ability를 AbilitySystem에 미리 등록한다.
    /// - 패턴 선택은 되었지만 spec이 없어 실행에 실패하는 일을 공통 계층에서 방지한다.
    /// </summary>
    private void RegisterConfiguredPatternAbilities()
    {
        if (abilitySystem == null || phases == null || phases.Count == 0)
            return;

        for (int phaseIndex = 0; phaseIndex < phases.Count; phaseIndex++)
        {
            BossPhaseConfig phase = phases[phaseIndex];
            IReadOnlyList<BossPatternEntry> patterns = phase != null ? phase.Patterns : null;
            if (patterns == null)
                continue;

            for (int patternIndex = 0; patternIndex < patterns.Count; patternIndex++)
            {
                BossPatternEntry pattern = patterns[patternIndex];
                AbilityDefinition ability = pattern != null ? pattern.Ability : null;
                if (ability == null)
                    continue;

                TryRegisterAbility(ability);

                AbilityDefinition followUpAbility = pattern.FollowUpAbility;
                if (followUpAbility != null)
                    TryRegisterAbility(followUpAbility);
            }
        }
    }

    protected virtual BossSpawnState CreateSpawnState()
    {
        return new BossSpawnState(this);
    }

    protected virtual BossEncounterIntroState CreateEncounterIntroState()
    {
        return new BossEncounterIntroState(this);
    }

    protected virtual BossCombatIdleState CreateCombatIdleState()
    {
        return new BossCombatIdleState(this);
    }

    protected virtual BossPatternSelectState CreatePatternSelectState()
    {
        return new BossPatternSelectState(this);
    }

    protected virtual BossPatternExecuteState CreatePatternExecuteState()
    {
        return new BossPatternExecuteState(this);
    }

    protected virtual BossGroggyState CreateGroggyState()
    {
        return new BossGroggyState(this);
    }

    protected virtual BossDeadState CreateDeadState()
    {
        return new BossDeadState(this);
    }

    /// <summary>보스별 평가 결과를 보정합니다.</summary>
    protected virtual BossPatternEvalResult AdjustPatternEval(BossPatternEntry patternEntry, BossPatternEvalResult result)
    {
        return result;
    }

    /// <summary>
    /// 책임:
    /// 후속 연계 패턴 실행 시 일반 선택 조건 대신 실제 실행 가능성 중심으로 평가한다.
    /// </summary>
    protected virtual BossPatternEvalResult EvaluateForcedFollowUpPattern(BossPatternEntry patternEntry)
    {
        if (patternEntry == null)
            return BossPatternEvalResult.HardFail("후속 패턴이 없습니다.");

        BossPatternEvalContext context = new BossPatternEvalContext(this, blackboard, patternRuntime);
        return patternEntry.EvaluateForcedFollowUp(context);
    }

    /// <summary>
    /// 책임:
    /// 패턴 정상 종료 후 authoring된 후속 Ability를 런타임 큐에 올려 다음 선택 사이클을 강제 연계로 전환한다.
    /// </summary>
    private void QueueFollowUpPattern(BossPatternEntry finishedPattern)
    {
        if (patternRuntime == null || finishedPattern == null || finishedPattern.FollowUpAbility == null)
            return;

        patternRuntime.QueueFollowUpAbility(finishedPattern.FollowUpAbility);
    }

    /// <summary>
    /// 책임:
    /// 큐에 쌓인 후속 Ability를 현재 phase의 패턴 엔트리로 해석하고, 일반 가중치 선택보다 우선 반환한다.
    /// </summary>
    private BossPatternEntry TrySelectQueuedFollowUpPattern()
    {
        if (patternRuntime == null || !patternRuntime.TryConsumeQueuedFollowUpAbility(out AbilityDefinition followUpAbility))
            return null;

        BossPatternEntry followUpPattern = FindPatternEntryByAbility(followUpAbility);
        if (followUpPattern == null)
        {
            Debug.LogWarning(
                $"[BossFSM] {name}: 후속 패턴 Ability '{followUpAbility.name}'를 현재 phase 설정에서 찾지 못했습니다.",
                this);
            return null;
        }

        BossPatternEvalResult result = EvaluateForcedFollowUpPattern(followUpPattern);
        if (!result.CanUse)
        {
            Debug.Log(
                $"[BossFSM] {name}: 후속 패턴 '{followUpAbility.name}' 실행 보류. state={result.State}, reason={result.Reason ?? "없음"}",
                this);
            return null;
        }

        patternRuntime.MarkSelectedPatternAsForcedFollowUp(followUpPattern);
        return followUpPattern;
    }

    /// <summary>
    /// 책임:
    /// Ability 참조로 authoring된 후속 패턴을 실제 phase 패턴 엔트리로 되찾는다.
    /// </summary>
    private BossPatternEntry FindPatternEntryByAbility(AbilityDefinition ability)
    {
        if (ability == null)
            return null;

        BossPatternEntry currentPhasePattern = FindPatternEntryByAbility(GetCurrentPhase(), ability);
        if (currentPhasePattern != null)
            return currentPhasePattern;

        if (phases == null)
            return null;

        for (int i = 0; i < phases.Count; i++)
        {
            BossPatternEntry pattern = FindPatternEntryByAbility(phases[i], ability);
            if (pattern != null)
                return pattern;
        }

        return null;
    }

    private static BossPatternEntry FindPatternEntryByAbility(BossPhaseConfig phase, AbilityDefinition ability)
    {
        IReadOnlyList<BossPatternEntry> patterns = phase != null ? phase.Patterns : null;
        if (patterns == null)
            return null;

        for (int i = 0; i < patterns.Count; i++)
        {
            BossPatternEntry pattern = patterns[i];
            if (pattern != null && pattern.Ability == ability)
                return pattern;
        }

        return null;
    }

    /// <summary>패턴 종료 후 정리 작업을 처리합니다.</summary>
    protected virtual void OnPatternEnd(BossPatternEntry patternEntry, bool forced)
    {
    }

    /// <summary>인트로 State 사용 여부를 정합니다.</summary>
    protected virtual bool CanUseEncounterIntro()
    {
        if (encounterDirector == null) encounterDirector = FindAnyObjectByType<BossEncounterDirector>();
        if (encounterDirector != null) return true;

        if (bossTalkManager == null) bossTalkManager = FindAnyObjectByType<BossTalkManager>();
        if (bossTalkManager != null) return true;

        return CanUseDialogue();
    }

    /// <summary>대화 사용 여부를 정합니다.</summary>
    protected virtual bool CanUseDialogue()
    {
        return false;
    }

    internal void PlayDeferredDeathAnimationFromPresentation()
    {
        base.PlayDeathAnimation();
    }

    private void ResolveDeathPresentation()
    {
        if (deathPresentation == null)
            deathPresentation = GetComponent<BossDeathPresentation>();

        deathPresentation?.Bind(this);
    }

    private void ResolveSpeechController()
    {
        if (speechController == null)
            speechController = GetComponent<BossSpeechController>();
    }
}
