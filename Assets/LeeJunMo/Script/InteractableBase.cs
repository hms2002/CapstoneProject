using UnityEngine;

public abstract class InteractableBase : MonoBehaviour, IInteractable
{
    public virtual void OnPlayerNearby() { }

    public virtual void OnPlayerLeave() { }

    public virtual void GetInteract(string text) { }

    public virtual void OnHighlight() { }

    public virtual void OnUnHighlight() { }

    public virtual Transform GetPromptAnchor() => transform;

    public abstract bool CanInteract(IPlayerInteractor player);

    public abstract void OnPlayerInteract(IPlayerInteractor player);

    public abstract InteractState GetInteractType();

    public abstract string GetInteractDescription();
}
