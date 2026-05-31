using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임:
/// - 방 encounter가 시작되면 연결된 문을 닫고, 추적 중인 몬스터가 모두 정리되면 다시 연다.
/// - 몬스터가 방 밖에 유인된 상태에서는 문을 열어 두어 전투 대상이 문 밖에 갇히지 않게 한다.
/// - 문이 아직 닫히지 않은 유예 상태에서 플레이어가 방을 벗어나면 encounter 시작을 취소한다.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-50)]
public sealed class RoomDoorMonsterKillLock : MonoBehaviour
{
    [Header("Door")]
    [SerializeField] private DoorObject targetDoor;

    [Header("Room")]
    [SerializeField] private MonsterSpawnRoomGroup targetRoomGroup;
    [SerializeField] private bool requireTrackedMonstersInsideBeforeClose = true;
    [Tooltip("몬스터가 방 밖에 감지되어도 이 시간 동안 상태가 유지될 때만 문을 다시 엽니다. 분열/점프 착지 전 순간적인 외부 판정을 흡수합니다.")]
    [SerializeField, Min(0f)] private float openForOutsideMonsterDelaySeconds = 0.35f;
    [Tooltip("몬스터 수가 0이 된 뒤 이 시간 동안 추가 등록이 없을 때만 문을 엽니다. 슬라임 분열처럼 사망 직후 자식 등록 전 공백을 흡수합니다.")]
    [SerializeField, Min(0f)] private float openAfterAllClearedDelaySeconds = 0.35f;

    [Header("Debug")]
    [SerializeField] private bool logDebug;

    private readonly List<GameObject> trackedMonsters = new();
    private bool roomEntered;
    private bool doorClosedByLock;
    private bool missingDoorWarningLogged;
    private bool missingRoomGroupWarningLogged;
    private bool registeredWithRoomGroup;
    private MonsterRoomArea2D cachedRoomArea;
    private float allClearedStableSince = -1f;
    private float outsideMonsterStableSince = -1f;

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

