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
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

public sealed class UiRectMaskRegressionPlayModeTests
{
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const string PrefabPath = "Assets/_Project/Prefabs/UI/GlobalUIRoot.prefab";
    static readonly Type Image = Type.GetType("UnityEngine.UI.Image, UnityEngine.UI", true);
    static readonly Type Mask = Type.GetType("UnityEngine.UI.Mask, UnityEngine.UI", true);
    static readonly Type Rectangle = Type.GetType("UnityEngine.UI.RectMask2D, UnityEngine.UI", true);
    static readonly Type Hybrid = Type.GetType("HybridRectClipView, UI", true);
    static readonly Type RenderLayout = Type.GetType("UiRenderLayerLayout, UI", false);
    static readonly Type UiBehaviour = Type.GetType("UnityEngine.EventSystems.UIBehaviour, UnityEngine.UI", true);
    static readonly Type Graphic = Type.GetType("UnityEngine.UI.Graphic, UnityEngine.UI", true);
    static readonly Type Scroll = Type.GetType("UnityEngine.UI.ScrollRect, UnityEngine.UI", true);
    static readonly Type FontBinding = Type.GetType("LocalizeTmpFontEvent, UI", true);
    static readonly Type TmpText = Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro", true);
    static readonly Type Settings = Type.GetType("UnityEngine.Localization.Settings.LocalizationSettings, Unity.Localization", true);
    static readonly Type AppSettings = Type.GetType("GameSettingsService, UI", true);
    static readonly Type Locale = Type.GetType("UnityEngine.Localization.Locale, Unity.Localization", true);
    object originalLocale;
    bool restoreLocale;

    [UnityTearDown]
    public IEnumerator RestoreLocale()
    {
        if (!restoreLocale) yield break;
        yield return WaitForLocalization();
        Settings.GetProperty("SelectedLocale").SetValue(null, originalLocale);
        yield return WaitForLocalization();
        restoreLocale = false;
        originalLocale = null;
    }

    [UnityTest]
    public IEnumerator AuthoredFiveLocales_RotationFadeScrollPixelsAndInputRemainEquivalent()
    {
        yield return WaitForAppBootstrap();
        yield return WaitForLocalization();
        originalLocale = Settings.GetProperty("SelectedLocale").GetValue(null);
        restoreLocale = true;
        foreach (string code in new[] { "ko", "en", "ja", "zh-Hans", "zh-Hant" })
        {
            Object locale = AssetDatabase.LoadAssetAtPath("Assets/_Project/Data/Localization/Locale_" + code + ".asset", Locale);
            Assert.That(locale, Is.Not.Null);
            Settings.GetProperty("SelectedLocale").SetValue(null, locale);
            yield return WaitForLocalization();
            AssertSelectedLocale(code);
            foreach (string screen in new[] { "SettingUI", "KeyMappingUI" })
            {
                using var pair = new ViewportPair(screen, code);
                yield return WaitForFonts(pair.Baseline.Root);
                yield return WaitForFonts(pair.Candidate.Root);
                // Includes both intermediate rotation directions, settled scrolling
                // and a transparent CanvasGroup pose from the open/close presentation.
                foreach (var pose in new[]
                {
                    new Vector3(-10f, 160f, .45f), new Vector3(-3f, 0f, 1f),
                    new Vector3(0f, 0f, 1f), new Vector3(0f, 160f, 1f),
                    new Vector3(0f, -160f, .45f), new Vector3(3f, 0f, 1f), new Vector3(10f, 160f, 1f),
                })
                {
                    pair.SetPose(pose.x, pose.y, pose.z);
                    AssertSelectedLocale(code);
                    yield return pair.Settle(pair.Baseline);
                    AssertSelectedLocale(code);
                    Color32[] baseline = pair.Capture(pair.Baseline, prepare: false);
                    string[] baselineHits = pair.Raycast(pair.Baseline, Vector2.zero, prepare: false);
                    yield return pair.Settle(pair.Candidate);
                    AssertSelectedLocale(code);
                    Color32[] candidate = pair.Capture(pair.Candidate, prepare: false);
                    string[] candidateHits = pair.Raycast(pair.Candidate, Vector2.zero, prepare: false);
                    Assert.That((bool)Hybrid.GetProperty("DiagnosticUsesRectClip").GetValue(pair.Candidate.View), Is.EqualTo(pose.x == 0f));
                    pair.AssertFontProjection();
                    TestContext.Out.WriteLine("Hybrid capture baseline=" + pair.Baseline.CaptureState + "; candidate=" + pair.Candidate.CaptureState);
                    AssertPixels(baseline, candidate, screen + "/" + code + "/" + pose);
                    CollectionAssert.AreEqual(baselineHits, candidateHits,
                        "Viewport input order must match while rotating, fading and scrolling.");
                }
            }
        }
    }

