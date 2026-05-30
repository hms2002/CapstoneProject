using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Notifies the room encounter only after the player's body collider is fully inside this room trigger.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class RoomEncounterEntryTrigger2D : MonoBehaviour
{
    [SerializeField] private MonsterSpawnRoomGroup targetRoomGroup;

    private readonly HashSet<Collider2D> activePlayerBodyColliders = new();
    private Collider2D triggerCollider;
    private bool missingRoomGroupWarningLogged;

    private void Reset()
    {
        if (targetRoomGroup == null)
            targetRoomGroup = GetComponentInParent<MonsterSpawnRoomGroup>();

        Collider2D triggerCollider = GetComponent<Collider2D>();
        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
    }

    private void Awake()
    {
        ResolveRoomGroup();
        EnsureTriggerCollider();
    }

    private void Update()
    {
        CompactInactiveBodyColliders();

        if (activePlayerBodyColliders.Count <= 0)
            return;

        if (!ResolveRoomGroup() || triggerCollider == null)
            return;

        foreach (Collider2D bodyCollider in activePlayerBodyColliders)
        {
            if (bodyCollider != null && IsBodyColliderFullyInsideRoom(bodyCollider))
            {
                targetRoomGroup.NotifyPlayerEnteredEncounter();
                return;
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!TryResolvePlayerBodyCollider(other, out Collider2D bodyCollider))
            return;

        if (!ResolveRoomGroup())
            return;

        activePlayerBodyColliders.Add(bodyCollider);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!activePlayerBodyColliders.Remove(other))
            return;

        if (activePlayerBodyColliders.Count > 0)
            return;

        if (!ResolveRoomGroup())
            return;

        targetRoomGroup.NotifyPlayerExitedEncounter();
    }

    private void OnDisable()
    {
        if (activePlayerBodyColliders.Count <= 0)
            return;

        activePlayerBodyColliders.Clear();
        if (targetRoomGroup != null)
            targetRoomGroup.NotifyPlayerExitedEncounter();
    }

    private bool ResolveRoomGroup()
    {
        if (targetRoomGroup != null)
            return true;

        targetRoomGroup = GetComponentInParent<MonsterSpawnRoomGroup>();
        if (targetRoomGroup != null)
            return true;

        if (!missingRoomGroupWarningLogged)
        {
            Debug.LogWarning("[RoomEncounterEntryTrigger2D] Missing target MonsterSpawnRoomGroup reference.", this);
            missingRoomGroupWarningLogged = true;
        }

        return false;
    }

    private void EnsureTriggerCollider()
    {
        triggerCollider = GetComponent<Collider2D>();
        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
    }

    private static bool TryResolvePlayerBodyCollider(Collider2D other, out Collider2D bodyCollider)
    {
        bodyCollider = null;

        if (other == null)
            return false;

        PlayerInteractor2D player = other.GetComponentInParent<PlayerInteractor2D>();
        if (player == null || !player.CompareTag("Player"))
            return false;

        Collider2D playerBodyCollider = player.BodyCollider;
        if (playerBodyCollider == null || playerBodyCollider != other)
            return false;

        bodyCollider = playerBodyCollider;
        return true;
    }

    private bool IsBodyColliderFullyInsideRoom(Collider2D bodyCollider)
    {
        if (bodyCollider == null || triggerCollider == null)
            return false;

        Bounds bounds = bodyCollider.bounds;
        Vector2 min = new(bounds.min.x, bounds.min.y);
        Vector2 max = new(bounds.max.x, bounds.max.y);
        Vector2 center = new(bounds.center.x, bounds.center.y);

        return triggerCollider.OverlapPoint(new Vector2(min.x, min.y)) &&
               triggerCollider.OverlapPoint(new Vector2(min.x, max.y)) &&
               triggerCollider.OverlapPoint(new Vector2(max.x, min.y)) &&
               triggerCollider.OverlapPoint(new Vector2(max.x, max.y)) &&
               triggerCollider.OverlapPoint(new Vector2(center.x, min.y)) &&
               triggerCollider.OverlapPoint(new Vector2(center.x, max.y)) &&
               triggerCollider.OverlapPoint(new Vector2(min.x, center.y)) &&
               triggerCollider.OverlapPoint(new Vector2(max.x, center.y)) &&
               triggerCollider.OverlapPoint(center);
    }

    private void CompactInactiveBodyColliders()
    {
        activePlayerBodyColliders.RemoveWhere(bodyCollider =>
            bodyCollider == null ||
            !bodyCollider.enabled ||
            !bodyCollider.gameObject.activeInHierarchy);
    }
}
