using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>Realizes optional return portals after layout/object construction and owns their generated lifetime and reveal snapshots.</summary>
public sealed partial class DungeonRoomBuilder
{
    [Header("Dead End Return Portals")]
    [SerializeField] private DungeonReturnPortal returnPortalPrefab;
    [SerializeField] private DungeonReturnTravel returnTravelPrefab;
    [SerializeField, Min(0.1f)] private float returnPortalClearance = 0.35f;
    private Transform generatedReturnRoot;
    private DungeonReturnTravel returnTravel;
    private readonly List<DungeonReturnPortal> returnPortals = new();
    private readonly Collider2D[] returnOverlapBuffer = new Collider2D[64];
    private readonly HashSet<Vector2Int> returnLandingCells = new();
    private const string ReturnStatePrefix = "return-portal:";
    private static readonly Vector2[] ReturnFootprintDirections = { Vector2.up, Vector2.right, Vector2.down, Vector2.left };
    public IReadOnlyList<DungeonReturnPortal> GeneratedReturnPortals => returnPortals;
    public DungeonReturnTravel ReturnTravel => returnTravel;

    private bool TryBuildReturnPortals(DungeonLayoutResult layout)
    {
        // Legacy scenes and visual-only previews do not acquire a new runtime dependency.
        if (returnPortalPrefab == null || returnTravelPrefab == null) return true;
        DungeonRoomPlacement start = null;
        foreach (DungeonRoomPlacement room in layout.Rooms)
            if (room.Template.LayoutData.roomType == RoomType.Start) { start = room; break; }
        if (start == null) return true;
        Physics2D.SyncTransforms();
        List<Vector2Int> startCells = FindReturnReachableCells(layout, start, out _);
        if (startCells.Count == 0)
        {
            Debug.LogWarning("[ReturnPortal] No reachable Start room landing floor; returns disabled for this build.", this);
            return true;
        }
        returnLandingCells.UnionWith(startCells);
        Vector3 desired = floorTilemap.GetCellCenterWorld((Vector3Int)Vector2Int.FloorToInt(start.WorldBounds.center));
        Transform overrideLanding = FindReturnAnchor(start.PlacementId, "ReturnLanding");
        if (overrideLanding != null) desired = overrideLanding.position;
        bool found = false;
        float distance = float.PositiveInfinity;
        Vector3 landing = default;
        foreach (Vector2Int cell in startCells)
        {
            Vector3 position = floorTilemap.GetCellCenterWorld((Vector3Int)cell);
            if (!ValidateReturnLanding(position, returnPortalClearance, null)) continue;
            float sqr = (position - desired).sqrMagnitude;
            if (sqr >= distance) continue;
            found = true; landing = position; distance = sqr;
        }
        if (overrideLanding != null && ValidateReturnLanding(desired, returnPortalClearance, null))
        { landing = desired; found = true; }
        if (!found)
        {
            Debug.LogWarning("[ReturnPortal] Start room has no clear landing footprint; returns disabled for this build.", this);
            return true;
        }
        var root = new GameObject("GeneratedReturnPortals");
        root.transform.SetParent(transform, false);
        generatedReturnRoot = root.transform;
        returnTravel = Instantiate(returnTravelPrefab, generatedReturnRoot);
        returnTravel.Configure(landing, start.PlacementId, GetComponent<DungeonMapRuntimeController>(), ValidateReturnLanding);
        foreach (DungeonRoomPlacement room in layout.Rooms)
        {
            if (room == start || !DungeonReturnPortalPlacement.TryGetOnlyConnection(layout, room.PlacementId, out int socketIndex)) continue;
            RoomSocketData socket = room.Template.LayoutData.sockets[socketIndex];
            RoomSocketDirection wallDirection = DungeonReturnPortalPlacement.Opposite(socket.direction);
            List<Vector2Int> reachable = FindReturnReachableCells(layout, room, out Vector2Int entrance);
            Transform explicitAnchor = FindReturnAnchor(room.PlacementId, "ReturnPortal_" + wallDirection);
            Vector3 portalPosition;
            if (explicitAnchor != null && reachable.Contains((Vector2Int)floorTilemap.WorldToCell(explicitAnchor.position)) &&
                IsReturnSpaceClear(explicitAnchor.position, returnPortalClearance, null, false))
            {
                portalPosition = explicitAnchor.position;
            }
            else if (DungeonReturnPortalPlacement.TryChooseOppositeWall(reachable, entrance, socket.direction,
                c => !HasReturnFloor(c), c => IsReturnSpaceClear(floorTilemap.GetCellCenterWorld((Vector3Int)c),
                    returnPortalClearance, null, false), out Vector2Int chosen))
            {
                portalPosition = floorTilemap.GetCellCenterWorld((Vector3Int)chosen);
            }
            else
            {
                Debug.LogWarning($"[ReturnPortal] {room.Template.name}: no safe opposite-wall position. Add ReturnPortal_{wallDirection} anchor.", this);
                continue;
            }
            DungeonReturnPortal portal = Instantiate(returnPortalPrefab, portalPosition, floorTilemap.transform.rotation, generatedReturnRoot);
            portal.name = $"ReturnPortal_{room.PlacementId}_{wallDirection}";
            generatedRoomGroupsByPlacement.TryGetValue(room.PlacementId, out MonsterSpawnRoomGroup group);
            bool onEntry = room.Template.LayoutData.roomType == RoomType.Event || group == null;
            portal.Configure(returnTravel, room.PlacementId, group, onEntry, wallDirection);
            returnPortals.Add(portal);
        }
        return true;
    }

