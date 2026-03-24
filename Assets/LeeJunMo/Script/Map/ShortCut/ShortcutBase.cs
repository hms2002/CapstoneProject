using UnityEngine;

public abstract class ShortcutBase : MonoBehaviour, IInteractable
{
    [Header("타겟 문")]
    [SerializeField] protected DoorObject targetDoor;
    [SerializeField] protected Transform promptAnchor;

    protected virtual void Awake()
    {
        if (targetDoor == null)
            targetDoor = GetComponentInParent<DoorObject>();
    }

    public virtual void OnPlayerNearby() { }
    public virtual void OnPlayerLeave() { }
    public virtual void OnHighlight() { }
    public virtual void OnUnHighlight() { }
    public virtual InteractState GetInteractType() => InteractState.Idle;
    public virtual void GetInteract(string text) { }
    public virtual Transform GetPromptAnchor() => promptAnchor != null ? promptAnchor : transform;

    public virtual bool CanInteract(IPlayerInteractor player)
    {
        return player != null &&
               player.CurrentState == InteractState.Idle &&
               targetDoor != null &&
               !targetDoor.IsOpen;
    }

    public void OnPlayerInteract(IPlayerInteractor player)
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

    public abstract string GetInteractDescription();

    protected abstract bool CheckCondition(IPlayerInteractor player);
    protected virtual void ConsumeCondition(IPlayerInteractor player) { }
    protected abstract void OnSuccess();

    protected virtual void OnFail(IPlayerInteractor player)
    {
        if (targetDoor != null)
            targetDoor.PlayShakeAnimation();
    }
}
