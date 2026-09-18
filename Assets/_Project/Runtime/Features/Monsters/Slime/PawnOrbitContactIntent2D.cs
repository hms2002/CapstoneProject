using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// - Pawn이 플레이어에게 접근한 뒤 정면으로 밀지 않고 주변을 접선 방향으로 돌며 압박하는 이동 의도를 만든다.
/// - 일반 몬스터 FSM의 추적 생명주기와 MovementMotor2D 의도 이동 인터페이스를 함께 만족한다.
/// - 개체별로 엇갈리는 짧은 전진과 휴식을 만들어 기어가는 이동 리듬을 담당한다.
/// - 벽으로 직선 접근이 막히면 경로의 방향만 사용하며 기존 기어가는 속도 리듬은 유지한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PawnOrbitContactIntent2D : MonoBehaviour, IIntentMovementSource2D, IEnemyChaseIntent, IMonsterSpawnContextReceiver
{
    [Header("Refs")]
    [SerializeField] private Enemy enemy;

    [Header("Detection")]
    [SerializeField, Min(0f)] private float detectionRange = 7f;
    [SerializeField, Min(0f)] private float targetAcquireInterval = 0.25f;

    [Header("Approach")]
    [SerializeField, Min(0f)] private float approachRange = 1.15f;
    [SerializeField, Min(0f)] private float approachSpeedScale = 2f;

    [Header("Orbit")]
    [SerializeField, Min(0.01f)] private float idealOrbitRadius = 0.72f;
    [SerializeField, Min(0f)] private float orbitSpeedScale = 1.55f;
    [SerializeField, Min(0f)] private float inwardPressure = 0.42f;
    [SerializeField, Min(0f)] private float outwardPressure = 0.75f;
    [SerializeField, Min(0f)] private float orbitRadiusDeadZone = 0.12f;

    [Header("Return")]
    [SerializeField, Min(0f)] private float returnSpeedScale = 0.9f;

    [Header("Crawl Rhythm")]
    [SerializeField, Min(0.02f)] private float crawlMoveSeconds = 0.22f;
    [SerializeField, Min(0f)] private float crawlRestSeconds = 0.32f;
    [SerializeField, Range(0f, 1f)] private float crawlPeakSpeedScale = 0.55f;

    private MonsterReturnHome2D returnHome;
    private MonsterSpawnContext spawnContext;
    private bool chaseEnabled = true;
    private int orbitSign = 1;
    private float nextTargetAcquireTime;
    private double crawlEpoch;
    private float crawlPhaseOffset;

    private const float PathRetrySeconds = 0.35f;
    private const float WaypointReachDistance = 0.18f;
    private readonly List<Vector2> approachPath = new();
    private readonly List<Collider2D> navigationBodies = new();
    private Rigidbody2D navigationBody;
    private bool navigationBodiesDirty = true;
    private TilemapPathfinder2D cachedPathfinder;
    private float nextFinderSearchTime;
    private float nextPathSearchTime;
    private Vector2 pathTarget;
    private int waypointIndex;
    private static int lastPawnSearchFrame = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSearchBudget() => lastPawnSearchFrame = -1;

    private void OnTransformChildrenChanged() => navigationBodiesDirty = true;

    private void OnEnable()
    {
        // Hash the instance without consuming gameplay RNG; pooled reuse restarts its clock.
        uint hash = unchecked((uint)GetInstanceID() * 2654435761u);
        crawlPhaseOffset = (hash & 0x00ffffffu) / 16777216f;
        crawlEpoch = Time.timeAsDouble;
        ResetNavigation();
    }

    private void Awake()
    {
        if (enemy == null)
            enemy = GetComponent<Enemy>();

        returnHome = GetComponent<MonsterReturnHome2D>();
        orbitSign = GetInstanceID() % 2 == 0 ? 1 : -1;
    }

    public IntentMovementData GetIntent()
    {
        if (returnHome != null && returnHome.TryGetReturnDirection(out Vector2 returnDirection))
            return CreateCrawlIntent(returnDirection, returnSpeedScale);

        if (!EnsureTarget())
            return IntentMovementData.None;

        Mob mob = enemy as Mob;
        if (mob != null && !mob.CanUseChaseMovement())
            return IntentMovementData.None;

        if (!chaseEnabled || !enemy.CanPerceiveTarget(enemy.Target))
            return IntentMovementData.None;

        Vector2 toTarget = (Vector2)(enemy.Target.position - transform.position);
        float distance = toTarget.magnitude;
        if (!CanIgnoreDetectionRange() && distance > detectionRange)
            return IntentMovementData.None;

        TilemapPathfinder2D finder = ResolvePathfinder();
        if (finder != null)
        {
            MonsterNavigationFootprint2D footprint = GetNavigationFootprint();
            if (!finder.HasDirectWalkableSegment(transform.position, enemy.Target.position, footprint))
                return CreateCrawlIntent(ResolvePathDirection(finder, enemy.Target.position, footprint), approachSpeedScale);

            approachPath.Clear();
            waypointIndex = 0;
        }

        if (distance > approachRange)
            return CreateCrawlIntent(toTarget.normalized, approachSpeedScale);

        Vector2 direction = ResolveOrbitDirection(toTarget, distance);
        return CreateCrawlIntent(direction, orbitSpeedScale);
    }

    private IntentMovementData CreateCrawlIntent(Vector2 direction, float speedScale)
    {
        float moveSeconds = Mathf.Max(0.02f, crawlMoveSeconds);
        float cycleSeconds = moveSeconds + Mathf.Max(0f, crawlRestSeconds);
        double elapsed = Time.timeAsDouble - crawlEpoch + crawlPhaseOffset * cycleSeconds;
        float phase = (float)(elapsed % cycleSeconds);
        if (phase >= moveSeconds)
            return IntentMovementData.None;

        // A smooth zero-to-peak-to-zero pulse leaves external knockback to the motor.
        float pulse = Mathf.Sin(Mathf.PI * phase / moveSeconds);
        float crawlScale = pulse * pulse * Mathf.Clamp01(crawlPeakSpeedScale);
        return IntentMovementData.FromDirection(direction, speedScale * crawlScale);
    }

    public void StartChase()
    {
        chaseEnabled = true;
    }

    public void StopChase()
    {
        chaseEnabled = false;
        ResetNavigation();
    }

    public bool IsTargetWithinDetectionRange()
    {
        if (!EnsureTarget())
            return false;

        if (!enemy.CanPerceiveTarget(enemy.Target))
            return false;

        Vector2 toTarget = enemy.Target.position - transform.position;
        return CanIgnoreDetectionRange() || toTarget.sqrMagnitude <= detectionRange * detectionRange;
    }

    /// <summary>Owns room-scoped pursuit context without changing the authored fallback range.</summary>
    public void ApplySpawnContext(MonsterSpawnContext context)
    {
        spawnContext = context;
        nextTargetAcquireTime = 0f;
        ResetNavigation();
    }

    private void ResetNavigation()
    {
        approachPath.Clear();
        waypointIndex = 0;
        cachedPathfinder = null;
        navigationBodiesDirty = true;
        nextFinderSearchTime = 0f;
        nextPathSearchTime = Time.time + crawlPhaseOffset * 0.15f;
    }

    private MonsterNavigationFootprint2D GetNavigationFootprint()
    {
        if (navigationBodiesDirty)
        {
            GetComponentsInChildren(true, navigationBodies);
            navigationBody = GetComponent<Rigidbody2D>();
            navigationBodiesDirty = false;
        }
        return MonsterNavigationFootprint2D.FromBodies(transform, navigationBody, navigationBodies);
    }

    private TilemapPathfinder2D ResolvePathfinder()
    {
        TilemapPathfinder2D candidate = spawnContext.Pathfinder;
        if (candidate == null || !candidate.isActiveAndEnabled || candidate.gameObject.scene != gameObject.scene)
            candidate = cachedPathfinder;
        if (candidate != null && candidate.isActiveAndEnabled && candidate.gameObject.scene == gameObject.scene)
        {
            if (cachedPathfinder != candidate)
            {
                approachPath.Clear();
                waypointIndex = 0;
            }
            return cachedPathfinder = candidate;
        }

        if (Time.time < nextFinderSearchTime) return null;
        nextFinderSearchTime = Time.time + 1f + crawlPhaseOffset * 0.2f;
        cachedPathfinder = null;
        approachPath.Clear();
        waypointIndex = 0;
        foreach (var finder in FindObjectsByType<TilemapPathfinder2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!finder.isActiveAndEnabled || finder.gameObject.scene != gameObject.scene) continue;
            cachedPathfinder = finder;
            break;
        }
        return cachedPathfinder;
    }

    private Vector2 ResolvePathDirection(TilemapPathfinder2D finder, Vector2 target, MonsterNavigationFootprint2D footprint)
    {
        Vector2 position = transform.position;
        while (waypointIndex < approachPath.Count &&
               (approachPath[waypointIndex] - position).sqrMagnitude <= WaypointReachDistance * WaypointReachDistance)
            waypointIndex++;

        bool exhausted = waypointIndex >= approachPath.Count;
        bool targetMoved = (target - pathTarget).sqrMagnitude > 0.35f * 0.35f;
        if ((exhausted || targetMoved) && Time.time >= nextPathSearchTime && lastPawnSearchFrame != Time.frameCount)
        {
            // At most one Pawn A* search per rendered frame, including frames with several physics ticks.
            lastPawnSearchFrame = Time.frameCount;
            nextPathSearchTime = Time.time + PathRetrySeconds + crawlPhaseOffset * 0.1f;
            pathTarget = target;
            approachPath.Clear();
            waypointIndex = 0;
            if (finder.TryBuildPath(position, target, out IReadOnlyList<Vector2> path, footprint))
                for (int i = 0; i < path.Count; i++) approachPath.Add(path[i]);

            // Skip the starting cell center only when the next segment is genuinely clear.
            if (approachPath.Count > 1 && finder.HasDirectWalkableSegment(position, approachPath[1], footprint))
                waypointIndex = 1;
            while (waypointIndex < approachPath.Count &&
                   (approachPath[waypointIndex] - position).sqrMagnitude <= WaypointReachDistance * WaypointReachDistance)
                waypointIndex++;
        }

        if (waypointIndex >= approachPath.Count) return Vector2.zero;
        if (!finder.HasDirectWalkableSegment(position, approachPath[waypointIndex], footprint))
        {
            approachPath.Clear();
            waypointIndex = 0;
            return Vector2.zero;
        }
        return (approachPath[waypointIndex] - position).normalized;
    }

    private bool CanIgnoreDetectionRange()
    {
        return enemy != null && enemy.Target != null && spawnContext.RoomArea != null &&
               spawnContext.RoomArea.Contains(transform.position) &&
               spawnContext.RoomArea.Contains(enemy.Target.position);
    }

    /// <summary>거리 오차를 보정하는 반경 성분과 접선 성분을 섞어 플레이어 주변을 미끄러지듯 돌게 한다.</summary>
    private Vector2 ResolveOrbitDirection(Vector2 toTarget, float distance)
    {
        if (distance <= 0.001f)
            toTarget = Vector2.right;

        Vector2 radialToPlayer = toTarget.normalized;
        Vector2 tangent = new Vector2(-radialToPlayer.y, radialToPlayer.x) * orbitSign;
        float radiusError = distance - idealOrbitRadius;
        Vector2 radialCorrection = Vector2.zero;

        if (radiusError > orbitRadiusDeadZone)
            radialCorrection = radialToPlayer * inwardPressure;
        else if (radiusError < -orbitRadiusDeadZone)
            radialCorrection = -radialToPlayer * outwardPressure;

        Vector2 result = tangent + radialCorrection;
        return result.sqrMagnitude > 0.0001f ? result.normalized : tangent;
    }

    /// <summary>타겟이 비어 있으면 낮은 빈도로 감지 범위 검색을 수행해 추적 대상을 회복한다.</summary>
    private bool EnsureTarget()
    {
        if (enemy == null)
            return false;

        if (enemy.Target != null)
            return true;

        if (Time.time < nextTargetAcquireTime)
            return false;

        nextTargetAcquireTime = Time.time + Mathf.Max(0.05f, targetAcquireInterval);
        float searchRange = detectionRange;
        if (spawnContext.RoomArea != null && spawnContext.RoomArea.Contains(transform.position))
        {
            Bounds bounds = spawnContext.RoomArea.AreaCollider.bounds;
            searchRange = Mathf.Max(searchRange,
                Vector2.Distance(transform.position, bounds.center) + ((Vector2)bounds.extents).magnitude);
        }

        return enemy.TryAcquireTargetInRange(searchRange);
    }
}
