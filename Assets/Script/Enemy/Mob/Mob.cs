using UnityEngine;
using UnityGAS;

public class Mob : Enemy
{
    [Header("참조")]
    [Tooltip("플레이어 추적 범위를 가진 컴포넌트입니다.")]
    [SerializeField] private EnemyChaseIntent2D chaseIntent;

    [Header("Debug")]
    [Tooltip("켜두면 일반 몬스터 FSM 초기화와 상태 전이 판단 로그를 출력합니다.")]
    [SerializeField] private bool logMobFsmDebug;

    private bool hasMoveBool;
    private MobStateMachine stateMachine;
    private MobAIContext aiContext;
    private bool triedInitializeStateMachine;

    protected EnemyChaseIntent2D ChaseIntent => chaseIntent;
    public bool LogMobFsmDebug => logMobFsmDebug;

    protected override void Awake()
    {
        base.Awake();

        if (chaseIntent == null)
            chaseIntent = GetComponent<EnemyChaseIntent2D>();

        hasMoveBool = CheckMoveBool();
    }

    private void Update()
    {
        if (isDead) return;

        EnsureTargetResolved();

        if (TryInitializeStateMachine())
            stateMachine?.Tick(aiContext);

        UpdateAnimation();
    }

    /// <summary>
    /// 책임:
    /// 플레이어가 몬스터보다 늦게 생성되는 씬/스폰 순서에서도 일반 몬스터가 추적 타깃을 회복하게 한다.
    /// </summary>
    private void EnsureTargetResolved()
    {
        if (Target != null)
            return;

        TryRefreshTarget(logWarning: false);
    }

    /// <summary>이 몬스터가 추적 이동을 사용할지 정합니다.</summary>
    public virtual bool CanUseChaseMovement()
    {
        return true;
    }

    /// <summary>이동과 방향 애니메이션을 갱신합니다.</summary>
    protected virtual void UpdateAnimation()
    {
        if (animator != null && movementMotor != null && hasMoveBool)
            animator.SetBool("isMoving", movementMotor.IsMoving);

        UpdateFacing();
    }

    /// <summary>타겟 기준으로 스프라이트 방향을 갱신합니다.</summary>
    protected virtual void UpdateFacing()
    {
        if (Target == null || sprite == null) return;

        if      (transform.position.x > Target.position.x) sprite.flipX = true;
        else if (transform.position.x < Target.position.x) sprite.flipX = false;
    }

