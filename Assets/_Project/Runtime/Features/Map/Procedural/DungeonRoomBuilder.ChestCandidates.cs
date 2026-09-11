using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Realizes selected optional chests only after room objects and deferred monster points exist.
/// Reuses ordinary object state IDs and chest/room lock ownership without changing direct chests.
/// </summary>
public sealed partial class DungeonRoomBuilder
{
    [Header("Optional Chests")]
    [SerializeField, Min(0)] private int maximumPossibleChests = 3;

    private readonly List<ChestCandidate> chestCandidates = new();

    public int MaximumPossibleChests => Mathf.Max(0, maximumPossibleChests);

    public void ConfigurePossibleChests(int maximumCount) => maximumPossibleChests = Mathf.Max(0, maximumCount);

    /// <summary>Stores a placed candidate's room, pose, prefab and stable persistence key.</summary>
    private readonly struct ChestCandidate
    {
        public readonly DungeonRoomPlacement Room;
        public readonly RoomObjectPlacementData Placement;
        public readonly ChestPossible Source;
        public readonly string StateId;

        public ChestCandidate(DungeonRoomPlacement room, RoomObjectPlacementData placement, ChestPossible source)
        {
            Room = room;
            Placement = placement;
            Source = source;
            StateId = CreateRuntimeStateId(room.PlacementId, placement.placementId);
        }
    }

    private bool TryBuildPossibleChests(DungeonLayoutResult layout, IReadOnlyList<DungeonObjectRuntimeStateData> savedStates)
    {
        var ids = new List<string>(chestCandidates.Count);
        foreach (ChestCandidate candidate in chestCandidates)
            ids.Add(candidate.StateId);
        HashSet<string> selected = ChestPossibleSelection.Select(ids, MaximumPossibleChests, layout.Seed, savedStates);

        foreach (ChestCandidate candidate in chestCandidates)
        {
            // Null is an explicit saved absence, not an invitation to reroll on re-entry.
            generatedRoomObjectsByStateId[candidate.StateId] = null;
            if (!selected.Contains(candidate.StateId))
                continue;

            RoomObjectPlacementData placement = candidate.Placement;
            placement.kind = RoomObjectKind.Chest;
            placement.prefab = candidate.Source.ChestPrefab.gameObject;
            Vector3 markerScale = placement.localScale == Vector3.zero
                ? candidate.Placement.prefab.transform.localScale
                : placement.localScale;
            placement.localScale = Vector3.Scale(markerScale, placement.prefab.transform.localScale);
            if (!TryBuildRoomObject(candidate.Room, placement, null, null, null, out GameObject instance))
                return false;

            generatedRoomObjects.Add(instance);
            generatedRoomObjectsByStateId[candidate.StateId] = instance;
            if (!generatedRoomObjectsByPlacement.TryGetValue(candidate.Room.PlacementId, out List<GameObject> roomObjects))
            {
                roomObjects = new List<GameObject>();
                generatedRoomObjectsByPlacement.Add(candidate.Room.PlacementId, roomObjects);
            }
            roomObjects.Add(instance);

            ChestMonsterKillLock chestLock = instance.GetComponentInChildren<ChestMonsterKillLock>(true);
            if (chestLock == null)
                continue;

            generatedRoomGroupsByPlacement.TryGetValue(candidate.Room.PlacementId, out MonsterSpawnRoomGroup group);
            var pendingPoints = new List<MonsterSpawnContainer>();
            foreach (GameObject roomObject in roomObjects)
            {
                if (roomObject != null && roomObject.TryGetComponent(out MonsterSpawnContainer point))
                    pendingPoints.Add(point);
            }
            chestLock.BindRoomEncounter(group, pendingPoints);
        }
        return true;
    }
}
