using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public sealed class TutorialDoorClosedTrigger : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private DoorObject targetDoor;
    [SerializeField] private RoomDoorMonsterKillLock roomDoorMonsterKillLock;
    [SerializeField] private bool requireActiveRoomLock = true;

    [Header("Trigger")]
    [SerializeField] private bool triggerOnce = true;
    [SerializeField] private UnityEvent onDoorClosed = new();

    private bool hasTriggered;

    public UnityEvent OnDoorClosed => onDoorClosed;

    private void Reset()
    {
        ResolveReferences();
    }

    private void OnValidate()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (targetDoor != null)
            targetDoor.ClosedPresentationCompleted += HandleDoorClosed;
    }

    private void OnDisable()
    {
        if (targetDoor != null)
            targetDoor.ClosedPresentationCompleted -= HandleDoorClosed;
    }

    public void ResetRuntimeTrigger()
    {
        hasTriggered = false;
    }

    private void HandleDoorClosed(DoorObject door)
    {
        if (triggerOnce && hasTriggered)
            return;

        if (!PassesRoomLockFilter())
            return;

        hasTriggered = true;
        onDoorClosed?.Invoke();
    }

    private bool PassesRoomLockFilter()
    {
        if (!requireActiveRoomLock || roomDoorMonsterKillLock == null)
            return true;

        return roomDoorMonsterKillLock.EncounterEntered &&
               roomDoorMonsterKillLock.RemainingMonsterCount > 0;
    }

    private void ResolveReferences()
    {
        if (targetDoor == null)
            targetDoor = GetComponentInParent<DoorObject>();

        if (roomDoorMonsterKillLock == null)
            roomDoorMonsterKillLock = GetComponentInParent<RoomDoorMonsterKillLock>();
    }
}
