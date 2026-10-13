#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Globalization;
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

    [TestCase("en-US")]
    [TestCase("fr-FR")]
    [TestCase("ko-KR")]
    public void DurationText_PreservesRuntimeRoundingCultureAndReuse(string cultureName)
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        CultureInfo culture = (CultureInfo)new CultureInfo(cultureName).Clone();
        try
        {
            CultureInfo.CurrentCulture = culture;
            float[] values = { 0.01f, 0.049999f, 0.05f, 0.050001f, 1.149999f, 1.15f, 1.150001f, 9.95f, float.MaxValue };
            foreach (float value in values)
            {
                bindings[0](Entry(value));
                Assert.AreEqual(value.ToString("0.0"), Text("durationText"));
            }
            bindings[0](Entry(1.21f));
            string first = Text("durationText");
            bindings[0](Entry(1.22f));
            Assert.AreSame(first, Text("durationText"), "Identical display characters reuse their string.");
            culture.NumberFormat.NumberDecimalSeparator = "::";
            bindings[0](Entry(1.22f));
            Assert.AreEqual(1.22f.ToString("0.0"), Text("durationText"), "Mutable culture formatting is observed.");
            bindings[0](Entry(0f));
            Assert.AreEqual(string.Empty, Text("durationText"));
            objects[0].SetActive(false);
            bindings[0](Entry(1.22f));
            Assert.IsTrue(objects[0].activeSelf);
            Assert.AreEqual(1.22f.ToString("0.0"), Text("durationText"));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    private sealed class PopupStub : IStackableUI
    {
        public bool IsActive { get; private set; } = true;
        public bool CanCloseOnEscape => true;
        public UIOpenGroup OpenGroup => UIOpenGroup.Overlay;
        public UIOpenGroup BlockedOpenGroups => UIOpenGroup.None;
        public UIGameplayLockProfile GameplayLockProfile => UIGameplayLockProfile.None;
        public void OpenUI() => IsActive = true;
        public void CloseUI() => IsActive = false;
    }

    [Test]
    public void PopupQueries_PreserveStackOrderingAndDetachedMutationSnapshot()
    {
        Type type = Type.GetType("PopupStackState, UI", true);
        object stack = Activator.CreateInstance(type);
        var first = new PopupStub();
        var second = new PopupStub();
        void Push(IStackableUI value) => type.GetMethod("Push").Invoke(stack, new object[] { value });
        IReadOnlyList<IStackableUI> Read() => (IReadOnlyList<IStackableUI>)type.GetMethod("ReadEntries").Invoke(stack, null);
        Push(first); Push(second); Push(first);
        var entries = Read();
        Assert.AreEqual(2, entries.Count);
        Assert.AreSame(second, entries[0]);
        Assert.AreSame(first, entries[1]);
        Assert.AreSame(entries, Read(), "Read-only queries do not copy the stack.");
        var snapshot = (IReadOnlyList<IStackableUI>)type.GetMethod("Snapshot").Invoke(stack, null);
        foreach (IStackableUI ui in snapshot)
            type.GetMethod("Remove").Invoke(stack, new object[] { ui });
        Assert.AreEqual(0, Read().Count);
        Assert.AreEqual(2, snapshot.Count, "Mutation callbacks retain the original snapshot contents.");
    }

    [Test]
    public void RunTimerText_PreservesCeilingCustomFormatAndCultureChange()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        Type type = Type.GetType("RunTimerHUD, UI", true);
        var go = new GameObject("Timer text regression", typeof(RectTransform));
        go.SetActive(false);
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            var text = go.AddComponent(Field("durationText").GetType());
            var hud = go.AddComponent(type);
            type.GetField("timeText", flags).SetValue(hud, text);
            MethodInfo update = type.GetMethod("UpdateTimeText", flags);
            PropertyInfo textProperty = text.GetType().GetProperty("text");
            foreach (float value in new[] { 61.01f, 61f, 60.99f, 0f })
            {
                update.Invoke(hud, new object[] { value, false, false });
                int seconds = Mathf.CeilToInt(value);
                Assert.AreEqual(string.Format("{0:00}:{1:00}", seconds / 60, seconds % 60), textProperty.GetValue(text));
            }
            type.GetField("timeFormat", flags).SetValue(hud, "{0:N1}:{1:N1}");
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            update.Invoke(hud, new object[] { 61f, false, false });
            Assert.AreEqual(string.Format("{0:N1}:{1:N1}", 1, 1), textProperty.GetValue(text));
            CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator = "::";
            update.Invoke(hud, new object[] { 61f, false, false });
            Assert.AreEqual(string.Format("{0:N1}:{1:N1}", 1, 1), textProperty.GetValue(text),
                "A writable NumberFormatInfo can change while time and culture identity stay the same.");
            CultureInfo.CurrentCulture = new CultureInfo("en-US");
            update.Invoke(hud, new object[] { 61f, false, false });
            Assert.AreEqual(string.Format("{0:N1}:{1:N1}", 1, 1), textProperty.GetValue(text));
        }
        finally { CultureInfo.CurrentCulture = previous; Object.DestroyImmediate(go); }
    }

    [Test]
    public void SkillCharges_ReuseTextWhileFillTicksAndResetOnSlotRebind()
    {
        Type slotType = Type.GetType("WeaponSkillHUD2D+SkillSlotUI, UI", true);
        Type presenter = Type.GetType("WeaponSkillHudSlotPresenter, UI", true);
        var owner = new GameObject("Charge HUD regression owner");
        var root = new GameObject("Charge HUD regression slot");
        var definition = ScriptableObject.CreateInstance<UnityGAS.AbilityDefinition>();
        try
        {
            definition.useCharges = true;
            definition.maxCharges = 3;
            definition.cooldown = 10f;
            var system = owner.AddComponent<UnityGAS.AbilitySystem>();
            var spec = system.GiveAbility(definition);
            object slot = Activator.CreateInstance(slotType);
            Component NewChild(string name, Type componentType)
            {
                var child = new GameObject(name, typeof(RectTransform));
                child.transform.SetParent(root.transform, false);
                return child.AddComponent(componentType);
            }
            var cooldown = NewChild("Cooldown", Field("durationText").GetType());
            var charges = NewChild("Charges", Field("durationText").GetType());
            var fill = NewChild("Fill", Field("durationFillImage").GetType());
            slotType.GetField("root").SetValue(slot, root);
            slotType.GetField("cooldownText").SetValue(slot, cooldown);
            slotType.GetField("chargeText").SetValue(slot, charges);
            slotType.GetField("cooldownFill").SetValue(slot, fill);
            var update = presenter.GetMethod("UpdateCooldownAndCharge");
            void Refresh() => update.Invoke(null, new object[] { slot, WeaponAbilitySlot.Skill1, definition, system, null });
            string Content(Component text) => (string)text.GetType().GetProperty("text").GetValue(text);
            spec.SetInt("__Charges", 2);
            spec.SetFloat("__RechargeRemaining", 1.21f);
            Refresh();
            string first = Content(cooldown);
            Assert.AreEqual(1.21f.ToString("0.0"), first);
            Assert.AreEqual("2/3", Content(charges));
            float oldFill = (float)fill.GetType().GetProperty("fillAmount").GetValue(fill);
            spec.SetFloat("__RechargeRemaining", 1.22f);
            Refresh();
            Assert.AreSame(first, Content(cooldown));
            Assert.Greater((float)fill.GetType().GetProperty("fillAmount").GetValue(fill), oldFill,
                "A cached numeric label does not quantize the animated fill.");
            spec.SetInt("__Charges", 3);
            Refresh();
            Assert.AreEqual(string.Empty, Content(cooldown));
            Assert.AreEqual("3/3", Content(charges));
            presenter.GetMethod("ApplySlot").Invoke(null, new object[] { slot, null, Color.white, null });
            Assert.AreEqual(string.Empty, Content(charges));
            spec.SetInt("__Charges", 2);
            Refresh();
            Assert.AreEqual(1.22f.ToString("0.0"), Content(cooldown));
            Assert.AreEqual("2/3", Content(charges));
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(definition);
        }
    }

    [Test]
    public void Hover_FollowsMovingAnchorWithoutRebuildingUnchangedContent()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        Type controllerType = Type.GetType("HoverUIController, UI", true);
        Type tooltipType = Type.GetType("StatusHudTooltipView, UI", true);
        FieldInfo singleton = tooltipType.GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        object previous = singleton.GetValue(null);
        var canvasObject = new GameObject("Hover regression canvas", typeof(RectTransform), typeof(Canvas));
        var controllerObject = new GameObject("Hover regression controller");
        GameObject tooltipObject = null;
        try
        {
            singleton.SetValue(null, null);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/GlobalUIRoot.prefab");
            var authoredTooltip = (Component)prefab.GetComponentInChildren(tooltipType, true);
            tooltipObject = Object.Instantiate(authoredTooltip.gameObject, canvasObject.transform);
            tooltipObject.transform.SetParent(canvasObject.transform, false);
            var tooltip = tooltipObject.GetComponent(tooltipType);
            var controller = controllerObject.AddComponent(controllerType);
            controllerType.GetField("canvas", flags).SetValue(controller, canvasObject.GetComponent<Canvas>());
            var targetObject = new GameObject("Hover anchor", typeof(RectTransform));
            targetObject.transform.SetParent(canvasObject.transform, false);
            var anchor = targetObject.GetComponent<RectTransform>();
            anchor.sizeDelta = new Vector2(48f, 48f);
            var rect = (RectTransform)tooltipType.GetProperty("Rect").GetValue(tooltip);
            MethodInfo show = controllerType.GetMethod("ShowStatusHover");
            MethodInfo tick = controllerType.GetMethod("LateUpdate", flags);
            PropertyInfo counter = controllerType.GetProperty("DiagnosticLayoutRefreshCount");
            for (int warm = 0; warm < 4; warm++)
            {
                show.Invoke(controller, new object[] { tooltip, anchor, Entry(10f) });
                tick.Invoke(controller, null);
            }
            int baseline = (int)counter.GetValue(controller);
            Vector2 before = rect.anchoredPosition;
            anchor.anchoredPosition += Vector2.up * 40f;
            for (int i = 0; i < 30; i++)
            {
                show.Invoke(controller, new object[] { tooltip, anchor, Entry(10.01f) });
                tick.Invoke(controller, null);
            }
            Assert.AreNotEqual(before, rect.anchoredPosition, "The anchor still moves the tooltip every frame.");
            Assert.AreEqual(baseline, counter.GetValue(controller), "Stable text only follows its anchor.");
            show.Invoke(controller, new object[] { tooltip, anchor, Entry(9.5f, 7) });
            var effect = (Component)tooltipType.GetField("effectText", flags).GetValue(tooltip);
            Assert.That((string)effect.GetType().GetProperty("text").GetValue(effect), Does.Contain(9.5f.ToString("0.0")));
            Assert.Greater((int)counter.GetValue(controller), baseline);
            controllerType.GetMethod("HideImmediate").Invoke(controller, null);
            Assert.IsFalse((bool)tooltipType.GetProperty("IsActive").GetValue(tooltip));
            show.Invoke(controller, new object[] { tooltip, anchor, Entry(9.5f, 7) });
            Assert.IsTrue((bool)tooltipType.GetProperty("IsActive").GetValue(tooltip));
        }
        finally
        {
            if (tooltipObject != null) Object.DestroyImmediate(tooltipObject);
            Object.DestroyImmediate(controllerObject);
            Object.DestroyImmediate(canvasObject);
            singleton.SetValue(null, previous);
        }
    }

    [TestCase("SlimeCorridor/Pawn")]
    [TestCase("SlimeCorridor/Knight")]
    public void MonsterHud_RepairsExternalLayoutChangesWithoutChangingOwnerProfile(string prefabName)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        Type type = Type.GetType("MonsterStackStatusWorldView, UI", true);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Monsters/" + prefabName + ".prefab");
        var target = Object.Instantiate(prefab, Vector3.one * 10000f, Quaternion.identity);
        try
        {
            var profile = target.GetComponent<MonsterSizeProfile>();
            var view = target.GetComponentInChildren(type, true);
            var health = (RectTransform)type.GetField("healthBar", flags).GetValue(view);
            var row = (RectTransform)type.GetField("statusRow", flags).GetValue(view);
            MethodInfo tick = type.GetMethod("LateUpdate", flags);
            tick.Invoke(view, null);
            health.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, profile.HealthBarWidth + 25f);
            health.gameObject.SetActive(!profile.ShowHealthBar);
            row.anchoredPosition += new Vector2(12f, 8f);
            tick.Invoke(view, null);
            Assert.That(health.rect.width, Is.EqualTo(profile.HealthBarWidth).Within(0.001f));
            Assert.That(health.gameObject.activeSelf, Is.EqualTo(profile.ShowHealthBar));
            Assert.That(row.anchoredPosition, Is.EqualTo(new Vector2(-profile.HealthBarWidth * 0.5f,
                profile.ShowHealthBar ? -15f : 0f)));
            target.SetActive(false);
            health.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 1f);
            row.anchoredPosition = Vector2.one;
            target.SetActive(true);
            tick.Invoke(view, null);
            Assert.That(health.rect.width, Is.EqualTo(profile.HealthBarWidth).Within(0.001f));
            Assert.That(row.anchoredPosition, Is.EqualTo(new Vector2(-profile.HealthBarWidth * 0.5f,
                profile.ShowHealthBar ? -15f : 0f)));
        }
        finally { Object.DestroyImmediate(target); }
    }
}
#endif
