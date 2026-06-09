using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MonsterReturnHome2D : MonoBehaviour, IMonsterSpawnContextReceiver
{
    // 이 클래스의 책임:
    // 몬스터가 방 밖으로 이탈한 뒤 추적이 해제되면 스폰 위치로 복귀하도록 상태를 관리하고, 복귀 방향을 제공한다.

    [Header("Refs")]
    [SerializeField] private Enemy enemy;
    [SerializeField] private EnemyChaseIntent2D chaseIntent;

    [Header("Return")]
    [SerializeField] private float returnDelay = 0.75f;
    [SerializeField] private float waypointReachDistance = 0.2f;
    [SerializeField] private float repathInterval = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLog;
    [SerializeField] private bool drawReturnPathGizmo = true;

    private MonsterSpawnContext spawnContext;
    private float lostTargetTime;
    private float nextRepathTime;
    private bool isReturningHome;
    private readonly List<Vector2> path = new();
    private int pathIndex;

    private void Awake()
    {
        if (enemy == null)
            enemy = GetComponent<Enemy>();

        if (chaseIntent == null)
            chaseIntent = GetComponent<EnemyChaseIntent2D>();
    }

    private void Update()
    {
        if (enemy == null || enemy.IsDead)
        {
            ResetReturnState();
            return;
        }

        if (ShouldResumeChase())
        {
            ResetReturnState();
            return;
        }

        if (ShouldStartReturn())
        {
            lostTargetTime += Time.deltaTime;
            if (lostTargetTime >= returnDelay)
                EnsureReturnPath();

            return;
        }

        lostTargetTime = 0f;

        if (isReturningHome)
            RefreshReturnPathIfNeeded();
    }

    /// <summary>스폰 시 전달된 홈 위치와 방 문맥을 저장합니다.</summary>
    public void ApplySpawnContext(MonsterSpawnContext context)
    {
        spawnContext = context;
        ResetReturnState();
        LogDebug($"스폰 문맥 수신: home={spawnContext.HomePosition}, roomArea={(spawnContext.RoomArea != null ? spawnContext.RoomArea.name : "null")}, pathfinder={(spawnContext.Pathfinder != null ? spawnContext.Pathfinder.name : "null")}");
    }

    /// <summary>복귀 상태라면 현재 waypoint로 향하는 방향을 제공합니다.</summary>
    public bool TryGetReturnDirection(out Vector2 direction)
    {
        direction = Vector2.zero;

        if (!isReturningHome)
            return false;

        if (path.Count == 0)
            return false;

        AdvanceWaypoints();
        if (pathIndex >= path.Count)
        {
            ResetReturnState();
            return false;
        }

        Vector2 currentWaypoint = path[pathIndex];
        Vector2 toWaypoint = currentWaypoint - (Vector2)transform.position;
        if (toWaypoint.sqrMagnitude <= 0.0001f)
            return false;

        direction = toWaypoint.normalized;
        return true;
    }

    /// <summary>복귀 상태 여부를 외부에 제공합니다.</summary>
    public bool IsReturningHome => isReturningHome;

    /// <summary>지금 방 밖으로 나가 있고 추적도 해제되어 복귀를 시작해야 하는지 판단합니다.</summary>
    private bool ShouldStartReturn()
    {
        if (spawnContext.RoomArea == null)
        {
            LogDebug("복귀 불가: roomArea가 없습니다.");
            return false;
        }

        bool isOutsideRoom = !spawnContext.RoomArea.Contains(transform.position);
        if (!isOutsideRoom)
            return false;

        return chaseIntent == null || !chaseIntent.IsTargetWithinDetectionRange();
    }

    /// <summary>플레이어를 다시 감지하면 복귀를 중단해야 하는지 판단합니다.</summary>
    private bool ShouldResumeChase()
    {
        return chaseIntent != null && chaseIntent.IsTargetWithinDetectionRange();
    }

    /// <summary>복귀 경로가 없으면 계산하고 복귀 상태를 시작합니다.</summary>
    private void EnsureReturnPath()
    {
        if (spawnContext.Pathfinder == null)
        {
            LogDebug("복귀 불가: pathfinder가 없습니다.");
            return;
        }

        if (isReturningHome && path.Count > 0)
            return;

        RebuildPath();
    }

    /// <summary>복귀 중에는 주기적으로 경로를 다시 계산해 막힘에 대응합니다.</summary>
    private void RefreshReturnPathIfNeeded()
    {
        if (Time.time < nextRepathTime)
            return;

        RebuildPath();
    }

    /// <summary>현재 위치에서 홈 위치까지의 경로를 다시 계산합니다.</summary>
    private void RebuildPath()
    {
        nextRepathTime = Time.time + Mathf.Max(0.05f, repathInterval);

        if (spawnContext.Pathfinder == null)
            return;

        if (!spawnContext.Pathfinder.TryBuildPath(transform.position, spawnContext.HomePosition, out IReadOnlyList<Vector2> result))
        {
            LogDebug($"경로 재계산 실패: current={(Vector2)transform.position}, home={spawnContext.HomePosition}");
            return;
        }

        path.Clear();
        for (int i = 0; i < result.Count; i++)
            path.Add(result[i]);

        pathIndex = 0;
        isReturningHome = path.Count > 0;
        LogDebug($"경로 재계산 성공: waypoints={path.Count}, current={(Vector2)transform.position}, home={spawnContext.HomePosition}");
    }

    /// <summary>현재 위치에 도달한 waypoint들을 소비하고 홈 도착을 판정합니다.</summary>
    private void AdvanceWaypoints()
    {
        float reachDistance = Mathf.Max(0.01f, waypointReachDistance);
        float reachDistanceSqr = reachDistance * reachDistance;

        while (pathIndex < path.Count)
        {
            Vector2 waypoint = path[pathIndex];
            if (((Vector2)transform.position - waypoint).sqrMagnitude > reachDistanceSqr)
                break;

            pathIndex++;
        }

        if (((Vector2)transform.position - (Vector2)spawnContext.HomePosition).sqrMagnitude <= reachDistanceSqr)
            ResetReturnState();
    }

    /// <summary>복귀 상태와 캐시된 경로를 초기화합니다.</summary>
    private void ResetReturnState()
    {
        lostTargetTime = 0f;
        nextRepathTime = 0f;
        isReturningHome = false;
        path.Clear();
        pathIndex = 0;
    }

    /// <summary>복귀 상태를 이해하기 쉽게 로그로 남깁니다.</summary>
    private void LogDebug(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[MonsterReturnHome2D] {name}: {message}", this);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawReturnPathGizmo)
            return;

        if (spawnContext.HomePosition != Vector3.zero)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(spawnContext.HomePosition, 0.18f);
            Gizmos.DrawLine(transform.position, spawnContext.HomePosition);
        }

        if (path.Count == 0)
            return;

        Gizmos.color = isReturningHome ? Color.green : Color.gray;
        for (int i = 0; i < path.Count; i++)
        {
            Gizmos.DrawSphere(path[i], 0.07f);

            if (i == 0)
            {
                Gizmos.DrawLine(transform.position, path[i]);
                continue;
            }

            Gizmos.DrawLine(path[i - 1], path[i]);
        }
    }
}