    [TestCase("SettingUI")]
    [TestCase("KeyMappingUI")]
    public void BlankViewportRaycastAndOutsideCulling_PreserveTheAuthoredSurface(string screen)
    {
        using var pair = new ViewportPair(screen, "en");
        pair.DisableLocalizers();
        pair.ClearChildRaycastTargets();
        pair.SetPose(0f, 0f, 1f);
        pair.Capture(pair.Baseline);
        Vector2 local = pair.Baseline.Viewport.rect.min + new Vector2(8f, 8f);
        string[] baseline = pair.Raycast(pair.Baseline, local);
        pair.Capture(pair.Candidate);
        string[] candidate = pair.Raycast(pair.Candidate, local);
        CollectionAssert.AreEqual(baseline, candidate);
        bool hitSurface = (bool)Image.GetProperty("raycastTarget").GetValue(pair.Candidate.MaskImage);
        string viewportPath = RelativePath(pair.Candidate.Root.transform, pair.Candidate.Viewport);
        Assert.That(candidate.Contains(viewportPath), Is.EqualTo(hitSurface), "The empty viewport must keep its original hit/no-hit contract.");
        Assert.That((float)Image.GetProperty("alphaHitTestMinimumThreshold").GetValue(pair.Candidate.MaskImage), Is.Zero);
        Assert.That(pair.Baseline.Outside.GetComponent<CanvasRenderer>().cull, Is.False);
        Assert.That(pair.Candidate.Outside.GetComponent<CanvasRenderer>().cull, Is.True, "Rect clipping must actually cull fully outside graphics.");
        if (hitSurface)
        {
            ((Behaviour)pair.Candidate.MaskImage).enabled = false;
            Canvas.ForceUpdateCanvases();
            Assert.That(pair.Raycast(pair.Candidate, local, prepare: false).Contains(viewportPath), Is.False,
                "Disabling the authored mask Image is a real input regression, even though its pixels were hidden.");
            ((Behaviour)pair.Candidate.MaskImage).enabled = true;
        }
        pair.SetPose(10f, 0f, 1f);
        pair.Capture(pair.Candidate);
        Assert.That(pair.Candidate.Outside.GetComponent<CanvasRenderer>().cull, Is.False, "Returning to the stencil must clear RectMask culling.");
    }

    [TestCase("SettingUI")]
    [TestCase("KeyMappingUI")]
    public void LocalView_RotationSkewTiltDisableAndDestroy_RestoreOriginalStateWithoutRngOrHierarchyChanges(string screen)
    {
        using var pair = new ViewportPair(screen, "en");
        pair.DisableLocalizers();
        pair.SetPose(0f, 0f, 1f);
        pair.Capture(pair.Candidate);
        int hierarchy = pair.Candidate.Root.GetComponentsInChildren<Transform>(true).Length;
        Random.State random = Random.state;
        for (int i = 0; i < 200; i++) pair.Tick();
        Assert.That(Random.state, Is.EqualTo(random));
        Assert.That(pair.Candidate.Root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(hierarchy));
        pair.SetPose(-10f, 0f, 1f); pair.Tick(); pair.AssertStencilRestored();
        int rotatingChanges = (int)Hybrid.GetProperty("DiagnosticStateChangeCount").GetValue(pair.Candidate.View);
        for (int i = 0; i < 200; i++) pair.Tick();
        Assert.That((int)Hybrid.GetProperty("DiagnosticStateChangeCount").GetValue(pair.Candidate.View), Is.EqualTo(rotatingChanges),
            "A stable rotated viewport must not reapply the same native stencil state each LateUpdate.");
        pair.Candidate.Root.transform.localScale = new Vector3(-1f, 1f, 1f);
        pair.SetPose(0f, 0f, 1f); pair.Tick(); pair.AssertStencilRestored();
        pair.Candidate.Root.transform.localScale = Vector3.one;
        pair.Candidate.Background.localRotation = Quaternion.Euler(10f, 0f, 0f);
        pair.Tick(); pair.AssertStencilRestored();
        pair.Candidate.Root.transform.localScale = new Vector3(1.3f, .8f, 1f);
        pair.SetPose(10f, 0f, 1f); pair.Tick(); pair.AssertStencilRestored();
        pair.Candidate.Root.transform.localScale = Vector3.one;
        pair.SetPose(0f, 0f, 1f); pair.Tick();
        Assert.That((bool)Hybrid.GetProperty("DiagnosticUsesRectClip").GetValue(pair.Candidate.View), Is.True);
        ((Behaviour)pair.Candidate.View).enabled = false;
        pair.AssertStencilRestored();
        ((Behaviour)pair.Candidate.View).enabled = true;
        pair.Tick();
        Object.DestroyImmediate(pair.Candidate.View);
        pair.AssertStencilRestored();
    }

