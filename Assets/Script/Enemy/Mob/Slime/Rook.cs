using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// - Slime 계열 Rook의 돌진 공격 판단, 돌진 문맥 생성, 사망 시 분열 규칙을 소유한다.
/// - 실제 돌진 실행과 충돌 처리는 RookChargeRunner에 위임한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MobAbilityCoordinator))]
[RequireComponent(typeof(RookChargeRunner))]
public class Rook : Slime, IMobTargetDetectionOverride
{
    private const string ChargePrepareTriggerName = "chargePrepare";
    private const string ChargeTriggerName = "charge";
    private const string IsChargingBoolName = "isCharging";
    private const string DieTriggerName = "die";
    private const float WarningTime = 1.0f;
    private const float DashDistance = 5f;
    private const float MaxWallDashDistance = 60f;
    private const float WallDashStopSkin = 0.03f;
    private const float ChargeCastThickness = 0.02f;
    private const float ChargeLinecastSkin = 0.02f;
    private const float WarningWidth = 1.1f;
    private const string WallLayerName = "Wall";
    private const float MaxHealth = 13f;
    private const float VisualScale = 1.2f;
    private const float ChaseSpeedMultiplier = 0.5f;
    private const float SplitSpread = 0.55f;

    [SerializeField] private GameObject splitPrefab;
    [SerializeField] private AbilityDefinition chargeAbility;
    [SerializeField] private GE_Damage_Spec damageEffect;
    [SerializeField] private GE_Knockback_Spec knockbackEffect;
    [SerializeField] private float chargeDamageAmount = 1f;
    [SerializeField] private float playerKnockbackImpulse = 120f;
    [SerializeField] private float dashSpeedMultiplier = 3f;
    [SerializeField] private float chaseAssistRange = 6f;
    [SerializeField] private float inRoomChargeRangeMultiplier = 2f;
    [SerializeField, Min(0)] private int splitCount = 2;
    [SerializeField] private bool logChargeCastDebug;
    [SerializeField] private bool logRookFsmDebug;
    [SerializeField] private bool logChargeHitCandidateDebug;
    [SerializeField] private float rookFsmLogInterval = 0.35f;

    private RookChargeRunner chargeRunner;
    private readonly RaycastHit2D[] chargeCastHits = new RaycastHit2D[64];
    private readonly RaycastHit2D[] chargeLineHits = new RaycastHit2D[16];
    private int cachedDashBlockerMask = -1;
    private MonsterRoomArea2D cachedRoomArea;
    private MonsterReturnHome2D returnHome;
    private bool suppressSplit;
    private bool hasChargePrepareTrigger;
    private bool hasChargeTrigger;
    private bool hasIsChargingBool;
    private bool hasDieTrigger;
    private bool lockFacing;
    private bool hasLoggedInvalidConfig;
    private bool hasLockedRoomTarget;
    private float nextRookFsmLogTime;
    private string lastRookFsmLogMessage;
    private ChargeCastResult cachedChargeCastResult;
    private GameObject cachedChargeCastTarget;
    private int cachedChargeCastFrame = -1;

    /// <summary>
    /// 책임:
    /// - Rook 돌진 실행에 필요한 고정 방향, 거리, 속도, 피해 payload를 한 번에 전달한다.
    /// - 경고 표시와 실제 돌진이 같은 문맥 값을 사용하게 해 연출/판정 싱크를 맞춘다.
    /// </summary>
    public readonly struct ChargeContext
    {
        public readonly GameObject Target;
        public readonly Vector2 StartPos;
        public readonly Vector2 Direction;
        public readonly float WarningTime;
        public readonly float DashDistance;
        public readonly float DashSpeed;
        public readonly float WarningWidth;
        public readonly CombatHitPayload HitPayload;

        public ChargeContext(
            GameObject target,
            Vector2 startPos,
            Vector2 direction,
            float warningTime,
            float dashDistance,
            float dashSpeed,
            float warningWidth,
            CombatHitPayload hitPayload)
        {
            Target = target;
            StartPos = startPos;
            Direction = direction;
            WarningTime = warningTime;
            DashDistance = dashDistance;
            DashSpeed = dashSpeed;
            WarningWidth = warningWidth;
            HitPayload = hitPayload;
        }
    }

    protected override void Awake()
    {
        base.Awake();

        CacheCoordinator();

        chargeRunner = GetComponent<RookChargeRunner>();
        if (chargeRunner == null)
            chargeRunner = gameObject.AddComponent<RookChargeRunner>();

        returnHome = GetComponent<MonsterReturnHome2D>();
        CacheAnimatorParameters();
        SyncChaseIntentRange();
        ApplyStats();
    }

    private void OnValidate()
    {
        chaseAssistRange = Mathf.Max(0f, chaseAssistRange);
        inRoomChargeRangeMultiplier = Mathf.Max(1f, inRoomChargeRangeMultiplier);
        rookFsmLogInterval = Mathf.Max(0.05f, rookFsmLogInterval);
    }

    protected override void Start()
    {
        base.Start();
        GiveAbility(chargeAbility);
    }

