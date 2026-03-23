using UnityEngine;

public abstract class ShortcutBase : MonoBehaviour, IInteractable
{
    [Header("타겟 문")]
    [SerializeField] protected DoorObject targetDoor;
    [SerializeField] protected Transform uiPopupPoint;

    protected DoorObject TargetDoor => targetDoor;
    public bool IsActivated => targetDoor != null && targetDoor.IsOpen;

    protected virtual void Awake()
    {
        if (targetDoor == null)
            targetDoor = GetComponentInParent<DoorObject>();
    }

    public virtual bool CanInteract(IPlayerInteractor player)
    {
        return player != null
            && player.CurrentState == InteractState.Idle
            && targetDoor != null
            && !targetDoor.IsOpen;
    }

    public void OnPlayerInteract(IPlayerInteractor player)
    {
        if (!CanInteract(player))
            return;

        if (!CheckCondition(player))
        {
            OnFail();
            return;
        }

        if (!ConsumeCondition(player))
        {
            OnFail();
            return;
        }

        OnSuccess();
    }

    public virtual void OnPlayerNearby() { }
    public virtual void OnPlayerLeave() { }
    public virtual void OnHighlight() { }
    public virtual void OnUnHighlight() { }
    public virtual InteractState GetInteractType() => InteractState.Idle;
    public virtual void GetInteract(string text) { }

    public abstract string GetInteractDescription();

    protected abstract bool CheckCondition(IPlayerInteractor player);
    protected virtual bool ConsumeCondition(IPlayerInteractor player) => true;
    protected abstract void OnSuccess();

    protected virtual void OnFail()
    {
        if (targetDoor != null)
            targetDoor.PlayShakeAnimation();
    }
}
