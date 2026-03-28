using UnityEngine;

public abstract class ShortcutBase : InteractableBase
{
    [Header("타겟 문")]
    [SerializeField] protected DoorObject targetDoor;
    [SerializeField] protected Transform promptAnchor;

    protected virtual void Awake()
    {
        if (targetDoor == null)
            targetDoor = GetComponentInParent<DoorObject>();
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

    protected abstract bool CheckCondition(IPlayerInteractor player);
    protected virtual void ConsumeCondition(IPlayerInteractor player) { }
    protected abstract void OnSuccess();

    protected virtual void OnFail(IPlayerInteractor player)
    {
        if (targetDoor != null)
            targetDoor.PlayShakeAnimation();
    }
}