    public override bool CanUseChaseMovement()
    {
        UpdateSpeed(ChaseSpeedMultiplier);

        if (!CanMove())
        {
            LogRookFsmThrottled($"CanUseChaseMovement=false. reason=CanMove false, isDead={IsDead}, chargeRunning={(chargeRunner != null && chargeRunner.IsRunning)}");
            return false;
        }

        GameObject targetObject = target != null ? target.gameObject : null;
        bool keepRoomTarget = ShouldKeepLockedRoomTarget(targetObject);
        SyncChaseIntentDetectionOverride(keepRoomTarget);
        if (!keepRoomTarget && !IsTargetWithinChaseAssistRange(targetObject, out float chaseDistance))
        {
            LogRookFsmThrottled(
                $"CanUseChaseMovement=false. reason=target outside chase assist range, " +
                $"distance={chaseDistance:F2}, chaseAssistRange={chaseAssistRange:F2}");
            return false;
        }

        bool canUse = chargeRunner == null || !chargeRunner.IsRunning;
        if (!canUse)
            LogRookFsmThrottled("CanUseChaseMovement=false. reason=charge runner is running.");

        return canUse;
    }

    /// <summary>룩 전용 FSM 감지 조건을 제공해 큰 방에서는 거리와 무관하게 돌진 판단까지 진입하게 합니다.</summary>
    public bool HasDetectedTargetForMobFsm()
    {
        GameObject targetObject = target != null ? target.gameObject : null;
        if (IsReturningHome())
        {
            ClearLockedRoomTarget();
            LogRookFsmThrottled("HasDetectedTargetForMobFsm=false. reason=returning home");
            return false;
        }

        if (targetObject == null)
        {
            ClearLockedRoomTarget();
            LogRookFsmThrottled("HasDetectedTargetForMobFsm=false. reason=target null");
            return false;
        }

        bool targetInRoom = IsTargetInOwnedRoom(targetObject);
        if (!targetInRoom)
            ClearLockedRoomTarget();

        if (ShouldKeepLockedRoomTarget(targetObject))
        {
            LogRookFsmThrottled($"HasDetectedTargetForMobFsm=true. reason=locked room target, target={targetObject.name}, position={transform.position}");
            return true;
        }

        if (CanStartRoomCharge(targetObject, "FSM detection"))
        {
            LockRoomTargetIfPossible(targetObject);
            LogRookFsmThrottled($"HasDetectedTargetForMobFsm=true. reason=charge ready, target={targetObject.name}, position={transform.position}");
            return true;
        }

        bool chaseFallbackDetected = IsTargetWithinChaseAssistRange(targetObject, out float chaseDistance);
        if (chaseFallbackDetected)
            LockRoomTargetIfPossible(targetObject);

        LogRookFsmThrottled(
            $"HasDetectedTargetForMobFsm={chaseFallbackDetected}. reason=charge not ready, chaseFallback={chaseFallbackDetected}, " +
            $"target={targetObject.name}, targetInRoom={targetInRoom}, lockedRoomTarget={hasLockedRoomTarget}, chaseDistance={chaseDistance:F2}, chaseAssistRange={chaseAssistRange:F2}, position={transform.position}");
        return chaseFallbackDetected;
    }

    protected override void OnDeathStarted()
    {
        CancelAbility();

        if (!suppressSplit && !IsPitFallDeath && splitPrefab != null && splitCount > 0)
        {
            PlaySplitDeathVanishEffect();
            SpawnSplit<Knight>(splitPrefab, splitCount, SplitSpread);
        }

        base.OnDeathStarted();
    }

    protected override void PlayDeathAnimation()
    {
        SetAnimatorTriggerIfAvailable(DieTriggerName, hasDieTrigger);
    }

    /// <summary>
    /// 책임:
    /// - Rook 돌진 경고/준비 동작 시작을 Animator trigger로 전달한다.
    /// - 돌진 판정과 이동 로직을 건드리지 않고 표현 상태만 전환한다.
    /// </summary>
    public void PlayChargePrepareAnimation()
    {
        SetAnimatorTriggerIfAvailable(ChargePrepareTriggerName, hasChargePrepareTrigger);
    }

    /// <summary>
    /// 책임:
    /// - Rook 실제 돌진 시작 타이밍을 Animator trigger로 전달한다.
    /// - 경고 표시 종료 후 dash 이동과 같은 순간에 돌진 애니메이션을 시작하게 한다.
    /// </summary>
    public void PlayChargeAnimation()
    {
        SetAnimatorTriggerIfAvailable(ChargeTriggerName, hasChargeTrigger);
    }

    /// <summary>
    /// 책임:
    /// - Rook이 실제 돌진 중인지 Animator bool로 전달한다.
    /// - 벽 충돌/취소/돌진 종료 전까지 Rush 애니메이션 상태가 유지되게 한다.
    /// </summary>
    public void SetChargeAnimationActive(bool isCharging)
    {
        SetAnimatorBoolIfAvailable(IsChargingBoolName, hasIsChargingBool, isCharging);
    }

