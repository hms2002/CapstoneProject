using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class RoomEncounterEntryTrigger2D : MonoBehaviour
{
    [SerializeField] private MonsterSpawnRoomGroup targetRoomGroup;

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

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayerCollider(other))
            return;

        if (!ResolveRoomGroup())
            return;

        targetRoomGroup.NotifyPlayerEnteredEncounter();
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
        Collider2D triggerCollider = GetComponent<Collider2D>();
        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
    }

    private static bool IsPlayerCollider(Collider2D other)
    {
        if (other == null)
            return false;

        if (other.GetComponentInParent<PlayerInteractor2D>() != null)
            return true;

        return other.CompareTag("Player");
    }
}
