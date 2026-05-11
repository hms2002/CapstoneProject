using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-50)]
public sealed class RoomDoorMonsterKillLock : MonoBehaviour
{
    [Header("Door")]
    [SerializeField] private DoorObject targetDoor;

    [Header("Room")]
    [SerializeField] private MonsterSpawnRoomGroup targetRoomGroup;

    [Header("Debug")]
    [SerializeField] private bool logDebug;

    private readonly List<GameObject> trackedMonsters = new();
    private bool roomEntered;
    private bool doorClosedByLock;
    private bool missingDoorWarningLogged;
    private bool missingRoomGroupWarningLogged;
    private bool registeredWithRoomGroup;

    public bool RoomEntered => roomEntered;
    public bool EncounterEntered => roomEntered;
    public int RemainingMonsterCount => CountRemainingMonsters();

    private void Reset()
    {
        if (targetDoor == null)
            targetDoor = GetComponentInChildren<DoorObject>();
    }

    private void OnEnable()
    {
        RegisterWithRoomGroup();
    }

    private void OnDisable()
    {
        UnregisterFromRoomGroup();
    }

    private void Awake()
    {
        ResolveDoor();
        RegisterWithRoomGroup();
    }

    private void Start()
    {
        ApplyInitialOpenState();
        RefreshDoorState();
    }

    private void Update()
    {
        int previousCount = trackedMonsters.Count;
        CompactDestroyedMonsterEntries();

        if (previousCount != trackedMonsters.Count)
        {
            RefreshDoorState();
            return;
        }

        if (roomEntered && targetDoor != null && targetDoor.IsOpen && CountRemainingMonsters() > 0)
            RefreshDoorState();
    }

    public void RegisterMonster(GameObject monster)
    {
        if (monster == null || trackedMonsters.Contains(monster))
            return;

        trackedMonsters.Add(monster);
        LogDebug($"Registered monster '{monster.name}'. remaining={RemainingMonsterCount}");
        RefreshDoorState();
    }

    private void ApplyInitialOpenState()
    {
        if (!ResolveDoor())
            return;

        targetDoor.ForceOpen(immediate: true, playPresentation: false);
        doorClosedByLock = false;
    }

    private void RegisterWithRoomGroup()
    {
        if (registeredWithRoomGroup)
            return;

        if (targetRoomGroup == null)
        {
            if (Application.isPlaying && !missingRoomGroupWarningLogged)
            {
                Debug.LogWarning("[RoomDoorMonsterKillLock] Missing target MonsterSpawnRoomGroup reference.", this);
                missingRoomGroupWarningLogged = true;
            }

            return;
        }

        targetRoomGroup.RegisterDoorLock(this);
        registeredWithRoomGroup = true;
    }

    private void UnregisterFromRoomGroup()
    {
        if (!registeredWithRoomGroup)
            return;

        if (targetRoomGroup != null)
            targetRoomGroup.UnregisterDoorLock(this);

        registeredWithRoomGroup = false;
    }

    public void NotifyRoomEncounterEntered()
    {
        if (roomEntered)
            return;

        roomEntered = true;
        LogDebug("Room encounter entered.");
        RefreshDoorState();
    }

    [ContextMenu("Clear Registered Monsters")]
    public void ClearRegisteredMonsters()
    {
        trackedMonsters.Clear();
        RefreshDoorState();
    }

    private void RefreshDoorState()
    {
        if (!ResolveDoor())
            return;

        int remainingCount = CountRemainingMonsters();
        if (!roomEntered || remainingCount <= 0)
        {
            OpenDoorIfNeeded(playPresentation: doorClosedByLock);
            return;
        }

        CloseDoorIfNeeded();
    }

    private void OpenDoorIfNeeded(bool playPresentation)
    {
        if (targetDoor == null)
            return;

        if (!targetDoor.IsOpen)
            targetDoor.ForceOpen(immediate: false, save: false, playPresentation: playPresentation);

        doorClosedByLock = false;
    }

    private void CloseDoorIfNeeded()
    {
        if (targetDoor == null)
            return;

        if (targetDoor.IsOpen)
            targetDoor.ForceClose(immediate: false);

        doorClosedByLock = true;
    }

    private int CountRemainingMonsters()
    {
        int count = 0;
        for (int i = 0; i < trackedMonsters.Count; i++)
        {
            if (trackedMonsters[i] != null)
                count++;
        }

        return count;
    }

    private void CompactDestroyedMonsterEntries()
    {
        for (int i = trackedMonsters.Count - 1; i >= 0; i--)
        {
            if (trackedMonsters[i] == null)
                trackedMonsters.RemoveAt(i);
        }
    }

    private bool ResolveDoor()
    {
        if (targetDoor != null)
            return true;

        targetDoor = GetComponentInChildren<DoorObject>();
        if (targetDoor != null)
            return true;

        if (!missingDoorWarningLogged)
        {
            Debug.LogWarning("[RoomDoorMonsterKillLock] Missing target DoorObject reference.", this);
            missingDoorWarningLogged = true;
        }

        return false;
    }

    private void LogDebug(string message)
    {
        if (!logDebug)
            return;

        Debug.Log($"[RoomDoorMonsterKillLock] {name}: {message}", this);
    }
}
