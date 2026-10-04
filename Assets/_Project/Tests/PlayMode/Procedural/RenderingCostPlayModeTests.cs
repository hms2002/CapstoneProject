#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityGAS;
using Object = UnityEngine.Object;

/// <summary>Measures production rendering-update CPU costs without changing gameplay, and reports GPU timing availability separately.</summary>
public sealed class RenderingCostPlayModeTests
{
    private readonly List<GameObject> instances = new();
    private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

    [TearDown]
    public void Cleanup()
    {
        foreach (var instance in instances)
            if (instance != null) Object.DestroyImmediate(instance);
        instances.Clear();
    }

    private static void Measure(string label, Action tick)
    {
        for (int warm = 0; warm < 30; warm++) tick();
        var samples = new double[21];
        for (int sample = 0; sample < samples.Length; sample++)
        {
            long start = Stopwatch.GetTimestamp();
            for (int frame = 0; frame < 120; frame++) tick();
            samples[sample] = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency / 120;
        }
        Array.Sort(samples);
        TestContext.WriteLine(FormattableString.Invariant(
            $"RENDERCPU,{label},median_ms={samples[10]:F6},p95_batch_ms={samples[19]:F6}"));
    }

    [TestCase(1, false, false)]
    [TestCase(20, false, false)]
    [TestCase(50, false, false)]
    [TestCase(20, true, false)]
    [TestCase(20, true, true)]
    public void TelegraphUpdateCost(int count, bool geometryUpdate, bool moving)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/VFX/Telegraphs/AttackTelegraphView.prefab");
        var style = AssetDatabase.LoadAssetAtPath<AttackTelegraphStyle>("Assets/_Project/Data/Abilities/TelegraphStyles/Monsters/AttackTelegraphStyle.asset");
        Assert.IsNotNull(prefab);
        Assert.IsNotNull(style);
        var type = Type.GetType("UnityGAS.AttackTelegraphView, Presentation", true);
        var ticks = new Action[count];
        var geometry = new Action<AttackTelegraphSpec>[count];
        var specs = new AttackTelegraphSpec[count];
        for (int i = 0; i < count; i++)
        {
            var go = Object.Instantiate(prefab);
            instances.Add(go);
            var view = go.GetComponent(type);
            var spec = i % 2 == 0
                ? AttackTelegraphSpec.CreateRectangle(Vector3.zero, new Vector2(8, 3), 0, 10, style)
                : AttackTelegraphSpec.CreateCircle(Vector3.zero, 6, 10, style);
            spec.useMeshOutline = true;
            spec.wallClipSampleCount = 48;
            type.GetMethod("Show").Invoke(view, new object[] { spec, style });
            Assert.IsTrue((bool)type.GetProperty("IsVisible").GetValue(view));
            Assert.IsNotNull(go.GetComponentInChildren<MeshFilter>());
            ticks[i] = (Action)Delegate.CreateDelegate(typeof(Action), view, type.GetMethod("Update", PrivateInstance));
            geometry[i] = (Action<AttackTelegraphSpec>)Delegate.CreateDelegate(typeof(Action<AttackTelegraphSpec>), view, type.GetMethod("UpdateGeometry"));
            specs[i] = spec;
        }
        int step = 0;
        Measure($"telegraph,count={count},geometryUpdate={geometryUpdate},moving={moving}", () =>
        {
            step++;
            for (int i = 0; i < count; i++)
            {
                if (moving)
                {
                    specs[i].center = new Vector3((step % 120) * .03f, i * .1f, 0);
                    specs[i].rotationDeg = step % 360;
                }
                if (geometryUpdate) geometry[i](specs[i]);
                ticks[i]();
            }
        });
        int rebuilds = 0, poseReuses = 0;
        foreach (var instance in instances)
        {
            var meshView = instance.GetComponent(Type.GetType("UnityGAS.AttackTelegraphWallClippedMeshView, Presentation", true));
            rebuilds += Rebuilds(meshView);
            poseReuses += (int)meshView.GetType().GetProperty("DiagnosticPoseReuseCount").GetValue(meshView);
        }
        TestContext.WriteLine($"RENDERCOUNTS,count={count},moving={moving},meshRebuildsIncludingSetup={rebuilds},poseReusesIncludingWarmup={poseReuses}");
    }

    [TestCase(1)]
    [TestCase(20)]
    [TestCase(50)]
    public void GroundPuddleUpdateCost(int count)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Map/Puddles/AlcoholPuddle.prefab");
        Assert.IsNotNull(prefab);
        var type = Type.GetType("UnityGAS.PuddleShaderVisual, Presentation", true);
        var ticks = new Action[count];
        for (int i = 0; i < count; i++)
        {
            // Clone only the authored visual subtree: no puddle damage/lifetime or global services.
            var source = prefab.GetComponentInChildren(type, true);
            Assert.IsNotNull(source);
            var go = Object.Instantiate(source.gameObject);
            instances.Add(go);
            go.SetActive(true);
            var view = go.GetComponent(type);
            // The projectile is a sibling in the authored prefab, outside this isolated clone.
            type.GetField("projectileVisualRoot", PrivateInstance).SetValue(view, null);
            type.GetMethod("SetMode").Invoke(view, new object[] { PuddleAreaMode.Ground });
            Assert.IsNotNull(go.GetComponent<MeshRenderer>().sharedMaterial);
            ticks[i] = (Action)Delegate.CreateDelegate(typeof(Action), view, type.GetMethod("Update", PrivateInstance));
        }
        Measure($"ground-puddle,count={count}", () =>
        {
            for (int i = 0; i < count; i++) ticks[i]();
        });
    }

    [UnityTest]
    public IEnumerator ReportGraphicsTimingAvailability()
    {
        TestContext.WriteLine($"GRAPHICS,device={SystemInfo.graphicsDeviceName},api={SystemInfo.graphicsDeviceType},cpu={SystemInfo.processorType},timingEnabled={FrameTimingManager.IsFeatureEnabled()}");
        var timing = new FrameTiming[1];
        int valid = 0;
        for (int frame = 0; frame < 60; frame++)
        {
            FrameTimingManager.CaptureFrameTimings();
            yield return null;
            if (FrameTimingManager.GetLatestTimings(1, timing) > 0 && timing[0].gpuFrameTime > 0) valid++;
        }
        TestContext.WriteLine($"GRAPHICS,positiveGpuTimingSamples={valid},note=availability-only-not-gameplay-GPU-benchmark");
    }

    private Component CreateMeshView()
    {
        var go = new GameObject("TelegraphGeometryTest");
        instances.Add(go);
        return go.AddComponent(Type.GetType("UnityGAS.AttackTelegraphWallClippedMeshView, Presentation", true));
    }

    private static void ShowMesh(Component view, AttackTelegraphSpec spec, AttackTelegraphStyle style, float progress)
        => view.GetType().GetMethod("ShowOrUpdate").Invoke(view, new object[] { spec, style, null, progress });

    private static void StyleMesh(Component view, AttackTelegraphStyle style, float progress)
        => view.GetType().GetMethod("ApplyStyle").Invoke(view, new object[] { style, progress });

    private static int Rebuilds(Component view)
        => (int)view.GetType().GetProperty("DiagnosticMeshRebuildCount").GetValue(view);

    private static Vector3[] Vertices(Component view) => view.GetComponent<MeshFilter>().sharedMesh.vertices;

    [TestCase(AttackTelegraphShape.Rectangle, false)]
    [TestCase(AttackTelegraphShape.Circle, false)]
    [TestCase(AttackTelegraphShape.Rectangle, true)]
    [TestCase(AttackTelegraphShape.Circle, true)]
    public void MovingPoseMatchesForcedRebuildAndPreservesWorldOutline(AttackTelegraphShape shape, bool uniformParent)
    {
        var view = CreateMeshView();
        var reference = CreateMeshView();
        var parent = new GameObject("MovingWarningOwner");
        instances.Add(parent);
        if (uniformParent)
        {
            parent.transform.localScale = Vector3.one * 2;
            parent.transform.rotation = Quaternion.Euler(0, 0, 17);
            view.transform.SetParent(parent.transform, false);
            reference.transform.SetParent(parent.transform, false);
        }
        var spec = AttackTelegraphSpec.CreateRectangle(Vector3.zero, new Vector2(8, 3), 37, 10);
        spec.shape = shape;
        spec.wallClipSampleCount = 48;
        ShowMesh(view, spec, null, .5f);
        int initialRebuilds = Rebuilds(view);
        for (int step = 1; step <= 30; step++)
        {
            spec.center = new Vector3(step * .07f, -step * .11f, 0);
            spec.rotationDeg = 37 + step * 13;
            if (uniformParent)
            {
                parent.transform.position = new Vector3(step * .03f, 2, 0);
                parent.transform.rotation = Quaternion.Euler(0, 0, step * 3);
            }
            ShowMesh(view, spec, null, .5f);
            reference.GetType().GetMethod("HideImmediate").Invoke(reference, null);
            ShowMesh(reference, spec, null, .5f);
            AssertWorldGeometryEqual(reference, view);
        }
        Assert.AreEqual(initialRebuilds, Rebuilds(view), "Pose-only changes must not upload mesh vertices.");
        Assert.AreEqual(30, (int)view.GetType().GetProperty("DiagnosticPoseReuseCount").GetValue(view));
        parent.transform.localScale = new Vector3(-2, 3, 1);
        if (!uniformParent)
        {
            view.transform.SetParent(parent.transform, true);
            reference.transform.SetParent(parent.transform, true);
        }
        StyleMesh(view, null, .5f);
        reference.GetType().GetMethod("HideImmediate").Invoke(reference, null);
        ShowMesh(reference, spec, null, .5f);
        AssertWorldGeometryEqual(reference, view);
        Assert.Greater(Rebuilds(view), initialRebuilds);
    }

    private static void AssertWorldGeometryEqual(Component expected, Component actual)
    {
        Vector3[] a = Vertices(expected), b = Vertices(actual);
        Assert.AreEqual(a.Length, b.Length);
        for (int i = 0; i < a.Length; i++)
            Assert.Less(Vector3.Distance(expected.transform.TransformPoint(a[i]), actual.transform.TransformPoint(b[i])), .0001f);
        var expectedBorder = expected.GetComponent<LineRenderer>();
        var actualBorder = actual.GetComponent<LineRenderer>();
        Assert.AreEqual(expectedBorder.positionCount, actualBorder.positionCount);
        Assert.AreEqual(expectedBorder.widthMultiplier, actualBorder.widthMultiplier);
        for (int i = 0; i < expectedBorder.positionCount; i++)
            Assert.Less(Vector3.Distance(expectedBorder.GetPosition(i), actualBorder.GetPosition(i)), .0001f);
    }

    [TestCase(AttackTelegraphShape.Rectangle)]
    [TestCase(AttackTelegraphShape.Circle)]
    public void MovingGrowingFillMatchesForcedRebuild(AttackTelegraphShape shape)
    {
        var view = CreateMeshView();
        var reference = CreateMeshView();
        var style = ScriptableObject.CreateInstance<AttackTelegraphStyle>();
        try
        {
            style.scaleFillWithProgress = true;
            var spec = AttackTelegraphSpec.CreateRectangle(Vector3.zero, new Vector2(8, 3), 0, 10);
            spec.shape = shape;
            spec.wallClipSampleCount = 48;
            for (int step = 0; step <= 10; step++)
            {
                spec.center = new Vector3(step * .2f, -step * .1f, 0);
                spec.rotationDeg = step * 31;
                ShowMesh(view, spec, style, step / 10f);
                reference.GetType().GetMethod("HideImmediate").Invoke(reference, null);
                ShowMesh(reference, spec, style, step / 10f);
                AssertWorldGeometryEqual(reference, view);
            }
            Assert.AreEqual(11, Rebuilds(view));
        }
        finally { Object.DestroyImmediate(style); }
    }

    [TestCase(AttackTelegraphShape.Rectangle)]
    [TestCase(AttackTelegraphShape.Circle)]
    [TestCase(AttackTelegraphShape.Ring)]
    [TestCase(AttackTelegraphShape.Sector)]
    [TestCase(AttackTelegraphShape.Line)]
    public void StableGeometryReusesMeshButColorsAndPoolReuseWork(AttackTelegraphShape shape)
    {
        var style = ScriptableObject.CreateInstance<AttackTelegraphStyle>();
        try
        {
            style.blinkFrequency = 0;
            style.fillColorStart = Color.red;
            style.fillColorEnd = Color.blue;
            var spec = AttackTelegraphSpec.CreateRectangle(Vector3.zero, new Vector2(8, 3), 0, 10);
            spec.shape = shape;
            spec.innerDiameter = 2;
            spec.sectorAngleDeg = 90;
            spec.lineEnd = Vector3.right * 8;
            spec.wallClipSampleCount = 48;
            var view = CreateMeshView();
            ShowMesh(view, spec, style, 0);
            Vector3[] original = Vertices(view);
            Color firstColor = view.GetComponent<MeshRenderer>().sharedMaterial.color;
            int before = Rebuilds(view);
            for (int i = 0; i < 10; i++)
            {
                ShowMesh(view, spec, style, .5f);
                StyleMesh(view, style, .5f);
            }
            Assert.AreEqual(before, Rebuilds(view));
            CollectionAssert.AreEqual(original, Vertices(view));
            Assert.AreNotEqual(firstColor, view.GetComponent<MeshRenderer>().sharedMaterial.color);
            view.GetType().GetMethod("HideImmediate").Invoke(view, null);
            ShowMesh(view, spec, style, .5f);
            Assert.AreEqual(before + 1, Rebuilds(view));
            Assert.IsTrue(view.GetComponent<LineRenderer>().enabled);
            CollectionAssert.AreEqual(original, Vertices(view));
        }
        finally { Object.DestroyImmediate(style); }
    }

    [Test]
    public void GrowthGeometryAndParentChangesMatchFreshRebuild()
    {
        var style = ScriptableObject.CreateInstance<AttackTelegraphStyle>();
        try
        {
            style.scaleFillWithProgress = true;
            var view = CreateMeshView();
            var reference = CreateMeshView();
            var spec = AttackTelegraphSpec.CreateCircle(Vector3.zero, 6, 10);
            spec.wallClipSampleCount = 48;
            ShowMesh(view, spec, style, .1f);
            float smallWidth = view.GetComponent<MeshFilter>().sharedMesh.bounds.size.x;
            StyleMesh(view, style, .8f);
            Assert.Greater(view.GetComponent<MeshFilter>().sharedMesh.bounds.size.x, smallWidth);
            ShowMesh(reference, spec, style, .8f);
            CollectionAssert.AreEqual(Vertices(reference), Vertices(view));

            spec.shape = AttackTelegraphShape.Rectangle;
            spec.center = new Vector3(2, 3, 0);
            spec.size = new Vector2(9, 2);
            spec.rotationDeg = 30;
            ShowMesh(view, spec, style, .8f);
            ShowMesh(reference, spec, style, .8f);
            CollectionAssert.AreEqual(Vertices(reference), Vertices(view));

            var parent = new GameObject("TelegraphParent");
            instances.Add(parent);
            view.transform.SetParent(parent.transform, true);
            reference.transform.SetParent(parent.transform, true);
            int before = Rebuilds(view);
            parent.transform.localScale = new Vector3(-2, 3, 1);
            parent.transform.rotation = Quaternion.Euler(0, 0, 20);
            StyleMesh(view, style, .8f);
            reference.GetType().GetMethod("HideImmediate").Invoke(reference, null);
            ShowMesh(reference, spec, style, .8f);
            Assert.AreEqual(before + 1, Rebuilds(view));
            CollectionAssert.AreEqual(Vertices(reference), Vertices(view));
            Assert.AreEqual(reference.transform.position, view.transform.position);
        }
        finally { Object.DestroyImmediate(style); }
    }

    [Test]
    public void ClippedLineObservesMovingWallWithoutSpecChange()
    {
        var wall = new GameObject("MovingWall");
        instances.Add(wall);
        wall.layer = 30;
        wall.transform.position = new Vector3(3, 0, 0);
        wall.AddComponent<BoxCollider2D>().size = new Vector2(.2f, 10);
        Physics2D.SyncTransforms();
        var spec = AttackTelegraphSpec.CreateLine(Vector3.zero, Vector3.right * 8, 1, 10)
            .WithWallClipping(1 << 30);
        var view = CreateMeshView();
        ShowMesh(view, spec, null, 0);
        float near = view.GetComponent<MeshFilter>().sharedMesh.bounds.max.x;
        int before = Rebuilds(view);
        wall.transform.position = new Vector3(5, 0, 0);
        Physics2D.SyncTransforms();
        StyleMesh(view, null, 0);
        float far = view.GetComponent<MeshFilter>().sharedMesh.bounds.max.x;
        Assert.AreEqual(before + 1, Rebuilds(view));
        Assert.That(near, Is.InRange(2.7f, 3f));
        Assert.That(far, Is.InRange(4.7f, 5f));
    }
}
#endif
