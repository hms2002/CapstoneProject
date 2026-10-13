#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Checks actual authored chest geometry across inactive binding and active reveal.</summary>
public sealed class ChestRevealWidthRegressionPlayModeTests
{
    const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const string PrefabPath = "Assets/_Project/Prefabs/UI/GlobalUIRoot.prefab";
    static readonly Type Reveal = Type.GetType("ChestFirstOpenRevealPresentation, UI", true);
    static readonly Type Chest = Type.GetType("ChestScreen, UI", true);
    static readonly Type Image = Type.GetType("UnityEngine.UI.Image, UnityEngine.UI", true);
    static readonly Type LayoutElement = Type.GetType("UnityEngine.UI.LayoutElement, UnityEngine.UI", true);
    static readonly Type LayoutGroup = Type.GetType("UnityEngine.UI.LayoutGroup, UnityEngine.UI", true);
    static readonly Type Grid = Type.GetType("UnityEngine.UI.GridLayoutGroup, UnityEngine.UI", true);
    static readonly Type LayoutUtility = Type.GetType("UnityEngine.UI.LayoutUtility, UnityEngine.UI", true);
    static readonly Type RectMask = Type.GetType("UnityEngine.UI.RectMask2D, UnityEngine.UI", true);
    static readonly Type Mask = Type.GetType("UnityEngine.UI.Mask, UnityEngine.UI", true);
    static readonly Type Fitter = Type.GetType("UnityEngine.UI.ContentSizeFitter, UnityEngine.UI", true);

    [TestCase(4)]
    [TestCase(5)]
    [TestCase(8)]
    public void AuthoredStock_InactiveCaptureThenActiveReveal_ContainsEveryOffer(int stockCount)
    {
        using var state = new ExistingStateGuard();
        using var fixture = new AuthoredChest(stockCount);
        Assert.That(fixture.Root.activeInHierarchy, Is.False);
        Assert.That(fixture.Right.rect.width, Is.EqualTo(13f).Within(.01f), "Preserve the historical stored-rect mismatch.");
        Assert.That(NativePreferredWidth(fixture.Right), Is.Zero,
            "uGUI ignores inactive LayoutElements; this precondition reproduces the original failure.");

        fixture.Capture();
        Assert.That(fixture.Cached("MiddleWidth"), Is.EqualTo(511f).Within(.01f));
        fixture.Open();
        Assert.That(NativePreferredWidth(fixture.Right), Is.EqualTo(56f).Within(.01f));
        Assert.That(fixture.Viewport.rect.width, Is.EqualTo(511f).Within(.01f));
        fixture.AssertVisibleOffers();
    }

    [Test]
    public void SelectionRerollAndReopen_KeepTheCapturedFrameUntilExplicitRelease()
    {
        using var state = new ExistingStateGuard();
        using var fixture = new AuthoredChest(8);
        fixture.Capture();
        fixture.Open();
        float capturedHeight = fixture.Cached("MiddleHeight");
        Assert.That(capturedHeight, Is.EqualTo(225f).Within(.01f));
        fixture.AssertVisibleOffers();

        // Selection removes offers from the grid. It must not shrink the open frame.
        fixture.ReplaceStock(3);
        fixture.Apply(0.5f);
        Assert.That(fixture.Cached("MiddleHeight"), Is.EqualTo(capturedHeight));
        Assert.That(fixture.Viewport.rect.height, Is.EqualTo(capturedHeight * .5f).Within(.01f));
        fixture.Apply(1f);
        fixture.AssertVisibleOffers();

        // A retained-selection reroll reflows offers but keeps the original reveal size.
        fixture.ReplaceStock(4);
        fixture.Apply(1f);
        Assert.That(fixture.Cached("MiddleWidth"), Is.EqualTo(511f).Within(.01f));
        Assert.That(fixture.Cached("MiddleHeight"), Is.EqualTo(capturedHeight));
        fixture.AssertVisibleOffers();

        fixture.Root.SetActive(false);
        Invoke(fixture.Presentation, "ReleaseInitialLayout");
        fixture.Capture();
        Assert.That(fixture.Cached("MiddleHeight"), Is.EqualTo(130f).Within(.01f),
            "The next binding measures its new stock instead of retaining the previous two-row frame.");
        fixture.Open();
        fixture.AssertVisibleOffers();
    }

