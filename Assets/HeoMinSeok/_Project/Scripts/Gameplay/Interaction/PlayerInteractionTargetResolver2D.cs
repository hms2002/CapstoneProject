using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerInteractionTargetResolver2D : MonoBehaviour
{
    [SerializeField] private PlayerInteractableTracker2D tracker;

    public IInteractable CurrentTarget { get; private set; }

    private void Awake()
    {
        ResolveTracker();
    }

    public IInteractable RefreshTarget(IPlayerInteractor player, Vector3 origin)
    {
        ResolveTracker();

        IInteractable nearest = tracker != null
            ? tracker.GetClosestInteractable(player, origin)
            : null;

        if (ReferenceEquals(nearest, CurrentTarget))
            return CurrentTarget;

        if (CurrentTarget != null)
            CurrentTarget.OnUnHighlight();

        CurrentTarget = nearest;

        if (CurrentTarget != null)
            CurrentTarget.OnHighlight();

        return CurrentTarget;
    }

    public void ClearCurrentTarget()
    {
        if (CurrentTarget != null)
            CurrentTarget.OnUnHighlight();

        CurrentTarget = null;
    }

    private void ResolveTracker()
    {
        if (tracker == null)
            tracker = GetComponent<PlayerInteractableTracker2D>();
    }
}
