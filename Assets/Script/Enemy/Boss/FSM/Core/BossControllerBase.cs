using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

public abstract class BossControllerBase : Enemy
{
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
    private BossStateMachine stateMachine;

    private BossSpawnState spawnState;
    private BossCombatIdleState combatIdleState;
    private BossPatternSelectState patternSelectState;
    private BossPatternExecuteState patternExecuteState;
    private BossGroggyState groggyState;
    private BossDeadState deadState;
    
    private bool combatActive;
    private bool hasCombatOverride;

    public AbilitySystem AbilitySystem => abilitySystem;
    public TagSystem TagSystem => tagSystem;
    public AttributeSet AttributeSet => attributeSet;
    public BossBlackboard Blackboard => blackboard;
    public BossStateMachine StateMachine => stateMachine;
    public Transform CurrentTarget => Target;
    public override Transform Target => target;

    protected override void Awake()
    {
        base.Awake();

        CacheComponents();
        bossDrop = GetComponent<BossDrop>();

        blackboard = new BossBlackboard(transform);
        stateMachine = new BossStateMachine(blackboard);
        CreateStates();
    }

    protected override void Start()
    {
        base.Start();

        if (initialTarget != null)
            SetTarget(initialTarget);

        blackboard.SetPhaseIndex(EvaluatePhaseIndexByHealthRatio(GetCurrentHpRatio()));
        stateMachine.ChangeState(spawnState);
        combatActive = hasCombatOverride ? combatActive : startCombatOnStart;
    }

    protected virtual void Update()
    {
        if (!combatActive)
            return;

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
    }

    public void BeginCombatEncounter(Transform combatTarget = null)
    {
        if (combatTarget != null)
            SetTarget(combatTarget);
        else if (Target == null)
            RefreshTarget();

        SetCombatActive(true);
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

        GameObject targetObject = Target != null ? Target.gameObject : null;
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

    protected override void OnEnemyAttributeChanged(AttributeDefinition attribute, float oldValue, float newValue)
    {
        base.OnEnemyAttributeChanged(attribute, oldValue, newValue);

        AttributeDefinition deathHealthAttribute = ResolveHealthAttribute();
        if (attribute == deathHealthAttribute && newValue <= 0f && oldValue > 0f)
            Die();
    }

    protected override void Die()
    {
        if (isDead)
            return;

        isDead = true;

        if (stateMachine != null && deadState != null && stateMachine.CurrentState != deadState)
            ChangeState(deadState);

        if (bossDrop != null)
            bossDrop.OnBossDead();

        base.Die();
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
        AttributeDefinition currentHealthAttribute = ResolveHealthAttribute();
        AttributeDefinition currentMaxHealthAttribute = ResolveMaxHealthAttribute();

        if (attributeSet == null || currentHealthAttribute == null || currentMaxHealthAttribute == null)
            return 1f;

        float currentHealth = attributeSet.GetAttributeValue(currentHealthAttribute);
        float maxHealth = attributeSet.GetAttributeValue(currentMaxHealthAttribute);

        if (maxHealth <= 0f)
            return 0f;

        return currentHealth / maxHealth;
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

    protected virtual void CreateStates()
    {
        spawnState = CreateSpawnState();
        combatIdleState = CreateCombatIdleState();
        patternSelectState = CreatePatternSelectState();
        patternExecuteState = CreatePatternExecuteState();
        groggyState = CreateGroggyState();
        deadState = CreateDeadState();
    }

    protected virtual BossSpawnState CreateSpawnState()
    {
        return new BossSpawnState(this);
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
}
