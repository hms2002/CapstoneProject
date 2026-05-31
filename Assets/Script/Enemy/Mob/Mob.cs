using UnityEngine;
using UnityGAS;

/// <summary>
/// 이 클래스의 책임:
/// 일반 몬스터의 공통 FSM 실행, 추적 타깃 회복, 이동 애니메이션과 기본 방향 전환을 관리한다.
/// </summary>
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
    private ChestMonsterKillLock lockTrackingChestLock;
    private MonsterSpawnRoomGroup lockTrackingRoomGroup;
    private IEnemyChaseIntent resolvedChaseIntent;
    private PitFallReaction2D pitFallReaction;
    private bool triedInitializeStateMachine;
    private bool suppressMonsterLootDrop;
    private int facingLockCount;
    private float spawnIdlePauseUntilTime;

    protected EnemyChaseIntent2D ChaseIntent => chaseIntent;
    protected MonsterSpawnRoomGroup LockTrackingRoomGroup => lockTrackingRoomGroup;
    public bool LogMobFsmDebug => logMobFsmDebug;

    protected override void Awake()
    {
        base.Awake();

        if (chaseIntent == null)
            chaseIntent = GetComponent<EnemyChaseIntent2D>();

        pitFallReaction = GetComponentInChildren<PitFallReaction2D>(includeInactive: true);
        resolvedChaseIntent = ResolveChaseIntent();

        hasMoveBool = CheckMoveBool();
    }

    private void Update()
    {
        if (isDead) return;

        EnsureTargetResolved();

        if (IsSpawnIdlePaused())
        {
            PerformSpawnIdlePauseCleanup();
            UpdateAnimation();
            return;
        }

        if (TryInitializeStateMachine())
        {
            if (IsPitFallSuppressed())
            {
                aiContext?.PerformSuppressionCleanup();
                return;
            }

            stateMachine?.Tick(aiContext);
        }

        UpdateAnimation();
    }

    /// <summary>
    /// 책임:
    /// - 구덩이 낙하 연출 중 일반 몬스터 FSM/공격/추적 갱신을 멈춰 전투 로직이 연출 상태와 따로 놀지 않게 한다.
    /// - PitFallReaction2D가 붙은 몬스터만 이 억제 규칙을 적용해 authoring 선택성을 유지한다.
    /// </summary>
    private bool IsPitFallSuppressed()
    {
        if (pitFallReaction == null)
            pitFallReaction = GetComponentInChildren<PitFallReaction2D>(includeInactive: true);

        return pitFallReaction != null && pitFallReaction.IsPitFallActive;
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
        return !IsSpawnIdlePaused();
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
        if (facingLockCount > 0)
            return;

        if (Target == null) return;

        TryApplySpriteFacingTargetX(Target.position.x);
    }

    /// <summary>
    /// 책임:
    /// - 방 입장 스폰 직후 일반 몬스터가 바로 추적/공격하지 않도록 짧은 대기 시간을 적용한다.
    /// - 스폰 연출과 실제 전투 시작 사이에 숨 쉴 틈을 만들어 VFX 스폰 체감을 안정화한다.
    /// </summary>
    public void ApplySpawnIdlePause(float seconds)
    {
        if (seconds <= 0f || isDead)
            return;

        spawnIdlePauseUntilTime = Mathf.Max(spawnIdlePauseUntilTime, Time.time + seconds);
        PerformSpawnIdlePauseCleanup();
    }

    private bool IsSpawnIdlePaused()
    {
        return spawnIdlePauseUntilTime > Time.time;
    }

    /// <summary>
    /// 책임:
    /// - 스폰 직후 대기 시간 동안 공격 실행뿐 아니라 추적 이동 의도까지 함께 비운다.
    /// - FSM context가 아직 초기화되기 전인 몬스터도 chase intent 캐시를 직접 정리해 제자리 대기하게 한다.
    /// </summary>
    private void PerformSpawnIdlePauseCleanup()
    {
        if (aiContext != null)
        {
            aiContext.PerformSuppressionCleanup();
            return;
        }

        ResolveChaseIntent()?.StopChase();
    }

    /// <summary>
    /// 책임:
    /// - 공격 방향이 확정된 일반 몬스터 패턴 동안 자동 flipX 갱신을 잠가 준비/공격 애니메이션 방향을 보존한다.
    /// - 중첩 패턴/정리 경로가 안전하게 공존하도록 카운트 기반으로 관리한다.
    /// </summary>
    public void PushFacingLock()
    {
        facingLockCount++;
    }

    /// <summary>
    /// 책임:
    /// - PushFacingLock으로 잠근 자동 flipX 갱신을 한 단계 해제한다.
    /// - 취소/사망/disable 경로에서 여러 번 호출되어도 음수로 내려가지 않게 보호한다.
    /// </summary>
    public void PopFacingLock()
    {
        facingLockCount = Mathf.Max(0, facingLockCount - 1);
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

    public void ApplyLockTrackingContext(ChestMonsterKillLock chestLock, MonsterSpawnRoomGroup roomGroup)
    {
        lockTrackingChestLock = chestLock;
        lockTrackingRoomGroup = roomGroup;
    }

    public void SuppressMonsterLootDrop()
    {
        suppressMonsterLootDrop = true;
    }

    protected void RegisterLockTrackedChild(GameObject child)
    {
        if (child == null)
            return;

        if (lockTrackingChestLock != null)
            lockTrackingChestLock.RegisterMonster(child);

        if (lockTrackingRoomGroup != null)
            lockTrackingRoomGroup.NotifyMonsterSpawned(child);

        if (child.TryGetComponent(out Mob childMob))
            childMob.ApplyLockTrackingContext(lockTrackingChestLock, lockTrackingRoomGroup);
    }

    protected override void OnDeathStarted()
    {
        EnterDeathState();

        if (!suppressMonsterLootDrop)
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
            ResolveChaseIntent(),
            abilityBridge,
            attackDecisionSource,
            ResolvePatternRunnerTargets(),
            ResolvePresentationCleanupTargets());
        stateMachine = new MobStateMachine();
        stateMachine.SetInitialState(new MobIdleState(), aiContext);
        LogFsmDebug($"FSM 초기화 완료. chaseIntent={(chaseIntent != null ? chaseIntent.name : "null")}, bridge={abilityBridge.GetType().Name}, decisionSource={attackDecisionSource.GetType().Name}");
        return true;
    }

    /// <summary>
    /// 책임:
    /// - 기존 EnemyChaseIntent2D와 몬스터별 추적 intent 구현을 모두 FSM 추적 인터페이스로 정규화한다.
    /// - Pawn처럼 개인화된 이동 intent가 일반 FSM 생명주기를 그대로 사용할 수 있게 한다.
    /// </summary>
    private IEnemyChaseIntent ResolveChaseIntent()
    {
        if (resolvedChaseIntent != null)
            return resolvedChaseIntent;

        if (chaseIntent != null)
            return chaseIntent;

        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IEnemyChaseIntent candidate)
            {
                resolvedChaseIntent = candidate;
                return resolvedChaseIntent;
            }
        }

        return null;
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