    [TestCase(false, 56f, 100f, 13f)]
    [TestCase(true, 56f, -1f, 56f)]
    [TestCase(true, 56f, 80f, 80f)]
    [TestCase(true, 56f, 40f, 56f)]
    public void InactiveWidth_RespectsEnabledMinAndPreferredButIgnoresDisabledLayout(
        bool enabled, float min, float preferred, float expected)
    {
        var root = new GameObject("Inactive chest width contract", typeof(RectTransform));
        root.SetActive(false);
        try
        {
            var rectangle = (RectTransform)root.transform;
            rectangle.sizeDelta = new Vector2(13f, 90f);
            var layout = (Behaviour)root.AddComponent(LayoutElement);
            SetProperty(layout, "minWidth", min);
            SetProperty(layout, "preferredWidth", preferred);
            layout.enabled = enabled;
            var resolve = Reveal.GetMethod("ResolveElementWidth", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(resolve, Is.Not.Null);
            Assert.That((float)resolve.Invoke(null, new object[] { rectangle }), Is.EqualTo(expected).Within(.01f));
            root.SetActive(true);
            Assert.That((float)resolve.Invoke(null, new object[] { rectangle }), Is.EqualTo(expected).Within(.01f),
                "Activation must not change the width contract used by the captured frame.");
        }
        finally { Object.DestroyImmediate(root); }
    }

    sealed class AuthoredChest : IDisposable
    {
        readonly GameObject staging;
        readonly GameObject canvas;
        readonly GameObject slotPrefab;
        readonly List<RectTransform> offers = new();
        public readonly GameObject Root;
        public readonly Component Presentation;
        public readonly RectTransform Viewport;
        public readonly RectTransform GridRoot;
        public readonly RectTransform Right;

        public AuthoredChest(int count)
        {
            staging = new GameObject("Inactive authored chest staging");
            staging.SetActive(false);
            canvas = new GameObject("Authored chest width canvas", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            try
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                Assert.That(asset, Is.Not.Null);
                Transform authored = asset.transform.Find("PopupCanvas/InventoryRoot");
                Assert.That(authored, Is.Not.Null);
                Root = Object.Instantiate(authored.gameObject, staging.transform);
                Assert.That(EditorUtility.IsPersistent(Root), Is.False);
                Presentation = Root.GetComponentInChildren(Reveal, true);
                Assert.That(Presentation, Is.Not.Null);
                SetField(Presentation, "applyLayoutInEditMode", false);
                AssertOwnedReferences(Presentation, Root.transform);
                Component chest = Root.GetComponentInChildren(Chest, true);
                slotPrefab = ((Component)Chest.GetField("chestSlotPrefab", Members).GetValue(chest)).gameObject;
                Viewport = (RectTransform)Reveal.GetField("middleViewport", Members).GetValue(Presentation);
                GridRoot = (RectTransform)Reveal.GetField("gridRoot", Members).GetValue(Presentation);
                Right = (RectTransform)GridRoot.parent.Find("RightMiddleFrame");
                Assert.That(Right, Is.Not.Null);

                // Isolate presentation before any Awake/OnEnable callback. Inventory bindings,
                // localization, save writers and UI-stack owners never run in this fixture.
                StripControllers(Root, Presentation);
                SetField(Presentation, "playPostRevealSlideFade", false);
                SetField(Presentation, "slotRevealUiParticleEmitterPrefab", null);
                SetField(Presentation, "slotRevealUiParticlePrewarmCount", 0);
                SetField(Presentation, "playOpenUiParticles", false);
                SetField(Presentation, "playSlotRevealUiParticles", false);
                SetField(Presentation, "playImpactUiParticles", false);
                SetField(Presentation, "forceOpenUiParticleRenderRootOnTop", false);
                Presentation.gameObject.SetActive(true);
                Root.SetActive(false);
                Root.transform.SetParent(canvas.transform, false);
                Right.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 13f);
                var rightElement = (Behaviour)Right.GetComponent(LayoutElement);
                Assert.That(rightElement, Is.Not.Null);
                Assert.That(rightElement.enabled, Is.True);
                Assert.That((float)LayoutElement.GetProperty("minWidth").GetValue(rightElement), Is.EqualTo(56f));
                Component grid = GridRoot.GetComponent(Grid);
                Assert.That(Grid.GetProperty("constraint").GetValue(grid).ToString(), Is.EqualTo("FixedColumnCount"));
                Assert.That(Grid.GetProperty("constraintCount").GetValue(grid), Is.EqualTo(4));
                Assert.That(Grid.GetProperty("cellSize").GetValue(grid), Is.EqualTo(new Vector2(90f, 90f)));
                Assert.That(Grid.GetProperty("spacing").GetValue(grid), Is.EqualTo(new Vector2(5f, 5f)));
                var padding = (RectOffset)LayoutGroup.GetProperty("padding").GetValue(grid);
                Assert.That(padding.left + padding.right, Is.EqualTo(20));
                Assert.That(padding.top + padding.bottom, Is.EqualTo(40));
                ReplaceStock(count);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void ReplaceStock(int count)
        {
            foreach (RectTransform offer in offers) Object.DestroyImmediate(offer.gameObject);
            offers.Clear();
            for (int index = 0; index < count; index++)
            {
                GameObject offer = Object.Instantiate(slotPrefab, staging.transform);
                StripControllers(offer, null);
                offer.name = "Authored offer " + index;
                offer.transform.SetParent(GridRoot, false);
                offer.SetActive(true);
                offers.Add((RectTransform)offer.transform);
            }
        }

        public void Capture() => Invoke(Presentation, "CaptureInitialLayout");
        public void Open() { Root.SetActive(true); Apply(1f); }
        public void Apply(float progress)
        {
            Invoke(Presentation, "ApplyManualRevealProgress", progress, true, false, null);
            Canvas.ForceUpdateCanvases();
        }
        public float Cached(string member)
        {
            object metrics = Reveal.GetField("initialLayoutMetrics", Members).GetValue(Presentation);
            Assert.That(metrics, Is.Not.Null);
            return (float)metrics.GetType().GetField(member, Members).GetValue(metrics);
        }

        public void AssertVisibleOffers()
        {
            Rect clip = Viewport.rect;
            Rect decoration = InLocalCoordinates(Right, Viewport);
            Rect grid = InLocalCoordinates(GridRoot, Viewport);
            Assert.That(decoration.xMin, Is.GreaterThanOrEqualTo(grid.xMax - .01f),
                "The later-drawn right decoration must not cover the offered-item grid.");
            foreach (RectTransform offer in offers)
            {
                Rect bounds = InLocalCoordinates(offer, Viewport);
                Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(clip.xMin - .01f), offer.name + " left clip");
                Assert.That(bounds.xMax, Is.LessThanOrEqualTo(clip.xMax + .01f), offer.name + " right clip");
                Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(clip.yMin - .01f), offer.name + " bottom clip");
                Assert.That(bounds.yMax, Is.LessThanOrEqualTo(clip.yMax + .01f), offer.name + " top clip");
                Assert.That(bounds.xMax, Is.LessThanOrEqualTo(decoration.xMin + .01f), offer.name + " right-frame overlap");
            }
        }

        public void Dispose()
        {
            Object.DestroyImmediate(canvas);
            Object.DestroyImmediate(staging);
        }
    }

