#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityGAS;
using Object = UnityEngine.Object;

/// <summary>Verifies authored puddle/projectile cross-scaling, animation playback and cleanup without changing combat rules.</summary>
public sealed class PuddleProjectileVisualPlayModeTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string PuddleFolder = "Assets/_Project/Prefabs/Map/Puddles/";
    private readonly List<GameObject> owned = new();
    private float previousTimeScale;
    private static Type ViewType => Type.GetType("UnityGAS.PuddleShaderVisual, Presentation", true);

    [SetUp]
    public void SetUp()
    {
        previousTimeScale = Time.timeScale;
        Time.timeScale = 1f;
        Type.GetType("PrewarmTraceRuntime, Editor")?.GetMethod("ResetSession", BindingFlags.Static | BindingFlags.NonPublic)?.Invoke(null, null);
        Own(new GameObject("PuddleVisualTestManager")).AddComponent<PuddleManager>();
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = owned.Count - 1; i >= 0; i--)
        {
            if (owned[i] == null) continue;
            // Existing puddle visuals allocate meshes; avoid leaking those test instances.
            var meshes = new List<Mesh>();
            foreach (var filter in owned[i].GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh != null && !AssetDatabase.Contains(filter.sharedMesh))
                    meshes.Add(filter.sharedMesh);
            Object.DestroyImmediate(owned[i]);
            foreach (var mesh in meshes)
                if (mesh != null) Object.DestroyImmediate(mesh);
        }
        owned.Clear();
        Time.timeScale = previousTimeScale;
    }

    private GameObject Own(GameObject value) { owned.Add(value); return value; }
    private static Transform Projectile(Component view) =>
        (Transform)ViewType.GetField("projectileVisualRoot", PrivateInstance).GetValue(view);

    [TestCase("Fire")]
    [TestCase("Alcohol")]
    public void AuthoredPuddle_ContainsInactiveNestedAnimationPrefab_WithoutGameplayComponents(string element)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PuddleFolder + element + "Puddle.prefab");
        var view = prefab.GetComponentInChildren(ViewType, true);
        Transform projectile = Projectile(view);
        Assert.That(projectile, Is.Not.Null);
        Assert.That(projectile.parent, Is.EqualTo(prefab.transform), "Shader scaling must not affect the animation root.");
        Assert.That(projectile.gameObject.activeSelf, Is.False);
        Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(projectile), Is.Not.Null);
        Assert.That(projectile.GetComponentsInChildren<Collider2D>(true), Is.Empty);
        Assert.That(projectile.GetComponentsInChildren<Rigidbody2D>(true), Is.Empty);
        Assert.That(projectile.GetComponentsInChildren<MonoBehaviour>(true), Is.Empty);
        var sprite = projectile.Find("Sprite");
        Assert.That(sprite.GetComponent<SpriteRenderer>().sprite, Is.Not.Null);
        var animator = sprite.GetComponent<Animator>();
        Assert.That(animator.runtimeAnimatorController, Is.Not.Null);
        Assert.That(animator.applyRootMotion, Is.False);
        var clips = animator.runtimeAnimatorController.animationClips;
        Assert.That(clips, Has.Length.EqualTo(1));
        Assert.That(clips[0].isLooping, Is.True);
        var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clips[0]);
        Assert.That(bindings, Has.Length.EqualTo(1));
        Assert.That(bindings[0].path, Is.Empty);
        var keys = AnimationUtility.GetObjectReferenceCurve(clips[0], bindings[0]);
        Assert.That(keys.Length, Is.EqualTo(8));
        foreach (var key in keys) Assert.That(key.value, Is.Not.Null);
    }

    [TestCase("Fire", "Lavaball_")]
    [TestCase("Alcohol", "Alcholeball_")]
    public void ProjectileVisual_PlaysAuthoredFrames_WithoutMovingTheRotationRoot(string element, string spritePrefix)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Prefabs/VFX/Puddles/PF_PuddleAbsorb_{element}.prefab");
        var visual = Own(Object.Instantiate(prefab));
        var animator = visual.GetComponentInChildren<Animator>(true);
        var renderer = animator.GetComponent<SpriteRenderer>();
        visual.transform.rotation = Quaternion.Euler(0f, 0f, 73f);
        animator.Update(0f);
        Sprite first = renderer.sprite;
        animator.Update(0.12f);
        Assert.That(renderer.sprite, Is.Not.EqualTo(first));
        Assert.That(renderer.sprite.name, Does.StartWith(spritePrefix));
        Assert.That(Quaternion.Angle(visual.transform.rotation, Quaternion.Euler(0f, 0f, 73f)), Is.LessThan(0.01f));
    }

    [Test]
    public void VisualModes_RestoreGroundAndHideConsumed_AndUseWorldSpaceFacing()
    {
        var root = Own(new GameObject("PuddleVisualTest"));
        root.transform.rotation = Quaternion.Euler(0f, 0f, 35f);
        var surface = new GameObject("Surface");
        surface.transform.SetParent(root.transform, false);
        var view = (Behaviour)surface.AddComponent(ViewType);
        var projectile = new GameObject("Projectile");
        projectile.transform.SetParent(root.transform, false);
        ViewType.GetField("projectileVisualRoot", PrivateInstance).SetValue(view, projectile.transform);
        var anchor = Own(new GameObject("Anchor"));
        anchor.transform.position = Vector3.up * 5f;
        var contract = (IPuddleShaderVisual)view;
        contract.SetAbsorbAnchor(anchor.transform);
        contract.SetMode(PuddleAreaMode.AbsorbPreparing);
        Assert.That(projectile.activeSelf, Is.True);
        Assert.That(projectile.transform.localScale, Is.EqualTo(Vector3.zero));
        Assert.That(Vector2.Dot(projectile.transform.right, Vector2.up), Is.GreaterThan(0.999f));
        Assert.That(surface.GetComponent<MeshRenderer>().enabled, Is.True);
        contract.SetMode(PuddleAreaMode.AbsorbProjectile);
        Assert.That(projectile.activeSelf, Is.True);
        Assert.That(surface.GetComponent<MeshRenderer>().enabled, Is.False);
        Assert.That(Vector2.Dot(projectile.transform.right, Vector2.up), Is.GreaterThan(0.999f));
        anchor.transform.position = Vector3.left * 5f;
        ViewType.GetMethod("LateUpdate", PrivateInstance).Invoke(view, null);
        Assert.That(Vector2.Dot(projectile.transform.right, Vector2.left), Is.GreaterThan(0.999f));
        view.enabled = false;
        Assert.That(projectile.activeSelf, Is.False);
        view.enabled = true;
        Assert.That(projectile.activeSelf, Is.True);
        contract.SetMode(PuddleAreaMode.Ground);
        Assert.That(projectile.activeSelf, Is.False);
        Assert.That(surface.GetComponent<MeshRenderer>().enabled, Is.True);
        contract.SetMode(PuddleAreaMode.Consumed);
        Assert.That(projectile.activeSelf, Is.False);
        Assert.That(surface.GetComponent<MeshRenderer>().enabled, Is.False);
    }

    [TestCase("Fire")]
    [TestCase("Alcohol")]
    public void Preparing_ShrinksCachedGroundMeshAndGrowsAuthoredScale_WithoutShaderMorph(string element)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PuddleFolder + element + "Puddle.prefab");
        var actor = Own(Object.Instantiate(prefab, Vector3.right * 100f, Quaternion.identity));
        var view = actor.GetComponentInChildren(ViewType, true);
        var contract = (IPuddleShaderVisual)view;
        var projectile = Projectile(view);
        Vector3 projectileScale = projectile.localScale;
        ViewType.GetField("useWallClipping", PrivateInstance).SetValue(view, true);
        ViewType.GetField("wallClipLayers", PrivateInstance).SetValue(view, (LayerMask)Physics2D.DefaultRaycastLayers);
        contract.SetRadii(2f, 0.25f);
        Mesh mesh = view.GetComponent<MeshFilter>().sharedMesh;
        Vector3[] vertices = mesh.vertices;
        Assert.That(vertices.Length, Is.GreaterThan(4));
        Vector3 groundScale = view.transform.localScale;

        contract.SetMode(PuddleAreaMode.AbsorbPreparing);
        Assert.That(view.transform.localScale, Is.EqualTo(groundScale));
        Assert.That(projectile.localScale, Is.EqualTo(Vector3.zero));
        var block = new MaterialPropertyBlock();
        view.GetComponent<MeshRenderer>().GetPropertyBlock(block);
        Assert.That(block.GetFloat("_Mode"), Is.EqualTo((float)PuddleAreaMode.Ground));
        Assert.That(block.GetFloat("_AbsorbProgress"), Is.Zero);

        SetPreparingProgress(view, 0.5f);
        Assert.That(Vector3.Distance(view.transform.localScale, groundScale * 0.5f), Is.LessThan(0.0001f));
        Assert.That(Vector3.Distance(projectile.localScale, projectileScale * 0.5f), Is.LessThan(0.0001f));
        CollectionAssert.AreEqual(vertices, mesh.vertices, "Preparing must keep the cached clipped shape.");
        Assert.That(ViewType.GetField("wallClipMeshDirty", PrivateInstance).GetValue(view), Is.True,
            "Preparing must not rebuild the dirty mesh; ground restoration will rebuild it.");

        SetPreparingProgress(view, 1f);
        Assert.That(view.transform.localScale, Is.EqualTo(Vector3.zero));
        Assert.That(projectile.localScale, Is.EqualTo(projectileScale));
        contract.SetMode(PuddleAreaMode.Ground);
        Assert.That(projectile.gameObject.activeSelf, Is.False);
        Assert.That(view.transform.localScale, Is.EqualTo(groundScale));
        Assert.That(projectile.localScale, Is.EqualTo(projectileScale));
        contract.SetMode(PuddleAreaMode.AbsorbPreparing);
        SetPreparingProgress(view, 0.5f);
        contract.SetMode(PuddleAreaMode.AbsorbProjectile);
        Assert.That(projectile.localScale, Is.EqualTo(projectileScale), "Early flight must complete the growth, not restart it.");
        Assert.That(view.GetComponent<MeshRenderer>().enabled, Is.False);
        actor.SetActive(false);
    }

    [Test]
    public void Preparing_RestoresNonUnitAuthoredScaleAcrossDisableAndRepeatedAbsorption()
    {
        var root = Own(new GameObject("AuthoredScalePuddleTest"));
        var surface = new GameObject("Surface");
        surface.transform.SetParent(root.transform, false);
        var view = (Behaviour)surface.AddComponent(ViewType);
        var projectile = new GameObject("Projectile");
        projectile.transform.SetParent(root.transform, false);
        Vector3 authoredScale = new Vector3(2f, 3f, 1f);
        projectile.transform.localScale = authoredScale;
        ViewType.GetField("projectileVisualRoot", PrivateInstance).SetValue(view, projectile.transform);
        var contract = (IPuddleShaderVisual)view;
        for (int cycle = 0; cycle < 2; cycle++)
        {
            contract.SetMode(PuddleAreaMode.AbsorbPreparing);
            SetPreparingProgress(view, 0.5f);
            Assert.That(projectile.transform.localScale, Is.EqualTo(authoredScale * 0.5f));
            view.enabled = false;
            Assert.That(projectile.activeSelf, Is.False);
            Assert.That(projectile.transform.localScale, Is.EqualTo(authoredScale));
            view.enabled = true;
            Assert.That(projectile.transform.localScale, Is.EqualTo(authoredScale * 0.5f));
            contract.SetMode(PuddleAreaMode.Ground);
            Assert.That(projectile.transform.localScale, Is.EqualTo(authoredScale));
        }
    }

    private static void SetPreparingProgress(Component view, float progress)
    {
        float duration = (float)ViewType.GetField("absorbVisualDurationSeconds", PrivateInstance).GetValue(view);
        ViewType.GetField("absorbElapsedSeconds", PrivateInstance).SetValue(view, duration * progress);
        ViewType.GetMethod("ApplyVisualScale", PrivateInstance).Invoke(view, null);
        ViewType.GetMethod("ApplyProperties", PrivateInstance).Invoke(view, null);
    }

    [Test]
    public void MissingProjectileReference_PreservesLegacyShaderPresentation()
    {
        var root = Own(new GameObject("LegacyPuddleVisualTest"));
        var contract = (IPuddleShaderVisual)root.AddComponent(ViewType);
        contract.SetMode(PuddleAreaMode.AbsorbProjectile);
        Assert.That(root.GetComponent<MeshRenderer>().enabled, Is.True);
    }

    [UnityTest]
    public IEnumerator BothPuddles_GrowDuringPreparation_AndRestoreOrConsumeWithoutNewObjects()
    {
        foreach (string element in new[] { "Fire", "Alcohol" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PuddleFolder + element + "Puddle.prefab");
            var actor = Own(Object.Instantiate(prefab, Vector3.right * (element == "Fire" ? 100f : 200f), Quaternion.identity));
            var puddle = actor.GetComponent<PuddleAreaBase>();
            typeof(PuddleAreaBase).GetField("absorbGatherDelaySeconds", PrivateInstance).SetValue(puddle, 0.05f);
            var view = actor.GetComponentInChildren(ViewType, true);
            var projectile = Projectile(view);
            int childCount = actor.GetComponentsInChildren<Transform>(true).Length;
            var anchor = Own(new GameObject("AbsorbAnchor"));
            anchor.transform.position = actor.transform.position + Vector3.right * 100f;
            puddle.EnterAbsorbProjectile(anchor.transform, 0.1f, _ => { });
            Assert.That(puddle.Mode, Is.EqualTo(PuddleAreaMode.AbsorbPreparing));
            Assert.That(projectile.gameObject.activeSelf, Is.True);
            Assert.That(projectile.localScale, Is.EqualTo(Vector3.zero));
            float deadline = Time.realtimeSinceStartup + 2f;
            while (puddle.Mode == PuddleAreaMode.AbsorbPreparing && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(puddle.Mode, Is.EqualTo(PuddleAreaMode.AbsorbProjectile));
            Assert.That(projectile.gameObject.activeSelf, Is.True);
            Assert.That(view.GetComponent<MeshRenderer>().enabled, Is.False);
            Assert.That(puddle.CanApplyProjectileEffect, Is.True);
            var groundCollider = (Collider2D)typeof(PuddleAreaBase).GetField("groundCollider", PrivateInstance).GetValue(puddle);
            var projectileCollider = (Collider2D)typeof(PuddleAreaBase).GetField("projectileCollider", PrivateInstance).GetValue(puddle);
            Assert.That(groundCollider.enabled, Is.False);
            Assert.That(projectileCollider.enabled, Is.True);
            puddle.CancelAbsorbToGround();
            Assert.That(projectile.gameObject.activeSelf, Is.False);
            Assert.That(view.GetComponent<MeshRenderer>().enabled, Is.True);
            Assert.That(groundCollider.enabled, Is.True);
            Assert.That(projectileCollider.enabled, Is.False);
            typeof(PuddleAreaBase).GetField("absorbGatherDelaySeconds", PrivateInstance).SetValue(puddle, 0f);
            puddle.EnterAbsorbProjectile(anchor.transform, 0.1f, _ => { });
            Assert.That(projectile.gameObject.activeSelf, Is.True);
            puddle.MarkConsumed();
            Assert.That(projectile.gameObject.activeSelf, Is.False);
            Assert.That(view.GetComponent<MeshRenderer>().enabled, Is.False);
            Assert.That(groundCollider.enabled || projectileCollider.enabled, Is.False);
            Assert.That(actor.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(childCount));
            actor.SetActive(false);
        }
    }
}
#endif
