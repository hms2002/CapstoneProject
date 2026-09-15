#if UNITY_EDITOR
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Verifies input HUD cleanup and boss cinematic HUD release without destroying the boss.</summary>
public sealed class InputHudCleanupPlayModeTests
{
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
