using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

public abstract class BossControllerBase : Enemy
{
    /*
    [Header("Core Components")]
    [Tooltip("보스가 사용하는 GAS AbilitySystem입니다.")]
    [SerializeField] private AbilitySystem abilitySystem;

    [Tooltip("보스 상태 태그를 관리하는 TagSystem입니다.")]
    [SerializeField] private TagSystem tagSystem;

    [Tooltip("보스 스탯을 관리하는 AttributeSet입니다.")]
    [SerializeField] private AttributeSet attributeSet;
    */

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
    private BossStateMachine stateMachine;

    private BossSpawnState spawnState;
    private BossCombatIdleState combatIdleState;
    private BossPatternSelectState patternSelectState;
    private BossPatternExecuteState patternExecuteState;
    private BossGroggyState groggyState;
    private BossDeadState deadState;

    private Transform currentTarget;

    public AbilitySystem AbilitySystem => abilitySystem;
    public TagSystem TagSystem => tagSystem;
    public AttributeSet AttributeSet => attributeSet;
    public BossBlackboard Blackboard => blackboard;
    public BossStateMachine StateMachine => stateMachine;
    public Transform CurrentTarget => currentTarget;

    protected override void Awake()
    {
        base.Awake();

        CacheComponents();

        currentTarget = initialTarget;

        blackboard = new BossBlackboard(transform);
        stateMachine = new BossStateMachine(blackboard);

        spawnState = new BossSpawnState(this);
        combatIdleState = new BossCombatIdleState(this);
        patternSelectState = new BossPatternSelectState(this);
        patternExecuteState = new BossPatternExecuteState(this);
        groggyState = new BossGroggyState(this);
        deadState = new BossDeadState(this);
    }

    protected override void Start()
    {
        base.Start();

        blackboard.SetPhaseIndex(EvaluatePhaseIndexByHealthRatio(GetCurrentHpRatio()));
        stateMachine.ChangeState(spawnState);
    }

    protected virtual void Update()
    {
        blackboard.Tick(Time.deltaTime, currentTarget, GetCurrentHpRatio());

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
        currentTarget = newTarget;
    }

    public void ChangeState(BossState nextState)
    {
        stateMachine.ChangeState(nextState);
    }

    public BossState GetCombatIdleState()
    {
        return combatIdleState;
    }

    public BossState GetPatternSelectState()
    {
        return patternSelectState;
    }

    public BossState GetPatternExecuteState()
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

    public bool TryStartPattern(BossPatternEntry patternEntry)
    {
        if (patternEntry == null || abilitySystem == null)
            return false;

        if (abilitySystem.IsBusy)
            return false;

        if (!patternEntry.IsSelectable(this, blackboard))
            return false;

        GameObject targetObject = currentTarget != null ? currentTarget.gameObject : null;
        bool isActivated = abilitySystem.TryActivateAbility(patternEntry.Ability, targetObject);

        if (!isActivated)
            return false;

        blackboard.BeginPattern(patternEntry);
        return true;
    }

    public void FinishCurrentPattern()
    {
        blackboard.EndPattern();
    }

    public void AbortCurrentPattern()
    {
        if (abilitySystem != null && abilitySystem.IsCasting)
            abilitySystem.CancelCasting(true);

        if (abilitySystem != null && abilitySystem.IsExecuting)
            abilitySystem.CancelExecution(true);

        blackboard.ClearPatternContext();
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
        if (attributeSet == null || healthAttribute == null || maxHealthAttribute == null)
            return 1f;

        float currentHealth = attributeSet.GetAttributeValue(healthAttribute);
        float maxHealth = attributeSet.GetAttributeValue(maxHealthAttribute);

        if (maxHealth <= 0f)
            return 0f;

        return currentHealth / maxHealth;
    }

    public void NotifySpawnFinished()
    {
        OnEnterSpawn();
    }
}