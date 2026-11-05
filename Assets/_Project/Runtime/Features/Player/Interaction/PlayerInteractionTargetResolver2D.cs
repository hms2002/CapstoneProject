using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerInteractionTargetResolver2D : MonoBehaviour
{
    private const float GroundDropSelectionMargin = 0.08f;

    [SerializeField] private PlayerInteractableTracker2D tracker;
    private Collider2D currentDropTrigger;

    public IInteractable CurrentTarget { get; private set; }

    private void Awake()
    {
        ResolveTracker();
    }

    public IInteractable RefreshTarget(IPlayerInteractor player, Vector3 origin, Collider2D sensor = null)
    {
        ResolveTracker();

        IInteractable nearest = tracker != null
            ? tracker.GetClosestInteractable(player, origin)
            : null;

        if (ReferenceEquals(nearest, CurrentTarget) || ShouldKeepCurrentDrop(nearest, player, origin, sensor))
            return CurrentTarget;

        ClearCurrentTarget();
        CurrentTarget = nearest;
        if (nearest is WorldItemPickup2D or WeaponDrop2D)
            currentDropTrigger = ((MonoBehaviour)nearest).GetComponent<Collider2D>();

        if (CurrentTarget != null)
            CurrentTarget.OnHighlight();

        return CurrentTarget;
    }

    public void ClearCurrentTarget()
    {
        if (CurrentTarget is MonoBehaviour previous && previous != null)
            CurrentTarget.OnUnHighlight();

        CurrentTarget = null;
        currentDropTrigger = null;
    }

    private bool ShouldKeepCurrentDrop(IInteractable nearest, IPlayerInteractor player, Vector2 origin, Collider2D sensor)
    {
        if (tracker == null || currentDropTrigger == null || !currentDropTrigger.isActiveAndEnabled ||
            (nearest != null && nearest is not (WorldItemPickup2D or WeaponDrop2D)))
            return false;

        if (CurrentTarget is not MonoBehaviour current || current == null ||
            !current.isActiveAndEnabled || !CurrentTarget.CanInteract(player))
            return false;

        // A selected drop can briefly lose contact at the edge of its trigger.
        if (!tracker.IsInInteractionRange(CurrentTarget))
        {
            if (sensor == null || !sensor.isActiveAndEnabled)
                return false;

            ColliderDistance2D gap = sensor.Distance(currentDropTrigger);
            if (!gap.isValid || Mathf.Abs(gap.distance) > GroundDropSelectionMargin)
                return false;
        }

        if (nearest == null)
            return true;

        float currentDistance = Vector2.Distance(origin, current.transform.position);
        float nearestDistance = Vector2.Distance(origin, ((MonoBehaviour)nearest).transform.position);
        return currentDistance - nearestDistance <= GroundDropSelectionMargin;
    }

    private void ResolveTracker()
    {
        if (tracker == null)
            tracker = GetComponent<PlayerInteractableTracker2D>();
    }
}