    /// <summary>이동 Bool 파라미터가 있는지 확인합니다.</summary>
    private bool CheckMoveBool()
    {
        if (animator == null)
            return false;

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.type == AnimatorControllerParameterType.Bool &&
                parameter.name == "isMoving")
            {
                return true;
            }
        }

        return false;
    }

    protected override void OnEnemyAttributeChanged(AttributeDefinition attribute, float oldValue, float newValue)
    {
        base.OnEnemyAttributeChanged(attribute, oldValue, newValue);

        if (attribute == healthDef && newValue <= 0f && !isDead)
            Die();
    }

    /// <summary>넉백 요청을 KnockbackReceiver2D에 넘깁니다.</summary>
    public void ApplyKnockbackFrom(GameObject causer, float impulse)
    {
        if (isDead || knockbackReceiver == null) return;

        knockbackReceiver.ApplyKnockback(causer, impulse);
    }

    protected override void OnDeathStarted()
    {
        EnterDeathState();
        LootManager.Instance?.SpawnMonsterLoot(transform.position);
    }

    private void OnDisable()
    {
        aiContext?.PerformFailSafeCleanup();
        ShutdownStateMachine();
    }

    /// <summary>FSM 기반 공격 판단을 지원하는 몬스터면 공통 상태 기계를 초기화합니다.</summary>
    private bool TryInitializeStateMachine()
    {
        if (stateMachine != null && aiContext != null)
            return true;

        if (triedInitializeStateMachine)
            return false;

        triedInitializeStateMachine = true;

        if (!TryResolveMobAbilityBridge(out IMobAbilityBridge abilityBridge))
        {
            LogFsmDebug("FSM 초기화 실패: IMobAbilityBridge를 찾지 못했습니다.");
            return false;
        }

        if (!TryResolveAttackDecisionSource(out IMobAttackDecisionSource attackDecisionSource))
        {
            LogFsmDebug("FSM 초기화 실패: IMobAttackDecisionSource를 찾지 못했습니다.");
            return false;
        }

        aiContext = new MobAIContext(
            this,
            chaseIntent,
            abilityBridge,
            attackDecisionSource,
            ResolvePatternRunnerTargets(),
            ResolvePresentationCleanupTargets());
        stateMachine = new MobStateMachine();
        stateMachine.SetInitialState(new MobIdleState(), aiContext);
        LogFsmDebug($"FSM 초기화 완료. chaseIntent={(chaseIntent != null ? chaseIntent.name : "null")}, bridge={abilityBridge.GetType().Name}, decisionSource={attackDecisionSource.GetType().Name}");
        return true;
    }

    /// <summary>현재 오브젝트에 붙은 pattern runner cleanup 대상을 수집합니다.</summary>
    private IMobPatternRunner[] ResolvePatternRunnerTargets()
    {
        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
        System.Collections.Generic.List<IMobPatternRunner> targets = null;

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is not IMobPatternRunner runner)
                continue;

            targets ??= new System.Collections.Generic.List<IMobPatternRunner>();
            targets.Add(runner);
        }

        return targets != null
            ? targets.ToArray()
            : System.Array.Empty<IMobPatternRunner>();
    }

    /// <summary>현재 오브젝트에 붙은 presentation cleanup provider를 수집합니다.</summary>
    private IMobPresentationCleanup[] ResolvePresentationCleanupTargets()
    {
        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
        System.Collections.Generic.List<IMobPresentationCleanup> targets = null;

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is not IMobPresentationCleanup cleanupTarget)
                continue;

            targets ??= new System.Collections.Generic.List<IMobPresentationCleanup>();
            targets.Add(cleanupTarget);
        }

        return targets != null
            ? targets.ToArray()
            : System.Array.Empty<IMobPresentationCleanup>();
    }

    /// <summary>현재 오브젝트에 붙은 일반 몬스터 bridge를 해석합니다.</summary>
    private bool TryResolveMobAbilityBridge(out IMobAbilityBridge abilityBridge)
    {
        abilityBridge = null;

        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IMobAbilityBridge resolvedBridge)
            {
                abilityBridge = resolvedBridge;
                return true;
            }
        }

        return false;
    }

    /// <summary>현재 오브젝트에 붙은 몬스터별 공격 결정 source를 해석합니다.</summary>
    private bool TryResolveAttackDecisionSource(out IMobAttackDecisionSource attackDecisionSource)
    {
        attackDecisionSource = null;

        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IMobAttackDecisionSource resolvedSource)
            {
                attackDecisionSource = resolvedSource;
                return true;
            }
        }

        return false;
    }

    /// <summary>공통 상태 기계를 안전하게 종료합니다.</summary>
    private void ShutdownStateMachine()
    {
        if (stateMachine == null || aiContext == null)
            return;

        LogFsmDebug("FSM 종료 cleanup 수행.");
        stateMachine.Shutdown(aiContext);
        stateMachine = null;
        aiContext = null;
        triedInitializeStateMachine = false;
    }

    /// <summary>FSM 디버그 스위치가 켜진 몬스터만 추적/전이 진단 로그를 남깁니다.</summary>
    public void LogFsmDebug(string message)
    {
        if (!logMobFsmDebug)
            return;

        Debug.Log($"[MobFSM] {name}: {message}", this);
    }

    /// <summary>사망 시 공통 FSM을 명시적인 터미널 상태로 전이시킵니다.</summary>
    private void EnterDeathState()
    {
        if (stateMachine == null || aiContext == null)
        {
            aiContext?.PerformFailSafeCleanup();
            return;
        }

        stateMachine.ChangeState(new MobDeathState(), aiContext);
    }

    private void OnDrawGizmos()
    {
        DrawChaseGizmos();
        DrawAttackGizmos();
    }

    /// <summary>추적 범위를 기즈모로 그립니다.</summary>
    private void DrawChaseGizmos()
    {
        EnemyChaseIntent2D gizmoChaseIntent = chaseIntent != null
            ? chaseIntent
            : GetComponent<EnemyChaseIntent2D>();

        if (gizmoChaseIntent == null) return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, gizmoChaseIntent.DetectionRange);

        if (!CanDrawStopRangeGizmo()) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, gizmoChaseIntent.StopRange);
    }

    /// <summary>정지 범위 기즈모를 그릴지 정합니다.</summary>
    protected virtual bool CanDrawStopRangeGizmo()
    {
        return true;
    }

    /// <summary>추가 공격 기즈모를 그립니다.</summary>
    protected virtual void DrawAttackGizmos()
    {
    }
}
