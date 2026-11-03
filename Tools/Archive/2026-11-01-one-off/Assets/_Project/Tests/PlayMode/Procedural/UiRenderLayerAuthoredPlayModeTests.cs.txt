#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Real retained authored subtrees. This file is copied into Assets only at the root compile checkpoint.
public sealed class UiRenderLayerAuthoredPlayModeTests
{
    const string Fixtures = "Assets/_Project/Tests/Fixtures/UIBatchingLegacy/";
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly Type RowType = Type.GetType("KeyBindingRowUI, UI", true);
    static readonly Type PanelType = Type.GetType("KeyBindingPanelUI, UI", true);
    static readonly Type LayoutType = Type.GetType("UiRenderLayerLayout, UI", true);
    static readonly Type AuthorType = Type.GetType("UiRenderLayerAuthoring, Editor", true);
    static readonly Type TmpType = Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro", true);
    static readonly Type FontType = Type.GetType("LocalizeTmpFontEvent, UI", true);
    static readonly Type StringType = Type.GetType("UnityEngine.Localization.Components.LocalizeStringEvent, Unity.Localization", true);
    static readonly Type Localization = Type.GetType("UnityEngine.Localization.Settings.LocalizationSettings, Unity.Localization", true);
    static readonly Type LocaleType = Type.GetType("UnityEngine.Localization.Locale, Unity.Localization", true);
    static readonly Type HybridType = Type.GetType("HybridRectClipView, UI", false);
    static readonly Type LiveType = Type.GetType("UiBatchingLiveScenarios, Editor", true);
    object previousLocale;
    bool changedLocale;

    [UnityTearDown]
    public IEnumerator RestoreLocale()
    {
        if (!changedLocale) yield break;
        yield return WaitLocalization(); // Do not release an unfinished current locale preload.
        Localization.GetProperty("SelectedLocale").SetValue(null, previousLocale);
        yield return WaitLocalization(); changedLocale = false; previousLocale = null;
    }

