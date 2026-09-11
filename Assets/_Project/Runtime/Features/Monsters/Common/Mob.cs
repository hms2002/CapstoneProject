using System.Collections.Generic;
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

    [Header("AI Thinking")]
    [Tooltip("공격 시작 여부를 다시 판단하는 최소 간격입니다. 0이면 매 프레임 판단합니다.")]
    [SerializeField, Min(0f)] private float attackThinkIntervalSeconds = 0.12f;
    [Tooltip("동시에 스폰된 몬스터의 첫 공격 판단이 한 프레임에 몰리지 않도록 랜덤 분산하는 시간입니다.")]
    [SerializeField, Min(0f)] private float attackThinkInitialSpreadSeconds = 0.12f;
    [Tooltip("긴 전투 중 몬스터들의 공격 판단 주기가 다시 같은 프레임으로 맞물리지 않도록 매 판단마다 더하는 랜덤 분산 시간입니다.")]
    [SerializeField, Min(0f)] private float attackThinkJitterSeconds = 0.04f;

    [Header("Post Attack Spacing")]
    [Tooltip("공격 후 공통 AI 후딜에 더할 시간입니다. 탱커/자폭형처럼 제외가 필요한 몬스터는 override로 무시합니다.")]
    [SerializeField, Min(0f)] private float postAttackRecoveryBonusSeconds = 0.2f;

    [Header("Recovery Retreat")]
    [Tooltip("공격 후 후딜 동안 플레이어에게서 살짝 물러나는 공통 행동을 사용할지 정합니다.")]
    [SerializeField] private bool usePostAttackRecoveryRetreat = true;
    [SerializeField, Min(0f)] private float recoveryRetreatMaxTargetDistance = 3f;
    [SerializeField, Min(0f)] private float recoveryRetreatMinTargetDistance = 1.1f;
    [SerializeField, Min(0f)] private float recoveryRetreatSpeedScale = 0.75f;
    [SerializeField, Range(0f, 1f)] private float recoveryRetreatFarSpeedScale = 0.35f;
    [SerializeField, Min(0.02f)] private float recoveryRetreatThinkInterval = 0.08f;
    [SerializeField, Min(0.02f)] private float recoveryRetreatVelocityDuration = 0.12f;
    [SerializeField, Min(0f)] private float recoveryRetreatDamping = 0f;
    [Tooltip("비워두면 Wall 레이어를 자동으로 사용합니다.")]
    [SerializeField] private LayerMask recoveryRetreatWallLayers;
    [SerializeField, Min(0f)] private float recoveryRetreatWallProbeDistance = 0.25f;

    [Header("Debug")]
    [Tooltip("켜두면 일반 몬스터 FSM 초기화와 상태 전이 판단 로그를 출력합니다.")]
    [SerializeField] private bool logMobFsmDebug;

    private bool hasMoveBool;
    private MobStateMachine stateMachine;
    private MobAIContext aiContext;
    private ChestMonsterKillLock lockTrackingChestLock;
    private MonsterSpawnRoomGroup lockTrackingRoomGroup;
    private MonsterLockTrackingUnit lockTrackingUnit;
    private IEnemyChaseIntent resolvedChaseIntent;
    private PitFallReaction2D pitFallReaction;
    private bool triedInitializeStateMachine;
    private bool suppressMonsterLootDrop;
    private int pitFallDeathResolutionDepth;
    private int facingLockCount;
    private float spawnIdlePauseUntilTime;
    private float recoveryRetreatEndTime;
    private float nextRecoveryRetreatThinkTime;
    private int recoveryRetreatSideSign = 1;
    private Vector2 cachedRecoveryRetreatVelocity;
    private readonly RaycastHit2D[] recoveryRetreatWallHits = new RaycastHit2D[4];
    private ContactFilter2D recoveryRetreatWallFilter;

    protected EnemyChaseIntent2D ChaseIntent => chaseIntent;
    protected MonsterSpawnRoomGroup LockTrackingRoomGroup => lockTrackingRoomGroup;
    protected virtual bool SuppressMonsterFieldItemLootDrop => false;
    public bool LogMobFsmDebug => logMobFsmDebug;
    public override bool IsRecognizingPlayer =>
        !isDead && aiContext != null && aiContext.HasDetectedTarget();

    protected override void Awake()
    {
        base.Awake();

        if (chaseIntent == null)
            chaseIntent = GetComponent<EnemyChaseIntent2D>();

        pitFallReaction = GetComponentInChildren<PitFallReaction2D>(includeInactive: true);
        resolvedChaseIntent = ResolveChaseIntent();
        recoveryRetreatSideSign = GetInstanceID() % 2 == 0 ? 1 : -1;
        ConfigureRecoveryRetreatWallFilter();

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

    /// <summary>
    /// 책임:
    /// - 공격 후 공통 recover 상태를 사용할지 몬스터별로 결정한다.
    /// - 기본 recover가 0이어도 공통 후딜 보너스가 있으면 recover 상태를 태워 몬스터 공격 템포를 완화한다.
    /// </summary>
    public virtual bool ShouldUsePostAttackRecoverState(float baseRecoverSeconds)
    {
        return baseRecoverSeconds > 0f || postAttackRecoveryBonusSeconds > 0f;
    }

    /// <summary>
    /// 책임:
    /// - 공격속도 보정이 끝난 recover 시간에 일반 몬스터 공통 후딜 보너스를 더한다.
    /// - 탱커/자폭형처럼 공격 후 바로 버티거나 특수 상태를 유지해야 하는 몬스터가 override로 제외할 수 있게 한다.
    /// </summary>
    public virtual float ResolvePostAttackRecoverSeconds(float scaledRecoverSeconds)
    {
        return Mathf.Max(0f, scaledRecoverSeconds) + Mathf.Max(0f, postAttackRecoveryBonusSeconds);
    }

    /// <summary>
    /// 책임:
    /// - 공격 후 recover 동안 플레이어에게서 물러나는 이동을 사용할 수 있는지 몬스터별로 결정한다.
    /// - Tank/Skeleton 같은 예외 타입은 기존 공격 후 자리잡기 감각을 유지하도록 override로 false를 반환한다.
    /// </summary>
    public virtual bool CanUsePostAttackRecoveryRetreat()
    {
        return usePostAttackRecoveryRetreat &&
               !isDead &&
               target != null &&
               externalMovement != null &&
               attributeStatSource != null;
    }

    /// <summary>
    /// 책임:
    /// - MobRecoverState 진입 시 후퇴 이동 캐시를 초기화하고, 첫 틱에서 즉시 방향을 계산하게 준비한다.
    /// - 실제 이동 적용은 ExternalMovementController2D로 위임해 MovementMotor2D의 벽 안전장치를 그대로 사용한다.
    /// </summary>
    public void BeginPostAttackRecoveryRetreat(float recoverEndTime)
    {
        recoveryRetreatEndTime = Mathf.Max(Time.time, recoverEndTime);
        nextRecoveryRetreatThinkTime = 0f;
        cachedRecoveryRetreatVelocity = Vector2.zero;
        externalMovement?.RemoveTimedVelocitiesFromSource(this);
    }

    /// <summary>
    /// 책임:
    /// - 공격 후 recover 상태에서 낮은 빈도로 후퇴 방향을 다시 계산하고 짧은 외부 이동으로 적용한다.
    /// - 매 프레임 주변 탐색을 하지 않고 타겟 방향/벽 캐스트만 사용해 군집 행동 보정 비용을 작게 유지한다.
    /// </summary>
    public void TickPostAttackRecoveryRetreat()
    {
        if (Time.time >= recoveryRetreatEndTime || !CanUsePostAttackRecoveryRetreat())
        {
            StopPostAttackRecoveryRetreat();
            return;
        }

        if (Time.time < nextRecoveryRetreatThinkTime)
            return;

        nextRecoveryRetreatThinkTime = Time.time + Mathf.Max(0.02f, recoveryRetreatThinkInterval);
        cachedRecoveryRetreatVelocity = ResolveRecoveryRetreatVelocity();
        externalMovement.RemoveTimedVelocitiesFromSource(this);

        if (cachedRecoveryRetreatVelocity.sqrMagnitude <= 0.0001f)
            return;

        externalMovement.AddTimedVelocity(
            cachedRecoveryRetreatVelocity,
            Mathf.Max(0.02f, recoveryRetreatVelocityDuration),
            Mathf.Max(0f, recoveryRetreatDamping),
            this);
    }

    /// <summary>
    /// 책임:
    /// - recover 상태 종료/중단 시 이 몬스터가 넣은 후퇴 외부 이동만 정리한다.
    /// - 넉백이나 다른 시스템이 넣은 외압을 함께 지우지 않도록 source 기반 제거를 사용한다.
    /// </summary>
    public void StopPostAttackRecoveryRetreat()
    {
        recoveryRetreatEndTime = 0f;
        cachedRecoveryRetreatVelocity = Vector2.zero;
        externalMovement?.RemoveTimedVelocitiesFromSource(this);
    }

    private Vector2 ResolveRecoveryRetreatVelocity()
    {
        if (target == null || attributeStatSource == null)
            return Vector2.zero;

        Vector2 toSelf = (Vector2)(transform.position - target.position);
        float distance = toSelf.magnitude;
        float maxDistance = Mathf.Max(0.01f, recoveryRetreatMaxTargetDistance);
        float minDistance = Mathf.Clamp(recoveryRetreatMinTargetDistance, 0f, maxDistance);
        if (distance >= maxDistance)
            return Vector2.zero;

        Vector2 awayDirection = distance > 0.001f
            ? toSelf / distance
            : new Vector2(recoveryRetreatSideSign, 0f);

        Vector2 retreatDirection = ResolveWallSafeRecoveryRetreatDirection(awayDirection);
        if (retreatDirection.sqrMagnitude <= 0.0001f)
            return Vector2.zero;

        float moveSpeed = Mathf.Max(0f, attributeStatSource.Get(StatId.MoveSpeedFinal));
        if (moveSpeed <= 0f)
            return Vector2.zero;

        float closeWeight = Mathf.InverseLerp(maxDistance, minDistance, distance);
        float distanceScale = Mathf.Lerp(recoveryRetreatFarSpeedScale, 1f, closeWeight);
        return retreatDirection.normalized * moveSpeed * Mathf.Max(0f, recoveryRetreatSpeedScale) * distanceScale;
    }

    private Vector2 ResolveWallSafeRecoveryRetreatDirection(Vector2 awayDirection)
    {
        if (awayDirection.sqrMagnitude <= 0.0001f)
            return Vector2.zero;

        awayDirection.Normalize();
        if (!IsRecoveryRetreatDirectionBlocked(awayDirection))
            return awayDirection;

        Vector2 tangent = new Vector2(-awayDirection.y, awayDirection.x) * recoveryRetreatSideSign;
        if (!IsRecoveryRetreatDirectionBlocked(tangent))
            return tangent;

        tangent = -tangent;
        return !IsRecoveryRetreatDirectionBlocked(tangent) ? tangent : Vector2.zero;
    }

    private bool IsRecoveryRetreatDirectionBlocked(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f ||
            recoveryRetreatWallLayers.value == 0 ||
            recoveryRetreatWallProbeDistance <= 0f ||
            collision == null ||
            !collision.enabled ||
            collision.isTrigger)
        {
            return false;
        }

        int hitCount = collision.Cast(
            direction.normalized,
            recoveryRetreatWallFilter,
            recoveryRetreatWallHits,
            recoveryRetreatWallProbeDistance);

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hitCollider = recoveryRetreatWallHits[i].collider;
            if (hitCollider != null && hitCollider.attachedRigidbody != rigid2D)
                return true;
        }

        return false;
    }

    private void ConfigureRecoveryRetreatWallFilter()
    {
        if (recoveryRetreatWallLayers.value == 0)
        {
            int wallLayer = LayerMask.NameToLayer("Wall");
            if (wallLayer >= 0)
                recoveryRetreatWallLayers = 1 << wallLayer;
        }

        recoveryRetreatWallFilter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = recoveryRetreatWallLayers,
            useTriggers = false
        };
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
        ApplyLockTrackingContext(chestLock, roomGroup, null);
    }

    internal void ApplyLockTrackingContext(
        ChestMonsterKillLock chestLock,
        MonsterSpawnRoomGroup roomGroup,
        MonsterLockTrackingUnit sharedUnit)
    {
        lockTrackingChestLock = chestLock;
        lockTrackingRoomGroup = roomGroup;

        if (sharedUnit != null)
            AssignLockTrackingUnit(sharedUnit);
        else if (chestLock != null || roomGroup != null)
            GetOrCreateLockTrackingUnit();
    }

    public void SuppressMonsterLootDrop()
    {
        suppressMonsterLootDrop = true;
    }

    public virtual void BeginPitFallDeathResolution(PitFallContext context)
    {
        pitFallDeathResolutionDepth++;
    }

    public virtual void EndPitFallDeathResolution(PitFallContext context)
    {
        pitFallDeathResolutionDepth = Mathf.Max(0, pitFallDeathResolutionDepth - 1);
    }

    protected void RegisterLockTrackedChild(GameObject child)
    {
        if (child == null)
            return;

        MonsterLockTrackingUnit unit = GetOrCreateLockTrackingUnit();
        unit.AddMember(child);

        if (child.TryGetComponent(out Mob childMob))
            childMob.ApplyLockTrackingContext(lockTrackingChestLock, lockTrackingRoomGroup, unit);
    }

    protected override void OnDeathStarted()
    {
        EnterDeathState();

        if (!suppressMonsterLootDrop && pitFallDeathResolutionDepth <= 0)
            LootManager.Instance?.SpawnMonsterLoot(transform.position, gameObject, SuppressMonsterFieldItemLootDrop);
    }

    internal MonsterLockTrackingUnit GetOrCreateLockTrackingUnit()
    {
        if (lockTrackingUnit == null)
            AssignLockTrackingUnit(new MonsterLockTrackingUnit());

        return lockTrackingUnit;
    }

    internal void AssignLockTrackingUnit(MonsterLockTrackingUnit unit)
    {
        if (unit == null)
            return;

        lockTrackingUnit = unit;
        lockTrackingUnit.AddMember(gameObject);
    }

    internal static MonsterLockTrackingUnit ResolveOrCreateLockTrackingUnit(GameObject monster)
    {
        Mob mob = ResolveMob(monster);
        if (mob != null)
            return mob.GetOrCreateLockTrackingUnit();

        MonsterLockTrackingUnit unit = new MonsterLockTrackingUnit();
        unit.AddMember(monster);
        return unit;
    }

    /// <summary>
    /// 책임:
    /// - 몬스터 프리팹 루트/자식 배치 차이를 흡수해 방 잠금 추적이 실제 Mob 본체를 찾게 한다.
    /// - 충돌체와 피격체를 child로 분리한 프리팹에서도 기존 encounter lock API를 그대로 사용할 수 있게 한다.
    /// </summary>
    private static Mob ResolveMob(GameObject monster)
    {
        if (monster == null)
            return null;

        Mob mob = monster.GetComponent<Mob>();
        if (mob != null)
            return mob;

        return monster.GetComponentInChildren<Mob>(includeInactive: true);
    }

    private void OnDisable()
    {
        pitFallDeathResolutionDepth = 0;
        StopPostAttackRecoveryRetreat();
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
            ResolvePresentationCleanupTargets(),
            attackThinkIntervalSeconds,
            attackThinkInitialSpreadSeconds,
            attackThinkJitterSeconds);
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

internal sealed class MonsterLockTrackingUnit
{
    private readonly List<GameObject> members = new();

    public void AddMember(GameObject member)
    {
        if (member == null)
            return;

        CompactDestroyedMembers();
        for (int i = 0; i < members.Count; i++)
        {
            if (members[i] == member)
                return;
        }

        members.Add(member);
    }

    public bool TryGetAliveRepresentative(out GameObject representative)
    {
        CompactDestroyedMembers();
        for (int i = 0; i < members.Count; i++)
        {
            GameObject member = members[i];
            if (IsAliveMember(member))
            {
                representative = member;
                return true;
            }
        }

        representative = null;
        return false;
    }

    public bool HasAliveMember()
    {
        return TryGetAliveRepresentative(out _);
    }

    private void CompactDestroyedMembers()
    {
        for (int i = members.Count - 1; i >= 0; i--)
        {
            if (members[i] == null)
                members.RemoveAt(i);
        }
    }

    private static bool IsAliveMember(GameObject member)
    {
        if (member == null)
            return false;

        Enemy enemy = member.GetComponent<Enemy>();
        if (enemy == null)
            enemy = member.GetComponentInChildren<Enemy>(includeInactive: true);

        return enemy == null || !enemy.IsDead;
    }
}
