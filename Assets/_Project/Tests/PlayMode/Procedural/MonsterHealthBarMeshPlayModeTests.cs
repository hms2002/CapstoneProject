#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityGAS;
using Object = UnityEngine.Object;

/// <summary>Compares the original authored HP rectangles with the actor-owned instanced projection.</summary>
public sealed class MonsterHealthBarMeshPlayModeTests
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static readonly Type ViewType = Type.GetType("MonsterStackStatusWorldView, UI", true);
    private static readonly Type MeshViewType = Type.GetType("MonsterHealthBarMeshView, UI", true);
    private static readonly Type ImageType = Type.GetType("UnityEngine.UI.Image, UnityEngine.UI", true);
    private static readonly Type AuthorType = Type.GetType("MonsterHealthBarMeshAuthoring, Editor", true);
    private static void RenderPhase() => ViewType.GetMethod("ResolveHealthProjection", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);

    [UnityTest]
    public IEnumerator PlainRectanglesAndInstancedQuad_StrictRgbaMatch_AllSizesHealthAndTrail()
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            Assert.Ignore("Requires the actual graphics pipeline for strict HP RGBA equivalence.");
        using (var assets = new SharedAssets())
        using (var original = new HudFixture(false, assets))
        using (var optimized = new HudFixture(true, assets))
        using (var camera = new RenderFixture())
        {
            yield return null;
            foreach (MonsterSizeCategory size in new[] { MonsterSizeCategory.Small, MonsterSizeCategory.Medium, MonsterSizeCategory.Large })
            foreach (Vector2 ratios in new[] { Vector2.zero, new Vector2(.3f, .8f), new Vector2(.625f, .625f), Vector2.one })
            {
                original.Root.SetActive(true);
                optimized.Root.SetActive(false);
                original.Configure(size, ratios.x, ratios.y);
                Color32[] expected = camera.Capture();
                original.Root.SetActive(false);
                optimized.Root.SetActive(true);
                // Enable resets presentation state; apply the same controlled HP state after enable.
                optimized.Configure(size, ratios.x, ratios.y);
                Color32[] actual = camera.Capture();
                int visible = 0;
                int mismatch = 0;
                int maximum = 0;
                for (int i = 0; i < expected.Length; i++)
                {
                    if (expected[i].a > 0) visible++;
                    int difference = Mathf.Max(Mathf.Abs(actual[i].r - expected[i].r), Mathf.Abs(actual[i].g - expected[i].g),
                        Mathf.Abs(actual[i].b - expected[i].b), Mathf.Abs(actual[i].a - expected[i].a));
                    if (difference > 2) mismatch++;
                    maximum = Mathf.Max(maximum, difference);
                }
                Assert.That(visible, Is.GreaterThan(100), "The original HP frame must be visible, including zero HP.");
                Assert.That(mismatch, Is.Zero, $"size={size}, HP/trail={ratios}, maximum RGBA delta={maximum}");
            }
        }
    }

    [Test]
    public void DamageDelayRepeatedHitsHealingAndMaxRescale_KeepLegacyRatios()
    {
        using (var assets = new SharedAssets())
        using (var fixture = new HudFixture(true, assets))
        {
            fixture.Apply(.8f, false, 0f);
            fixture.Apply(.45f, true, 1f);
            fixture.Trail(1.29f);
            Assert.That(fixture.Ratio("trailRatio"), Is.EqualTo(.8f).Within(.0001f));
            fixture.Trail(1.55f);
            Assert.That(fixture.Ratio("trailRatio"), Is.EqualTo(.625f).Within(.0001f));
            fixture.Apply(.3f, true, 1.6f);
            fixture.Trail(1.89f);
            Assert.That(fixture.Ratio("trailRatio"), Is.EqualTo(.59f).Within(.0001f));
            fixture.Trail(2.4f);
            Assert.That(fixture.Ratio("trailRatio"), Is.EqualTo(.3f).Within(.0001f));
            fixture.Apply(.7f, false, 2.5f);
            Assert.That(fixture.Ratio("trailRatio"), Is.EqualTo(.7f));
            fixture.Apply(.35f, false, 2.6f);
            fixture.Tick();
            var block = new MaterialPropertyBlock();
            fixture.Renderer.GetPropertyBlock(block);
            Vector4 data = block.GetVector("_HealthData");
            Assert.That(data.z, Is.EqualTo(.35f));
            Assert.That(data.w, Is.EqualTo(.35f));
        }
    }

    [UnityTest]
    public IEnumerator OverlappingActors_HpAndCountTimerRows_StrictRgbaMatchLegacySorting()
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            Assert.Ignore("Requires the actual graphics pipeline for overlapping actor sorting equivalence.");
        using (var assets = new SharedAssets())
        using (var legacyFirst = new HudFixture(false, assets))
        using (var legacySecond = new HudFixture(false, assets))
        using (var meshFirst = new HudFixture(true, assets))
        using (var meshSecond = new HudFixture(true, assets))
        using (var camera = new RenderFixture(.55f))
        {
            yield return null;
            // Same-plane HP/row overlap, HP crossing the other actor's status row, and a different-Z pair.
            foreach (Vector3 secondPose in new[] { new Vector3(.18f, -.03f, 0f), new Vector3(.12f, -.16f, 0f), new Vector3(.12f, -.08f, .04f) })
            {
                meshFirst.Root.SetActive(false); meshSecond.Root.SetActive(false);
                PrepareOverlap(legacyFirst, Vector3.zero, .25f, .8f, "Burn", MonsterStatusValueKind.Stacks, 12f);
                PrepareOverlap(legacySecond, secondPose, .72f, .9f, "Electrocuted", MonsterStatusValueKind.Seconds, 3.2f);
                Color32[] expected = camera.Capture();
                legacyFirst.Root.SetActive(false); legacySecond.Root.SetActive(false);
                PrepareOverlap(meshFirst, Vector3.zero, .25f, .8f, "Burn", MonsterStatusValueKind.Stacks, 12f);
                PrepareOverlap(meshSecond, secondPose, .72f, .9f, "Electrocuted", MonsterStatusValueKind.Seconds, 3.2f);
                Color32[] actual = camera.Capture();
                Assert.That(meshFirst.Renderer.enabled, Is.False, "Intersecting actors must retain the authored Canvas sorting path.");
                Assert.That(meshSecond.Renderer.enabled, Is.False);
                Assert.That(meshFirst.Renderer.sortingLayerID, Is.EqualTo(legacyFirst.Canvas.sortingLayerID));
                Assert.That(meshFirst.Renderer.sortingOrder, Is.EqualTo(legacyFirst.Canvas.sortingOrder));
                Assert.That(meshSecond.Renderer.sortingLayerID, Is.EqualTo(legacySecond.Canvas.sortingLayerID));
                Assert.That(meshSecond.Renderer.sortingOrder, Is.EqualTo(legacySecond.Canvas.sortingOrder));
                int visible = 0, mismatch = 0, maximum = 0;
                for (int i = 0; i < expected.Length; i++)
                {
                    if (expected[i].a > 0) visible++;
                    int difference = Mathf.Max(Mathf.Abs(actual[i].r - expected[i].r), Mathf.Abs(actual[i].g - expected[i].g),
                        Mathf.Abs(actual[i].b - expected[i].b), Mathf.Abs(actual[i].a - expected[i].a));
                    if (difference > 2) mismatch++;
                    maximum = Mathf.Max(maximum, difference);
                }
                Assert.That(visible, Is.GreaterThan(100), "Both legacy HP bars and status rows must produce a visible canary.");
                Assert.That(mismatch, Is.Zero, $"Overlapping pose={secondPose}, max RGBA delta={maximum}, mismatched pixels={mismatch}");
            }
        }
    }

    private static void PrepareOverlap(HudFixture fixture, Vector3 position, float health, float trail,
        string statusId, MonsterStatusValueKind kind, float displayValue)
    {
        fixture.Root.SetActive(true);
        fixture.Root.transform.position = position;
        fixture.Statuses.ClearAll();
        Assert.That(fixture.Statuses.Register(new TestStatus(statusId, kind, displayValue)), Is.True);
        fixture.Configure(MonsterSizeCategory.Medium, health, trail);
        Assert.That(fixture.Canvas.enabled, Is.True);
        Assert.That(fixture.Hud.transform.Find("Visual/Statuses/" + statusId).gameObject.activeInHierarchy, Is.True);
        Assert.That(Vector3.Distance(fixture.Hud.transform.position, fixture.Anchor.position), Is.LessThan(.0001f));
    }

    [UnityTest]
    public IEnumerator OverlappingHpOnlyActors_StrictRgbaMatchLegacy_ThenSeparatedActorsUseMesh()
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            Assert.Ignore("Requires native rendering for the original HP-only sorting path.");
        using (var assets = new SharedAssets())
        using (var legacyFirst = new HudFixture(false, assets))
        using (var legacySecond = new HudFixture(false, assets))
        using (var meshFirst = new HudFixture(true, assets))
        using (var meshSecond = new HudFixture(true, assets))
        using (var camera = new RenderFixture(.55f))
        {
            yield return null;
            meshFirst.Root.SetActive(false); meshSecond.Root.SetActive(false);
            legacyFirst.Root.transform.position = Vector3.zero;
            legacySecond.Root.transform.position = new Vector3(.15f, .025f, .04f);
            legacyFirst.Configure(MonsterSizeCategory.Medium, .25f, .8f);
            legacySecond.Configure(MonsterSizeCategory.Medium, .72f, .9f);
            Color32[] expected = camera.Capture();
            legacyFirst.Root.SetActive(false); legacySecond.Root.SetActive(false);
            meshFirst.Root.SetActive(true); meshSecond.Root.SetActive(true);
            meshFirst.Root.transform.position = Vector3.zero;
            meshSecond.Root.transform.position = new Vector3(.15f, .025f, .04f);
            meshFirst.Configure(MonsterSizeCategory.Medium, .25f, .8f);
            meshSecond.Configure(MonsterSizeCategory.Medium, .72f, .9f);
            Color32[] actual = camera.Capture();
            AssertPixelsMatch(expected, actual, "HP-only overlap");
            Assert.That(meshFirst.Canvas.enabled && meshSecond.Canvas.enabled, Is.True);
            Assert.That(meshFirst.Renderer.enabled || meshSecond.Renderer.enabled, Is.False);
            meshSecond.Root.transform.position = new Vector3(3f, 0f, .04f);
            meshSecond.Tick();
            Assert.That(meshFirst.Renderer.enabled && meshSecond.Renderer.enabled, Is.True);
            Assert.That(meshFirst.Canvas.enabled || meshSecond.Canvas.enabled, Is.False);
            AssertLegacyGraphics(meshFirst, false); AssertLegacyGraphics(meshSecond, false);
        }
    }

    [Test]
    public void OverlapRouting_RestoresAfterDisableReenableSeparation_AndKeepsDistantActorInSameMode()
    {
        using (var assets = new SharedAssets())
        using (var first = new HudFixture(true, assets))
        using (var second = new HudFixture(true, assets))
        using (var distant = new HudFixture(true, assets))
        {
            first.Root.transform.position = Vector3.zero;
            second.Root.transform.position = new Vector3(.15f, .02f, 0f);
            distant.Root.transform.position = new Vector3(6f, 0f, 0f);
            first.Configure(MonsterSizeCategory.Medium, .25f, .8f);
            second.Configure(MonsterSizeCategory.Medium, .72f, .9f);
            distant.Configure(MonsterSizeCategory.Medium, .4f, .6f);
            Assert.That(first.Canvas.enabled && second.Canvas.enabled && distant.Canvas.enabled, Is.True);
            Assert.That(first.Renderer.enabled || second.Renderer.enabled || distant.Renderer.enabled, Is.False);
            AssertLegacyGraphics(first, true); AssertLegacyGraphics(second, true); AssertLegacyGraphics(distant, true);
            // A stable fallback must not dirty its Canvas again in the next LateUpdate.
            ViewType.GetMethod("LateUpdate", Fields).Invoke(distant.View, null);
            Assert.That(distant.Canvas.enabled, Is.True);
            second.Root.SetActive(false);
            RenderPhase();
            Assert.That(first.Renderer.enabled && distant.Renderer.enabled, Is.True);
            Assert.That(first.Canvas.enabled || distant.Canvas.enabled || second.Canvas.enabled, Is.False);
            AssertLegacyGraphics(second, false);
            second.Root.SetActive(true);
            second.Configure(MonsterSizeCategory.Medium, .72f, .9f);
            Assert.That(first.Canvas.enabled && second.Canvas.enabled && distant.Canvas.enabled, Is.True);
            Assert.That(first.Renderer.enabled || second.Renderer.enabled || distant.Renderer.enabled, Is.False);
            second.Root.transform.position = new Vector3(3f, 0f, 0f);
            second.Tick();
            Assert.That(first.Renderer.enabled && second.Renderer.enabled && distant.Renderer.enabled, Is.True);
            Assert.That(first.Canvas.enabled || second.Canvas.enabled || distant.Canvas.enabled, Is.False);
            AssertLegacyGraphics(first, false); AssertLegacyGraphics(second, false); AssertLegacyGraphics(distant, false);
        }
    }

    [Test]
    public void OverflowingStatusCountBounds_UsePreferredTextWidthBeforeMeshGeneration()
    {
        using (var assets = new SharedAssets())
        using (var first = new HudFixture(true, assets))
        using (var second = new HudFixture(true, assets))
        {
            first.Root.transform.position = Vector3.zero;
            second.Root.transform.position = new Vector3(1.25f, -.16f, 0f);
            first.Configure(MonsterSizeCategory.Medium, .25f, .8f);
            second.Configure(MonsterSizeCategory.Medium, .72f, .9f);
            Assert.That(first.Renderer.enabled && second.Renderer.enabled, Is.True, "The two bare HP rectangles are separated.");
            Assert.That(first.Statuses.Register(new TestStatus("Burn", MonsterStatusValueKind.Stacks, 123456789f)), Is.True);
            first.Tick();
            Assert.That(first.Canvas.enabled && second.Canvas.enabled, Is.True);
            Assert.That(first.Renderer.enabled || second.Renderer.enabled, Is.False,
                "Large count text must participate before its Canvas/TMP mesh has rebuilt.");
        }
    }

    private static void AssertLegacyGraphics(HudFixture fixture, bool enabled)
    {
        Component[] images = fixture.Hud.transform.Find("Visual/HealthBar").GetComponentsInChildren(ImageType, true);
        Assert.That(images.Length, Is.EqualTo(6));
        foreach (Component image in images) Assert.That(((Behaviour)image).enabled, Is.EqualTo(enabled));
    }

    private static void AssertPixelsMatch(Color32[] expected, Color32[] actual, string label)
    {
        int visible = 0, mismatch = 0, maximum = 0;
        for (int i = 0; i < expected.Length; i++)
        {
            if (expected[i].a > 0) visible++;
            int difference = Mathf.Max(Mathf.Abs(actual[i].r - expected[i].r), Mathf.Abs(actual[i].g - expected[i].g),
                Mathf.Abs(actual[i].b - expected[i].b), Mathf.Abs(actual[i].a - expected[i].a));
            if (difference > 2) mismatch++;
            maximum = Mathf.Max(maximum, difference);
        }
        Assert.That(visible, Is.GreaterThan(100), "The baseline must produce a nonblank rendering.");
        Assert.That(mismatch, Is.Zero, $"{label}: maximum RGBA delta={maximum}, mismatched pixels={mismatch}");
    }

    [Test]
    public void StatusCanvas_DisablesForHpOnlyAndReturnsForStacksAndTimers_WithoutMovingAnchors()
    {
        using (var assets = new SharedAssets())
        using (var fixture = new HudFixture(true, assets))
        {
            fixture.Configure(MonsterSizeCategory.Small, 1f, 1f);
            Assert.That(fixture.Canvas.enabled, Is.False);
            Assert.That(fixture.Renderer.enabled, Is.True);
            var burn = new TestStatus("Burn", MonsterStatusValueKind.Stacks, 12f);
            var timer = new TestStatus("Electrocuted", MonsterStatusValueKind.Seconds, 3.2f);
            fixture.Statuses.Register(burn);
            fixture.Statuses.Register(timer);
            fixture.Tick();
            Assert.That(fixture.Canvas.enabled, Is.True);
            var row = (RectTransform)fixture.Hud.transform.Find("Visual/Statuses");
            Assert.That(row.anchoredPosition, Is.EqualTo(new Vector2(-37.5f, -15f)));
            Assert.That(fixture.Hud.transform.Find("Visual/Statuses/Burn").gameObject.activeSelf, Is.True);
            Assert.That(fixture.Hud.transform.Find("Visual/Statuses/Electrocuted").gameObject.activeSelf, Is.True);
            Assert.That(burn.PulseSubscriberCount, Is.EqualTo(1));
            Assert.That(timer.PulseSubscriberCount, Is.EqualTo(1));
            burn.IsActive = timer.IsActive = false;
            fixture.Tick();
            Assert.That(fixture.Canvas.enabled, Is.False);
            Assert.That(burn.PulseSubscriberCount, Is.Zero);
            Assert.That(timer.PulseSubscriberCount, Is.Zero);
        }
    }

    [Test]
    public void FlippedScaledParent_HudStaysUpright_ShowHealthToggleAndDisableHideBothProjections()
    {
        using (var assets = new SharedAssets())
        using (var fixture = new HudFixture(true, assets))
        {
            fixture.Root.transform.localScale = new Vector3(-2f, 1.5f, 1f);
            fixture.Configure(MonsterSizeCategory.Large, .5f, .5f);
            Assert.That(Vector3.Distance(fixture.Hud.transform.position, fixture.Anchor.position), Is.LessThan(.0001f));
            Assert.That(Quaternion.Angle(fixture.Hud.transform.rotation, Quaternion.identity), Is.LessThan(.0001f));
            Assert.That(fixture.Hud.transform.lossyScale.x, Is.EqualTo(.014f).Within(.0001f));
            fixture.Root.transform.rotation = Quaternion.Euler(0f, 0f, 27f);
            fixture.Tick();
            Assert.That(Quaternion.Angle(fixture.Hud.transform.rotation, Quaternion.identity), Is.LessThan(.0001f));
            var profile = new SerializedObject(fixture.Profile);
            profile.FindProperty("showHealthBar").boolValue = false;
            profile.ApplyModifiedPropertiesWithoutUndo();
            fixture.Tick();
            Assert.That(fixture.Renderer.enabled, Is.False);
            Assert.That(((RectTransform)fixture.Hud.transform.Find("Visual/Statuses")).anchoredPosition,
                Is.EqualTo(new Vector2(-70f, 0f)));
            fixture.Root.SetActive(false);
            Assert.That(fixture.Renderer.enabled, Is.False);
            Assert.That(fixture.Canvas.enabled, Is.False);
            fixture.Root.SetActive(true);
            profile.Update(); profile.FindProperty("showHealthBar").boolValue = true; profile.ApplyModifiedPropertiesWithoutUndo();
            fixture.Configure(MonsterSizeCategory.Large, .8f, .8f);
            Assert.That(fixture.Renderer.enabled, Is.True);
        }
    }

    [Test]
    public void Actors_BorrowSameMeshAndMaterialButKeepIndependentInstanceData()
    {
        using (var assets = new SharedAssets())
        using (var first = new HudFixture(true, assets))
        using (var second = new HudFixture(true, assets))
        {
            second.Root.transform.position = new Vector3(3f, 0f, 0f);
            first.Configure(MonsterSizeCategory.Small, .25f, .5f);
            second.Configure(MonsterSizeCategory.Large, .75f, 1f);
            Assert.That(first.Renderer.sharedMaterial, Is.SameAs(second.Renderer.sharedMaterial));
            Assert.That(first.Renderer.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(second.Renderer.GetComponent<MeshFilter>().sharedMesh));
            var block = new MaterialPropertyBlock();
            first.Renderer.GetPropertyBlock(block);
            Assert.That(block.GetVector("_HealthData"), Is.EqualTo(new Vector4(75f, 10f, .25f, .5f)));
            second.Renderer.GetPropertyBlock(block);
            Assert.That(block.GetVector("_HealthData"), Is.EqualTo(new Vector4(140f, 10f, .75f, 1f)));
            first.Root.SetActive(false);
            Assert.That(second.Renderer.enabled, Is.True);
            Assert.That(assets.Material != null && assets.Mesh != null, Is.True);
        }
    }

    [Test]
    public void GroggyMerge_StaggerCanvasAndRows_RespectOptInAndDisableLifecycle()
    {
        using (var assets = new SharedAssets())
        using (var fixture = new HudFixture(true, assets, true))
        {
            fixture.Configure(MonsterSizeCategory.Medium, .7f, .9f);
            Assert.That(fixture.Statuses.TryGetActive("Burn", out _), Is.False);
            Assert.That(fixture.StaggerBar.gameObject.activeInHierarchy, Is.True);
            Assert.That(fixture.Canvas.enabled, Is.True, "A separate HP mesh must not suppress the ordinary stagger Graphics.");
            Assert.That(fixture.Renderer.enabled, Is.True);
            AssertLegacyGraphics(fixture, false);
            Assert.That(fixture.StatusRow.anchoredPosition, Is.EqualTo(new Vector2(-50f, -24f)));
            fixture.Gauge.AddBuildUp(25f, fixture.Root, fixture.Root);
            Assert.That(fixture.StaggerRatio, Is.EqualTo(.75f).Within(.0001f));

            fixture.Gauge.enabled = false;
            fixture.Tick();
            Assert.That(fixture.StaggerBar.gameObject.activeSelf, Is.False);
            Assert.That(fixture.Canvas.enabled, Is.False);
            Assert.That(fixture.Renderer.enabled, Is.True);
            Assert.That(fixture.StatusRow.anchoredPosition, Is.EqualTo(new Vector2(-50f, -15f)));
            fixture.Gauge.enabled = true;
            fixture.Tick();
            Assert.That(fixture.StaggerRatio, Is.EqualTo(.75f).Within(.0001f));
            Assert.That(fixture.Canvas.enabled && fixture.Renderer.enabled, Is.True);

            fixture.ShowHealth(false);
            fixture.Tick();
            Assert.That(fixture.StaggerBar.gameObject.activeSelf || fixture.Renderer.enabled || fixture.Canvas.enabled, Is.False);
            Assert.That(fixture.StatusRow.anchoredPosition, Is.EqualTo(new Vector2(-50f, 0f)));
            fixture.ShowHealth(true);
            fixture.Root.SetActive(false);
            Assert.That(fixture.Canvas.enabled || fixture.Renderer.enabled, Is.False);
            Assert.That(GaugeSubscriberCount(fixture.Gauge), Is.Zero);
            fixture.Root.SetActive(true);
            fixture.Configure(MonsterSizeCategory.Medium, .7f, .9f);
            Assert.That(fixture.Canvas.enabled && fixture.Renderer.enabled, Is.True);
            Assert.That(GaugeSubscriberCount(fixture.Gauge), Is.EqualTo(1), "Pool reactivation must subscribe once.");
            fixture.Gauge.AddBuildUp(25f, fixture.Root, fixture.Root);
            Assert.That(fixture.StaggerRatio, Is.EqualTo(.5f).Within(.0001f));
        }
        using (var assets = new SharedAssets())
        using (var ordinary = new HudFixture(true, assets))
        using (var notOptedIn = new HudFixture(true, assets, true, false))
        {
            notOptedIn.Root.transform.position = Vector3.right * 3f;
            ordinary.Tick(); notOptedIn.Tick();
            Assert.That(ordinary.StaggerBar.gameObject.activeSelf || notOptedIn.StaggerBar.gameObject.activeSelf, Is.False);
            Assert.That(ordinary.Canvas.enabled || notOptedIn.Canvas.enabled, Is.False);
            Assert.That(ordinary.Renderer.enabled && notOptedIn.Renderer.enabled, Is.True);
            Assert.That(ordinary.StatusRow.anchoredPosition.y, Is.EqualTo(-15f));
            Assert.That(notOptedIn.StatusRow.anchoredPosition.y, Is.EqualTo(-15f));
        }
    }

    [Test]
    public void GroggyMerge_RecoveryAdvancesWithoutFurtherGaugeEvents_AndDestroyUnsubscribes()
    {
        using (var assets = new SharedAssets())
        using (var fixture = new HudFixture(true, assets, true))
        using (var survivor = new HudFixture(true, assets))
        {
            survivor.Root.transform.position = Vector3.right * 3f;
            survivor.Tick();
            int gaugeEvents = 0;
            Action<float, float> observe = (_, __) => gaugeEvents++;
            fixture.Gauge.OnGaugeChanged += observe;
            fixture.Gauge.AddBuildUp(100f, fixture.Root, fixture.Root);
            fixture.Tick();
            Assert.That(fixture.Runner.HasActiveEffect(fixture.GroggyEffect, fixture.Root), Is.True);
            Assert.That(fixture.StaggerRatio, Is.Zero.Within(.0001f));
            int atTrigger = gaugeEvents;
            typeof(GameplayEffectRunner).GetMethod("TickEffects", Fields).Invoke(fixture.Runner, new object[] { 1f });
            fixture.Tick();
            Assert.That(fixture.StaggerRatio, Is.EqualTo(1f / 3f).Within(.0001f));
            Assert.That(gaugeEvents, Is.EqualTo(atTrigger), "Time projection must progress without another gauge event.");
            typeof(GameplayEffectRunner).GetMethod("TickEffects", Fields).Invoke(fixture.Runner, new object[] { 1f });
            fixture.Tick();
            Assert.That(fixture.StaggerRatio, Is.EqualTo(2f / 3f).Within(.0001f));
            Assert.That(gaugeEvents, Is.EqualTo(atTrigger));
            typeof(GameplayEffectRunner).GetMethod("TickEffects", Fields).Invoke(fixture.Runner, new object[] { 1.1f });
            fixture.Tick();
            Assert.That(fixture.Runner.HasActiveEffect(fixture.GroggyEffect, fixture.Root), Is.False);
            Assert.That(fixture.StaggerRatio, Is.EqualTo(1f).Within(.0001f));
            fixture.Gauge.OnGaugeChanged -= observe;
            Assert.That(GaugeSubscriberCount(fixture.Gauge), Is.EqualTo(1));
            Object.DestroyImmediate(fixture.Hud);
            Assert.That(GaugeSubscriberCount(fixture.Gauge), Is.Zero);
            fixture.Gauge.AddBuildUp(1f, fixture.Root, fixture.Root);
            survivor.Tick();
            Assert.That(survivor.Renderer.enabled, Is.True);
            Assert.That(survivor.Canvas.enabled, Is.False);
            Assert.That(assets.Material != null && assets.Mesh != null, Is.True);
        }
    }

    [UnityTest]
    public IEnumerator GroggyMerge_StaggerOnlyIntersection_PreservesLegacyRasterAndRestoresMesh()
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            Assert.Ignore("Requires actual graphics for stagger-to-HP cross-actor sorting equivalence.");
        using (var assets = new SharedAssets())
        using (var legacyFirst = new HudFixture(false, assets, true))
        using (var legacySecond = new HudFixture(false, assets))
        using (var meshFirst = new HudFixture(true, assets, true))
        using (var meshSecond = new HudFixture(true, assets))
        using (var camera = new RenderFixture(.4f))
        {
            yield return null;
            meshFirst.Root.SetActive(false); meshSecond.Root.SetActive(false);
            PrepareStaggerIntersection(legacyFirst, legacySecond);
            Color32[] expected = camera.Capture();
            legacyFirst.Root.SetActive(false); legacySecond.Root.SetActive(false);
            meshFirst.Root.SetActive(true); meshSecond.Root.SetActive(true);
            PrepareStaggerIntersection(meshFirst, meshSecond);
            Assert.That(meshFirst.Renderer.enabled || meshSecond.Renderer.enabled, Is.False,
                "A stagger-only crossing must select original whole-Canvas ordering.");
            Assert.That(meshFirst.Canvas.enabled && meshSecond.Canvas.enabled, Is.True);
            AssertLegacyGraphics(meshFirst, true); AssertLegacyGraphics(meshSecond, true);
            AssertPixelsMatch(expected, camera.Capture(), "Stagger rectangle crosses another actor HP without HP-to-HP overlap");
            meshSecond.Root.transform.position = Vector3.right * 3f;
            meshSecond.Tick();
            Assert.That(meshFirst.Renderer.enabled && meshSecond.Renderer.enabled, Is.True);
            Assert.That(meshFirst.Canvas.enabled, Is.True, "Stagger remains visible after leaving legacy sorting.");
            Assert.That(meshSecond.Canvas.enabled, Is.False);
        }
    }

    private static void PrepareStaggerIntersection(HudFixture first, HudFixture second)
    {
        first.Root.transform.position = Vector3.zero;
        second.Root.transform.position = Vector3.zero;
        first.Attributes.TrySetBaseValue(first.CurrentGauge, 50f, first.Root);
        first.Configure(MonsterSizeCategory.Medium, .25f, .8f);
        second.Configure(MonsterSizeCategory.Medium, .72f, .9f);
        Rect firstHp = WorldRect(first.HealthBar);
        Rect secondHp = WorldRect(second.HealthBar);
        second.Root.transform.position = new Vector3(.12f, firstHp.yMin - .003f - secondHp.yMax, 0f);
        second.Tick();
        Assert.That(WorldRect(first.HealthBar).Overlaps(WorldRect(second.HealthBar)), Is.False,
            "The fixture must isolate stagger-only overlap, not the existing HP overlap case.");
        Assert.That(WorldRect(first.StaggerBar).Overlaps(WorldRect(second.HealthBar)), Is.True);
    }

    [Test]
    public void GroggyMerge_ThreeAuthoredTankProfiles_UseHealthThirdAndReactToMaxHealthChanges()
    {
        var sharedHud = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/MonsterWorldHud.prefab");
        Assert.That(sharedHud, Is.Not.Null);
        var hudView = sharedHud.GetComponent(ViewType);
        Assert.That(hudView, Is.Not.Null);
        var hudData = new SerializedObject(hudView);
        var meshView = hudData.FindProperty("meshHealthBar").objectReferenceValue as Component;
        var statusCanvas = hudData.FindProperty("statusCanvas").objectReferenceValue as Canvas;
        var staggerBar = hudData.FindProperty("staggerBar").objectReferenceValue as RectTransform;
        var staggerFill = hudData.FindProperty("staggerFill").objectReferenceValue as Component;
        Assert.That(meshView, Is.Not.Null, "Merged shared prefab must retain its authored HP mesh reference.");
        Assert.That(meshView.GetType(), Is.EqualTo(MeshViewType));
        Assert.That((bool)MeshViewType.GetProperty("IsConfigured").GetValue(meshView), Is.True);
        Assert.That(statusCanvas, Is.SameAs(sharedHud.GetComponent<Canvas>()));
        Assert.That(staggerBar, Is.SameAs(sharedHud.transform.Find("Visual/StaggerBar") as RectTransform));
        Assert.That(staggerBar, Is.Not.Null);
        Assert.That(staggerFill, Is.Not.Null);
        Assert.That(staggerFill.GetType(), Is.EqualTo(ImageType));
        Assert.That(staggerFill.transform.IsChildOf(staggerBar), Is.True, "Stagger fill must remain inside its authored bar.");
        var legacy = new SerializedObject(meshView).FindProperty("legacyGraphics");
        Assert.That(legacy.arraySize, Is.EqualTo(6));
        Transform healthBar = sharedHud.transform.Find("Visual/HealthBar");
        for (int i = 0; i < legacy.arraySize; i++)
        {
            var image = legacy.GetArrayElementAtIndex(i).objectReferenceValue as Component;
            Assert.That(image, Is.Not.Null);
            Assert.That(image.GetType(), Is.EqualTo(ImageType));
            Assert.That(image, Is.Not.SameAs(staggerFill), "HP fallback must not disable the separate stagger fill.");
            Assert.That(image.transform.IsChildOf(healthBar), Is.True);
        }
        string[] paths = { "CommonCorridor/GoblinTank", "CommonCorridor/ArcaneTankGolem", "SlimeCorridor/Rook" };
        string[] profiles = { "GoblinTank", "ArcaneTankGolem", "Rook" };
        for (int i = 0; i < paths.Length; i++)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Monsters/" + paths[i] + ".prefab");
            Assert.That(prefab, Is.Not.Null, paths[i]);
            var authored = prefab.GetComponent<StaggerGaugeSystem>();
            Assert.That(authored, Is.Not.Null, paths[i]);
            Assert.That(authored.HasHealthScaledGauge, Is.True, paths[i]);
            var serialized = new SerializedObject(authored);
            var maxHealth = serialized.FindProperty("maxHealthAttribute").objectReferenceValue as AttributeDefinition;
            float multiplier = serialized.FindProperty("maxGaugePerMaxHealth").floatValue;
            Assert.That(maxHealth, Is.Not.Null);
            Assert.That(multiplier, Is.EqualTo(1f / 3f).Within(.0001f), paths[i]);
            var init = AssetDatabase.LoadAssetAtPath<AttributeInitProfileSO>(
                "Assets/_Project/Data/Attributes/InitProfiles/Enemies/Mobs/" + profiles[i] + "AttributeOverrideInitProfile.asset");
            Assert.That(init, Is.Not.Null);
            float health = -1f, gauge = -1f;
            foreach (var entry in init.Entries)
            {
                if (entry.attribute == maxHealth) health = entry.baseValue;
                if (entry.attribute == authored.maxGaugeAttribute) gauge = entry.baseValue;
            }
            Assert.That(health, Is.GreaterThan(0f), paths[i]);
            Assert.That(gauge, Is.EqualTo(health * multiplier).Within(.0001f), paths[i]);
            using (var assets = new SharedAssets())
            using (var fixture = new HudFixture(true, assets, true))
            {
                Assert.That(fixture.Attributes.TrySetBaseValue(fixture.MaxHealth, health * 2f, fixture.Root), Is.True);
                Assert.That(fixture.Attributes.GetAttributeValue(fixture.MaxGauge), Is.EqualTo(health * 2f / 3f).Within(.0001f));
                fixture.Gauge.AddBuildUp(health / 3f, fixture.Root, fixture.Root);
                fixture.Tick();
                Assert.That(fixture.StaggerRatio, Is.EqualTo(.5f).Within(.0001f));
            }
        }
    }

    private static int GaugeSubscriberCount(StaggerGaugeSystem gauge) =>
        ((Delegate)typeof(StaggerGaugeSystem).GetField("OnGaugeChanged", Fields).GetValue(gauge))?.GetInvocationList().Length ?? 0;

    private static Rect WorldRect(RectTransform transform)
    {
        var corners = new Vector3[4]; transform.GetWorldCorners(corners);
        return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
    }

    private sealed class GroggyProbeEffect : GameplayEffect
    {
        public override void Apply(GameObject target, GameObject instigator, int stackCount = 1) { }
        public override void Remove(GameObject target, GameObject instigator) { }
    }

    private sealed class TestStatus : IMonsterStatusSource
    {
        public string StatusId { get; }
        public MonsterStatusValueKind ValueKind { get; }
        public float DisplayValue { get; }
        public Color DisplayColor => Color.yellow;
        public bool IsActive { get; set; } = true;
        private event Action pulse;
        public int PulseSubscriberCount => pulse?.GetInvocationList().Length ?? 0;
        public event Action PulseRequested { add => pulse += value; remove => pulse -= value; }
        public void Clear() => IsActive = false;
        public TestStatus(string id, MonsterStatusValueKind kind, float value) { StatusId = id; ValueKind = kind; DisplayValue = value; }
    }

    private sealed class SharedAssets : IDisposable
    {
        public readonly Mesh Mesh;
        public readonly Material Material;
        public SharedAssets()
        {
            Shader shader = Shader.Find("Project/Monster HP Instanced");
            Assert.That(shader, Is.Not.Null);
            Mesh = new Mesh { name = "HP test shared quad" };
            Mesh.vertices = new[] { new Vector3(-.5f, -.5f, 0), new Vector3(-.5f, .5f, 0), new Vector3(.5f, .5f, 0), new Vector3(.5f, -.5f, 0) };
            Mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
            Mesh.triangles = new[] { 0, 1, 2, 2, 3, 0 }; Mesh.RecalculateBounds();
            Material = new Material(shader) { enableInstancing = true };
        }
        public void Dispose() { Object.DestroyImmediate(Mesh); Object.DestroyImmediate(Material); }
    }

    private sealed class HudFixture : IDisposable
    {
        public readonly GameObject Root;
        public readonly GameObject Hud;
        public readonly Component View;
        public readonly MonsterSizeProfile Profile;
        public readonly MonsterStatusRuntime Statuses;
        public readonly Transform Anchor;
        public readonly AttributeSet Attributes;
        public readonly StaggerGaugeSystem Gauge;
        public readonly GameplayEffectRunner Runner;
        public readonly AttributeDefinition CurrentGauge, MaxGauge, MaxHealth;
        public readonly GameplayEffect GroggyEffect;
        private readonly System.Collections.Generic.List<Object> owned = new();
        public RectTransform HealthBar => (RectTransform)Hud.transform.Find("Visual/HealthBar");
        public RectTransform StaggerBar => (RectTransform)Hud.transform.Find("Visual/StaggerBar");
        public RectTransform StatusRow => (RectTransform)Hud.transform.Find("Visual/Statuses");
        public float StaggerRatio => ((RectTransform)((Component)ViewType.GetField("staggerFill", Fields).GetValue(View)).transform).anchorMax.x;
        public Canvas Canvas => Hud.GetComponent<Canvas>();
        public MeshRenderer Renderer => Hud.transform.Find("Visual/HealthBar/HPGeometry")?.GetComponent<MeshRenderer>();
        public HudFixture(bool migrate, SharedAssets assets, bool withGauge = false, bool optedIn = true)
        {
            Root = new GameObject("Controlled HP actor"); Root.SetActive(false);
            Profile = Root.AddComponent<MonsterSizeProfile>();
            Statuses = Root.AddComponent<MonsterStatusRuntime>();
            Anchor = new GameObject("HudAnchor").transform; Anchor.SetParent(Root.transform, false);
            var profile = new SerializedObject(Profile); profile.FindProperty("hudAnchor").objectReferenceValue = Anchor; profile.ApplyModifiedPropertiesWithoutUndo();
            AttributeDefinition health = null;
            if (withGauge)
            {
                health = Attribute("Groggy merge health", 300f);
                MaxHealth = Attribute("Groggy merge max health", 300f);
                CurrentGauge = Attribute("Groggy merge current gauge", 0f);
                MaxGauge = Attribute("Groggy merge max gauge", 100f);
                var catalog = ScriptableObject.CreateInstance<AttributeCatalogSO>(); owned.Add(catalog);
                typeof(AttributeCatalogSO).GetField("attributes", Fields).SetValue(catalog, new[] { health, MaxHealth, CurrentGauge, MaxGauge });
                Attributes = Root.AddComponent<AttributeSet>();
                typeof(AttributeSet).GetField("attributeCatalog", Fields).SetValue(Attributes, catalog);
                Runner = Root.AddComponent<GameplayEffectRunner>();
                Gauge = Root.AddComponent<StaggerGaugeSystem>();
                Gauge.currentGaugeAttribute = CurrentGauge; Gauge.maxGaugeAttribute = MaxGauge;
                typeof(StaggerGaugeSystem).GetField("maxHealthAttribute", Fields).SetValue(Gauge, MaxHealth);
                typeof(StaggerGaugeSystem).GetField("maxGaugePerMaxHealth", Fields).SetValue(Gauge, optedIn ? 1f / 3f : 0f);
                GroggyEffect = ScriptableObject.CreateInstance<GroggyProbeEffect>(); owned.Add(GroggyEffect);
                GroggyEffect.name = "Groggy merge timed probe"; GroggyEffect.duration = 3f;
                Gauge.staggeredEffect = GroggyEffect;
            }
            Hud = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/MonsterWorldHud.prefab"), Root.transform, false);
            View = Hud.GetComponent(ViewType);
            if (withGauge)
            {
                ViewType.GetField("healthAttribute", Fields).SetValue(View, health);
                ViewType.GetField("maxHealthAttribute", Fields).SetValue(View, MaxHealth);
            }
            ViewType.GetField("meshHealthBar", Fields).SetValue(View, null);
            ViewType.GetField("statusCanvas", Fields).SetValue(View, null);
            Transform bar = Hud.transform.Find("Visual/HealthBar");
            foreach (Component image in bar.GetComponentsInChildren(ImageType, true)) ((Behaviour)image).enabled = true;
            Transform previous = bar.Find("HPGeometry"); if (previous != null) Object.DestroyImmediate(previous.gameObject);
            if (migrate) AuthorType.GetMethod("ApplyToLoadedHud").Invoke(null, new object[] { Hud, assets.Mesh, assets.Material });
            foreach (Transform transform in Root.GetComponentsInChildren<Transform>(true)) transform.gameObject.layer = 28;
            Root.SetActive(true);
            if (withGauge)
            {
                typeof(StaggerGaugeSystem).GetMethod("Start", Fields).Invoke(Gauge, null);
                Runner.enabled = false; // Recovery tests advance the real runner deterministically.
            }
            Configure(MonsterSizeCategory.Medium, 1f, 1f);
        }
        private AttributeDefinition Attribute(string name, float initial)
        {
            var attribute = ScriptableObject.CreateInstance<AttributeDefinition>(); owned.Add(attribute);
            attribute.name = name; attribute.defaultBaseValue = initial; attribute.minValue = 0f; attribute.maxValue = 10000f;
            return attribute;
        }
        public void ShowHealth(bool enabled)
        {
            var profile = new SerializedObject(Profile); profile.FindProperty("showHealthBar").boolValue = enabled; profile.ApplyModifiedPropertiesWithoutUndo();
        }
        public void Configure(MonsterSizeCategory size, float health, float trail)
        {
            var profile = new SerializedObject(Profile); profile.FindProperty("size").enumValueIndex = (int)size; profile.ApplyModifiedPropertiesWithoutUndo();
            Apply(trail, false, Time.time);
            Apply(health, health < trail, Time.time);
            Tick();
        }
        public void Apply(float ratio, bool damaged, float now) => ViewType.GetMethod("ApplyHealthRatio", Fields).Invoke(View, new object[] { ratio, damaged, now });
        public void Trail(float now) => ViewType.GetMethod("UpdateHealthTrail", Fields).Invoke(View, new object[] { now });
        public float Ratio(string name) => (float)ViewType.GetField(name, Fields).GetValue(View);
        public void Tick()
        {
            ViewType.GetMethod("LateUpdate", Fields).Invoke(View, null);
            RenderPhase();
        }
        public void Dispose()
        {
            Object.DestroyImmediate(Root);
            foreach (Object item in owned) if (item != null) Object.DestroyImmediate(item);
        }
    }

    private sealed class RenderFixture : IDisposable
    {
        private readonly GameObject root = new GameObject("HP regression camera", typeof(Camera));
        private readonly Camera camera;
        private readonly RenderTexture target;
        public RenderFixture(float orthographicSize = .25f)
        {
            camera = root.GetComponent<Camera>(); camera.enabled = false; camera.orthographic = true; camera.orthographicSize = orthographicSize;
            camera.transform.position = new Vector3(0, 0, -10); camera.cullingMask = 1 << 28;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear; camera.allowHDR = false; camera.allowMSAA = false;
            target = new RenderTexture(700, 160, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear); target.Create(); camera.targetTexture = target;
        }
        public Color32[] Capture()
        {
            Canvas.ForceUpdateCanvases();
            if (GraphicsSettings.currentRenderPipeline != null)
            {
                var request = new RenderPipeline.StandardRequest { destination = target };
                Assert.That(RenderPipeline.SupportsRenderRequest(camera, request), Is.True);
                RenderPipeline.SubmitRenderRequest(camera, request);
            }
            else camera.Render();
            var readback = new Texture2D(700, 160, TextureFormat.RGBA32, false, true);
            RenderTexture previous = RenderTexture.active;
            try { RenderTexture.active = target; readback.ReadPixels(new Rect(0, 0, 700, 160), 0, 0); readback.Apply(); return readback.GetPixels32(); }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(readback); }
        }
        public void Dispose() { Object.DestroyImmediate(root); target.Release(); Object.DestroyImmediate(target); }
    }
}
#endif