    [UnityTest]
    public IEnumerator LegacyVsLayered_FiveLocaleGeometryPixelsScrollAndListeningRemainEquivalent()
    {
        yield return (IEnumerator)LiveType.GetMethod("WaitForAppLocaleBootstrap", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        yield return WaitLocalization();
        previousLocale = Localization.GetProperty("SelectedLocale").GetValue(null); changedLocale = true;
        foreach (string code in new[] { "ko", "en", "ja", "zh-Hans", "zh-Hant" })
        {
            Object locale = AssetDatabase.LoadAssetAtPath("Assets/_Project/Data/Localization/Locale_" + code + ".asset", LocaleType);
            Assert.That(locale, Is.Not.Null);
            yield return WaitLocalization();
            Localization.GetProperty("SelectedLocale").SetValue(null, locale);
            yield return WaitLocalization();
            foreach (string screen in new[] { "SettingUI", "TitleSettingUI", "KeyMappingUI" })
            {
                using var pair = new Pair(screen, code);
                yield return WaitFonts(pair.Old.Root); yield return WaitFonts(pair.New.Root);
                foreach (float scroll in new[] { 1f, .5f, 0f })
                {
                    pair.SetScroll(scroll);
                    yield return null; yield return null;
                    pair.AssertGeometryAndFonts();
                    AssertLocale(code);
                    ComparePixels(pair.Render(pair.Old), pair.Render(pair.New), screen + "/" + code + "/scroll=" + scroll);
                    pair.AssertButtonGeometryAndNavigation();
                    pair.AssertPointerButtons();
                }
                if (screen == "KeyMappingUI")
                {
                    foreach (var state in new[] { new Vector2(1, 0), new Vector2(0, 1), Vector2.zero })
                    {
                        pair.SetListening(state.x != 0, state.y != 0);
                        yield return null; yield return null;
                        pair.AssertGeometryAndFonts();
                        ComparePixels(pair.Render(pair.Old), pair.Render(pair.New), code + "/listening=" + state);
                        pair.AssertButtonGeometryAndNavigation();
                    }
                }
                pair.Old.Root.SetActive(false); pair.New.Root.SetActive(false);
                pair.Old.Root.SetActive(true); pair.New.Root.SetActive(true);
                yield return null; yield return null;
                pair.AssertGeometryAndFonts();
                ComparePixels(pair.Render(pair.Old), pair.Render(pair.New), screen + "/" + code + "/reopen");
            }
        }
    }

    [UnityTest]
    public IEnumerator ActualKeyRow_PrimarySecondaryResetClicks_TogglesAndStaticProjectionPreserveOwnership()
    {
        yield return (IEnumerator)LiveType.GetMethod("WaitForAppLocaleBootstrap", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        yield return WaitLocalization();
        using var pair = new Pair("KeyMappingUI", "en");
        yield return WaitFonts(pair.Old.Root); yield return WaitFonts(pair.New.Root);
        pair.SetScroll(1f); yield return null; yield return null;
        pair.AssertButtonGeometryAndNavigation();
        foreach (bool secondary in new[] { false, true })
        {
            pair.InvokeRowButton(secondary ? "secondaryBindingButton" : "primaryBindingButton");
            yield return null; yield return null;
            pair.AssertListeningState(secondary);
            pair.CancelListening();
        }
        pair.ChangeAndResetWorkingKey();
        pair.SetSecondaryVisible(false); yield return null; yield return null;
        pair.AssertGeometryAndFonts();
        ComparePixels(pair.Render(pair.Old), pair.Render(pair.New), "secondary-hidden");
        pair.SetSecondaryVisible(true); yield return null; yield return null;
        pair.AssertGeometryAndFonts();
        int writes = pair.ProjectionWrites();
        for (int frame = 0; frame < 8; frame++) { yield return null; Canvas.ForceUpdateCanvases(); }
        Assert.That(pair.ProjectionWrites(), Is.EqualTo(writes), "Actual authored static rows must not continually dirty transforms.");
    }

    static IEnumerator WaitLocalization()
    {
        double deadline = Time.realtimeSinceStartupAsDouble + 30;
        object previousHandle = null, previousSelected = null;
        int stableFrames = 0;
        string state = "not sampled";
        while (Time.realtimeSinceStartupAsDouble < deadline)
        {
            object handle = Localization.GetProperty("InitializationOperation").GetValue(null);
            bool ready = HandleSucceeded(handle, "current initialization", out state);
            object selected = Localization.GetProperty("SelectedLocale").GetValue(null);
            if (ready)
            {
                foreach (string databaseName in new[] { "StringDatabase", "AssetDatabase" })
                {
                    object database = Localization.GetProperty(databaseName).GetValue(null);
                    object preload = database.GetType().GetProperty("PreloadOperation").GetValue(database);
                    ready &= HandleSucceeded(preload, databaseName + " preload", out string preloadState);
                    state += "; " + preloadState;
                }
            }
            // SelectedLocale can be written before its new initialization finishes.
            // Re-read the current handle and leave completed callbacks two full frames
            // before another write; one old completed handle is not readiness evidence.
            stableFrames = ready && handle.Equals(previousHandle) && ReferenceEquals(selected, previousSelected)
                ? stableFrames + 1 : ready ? 1 : 0;
            previousHandle = handle; previousSelected = selected;
            if (stableFrames >= 3) yield break;
            yield return null;
        }
        Assert.Fail("Current locale initialization/preloads did not settle within 30 wall-clock seconds: " + state);
    }
    static bool HandleSucceeded(object handle, string label, out string state)
    {
        Type type = handle.GetType();
        bool valid = (bool)type.GetMethod("IsValid").Invoke(handle, null);
        bool done = valid && (bool)type.GetProperty("IsDone").GetValue(handle);
        string status = valid ? type.GetProperty("Status").GetValue(handle).ToString() : "invalid";
        state = label + ":valid=" + valid + ",done=" + done + ",status=" + status;
        if (done && status == "Failed")
            Assert.Fail(state + ",exception=" + type.GetProperty("OperationException").GetValue(handle));
        return done && status == "Succeeded";
    }
    static IEnumerator WaitFonts(GameObject root)
    {
        double deadline = Time.realtimeSinceStartupAsDouble + 30;
        int stable = 0;
        while (Time.realtimeSinceStartupAsDouble < deadline)
        {
            bool ready = true;
            foreach (Component binding in root.GetComponentsInChildren(FontType, true))
            {
                if (!((Behaviour)binding).isActiveAndEnabled) continue;
                object reference = FontType.GetProperty("AssetReference").GetValue(binding);
                object handle = reference.GetType().GetProperty("CurrentLoadingOperationHandle").GetValue(reference);
                bool valid = (bool)handle.GetType().GetMethod("IsValid").Invoke(handle, null);
                ready &= valid && (bool)handle.GetType().GetProperty("IsDone").GetValue(handle);
                if (!valid || !(bool)handle.GetType().GetProperty("IsDone").GetValue(handle)) continue;
                Assert.That(handle.GetType().GetProperty("Status").GetValue(handle).ToString(), Is.EqualTo("Succeeded"));
                ready &= ReferenceEquals(handle.GetType().GetProperty("Result").GetValue(handle), TmpType.GetProperty("font").GetValue(binding.GetComponent(TmpType)));
            }
            foreach (Component binding in root.GetComponentsInChildren(StringType, true))
            {
                if (!((Behaviour)binding).isActiveAndEnabled) continue;
                object reference = StringType.GetProperty("StringReference").GetValue(binding);
                object handle = reference.GetType().GetProperty("CurrentLoadingOperationHandle").GetValue(reference);
                bool valid = (bool)handle.GetType().GetMethod("IsValid").Invoke(handle, null);
                if (!valid || !(bool)handle.GetType().GetProperty("IsDone").GetValue(handle)) { ready = false; continue; }
                Assert.That(handle.GetType().GetProperty("Status").GetValue(handle).ToString(), Is.EqualTo("Succeeded"));
                object result = handle.GetType().GetProperty("Result").GetValue(handle);
                object table = result.GetType().GetProperty("Table").GetValue(result);
                object identifier = table.GetType().GetProperty("LocaleIdentifier").GetValue(table);
                object locale = Localization.GetProperty("SelectedLocale").GetValue(null);
                Assert.That(identifier, Is.EqualTo(LocaleType.GetProperty("Identifier").GetValue(locale)), "String callback completed for a different locale.");
            }
            stable = ready ? stable + 1 : 0;
            if (stable >= 3) yield break;
            yield return null;
        }
        Assert.Fail("Actual authored font callbacks did not settle.");
    }
    static void AssertLocale(string code)
    {
        object locale = Localization.GetProperty("SelectedLocale").GetValue(null);
        object identifier = LocaleType.GetProperty("Identifier").GetValue(locale);
        Assert.That(identifier.GetType().GetProperty("Code").GetValue(identifier), Is.EqualTo(code));
    }
    static void ComparePixels(Color32[] a, Color32[] b, string context)
    {
        int visible = 0, changed = 0, max = 0;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i].a > 8) visible++;
            int delta = Mathf.Max(Mathf.Abs(a[i].r - b[i].r), Mathf.Abs(a[i].g - b[i].g), Mathf.Abs(a[i].b - b[i].b), Mathf.Abs(a[i].a - b[i].a));
            if (delta > 1) changed++; max = Mathf.Max(max, delta);
        }
        TestContext.Out.WriteLine($"AUTHORED_LAYER_PIXELS,{context},visible={visible},changed={changed},maxRGBA={max},tolerance=1");
        Assert.That(visible, Is.GreaterThan(128), "An empty fixture is not equivalence evidence.");
        Assert.That(changed, Is.Zero, context + " raster/stacking regression.");
    }

    sealed class View
    {
        public GameObject Root, Ghost;
        public Component Panel;
        public RectTransform Background, Content;
        public ScrollRect Scroll;
        public readonly List<Component> Rows = new();
        public readonly Dictionary<string, RectTransform> Rectangles = new();
        public readonly Dictionary<string, Component> Texts = new();
        public readonly Dictionary<string, Button> Buttons = new();
    }
    sealed class Pair : IDisposable
    {
        readonly GameObject staging, canvasObject, cameraObject;
        readonly Camera camera;
        readonly RenderTexture texture;
        readonly Texture2D readback;
        public readonly View Old, New;
        readonly string screen;
        public Pair(string screen, string code)
        {
            this.screen = screen;
            staging = new GameObject("Inactive authored regression staging"); staging.SetActive(false);
            cameraObject = new GameObject("Authored layer regression camera", typeof(Camera)); camera = cameraObject.GetComponent<Camera>();
            camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 1;
            camera.transform.position = new Vector3(0, 0, -10); camera.nearClipPlane = .1f; camera.farClipPlane = 100;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear; camera.cullingMask = 1 << 31;
            camera.allowHDR = false; camera.allowMSAA = false;
            texture = new RenderTexture(1024, 768, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear); texture.Create(); camera.targetTexture = texture;
            readback = new Texture2D(1024, 768, TextureFormat.RGBA32, false, true);
            canvasObject = new GameObject("Authored layer regression canvas", typeof(RectTransform), typeof(Canvas)); canvasObject.layer = 31;
            Canvas canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1; canvas.scaleFactor = .4f;
            canvasObject.AddComponent<GraphicRaycaster>();
            Old = Create(screen, code, false, canvas); New = Create(screen, code, true, canvas);
            Old.Root.SetActive(true); New.Root.SetActive(true); Canvas.ForceUpdateCanvases();
        }

        View Create(string name, string code, bool candidate, Canvas canvas)
        {
            GameObject original = AssetDatabase.LoadAssetAtPath<GameObject>(Fixtures + name + ".prefab");
            Assert.That(original, Is.Not.Null, "Root must retain legacy UI fixtures before render authoring.");
            var view = new View { Root = Object.Instantiate(original, staging.transform, false) };
            view.Root.name = name;
            GuardReferences(view.Root);
            if (candidate) AuthorType.GetMethod("AuthorPanels").Invoke(null, new object[] { view.Root });
            view.Background = (RectTransform)view.Root.transform.Find("Background");
            view.Content = (RectTransform)view.Background.Find("ContentsMask/Contents");
            view.Scroll = view.Root.GetComponentInChildren<ScrollRect>(true);
            Assert.That(view.Background, Is.Not.Null); Assert.That(view.Content, Is.Not.Null); Assert.That(view.Scroll, Is.Not.Null);
            if (name == "KeyMappingUI")
            {
                // A retained inactive owner executes only explicit row/rebind methods;
                // its Awake/UI-stack/profile callbacks never run in this isolated view.
                view.Ghost = Object.Instantiate(original, staging.transform, false); view.Ghost.name = "Inactive logical owner";
                view.Panel = view.Ghost.GetComponent(PanelType);
                PanelType.GetMethod("LoadWorkingBindingsFromService", F).Invoke(view.Panel, null);
                CreateRows(view, code, candidate);
            }
            CacheSemanticObjects(view);
            StripAndGuard(view.Root);
            if (HybridType != null)
                foreach (Component clip in view.Root.GetComponentsInChildren(HybridType, true))
                    HybridType.GetField("rootCanvas", F).SetValue(clip, canvas);
            view.Root.transform.SetParent(canvas.transform, false);
            foreach (Transform child in view.Root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
            return view;
        }

        void CreateRows(View view, string code, bool candidate)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Fixtures + "KeyMapRow.prefab"); Assert.That(prefab, Is.Not.Null);
            object actions = PanelType.GetMethod("ResolveActionOrder", F).Invoke(view.Panel, null);
            int index = 0;
            foreach (object action in (IEnumerable)actions)
            {
                GameObject row = Object.Instantiate(prefab, view.Content, false); row.name = "Row_" + index++;
                Component controller = row.GetComponent(RowType); view.Rows.Add(controller);
                if (candidate) AuthorRow(row);
                RowType.GetMethod("Bind").Invoke(controller, new[] { view.Panel, action });
                if (candidate)
                {
                    row.transform.SetSiblingIndex(index - 1);
                    RowType.GetMethod("BindRenderLayout").Invoke(controller, new[] { view.Root.GetComponent(LayoutType) });
                }
                GuardReferences(row);
            }
            Assert.That(view.Rows.Count, Is.GreaterThan(8), "The real remappable action list is required.");
        }

        static void AuthorRow(GameObject row)
        {
            var candidates = new List<RectTransform>();
            AuthorType.GetMethod("Collect", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { row.transform, candidates, true, true });
            Type bindingType = LayoutType.GetNestedType("Binding", BindingFlags.Public);
            Array entries = Array.CreateInstance(bindingType, candidates.Count);
            for (int i = 0; i < candidates.Count; i++)
            {
                object layer = AuthorType.GetMethod("Layer", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { candidates[i] });
                entries.SetValue(AuthorType.GetMethod("Wrap", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new[] { (object)candidates[i], layer, true }), i);
            }
            Component layout = row.AddComponent(LayoutType);
            LayoutType.GetMethod("ConfigureAuthored").Invoke(layout, new object[] { Array.Empty<RectTransform>(), entries });
        }

        static void CacheSemanticObjects(View view)
        {
            foreach (RectTransform rectangle in view.Root.GetComponentsInChildren<RectTransform>(true))
            {
                if (rectangle.name.EndsWith("_Layout") || rectangle.name.EndsWith("_Render") || rectangle.name.StartsWith("Render_")) continue;
                // Store semantic paths through the authored slot/anchor wrappers before activation.
                string key = Path(view.Root.transform, rectangle); view.Rectangles.Add(key, rectangle);
                Component text = rectangle.GetComponent(TmpType); if (text != null) view.Texts.Add(key, text);
                Button button = rectangle.GetComponent<Button>(); if (button != null) view.Buttons.Add(key, button);
            }
        }
        static string Path(Transform root, Transform child)
        {
            var pieces = new List<string>();
            while (child != root)
            {
                Assert.That(child, Is.Not.Null);
                Transform slot = child;
                if (child.parent != null && child.parent.name == child.name + "_Render" && child.parent.parent != null &&
                    child.parent.parent.name == child.name + "_Layout") slot = child.parent.parent;
                int ordinal = 0;
                if (slot.parent != null)
                    for (int i = 0; i < slot.GetSiblingIndex(); i++) if (slot.parent.GetChild(i).name == slot.name) ordinal++;
                pieces.Insert(0, child.name + "[" + ordinal + "]");
                child = slot.parent;
            }
            return string.Join("/", pieces);
        }
        static void StripAndGuard(GameObject root)
        {
            foreach (MonoBehaviour script in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (script is not UnityEngine.EventSystems.UIBehaviour && script.GetType() != FontType && script.GetType().Name != "LocalizeStringEvent" &&
                    script.GetType() != LayoutType && script.GetType() != HybridType && script.GetType() != RowType) Object.DestroyImmediate(script);
            foreach (Component control in root.GetComponentsInChildren<UnityEngine.EventSystems.UIBehaviour>(true))
            {
                var serialized = new SerializedObject(control); var property = serialized.GetIterator();
                while (property.Next(true)) if (property.isArray && property.propertyPath.EndsWith(".m_PersistentCalls.m_Calls")) property.arraySize = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            GuardReferences(root);
        }
        static void GuardReferences(GameObject root)
        {
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue;
                string type = component.GetType().Name;
                if (type != "LocalizeTmpFontEvent" && type != "LocalizeStringEvent" && type != "ScrollRect" && type != "UiRenderLayerLayout" && type != "KeyBindingRowUI") continue;
                var serialized = new SerializedObject(component); var property = serialized.GetIterator();
                while (property.Next(true)) if (property.propertyType == SerializedPropertyType.ObjectReference)
                {
                    Object reference = property.objectReferenceValue;
                    Transform target = reference is Component c ? c.transform : reference is GameObject go ? go.transform : null;
                    if (target != null) Assert.That(target.IsChildOf(root.transform), Is.True, "Reference escaped inactive fixture: " + type + "/" + property.propertyPath);
                }
            }
        }

        public void SetScroll(float position) { foreach (View view in new[] { Old, New }) view.Scroll.verticalNormalizedPosition = position; Canvas.ForceUpdateCanvases(); }
        public void SetListening(bool primary, bool secondary)
        { foreach (View view in new[] { Old, New }) foreach (Component row in view.Rows.Take(2)) RowType.GetMethod("SetListeningState").Invoke(row, new object[] { primary, secondary }); }
        public void SetSecondaryVisible(bool visible)
        { foreach (View view in new[] { Old, New }) foreach (Component row in view.Rows.Take(2)) ((GameObject)RowType.GetField("secondaryRoot", F).GetValue(row)).SetActive(visible); }
        public void InvokeRowButton(string buttonName)
        { foreach (View view in new[] { Old, New }) ((Button)RowType.GetField(buttonName, F).GetValue(view.Rows[0])).onClick.Invoke(); }
        public void AssertListeningState(bool secondary)
        {
            foreach (View view in new[] { Old, New })
            {
                Assert.That(PanelType.GetField("listeningRow", F).GetValue(view.Panel), Is.SameAs(view.Rows[0]));
                Assert.That((bool)PanelType.GetField("listeningSecondary", F).GetValue(view.Panel), Is.EqualTo(secondary));
                Assert.That(((Button)RowType.GetField("primaryBindingButton", F).GetValue(view.Rows[0])).interactable, Is.False);
                Assert.That(((Button)RowType.GetField("resetButton", F).GetValue(view.Rows[0])).interactable, Is.False);
            }
        }
        public void CancelListening() { foreach (View view in new[] { Old, New }) PanelType.GetMethod("CancelRebind", F).Invoke(view.Panel, null); }
        public void ChangeAndResetWorkingKey()
        {
            foreach (View view in new[] { Old, New })
            {
                object action = RowType.GetProperty("Action").GetValue(view.Rows[0]);
                PanelType.GetMethod("SetWorkingKey", F).Invoke(view.Panel, new[] { action, (object)false, KeyCode.F12 });
                RowType.GetMethod("Refresh").Invoke(view.Rows[0], null);
                ((Button)RowType.GetField("resetButton", F).GetValue(view.Rows[0])).onClick.Invoke();
                Assert.That((bool)PanelType.GetMethod("CanResetAction").Invoke(view.Panel, new[] { action }), Is.False);
            }
        }
        public int ProjectionWrites() => New.Root.GetComponentsInChildren(LayoutType, true).Cast<Component>().Sum(c => (int)LayoutType.GetProperty("DiagnosticTransformWrites").GetValue(c));
        public void AssertGeometryAndFonts()
        {
            Canvas.ForceUpdateCanvases();
            CollectionAssert.AreEquivalent(Old.Rectangles.Keys, New.Rectangles.Keys);
            foreach (string key in Old.Rectangles.Keys)
            {
                RectTransform a = Old.Rectangles[key], b = New.Rectangles[key];
                Assert.That(b.gameObject.activeInHierarchy, Is.EqualTo(a.gameObject.activeInHierarchy), key + " visibility");
                if (!a.gameObject.activeInHierarchy) continue;
                var ca = new Vector3[4]; var cb = new Vector3[4]; a.GetWorldCorners(ca); b.GetWorldCorners(cb);
                for (int corner = 0; corner < 4; corner++) Assert.That(Vector3.Distance(ca[corner], cb[corner]), Is.LessThan(.01f), key + " corner " + corner);
            }
            foreach (string key in Old.Texts.Keys)
            {
                Assert.That(TmpType.GetProperty("font").GetValue(New.Texts[key]), Is.SameAs(TmpType.GetProperty("font").GetValue(Old.Texts[key])), key + " font");
                Assert.That(TmpType.GetProperty("text").GetValue(New.Texts[key]), Is.EqualTo(TmpType.GetProperty("text").GetValue(Old.Texts[key])), key + " text");
            }
        }
        public void AssertButtonGeometryAndNavigation()
        {
            Dictionary<string, ButtonState> before = SnapshotButtons(Old);
            Dictionary<string, ButtonState> after = SnapshotButtons(New);
            foreach (string key in Old.Buttons.Keys)
            {
                ButtonState a = before[key], b = after[key];
                Assert.That(b.active, Is.EqualTo(a.active), key + " active state");
                Assert.That(b.interactable, Is.EqualTo(a.interactable), key + " interactability");
                Assert.That(b.mode, Is.EqualTo(a.mode), key + " navigation mode");
                Assert.That(a.ownedTarget && b.ownedTarget, Is.True, key + " owned targetGraphic");
                if (!a.active) continue;
                Assert.That(Vector2.Distance(a.targetSize, b.targetSize), Is.LessThan(.01f), key + " targetGraphic rectangle");
                for (int corner = 0; corner < 4; corner++) Assert.That(Vector3.Distance(a.targetCorners[corner], b.targetCorners[corner]), Is.LessThan(.01f), key + " targetGraphic corner " + corner);
                CollectionAssert.AreEqual(a.navigation, b.navigation, key + " automatic navigation targets");
            }
            Old.Root.SetActive(true); New.Root.SetActive(true); Canvas.ForceUpdateCanvases();
        }
        sealed class ButtonState
        {
            public bool active, interactable, ownedTarget;
            public Navigation.Mode mode;
            public Vector2 targetSize;
            public Vector3[] targetCorners;
            public string[] navigation;
        }
        Dictionary<string, ButtonState> SnapshotButtons(View view)
        {
            PrepareRender(view);
            var result = new Dictionary<string, ButtonState>();
            foreach (var entry in view.Buttons)
            {
                Button button = entry.Value;
                var state = new ButtonState { active = button.gameObject.activeInHierarchy, interactable = button.interactable,
                    mode = button.navigation.mode, ownedTarget = button.targetGraphic != null && button.targetGraphic.transform.IsChildOf(view.Root.transform),
                    targetCorners = new Vector3[4], navigation = NavigationTargets(view, button) };
                if (button.targetGraphic != null) { state.targetSize = button.targetGraphic.rectTransform.rect.size; button.targetGraphic.rectTransform.GetWorldCorners(state.targetCorners); }
                result.Add(entry.Key, state);
            }
            return result;
        }
        static string[] NavigationTargets(View view, Button button) => new[] { Vector3.left, Vector3.right, Vector3.up, Vector3.down }
            .Select(direction => view.Buttons.FirstOrDefault(p => p.Value == button.FindSelectable(direction)).Key).ToArray();
        public void AssertPointerButtons()
        {
            // One actual render per side establishes native Graphic depth/culling.
            // Raycasts only observe that frame; every button retains a strict route check.
            Dictionary<string, PointerState> before = SnapshotPointers(Old);
            Dictionary<string, PointerState> after = SnapshotPointers(New);
            foreach (string key in Old.Buttons.Keys)
            {
                Assert.That(after[key].active, Is.EqualTo(before[key].active), key + " pointer active state");
                if (!before[key].active) continue;
                CollectionAssert.AreEqual(before[key].routes, after[key].routes, key + " pointer click routing\nBaseline: " +
                    before[key].diagnostics + "\nCandidate: " + after[key].diagnostics);
            }
            Old.Root.SetActive(true); New.Root.SetActive(true); Canvas.ForceUpdateCanvases();
        }
        sealed class PointerState
        {
            public bool active;
            public string[] routes;
            public string diagnostics;
        }
        Dictionary<string, PointerState> SnapshotPointers(View view)
        {
            PrepareRender(view);
            var snapshot = new Dictionary<string, PointerState>();
            foreach (var entry in view.Buttons)
            {
                Button button = entry.Value;
                snapshot.Add(entry.Key, button.gameObject.activeInHierarchy
                    ? HitRenderedButton(view, button)
                    : new PointerState { active = false, routes = Array.Empty<string>(), diagnostics = "inactive hierarchy" });
            }
            return snapshot;
        }
        PointerState HitRenderedButton(View view, Button button)
        {
            var results = new List<UnityEngine.EventSystems.RaycastResult>();
            var point = new UnityEngine.EventSystems.PointerEventData(null) { position = RectTransformUtility.WorldToScreenPoint(camera, button.transform.position) };
            canvasObject.GetComponent<GraphicRaycaster>().Raycast(point, results);
            string hits = string.Join(" | ", results.Select(hit => Path(view.Root.transform, hit.gameObject.transform) + ":" +
                GraphicDiagnostics(hit.gameObject.GetComponent<Graphic>())));
            string containing = string.Join(" | ", view.Root.GetComponentsInChildren<Graphic>(true)
                .Where(graphic => graphic.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(graphic.rectTransform, point.position, camera))
                .Select(graphic => Path(view.Root.transform, graphic.transform) + ":" + GraphicDiagnostics(graphic)));
            return new PointerState
            {
                active = button.gameObject.activeInHierarchy,
                diagnostics = "point=" + point.position + ",buttonActive=" + button.gameObject.activeInHierarchy +
                    ",target=" + GraphicDiagnostics(button.targetGraphic) + ",hits=[" + hits + "],containing=[" + containing + "]",
                routes = results.Select(hit => hit.gameObject.GetComponentInParent<Button>()).Where(hit => hit != null)
                    .Select(hit => view.Buttons.FirstOrDefault(p => p.Value == hit).Key).Distinct().ToArray()
            };
        }
        static string GraphicDiagnostics(Graphic graphic) => graphic == null ? "null" :
            "rect=" + graphic.rectTransform.rect + ",enabled=" + graphic.enabled + ",raycast=" + graphic.raycastTarget +
            ",depth=" + graphic.depth + ",cull=" + graphic.canvasRenderer.cull;
        void PrepareRender(View view)
        {
            Old.Root.SetActive(false); New.Root.SetActive(false); view.Root.SetActive(true);
            foreach (Component text in view.Root.GetComponentsInChildren(TmpType, true)) if (((Behaviour)text).isActiveAndEnabled) TmpType.GetMethod("ForceMeshUpdate").Invoke(text, new object[] { false, false });
            Canvas.ForceUpdateCanvases();
            if (GraphicsSettings.currentRenderPipeline != null)
            { var request = new RenderPipeline.StandardRequest { destination = texture }; Assert.IsTrue(RenderPipeline.SupportsRenderRequest(camera, request)); RenderPipeline.SubmitRenderRequest(camera, request); }
            else camera.Render();
        }
        public Color32[] Render(View view, bool restoreBoth = true)
        {
            PrepareRender(view);
            RenderTexture previous = RenderTexture.active;
            try { RenderTexture.active = texture; readback.ReadPixels(new Rect(0, 0, 1024, 768), 0, 0); readback.Apply(); return readback.GetPixels32(); }
            finally { RenderTexture.active = previous; if (restoreBoth) { Old.Root.SetActive(true); New.Root.SetActive(true); Canvas.ForceUpdateCanvases(); } }
        }
        public void Dispose()
        {
            Object.DestroyImmediate(canvasObject); Object.DestroyImmediate(staging); Object.DestroyImmediate(cameraObject);
            texture.Release(); Object.DestroyImmediate(texture); Object.DestroyImmediate(readback); Canvas.ForceUpdateCanvases();
        }
    }
}
#endif