    static IEnumerator WaitForLocalization()
    {
        for (int frame = 0; frame < 180; frame++)
        {
            object operation = Settings.GetProperty("InitializationOperation").GetValue(null);
            if ((bool)operation.GetType().GetProperty("IsDone").GetValue(operation))
            {
                Assert.That(operation.GetType().GetProperty("Status").GetValue(operation).ToString(), Is.EqualTo("Succeeded"));
                yield break;
            }
            yield return null;
        }
        Assert.Fail("Localization initialization did not finish before clipping verification.");
    }

    static IEnumerator WaitForAppBootstrap()
    {
        for (int frame = 0; frame < 180; frame++)
        {
            object service = AppSettings.GetProperty("Instance").GetValue(null);
            if (service != null && (bool)AppSettings.GetField("initialized", Fields).GetValue(service) &&
                AppSettings.GetField("localeInitialization", Fields).GetValue(service) == null)
                yield break;
            yield return null;
        }
        Assert.Fail("The app's saved-locale bootstrap did not finish before the five-locale fixture.");
    }

    static void AssertSelectedLocale(string expected)
    {
        object selected = Settings.GetProperty("SelectedLocale").GetValue(null);
        Assert.That(selected, Is.Not.Null);
        object identifier = Locale.GetProperty("Identifier").GetValue(selected);
        Assert.That(identifier.GetType().GetProperty("Code").GetValue(identifier), Is.EqualTo(expected),
            "The real selected locale must remain the requested locale during every captured pose.");
    }

    static IEnumerator WaitForStrings(GameObject root)
    {
        yield return null;
        for (int frame = 0; frame < 180; frame++)
        {
            bool done = true;
            foreach (MonoBehaviour binding in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (binding.GetType().Name != "LocalizeStringEvent" || !binding.isActiveAndEnabled) continue;
                object reference = binding.GetType().GetProperty("StringReference").GetValue(binding);
                if ((bool)reference.GetType().GetProperty("IsEmpty").GetValue(reference)) continue;
                object handle = reference.GetType().GetProperty("CurrentLoadingOperationHandle").GetValue(reference);
                if (!(bool)handle.GetType().GetMethod("IsValid").Invoke(handle, null) ||
                    !(bool)handle.GetType().GetProperty("IsDone").GetValue(handle)) { done = false; continue; }
                Assert.That(handle.GetType().GetProperty("Status").GetValue(handle).ToString(), Is.EqualTo("Succeeded"));
            }
            if (done) { yield return null; yield break; }
            yield return null;
        }
        Assert.Fail("The authored localized string callbacks did not settle.");
    }

