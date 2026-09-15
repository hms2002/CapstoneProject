#if UNITY_EDITOR
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Verifies input HUD cleanup and boss cinematic HUD release without destroying the boss.</summary>
public sealed class InputHudCleanupPlayModeTests
{
    [Test]
    public void ProceduralFader_RebuildsAfterPlacementAndDiscardsColorsBeforeReplacement()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var root = new GameObject("ProceduralFaderTest");
        root.SetActive(false);
        root.AddComponent<Grid>();
        var tileObject = new GameObject("Foreground", typeof(UnityEngine.Tilemaps.Tilemap));
        tileObject.transform.SetParent(root.transform);
        var map = tileObject.GetComponent<UnityEngine.Tilemaps.Tilemap>();
        var tile = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
        var player = new GameObject("FaderPlayer");
        player.layer = 3;
        player.transform.position = new Vector3(0.5f, 0.5f);
        player.AddComponent<BoxCollider2D>().isTrigger = true;
        try
        {
            var builder = root.AddComponent<DungeonRoomBuilder>();
            typeof(DungeonRoomBuilder).GetField("foregroundTilemap", flags).SetValue(builder, map);
            Type type = Type.GetType("TilemapOcclusionFader2D, Infrastructure", true);
            var fader = (Behaviour)root.AddComponent(type);
            type.GetField("targetTilemaps", flags).SetValue(fader, new[] { map });
            type.GetField("detectionLayers", flags).SetValue(fader, (LayerMask)(1 << 3));
            type.GetField("includeTriggers", flags).SetValue(fader, true);
            root.SetActive(true);
            map.SetTile(Vector3Int.zero, tile);
            ((Action)typeof(DungeonRoomBuilder).GetField("TileContentBuilt", flags).GetValue(builder))();
            Physics2D.SyncTransforms();
            type.GetMethod("RefreshActiveGroups", flags).Invoke(fader, null);
            type.GetMethod("UpdateGroupFade", flags).Invoke(fader, new object[] { 1f });
            Assert.That(map.GetColor(Vector3Int.zero).a, Is.EqualTo(0.35f).Within(0.001f));
            builder.ClearGeneratedTiles();
            map.SetTile(Vector3Int.zero, tile);
            map.SetTileFlags(Vector3Int.zero, UnityEngine.Tilemaps.TileFlags.None);
            map.SetColor(Vector3Int.zero, Color.red);
            ((Action)typeof(DungeonRoomBuilder).GetField("TileContentBuilt", flags).GetValue(builder))();
            Assert.That(map.GetColor(Vector3Int.zero), Is.EqualTo(Color.red), "Old cache cannot overwrite a newly placed cell.");
            fader.enabled = false;
            Assert.That(map.GetColor(Vector3Int.zero), Is.EqualTo(Color.red));
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(tile);
        }
    }

    /// <summary>Supplies overlapping projections without installing gameplay status effects.</summary>
    private sealed class DuplicateStatusSource : IStatusHudSource
    {
        public readonly System.Collections.Generic.List<StatusHudEntry> Entries = new();
        public void CollectStatusHudEntries(System.Collections.Generic.List<StatusHudEntry> buffer) => buffer.AddRange(Entries);
    }

    [Test]
    public void StatusProjection_DeduplicatesSameOwnerButPreservesDifferentSlots()
    {
        var root = new GameObject("StatusDedupTest");
        root.SetActive(false);
        var definition = ScriptableObject.CreateInstance<StatusHudDefinition>();
        var source = new DuplicateStatusSource();
        source.Entries.Add(definition.CreateEntry("test.slot.a", 1, 0.1f, 1f, false, true));
        source.Entries.Add(definition.CreateEntry("test.slot.a", 1, 0.3f, 1f, false, true));
        source.Entries.Add(definition.CreateEntry("test.slot.b", 1, 0.2f, 1f, false, true));
        try
        {
            StatusHudSourceRegistry.RegisterSource(source);
            Type type = Type.GetType("StatusHudService, UI", throwOnError: true);
            Component service = root.AddComponent(type);
            var buffer = new System.Collections.Generic.List<StatusHudEntry>();
            type.GetMethod("CollectEntries").Invoke(service, new object[] { buffer });
            var a = buffer.FindAll(e => e.OwnerKey == "test.slot.a");
            Assert.That(a.Count, Is.EqualTo(1));
            Assert.That(a[0].RemainingTime, Is.EqualTo(0.3f));
            Assert.That(buffer.FindAll(e => e.OwnerKey == "test.slot.b").Count, Is.EqualTo(1));
        }
        finally
        {
            StatusHudSourceRegistry.UnregisterSource(source);
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(definition);
        }
    }

    /// <summary>Records per-boss release requests without creating production HUD objects.</summary>
    private sealed class RecordingBossHudBackend : IBossHudBackend
    {
        public IBossHudSource Released;
        public void RegisterBoss(IBossHudSource boss, string bossDisplayNameOverride = null, BossHudHealthBarTheme healthBarTheme = null) { }
        public void MarkBossDefeated(IBossHudSource boss) { }
        public void UnbindBoss(IBossHudSource boss) => Released = boss;
    }

    [Test]
    public void BossVanish_ReleasesHudWhileOwnerRemainsAlive()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        FieldInfo backendField = typeof(BossHudPlayback).GetField("backend", BindingFlags.Static | BindingFlags.NonPublic);
        var previous = (IBossHudBackend)backendField.GetValue(null);
        var recorder = new RecordingBossHudBackend();
        var root = new GameObject("BossHudVanishTest");
        root.SetActive(false);
        try
        {
            var owner = root.AddComponent<DragonController>();
            var presentation = root.AddComponent<BossDeathPresentation>();
            var renderer = root.AddComponent<SpriteRenderer>();
            typeof(BossDeathPresentation).GetField("owner", flags).SetValue(presentation, owner);
            BossHudPlayback.RegisterBackend(recorder);
            typeof(BossDeathPresentation).GetMethod("HideBossVisuals", flags).Invoke(presentation, null);
            Assert.That(recorder.Released, Is.SameAs(owner));
            Assert.That(owner != null, Is.True, "Reward and cinematic ownership remains alive.");
            Assert.That(renderer.enabled, Is.False);
            typeof(BossDeathPresentation).GetMethod("HideBossVisuals", flags).Invoke(presentation, null);
            Assert.That(recorder.Released, Is.SameAs(owner), "Repeated cleanup remains safe.");
        }
        finally
        {
            Object.DestroyImmediate(root);
            BossHudPlayback.UnregisterBackend(recorder);
            BossHudPlayback.RegisterBackend(previous);
        }
    }

    [TestCase("WeaponSkillHUD2D")]
    [TestCase("SwapWeaponSkillHUD2D")]
    public void ClearSlots_AfterInputServiceDestroyed_DoesNotRecreateIt(string typeName)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        Type type = Type.GetType(typeName + ", UI", throwOnError: true);
        var root = new GameObject("InputHudCleanupTest");
        root.SetActive(false);
        InputBindingService service = InputBindingService.EnsureInstance();
        try
        {
            Component hud = root.AddComponent(type);
            // Simulate a HUD that cached the service before scene shutdown.
            type.GetField("cachedInputBindingService", flags).SetValue(hud, service);
            Object.DestroyImmediate(service.gameObject);
            Assert.That(InputBindingService.Instance == null, Is.True);

            type.GetMethod("RefreshAbilityRefs", flags).Invoke(hud, null);
            Assert.That(InputBindingService.Instance == null, Is.True,
                "Clearing a HUD must not call the service-creating EnsureInstance path.");

            service = InputBindingService.EnsureInstance();
            Assert.That(type.GetMethod("GetInputBindingService", flags).Invoke(hud, null), Is.SameAs(service),
                "Normal bootstrap replacement must still be discovered.");
        }
        finally
        {
            Object.DestroyImmediate(root);
            // Leave the normal bootstrapped service available to other tests.
            InputBindingService.EnsureInstance();
        }
    }
}
#endif
