using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerInteractableTracker2D : MonoBehaviour
{
    private readonly List<IInteractable> nearbyObjects = new();
    private readonly Dictionary<IInteractable, int> nearbyOverlapCounts = new();

    public IReadOnlyList<IInteractable> NearbyObjects => nearbyObjects;

    public void RegisterOverlap(Collider2D other)
    {
        IInteractable interactable = ResolveInteractable(other);
        if (interactable == null)
            return;

        if (nearbyOverlapCounts.TryGetValue(interactable, out int overlapCount))
        {
            nearbyOverlapCounts[interactable] = overlapCount + 1;
            return;
        }

        nearbyOverlapCounts.Add(interactable, 1);
        nearbyObjects.Add(interactable);
        interactable.OnPlayerNearby();
    }

    public void UnregisterOverlap(Collider2D other)
    {
        IInteractable interactable = ResolveInteractable(other);
        if (interactable == null || !nearbyOverlapCounts.TryGetValue(interactable, out int overlapCount))
            return;

        overlapCount--;
        if (overlapCount > 0)
        {
            nearbyOverlapCounts[interactable] = overlapCount;
            return;
        }

        nearbyOverlapCounts.Remove(interactable);
        nearbyObjects.Remove(interactable);
        interactable.OnPlayerLeave();
    }

    public IInteractable GetClosestInteractable(IPlayerInteractor player, Vector3 origin)
    {
        if (nearbyObjects.Count == 0)
            return null;

        float closestDistance = float.MaxValue;
        IInteractable closest = null;

        for (int i = nearbyObjects.Count - 1; i >= 0; i--)
        {
            IInteractable interactable = nearbyObjects[i];
            if (interactable == null || (interactable is MonoBehaviour behaviour && behaviour == null))
            {
                RemoveDestroyedInteractableAt(i, interactable);
                continue;
            }

            MonoBehaviour interactableBehaviour = (MonoBehaviour)interactable;
            float distance = Vector2.Distance(origin, interactableBehaviour.transform.position);
            if (distance < closestDistance && interactable.CanInteract(player))
            {
                closestDistance = distance;
                closest = interactable;
            }
        }

        return closest;
    }

    private void RemoveDestroyedInteractableAt(int index, IInteractable interactable)
    {
        if (interactable != null)
            nearbyOverlapCounts.Remove(interactable);

        nearbyObjects.RemoveAt(index);
    }

    private static IInteractable ResolveInteractable(Collider2D other)
    {
        IInteractable interactable = other.GetComponent<IInteractable>();
        if (interactable != null)
            return interactable;

        return other.GetComponentInParent<IInteractable>();
    }
}