    static IEnumerator WaitForFonts(GameObject root)
    {
        float deadline = Time.realtimeSinceStartup + 30f;
        var reported = new Dictionary<Component, string>();
        yield return null;
        while (Time.realtimeSinceStartup < deadline)
        {
            bool done = true;
            foreach (Component binding in root.GetComponentsInChildren(FontBinding, true))
            {
                if (!((Behaviour)binding).isActiveAndEnabled) continue;
                object reference = FontBinding.GetProperty("AssetReference").GetValue(binding);
                object handle = reference.GetType().GetProperty("CurrentLoadingOperationHandle").GetValue(reference);
                bool valid = (bool)handle.GetType().GetMethod("IsValid").Invoke(handle, null);
                bool finished = valid && (bool)handle.GetType().GetProperty("IsDone").GetValue(handle);
                string status = valid ? handle.GetType().GetProperty("Status").GetValue(handle).ToString() : "invalid";
                Object result = finished ? handle.GetType().GetProperty("Result").GetValue(handle) as Object : null;
                Component text = binding.GetComponent(TmpText);
                Object actual = TmpText.GetProperty("font").GetValue(text) as Object;
                bool matches = finished && ReferenceEquals(result, actual);
                object update = FontBinding.GetProperty("OnUpdateAsset").GetValue(binding);
                int listeners = (int)update.GetType().GetMethod("GetPersistentEventCount").Invoke(update, null);
                string report = RelativePath(root.transform, binding.transform) + ";valid=" + valid + ";done=" + finished +
                    ";status=" + status + ";empty=" + reference.GetType().GetProperty("IsEmpty").GetValue(reference) +
                    ";hasHandler=" + reference.GetType().GetProperty("HasChangeHandler").GetValue(reference) +
                    ";listeners=" + listeners + ";actual=" + FontIdentity(actual) + ";result=" + FontIdentity(result) +
                    ";referenceMatch=" + matches + ";nativeAssetMatch=" + (actual == result);
                if (!reported.TryGetValue(binding, out string previous) || previous != report)
                {
                    TestContext.Out.WriteLine("HybridFontReadiness=" + report);
                    reported[binding] = report;
                }
                if (finished) Assert.That(status, Is.EqualTo("Succeeded"), report);
                if (!matches) done = false;
            }
            if (done) { yield return null; yield break; }
            yield return null;
        }
        Assert.Fail("The authored five-locale font callbacks did not settle within 30 wall-clock seconds.\n" +
            string.Join("\n", reported.Values));
    }

    static string FontIdentity(Object font) => font == null ? "null" :
        font.name + "#" + font.GetInstanceID() + "@" + AssetDatabase.GetAssetPath(font);

