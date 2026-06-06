using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 책임:
/// - 지정된 타일맵의 연결된 전경/기둥 타일 그룹을 캐싱한다.
/// - 감지 대상 collider가 그룹 셀과 겹칠 때 해당 그룹을 부드럽게 투명화하고, 벗어나면 원래 색으로 복구한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class TilemapOcclusionFader2D : MonoBehaviour
{
    [Header("Tilemaps")]
    [SerializeField] private Tilemap[] targetTilemaps;

    [Header("Detection")]
    [SerializeField] private LayerMask detectionLayers;
    [SerializeField] private bool includeTriggers;
    [SerializeField] private Vector2 sampleOffset;
    [SerializeField, Min(0.02f)] private float checkInterval = 0.08f;
    [SerializeField, Min(1)] private int maxDetectedColliders = 32;

    [Header("Fade")]
    [SerializeField, Range(0f, 1f)] private float fadedAlpha = 0.35f;
    [SerializeField, Min(0f)] private float fadeDuration = 0.12f;

    [Header("Debug")]
    [SerializeField] private bool drawDetectionBounds;

    private readonly List<TilemapState> tilemapStates = new();
    private readonly HashSet<TileGroup> activeGroups = new();

    private Collider2D[] runtimeColliderBuffer;
    private ContactFilter2D detectionFilter;
    private float nextCheckTime;
    private bool hasBuiltCache;

    private static readonly Vector3Int[] CardinalDirections =
    {
        new(1, 0, 0),
        new(-1, 0, 0),
        new(0, 1, 0),
        new(0, -1, 0),
    };

    private void Awake()
    {
        RebuildCache();
    }

    private void OnEnable()
    {
        if (!hasBuiltCache)
            RebuildCache();

        nextCheckTime = 0f;
    }

    private void OnDisable()
    {
        RestoreAllGroups();
    }

    private void OnDestroy()
    {
        RestoreAllGroups();
    }

    private void Update()
    {
        if (!hasBuiltCache)
            RebuildCache();

        if (Time.time >= nextCheckTime)
        {
            nextCheckTime = Time.time + Mathf.Max(0.02f, checkInterval);
            RefreshActiveGroups();
        }

        UpdateGroupFade(Time.deltaTime);
    }

    /// <summary>타일맵 타일 구성이 런타임에 바뀐 경우 연결 그룹 캐시를 다시 만듭니다.</summary>
    public void RebuildCache()
    {
        RestoreAllGroups();
        activeGroups.Clear();
        tilemapStates.Clear();

        List<Tilemap> tilemaps = ResolveTilemaps();
        foreach (Tilemap tilemap in tilemaps)
        {
            TilemapState state = BuildTilemapState(tilemap);
            if (state.Groups.Count > 0)
                tilemapStates.Add(state);
        }

        runtimeColliderBuffer = new Collider2D[Mathf.Max(1, maxDetectedColliders)];

        detectionFilter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = detectionLayers,
            useTriggers = includeTriggers,
        };

        hasBuiltCache = true;
    }

    private List<Tilemap> ResolveTilemaps()
    {
        List<Tilemap> resolved = new();
        if (targetTilemaps != null)
        {
            for (int i = 0; i < targetTilemaps.Length; i++)
            {
                Tilemap tilemap = targetTilemaps[i];
                if (tilemap != null && !resolved.Contains(tilemap))
                    resolved.Add(tilemap);
            }
        }

        if (resolved.Count > 0)
            return resolved;

        Tilemap ownTilemap = GetComponent<Tilemap>();
        if (ownTilemap != null)
            resolved.Add(ownTilemap);

        Tilemap[] childTilemaps = GetComponentsInChildren<Tilemap>(true);
        for (int i = 0; i < childTilemaps.Length; i++)
        {
            Tilemap tilemap = childTilemaps[i];
            if (tilemap != null && !resolved.Contains(tilemap))
                resolved.Add(tilemap);
        }

        return resolved;
    }

    private static TilemapState BuildTilemapState(Tilemap tilemap)
    {
        TilemapState state = new(tilemap);
        BoundsInt bounds = tilemap.cellBounds;
        HashSet<Vector3Int> visited = new();
        Queue<Vector3Int> queue = new();

        foreach (Vector3Int startCell in bounds.allPositionsWithin)
        {
            if (visited.Contains(startCell) || !tilemap.HasTile(startCell))
                continue;

            TileGroup group = new(state);
            queue.Enqueue(startCell);
            visited.Add(startCell);

            while (queue.Count > 0)
            {
                Vector3Int cell = queue.Dequeue();
                group.Cells.Add(new TileCellState(cell, tilemap.GetColor(cell), tilemap.GetTileFlags(cell)));
                state.CellToGroup[cell] = group;

                for (int i = 0; i < CardinalDirections.Length; i++)
                {
                    Vector3Int next = cell + CardinalDirections[i];
                    if (visited.Contains(next) || !bounds.Contains(next) || !tilemap.HasTile(next))
                        continue;

                    visited.Add(next);
                    queue.Enqueue(next);
                }
            }

            if (group.Cells.Count > 0)
                state.Groups.Add(group);
        }

        state.WorldBounds = CalculateWorldBounds(tilemap, bounds);
        return state;
    }

    private static Bounds CalculateWorldBounds(Tilemap tilemap, BoundsInt cellBounds)
    {
        Vector3 min = tilemap.CellToWorld(new Vector3Int(cellBounds.xMin, cellBounds.yMin, cellBounds.zMin));
        Vector3 max = tilemap.CellToWorld(new Vector3Int(cellBounds.xMax, cellBounds.yMax, cellBounds.zMax));
        Bounds bounds = new();
        bounds.SetMinMax(Vector3.Min(min, max), Vector3.Max(min, max));

        if (bounds.size.x <= 0.01f || bounds.size.y <= 0.01f)
        {
            Bounds localBounds = tilemap.localBounds;
            Vector3 center = tilemap.transform.TransformPoint(localBounds.center);
            Vector3 size = Vector3.Scale(localBounds.size, tilemap.transform.lossyScale);
            bounds = new Bounds(center, new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z)));
        }

        return bounds;
    }

    private void RefreshActiveGroups()
    {
        activeGroups.Clear();

        if (detectionLayers.value == 0 || runtimeColliderBuffer == null)
        {
            ApplyActiveGroupTargets();
            return;
        }

        detectionFilter.layerMask = detectionLayers;
        detectionFilter.useTriggers = includeTriggers;

        for (int i = 0; i < tilemapStates.Count; i++)
        {
            TilemapState state = tilemapStates[i];
            Bounds bounds = state.WorldBounds;
            int count = Physics2D.OverlapBox(bounds.center, bounds.size, 0f, detectionFilter, runtimeColliderBuffer);

            for (int j = 0; j < count; j++)
            {
                Collider2D detected = runtimeColliderBuffer[j];
                if (detected == null)
                    continue;

                Vector3 samplePoint = detected.bounds.center + (Vector3)sampleOffset;
                Vector3Int cell = state.Tilemap.WorldToCell(samplePoint);
                if (state.CellToGroup.TryGetValue(cell, out TileGroup group))
                    activeGroups.Add(group);
            }
        }

        ApplyActiveGroupTargets();
    }

    private void ApplyActiveGroupTargets()
    {
        for (int i = 0; i < tilemapStates.Count; i++)
        {
            List<TileGroup> groups = tilemapStates[i].Groups;
            for (int j = 0; j < groups.Count; j++)
            {
                TileGroup group = groups[j];
                group.TargetAlpha = activeGroups.Contains(group) ? fadedAlpha : 1f;
            }
        }
    }

    private void UpdateGroupFade(float deltaTime)
    {
        float speed = fadeDuration <= 0f ? float.PositiveInfinity : 1f / fadeDuration;

        for (int i = 0; i < tilemapStates.Count; i++)
        {
            List<TileGroup> groups = tilemapStates[i].Groups;
            for (int j = 0; j < groups.Count; j++)
            {
                TileGroup group = groups[j];
                float nextAlpha = fadeDuration <= 0f
                    ? group.TargetAlpha
                    : Mathf.MoveTowards(group.CurrentAlpha, group.TargetAlpha, speed * deltaTime);

                if (Mathf.Approximately(nextAlpha, group.CurrentAlpha))
                    continue;

                group.CurrentAlpha = nextAlpha;
                ApplyGroupAlpha(group);
            }
        }
    }

    private static void ApplyGroupAlpha(TileGroup group)
    {
        Tilemap tilemap = group.Owner.Tilemap;
        for (int i = 0; i < group.Cells.Count; i++)
        {
            TileCellState cell = group.Cells[i];
            Color color = cell.OriginalColor;
            color.a *= group.CurrentAlpha;
            tilemap.SetTileFlags(cell.Cell, TileFlags.None);
            tilemap.SetColor(cell.Cell, color);
        }
    }

    private void RestoreAllGroups()
    {
        for (int i = 0; i < tilemapStates.Count; i++)
        {
            List<TileGroup> groups = tilemapStates[i].Groups;
            for (int j = 0; j < groups.Count; j++)
                RestoreGroup(groups[j]);
        }
    }

    private static void RestoreGroup(TileGroup group)
    {
        Tilemap tilemap = group.Owner.Tilemap;
        if (tilemap == null)
            return;

        for (int i = 0; i < group.Cells.Count; i++)
        {
            TileCellState cell = group.Cells[i];
            tilemap.SetTileFlags(cell.Cell, TileFlags.None);
            tilemap.SetColor(cell.Cell, cell.OriginalColor);
            tilemap.SetTileFlags(cell.Cell, cell.OriginalFlags);
        }

        group.CurrentAlpha = 1f;
        group.TargetAlpha = 1f;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDetectionBounds)
            return;

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
        for (int i = 0; i < tilemapStates.Count; i++)
            Gizmos.DrawWireCube(tilemapStates[i].WorldBounds.center, tilemapStates[i].WorldBounds.size);
    }

    /// <summary>
    /// 책임:
    /// - 하나의 Tilemap에 대해 셀에서 연결 그룹을 빠르게 찾기 위한 런타임 캐시를 보관한다.
    /// </summary>
    private sealed class TilemapState
    {
        public TilemapState(Tilemap tilemap)
        {
            Tilemap = tilemap;
        }

        public readonly Tilemap Tilemap;
        public readonly Dictionary<Vector3Int, TileGroup> CellToGroup = new();
        public readonly List<TileGroup> Groups = new();
        public Bounds WorldBounds;
    }

    /// <summary>
    /// 책임:
    /// - 상하좌우로 연결된 타일 묶음과 현재/목표 투명도 상태를 보관한다.
    /// </summary>
    private sealed class TileGroup
    {
        public TileGroup(TilemapState owner)
        {
            Owner = owner;
        }

        public readonly TilemapState Owner;
        public readonly List<TileCellState> Cells = new();
        public float CurrentAlpha = 1f;
        public float TargetAlpha = 1f;
    }

    /// <summary>
    /// 책임:
    /// - 투명화 적용 전의 타일 셀 색상과 플래그를 복구할 수 있도록 저장한다.
    /// </summary>
    private readonly struct TileCellState
    {
        public TileCellState(Vector3Int cell, Color originalColor, TileFlags originalFlags)
        {
            Cell = cell;
            OriginalColor = originalColor;
            OriginalFlags = originalFlags;
        }

        public readonly Vector3Int Cell;
        public readonly Color OriginalColor;
        public readonly TileFlags OriginalFlags;
    }
}
