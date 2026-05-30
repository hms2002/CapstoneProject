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
    [SerializeField] private bool logRoomEntryTriggerDebug;

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
                LogTrigger($"fully inside: body={FormatCollider(bodyCollider)}, room={targetRoomGroup.name}");
                targetRoomGroup.NotifyPlayerEnteredEncounter();
                return;
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!TryResolvePlayerBodyCollider(other, out Collider2D bodyCollider))
        {
            LogTrigger($"enter ignored: not player body collider={FormatCollider(other)}");
            return;
        }

        if (!ResolveRoomGroup())
        {
            LogTrigger($"enter ignored: missing room group collider={FormatCollider(other)}");
            return;
        }

        activePlayerBodyColliders.Add(bodyCollider);
        LogTrigger($"enter accepted: body={FormatCollider(bodyCollider)}, activeCount={activePlayerBodyColliders.Count}, room={targetRoomGroup.name}");
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!activePlayerBodyColliders.Remove(other))
        {
            LogTrigger($"exit ignored: body not tracked={FormatCollider(other)}");
            return;
        }

        if (activePlayerBodyColliders.Count > 0)
        {
            LogTrigger($"exit partial: body={FormatCollider(other)}, activeCount={activePlayerBodyColliders.Count}");
            return;
        }

        if (!ResolveRoomGroup())
        {
            LogTrigger($"exit ignored: missing room group collider={FormatCollider(other)}");
            return;
        }

        LogTrigger($"exit accepted: body={FormatCollider(other)}, room={targetRoomGroup.name}");
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

    private void LogTrigger(string message)
    {
        if (!logRoomEntryTriggerDebug)
            return;

        Debug.Log($"[RoomEntryTrigger] {name}: {message}", this);
    }

    private static string FormatCollider(Collider2D collider)
    {
        return collider != null ? $"{collider.name}({collider.GetType().Name})" : "null";
    }
}
