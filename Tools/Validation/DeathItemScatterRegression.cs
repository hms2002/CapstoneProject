// Run in the isolated Unity harness with freshly built Gameplay/Core assemblies.
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class DeathItemScatterRegression
{
    private const string Pending = "DeathItemScatterRegression.Pending";
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Set(object o, string field, object value) => o.GetType().GetField(field, Flags).SetValue(o, value);
    private static void Call(object o, string method) => o.GetType().GetMethod(method, Flags).Invoke(o, null);
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }

    public static void Run()
    {
        SessionState.SetBool(Pending, true);
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void Resume()
    {
        if (SessionState.GetBool(Pending, false)) EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        EditorApplication.update -= Tick;
        SessionState.SetBool(Pending, false);
        try { Exercise(); Debug.Log("DEATH_SCATTER_PASS: all containers preserved, relic levels, copies locked/unregistered, cleanup, empty inventory."); EditorApplication.Exit(0); }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    private static void Exercise()
    {
        var prefab = new GameObject("Drop fixture");
        prefab.AddComponent<BoxCollider2D>();
        prefab.AddComponent<WorldItemPickup2D>();
        var lootHost = new GameObject("Loot fixture"); lootHost.SetActive(false);
        var loot = lootHost.AddComponent<LootManager>();
        typeof(LootManager).GetProperty("Instance").GetSetMethod(true).Invoke(null, new object[] { loot });
        Set(loot, "worldItemPrefab", prefab);
        Set(loot, "spawnService", new LootSpawnService(prefab, null, null));
        var backend = new CaptureBackend();
        WorldItemDropAnimationPlayback.RegisterBackend(backend);

        var host = new GameObject("Player fixture"); host.SetActive(false);
        var death = host.AddComponent<PlayerDeathReturnToHub2D>();
        var weapons = host.AddComponent<WeaponInventory2D>();
        var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
        Set(weapons, "slots", new[] { weapon, null });
        var consumables = host.AddComponent<PlayerConsumableInventory>();
        var consumable = ScriptableObject.CreateInstance<ConsumableDefinition>();
        Set(consumables, "slots", new[] { consumable, consumable, null });
        var relics = host.AddComponent<RelicInventory>();
        var relic = ScriptableObject.CreateInstance<RelicDefinition>();
        relic.maxLevel = 3;
        var entryType = typeof(RelicInventory).GetNestedType("Entry", BindingFlags.NonPublic);
        var entries = Array.CreateInstance(entryType, 1);
        var entry = Activator.CreateInstance(entryType);
        entryType.GetField("def").SetValue(entry, relic);
        entryType.GetField("level").SetValue(entry, 3);
        entries.SetValue(entry, 0); Set(relics, "slots", entries); Set(relics, "capacity", 1);
        var bag = host.AddComponent<PlayerBackpackInventory>();
        var bagState = new ChestInventory(2);
        bagState.Set(0, weapon); bagState.SetRelicWithLevel(1, relic, 2);
        Set(bag, "inventory", bagState);
        Check(bag.GetRelicLevelInSlot(1) == 2, "Invalid fixture relic level");

        Call(death, "ScatterDeathItemCopies");
        Check(backend.Copies.Count == 6, "Expected every occupied slot, including duplicates and backpack");
        Check(weapons.GetWeaponInSlot(0) == weapon && consumables.GetConsumableInSlot(1) == consumable,
            "Original weapon/consumable changed");
        Check(relics.GetRelicLevelInSlot(0) == 3 && bag.GetRelicLevelInSlot(1) == 2 && bag.Get(0) == weapon,
            "Original relic levels/backpack changed");
        Check(backend.Copies[1].RelicLevel == 3 && backend.Copies[5].RelicLevel == 2, "Copy levels lost");
        foreach (var copy in backend.Copies)
        {
            copy.SetInteractionLocked(false);
            copy.gameObject.SetActive(false); copy.gameObject.SetActive(true);
            Check(!copy.CanInteract(null) && !copy.GetComponent<Collider2D>().enabled, "Copy unlocked");
            Check(!Contains(copy), "Copy leaked into nearby loot registry");
            copy.OnPlayerInteract(null);
            Check(copy.Item != null, "Direct interaction changed copy");
            Check(copy.gameObject.scene == host.scene, "Copy scene ownership wrong");
        }
        Call(death, "OnDisable");
        foreach (var copy in backend.Copies) Check(!copy.gameObject.activeSelf, "Cleanup left copy active");
        int normalCopies = backend.Copies.Count;
        // Tutorial combat deliberately disables normal routing, then invokes this entry at zero HP.
        death.enabled = false;
        death.PrepareForScriptedDeathPresentation();
        Check(backend.Copies.Count == normalCopies + 6, "Disabled scripted death did not scatter all slots");
        death.PrepareForScriptedDeathPresentation();
        Check(backend.Copies.Count == normalCopies + 6, "Repeated scripted preparation duplicated copies");
        Check(!(bool)typeof(PlayerDeathReturnToHub2D).GetField("isDeathSequenceRunning", Flags).GetValue(death),
            "Scripted scatter entered normal game-over routing");
        Check(weapons.GetWeaponInSlot(0) == weapon && bag.Get(0) == weapon && relics.GetRelicLevelInSlot(0) == 3,
            "Scripted scatter removed original inventory");
        Call(death, "OnDestroy");
        foreach (var copy in backend.Copies) Check(!copy.gameObject.activeSelf, "Disabled-player destruction left a copy active");
        Set(death, "isDeathSequenceRunning", true);
        Check(!death.TryStartTimeOverSequence(), "Repeated death was accepted");
        var empty = new GameObject("Empty fixture"); empty.SetActive(false);
        int count = backend.Copies.Count;
        Call(empty.AddComponent<PlayerDeathReturnToHub2D>(), "ScatterDeathItemCopies");
        Check(backend.Copies.Count == count, "Empty inventory created a copy");
    }

    private static bool Contains(WorldItemPickup2D copy)
    {
        foreach (var item in WorldItemRegistry.Items) if (item == copy) return true;
        return false;
    }

    private sealed class CaptureBackend : IWorldItemDropAnimationBackend
    {
        public readonly List<WorldItemPickup2D> Copies = new();
        public bool TryPlayDrop(GameObject owner, Vector3 start, Vector3 end, Action completed)
        {
            Copies.Add(owner.GetComponent<WorldItemPickup2D>());
            Check(start != end, "Scatter has no travel");
            completed?.Invoke();
            return false; // Also exercises the missing-animation fallback.
        }
    }
}
