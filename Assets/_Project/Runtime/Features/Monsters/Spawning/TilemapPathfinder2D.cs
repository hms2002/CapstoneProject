using System.Collections.Generic;
using UnityEngine;
using Grid = UnityEngine.Grid;
using UnityEngine.Tilemaps;

/// <summary>
/// 책임:
/// - 타일맵 격자 기준으로 월드 좌표 사이의 경로를 계산한다.
/// - 막힌 레이어를 피해서 복귀용 waypoint 목록을 제공한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class TilemapPathfinder2D : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField] private Grid grid;
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private Tilemap[] additionalGroundTilemaps;

    [Header("Collision")]
    [SerializeField] private LayerMask blockedLayers = 1 << 30;
    [SerializeField] private Vector2 probeSize = new Vector2(0.7f, 0.7f);

    [Header("Search")]
    [SerializeField] private bool allowDiagonal = false;
    [SerializeField] private int maxPaddingCells = 16;
    [SerializeField] private int maxVisitedNodes = 2048;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLog;
    [SerializeField] private bool drawLastPathGizmo = true;

    private readonly List<Vector2> reusablePath = new();
    private readonly Dictionary<Vector2Int, Vector2Int> cameFrom = new();
    private readonly Dictionary<Vector2Int, int> gScore = new();
    private readonly List<Vector2Int> openSet = new();
    private readonly HashSet<Vector2Int> closedSet = new();
    private readonly HashSet<Tilemap> runtimeGroundTilemaps = new();
    private readonly List<Vector2> lastDebugPath = new();
    private Vector2 lastDebugStart;
    private Vector2 lastDebugEnd;
    private bool lastDebugSucceeded;

    /// <summary>시작점에서 목표점까지의 경로를 월드 좌표 waypoint 목록으로 계산합니다.</summary>
    public bool TryBuildPath(Vector2 startWorld, Vector2 endWorld, out IReadOnlyList<Vector2> path)
    {
        reusablePath.Clear();
        path = reusablePath;
        lastDebugStart = startWorld;
        lastDebugEnd = endWorld;
        lastDebugSucceeded = false;
        lastDebugPath.Clear();

        Vector2Int startCell = WorldToCell(startWorld);
        Vector2Int endCell = WorldToCell(endWorld);

        if (startCell == endCell)
        {
            reusablePath.Add(CellToWorld(startCell));
            CacheDebugPath(reusablePath, true);
            LogDebug($"경로 생략: 시작 셀과 목표 셀이 같습니다. start={startCell}");
            return true;
        }

        if (!IsWalkable(endCell))
        {
            endCell = FindNearestWalkableCell(endCell, radius: 2);
            if (endCell == startCell && !IsWalkable(endCell))
            {
                LogDebug($"경로 실패: 목표 셀과 인접 셀을 모두 사용할 수 없습니다. requestedEnd={WorldToCell(endWorld)}");
                return false;
            }
        }

        RectInt searchBounds = BuildSearchBounds(startCell, endCell);

        openSet.Clear();
        closedSet.Clear();
        cameFrom.Clear();
        gScore.Clear();

        openSet.Add(startCell);
        gScore[startCell] = 0;

        int visited = 0;
        while (openSet.Count > 0 && visited < maxVisitedNodes)
        {
            visited++;

            int currentIndex = FindBestOpenIndex(endCell);
            Vector2Int current = openSet[currentIndex];

            if (current == endCell)
            {
                ReconstructPath(current);
                path = reusablePath;
                CacheDebugPath(reusablePath, reusablePath.Count > 0);
                LogDebug($"경로 성공: start={startCell}, end={endCell}, waypoints={reusablePath.Count}, visited={visited}");
                return reusablePath.Count > 0;
            }

            openSet.RemoveAt(currentIndex);
            closedSet.Add(current);

            foreach (Vector2Int neighbor in EnumerateNeighbors(current))
            {
                if (!searchBounds.Contains(neighbor))
                    continue;

                if (closedSet.Contains(neighbor))
                    continue;

                if (!IsWalkable(neighbor))
                    continue;

                int tentativeG = gScore[current] + StepCost(current, neighbor);
                if (!gScore.TryGetValue(neighbor, out int existingG) || tentativeG < existingG)
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeG;

                    if (!openSet.Contains(neighbor))
                        openSet.Add(neighbor);
                }
            }
        }

        LogDebug($"경로 실패: open set 소진 또는 방문 제한 초과. start={startCell}, end={endCell}, visited={visited}");
        return false;
    }

    public void RegisterRuntimeGroundTilemap(Tilemap tilemap)
    {
        if (tilemap == null)
            return;

        runtimeGroundTilemaps.Add(tilemap);
    }

    public void UnregisterRuntimeGroundTilemap(Tilemap tilemap)
    {
        if (tilemap == null)
            return;

        runtimeGroundTilemaps.Remove(tilemap);
    }

    /// <summary>
    /// 책임:
    /// 현재 pathfinder의 차단 레이어/탐색 크기 기준으로 두 월드 좌표 사이를 직선 이동할 수 있는지 빠르게 판정한다.
    /// 추적 의도는 이 결과로 열린 공간에서는 직선 추적을 유지하고, 막힌 경우에만 경로 탐색으로 전환한다.
    /// </summary>
    public bool HasDirectWalkableSegment(Vector2 startWorld, Vector2 endWorld)
    {
        Vector2 delta = endWorld - startWorld;
        float distance = delta.magnitude;
        if (distance <= 0.001f)
            return true;

        RaycastHit2D hit = Physics2D.BoxCast(
            startWorld,
            probeSize,
            0f,
            delta / distance,
            distance,
            blockedLayers);

        return hit.collider == null;
    }

    /// <summary>지정한 셀이 막혀 있다면 인접 셀 중 가장 가까운 이동 가능 셀을 찾습니다.</summary>
    private Vector2Int FindNearestWalkableCell(Vector2Int center, int radius)
    {
        if (IsWalkable(center))
            return center;

        for (int r = 1; r <= radius; r++)
        {
            for (int y = -r; y <= r; y++)
            {
                for (int x = -r; x <= r; x++)
                {
                    Vector2Int candidate = new Vector2Int(center.x + x, center.y + y);
                    if (IsWalkable(candidate))
                        return candidate;
                }
            }
        }

        return center;
    }

    /// <summary>현재 셀에서 이동 가능한 이웃 셀들을 순회합니다.</summary>
    private IEnumerable<Vector2Int> EnumerateNeighbors(Vector2Int cell)
    {
        yield return new Vector2Int(cell.x + 1, cell.y);
        yield return new Vector2Int(cell.x - 1, cell.y);
        yield return new Vector2Int(cell.x, cell.y + 1);
        yield return new Vector2Int(cell.x, cell.y - 1);

        if (!allowDiagonal)
            yield break;

        yield return new Vector2Int(cell.x + 1, cell.y + 1);
        yield return new Vector2Int(cell.x + 1, cell.y - 1);
        yield return new Vector2Int(cell.x - 1, cell.y + 1);
        yield return new Vector2Int(cell.x - 1, cell.y - 1);
    }

    /// <summary>현재 열린 셀 목록에서 목표까지 예상 비용이 가장 낮은 셀 인덱스를 찾습니다.</summary>
    private int FindBestOpenIndex(Vector2Int goal)
    {
        int bestIndex = 0;
        int bestScore = int.MaxValue;

        for (int i = 0; i < openSet.Count; i++)
        {
            Vector2Int cell = openSet[i];
            int score = gScore[cell] + Heuristic(cell, goal);
            if (score < bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    /// <summary>완성된 셀 경로를 월드 waypoint 목록으로 복원합니다.</summary>
    private void ReconstructPath(Vector2Int goal)
    {
        reusablePath.Clear();

        Vector2Int current = goal;
        reusablePath.Add(CellToWorld(current));

        while (cameFrom.TryGetValue(current, out Vector2Int previous))
        {
            current = previous;
            reusablePath.Add(CellToWorld(current));
        }

        reusablePath.Reverse();
    }

    /// <summary>타일 탐색 범위를 시작/목표 셀 기준의 여유 직사각형으로 만듭니다.</summary>
    private RectInt BuildSearchBounds(Vector2Int startCell, Vector2Int endCell)
    {
        int minX = Mathf.Min(startCell.x, endCell.x) - maxPaddingCells;
        int maxX = Mathf.Max(startCell.x, endCell.x) + maxPaddingCells;
        int minY = Mathf.Min(startCell.y, endCell.y) - maxPaddingCells;
        int maxY = Mathf.Max(startCell.y, endCell.y) + maxPaddingCells;

        return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    /// <summary>지정한 셀이 이동 가능한 셀인지 판정합니다.</summary>
    private bool IsWalkable(Vector2Int cell)
    {
        if (!HasGroundTile(cell))
            return false;

        Vector2 center = CellToWorld(cell);
        Collider2D blocker = Physics2D.OverlapBox(center, probeSize, 0f, blockedLayers);
        return blocker == null;
    }

    private bool HasGroundTile(Vector2Int cell)
    {
        Vector3Int tileCell = new Vector3Int(cell.x, cell.y, 0);
        bool hasAnyGroundMap = false;

        if (TryGroundTilemapHasTile(groundTilemap, tileCell, ref hasAnyGroundMap))
            return true;

        if (additionalGroundTilemaps != null)
        {
            for (int i = 0; i < additionalGroundTilemaps.Length; i++)
            {
                if (TryGroundTilemapHasTile(additionalGroundTilemaps[i], tileCell, ref hasAnyGroundMap))
                    return true;
            }
        }

        foreach (Tilemap tilemap in runtimeGroundTilemaps)
        {
            if (TryGroundTilemapHasTile(tilemap, tileCell, ref hasAnyGroundMap))
                return true;
        }

        return !hasAnyGroundMap;
    }

    private static bool TryGroundTilemapHasTile(Tilemap tilemap, Vector3Int cell, ref bool hasAnyGroundMap)
    {
        if (!IsUsableGroundTilemap(tilemap))
            return false;

        hasAnyGroundMap = true;
        return tilemap.HasTile(cell);
    }

    private static bool IsUsableGroundTilemap(Tilemap tilemap)
    {
        return tilemap != null && tilemap.isActiveAndEnabled && tilemap.gameObject.activeInHierarchy;
    }

    /// <summary>월드 좌표를 pathfinding 셀 좌표로 변환합니다.</summary>
    private Vector2Int WorldToCell(Vector2 worldPosition)
    {
        if (grid != null)
            return (Vector2Int)grid.WorldToCell(worldPosition);

        return new Vector2Int(
            Mathf.RoundToInt(worldPosition.x),
            Mathf.RoundToInt(worldPosition.y));
    }

    /// <summary>pathfinding 셀 좌표를 셀 중심 월드 좌표로 변환합니다.</summary>
    private Vector2 CellToWorld(Vector2Int cell)
    {
        if (grid != null)
        {
            Vector3 cellCenter = grid.GetCellCenterWorld((Vector3Int)cell);
            return new Vector2(cellCenter.x, cellCenter.y);
        }

        return new Vector2(cell.x, cell.y);
    }

    /// <summary>휴리스틱 비용을 계산합니다.</summary>
    private int Heuristic(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        return allowDiagonal ? Mathf.Max(dx, dy) * 10 : (dx + dy) * 10;
    }

    /// <summary>이웃 셀로 이동하는 기본 비용을 계산합니다.</summary>
    private int StepCost(Vector2Int current, Vector2Int neighbor)
    {
        bool diagonal = current.x != neighbor.x && current.y != neighbor.y;
        return diagonal ? 14 : 10;
    }

    /// <summary>마지막 경로 계산 결과를 디버그 표시용으로 저장합니다.</summary>
    private void CacheDebugPath(List<Vector2> path, bool success)
    {
        lastDebugSucceeded = success;
        lastDebugPath.Clear();
        for (int i = 0; i < path.Count; i++)
            lastDebugPath.Add(path[i]);
    }

    /// <summary>길찾기 동작을 로그로 남깁니다.</summary>
    private void LogDebug(string message)
    {
        if (!enableDebugLog)
            return;

        Debug.Log($"[TilemapPathfinder2D] {name}: {message}", this);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawLastPathGizmo)
            return;

        Gizmos.color = lastDebugSucceeded ? Color.cyan : Color.red;
        Gizmos.DrawSphere(lastDebugStart, 0.12f);
        Gizmos.DrawSphere(lastDebugEnd, 0.12f);

        if (lastDebugPath.Count == 0)
        {
            Gizmos.DrawLine(lastDebugStart, lastDebugEnd);
            return;
        }

        for (int i = 0; i < lastDebugPath.Count; i++)
        {
            Vector2 waypoint = lastDebugPath[i];
            Gizmos.DrawSphere(waypoint, 0.08f);

            if (i == 0)
            {
                Gizmos.DrawLine(lastDebugStart, waypoint);
                continue;
            }

            Gizmos.DrawLine(lastDebugPath[i - 1], waypoint);
        }

        Gizmos.DrawLine(lastDebugPath[lastDebugPath.Count - 1], lastDebugEnd);
    }
}