    sealed class ExistingStateGuard : IDisposable
    {
        readonly IItemContainer chest = ItemContainerGroupRegistry.Chest;
        readonly IItemContainer consumables = ItemContainerGroupRegistry.ConsumableEquip;
        readonly IItemContainer weapons = ItemContainerGroupRegistry.WeaponEquip;
        readonly IItemContainer relics = ItemContainerGroupRegistry.RelicEquip;
        readonly bool inspection = ItemContainerGroupRegistry.IsInspectionOnly;
        readonly GameData profile = GameDataStore.Data;
        readonly GamePlayData run = RunSessionStore.Data;
        readonly string profileJson = Json(GameDataStore.Data);
        readonly string runJson = Json(RunSessionStore.Data);
        public void Dispose()
        {
            Assert.That(ItemContainerGroupRegistry.Chest, Is.SameAs(chest));
            Assert.That(ItemContainerGroupRegistry.ConsumableEquip, Is.SameAs(consumables));
            Assert.That(ItemContainerGroupRegistry.WeaponEquip, Is.SameAs(weapons));
            Assert.That(ItemContainerGroupRegistry.RelicEquip, Is.SameAs(relics));
            Assert.That(ItemContainerGroupRegistry.IsInspectionOnly, Is.EqualTo(inspection));
            Assert.That(GameDataStore.Data, Is.SameAs(profile));
            Assert.That(RunSessionStore.Data, Is.SameAs(run));
            Assert.That(Json(GameDataStore.Data), Is.EqualTo(profileJson));
            Assert.That(Json(RunSessionStore.Data), Is.EqualTo(runJson));
        }
        static string Json(object value) => value != null ? JsonUtility.ToJson(value) : null;
    }

