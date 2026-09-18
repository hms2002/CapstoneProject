using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>Realizes optional return portals after layout/object construction and owns their generated lifetime and reveal snapshots.</summary>
public sealed partial class DungeonRoomBuilder
{
    [Header("Dead End Return Portals")]
    [Tooltip("Prefer safe reachable floor nearest the room center. Authored portal guides still take priority. Disable for legacy opposite-wall placement.")]
    [SerializeField] private bool preferReturnPortalRoomCenter = true;
    [SerializeField] private DungeonReturnPortal returnPortalPrefab;
    [SerializeField] private DungeonReturnTravel returnTravelPrefab;
    [SerializeField] private DungeonReturnPortal bossShortcutPrefab;
    [SerializeField] private DungeonReturnTravel bossShortcutTravelPrefab;
    [SerializeField, Min(0.1f)] private float returnPortalClearance = 0.35f;
    private Transform generatedReturnRoot;
    private DungeonReturnTravel returnTravel;
    private DungeonReturnPortal bossShortcut;
    private DungeonReturnTravel bossShortcutTravel;
    private readonly HashSet<Vector2Int> bossLandingCells = new();
    private const string BossShortcutStateId = "boss-shortcut";
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
            CapstoneDiagnostics.EditorOnlyLog.LogWarning("[ReturnPortal] No reachable Start room landing floor; returns disabled for this build.", this);
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
            CapstoneDiagnostics.EditorOnlyLog.LogWarning("[ReturnPortal] Start room has no clear landing footprint; returns disabled for this build.", this);
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
            // A guide can explicitly choose a different wall orientation from the entrance.
            if (explicitAnchor == null)
                for (int i = 0; i < 4; i++)
                {
                    var direction = (RoomSocketDirection)i;
                    explicitAnchor = FindReturnAnchor(room.PlacementId, "ReturnPortal_" + direction);
                    if (explicitAnchor == null) continue;
                    wallDirection = direction;
                    break;
                }
            Vector3 portalPosition;
            if (explicitAnchor != null && reachable.Contains((Vector2Int)floorTilemap.WorldToCell(explicitAnchor.position)) &&
                IsReturnPortalFloorClear(explicitAnchor.position, room.WorldBounds, wallDirection))
            {
                portalPosition = explicitAnchor.position;
            }
            else if (TryChooseReturnPortalCell(room, reachable, entrance, socket.direction, out Vector2Int chosen))
            {
                if (explicitAnchor != null)
                    CapstoneDiagnostics.EditorOnlyLog.LogWarning($"[ReturnPortal] {room.Template.name}: guide '{explicitAnchor.name}' is unreachable or obstructed; using automatic safe placement.", explicitAnchor);
                wallDirection = DungeonReturnPortalPlacement.Opposite(socket.direction);
                portalPosition = floorTilemap.GetCellCenterWorld((Vector3Int)chosen);
            }
            else
            {
                CapstoneDiagnostics.EditorOnlyLog.LogWarning($"[ReturnPortal] {room.Template.name}: no safe reachable portal position; portal skipped. Check floor, obstacles and ReturnPortal_{wallDirection} guide.", this);
                continue;
            }
            DungeonReturnPortal portal = Instantiate(returnPortalPrefab, portalPosition, floorTilemap.transform.rotation, generatedReturnRoot);
            portal.name = $"ReturnPortal_{room.PlacementId}_{wallDirection}";
            generatedRoomGroupsByPlacement.TryGetValue(room.PlacementId, out MonsterSpawnRoomGroup group);
            bool onEntry = room.Template.LayoutData.roomType == RoomType.Event || group == null;
            portal.Configure(returnTravel, room.PlacementId, group, onEntry, wallDirection);
            returnPortals.Add(portal);
        }
        TryBuildBossShortcut(layout, start, startCells, landing);
        return true;
    }

    private bool TryChooseReturnPortalCell(DungeonRoomPlacement room, List<Vector2Int> reachable,
        Vector2Int entrance, RoomSocketDirection entranceDirection, out Vector2Int chosen)
    {
        bool CanPlace(Vector2Int cell) => IsReturnPortalFloorClear(
            floorTilemap.GetCellCenterWorld((Vector3Int)cell), room.WorldBounds,
            DungeonReturnPortalPlacement.Opposite(entranceDirection));
        return preferReturnPortalRoomCenter
            ? DungeonReturnPortalPlacement.TryChooseCenter(reachable, room.WorldBounds.center, CanPlace, out chosen)
            : DungeonReturnPortalPlacement.TryChooseOppositeWall(reachable, entrance, entranceDirection,
                c => !HasReturnFloor(c), CanPlace, out chosen);
    }

    private bool IsReturnPortalFloorClear(Vector3 position, RectInt roomBounds, RoomSocketDirection direction)
    {
        // Validate the entire conservative footprint, including corners near pit edges.
        // Stay inside this room even when another room's floor touches its bounds.
        for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
            {
                Vector2Int cell = (Vector2Int)floorTilemap.WorldToCell(position +
                    new Vector3(x * returnPortalClearance, y * returnPortalClearance));
                if (!roomBounds.Contains(cell) || !HasReturnFloor(cell)) return false;
            }
        if (!IsReturnSpaceClear(position, returnPortalClearance, null, false)) return false;

        // The interaction capsule is wider than the safe standing footprint.
        // Keep it away from chests/NPCs/bells to avoid competing F-key targets.
        var capsule = returnPortalPrefab.GetComponent<CapsuleCollider2D>();
        if (capsule == null) return true;
        float length = Mathf.Max(capsule.size.x, capsule.size.y);
        float thickness = Mathf.Min(capsule.size.x, capsule.size.y);
        bool horizontal = direction == RoomSocketDirection.Up || direction == RoomSocketDirection.Down;
        Vector2 size = horizontal ? new Vector2(length, thickness) : new Vector2(thickness, length);
        Vector3 scale3 = Vector3.Scale(transform.lossyScale, returnPortalPrefab.transform.localScale);
        Vector2 scale = new(Mathf.Abs(scale3.x), Mathf.Abs(scale3.y));
        Quaternion rotation = floorTilemap.transform.rotation;
        Vector3 center = position + rotation * (Vector3)Vector2.Scale(capsule.offset, scale);
        int count = Physics2D.OverlapBox(center, Vector2.Scale(size, scale), rotation.eulerAngles.z,
            new ContactFilter2D { useTriggers = true }, returnOverlapBuffer);
        if (count >= returnOverlapBuffer.Length) return false;
        for (int i = 0; i < count; i++)
        {
            Collider2D collider = returnOverlapBuffer[i];
            if (collider == null || collider.gameObject.scene != gameObject.scene) continue;
            if (collider.GetComponentInParent<InteractableBase>() != null) return false;
        }
        return true;
    }

    /// <summary>Creates a Start-to-Boss shortcut with independent destination validation and visit-based unlocking.</summary>
    private void TryBuildBossShortcut(DungeonLayoutResult layout, DungeonRoomPlacement start,
        List<Vector2Int> startCells, Vector3 fallbackPosition)
    {
        if (bossShortcutPrefab == null) return;
        DungeonRoomPlacement boss = null;
        foreach (DungeonRoomPlacement room in layout.Rooms)
            if (room.Template.LayoutData.roomType == RoomType.Boss) { boss = room; break; }
        if (boss == null) return;
        List<Vector2Int> reachable = FindReturnReachableCells(layout, boss, out _);
        bossLandingCells.UnionWith(reachable);
        Vector3 desired = floorTilemap.GetCellCenterWorld((Vector3Int)Vector2Int.FloorToInt(boss.WorldBounds.center));
        if (!TryChooseShortcutFloor(reachable, desired, out Vector3 destination))
        {
            CapstoneDiagnostics.EditorOnlyLog.LogWarning("[BossShortcut] No safe reachable floor in Boss room; shortcut skipped.", this);
            return;
        }
        Vector3 center = floorTilemap.GetCellCenterWorld((Vector3Int)Vector2Int.FloorToInt(start.WorldBounds.center));
        Vector3 portalPosition = TryChooseShortcutFloor(startCells, center, out Vector3 chosen) ? chosen : fallbackPosition;
        bossShortcutTravel = Instantiate(bossShortcutTravelPrefab != null ? bossShortcutTravelPrefab : returnTravelPrefab, generatedReturnRoot);
        bossShortcutTravel.name = "BossShortcutTravel";
        bossShortcutTravel.Configure(destination, boss.PlacementId, GetComponent<DungeonMapRuntimeController>(), ValidateBossLanding);
        bossShortcut = Instantiate(bossShortcutPrefab, portalPosition, floorTilemap.transform.rotation, generatedReturnRoot);
        bossShortcut.name = "StartToBossPortal";
        bossShortcut.Configure(bossShortcutTravel, boss.PlacementId, null, true, RoomSocketDirection.Up);
    }

    private bool TryChooseShortcutFloor(List<Vector2Int> cells, Vector3 desired, out Vector3 chosen)
    {
        chosen = default;
        float best = float.PositiveInfinity;
        foreach (Vector2Int cell in cells)
        {
            Vector3 position = floorTilemap.GetCellCenterWorld((Vector3Int)cell);
            if (!IsReturnSpaceClear(position, returnPortalClearance, null, false)) continue;
            bool floor = true;
            foreach (Vector2 offset in ReturnFootprintDirections)
                if (!HasReturnFloor((Vector2Int)floorTilemap.WorldToCell(position + (Vector3)(offset * returnPortalClearance))))
                { floor = false; break; }
            if (!floor) continue;
            float distance = (position - desired).sqrMagnitude;
            if (distance >= best) continue;
            best = distance;
            chosen = position;
        }
        return !float.IsPositiveInfinity(best);
    }

    private bool ValidateBossLanding(Vector3 position, float radius, Transform ignoredPlayer)
    {
        if (floorTilemap == null || !bossLandingCells.Contains((Vector2Int)floorTilemap.WorldToCell(position))) return false;
        foreach (Vector2 offset in ReturnFootprintDirections)
            if (!HasReturnFloor((Vector2Int)floorTilemap.WorldToCell(position + (Vector3)(offset * radius)))) return false;
        return IsReturnSpaceClear(position, radius, ignoredPlayer, false);
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
            (holeTilemap == null || !holeTilemap.HasTile(tileCell)) &&
            (wallTilemap == null || wallTilemap.GetColliderType(tileCell) == Tile.ColliderType.None);
    }

    private bool CanTraverseReturnCell(Vector2Int cell) => HasReturnFloor(cell) &&
        IsReturnSpaceClear(floorTilemap.GetCellCenterWorld((Vector3Int)cell), returnPortalClearance, null, true);

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
            // Interaction triggers do not obstruct a landing; the Start shortcut can share the return landing floor.
            if (collider.isTrigger && collider.GetComponentInParent<DungeonReturnPortal>() != null) continue;
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
        if (bossShortcut != null) bossShortcut.NotifyRoomEntered(roomId);
        foreach (DungeonReturnPortal portal in returnPortals)
            if (portal != null) portal.NotifyRoomEntered(roomId);
    }

    private void CaptureReturnPortalStates(List<DungeonObjectRuntimeStateData> states)
    {
        if (bossShortcut != null) states.Add(new DungeonObjectRuntimeStateData
        { stateId = BossShortcutStateId, isPresent = true, isActive = bossShortcut.IsRevealed });
        foreach (DungeonReturnPortal portal in returnPortals)
            if (portal != null) states.Add(new DungeonObjectRuntimeStateData
            { stateId = ReturnStatePrefix + portal.RoomPlacementId, isPresent = true, isActive = portal.IsRevealed });
    }

    private bool RestoreReturnPortalState(DungeonObjectRuntimeStateData state)
    {
        if (state?.stateId == BossShortcutStateId)
        {
            if (bossShortcut != null) bossShortcut.RestoreRevealed(state.isActive);
            return true;
        }
        if (state?.stateId == null || !state.stateId.StartsWith(ReturnStatePrefix, System.StringComparison.Ordinal)) return false;
        if (int.TryParse(state.stateId.Substring(ReturnStatePrefix.Length), out int id))
            foreach (DungeonReturnPortal portal in returnPortals)
                if (portal != null && portal.RoomPlacementId == id) portal.RestoreRevealed(state.isActive);
        return true;
    }

    private void ClearReturnPortals()
    {
        if (bossShortcutTravel != null) bossShortcutTravel.Cancel();
        bossShortcutTravel = null;
        bossShortcut = null;
        bossLandingCells.Clear();
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
    public void EditorConfigureBossShortcut(DungeonReturnPortal portal, DungeonReturnTravel travel = null)
    { bossShortcutPrefab = portal; bossShortcutTravelPrefab = travel; }
    public void EditorConfigureReturnPortals(DungeonReturnPortal portal, DungeonReturnTravel travel)
    { returnPortalPrefab = portal; returnTravelPrefab = travel; }
#endif
}
