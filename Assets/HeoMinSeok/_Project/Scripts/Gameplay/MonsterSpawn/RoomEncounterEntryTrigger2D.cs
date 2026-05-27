using UnityEngine;
using UnityGAS;
using System.Collections.Generic;

/// <summary>
/// 책임:
/// - 플레이어의 공식 전투 접점이 방 입장 영역에 들어왔을 때 방 encounter 시작을 MonsterSpawnRoomGroup에 알린다.
/// - 문 닫힘이 유예된 상태에서 플레이어가 다시 방 밖으로 나가면 encounter 시작 요청을 취소한다.
/// - 공격 이펙트/센서처럼 플레이어 하위에 붙은 비본체 collider가 방 입장으로 오인되지 않도록 직접 CombatHurtbox2D만 인정한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class RoomEncounterEntryTrigger2D : MonoBehaviour
{
    [SerializeField] private MonsterSpawnRoomGroup targetRoomGroup;

    private readonly HashSet<Collider2D> activePlayerColliders = new();
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

        bool wasEmpty = activePlayerColliders.Count == 0;
        activePlayerColliders.Add(other);

        if (wasEmpty)
            targetRoomGroup.NotifyPlayerEnteredEncounter();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!activePlayerColliders.Remove(other))
            return;

        if (activePlayerColliders.Count > 0)
            return;

        if (!ResolveRoomGroup())
            return;

        targetRoomGroup.NotifyPlayerExitedEncounter();
    }

    private void OnDisable()
    {
        if (activePlayerColliders.Count <= 0)
            return;

        activePlayerColliders.Clear();
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
        Collider2D triggerCollider = GetComponent<Collider2D>();
        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
    }

    private static bool IsPlayerCollider(Collider2D other)
    {
        if (other == null)
            return false;

        CombatHurtbox2D hurtbox = other.GetComponent<CombatHurtbox2D>();
        if (hurtbox == null || !hurtbox.OwnsCollider(other))
            return false;

        GameObject targetRoot = hurtbox.ResolveTargetRoot();
        return targetRoot != null && targetRoot.CompareTag("Player");
    }
}
