using UnityEngine;
using UnityGAS;

/// <summary>Player-owned purchased upgrade. Room encounter state owns activation; GE owns the bonus.</summary>
public sealed class OutOfCombatMoveSpeedRuntime : MonoBehaviour, ISceneReappliedEffectSource
{
    private CombatBuffDebuffApplicationDefinition definition;
    private CombatBuffDebuffApplier applier;
    private AbilitySystem abilities;
    private MonsterSpawnRoomGroup currentRoom;
    private bool applied;
    private const string OwnerKey = "upgrade.out_of_combat_speed";

    public void Configure(CombatBuffDebuffApplicationDefinition buff)
    {
        if (definition != buff) Release();
        definition = buff;
        abilities = GetComponent<AbilitySystem>();
        applier = CombatBuffDebuffApplier.GetOrAdd(gameObject);
        foreach (var group in FindObjectsByType<MonsterSpawnRoomGroup>(FindObjectsSortMode.None))
            if (group.PlayerEncounterEntered) currentRoom = group;
        Refresh();
    }

    private void OnEnable()
    {
        MonsterSpawnRoomGroup.ActiveRoomEntered += EnterRoom;
        MonsterSpawnRoomGroup.ActiveRoomExited += ExitRoom;
        if (definition != null) Configure(definition);
    }
    private void OnDisable()
    {
        MonsterSpawnRoomGroup.ActiveRoomEntered -= EnterRoom;
        MonsterSpawnRoomGroup.ActiveRoomExited -= ExitRoom;
        Release();
        currentRoom = null;
    }
    private void EnterRoom(MonsterSpawnRoomGroup room) { currentRoom = room; Refresh(); }
    private void ExitRoom(MonsterSpawnRoomGroup room) { if (currentRoom == room) currentRoom = null; Refresh(); }
    private void Update() => Refresh();

    private void Refresh()
    {
        if (definition == null || abilities == null || applier == null) return;
        bool roomCombat = currentRoom != null &&
            (!currentRoom.RoomEntrySpawnStarted || currentRoom.RemainingRegisteredOrPendingCount > 0);
        // Boss/legacy encounters without a room group still block the travel bonus.
        bool otherCombat = currentRoom == null && Enemy.IsAnyEnemyRecognizingPlayer();
        bool shouldApply = RunSessionStore.IsRunActive && !roomCombat && !otherCombat;
        if (!shouldApply) { Release(); return; }
        if (!applied || abilities.EffectRunner.FindActiveEffect(definition.GameplayEffect, gameObject, this) == null)
            applied = applier.ApplyFromSource(this, gameObject, definition, OwnerKey);
    }

    private void Release()
    {
        if (!applied) return;
        if (definition != null && abilities != null && abilities.EffectRunner != null)
            abilities.EffectRunner.EndEffectsBySourceObject(gameObject, definition.GameplayEffect, this);
        applied = false;
    }
}
