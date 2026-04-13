using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

public abstract class BossControllerBase : Enemy
{
    // 이 클래스의 책임:
    // Enemy의 공통 전투/사망 처리 위에 보스 전용 전투 상태, 페이즈, 반응 전환을 조율한다.

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
    private BossDrop bossDrop;
    private BossEncounterDirector encounterDirector;
    private BossTalkManager bossTalkManager;
    private BossDeathPresentation deathPresentation;
    private BossSpeechController speechController;

    private bool combatActive;
    private bool hasCombatOverride;
    private bool encounterIntroFinished;

    public AbilitySystem AbilitySystem => abilitySystem;
    public TagSystem TagSystem => tagSystem;
    public AttributeSet AttributeSet => attributeSet;
    public BossBlackboard Blackboard => blackboard;
    public BossPatternRuntimeState PatternRuntime => patternRuntime;
    public BossStateMachine StateMachine => stateMachine;
    public Transform CurrentTarget => Target;
    public override Transform Target => target;
    protected int ConfiguredPhaseCount => phases != null ? phases.Count : 0;
    public float CurrentHealthRatio => GetCurrentHpRatio();
    public float CurrentHealthValue => GetCurrentHealthValue();
    public float MaxHealthValue => GetCurrentMaxHealthValue();

    protected override void Awake()
    {
        base.Awake();

        CacheComponents();
        bossDrop = GetComponent<BossDrop>();
        speechController = GetComponent<BossSpeechController>();
        ResolveDeathPresentation();

        blackboard = CreateBlackboard();
        patternRuntime = CreatePatternRuntimeState();
        stateMachine = new BossStateMachine(blackboard);
        CreateStates();
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

    public void SetCombatActive(bool isActive)
    {
        combatActive = isActive;
        hasCombatOverride = true;

        if (!isActive)
            AbortCurrentPattern();

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
        return BossPatternSelector.Select(this, blackboard, GetCurrentPhase());
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

        if (abilitySystem.IsBusy) return false;

        BossPatternEvalResult result = EvaluatePattern(patternEntry);
        if (!result.CanUse) return false;

        GameObject targetObject = Target != null ? Target.gameObject : null;
        bool isActivated = abilitySystem.TryActivateAbility(patternEntry.Ability, targetObject);

        if (!isActivated)
            return false;

        patternRuntime.BeginPattern(patternEntry);
        return true;
    }

    public void FinishCurrentPattern()
    {
        BossPatternEntry finishedPattern = patternRuntime != null ? patternRuntime.CurrentPattern : null;
        OnPatternEnd(finishedPattern, false);
        patternRuntime?.EndPattern();
    }

    public void AbortCurrentPattern()
    {
        BossPatternEntry activePattern = patternRuntime != null ? patternRuntime.CurrentPattern ?? patternRuntime.ReservedPattern : null;

        if (abilitySystem != null && abilitySystem.IsCasting)
            abilitySystem.CancelCasting(true);

        if (abilitySystem != null && abilitySystem.IsExecuting)
            abilitySystem.CancelExecution(true);

        OnPatternEnd(activePattern, true);
        patternRuntime?.ClearPatternContext();
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

        SetCombatActive(false);
        ResolveDeathPresentation();
        deathPresentation?.NotifyDeathStarted();
    }

    protected override void OnDestroy()
    {
        SyncBossHudRegistration(forceUnbind: true);
        base.OnDestroy();
    }

    protected override void DestroyAfterDelay()
    {
        ResolveDeathPresentation();
        if (deathPresentation != null && deathPresentation.TryBeginDeathSequence())
            return;

        SpawnDeathRewards();
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
        return deadTag != null && tagSystem != null && tagSystem.HasTag(deadTag);
    }

    public bool HasGroggyTag()
    {
        return groggyTag != null && tagSystem != null && tagSystem.HasTag(groggyTag);
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
            return;

        if (forceUnbind || !combatActive)
        {
            BossHudController.Instance.UnbindBoss(this);
            return;
        }

        BossHudController.Instance.BindBoss(this);
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

                abilitySystem.GiveAbility(ability);
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

    private void SpawnDeathRewards()
    {
        if (bossDrop != null)
            bossDrop.OnBossDead();
    }

    internal void PlayDeferredDeathAnimationFromPresentation()
    {
        base.PlayDeathAnimation();
    }

    private void ResolveDeathPresentation()
    {
        if (deathPresentation == null)
            deathPresentation = GetComponent<BossDeathPresentation>();

        deathPresentation?.Bind(this, bossDrop);
    }

    private void ResolveSpeechController()
    {
        if (speechController == null)
            speechController = GetComponent<BossSpeechController>();
    }
}
