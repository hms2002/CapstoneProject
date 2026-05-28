using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public sealed class TutorialSceneSequenceDirector : MonoBehaviour
{
    [Header("Scene Start")]
    [SerializeField] private bool playOnStart;
    [SerializeField] private bool waitForSceneTransitionBeforeStart;
    [SerializeField, Min(0f)] private float startDelaySeconds;

    [Header("Trigger")]
    [SerializeField] private bool triggerOnce = true;
    [SerializeField] private string playerTag = "Player";

    [Header("Player Lock")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private PlayerCinematicProtection playerProtection;
    [SerializeField] private bool blockPlayerTargetability;

    [Header("Room Flow")]
    [SerializeField] private MonsterSpawnRoomGroup monsterRoomGroup;
    [SerializeField] private ChestMonsterKillLock chestMonsterKillLock;
    [SerializeField] private RoomDoorMonsterKillLock roomDoorMonsterKillLock;

    [Header("Door Control")]
    [SerializeField] private DoorObject[] doorsToOpen;
    [SerializeField] private DoorObject[] doorsToClose;

    [Header("Events")]
    [SerializeField] private UnityEvent onSceneStarted = new();
    [SerializeField] private UnityEvent onTriggerEntered = new();
    [SerializeField] private UnityEvent onCombatTutorialStarted = new();
    [SerializeField] private UnityEvent onMonstersCleared = new();
    [SerializeField] private UnityEvent onChestOpened = new();
    [SerializeField] private UnityEvent onPortalEntered = new();
    [SerializeField] private UnityEvent onSequenceCompleted = new();

    private Coroutine startRoutine;
    private Coroutine monsterClearRoutine;
    private PlayerTargetabilityBlocker targetabilityBlocker;
    private bool hasTriggered;
    private bool hasRaisedMonsterClear;
    private bool hasAcquiredPlayerProtection;
    private bool hasAcquiredTargetabilityBlock;

    public UnityEvent OnSceneStarted => onSceneStarted;
    public UnityEvent OnTriggerEntered => onTriggerEntered;
    public UnityEvent OnCombatTutorialStarted => onCombatTutorialStarted;
    public UnityEvent OnMonstersCleared => onMonstersCleared;
    public UnityEvent OnChestOpened => onChestOpened;
    public UnityEvent OnPortalEntered => onPortalEntered;
    public UnityEvent OnSequenceCompleted => onSequenceCompleted;

    private void OnEnable()
    {
        if (chestMonsterKillLock != null)
            chestMonsterKillLock.OnLockStateChanged += HandleChestLockStateChanged;
    }

    private void Start()
    {
        if (playOnStart)
            BeginSceneStart();
    }

    private void OnDisable()
    {
        if (chestMonsterKillLock != null)
            chestMonsterKillLock.OnLockStateChanged -= HandleChestLockStateChanged;

        if (startRoutine != null)
        {
            StopCoroutine(startRoutine);
            startRoutine = null;
        }

        if (monsterClearRoutine != null)
        {
            StopCoroutine(monsterClearRoutine);
            monsterClearRoutine = null;
        }

        ReleasePlayerLock();
        ReleaseTargetabilityBlock();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayerCollider(other))
            return;

        NotifyTriggerEntered();
    }

    public void BeginSceneStart()
    {
        if (startRoutine != null)
            StopCoroutine(startRoutine);

        startRoutine = StartCoroutine(SceneStartRoutine(Mathf.Max(0f, startDelaySeconds)));
    }

    public void BeginSceneStartAfterDelay(float delaySeconds)
    {
        if (startRoutine != null)
            StopCoroutine(startRoutine);

        startRoutine = StartCoroutine(SceneStartRoutine(Mathf.Max(0f, delaySeconds)));
    }

    public void NotifyTriggerEntered()
    {
        if (triggerOnce && hasTriggered)
            return;

        hasTriggered = true;
        onTriggerEntered?.Invoke();
    }

    public void NotifyCombatTutorialStarted()
    {
        if (blockPlayerTargetability)
            AcquireTargetabilityBlock();

        monsterRoomGroup?.NotifyPlayerEnteredEncounter();
        onCombatTutorialStarted?.Invoke();
        StartWaitingForMonsterClear();
    }

    public void NotifyCombatTutorialEnded()
    {
        monsterRoomGroup?.NotifyPlayerExitedEncounter();
        ReleaseTargetabilityBlock();
    }

    public void NotifyChestOpened()
    {
        onChestOpened?.Invoke();
    }

    public void NotifyPortalEntered()
    {
        onPortalEntered?.Invoke();
    }

    public void NotifySequenceCompleted()
    {
        onSequenceCompleted?.Invoke();
    }

    public void ResetMonsterClearSignal()
    {
        hasRaisedMonsterClear = false;
    }

    public void StartWaitingForMonsterClear()
    {
        if (monsterClearRoutine != null)
            return;

        if (chestMonsterKillLock == null && roomDoorMonsterKillLock == null)
        {
            Debug.LogWarning("[TutorialSceneSequenceDirector] No monster clear lock is assigned.", this);
            return;
        }

        monsterClearRoutine = StartCoroutine(WaitForMonsterClearRoutine());
    }

    public void AcquirePlayerLock()
    {
        ResolvePlayerReferences();

        if (playerTransform == null)
            return;

        if (playerProtection == null)
            playerProtection = playerTransform.gameObject.AddComponent<PlayerCinematicProtection>();

        playerProtection.Acquire(this);
        hasAcquiredPlayerProtection = true;
    }

    public void ReleasePlayerLock()
    {
        if (!hasAcquiredPlayerProtection)
            return;

        playerProtection?.Release(this);
        hasAcquiredPlayerProtection = false;
    }

    public void AcquireTargetabilityBlock()
    {
        ResolvePlayerReferences();

        if (playerTransform == null)
            return;

        targetabilityBlocker = PlayerTargetabilityBlocker.GetOrAdd(playerTransform);
        targetabilityBlocker?.Acquire(this);
        hasAcquiredTargetabilityBlock = true;
    }

    public void ReleaseTargetabilityBlock()
    {
        if (!hasAcquiredTargetabilityBlock)
            return;

        targetabilityBlocker?.Release(this);
        hasAcquiredTargetabilityBlock = false;
    }

    public void OpenDoors()
    {
        ApplyDoorOpenState(doorsToOpen, open: true);
    }

    public void CloseDoors()
    {
        ApplyDoorOpenState(doorsToClose, open: false);
    }

    private IEnumerator SceneStartRoutine(float delaySeconds)
    {
        if (waitForSceneTransitionBeforeStart)
        {
            while (IsSceneTransitionActive())
                yield return null;
        }

        if (delaySeconds > 0f)
        {
            float elapsed = 0f;
            while (elapsed < delaySeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        startRoutine = null;
        onSceneStarted?.Invoke();
    }

    private static bool IsSceneTransitionActive()
    {
        SceneTransitionCoordinator transitionCoordinator = SceneTransitionCoordinator.Instance;
        if (transitionCoordinator != null && transitionCoordinator.IsTransitionActive)
            return true;

        SceneFadeTransitionService fadeService = SceneFadeTransitionService.Instance;
        return fadeService != null && fadeService.IsTransitionActive;
    }

    private IEnumerator WaitForMonsterClearRoutine()
    {
        while (!IsMonsterClear())
            yield return null;

        monsterClearRoutine = null;
        RaiseMonsterClearOnce();
    }

    private bool IsMonsterClear()
    {
        if (chestMonsterKillLock != null)
            return chestMonsterKillLock.IsUnlocked || chestMonsterKillLock.RemainingAliveCount <= 0;

        if (roomDoorMonsterKillLock != null)
            return roomDoorMonsterKillLock.EncounterEntered && roomDoorMonsterKillLock.RemainingMonsterCount <= 0;

        return false;
    }

    private void HandleChestLockStateChanged(bool isUnlocked)
    {
        if (isUnlocked)
            RaiseMonsterClearOnce();
    }

    private void RaiseMonsterClearOnce()
    {
        if (hasRaisedMonsterClear)
            return;

        hasRaisedMonsterClear = true;
        onMonstersCleared?.Invoke();
    }

    private void ResolvePlayerReferences()
    {
        if (playerTransform == null)
            playerTransform = PlayerRuntimeRegistry.GetPlayerTransform();

        if (playerTransform == null)
            return;

        if (playerProtection == null)
            playerProtection = playerTransform.GetComponent<PlayerCinematicProtection>();
    }

    private bool IsPlayerCollider(Collider2D other)
    {
        if (other == null)
            return false;

        Transform runtimePlayer = playerTransform != null ? playerTransform : PlayerRuntimeRegistry.GetPlayerTransform();
        if (runtimePlayer != null && (other.transform == runtimePlayer || other.transform.IsChildOf(runtimePlayer)))
            return true;

        return !string.IsNullOrWhiteSpace(playerTag) && other.CompareTag(playerTag);
    }

    private static void ApplyDoorOpenState(DoorObject[] doors, bool open)
    {
        if (doors == null)
            return;

        for (int i = 0; i < doors.Length; i++)
        {
            DoorObject door = doors[i];
            if (door == null)
                continue;

            if (open)
                door.ForceOpen(immediate: false, save: false, playPresentation: true);
            else
                door.ForceClose(immediate: false);
        }
    }
}
