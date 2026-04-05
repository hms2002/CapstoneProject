using UnityEngine;

public abstract class ShortcutBase : InteractableBase
{
    [Header("설정")]
    [SerializeField] protected DoorObject targetDoor;
    [SerializeField] protected Transform promptAnchor;

    [SerializeField, HideInInspector] private DoorObject lastSyncedTargetDoor;

    public DoorObject TargetDoor => targetDoor;

    protected virtual DoorObject.DoorType RequiredDoorType => DoorObject.DoorType.Locked;
    protected abstract bool RequiredDoorIsPermanent { get; }

    protected virtual void Awake()
    {
        if (targetDoor == null)
            targetDoor = GetComponentInParent<DoorObject>();

        SyncTargetDoorConfiguration();
    }

    protected virtual void OnValidate()
    {
        if (targetDoor == null)
            targetDoor = GetComponentInParent<DoorObject>();

#if UNITY_EDITOR
        if (lastSyncedTargetDoor != null && lastSyncedTargetDoor != targetDoor)
            lastSyncedTargetDoor.EditorSyncConfigurationFromLinkedShortcuts();
#endif

        SyncTargetDoorConfiguration();

#if UNITY_EDITOR
        lastSyncedTargetDoor = targetDoor;
#endif
    }

    protected virtual void OnDestroy()
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
            return;

        if (lastSyncedTargetDoor != null)
            lastSyncedTargetDoor.EditorSyncConfigurationFromLinkedShortcuts();
#endif
    }

    public override InteractState GetInteractType() => InteractState.Idle;
    public override Transform GetPromptAnchor() => promptAnchor != null ? promptAnchor : transform;

    public override bool CanInteract(IPlayerInteractor player)
    {
        return player != null &&
               player.CurrentState == InteractState.Idle &&
               targetDoor != null &&
               !targetDoor.IsOpen;
    }

    public override void OnPlayerInteract(IPlayerInteractor player)
    {
        if (!CanInteract(player))
            return;

        if (!CheckCondition(player))
        {
            OnFail(player);
            return;
        }

        ConsumeCondition(player);
        OnSuccess();
    }

    public abstract override string GetInteractDescription();

    public virtual bool TryGetRequiredDoorConfiguration(out DoorObject.DoorType doorType, out bool isPermanent)
    {
        doorType = RequiredDoorType;
        isPermanent = RequiredDoorIsPermanent;
        return true;
    }

    protected abstract bool CheckCondition(IPlayerInteractor player);
    protected virtual void ConsumeCondition(IPlayerInteractor player) { }
    protected abstract void OnSuccess();

    protected virtual void OnFail(IPlayerInteractor player)
    {
        if (targetDoor != null)
            targetDoor.PlayShakeAnimation();
    }

    private void SyncTargetDoorConfiguration()
    {
        if (targetDoor == null)
            return;

        if (!TryGetRequiredDoorConfiguration(out DoorObject.DoorType requiredDoorType, out bool requiredDoorIsPermanent))
            return;

        targetDoor.ApplyConfigurationFromShortcut(requiredDoorType, requiredDoorIsPermanent, this);
    }
}