    static void StripControllers(GameObject root, Component retained)
    {
        foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            if (behaviour != retained && !Image.IsInstanceOfType(behaviour) && !LayoutElement.IsInstanceOfType(behaviour) &&
                !LayoutGroup.IsInstanceOfType(behaviour) && !RectMask.IsInstanceOfType(behaviour) &&
                !Mask.IsInstanceOfType(behaviour) && !Fitter.IsInstanceOfType(behaviour))
                Object.DestroyImmediate(behaviour);
    }
    static void AssertOwnedReferences(Component component, Transform owner)
    {
        var serialized = new SerializedObject(component);
        var property = serialized.GetIterator();
        while (property.Next(true))
        {
            if (property.propertyPath.StartsWith("m_", StringComparison.Ordinal)) continue;
            if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
            Object reference = property.objectReferenceValue;
            Transform transform = reference is Component other ? other.transform : reference is GameObject go ? go.transform : null;
            if (transform == null) continue;
            if (property.propertyPath.IndexOf("Prefab", StringComparison.OrdinalIgnoreCase) >= 0 && EditorUtility.IsPersistent(reference)) continue;
            Assert.That(transform.IsChildOf(owner), Is.True, "Reveal reference escaped the cloned common ancestor: " + property.propertyPath);
        }
    }
    static Rect InLocalCoordinates(RectTransform rectangle, RectTransform frame)
    {
        var corners = new Vector3[4];
        rectangle.GetWorldCorners(corners);
        Vector2 min = new(float.PositiveInfinity, float.PositiveInfinity), max = new(float.NegativeInfinity, float.NegativeInfinity);
        foreach (Vector3 corner in corners)
        {
            Vector2 point = frame.InverseTransformPoint(corner);
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
    static float NativePreferredWidth(RectTransform rectangle) =>
        (float)LayoutUtility.GetMethod("GetPreferredWidth").Invoke(null, new object[] { rectangle });
    static void SetField(Component target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, Members);
        Assert.That(field, Is.Not.Null, "Unknown fixture field: " + name);
        field.SetValue(target, value);
    }
    static void SetProperty(Component target, string name, object value) => target.GetType().GetProperty(name).SetValue(target, value);
    static void Invoke(Component target, string name, params object[] arguments) => target.GetType().GetMethod(name, Members).Invoke(target, arguments);
}
#endif