    /// <summary>
    /// 책임:
    /// - Rook 돌진 중 공통 타겟 추적 flip 갱신을 잠그거나 해제한다.
    /// - 돌진 방향이 확정된 뒤 플레이어가 반대로 넘어가도 시각 방향이 흔들리지 않게 한다.
    /// </summary>
    public void SetFacingLocked(bool isLocked)
    {
        lockFacing = isLocked;
    }

    /// <summary>돌진 중에는 확정된 방향을 유지하고, 그 외에는 공통 타겟 바라보기를 사용합니다.</summary>
    protected override void UpdateFacing()
    {
        if (lockFacing)
            return;

        base.UpdateFacing();
    }

    protected override void DrawAttackGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, MaxWallDashDistance);

        Gizmos.color = new Color(1f, 0.55f, 0.05f, 1f);
        Gizmos.DrawWireSphere(transform.position, chaseAssistRange);
    }

    /// <summary>분열로 생성된 룩의 대기 시간과 분열 가능 상태를 설정합니다.</summary>
    public override void InitSplit(Transform nextTarget)
    {
        suppressSplit = false;
        base.InitSplit(nextTarget);
    }

    /// <summary>룩 돌진 공격에 필요한 실행 정보를 만듭니다.</summary>
    public bool TryBuildChargeContext(AbilitySystem system, AbilitySpec spec, GameObject explicitTarget, out ChargeContext context)
    {
        context = default;

        if (!CanAct()) return false;
        if (!HasChargeData()) return false;

        GameObject targetObject = GetTarget(explicitTarget);
        if (!CanStartRoomCharge(targetObject, "TryBuildChargeContext")) return false;

        Vector2 direction = GetDirection(targetObject);
        ChargeCastResult chargeCast = ResolveChargeCastCached(direction, targetObject);
        context = new ChargeContext(
            targetObject,
            chargeCast.Start,
            direction,
            WarningTime,
            chargeCast.Distance,
            GetDashSpeed(),
            WarningWidth,
            MakePayload(system, spec, damageEffect, knockbackEffect, chargeDamageAmount, playerKnockbackImpulse));
        return true;
    }

    /// <summary>룩이 구덩이에 빠졌을 때 분열 없이 즉사 처리합니다.</summary>
    public void FallIntoHole()
    {
        if (isDead) return;

        suppressSplit = true;
        Die();
    }

    /// <summary>FSM에서 사용할 룩 돌진 공격 요청을 만듭니다.</summary>
    public override bool TryBuildAttackRequest(out MobAttackRequest request)
    {
        request = default;

        if (!CanAct()) return false;
        if (!HasChargeData()) return false;

        GameObject targetObject = target != null ? target.gameObject : null;
        if (!CanStartRoomCharge(targetObject, "TryBuildAttackRequest")) return false;

        request = new MobAttackRequest(chargeAbility, targetObject);
        LogRookFsmThrottled($"TryBuildAttackRequest=true. ability={(chargeAbility != null ? chargeAbility.name : "null")}, target={(targetObject != null ? targetObject.name : "null")}");
        return request.IsValid;
    }

    /// <summary>룩의 돌진 지속 시간을 계산합니다.</summary>
    public float GetDashTime(float dashSpeed)
    {
        return GetDashTime(dashSpeed, DashDistance);
    }

    /// <summary>룩의 실제 돌진 거리와 속도를 기준으로 돌진 지속 시간을 계산합니다.</summary>
    public float GetDashTime(float dashSpeed, float dashDistance)
    {
        if (dashSpeed <= 0f) return 0f;

        return Mathf.Max(0f, dashDistance) / dashSpeed;
    }

    /// <summary>룩이 사용할 돌진 속도를 계산합니다.</summary>
    public float GetDashSpeed()
    {
        return GetPlayerSpeed() * dashSpeedMultiplier;
    }

    /// <summary>
    /// 책임:
    /// - 룩 돌진을 멈춰야 하는 충돌체인지 판정한다.
    /// - 닫힌 문은 레이어와 trigger 여부와 무관하게 차단하고, 열린 문과 일반 trigger는 통과시킨다.
    /// </summary>
    public bool IsChargeBlocker(Collider2D candidate)
    {
        if (candidate == null)
            return false;

        if (IsHoleTrapCollider(candidate))
            return false;

        if (CombatPathBlocker2DUtility.BlocksCombatPath(candidate, gameObject, CombatPathBlockerQuery.Charge))
        {
            LogChargeBlockerDecision(candidate, "IsChargeBlocker");
            return true;
        }

        if (candidate.isTrigger)
            return false;

        int layerBit = 1 << candidate.gameObject.layer;
        return (ResolveDashBlockerMask() & layerBit) != 0;
    }

    /// <summary>룩의 기본 스탯과 크기를 적용합니다.</summary>
    protected override void ApplyStats()
    {
        SetStats("Rook", MaxHealth, VisualScale);
    }

    /// <summary>
    /// 책임:
    /// - 룩 돌진 시전 조건을 "같은 방 안에 있고, 사이에 돌진 차단물이 없는가"로 판정한다.
    /// - 방 경계 데이터가 없는 테스트 환경에서는 시야 차단물 검사만으로 동작하게 한다.
    /// </summary>
    private bool CanStartRoomCharge(GameObject targetObject, string source)
    {
        if (targetObject == null)
        {
            LogRookFsmThrottled($"CanStartRoomCharge=false. source={source}, reason=target null");
            return false;
        }

        if (IsReturningHome())
        {
            LogRookFsmThrottled($"CanStartRoomCharge=false. source={source}, reason=returning home");
            return false;
        }

        Vector2 direction = GetDirection(targetObject);
        ChargeCastResult chargeCast = ResolveChargeCastCached(direction, targetObject);
        float targetDistance = Vector2.Distance(chargeCast.Start, ResolveChargeLineTargetPoint(targetObject));
        float allowedChargeRange = ResolveAllowedChargeRange(targetObject);
        if (targetDistance > allowedChargeRange)
        {
            LogRookFsmThrottled(
                $"CanStartRoomCharge=false. source={source}, reason=target outside allowed charge range, " +
                $"targetDistance={targetDistance:F2}, allowedChargeRange={allowedChargeRange:F2}, targetInRoom={IsTargetInOwnedRoom(targetObject)}, target={targetObject.name}");
            return false;
        }

        if (!chargeCast.ReachesTarget(targetDistance))
        {
            LogRookFsmThrottled(
                $"CanStartRoomCharge=false. source={source}, reason=target beyond charge cast, " +
                $"targetDistance={targetDistance:F2}, allowedChargeRange={allowedChargeRange:F2}, castDistance={chargeCast.Distance:F2}, blocker={(chargeCast.Blocker != null ? chargeCast.Blocker.name : "none")}, target={targetObject.name}");
            return false;
        }

        LogRookFsmThrottled(
            $"CanStartRoomCharge=true. source={source}, targetDistance={targetDistance:F2}, " +
            $"allowedChargeRange={allowedChargeRange:F2}, castDistance={chargeCast.Distance:F2}, target={targetObject.name}");
        return true;
    }

    /// <summary>돌진 각이 나오지 않을 때 룩이 위치를 다시 잡기 위해 추적해도 되는 거리인지 판정합니다.</summary>
    private bool IsTargetWithinChaseAssistRange(GameObject targetObject, out float distance)
    {
        distance = float.PositiveInfinity;
        if (targetObject == null)
            return false;

        distance = Vector2.Distance(transform.position, targetObject.transform.position);
        return distance <= Mathf.Max(0f, chaseAssistRange);
    }

    /// <summary>
    /// 책임:
    /// - 룩의 돌진 시전 허용 거리를 방 문맥에 따라 결정한다.
    /// - 플레이어가 룩 소속 방 안에 있으면 추적 보조 범위보다 긴 압박을 허용하고, 방 밖이면 추적 범위와 동일하게 제한한다.
    /// </summary>
    private float ResolveAllowedChargeRange(GameObject targetObject)
    {
        float baseRange = Mathf.Max(0f, chaseAssistRange);
        if (!IsTargetInOwnedRoom(targetObject))
            return baseRange;

        return baseRange * Mathf.Max(1f, inRoomChargeRangeMultiplier);
    }

    /// <summary>플레이어가 룩이 소속된 방 안에 있는지 판정하고, 방 데이터가 없으면 테스트 편의를 위해 방 안으로 간주합니다.</summary>
    private bool IsTargetInOwnedRoom(GameObject targetObject)
    {
        if (targetObject == null)
            return false;

        MonsterRoomArea2D roomArea = ResolveRoomArea();
        if (roomArea == null)
            return true;

        return roomArea.Contains(targetObject.transform.position);
    }

    /// <summary>
    /// 책임:
    /// - 룩이 한 번 인식한 플레이어를 방 안에 머무르는 동안 계속 추적 대상으로 유지할지 판정한다.
    /// - 순간적인 돌진 각/거리 조건 실패가 FSM 감지 해제로 이어져 룩이 멈추는 상황을 막는다.
    /// </summary>
    private bool ShouldKeepLockedRoomTarget(GameObject targetObject)
    {
        return hasLockedRoomTarget && IsTargetInOwnedRoom(targetObject);
    }

    /// <summary>현재 타겟이 룩 소속 방 안에 있다면 지속 추적 대상으로 잠급니다.</summary>
    private void LockRoomTargetIfPossible(GameObject targetObject)
    {
        if (!IsTargetInOwnedRoom(targetObject))
            return;

        hasLockedRoomTarget = true;
    }

    /// <summary>플레이어가 방을 벗어나거나 복귀 상태가 되면 지속 추적 대상을 해제합니다.</summary>
    private void ClearLockedRoomTarget()
    {
        hasLockedRoomTarget = false;
        SyncChaseIntentDetectionOverride(false);
    }

    /// <summary>
    /// 책임:
    /// - 룩이 방 안 플레이어를 지속 추적할 때 EnemyChaseIntent2D의 원형 detectionRange 제한과 충돌하지 않게 한다.
    /// - 방 밖이나 잠금 해제 상태에서는 공통 추적 감지 제한을 다시 사용한다.
    /// </summary>
    private void SyncChaseIntentDetectionOverride(bool ignoreDetectionRange)
    {
        if (ChaseIntent == null)
            return;

        ChaseIntent.SetIgnoreDetectionRange(ignoreDetectionRange);
    }

    /// <summary>
    /// 책임:
    /// - 룩이 홈 복귀 중인지 조회해 복귀 이동과 공격 준비가 동시에 일어나는 상태 충돌을 막는다.
    /// - MonsterReturnHome2D가 스폰 후 런타임에 추가되는 경우도 있어 필요할 때 재탐색한다.
    /// </summary>
    private bool IsReturningHome()
    {
        if (returnHome == null)
            returnHome = GetComponent<MonsterReturnHome2D>();

        return returnHome != null && returnHome.IsReturningHome;
    }

    /// <summary>룩이 현재 소속된 방 경계 데이터를 찾습니다.</summary>
    private MonsterRoomArea2D ResolveRoomArea()
    {
        if (cachedRoomArea != null)
            return cachedRoomArea;

        if (LockTrackingRoomGroup != null)
            cachedRoomArea = LockTrackingRoomGroup.GetComponentInChildren<MonsterRoomArea2D>();

        if (cachedRoomArea == null)
            cachedRoomArea = GetComponentInParent<MonsterRoomArea2D>();

        return cachedRoomArea;
    }

    /// <summary>
    /// 책임:
    /// - 룩 전용 추적 보조 범위와 실제 이동 의도를 내는 EnemyChaseIntent2D의 감지 범위를 동기화한다.
    /// - FSM은 Chase로 전이했는데 intent가 자체 detectionRange 때문에 이동을 거부하는 불일치를 막는다.
    /// </summary>
    private void SyncChaseIntentRange()
    {
        if (ChaseIntent == null)
            return;

        if (ChaseIntent.DetectionRange >= chaseAssistRange)
            return;

        ChaseIntent.SetDetectionRange(chaseAssistRange);
    }

    /// <summary>타깃 위치가 룩이 소속된 방 경계 안에 있는지 확인합니다.</summary>
    /// <summary>룩 돌진 경로 검사용 시작점을 계산합니다.</summary>
    private Vector2 ResolveChargeLineOrigin()
    {
        if (collision != null)
            return collision.bounds.center;

        return transform.position;
    }

    /// <summary>
    /// 책임:
    /// - 룩 돌진 경고/거리 계산의 시작점을 body 중심이 아니라 진행 방향 앞면으로 보정한다.
    /// - 경고 표시가 본체 중앙이나 뒤쪽에서 시작해 보이는 어색함을 줄인다.
    /// </summary>
    private Vector2 ResolveChargeStartPosition(Vector2 direction)
    {
        return transform.position;
    }

    /// <summary>
    /// 책임:
    /// - 룩 돌진 경고와 실제 이동이 함께 사용할 단일 cast 결과를 보관한다.
    /// - 공격 폭 기준 차단 거리 하나만 사용해 경고/이동 불일치를 막는다.
    /// </summary>
    private readonly struct ChargeCastResult
    {
        public readonly Vector2 Start;
        public readonly float Distance;
        public readonly Collider2D Blocker;

        public ChargeCastResult(Vector2 start, float distance, Collider2D blocker)
        {
            Start = start;
            Distance = distance;
            Blocker = blocker;
        }

        public bool ReachesTarget(float targetDistance)
        {
            return targetDistance <= Distance + WallDashStopSkin;
        }
    }

    /// <summary>룩 돌진 경로 검사용 타깃 지점을 계산합니다.</summary>
    private static Vector2 ResolveChargeLineTargetPoint(GameObject targetObject)
    {
        if (targetObject == null)
            return Vector2.zero;

        Collider2D targetCollider = targetObject.GetComponent<Collider2D>();
        if (targetCollider == null)
            targetCollider = targetObject.GetComponentInChildren<Collider2D>();

        if (targetCollider != null)
            return targetCollider.bounds.center;

        return targetObject.transform.position;
    }

    /// <summary>검사에 걸린 collider가 지정 transform 또는 그 자식 소유인지 확인합니다.</summary>
    private static bool IsColliderOwnedByTransform(Collider2D hit, Transform owner)
    {
        if (hit == null || owner == null)
            return false;

        Transform hitTransform = hit.transform;
        if (hitTransform == owner || hitTransform.IsChildOf(owner))
            return true;

        Rigidbody2D attachedBody = hit.attachedRigidbody;
        return attachedBody != null &&
               (attachedBody.transform == owner || attachedBody.transform.IsChildOf(owner));
    }

    /// <summary>룩 돌진 설정이 모두 연결되어 있는지 확인합니다.</summary>
    private bool HasChargeData()
    {
        bool isValid = chargeAbility != null &&
                       damageEffect != null &&
                       knockbackEffect != null &&
                       abilitySystem != null &&
                       chargeRunner != null;

        if (isValid) return true;

        if (!hasLoggedInvalidConfig)
        {
            Debug.LogError($"[{nameof(Rook)}] 돌진 설정이 비어 있습니다.", this);
            hasLoggedInvalidConfig = true;
        }

        return false;
    }

    /// <summary>
    /// 책임:
    /// - 룩 돌진 경고와 실제 이동에 함께 사용할 차단 거리 하나를 계산한다.
    /// - 공격 경고 폭 기준 BoxCast를 사용해 사선 벽에서 경고와 이동의 끝점이 갈라지지 않게 한다.
    /// </summary>
    private ChargeCastResult ResolveChargeCast(Vector2 direction, GameObject targetObject)
    {
        Vector2 origin = ResolveChargeStartPosition(direction);
        if (direction.sqrMagnitude <= 0.0001f)
            return new ChargeCastResult(origin, DashDistance, null);

        Vector2 normalizedDirection = direction.normalized;
        float angleDeg = Mathf.Atan2(normalizedDirection.y, normalizedDirection.x) * Mathf.Rad2Deg;
        ContactFilter2D filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = ResolveDashBlockerMask(),
            useTriggers = true
        };

        Vector2 castSize = new Vector2(ChargeCastThickness, WarningWidth);
        int hitCount = Physics2D.BoxCast(
            origin,
            castSize,
            angleDeg,
            normalizedDirection,
            filter,
            chargeCastHits,
            MaxWallDashDistance);

        EvaluateChargeLineSamples(
            origin,
            normalizedDirection,
            targetObject,
            ref hitCount,
            out float nearestSampleDistance,
            out Collider2D nearestSampleBlocker);

        if (logChargeCastDebug && hitCount >= chargeCastHits.Length)
        {
            Debug.LogWarning(
                $"[RookChargeCast] hit buffer full. caster={name}, hitCount={hitCount}, buffer={chargeCastHits.Length}. " +
                "벽/문 hit가 누락될 수 있습니다.",
                this);
        }

        float nearestDistance = float.PositiveInfinity;
        Collider2D nearestBlocker = null;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = chargeCastHits[i];
            bool accepted = IsValidDashDistanceBlocker(hit, targetObject);
            LogChargeCastHitCandidate(i, hit, accepted, targetObject);
            if (!accepted)
                continue;

            if (hit.distance >= nearestDistance)
                continue;

            nearestDistance = hit.distance;
            nearestBlocker = hit.collider;
        }

        if (nearestSampleBlocker != null && nearestSampleDistance < nearestDistance)
        {
            nearestDistance = nearestSampleDistance;
            nearestBlocker = nearestSampleBlocker;
        }

        if (float.IsPositiveInfinity(nearestDistance))
            return new ChargeCastResult(origin, MaxWallDashDistance, null);

        float distance = Mathf.Max(0.1f, nearestDistance - WallDashStopSkin);
        return new ChargeCastResult(origin, distance, nearestBlocker);
    }

    /// <summary>
    /// 책임:
    /// - 같은 프레임의 FSM 감지/공격 요청/문맥 생성이 동일한 돌진 cast 결과를 공유하게 한다.
    /// - 공격 가능 판정과 실제 경고/이동 거리가 서로 다른 cast 결과를 쓰는 불일치를 막는다.
    /// </summary>
    private ChargeCastResult ResolveChargeCastCached(Vector2 direction, GameObject targetObject)
    {
        if (cachedChargeCastFrame == Time.frameCount && cachedChargeCastTarget == targetObject)
            return cachedChargeCastResult;

        cachedChargeCastResult = ResolveChargeCast(direction, targetObject);
        cachedChargeCastTarget = targetObject;
        cachedChargeCastFrame = Time.frameCount;
        return cachedChargeCastResult;
    }

    /// <summary>
    /// 책임:
    /// - BoxCast가 타일맵/복합 콜라이더 모서리를 누락하는 상황을 보완하기 위해 경고 폭 안의 여러 선을 검사한다.
    /// - 중심선과 양쪽 가장자리 샘플 중 가장 가까운 차단물을 실제 돌진 끝점 후보로 제공한다.
    /// </summary>
    private void EvaluateChargeLineSamples(
        Vector2 origin,
        Vector2 direction,
        GameObject targetObject,
        ref int boxCastHitCount,
        out float nearestDistance,
        out Collider2D nearestBlocker)
    {
        nearestDistance = float.PositiveInfinity;
        nearestBlocker = null;

        Vector2 perpendicular = new Vector2(-direction.y, direction.x);
        float halfWidth = WarningWidth * 0.5f;
        float edgeOffset = Mathf.Max(0f, halfWidth - ChargeLinecastSkin);

        EvaluateChargeLineSample(origin, direction, targetObject, ref boxCastHitCount, ref nearestDistance, ref nearestBlocker, 0f);
        EvaluateChargeLineSample(origin + perpendicular * edgeOffset, direction, targetObject, ref boxCastHitCount, ref nearestDistance, ref nearestBlocker, edgeOffset);
        EvaluateChargeLineSample(origin - perpendicular * edgeOffset, direction, targetObject, ref boxCastHitCount, ref nearestDistance, ref nearestBlocker, -edgeOffset);
    }

    /// <summary>단일 경고 폭 샘플선을 raycast해 가장 가까운 돌진 차단물을 갱신합니다.</summary>
    private void EvaluateChargeLineSample(
        Vector2 sampleOrigin,
        Vector2 direction,
        GameObject targetObject,
        ref int boxCastHitCount,
        ref float nearestDistance,
        ref Collider2D nearestBlocker,
        float lateralOffset)
    {
        ContactFilter2D filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = ResolveDashBlockerMask(),
            useTriggers = true
        };

        int hitCount = Physics2D.Raycast(sampleOrigin, direction, filter, chargeLineHits, MaxWallDashDistance);
        boxCastHitCount += hitCount;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = chargeLineHits[i];
            if (!IsValidDashDistanceBlocker(hit, targetObject))
            {
                LogChargeLineSampleHitCandidate(lateralOffset, i, hit, false, targetObject);
                continue;
            }

            LogChargeLineSampleHitCandidate(lateralOffset, i, hit, true, targetObject);
            if (hit.distance >= nearestDistance)
                continue;

            nearestDistance = hit.distance;
            nearestBlocker = hit.collider;
        }
    }

    /// <summary>BoxCast 보조 샘플 raycast 후보의 채택/제외 이유를 로그로 출력합니다.</summary>
    private void LogChargeLineSampleHitCandidate(float lateralOffset, int index, RaycastHit2D hit, bool accepted, GameObject targetObject)
    {
        if (!logChargeHitCandidateDebug || hit.collider == null)
            return;

        string layerName = LayerMask.LayerToName(hit.collider.gameObject.layer);
        if (string.IsNullOrWhiteSpace(layerName))
            layerName = hit.collider.gameObject.layer.ToString();

        Debug.Log(
            $"[RookChargeLineSampleHit] caster={name}, lateralOffset={lateralOffset:F2}, index={index}, " +
            $"collider={hit.collider.name}/{hit.collider.GetType().Name}, layer={layerName}, trigger={hit.collider.isTrigger}, " +
            $"distance={hit.distance:F3}, accepted={accepted}, reason={ResolveChargeCastHitDebugReason(hit, targetObject)}",
            this);
    }

    /// <summary>
    /// 책임:
    /// - 룩 돌진 cast가 감지한 후보 collider가 왜 채택/제외됐는지 테스트 로그로 보여준다.
    /// - Tilemap/문/아군 collider가 경로 차단 판정에서 빠지는 원인을 빠르게 분리한다.
    /// </summary>
    private void LogChargeCastHitCandidate(int index, RaycastHit2D hit, bool accepted, GameObject targetObject)
    {
        if (!logChargeHitCandidateDebug)
            return;

        Collider2D hitCollider = hit.collider;
        if (hitCollider == null)
            return;

        string layerName = LayerMask.LayerToName(hitCollider.gameObject.layer);
        if (string.IsNullOrWhiteSpace(layerName))
            layerName = hitCollider.gameObject.layer.ToString();

        string reason = ResolveChargeCastHitDebugReason(hit, targetObject);
        Debug.Log(
            $"[RookChargeCastHit] caster={name}, index={index}, collider={hitCollider.name}/{hitCollider.GetType().Name}, " +
            $"layer={layerName}, trigger={hitCollider.isTrigger}, distance={hit.distance:F3}, accepted={accepted}, reason={reason}",
            this);
    }

    /// <summary>
    /// 책임:
    /// - 룩 돌진 cast 후보 collider의 제외 사유를 사람이 읽을 수 있는 문자열로 변환한다.
    /// - 디버그 로그가 판정 로직과 같은 기준을 설명하게 한다.
    /// </summary>
    private string ResolveChargeCastHitDebugReason(RaycastHit2D hit, GameObject targetObject)
    {
        Collider2D hitCollider = hit.collider;
        if (hitCollider == null)
            return "no collider";

        if (hit.distance <= WallDashStopSkin)
            return "too close to cast origin";

        if (IsColliderOwnedByTransform(hitCollider, transform))
            return "self";

        if (targetObject != null && IsColliderOwnedByTransform(hitCollider, targetObject.transform))
            return "target";

        if (IsHoleTrapCollider(hitCollider))
            return "hole trap";

        if (CombatPathBlocker2DUtility.BlocksCombatPath(hitCollider, gameObject, CombatPathBlockerQuery.Charge))
            return $"combat path blocker ({CombatPathBlocker2DUtility.DescribeBlockerDecision(hitCollider, gameObject, CombatPathBlockerQuery.Charge)})";

        if (hitCollider.isTrigger)
            return "trigger";

        int layerBit = 1 << hitCollider.gameObject.layer;
        if ((ResolveDashBlockerMask() & layerBit) != 0)
            return "dash blocker layer";

        return "not a dash blocker";
    }

    /// <summary>
    /// 책임:
    /// - 룩 돌진 중 문/경로 차단자 판정이 true가 된 순간의 collider와 차단 구현체 상세를 로그로 남긴다.
    /// - trigger가 obstacle처럼 처리되는지 확인하기 위한 임시 진단 정보를 제공한다.
    /// </summary>
    private void LogChargeBlockerDecision(Collider2D candidate, string source)
    {
        if (!logChargeHitCandidateDebug || candidate == null)
            return;

        string layerName = LayerMask.LayerToName(candidate.gameObject.layer);
        if (string.IsNullOrWhiteSpace(layerName))
            layerName = candidate.gameObject.layer.ToString();

        string decision = CombatPathBlocker2DUtility.DescribeBlockerDecision(
            candidate,
            gameObject,
            CombatPathBlockerQuery.Charge);

        Debug.Log(
            $"[RookChargeBlockerDecision] caster={name}, source={source}, collider={candidate.name}/{candidate.GetType().Name}, " +
            $"layer={layerName}, trigger={candidate.isTrigger}, decision={decision}",
            this);
    }

    /// <summary>
    /// 책임:
    /// - 룩 FSM/공격 전이 진단 로그를 과도하게 찍지 않도록 제한하면서 실패 이유 변화를 놓치지 않게 출력한다.
    /// - 문/벽 주변에서 Idle/Chase/Attack 중 어디에 갇히는지 테스트 중 빠르게 확인하게 한다.
    /// </summary>
    private void LogRookFsmThrottled(string message)
    {
        if (!logRookFsmDebug)
            return;

        bool isNewMessage = lastRookFsmLogMessage != message;
        if (!isNewMessage && Time.time < nextRookFsmLogTime)
            return;

        lastRookFsmLogMessage = message;
        nextRookFsmLogTime = Time.time + Mathf.Max(0.05f, rookFsmLogInterval);
        Debug.Log($"[RookFSM] {name}: {message}", this);
    }

    /// <summary>
    /// 책임:
    /// - 돌진 거리 산출용 cast/raycast 결과에서 자기 자신, 타깃, 시작 지점 겹침을 제외한다.
    /// - 제자리 차단으로 오인해 매우 짧은 경고만 표시되는 상황을 방지한다.
    /// </summary>
    private bool IsValidDashDistanceBlocker(RaycastHit2D hit, GameObject targetObject)
    {
        Collider2D hitCollider = hit.collider;
        if (hitCollider == null)
            return false;

        if (hit.distance <= WallDashStopSkin)
            return false;

        if (IsColliderOwnedByTransform(hitCollider, transform))
            return false;

        if (targetObject != null && IsColliderOwnedByTransform(hitCollider, targetObject.transform))
            return false;

        return IsChargeBlocker(hitCollider);
    }

    /// <summary>룩 돌진 경로 산출에서 구덩이 기믹 collider를 벽/문 차단물과 분리합니다.</summary>
    private static bool IsHoleTrapCollider(Collider2D candidate)
    {
        return candidate != null &&
               (candidate.GetComponent<HoleTrap>() != null ||
                candidate.GetComponentInParent<HoleTrap>() != null);
    }

    /// <summary>룩 돌진을 막는 지형 레이어 마스크를 구성합니다.</summary>
    private int ResolveDashBlockerMask()
    {
        if (cachedDashBlockerMask >= 0)
            return cachedDashBlockerMask;

        int mask = 0;
        AddLayerToMaskIfExists(ref mask, WallLayerName);
        cachedDashBlockerMask = mask;
        return cachedDashBlockerMask;
    }

    /// <summary>프로젝트에 존재하는 레이어만 안전하게 마스크에 추가합니다.</summary>
    private static void AddLayerToMaskIfExists(ref int mask, string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer < 0)
            return;

        mask |= 1 << layer;
    }

    /// <summary>Animator Controller에 Rook 전용 트리거가 있는지 캐시합니다.</summary>
    private void CacheAnimatorParameters()
    {
        hasChargePrepareTrigger = HasAnimatorParameter(ChargePrepareTriggerName, AnimatorControllerParameterType.Trigger);
        hasChargeTrigger = HasAnimatorParameter(ChargeTriggerName, AnimatorControllerParameterType.Trigger);
        hasIsChargingBool = HasAnimatorParameter(IsChargingBoolName, AnimatorControllerParameterType.Bool);
        hasDieTrigger = HasAnimatorParameter(DieTriggerName, AnimatorControllerParameterType.Trigger);
    }

    /// <summary>지정한 Animator 파라미터가 존재하고 타입이 맞는지 확인합니다.</summary>
    private bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType parameterType)
    {
        if (animator == null)
            return false;

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.type == parameterType && parameter.name == parameterName)
                return true;
        }

        return false;
    }

    /// <summary>파라미터가 존재할 때만 Animator trigger를 전달해 authoring 중 콘솔 오류를 방지합니다.</summary>
    private void SetAnimatorTriggerIfAvailable(string triggerName, bool hasTrigger)
    {
        if (!hasTrigger || animator == null)
            return;

        animator.SetTrigger(triggerName);
    }

    /// <summary>파라미터가 존재할 때만 Animator bool을 전달해 authoring 중 콘솔 오류를 방지합니다.</summary>
    private void SetAnimatorBoolIfAvailable(string boolName, bool hasBool, bool value)
    {
        if (!hasBool || animator == null)
            return;

        animator.SetBool(boolName, value);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        chargeRunner?.HandleBodyCollision(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        chargeRunner?.HandleBodyCollision(collision);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        chargeRunner?.HandleTrigger(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        chargeRunner?.HandleTrigger(other);
    }
}
