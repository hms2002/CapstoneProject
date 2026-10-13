#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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

    [Test]
    public void BuffArrows_AuthoredOnlyOnAlcohol()
    {
        Type type = Type.GetType("UnityGAS.PuddleParticleVisual, Presentation", true);
        foreach (string element in new[] { "Alcohol", "Fire" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PuddleFolder + element + "Puddle.prefab");
            var view = prefab.GetComponentInChildren(type, true);
            var sprite = (Sprite)type.GetField("buffArrowSprite", PrivateInstance).GetValue(view);
            Assert.That(sprite != null, Is.EqualTo(element == "Alcohol"));
        }
    }

    [Test]
    public void BuffArrows_IgnoreSurfaceQuadScaleAndStayInsideRadius()
    {
        Type type = Type.GetType("UnityGAS.PuddleParticleVisual, Presentation", true);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PuddleFolder + "AlcoholPuddle.prefab");
        var instance = Own(Object.Instantiate(prefab));
        var view = instance.GetComponentInChildren(type, true);
        var update = type.GetMethod("Update", PrivateInstance);
        update.Invoke(view, null);
        var arrows = (SpriteRenderer[])type.GetField("buffArrows", PrivateInstance).GetValue(view);
        var ages = (float[])type.GetField("arrowAges", PrivateInstance).GetValue(view);
        float radius = instance.GetComponent<AlcoholPuddleArea>().GroundRadius;
        for (int sample = 0; sample < 20; sample++)
        {
            for (int i = 0; i < ages.Length; i++) ages[i] = sample / 20f * 0.85f;
            update.Invoke(view, null);
            foreach (var arrow in arrows)
            {
                Assert.That(arrow.transform.parent, Is.EqualTo(instance.transform));
                var line = arrow.GetComponent<LineRenderer>();
                Assert.That(line.enabled, Is.True);
                Assert.That(arrow.enabled, Is.False);
                Assert.That(line.endWidth, Is.EqualTo(0.045f).Within(0.0001f));
                Assert.That(line.startColor.a, Is.Zero);
                Assert.That(Vector2.Distance(arrow.transform.position, instance.transform.position) +
                    Vector3.Distance(line.GetPosition(0), line.GetPosition(1)) * 0.5f + line.endWidth,
                    Is.LessThan(radius));
            }
        }
        var lines = instance.GetComponentsInChildren<LineRenderer>();
        var contract = (IPuddleParticleVisual)view;
        contract.ApplyMode(PuddleAreaMode.AbsorbPreparing);
        foreach (var line in lines) Assert.That(line.enabled, Is.False);
        contract.ApplyMode(PuddleAreaMode.Ground);
        update.Invoke(view, null);
        Assert.That(instance.GetComponentsInChildren<LineRenderer>(), Is.EqualTo(lines));
        ((MonoBehaviour)view).enabled = false;
        foreach (var line in lines) Assert.That(line.enabled, Is.False);
    }

    [Test]
    public void BuffArrows_HideOnTransitionsAndReuseChildren()
    {
        Type type = Type.GetType("UnityGAS.PuddleParticleVisual, Presentation", true);
        GameObject owner = Own(new GameObject("BuffArrowTest"));
        var view = (MonoBehaviour)owner.AddComponent(type);
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Sprites/UI/DownArrow.png");
        type.GetField("buffArrowSprite", PrivateInstance).SetValue(view, sprite);
        var update = type.GetMethod("Update", PrivateInstance);
        var contract = (IPuddleParticleVisual)view;
        contract.SetSurfaceRadius(2);
        update.Invoke(view, null);
        SpriteRenderer[] arrows = owner.GetComponentsInChildren<SpriteRenderer>();
        Assert.That(arrows.Length, Is.EqualTo(6));
        foreach (var arrow in arrows) Assert.That(arrow.enabled && arrow.flipY, Is.True);
        foreach (var mode in new[] { PuddleAreaMode.Igniting, PuddleAreaMode.AbsorbPreparing,
                     PuddleAreaMode.AbsorbProjectile, PuddleAreaMode.Consumed })
        {
            contract.ApplyMode(mode);
            foreach (var arrow in arrows) Assert.That(arrow.enabled, Is.False);
            contract.ApplyMode(PuddleAreaMode.Ground);
            update.Invoke(view, null);
            Assert.That(owner.GetComponentsInChildren<SpriteRenderer>(), Is.EqualTo(arrows));
        }
        contract.SetElementType(PuddleElementType.Fire);
        foreach (var arrow in arrows) Assert.That(arrow.enabled, Is.False);
        contract.SetElementType(PuddleElementType.Alcohol);
        update.Invoke(view, null);
        view.enabled = false;
        foreach (var arrow in arrows) Assert.That(arrow.enabled, Is.False);
    }

    [Test]
    public void BuffStreaks_RepairPresentationPropertiesWithoutResettingTheirClock()
    {
        Type type = Type.GetType("UnityGAS.PuddleParticleVisual, Presentation", true);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PuddleFolder + "AlcoholPuddle.prefab");
        var instance = Own(Object.Instantiate(prefab));
        var view = instance.GetComponentInChildren(type, true);
        var update = type.GetMethod("Update", PrivateInstance);
        update.Invoke(view, null);
        var arrows = (SpriteRenderer[])type.GetField("buffArrows", PrivateInstance).GetValue(view);
        var ages = (float[])type.GetField("arrowAges", PrivateInstance).GetValue(view);
        float age = ages[0];
        float lifetime = (float)type.GetField("buffArrowLifetime", PrivateInstance).GetValue(view);
        var line = arrows[0].GetComponent<LineRenderer>();
        arrows[0].enabled = true;
        arrows[0].transform.localScale = Vector3.one * 3f;
        line.enabled = false;
        line.startWidth = line.endWidth = 0.3f;
        type.GetField("streakWidth", PrivateInstance).SetValue(view, 0.07f);

        update.Invoke(view, null);

        Assert.That(arrows[0].enabled, Is.False);
        Assert.That(arrows[0].transform.localScale, Is.EqualTo(Vector3.one));
        Assert.That(line.enabled, Is.True);
        Assert.That(line.startWidth, Is.EqualTo(0.07f * 0.35f).Within(0.0001f));
        Assert.That(line.endWidth, Is.EqualTo(0.07f).Within(0.0001f));
        Assert.That(ages[0], Is.EqualTo((age + Time.deltaTime) % lifetime).Within(0.0001f));
    }

    [Test]
    public void GroundProperties_RepairExternalBlockWritesAndApplySettersImmediately()
    {
        var root = Own(new GameObject("PuddlePropertyOwner"));
        root.transform.position = Vector3.right * 1000f;
        var view = root.AddComponent(ViewType);
        var contract = (IPuddleShaderVisual)view;
        var anchor = Own(new GameObject("PuddlePropertyAnchor"));
        anchor.transform.position = root.transform.position + Vector3.right * 5f;
        contract.SetElementType(PuddleElementType.Fire);
        contract.SetRadii(2f, 0.3f);
        contract.SetAbsorbAnchor(anchor.transform);
        var renderer = root.GetComponent<MeshRenderer>();
        var expected = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(expected);
        float timeOffset = expected.GetFloat("_TimeOffset");
        int sentinel = Shader.PropertyToID("_PuddleExternalColor");
        var external = new MaterialPropertyBlock();
        external.SetColor(sentinel, Color.magenta);
        renderer.SetPropertyBlock(external);

        ViewType.GetMethod("Update", PrivateInstance).Invoke(view, null);

        renderer.GetPropertyBlock(external);
        Assert.That(external.GetColor(sentinel), Is.EqualTo(Color.magenta));
        Assert.That(external.GetFloat("_ElementType"), Is.EqualTo(1f));
        Assert.That(external.GetFloat("_Radius"), Is.EqualTo(2f));
        Assert.That(external.GetFloat("_IgnitionProgress"), Is.EqualTo(1f));
        Assert.That(external.GetFloat("_TimeOffset"), Is.EqualTo(timeOffset));
        Assert.That(external.HasFloat("_Mode") && external.HasFloat("_AbsorbProgress"), Is.True);
        Assert.That(external.GetFloat("_Mode"), Is.EqualTo((float)PuddleAreaMode.Ground));
        Assert.That(external.GetVector("_AbsorbDirection"), Is.EqualTo(new Vector4(1f, 0f, 0f, 0f)));

        anchor.transform.position = root.transform.position + Vector3.up * 5f;
        ViewType.GetMethod("Update", PrivateInstance).Invoke(view, null);
        renderer.GetPropertyBlock(external);
        Assert.That(external.GetVector("_AbsorbDirection"), Is.EqualTo(new Vector4(0f, 1f, 0f, 0f)));
        contract.SetElementType(PuddleElementType.Alcohol);
        contract.SetMode(PuddleAreaMode.Igniting);
        contract.SetIgnitionProgress(0.35f);
        contract.SetRadii(2.5f, 0.3f);
        renderer.GetPropertyBlock(external);
        Assert.That(external.GetFloat("_ElementType"), Is.Zero);
        Assert.That(external.GetFloat("_Mode"), Is.EqualTo((float)PuddleAreaMode.Igniting));
        Assert.That(external.GetFloat("_IgnitionProgress"), Is.EqualTo(0.35f));
        Assert.That(external.GetFloat("_Radius"), Is.EqualTo(2.5f));
        Assert.That(external.GetColor(sentinel), Is.EqualTo(Color.magenta));
    }

    [Test]
    public void GroundUpdate_ObservesParentMovementAndRepairsLocalScale()
    {
        var parent = Own(new GameObject("PuddleMovingParent"));
        parent.transform.position = Vector3.right * 2000f;
        var root = new GameObject("PuddleMovingSurface");
        root.transform.SetParent(parent.transform, false);
        var view = root.AddComponent(ViewType);
        var wall = Own(new GameObject("PuddleClipWall"));
        wall.layer = 30;
        wall.transform.position = parent.transform.position + Vector3.right * 0.75f;
        wall.AddComponent<BoxCollider2D>().size = new Vector2(0.1f, 10f);
        Physics2D.SyncTransforms();
        ViewType.GetField("useWallClipping", PrivateInstance).SetValue(view, true);
        ViewType.GetField("wallClipLayers", PrivateInstance).SetValue(view, (LayerMask)(1 << 30));
        ((IPuddleShaderVisual)view).SetRadii(2f, 0.3f);
        Mesh mesh = root.GetComponent<MeshFilter>().sharedMesh;
        float nearWall = mesh.vertices[1].x;
        Vector3 scale = root.transform.localScale;
        parent.transform.position += Vector3.left;
        parent.transform.localScale = Vector3.one * 2f;
        root.transform.localScale = Vector3.one * 7f;
        Physics2D.SyncTransforms();

        ViewType.GetMethod("Update", PrivateInstance).Invoke(view, null);

        Assert.That(mesh.vertices[1].x, Is.GreaterThan(nearWall));
        Assert.That(root.transform.localScale, Is.EqualTo(scale));
        Assert.That(root.transform.lossyScale, Is.EqualTo(scale * 2f));
    }

    [Test]
    public void ShaderPixels_MatchOriginalForGroundIgnitionAndBothProjectilePaths()
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            Assert.Ignore("Pixel comparison requires a graphics device.");

        string fixture = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs", "SessionLogs",
            "2026-10-07-frame-performance", "puddle-shader-before.txt"));
        Assert.That(File.Exists(fixture), Is.True, "The archived original shader is the visual baseline.");
        string original = File.ReadAllText(fixture).Replace("Shader \"Custom/PuddleShaderVisual\"",
            "Shader \"Hidden/Tests/PuddleShaderBefore\"");
        var template = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/M_PuddleShaderVisual.mat");
        Assert.That(template, Is.Not.Null);
        bool previousAsyncCompilation = ShaderUtil.allowAsyncCompilation;
        Vector4 previousTime = Shader.GetGlobalVector("_Time");
        Shader baseline = null;
        Material before = null;
        Material after = null;
        Mesh quad = null;
        RenderTexture target = null;
        Texture2D readback = null;
        try
        {
            ShaderUtil.allowAsyncCompilation = false;
            baseline = ShaderUtil.CreateShaderAsset(original, true);
            Assert.That(baseline, Is.Not.Null);
            before = new Material(template) { shader = baseline };
            after = new Material(template);
            ShaderUtil.CompilePass(before, 0, true);
            ShaderUtil.CompilePass(after, 0, true);
            Assert.That(ShaderUtil.ShaderHasError(baseline) || ShaderUtil.ShaderHasError(after.shader), Is.False);
            quad = new Mesh
            {
                vertices = new[] { Vector3.zero, Vector3.right, new Vector3(1f, 1f, 0f), Vector3.up },
                uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up },
                triangles = new[] { 0, 1, 2, 0, 2, 3 }
            };
            target = new RenderTexture(128, 128, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Assert.That(target.Create(), Is.True);
            readback = new Texture2D(128, 128, TextureFormat.RGBA32, false, true);
            var states = new[]
            {
                (PuddleAreaMode.Ground, 0f, 0f, 0f, false),
                (PuddleAreaMode.Ground, 1f, 1f, 0f, false),
                (PuddleAreaMode.Igniting, 0f, 0.25f, 0f, false),
                (PuddleAreaMode.Igniting, 0f, 0.75f, 0f, false),
                (PuddleAreaMode.AbsorbPreparing, 0f, 0f, 0f, false),
                (PuddleAreaMode.AbsorbPreparing, 0f, 0f, 0.5f, false),
                (PuddleAreaMode.AbsorbPreparing, 0f, 0f, 0.79f, false),
                (PuddleAreaMode.AbsorbPreparing, 0f, 0f, 0.95f, false),
                (PuddleAreaMode.AbsorbPreparing, 1f, 1f, 1f, false),
                (PuddleAreaMode.AbsorbProjectile, 0f, 0f, 0.25f, false),
                (PuddleAreaMode.AbsorbProjectile, 0f, 0f, 1f, false),
                (PuddleAreaMode.AbsorbProjectile, 1f, 1f, 0.5f, false),
                (PuddleAreaMode.AbsorbPreparing, 0f, 0f, 0.5f, true),
                (PuddleAreaMode.AbsorbPreparing, 1f, 1f, 0.95f, true)
            };
            const int channelTolerance = 1; // One RGBA8 quantization step; no mismatched pixels above it.
            foreach (var state in states)
            {
                var (mode, element, ignition, progress, authoredProjectile) = state;
                foreach (Material material in new[] { before, after })
                {
                    material.SetFloat("_Mode", (float)(authoredProjectile ? PuddleAreaMode.Ground : mode));
                    material.SetFloat("_ElementType", element);
                    material.SetFloat("_IgnitionProgress", ignition);
                    material.SetFloat("_AbsorbProgress", authoredProjectile ? 0f : progress);
                    material.SetFloat("_TimeOffset", 2.75f);
                    material.SetVector("_AbsorbDirection", new Vector4(0.6f, 0.8f, 0f, 0f));
                }
                Color32[] expected = RenderShaderPixels(before, quad, target, readback);
                Color32[] actual = RenderShaderPixels(after, quad, target, readback);
                int visiblePixels = 0;
                int maximumDelta = 0;
                for (int i = 0; i < expected.Length; i++)
                {
                    if (expected[i].a > 16) visiblePixels++;
                    maximumDelta = Mathf.Max(maximumDelta, Mathf.Abs(expected[i].r - actual[i].r));
                    maximumDelta = Mathf.Max(maximumDelta, Mathf.Abs(expected[i].g - actual[i].g));
                    maximumDelta = Mathf.Max(maximumDelta, Mathf.Abs(expected[i].b - actual[i].b));
                    maximumDelta = Mathf.Max(maximumDelta, Mathf.Abs(expected[i].a - actual[i].a));
                }
                string label = $"mode={mode},element={element},ignition={ignition},progress={progress},authored={authoredProjectile}";
                Assert.That(visiblePixels, Is.GreaterThan(32), $"A blank baseline cannot verify equivalence: {label}");
                Assert.That(maximumDelta, Is.LessThanOrEqualTo(channelTolerance), label);
                TestContext.WriteLine($"PUDDLEPIXELS,{label},visiblePixels={visiblePixels},maximumChannelDelta={maximumDelta},tolerance={channelTolerance}");
            }
        }
        finally
        {
            Shader.SetGlobalVector("_Time", previousTime);
            ShaderUtil.allowAsyncCompilation = previousAsyncCompilation;
            if (readback != null) Object.DestroyImmediate(readback);
            if (target != null)
            {
                target.Release();
                Object.DestroyImmediate(target);
            }
            if (quad != null) Object.DestroyImmediate(quad);
            if (before != null) Object.DestroyImmediate(before);
            if (after != null) Object.DestroyImmediate(after);
            if (baseline != null) Object.DestroyImmediate(baseline);
        }
    }

    private static Color32[] RenderShaderPixels(Material material, Mesh quad, RenderTexture target, Texture2D readback)
    {
        RenderTexture previousTarget = RenderTexture.active;
        bool previousSrgbWrite = GL.sRGBWrite;
        GL.PushMatrix();
        try
        {
            RenderTexture.active = target;
            GL.sRGBWrite = false;
            GL.Clear(true, true, Color.clear);
            GL.LoadOrtho();
            Shader.SetGlobalVector("_Time", new Vector4(0.3125f, 6.25f, 12.5f, 18.75f));
            Assert.That(material.SetPass(0), Is.True);
            Graphics.DrawMeshNow(quad, Matrix4x4.identity);
            readback.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0);
            readback.Apply();
            return readback.GetPixels32();
        }
        finally
        {
            GL.PopMatrix();
            GL.sRGBWrite = previousSrgbWrite;
            RenderTexture.active = previousTarget;
        }
    }

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
