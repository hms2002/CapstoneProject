#if UNITY_EDITOR
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Verifies that clearing weapon HUDs after input-service destruction never recreates a global service.</summary>
public sealed class InputHudCleanupPlayModeTests
{
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
