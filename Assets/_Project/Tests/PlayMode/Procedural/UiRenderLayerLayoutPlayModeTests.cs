#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class UiRenderLayerLayoutPlayModeTests
{
    static readonly Type LayoutType = Type.GetType("UiRenderLayerLayout, UI", true);
    static readonly Type BindingType = LayoutType.GetNestedType("Binding", BindingFlags.Public);
    const BindingFlags Methods = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    GameObject canvas;

    [TearDown]
    public void Cleanup() { if (canvas != null) Object.DestroyImmediate(canvas); }

    [UnityTest]
    public IEnumerator Projection_PreservesCornersThroughScrollScaleAndRotation_AndStableFramesDoNotWrite()
    {
        var fixture = CreateFixture();
        fixture.row.gameObject.SetActive(true);
        yield return null;
        foreach (float angle in new[] { 0f, -10f, 10f, 0f })
        {
            fixture.content.localRotation = Quaternion.Euler(0, 0, angle);
            fixture.content.localScale = new Vector3(1.25f, 1.25f, 1);
            fixture.content.anchoredPosition += new Vector2(0, 18);
            fixture.slot.sizeDelta = new Vector2(230, 55);
            yield return null;
            Canvas.ForceUpdateCanvases();
            AssertCorners(fixture.slot, fixture.target);
        }
        Canvas.ForceUpdateCanvases();
        int writes = Counter(fixture.layout);
        for (int i = 0; i < 10; i++) { yield return null; Canvas.ForceUpdateCanvases(); }
        Assert.That(Counter(fixture.layout), Is.EqualTo(writes), "Static projection must not dirty transforms every frame.");
    }

    [UnityTest]
    public IEnumerator TargetVisibility_RowDisableReenableAndDestroy_KeepExplicitAnchorOwnership()
    {
        var fixture = CreateFixture();
        fixture.row.gameObject.SetActive(true); yield return null;
        fixture.target.gameObject.SetActive(false); yield return null;
        Assert.IsFalse(fixture.slot.gameObject.activeSelf);
        Assert.IsFalse(fixture.anchor.gameObject.activeSelf);
        fixture.target.gameObject.SetActive(true); yield return null;
        Assert.IsTrue(fixture.slot.gameObject.activeSelf);
        Assert.IsTrue(fixture.anchor.gameObject.activeSelf);
        fixture.row.gameObject.SetActive(false); yield return null;
        Assert.That(fixture.anchor.parent, Is.SameAs(fixture.slot), "Disabled rows reclaim their existing render anchors.");
        fixture.row.gameObject.SetActive(true); yield return null;
        Assert.That(fixture.anchor.parent, Is.SameAs(fixture.layer));
        Object.Destroy(fixture.row.gameObject); yield return null; yield return null;
        Assert.IsTrue(fixture.anchor == null, "Destroying a logical row must not leak a detached render node.");
    }

    [UnityTest]
    public IEnumerator ExistingButtonTargetGraphicClickAndNavigationReferences_SurviveProjection()
    {
        var fixture = CreateFixture();
        var image = fixture.target.gameObject.AddComponent<Image>();
        var button = fixture.target.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
        int clicks = 0; button.onClick.AddListener(() => clicks++);
        fixture.row.gameObject.SetActive(true); yield return null;
        Assert.That(button.targetGraphic, Is.SameAs(image));
        Assert.That(button.navigation.mode, Is.EqualTo(Navigation.Mode.Automatic));
        button.onClick.Invoke(); Assert.That(clicks, Is.EqualTo(1));
        button.interactable = false; Assert.IsFalse(button.IsInteractable());
        fixture.row.gameObject.SetActive(false); fixture.row.gameObject.SetActive(true); yield return null;
        Assert.That(button.targetGraphic, Is.SameAs(image));
        button.onClick.Invoke(); Assert.That(clicks, Is.EqualTo(2), "Reattachment does not add/remove logical click listeners.");
        AssertCorners(fixture.slot, fixture.target);
    }

    (RectTransform content, RectTransform row, RectTransform slot, RectTransform anchor, RectTransform target, RectTransform layer, Component layout) CreateFixture()
    {
        canvas = new GameObject("Render layer test canvas", typeof(RectTransform), typeof(Canvas));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        RectTransform content = Rect("Contents", canvas.transform);
        content.sizeDelta = new Vector2(800, 600);
        RectTransform layer = Rect("Render_Text", content);
        Stretch(layer);
        var owner = content.gameObject.AddComponent(LayoutType);
        LayoutType.GetMethod("ConfigureAuthored").Invoke(owner, new object[] { new[] { layer }, Array.CreateInstance(BindingType, 0) });
        RectTransform row = Rect("Logical row", content);
        row.gameObject.SetActive(false); row.sizeDelta = new Vector2(600, 100);
        RectTransform slot = Rect("Layout slot", row);
        slot.anchoredPosition = new Vector2(70, -20); slot.sizeDelta = new Vector2(200, 50);
        RectTransform anchor = Rect("Authored render anchor", slot); Stretch(anchor);
        RectTransform target = Rect("Existing target", anchor); Stretch(target);
        var layout = row.gameObject.AddComponent(LayoutType);
        var binding = Activator.CreateInstance(BindingType);
        BindingType.GetField("layoutSlot").SetValue(binding, slot); BindingType.GetField("renderAnchor").SetValue(binding, anchor);
        BindingType.GetField("target").SetValue(binding, target.gameObject); BindingType.GetField("layer").SetValue(binding, 0);
        Array entries = Array.CreateInstance(BindingType, 1); entries.SetValue(binding, 0);
        LayoutType.GetMethod("ConfigureAuthored").Invoke(layout, new object[] { Array.Empty<RectTransform>(), entries });
        LayoutType.GetMethod("BindTo").Invoke(layout, new object[] { owner });
        return (content, row, slot, anchor, target, layer, layout);
    }

    static RectTransform Rect(string name, Transform parent)
    { var rectangle = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rectangle.SetParent(parent, false); return rectangle; }
    static void Stretch(RectTransform rectangle)
    { rectangle.anchorMin = Vector2.zero; rectangle.anchorMax = Vector2.one; rectangle.offsetMin = Vector2.zero; rectangle.offsetMax = Vector2.zero; }
    static int Counter(Component layout) => (int)LayoutType.GetProperty("DiagnosticTransformWrites").GetValue(layout);
    static void AssertCorners(RectTransform before, RectTransform after)
    {
        var a = new Vector3[4]; var b = new Vector3[4]; before.GetWorldCorners(a); after.GetWorldCorners(b);
        for (int i = 0; i < 4; i++) Assert.That(Vector3.Distance(a[i], b[i]), Is.LessThan(.005f), "Projected rectangle corner " + i);
    }
}
#endif