        if (roomEntered && allClearedStableSince >= 0f)
            RefreshDoorState();
    }

    public void RegisterMonster(GameObject monster)
    {
        if (monster == null || trackedMonsters.Contains(monster))
            return;

        trackedMonsters.Add(monster);
        ResetAllClearedDelay();
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

    public void NotifyRoomEncounterExited()
    {
        if (!roomEntered)
            return;

        if (doorClosedByLock)
        {
            LogDebug("Room encounter exit ignored because the door is already locked.");
            return;
        }

        roomEntered = false;
        ResetAllClearedDelay();
        LogDebug("Room encounter exited before lock. Closing sequence cancelled.");
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
        if (!roomEntered)
        {
            ResetAllClearedDelay();
            ResetOutsideMonsterDelay();
            OpenDoorIfNeeded(playPresentation: doorClosedByLock);
            return;
        }

        if (remainingCount <= 0)
        {
            ResetOutsideMonsterDelay();
            if (ShouldDelayOpeningAfterAllCleared())
                return;

            OpenDoorIfNeeded(playPresentation: doorClosedByLock);
            return;
        }

        ResetAllClearedDelay();
        if (ShouldKeepDoorOpenForOutsideMonsters(out string outsideMonsterName))
        {
            if (ShouldDelayOpeningForOutsideMonster(outsideMonsterName))
                return;

            OpenDoorIfNeeded(playPresentation: doorClosedByLock);
            return;
        }

        ResetOutsideMonsterDelay();
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
        int count = targetRoomGroup != null ? targetRoomGroup.PendingRoomEntrySpawnCount : 0;
        for (int i = 0; i < trackedMonsters.Count; i++)
        {
            if (trackedMonsters[i] != null)
                count++;
        }

        return count;
    }

    /// <summary>
    /// 책임:
    /// - 전투 종료 직후 짧은 시간 동안 문 열림을 보류해 사망/분열 등록 사이의 공백을 흡수한다.
    /// - 새 몬스터가 등록되면 안정화 타이머가 리셋되어 문이 열렸다 닫히는 깜빡임을 방지한다.
    /// </summary>
    private bool ShouldDelayOpeningAfterAllCleared()
    {
        if (!doorClosedByLock || openAfterAllClearedDelaySeconds <= 0f)
            return false;

        if (allClearedStableSince < 0f)
        {
            allClearedStableSince = Time.time;
            LogDebug($"All monsters cleared. Waiting {openAfterAllClearedDelaySeconds:0.###}s before opening door.");
            return true;
        }

        return Time.time - allClearedStableSince < openAfterAllClearedDelaySeconds;
    }

    private void ResetAllClearedDelay()
    {
        allClearedStableSince = -1f;
    }

    /// <summary>
    /// 책임:
    /// - 몬스터가 방 밖으로 감지된 뒤 짧은 안정화 시간을 둬 순간적인 분열/점프 위치 오차로 문이 열리는 일을 막는다.
    /// </summary>
    private bool ShouldDelayOpeningForOutsideMonster(string outsideMonsterName)
    {
        if (!doorClosedByLock || openForOutsideMonsterDelaySeconds <= 0f)
            return false;

        if (outsideMonsterStableSince < 0f)
        {
            outsideMonsterStableSince = Time.time;
            LogDebug($"Outside monster '{outsideMonsterName}' detected. Waiting {openForOutsideMonsterDelaySeconds:0.###}s before opening door.");
            return true;
        }

        return Time.time - outsideMonsterStableSince < openForOutsideMonsterDelaySeconds;
    }

    private void ResetOutsideMonsterDelay()
    {
        outsideMonsterStableSince = -1f;
    }

    /// <summary>
    /// 책임:
    /// - 방 밖으로 유인된 몬스터가 문에 막혀 전투에 복귀하지 못하는 상황을 방지한다.
    /// - 방 영역 정보가 없으면 기존 문 닫힘 규칙을 유지해 authoring 누락이 전투를 영구 개방하지 않게 한다.
    /// </summary>
    private bool ShouldKeepDoorOpenForOutsideMonsters(out string outsideMonsterName)
    {
        outsideMonsterName = string.Empty;

        if (!requireTrackedMonstersInsideBeforeClose)
            return false;

        MonsterRoomArea2D roomArea = ResolveRoomArea();
        if (roomArea == null)
            return false;

        for (int i = 0; i < trackedMonsters.Count; i++)
        {
            GameObject monster = trackedMonsters[i];
            if (monster == null)
                continue;

            if (IsSplitLandingInProgress(monster))
            {
                LogDebug($"Keeping door closed because '{monster.name}' is still resolving split landing.");
                continue;
            }

            if (!roomArea.Contains(monster.transform.position))
            {
                outsideMonsterName = monster.name;
                LogDebug($"Keeping door open because '{outsideMonsterName}' is outside room area.");
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 책임:
    /// - 분열 착지 중인 슬라임을 방 밖 몬스터 예외 개방 조건에서 제외해 착지 연출 중 문 깜빡임을 막는다.
    /// </summary>
    private static bool IsSplitLandingInProgress(GameObject monster)
    {
        if (monster == null)
            return false;

        SlimeSplitLandingMotion2D landingMotion = monster.GetComponent<SlimeSplitLandingMotion2D>();
        if (landingMotion == null)
            landingMotion = monster.GetComponentInChildren<SlimeSplitLandingMotion2D>(includeInactive: true);

        return landingMotion != null && landingMotion.IsRunning;
    }

    private MonsterRoomArea2D ResolveRoomArea()
    {
        if (cachedRoomArea != null)
            return cachedRoomArea;

        if (targetRoomGroup == null)
            return null;

        cachedRoomArea = targetRoomGroup.GetComponentInChildren<MonsterRoomArea2D>();
        return cachedRoomArea;
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