    static void AssertPixels(Color32[] baseline, Color32[] current, string context)
    {
        int painted = 0, different = 0, maximum = 0;
        for (int i = 0; i < baseline.Length; i++)
        {
            Color32 a = baseline[i], b = current[i];
            if (a.a > 8) painted++;
            int delta = Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b), Mathf.Abs(a.a - b.a));
            if (delta > 0) different++;
            maximum = Mathf.Max(maximum, delta);
        }
        TestContext.Out.WriteLine($"HybridRectClipPixels={context}; visible={painted}; different={different}; maximumRGBA={maximum}");
        Assert.That(painted, Is.GreaterThan(128), "An empty screen is not clipping evidence.");
        Assert.That(different, Is.Zero, context + ": native RGBA clipping mismatch (max " + maximum + ").");
    }

    static string RelativePath(Transform root, Transform child)
    {
        var parts = new List<string>();
        while (child != root)
        {
            Assert.That(child, Is.Not.Null, "A fixture reference escaped its owned subtree.");
            parts.Insert(0, child.name); child = child.parent;
        }
        return string.Join("/", parts);
    }

    sealed class ViewportData
    {
        public GameObject Root, Outside;
        public RectTransform Background, Viewport, Content;
        public Component MaskImage, Stencil, Rectangle, View;
        public Color OriginalColor;
        public bool OriginalCull;
        public Vector2 ContentPosition;
        public CanvasGroup Group;
        public Dictionary<string, Component> FontProjection;
        public string CaptureState;
        public Vector2 ScreenPoint(Vector2 local, Camera camera) => RectTransformUtility.WorldToScreenPoint(camera, Viewport.TransformPoint(local));
    }

    sealed class ViewportPair : IDisposable
    {
        readonly GameObject canvasRoot, cameraRoot, staging;
        readonly Component raycaster;
        readonly RenderTexture target;
        readonly Texture2D readback;
        public readonly Camera Camera;
        public readonly ViewportData Baseline, Candidate;
        Vector3 requestedPose;

        public ViewportPair(string screen, string code)
        {
            cameraRoot = new GameObject("Hybrid clipping camera", typeof(Camera));
            Camera = cameraRoot.GetComponent<Camera>();
            Camera.enabled = false; Camera.orthographic = true; Camera.orthographicSize = 1f;
            Camera.nearClipPlane = .1f; Camera.farClipPlane = 100f;
            Camera.clearFlags = CameraClearFlags.SolidColor; Camera.backgroundColor = Color.clear;
            Camera.allowHDR = false; Camera.allowMSAA = false; Camera.cullingMask = 1 << 31;
            Camera.transform.position = new Vector3(0f, 0f, -10f);
            target = new RenderTexture(768, 432, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            target.Create(); Camera.targetTexture = target;
            readback = new Texture2D(768, 432, TextureFormat.RGBA32, false, true);
            canvasRoot = new GameObject("Hybrid clipping canvas", typeof(RectTransform), typeof(Canvas));
            canvasRoot.layer = 31;
            Canvas canvas = canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = Camera; canvas.planeDistance = 1f; canvas.scaleFactor = .4f;
            Type raycasterType = Type.GetType("UnityEngine.UI.GraphicRaycaster, UnityEngine.UI", true);
            raycaster = canvasRoot.AddComponent(raycasterType);
            staging = new GameObject("Inactive hybrid clipping staging"); staging.SetActive(false);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Transform authored = prefab.transform.Find("PopupCanvas/" + screen);
            Assert.That(authored, Is.Not.Null);
            Baseline = Create(authored, canvas, screen, code, false);
            Candidate = Create(authored, canvas, screen, code, true);
            Baseline.Root.SetActive(true); Candidate.Root.SetActive(true);
            Canvas.ForceUpdateCanvases();
        }

        ViewportData Create(Transform authored, Canvas canvas, string screen, string code, bool candidate)
        {
            var data = new ViewportData { Root = Object.Instantiate(authored.gameObject, staging.transform) };
            data.Root.name = screen;
            data.Background = (RectTransform)data.Root.transform.Find("Background");
            data.Viewport = (RectTransform)data.Background.Find("ContentsMask");
            data.Content = (RectTransform)data.Viewport.Find("Contents");
            data.MaskImage = data.Viewport.GetComponent(Image);
            data.Stencil = data.Viewport.GetComponent(Mask);
            Assert.That(data.MaskImage, Is.Not.Null); Assert.That(data.Stencil, Is.Not.Null);
            Assert.That((bool)Mask.GetProperty("showMaskGraphic").GetValue(data.Stencil), Is.False);
            Assert.That(Image.GetProperty("sprite").GetValue(data.MaskImage), Is.Null);
            Assert.That((float)Image.GetProperty("alphaHitTestMinimumThreshold").GetValue(data.MaskImage), Is.Zero);
            data.OriginalColor = (Color)Image.GetProperty("color").GetValue(data.MaskImage);
            data.OriginalCull = data.MaskImage.GetComponent<CanvasRenderer>().cullTransparentMesh;
            foreach (MonoBehaviour behaviour in data.Root.GetComponentsInChildren<MonoBehaviour>(true))
                if (!UiBehaviour.IsInstanceOfType(behaviour) && behaviour.GetType() != FontBinding &&
                    behaviour.GetType().Name != "LocalizeStringEvent" && behaviour.GetType() != Hybrid && behaviour.GetType() != RenderLayout)
                    Object.DestroyImmediate(behaviour);
            // UGUI listeners can target screen controllers outside an isolated fixture.
            // They are irrelevant to hit testing, and must not invoke an asset reference.
            foreach (Component control in data.Root.GetComponentsInChildren(UiBehaviour, true))
            {
                var serialized = new SerializedObject(control); var property = serialized.GetIterator();
                while (property.Next(true))
                    if (property.isArray && property.propertyPath.EndsWith(".m_PersistentCalls.m_Calls", StringComparison.Ordinal)) property.arraySize = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (Component scroll in data.Root.GetComponentsInChildren(Scroll, true))
            {
                foreach (string name in new[] { "content", "viewport", "horizontalScrollbar", "verticalScrollbar" })
                {
                    var reference = Scroll.GetProperty(name).GetValue(scroll) as Component;
                    if (reference != null) Assert.That(reference.transform.IsChildOf(data.Root.transform), Is.True,
                        "ScrollRect references must be remapped before activation: " + name);
                }
            }
            foreach (Component binding in data.Root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (binding.GetType() != FontBinding && binding.GetType().Name != "LocalizeStringEvent") continue;
                object update = binding.GetType().GetProperty(binding.GetType() == FontBinding ? "OnUpdateAsset" : "OnUpdateString").GetValue(binding);
                int count = (int)update.GetType().GetMethod("GetPersistentEventCount").Invoke(update, null);
                for (int i = 0; i < count; i++)
                {
                    var reference = update.GetType().GetMethod("GetPersistentTarget").Invoke(update, new object[] { i }) as Component;
                    Assert.That(reference == null || reference.transform.IsChildOf(data.Root.transform), Is.True, "Localization callbacks must target the cloned view.");
                }
            }
            data.View = data.Viewport.GetComponent(Hybrid);
            data.Rectangle = data.Viewport.GetComponent(Rectangle);
            if (data.View != null)
            {
                foreach (string name in new[] { "originalMask", "rectMask", "maskImage", "viewport" })
                {
                    var reference = (Component)Hybrid.GetField(name, Fields).GetValue(data.View);
                    Assert.That(reference.transform.IsChildOf(data.Root.transform), Is.True, "Hybrid references must be owned before activation.");
                }
            }
            if (!candidate)
            {
                if (data.View != null) Object.DestroyImmediate(data.View);
                if (data.Rectangle != null) Object.DestroyImmediate(data.Rectangle);
                data.View = null; data.Rectangle = null;
            }
            else
            {
                data.Rectangle ??= data.Viewport.gameObject.AddComponent(Rectangle);
                ((Behaviour)data.Rectangle).enabled = false;
                data.View ??= data.Viewport.gameObject.AddComponent(Hybrid);
                Hybrid.GetMethod("ConfigureForAuthoring").Invoke(data.View,
                    new object[] { data.Stencil, data.Rectangle, data.MaskImage, data.Viewport, canvas });
                Assert.That(Hybrid.GetField("rootCanvas", Fields).GetValue(data.View), Is.SameAs(canvas));
            }
            if (screen == "KeyMappingUI") AddAuthoredRows(data.Content, code);
            if (RenderLayout != null)
            {
                Component owner = data.Root.GetComponent(RenderLayout);
                foreach (Component layout in data.Root.GetComponentsInChildren(RenderLayout, true))
                {
                    var serialized = new SerializedObject(layout); var property = serialized.GetIterator();
                    while (property.Next(true))
                    {
                        if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                        Object reference = property.objectReferenceValue;
                        Transform owned = reference is Component component ? component.transform : reference is GameObject go ? go.transform : null;
                        if (owned != null) Assert.That(owned.IsChildOf(data.Root.transform), Is.True, "Render-layer anchors must be remapped before activation.");
                    }
                    if (owner != null && layout != owner) RenderLayout.GetMethod("BindTo").Invoke(layout, new object[] { owner });
                }
            }
            // Include the newly cloned authored key rows in the callback ownership guard.
            foreach (Component binding in data.Root.GetComponentsInChildren(FontBinding, true))
            {
                object update = FontBinding.GetProperty("OnUpdateAsset").GetValue(binding);
                int count = (int)update.GetType().GetMethod("GetPersistentEventCount").Invoke(update, null);
                for (int i = 0; i < count; i++)
                {
                    var reference = update.GetType().GetMethod("GetPersistentTarget").Invoke(update, new object[] { i }) as Component;
                    Assert.That(reference == null || reference.transform.IsChildOf(data.Root.transform), Is.True, "Every font callback must remain fixture-owned before activation.");
                }
            }
            data.Outside = Canary(data.Viewport, "Outside clip canary", new Color(.9f, .1f, .3f, 1f));
            Canary(data.Viewport, "Inside clip canary", new Color(.1f, .9f, .3f, 1f));
            data.Group = data.Background.GetComponent<CanvasGroup>() ?? data.Background.gameObject.AddComponent<CanvasGroup>();
            data.FontProjection = data.Root.GetComponentsInChildren(TmpText, true).ToDictionary(text => RelativePath(data.Root.transform, text.transform));
            data.Root.transform.SetParent(canvas.transform, false);
            foreach (Transform child in data.Root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
            data.ContentPosition = data.Content.anchoredPosition;
            return data;
        }

        static void AddAuthoredRows(RectTransform content, string code)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/KeyMapRow.prefab");
            Assert.That(prefab, Is.Not.Null);
            for (int i = 0; i < 10; i++)
            {
                GameObject row = Object.Instantiate(prefab, content, false); row.name = "Authored row " + i;
                foreach (MonoBehaviour behaviour in row.GetComponentsInChildren<MonoBehaviour>(true))
                    if (!UiBehaviour.IsInstanceOfType(behaviour) && behaviour.GetType() != FontBinding && behaviour.GetType().Name != "LocalizeStringEvent" && behaviour.GetType() != RenderLayout)
                        Object.DestroyImmediate(behaviour);
                Component text = row.GetComponentsInChildren(TmpText, true).First();
                string sample = code switch { "ko" => "설정", "ja" => "設定", "zh-Hans" => "设置", "zh-Hant" => "設定", _ => "Settings" };
                TmpText.GetProperty("text").SetValue(text, sample + " " + i);
            }
        }

        static GameObject Canary(RectTransform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), Image);
            go.transform.SetParent(parent, false); ((RectTransform)go.transform).sizeDelta = new Vector2(24f, 24f);
            Image.GetProperty("color").SetValue(go.GetComponent(Image), color);
            return go;
        }

        public void SetPose(float rotation, float scrollOffset, float alpha)
        {
            requestedPose = new Vector3(rotation, scrollOffset, alpha);
            foreach (ViewportData data in new[] { Baseline, Candidate })
            {
                data.Background.localRotation = Quaternion.Euler(0f, 0f, rotation);
                data.Content.anchoredPosition = data.ContentPosition + new Vector2(0f, scrollOffset);
                data.Group.alpha = alpha;
            }
        }

        public void Tick()
        {
            if (Candidate.View != null && ((Behaviour)Candidate.View).isActiveAndEnabled)
                Hybrid.GetMethod("LateUpdate", Fields).Invoke(Candidate.View, null);
        }

        public IEnumerator Settle(ViewportData data)
        {
            Prepare(data);
            yield return WaitForFonts(data.Root);
            yield return WaitForStrings(data.Root);
            ApplyRequestedPose(data);
            Tick(); Canvas.ForceUpdateCanvases();
        }

        void Prepare(ViewportData data)
        {
            Baseline.Root.SetActive(false); Candidate.Root.SetActive(false);
            data.Root.SetActive(true);
            ApplyRequestedPose(data);
            Tick(); Canvas.ForceUpdateCanvases();
        }

        void ApplyRequestedPose(ViewportData data)
        {
            data.Background.localRotation = Quaternion.Euler(0f, 0f, requestedPose.x);
            data.Content.anchoredPosition = data.ContentPosition + new Vector2(0f, requestedPose.y);
            data.Group.alpha = requestedPose.z;
        }

        public Color32[] Capture(ViewportData data, bool prepare = true)
        {
            Assert.That((Camera.cullingMask & (1 << canvasRoot.layer)) != 0, Is.True,
                "The fixture camera must include the root Canvas layer, not only its child Graphic layers.");
            // Both captures must pass through the same OnEnable/layout path.
            // Otherwise the already-active ScrollRect and freshly-enabled one
            // update their scrollbars and localized text at different stages.
            if (prepare) Prepare(data);
            data.Outside.GetComponent<RectTransform>().anchoredPosition = new Vector2(data.Viewport.rect.width * .5f + 32f, 0f);
            Tick();
            foreach (Component text in data.Root.GetComponentsInChildren(TmpText, true))
                if (((Behaviour)text).isActiveAndEnabled)
                    TmpText.GetMethod("ForceMeshUpdate").Invoke(text, new object[] { false, false });
            Canvas.ForceUpdateCanvases();
            if (GraphicsSettings.currentRenderPipeline != null)
            {
                var request = new RenderPipeline.StandardRequest { destination = target };
                Assert.That(RenderPipeline.SupportsRenderRequest(Camera, request), Is.True);
                RenderPipeline.SubmitRenderRequest(Camera, request);
            }
            else Camera.Render();
            data.CaptureState = "rotation=" + data.Background.localEulerAngles + ",viewport=" + data.Viewport.rect +
                ",content=" + data.Content.anchoredPosition + ",alpha=" + data.Group.alpha +
                ",stencil=" + ((Behaviour)data.Stencil).enabled + ",image=" + Image.GetProperty("color").GetValue(data.MaskImage) +
                ",imageCull=" + data.MaskImage.GetComponent<CanvasRenderer>().cull +
                ",rectangle=" + (data.Rectangle != null && ((Behaviour)data.Rectangle).enabled);
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = target; readback.ReadPixels(new Rect(0, 0, 768, 432), 0, 0); readback.Apply();
                return readback.GetPixels32();
            }
            finally { RenderTexture.active = previous; }
        }

        public string[] Raycast(ViewportData data, Vector2 local, bool prepare = true)
        {
            if (prepare)
            {
                Baseline.Root.SetActive(false); Candidate.Root.SetActive(false);
                data.Root.SetActive(true);
                data.Background.localRotation = Quaternion.Euler(0f, 0f, requestedPose.x);
                data.Content.anchoredPosition = data.ContentPosition + new Vector2(0f, requestedPose.y);
                data.Group.alpha = requestedPose.z;
            }
            Tick(); Canvas.ForceUpdateCanvases();
            // GraphicRaycaster rejects depth == -1. After an enable/disable cycle,
            // submitted render depth must be rebuilt for both fixtures equally.
            if (GraphicsSettings.currentRenderPipeline != null)
            {
                var request = new RenderPipeline.StandardRequest { destination = target };
                Assert.That(RenderPipeline.SupportsRenderRequest(Camera, request), Is.True);
                RenderPipeline.SubmitRenderRequest(Camera, request);
            }
            else Camera.Render();
            // An inactive ScrollRect can have restored layout-driver values. Compute
            // screen coordinates only after the selected fixture has been rebuilt.
            Vector2 point = data.ScreenPoint(local, Camera);
            Type pointer = Type.GetType("UnityEngine.EventSystems.PointerEventData, UnityEngine.UI", true);
            Type eventSystem = Type.GetType("UnityEngine.EventSystems.EventSystem, UnityEngine.UI", true);
            object eventData = pointer.GetConstructor(new[] { eventSystem }).Invoke(new object[] { null });
            pointer.GetProperty("position").SetValue(eventData, point);
            Type result = Type.GetType("UnityEngine.EventSystems.RaycastResult, UnityEngine.UI", true);
            Type list = typeof(List<>).MakeGenericType(result); var results = (IList)Activator.CreateInstance(list);
            raycaster.GetType().GetMethod("Raycast", new[] { pointer, list }).Invoke(raycaster, new object[] { eventData, results });
            return results.Cast<object>().Select(hit => RelativePath(data.Root.transform,
                ((GameObject)result.GetProperty("gameObject").GetValue(hit)).transform)).ToArray();
        }

        public void AssertFontProjection()
        {
            foreach (var projection in Candidate.FontProjection)
            {
                Component text = projection.Value;
                Component original = Baseline.FontProjection[projection.Key];
                Assert.That(TmpText.GetProperty("font").GetValue(text), Is.SameAs(TmpText.GetProperty("font").GetValue(original)));
                Assert.That(TmpText.GetProperty("text").GetValue(text), Is.EqualTo(TmpText.GetProperty("text").GetValue(original)));
            }
        }

        public void DisableLocalizers()
        {
            foreach (ViewportData data in new[] { Baseline, Candidate })
                foreach (Component binding in data.Root.GetComponentsInChildren<MonoBehaviour>(true))
                    if (binding.GetType() == FontBinding || binding.GetType().Name == "LocalizeStringEvent") ((Behaviour)binding).enabled = false;
        }

        public void ClearChildRaycastTargets()
        {
            foreach (ViewportData data in new[] { Baseline, Candidate })
                foreach (Component graphic in data.Viewport.GetComponentsInChildren(Graphic, true))
                    if (graphic != data.MaskImage) Graphic.GetProperty("raycastTarget").SetValue(graphic, false);
        }

        public void AssertStencilRestored()
        {
            Assert.That(((Behaviour)Candidate.Stencil).enabled, Is.True);
            Assert.That(((Behaviour)Candidate.Rectangle).enabled, Is.False);
            Assert.That((Color)Image.GetProperty("color").GetValue(Candidate.MaskImage), Is.EqualTo(Candidate.OriginalColor));
            Assert.That(Candidate.MaskImage.GetComponent<CanvasRenderer>().cullTransparentMesh, Is.EqualTo(Candidate.OriginalCull));
        }

        public void Dispose()
        {
            Object.DestroyImmediate(canvasRoot); Object.DestroyImmediate(staging); Object.DestroyImmediate(cameraRoot);
            target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(readback);
            Canvas.ForceUpdateCanvases();
        }
    }
}
#endif