    private List<Vector2Int> FindReturnReachableCells(DungeonLayoutResult layout, DungeonRoomPlacement room, out Vector2Int entrance)
    {
        entrance = default;
        foreach (DungeonSocketConnection connection in layout.Connections)
        {
            int index = connection.FirstRoomPlacementId == room.PlacementId ? connection.FirstSocketIndex :
                connection.SecondRoomPlacementId == room.PlacementId ? connection.SecondSocketIndex : -1;
            if (index < 0) continue;
            RoomSocketData socket = room.Template.LayoutData.sockets[index];
            // Connected socket cells may contain a Door; begin just inside its threshold.
            for (int depth = 1; depth <= 2; depth++)
                for (int width = 0; width < RoomSocketGeometry.ResolveWidth(socket); width++)
                {
                    Vector2Int candidate = room.Origin + RoomSocketGeometry.GetLocalCell(socket, width) -
                        DungeonReturnPortalPlacement.Direction(socket.direction) * depth;
                    if (!room.WorldBounds.Contains(candidate) || !CanTraverseReturnCell(candidate)) continue;
                    entrance = candidate;
                    return DungeonReturnPortalPlacement.Reachable(room.WorldBounds, entrance, CanTraverseReturnCell);
                }
            break;
        }
        return new List<Vector2Int>();
    }

    private bool HasReturnFloor(Vector2Int cell)
    {
        Vector3Int tileCell = (Vector3Int)cell;
        return floorTilemap.HasTile(tileCell) &&
            (wallTilemap == null || wallTilemap.GetColliderType(tileCell) == Tile.ColliderType.None);
    }

    private bool CanTraverseReturnCell(Vector2Int cell) => HasReturnFloor(cell) &&
        IsReturnSpaceClear(floorTilemap.GetCellCenterWorld((Vector3Int)cell), 0.15f, null, true);

    private bool ValidateReturnLanding(Vector3 position, float radius, Transform ignoredPlayer)
    {
        if (floorTilemap == null || !returnLandingCells.Contains((Vector2Int)floorTilemap.WorldToCell(position))) return false;
        // Include the full conservative player footprint, not only the center tile.
        foreach (Vector2 offset in ReturnFootprintDirections)
            if (!HasReturnFloor((Vector2Int)floorTilemap.WorldToCell(position + (Vector3)(offset * radius)))) return false;
        return IsReturnSpaceClear(position, radius, ignoredPlayer, false);
    }

    private bool IsReturnSpaceClear(Vector3 position, float radius, Transform ignoredPlayer, bool traversal)
    {
        int count = Physics2D.OverlapCircle(position, Mathf.Max(0.1f, radius),
            new ContactFilter2D { useTriggers = true }, returnOverlapBuffer);
        if (count >= returnOverlapBuffer.Length) return false;
        for (int i = 0; i < count; i++)
        {
            Collider2D collider = returnOverlapBuffer[i];
            if (collider == null || collider.gameObject.scene != gameObject.scene ||
                (ignoredPlayer != null && collider.transform.IsChildOf(ignoredPlayer))) continue;
            if (collider.GetComponentInParent<HoleTrap>() != null) return false;
            if (traversal && collider.GetComponentInParent<DoorObject>() != null) continue;
            if (!collider.isTrigger || (!traversal && collider.GetComponentInParent<InteractableBase>() != null)) return false;
        }
        return true;
    }

    private Transform FindReturnAnchor(int roomId, string slotId)
    {
        if (!generatedRoomObjectsByPlacement.TryGetValue(roomId, out List<GameObject> objects)) return null;
        foreach (GameObject instance in objects)
            if (instance != null)
                foreach (ProceduralRoomAnchor anchor in instance.GetComponentsInChildren<ProceduralRoomAnchor>(true))
                    if (anchor.SlotId == slotId) return anchor.Target;
        return null;
    }

    private void NotifyReturnRoomEntered(int roomId)
    {
        foreach (DungeonReturnPortal portal in returnPortals)
            if (portal != null) portal.NotifyRoomEntered(roomId);
    }

    private void CaptureReturnPortalStates(List<DungeonObjectRuntimeStateData> states)
    {
        foreach (DungeonReturnPortal portal in returnPortals)
            if (portal != null) states.Add(new DungeonObjectRuntimeStateData
            { stateId = ReturnStatePrefix + portal.RoomPlacementId, isPresent = true, isActive = portal.IsRevealed });
    }

    private bool RestoreReturnPortalState(DungeonObjectRuntimeStateData state)
    {
        if (state?.stateId == null || !state.stateId.StartsWith(ReturnStatePrefix, System.StringComparison.Ordinal)) return false;
        if (int.TryParse(state.stateId.Substring(ReturnStatePrefix.Length), out int id))
            foreach (DungeonReturnPortal portal in returnPortals)
                if (portal != null && portal.RoomPlacementId == id) portal.RestoreRevealed(state.isActive);
        return true;
    }

    private void ClearReturnPortals()
    {
        if (returnTravel != null) returnTravel.Cancel();
        returnTravel = null;
        returnPortals.Clear();
        returnLandingCells.Clear();
        if (generatedReturnRoot == null) return;
        generatedReturnRoot.gameObject.SetActive(false);
        if (Application.isPlaying) Destroy(generatedReturnRoot.gameObject);
        else DestroyImmediate(generatedReturnRoot.gameObject);
        generatedReturnRoot = null;
    }

#if UNITY_EDITOR
    public void EditorConfigureReturnPortals(DungeonReturnPortal portal, DungeonReturnTravel travel)
    { returnPortalPrefab = portal; returnTravelPrefab = travel; }
#endif
}
