#if UNITY_EDITOR
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>Responsibility: validate authored guidance binding, directional projection and cleanup without spawning gameplay.</summary>
public sealed class PlayerRoomChestGuidancePlayModeTests
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static Type ViewType => Type.GetType("PlayerRoomChestGuidanceView, Presentation", true);
    private const string PrefabPath = "Assets/_Project/Prefabs/Map/Navigation/PlayerRoomChestGuidance.prefab";

    [Test]
    public void PlayerPrefabHasExactlyOneAuthoredGuide()
    {
        var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/PF Player.prefab");
        Assert.That(player.GetComponentsInChildren(ViewType, true).Length, Is.EqualTo(1));
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.That(prefab.GetComponentsInChildren<SpriteRenderer>(true).Length, Is.EqualTo(2));
        foreach (var renderer in prefab.GetComponentsInChildren<SpriteRenderer>(true))
        {
            Assert.That(renderer.sprite, Is.Not.Null);
            Assert.That(renderer.sortingLayerName, Is.EqualTo("UI"));
            Assert.That(renderer.maskInteraction, Is.EqualTo(SpriteMaskInteraction.None));
            Assert.That(renderer.enabled, Is.False);
        }
    }

    [TestCase(1f, 0f)]
    [TestCase(-1f, 0f)]
    [TestCase(0f, 1f)]
    [TestCase(0f, -1f)]
    public void ArrowFacesChestWhileIconStaysUprightAndDisableHidesBoth(float x, float y)
    {
        var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        try
        {
            var view = root.GetComponent(ViewType);
            Vector3 player = new Vector3(4f, 3f, 0f);
            Vector3 origin = player + new Vector3(0f, 0.3f, 0f);
            Vector3 direction = new Vector3(x, y, 0f);
            ViewType.GetMethod("RenderTarget", Private).Invoke(view, new object[] { player, origin + direction * 10f });
            var arrow = (SpriteRenderer)ViewType.GetField("arrow", Private).GetValue(view);
            var icon = (SpriteRenderer)ViewType.GetField("chestIcon", Private).GetValue(view);
            Assert.That(Vector3.Distance(root.transform.position, origin + direction * 1.2f), Is.LessThan(0.001f));
            Assert.That(Vector3.Dot(arrow.transform.rotation * Vector3.left, direction), Is.GreaterThan(0.999f));
            Assert.That(Quaternion.Angle(icon.transform.rotation, Quaternion.identity), Is.LessThan(0.01f));
            Assert.That(arrow.enabled && icon.enabled, Is.True);
            ((Behaviour)view).enabled = false;
            Assert.That(arrow.enabled || icon.enabled, Is.False);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
#endif
