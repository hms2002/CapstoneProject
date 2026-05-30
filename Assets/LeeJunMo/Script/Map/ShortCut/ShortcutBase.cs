using UnityEngine;
using UnityGAS;

/// <summary>
/// 잠긴 문과 연결된 숏컷 상호작용의 조건 검사, 비용 소비, 성공 연출 실행 흐름을 관리합니다.
/// </summary>
public abstract class ShortcutBase : InteractableBase
{
    [Header("설정")]
    [SerializeField] protected DoorObject targetDoor;
    [SerializeField] protected Transform promptAnchor;

    [Header("Presentation")]
    [SerializeField] private Transform presentationAnchor;
    [SerializeField] private WorldObjectPresentationDefinition successPresentation = new();

    [SerializeField, HideInInspector] private DoorObject lastSyncedTargetDoor;
    private WorldObjectPresentationRuntime successPresentationRuntime;

    public DoorObject TargetDoor => targetDoor;

    protected virtual DoorObject.DoorType RequiredDoorType => DoorObject.DoorType.Locked;
    protected abstract bool RequiredDoorIsPermanent { get; }

    protected virtual void Awake()
    {
        if (targetDoor == null)
            targetDoor = GetComponentInParent<DoorObject>();

        successPresentationRuntime = new WorldObjectPresentationRuntime(gameObject);
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
        if (TryBeginDeferredSuccess(player))
            return;

        CompleteSuccessfulInteraction(player);
    }

    protected void CompleteSuccessfulInteraction(IPlayerInteractor player)
    {
        OnSuccess();
        PlaySuccessPresentation(player);
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
    protected virtual bool TryBeginDeferredSuccess(IPlayerInteractor player) => false;
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

    private void PlaySuccessPresentation(IPlayerInteractor player)
    {
        successPresentationRuntime?.PlayExecuteOnly(
            successPresentation,
            instigator: player?.Transform != null ? player.Transform.gameObject : null,
            target: gameObject,
            anchor: ResolvePresentationAnchor(),
            sourceObject: this);
    }

    private Transform ResolvePresentationAnchor()
    {
        if (presentationAnchor != null)
            return presentationAnchor;

        return promptAnchor != null ? promptAnchor : transform;
    }
}
