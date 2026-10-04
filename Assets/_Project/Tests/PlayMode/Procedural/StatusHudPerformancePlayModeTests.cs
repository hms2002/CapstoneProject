#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Measures production HUD binding via a typed delegate and verifies pooled display state using the authored prefab.</summary>
public sealed class StatusHudPerformancePlayModeTests
{
    // Mirrors the public readonly entry binding contract without introducing a UI assembly dependency.
    private delegate void BindEntry(in StatusHudEntry entry);
    private readonly List<GameObject> objects = new();
    private readonly List<Component> views = new();
    private readonly List<BindEntry> bindings = new();
    private Type viewType;

    [SetUp]
    public void SetUp()
    {
        viewType = Type.GetType("StatusHudEntryView, UI", true);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/status_HUD_EntryView.prefab");
        Assert.IsNotNull(prefab);
        for (int i = 0; i < 20; i++)
        {
            var go = Object.Instantiate(prefab);
            objects.Add(go);
            var view = go.GetComponent(viewType);
            views.Add(view);
            bindings.Add((BindEntry)Delegate.CreateDelegate(typeof(BindEntry), view, viewType.GetMethod("Bind")));
        }
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var go in objects) if (go != null) Object.DestroyImmediate(go);
        objects.Clear(); views.Clear(); bindings.Clear();
    }

    private static StatusHudEntry Entry(float time = 10, int stacks = 5, bool visible = true, string progress = null, bool highlighted = false, bool showStacks = true)
        => new("benchmark", "status", "Name", "Story", "Effect", null, stacks, showStacks,
            time, 10, true, default, 0, highlighted, visible, progress);

    private int Counter(string name, int count)
    {
        int sum = 0;
        for (int i = 0; i < count; i++) sum += (int)viewType.GetProperty(name).GetValue(views[i]);
        return sum;
    }

    [TestCase(1, false)]
    [TestCase(8, false)]
    [TestCase(20, false)]
    [TestCase(8, true)]
    public void MeasureBinding(int count, bool ticking)
    {
        var entries = new StatusHudEntry[120];
        for (int i = 0; i < entries.Length; i++) entries[i] = Entry(ticking ? 10 - i / 60f : 10);
        for (int warm = 0; warm < 8; warm++)
            for (int i = 0; i < count; i++) bindings[i](entries[0]);
        int setups = Counter("DiagnosticVisualSetupCount", count);
        int formats = Counter("DiagnosticStackFormatCount", count);
        var times = new double[21];
        for (int sample = 0; sample < times.Length; sample++)
        {
            long start = Stopwatch.GetTimestamp();
            for (int frame = 0; frame < entries.Length; frame++)
                for (int i = 0; i < count; i++) bindings[i](entries[frame]);
            times[sample] = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency / entries.Length;
        }
        Array.Sort(times);
        TestContext.WriteLine(FormattableString.Invariant(
            $"HUDBENCH,entries={count},ticking={ticking},median_ms={times[10]:F6},p95_ms={times[19]:F6},setups={Counter("DiagnosticVisualSetupCount", count) - setups},formats={Counter("DiagnosticStackFormatCount", count) - formats}"));
        Assert.AreEqual("5", Text("stackText"));
        Assert.AreEqual(entries[119].RemainingTime.ToString("0.0"), Text("durationText"));
    }

    private Component Field(string name) => (Component)viewType.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(views[0]);
    private string Text(string name) => (string)Field(name).GetType().GetProperty("text").GetValue(Field(name));

    [Test]
    public void StackProgressTimerAndPoolReuseRemainCorrect()
    {
        bindings[0](Entry());
        Assert.AreEqual("5", Text("stackText"));
        bindings[0](Entry(3.14f, 7));
        Assert.AreEqual("7", Text("stackText"));
        Assert.AreEqual(3.14f.ToString("0.0"), Text("durationText"));
        bindings[0](Entry(progress: "42%"));
        Assert.AreEqual("42%", Text("stackText"));
        bindings[0](Entry(stacks: 0));
        Assert.AreEqual("", Text("stackText"));
        bindings[0](Entry(visible: false));
        Assert.IsFalse(objects[0].activeSelf);
        bindings[0](Entry(0, 9));
        Assert.IsTrue(objects[0].activeSelf);
        Assert.AreEqual("9", Text("stackText"));
        Assert.AreEqual("", Text("durationText"));
    }

    [Test]
    public void StableStacksDoNotReinitializeOrReformatButTimerStillUpdates()
    {
        bindings[0](Entry());
        int setups = Counter("DiagnosticVisualSetupCount", 1);
        int formats = Counter("DiagnosticStackFormatCount", 1);
        for (int i = 0; i < 60; i++) bindings[0](Entry(9 - i / 60f));
        Assert.AreEqual(setups, Counter("DiagnosticVisualSetupCount", 1));
        Assert.AreEqual(formats, Counter("DiagnosticStackFormatCount", 1));
        Assert.AreEqual((9 - 59 / 60f).ToString("0.0"), Text("durationText"));
        bindings[0](Entry(showStacks: false));
        Assert.AreEqual("", Text("stackText"));
        bindings[0](Entry());
        Assert.AreEqual("5", Text("stackText"));
        Assert.AreEqual(formats + 1, Counter("DiagnosticStackFormatCount", 1));
    }

    [Test]
    public void HighlightAndProgressLayoutRestoreAcrossReuse()
    {
        bindings[0](Entry());
        var textRect = Field("stackText").GetComponent<RectTransform>();
        Vector2 originalMin = textRect.anchorMin, originalMax = textRect.anchorMax;
        Component background = Field("backgroundImage");
        var color = background.GetType().GetProperty("color");
        var normalColor = (Color)color.GetValue(background);
        bindings[0](Entry(progress: "50%", highlighted: true));
        Assert.AreEqual(Vector2.zero, textRect.anchorMin);
        Assert.AreEqual(Vector2.one, textRect.anchorMax);
        Assert.AreNotEqual(normalColor, color.GetValue(background));
        bindings[0](Entry(visible: false));
        bindings[0](Entry());
        Assert.AreEqual(normalColor, color.GetValue(background));
        Assert.AreEqual(originalMin, textRect.anchorMin);
        Assert.AreEqual(originalMax, textRect.anchorMax);
        Assert.AreEqual("5", Text("stackText"));
    }
}
#endif
