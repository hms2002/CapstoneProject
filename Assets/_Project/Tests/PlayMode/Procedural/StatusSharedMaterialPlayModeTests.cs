#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class StatusSharedMaterialPlayModeTests
{
    delegate void BindEntry(in StatusHudEntry entry);
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    readonly List<GameObject> objects = new();
    readonly List<Object> resources = new();
    object originalLocale;
    bool restoreLocale;
    static readonly Type View = Type.GetType("StatusHudEntryView, UI", true);
    static readonly Type FontBinding = Type.GetType("LocalizeTmpFontEvent, UI", true);
    static readonly Type FontAsset = Type.GetType("TMPro.TMP_FontAsset, Unity.TextMeshPro", true);
    static readonly Type Settings = Type.GetType("UnityEngine.Localization.Settings.LocalizationSettings, Unity.Localization", true);
    static readonly Type Locale = Type.GetType("UnityEngine.Localization.Locale, Unity.Localization", true);
    static readonly Type MaterialManager = Type.GetType("TMPro.TMP_MaterialManager, Unity.TextMeshPro", true);
    static readonly Type Graphic = Type.GetType("UnityEngine.UI.Graphic, UnityEngine.UI", true);
    const string KoreanFont = "Assets/_Project/Art/Font/Galmuri9 SDF BlackOutline.asset";
    const string JapaneseFont = "Assets/_Project/Data/Localization/Fonts/Galmuri/Galmuri9 Japanese SDF.asset";
    const string SimplifiedFont = "Assets/_Project/Data/Localization/Fonts/FusionPixel/fusion-pixel-12px-proportional-zh_hans SDF.asset";
    const string TraditionalFont = "Assets/_Project/Data/Localization/Fonts/FusionPixel/fusion-pixel-12px-proportional-zh_hant SDF.asset";
    static StatusHudEntry Entry(bool progress) => new("shared", "shared", "Name", "Story", "Effect", null,
        5, true, 3, 10, true, default, 0, false, true, progress ? "42%" : null);

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        foreach (GameObject go in objects) if (go != null) Object.DestroyImmediate(go);
        objects.Clear();
        // Exercise the actual render callback cleanup before destroying its source styles.
        Canvas.ForceUpdateCanvases();
        foreach (Object resource in resources)
        {
            if (resource == null) continue;
            if (resource is RenderTexture target) target.Release();
            Object.DestroyImmediate(resource);
        }
        resources.Clear();
        if (restoreLocale)
        {
            // Locale changes restart the package's preload operation. Finish the
            // current lease and its restoration before another test can change it.
            yield return WaitForLocalization();
            Settings.GetProperty("SelectedLocale").SetValue(null, originalLocale);
            yield return WaitForLocalization();
        }
        restoreLocale = false;
        originalLocale = null;
    }

    [TestCase("Assets/_Project/Art/Font/Galmuri9 SDF BlackOutline.asset")]
    [TestCase("Assets/_Project/Art/Font/Galmuri9 SDF.asset")]
    [TestCase("Assets/_Project/Data/Localization/Fonts/Galmuri/Galmuri9 Japanese SDF.asset")]
    [TestCase("Assets/_Project/Data/Localization/Fonts/FusionPixel/fusion-pixel-12px-proportional-zh_hans SDF.asset")]
    [TestCase("Assets/_Project/Data/Localization/Fonts/FusionPixel/fusion-pixel-12px-proportional-zh_hant SDF.asset")]
    public void IdenticalProgressStyleAndAtlas_ShareMaterialAndRestoreNormal(string fontPath)
    {
        var first = Create(); var second = Create();
        Object font = AssetDatabase.LoadAssetAtPath(fontPath, FontAsset);
        Assert.That(font, Is.Not.Null);
        ApplyFont(first.binding, font); ApplyFont(second.binding, font);
        Material normal = Material(first.text);
        first.bind(Entry(true)); second.bind(Entry(true));
        Assert.That(Material(first.text), Is.SameAs(Material(second.text)));
        Assert.That(Material(first.text).GetFloat("_OutlineWidth"), Is.EqualTo(0.2f));
        Assert.That(Material(first.text).GetColor("_OutlineColor"), Is.EqualTo(Color.black));
        Texture atlas = Material(font).GetTexture("_MainTex");
        Assert.That(Material(first.text).GetTexture("_MainTex"), Is.SameAs(atlas));
        Assert.That((string)first.text.GetType().GetProperty("text").GetValue(first.text), Is.EqualTo("42%"));
        first.bind(Entry(false)); second.bind(Entry(false));
        Assert.That(Material(first.text), Is.SameAs(normal));
        Assert.That(Material(second.text), Is.SameAs(normal));
    }

    [Test]
    public void LocaleChangesDuringProgress_KeepStyleAndUseTheNewAtlas()
    {
        var fixture = Create();
        fixture.bind(Entry(true));
        foreach (string path in new[] {
            "Assets/_Project/Art/Font/Galmuri9 SDF.asset",
            "Assets/_Project/Data/Localization/Fonts/Galmuri/Galmuri9 Japanese SDF.asset",
            "Assets/_Project/Data/Localization/Fonts/FusionPixel/fusion-pixel-12px-proportional-zh_hans SDF.asset" })
        {
            Object font = AssetDatabase.LoadAssetAtPath(path, FontAsset);
            ApplyFont(fixture.binding, font);
            Assert.That(Material(fixture.text).GetFloat("_OutlineWidth"), Is.EqualTo(0.2f));
            Assert.That(Material(fixture.text).GetTexture("_MainTex"), Is.SameAs(Material(font).GetTexture("_MainTex")));
        }
        fixture.bind(Entry(false));
        Assert.That((string)fixture.text.GetType().GetProperty("text").GetValue(fixture.text), Is.EqualTo("5"));
    }

    [Test]
    public void DisableReuseAndRepeatedProgressSwitch_DoNotLeavePerEntryMaterials()
    {
        var first = Create(); var second = Create();
        for (int i = 0; i < 20; i++)
        {
            first.bind(Entry(true)); second.bind(Entry(true));
            Assert.That(Material(first.text), Is.SameAs(Material(second.text)));
            first.go.SetActive(false); first.go.SetActive(true);
            first.bind(Entry(false)); second.bind(Entry(false));
            Assert.That(Material(first.text), Is.SameAs(Material(second.text)));
        }
    }

    [TestCase(KoreanFont, false)]
    [TestCase("Assets/_Project/Art/Font/Galmuri9 SDF.asset", false)]
    [TestCase(JapaneseFont, false)]
    [TestCase(SimplifiedFont, false)]
    [TestCase(TraditionalFont, false)]
    [TestCase(KoreanFont, true)]
    [TestCase(TraditionalFont, true)]
    public void AuthoredProgressPixels_MatchFrozenOutlineSetterPath(string fontPath, bool masked)
    {
        var render = new AuthoredEntryRenderFixture(this, masked);
        var baseline = render.CreateEntry();
        var optimized = render.CreateEntry();
        DisableFontEvents(baseline.go); DisableFontEvents(optimized.go);
        Object font = AssetDatabase.LoadAssetAtPath(fontPath, FontAsset);
        Assert.That(font, Is.Not.Null);
        ApplyFont(baseline.binding, font); ApplyFont(optimized.binding, font);

        // Frozen SetProgressLayout used these TMP setters after selecting the normal
        // localized font. The fallback branch retains that exact production path.
        View.GetField("progressTextMaterial", Fields).SetValue(baseline.go.GetComponent(View), null);
        baseline.bind(Entry(true)); optimized.bind(Entry(true));
        Assert.That(Material(baseline.text).GetFloat("_OutlineWidth"), Is.EqualTo(0.2f));
        Assert.That(Material(baseline.text).GetColor("_OutlineColor"), Is.EqualTo(Color.black));
        Color32[] original = render.Capture(baseline.go, baseline.text);
        Color32[] shared = render.Capture(optimized.go, optimized.text);
        AssertPixelsEqual(original, shared, fontPath + (masked ? " masked" : " unmasked"));
        AssertRendererAtlas(optimized.text, font);
    }

    [UnityTest]
    public IEnumerator SelectedLocaleAsync_DuringProgressAndNormalRestore_UsesTheRenderedAtlas()
    {
        yield return BeginLocaleLease();
        var render = new AuthoredEntryRenderFixture(this, false);
        var fixture = render.CreateEntry();
        var peer = Create();
        Material normalStyle = (Material)FontBinding.GetProperty("AuthoredMaterial").GetValue(fixture.binding);
        SetLocale("ko");
        yield return WaitForFont(fixture.text, fixture.binding, ExpectedFont("ko"));
        yield return WaitForFont(peer.text, peer.binding, ExpectedFont("ko"));
        fixture.bind(Entry(false)); peer.bind(Entry(false));
        // Reproduce the otherwise invisible old-atlas CanvasRenderer override trap.
        fixture.text.GetComponent<CanvasRenderer>().SetTexture(Material(ExpectedFont("zh-Hant")).mainTexture);
        fixture.bind(Entry(true)); peer.bind(Entry(true));
        Assert.That(Material(fixture.text), Is.SameAs(Material(peer.text)));
        render.AssertTextureOverride(fixture.go, fixture.text, ExpectedFont("ko"), "progress entry");

        foreach (string code in new[] { "ja", "zh-Hans", "zh-Hant", "en", "ko" })
        {
            SetLocale(code);
            yield return WaitForFont(fixture.text, fixture.binding, ExpectedFont(code));
            yield return WaitForFont(peer.text, peer.binding, ExpectedFont(code));
            Assert.That(Material(fixture.text), Is.SameAs(Material(peer.text)), code);
            Assert.That(Material(fixture.text).GetFloat("_OutlineWidth"), Is.EqualTo(0.2f), code);
            Assert.That(Material(fixture.text).GetColor("_OutlineColor"), Is.EqualTo(Color.black), code);
            Assert.That(Text(fixture.text), Is.EqualTo("42%"), code);
            render.AssertTextureOverride(fixture.go, fixture.text, ExpectedFont(code), code + " progress");
        }

        // Restore normal while the new locale's package operation is in flight.
        SetLocale("ja");
        fixture.bind(Entry(false)); peer.bind(Entry(false));
        yield return WaitForFont(fixture.text, fixture.binding, ExpectedFont("ja"));
        yield return WaitForFont(peer.text, peer.binding, ExpectedFont("ja"));
        Assert.That(Material(fixture.text), Is.SameAs(Material(peer.text)));
        Assert.That(FontBinding.GetProperty("AuthoredMaterial").GetValue(fixture.binding), Is.SameAs(normalStyle));
        Assert.That(Material(fixture.text).GetFloat("_OutlineWidth"), Is.EqualTo(normalStyle.GetFloat("_OutlineWidth")));
        Assert.That(Material(fixture.text).GetColor("_OutlineColor"), Is.EqualTo(normalStyle.GetColor("_OutlineColor")));
        Assert.That(Text(fixture.text), Is.EqualTo("5"));
        render.AssertTextureOverride(fixture.go, fixture.text, ExpectedFont("ja"), "normal restored during locale load");
    }

    [UnityTest]
    public IEnumerator InactiveProgressPool_LocaleChangesReenableAndNormalRestore_UseTheLatestPackageFont()
    {
        yield return BeginLocaleLease();
        var render = new AuthoredEntryRenderFixture(this, false);
        var pooled = render.CreateEntry();
        var peer = Create();
        SetLocale("ko");
        yield return WaitForFont(pooled.text, pooled.binding, ExpectedFont("ko"));
        yield return WaitForFont(peer.text, peer.binding, ExpectedFont("ko"));
        pooled.bind(Entry(true)); peer.bind(Entry(true));
        foreach (string code in new[] { "zh-Hans", "zh-Hant", "ja" })
        {
            SetLocale("en"); // A pending refresh can be cancelled by pool deactivation.
            pooled.go.SetActive(false);
            object assetReference = FontBinding.GetProperty("AssetReference").GetValue(pooled.binding);
            Assert.That((bool)assetReference.GetType().GetProperty("HasChangeHandler").GetValue(assetReference), Is.False);
            yield return WaitForFont(peer.text, peer.binding, ExpectedFont("en"));
            SetLocale(code);
            yield return WaitForFont(peer.text, peer.binding, ExpectedFont(code));
            pooled.bind(Entry(true)); // Real Bind activates the retained entry before async delivery.
            yield return WaitForFont(pooled.text, pooled.binding, ExpectedFont(code));
            Assert.That((bool)assetReference.GetType().GetProperty("HasChangeHandler").GetValue(assetReference), Is.True);
            Assert.That(Material(pooled.text), Is.SameAs(Material(peer.text)), code);
            Assert.That(Material(pooled.text).GetFloat("_OutlineWidth"), Is.EqualTo(0.2f));
            render.AssertTextureOverride(pooled.go, pooled.text, ExpectedFont(code), code + " pooled progress");
            pooled.bind(Entry(false)); peer.bind(Entry(false));
            Assert.That(Material(pooled.text), Is.SameAs(Material(peer.text)), code + " normal");
            Assert.That(Text(pooled.text), Is.EqualTo("5"));
            render.AssertTextureOverride(pooled.go, pooled.text, ExpectedFont(code), code + " pooled normal");
            pooled.bind(Entry(true)); peer.bind(Entry(true));
        }
    }

    [Test]
    public void OwnedSharedStyleReferences_PoolingRetainsAndDestroyReleasesThroughOnPreRender()
    {
        var first = Create(); var second = Create();
        DisableFontEvents(first.go); DisableFontEvents(second.go);
        Object font = ExpectedFont("ko");
        ApplyFont(first.binding, font); ApplyFont(second.binding, font);
        var normal = new Material((Material)FontBinding.GetProperty("AuthoredMaterial").GetValue(first.binding));
        var progress = new Material((Material)View.GetField("progressTextMaterial", Fields).GetValue(first.go.GetComponent(View)));
        resources.Add(normal); resources.Add(progress);
        // Unique fixture-owned source keys avoid assuming the shared global cache is empty.
        ApplyStyle(first.binding, normal); ApplyStyle(second.binding, normal);
        View.GetField("progressTextMaterial", Fields).SetValue(first.go.GetComponent(View), progress);
        View.GetField("progressTextMaterial", Fields).SetValue(second.go.GetComponent(View), progress);
        Canvas.ForceUpdateCanvases();
        AssertOwnedReferenceCount(normal, 2);
        for (int i = 0; i < 20; i++)
        {
            first.bind(Entry(true)); second.bind(Entry(true));
            Canvas.ForceUpdateCanvases();
            AssertOwnedReferenceCount(progress, 2);
            AssertOwnedReferenceCount(normal, 0);
            first.go.SetActive(false);
            Canvas.ForceUpdateCanvases();
            AssertOwnedReferenceCount(progress, 2, "An inactive pooled entry retains its style lease.");
            first.bind(Entry(true));
            Canvas.ForceUpdateCanvases();
            AssertOwnedReferenceCount(progress, 2, "Reactivation must not add another lease for the same material.");
            first.bind(Entry(false)); second.bind(Entry(false));
            Canvas.ForceUpdateCanvases();
            AssertOwnedReferenceCount(normal, 2);
            AssertOwnedReferenceCount(progress, 0);
        }
        first.bind(Entry(true)); second.bind(Entry(true));
        Canvas.ForceUpdateCanvases();
        Material retained = Material(first.text);
        object record = FindOwnedRecords(progress)[0];
        object key = record.GetType().GetField("fallbackID", Fields).GetValue(record);
        IDictionary lookup = (IDictionary)MaterialManager.GetField("m_fallbackMaterialLookup", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        Assert.That(ContainsValue(lookup, key), Is.True, "The material lookup and reference table must both own the active material.");
        Object.DestroyImmediate(first.go);
        Canvas.ForceUpdateCanvases();
        AssertOwnedReferenceCount(progress, 1);
        Assert.That(retained != null, Is.True, "One live entry still owns the shared style.");
        Object.DestroyImmediate(second.go);
        Canvas.ForceUpdateCanvases();
        AssertOwnedReferenceCount(progress, 0);
        AssertOwnedReferenceCount(normal, 0);
        Assert.That(ContainsValue(lookup, key), Is.False, "OnPreRender must also remove the released material lookup.");
        Assert.That(retained == null, Is.True, "The native fallback material is destroyed after its final lease is released.");
    }

    IEnumerator BeginLocaleLease()
    {
        yield return WaitForLocalization();
        originalLocale = Settings.GetProperty("SelectedLocale").GetValue(null);
        restoreLocale = true;
    }

    static IEnumerator WaitForLocalization()
    {
        for (int frame = 0; frame < 180; frame++)
        {
            // Read the current operation: a locale change replaces the previous handle.
            object operation = Settings.GetProperty("InitializationOperation").GetValue(null);
            if ((bool)operation.GetType().GetProperty("IsDone").GetValue(operation))
            {
                Assert.That(operation.GetType().GetProperty("Status").GetValue(operation).ToString(), Is.EqualTo("Succeeded"));
                yield break;
            }
            yield return null;
        }
        Assert.Fail("Localization initialization timed out.");
    }

    static void SetLocale(string code)
    {
        Object locale = AssetDatabase.LoadAssetAtPath("Assets/_Project/Data/Localization/Locale_" + code + ".asset", Locale);
        Assert.That(locale, Is.Not.Null, code);
        Settings.GetProperty("SelectedLocale").SetValue(null, locale);
    }

    static Object ExpectedFont(string code)
    {
        string path = code switch { "ja" => JapaneseFont, "zh-Hans" => SimplifiedFont, "zh-Hant" => TraditionalFont, _ => KoreanFont };
        Object font = AssetDatabase.LoadAssetAtPath(path, FontAsset);
        Assert.That(font, Is.Not.Null, path);
        return font;
    }

    static IEnumerator WaitForFont(Component text, Component binding, Object expected)
    {
        // Two locales may share one font, so matching the font alone does not
        // prove that the new locale's global preload operation has completed.
        yield return WaitForLocalization();
        object assetReference = FontBinding.GetProperty("AssetReference").GetValue(binding);
        // Always allow package async completion and the adapter's layout LateUpdate.
        yield return null;
        for (int frame = 0; frame < 180; frame++)
        {
            object operation = assetReference.GetType().GetProperty("CurrentLoadingOperationHandle").GetValue(assetReference);
            bool valid = (bool)operation.GetType().GetMethod("IsValid").Invoke(operation, null);
            if (valid && (bool)operation.GetType().GetProperty("IsDone").GetValue(operation))
            {
                Assert.That(operation.GetType().GetProperty("Status").GetValue(operation).ToString(), Is.EqualTo("Succeeded"));
                if (ReferenceEquals(operation.GetType().GetProperty("Result").GetValue(operation), expected) &&
                    ReferenceEquals(text.GetType().GetProperty("font").GetValue(text), expected))
                {
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    yield break;
                }
            }
            yield return null;
        }
        Assert.Fail("The authored Localization Asset Table callback did not deliver " + expected.name + ".");
    }

    static void DisableFontEvents(GameObject go)
    {
        foreach (Component binding in go.GetComponentsInChildren(FontBinding, true)) ((Behaviour)binding).enabled = false;
    }

    static void ApplyStyle(Component binding, Material style) =>
        FontBinding.GetMethod("ApplyAuthoredMaterialStyle").Invoke(binding, new object[] { style });
    static string Text(Component text) => (string)text.GetType().GetProperty("text").GetValue(text);

    static List<object> FindOwnedRecords(Material style)
    {
        IDictionary table = (IDictionary)MaterialManager.GetField("m_fallbackMaterials", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        var matches = new List<object>();
        foreach (object record in table.Values)
            if (ReferenceEquals(record.GetType().GetField("sourceMaterial", Fields).GetValue(record), style)) matches.Add(record);
        return matches;
    }

    static void AssertOwnedReferenceCount(Material style, int expected, string message = null)
    {
        List<object> records = FindOwnedRecords(style);
        Assert.That(records.Count, Is.EqualTo(expected == 0 ? 0 : 1), message);
        if (records.Count > 0)
            Assert.That((int)records[0].GetType().GetField("count", Fields).GetValue(records[0]), Is.EqualTo(expected), message);
    }

    static bool ContainsValue(IDictionary dictionary, object expected)
    {
        foreach (object value in dictionary.Values) if (Equals(value, expected)) return true;
        return false;
    }

    static void AssertRendererAtlas(Component text, Object font)
    {
        Texture atlas = Material(font).mainTexture;
        Assert.That(Material(text).mainTexture, Is.SameAs(atlas));
        Material rendered = text.GetComponent<CanvasRenderer>().GetMaterial(0);
        Assert.That(rendered, Is.Not.Null);
        Assert.That(rendered.mainTexture, Is.SameAs(atlas), "CanvasRenderer material must match the current localized atlas.");
    }

    static void AssertPixelsEqual(Color32[] original, Color32[] current, string context)
    {
        Assert.That(current.Length, Is.EqualTo(original.Length));
        int painted = 0, different = 0, maxDifference = 0;
        for (int i = 0; i < original.Length; i++)
        {
            if (original[i].a > 8) painted++;
            Color32 a = original[i], b = current[i];
            int delta = Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b), Mathf.Abs(a.a - b.a));
            if (delta != 0) different++;
            maxDifference = Mathf.Max(maxDifference, delta);
        }
        Assert.That(painted, Is.GreaterThan(5), context + ": the actual text must be visible; empty captures are not equivalent evidence.");
        Assert.That(different, Is.Zero, context + ": RGBA pixel mismatch; maximum channel delta " + maxDifference + ".");
    }

    sealed class AuthoredEntryRenderFixture
    {
        readonly StatusSharedMaterialPlayModeTests owner;
        readonly GameObject root;
        readonly Transform entryParent;
        readonly Camera camera;
        readonly RenderTexture target;
        readonly Texture2D readback;
        readonly List<GameObject> entries = new();

        public AuthoredEntryRenderFixture(StatusSharedMaterialPlayModeTests owner, bool masked)
        {
            this.owner = owner;
            const int layer = 31;
            root = new GameObject("AuthoredStatusCanvas", typeof(RectTransform), typeof(Canvas));
            owner.objects.Add(root); root.layer = layer;
            var cameraRoot = new GameObject("AuthoredStatusCamera", typeof(Camera));
            owner.objects.Add(cameraRoot);
            camera = cameraRoot.GetComponent<Camera>();
            camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 1f;
            camera.nearClipPlane = 0.1f; camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
            camera.cullingMask = 1 << layer; camera.allowHDR = false; camera.allowMSAA = false;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            target = new RenderTexture(128, 128, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            target.Create(); owner.resources.Add(target); camera.targetTexture = target;
            readback = new Texture2D(128, 128, TextureFormat.RGBA32, false, true);
            owner.resources.Add(readback);
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1f;
            entryParent = root.transform;
            if (masked)
            {
                Type image = Type.GetType("UnityEngine.UI.Image, UnityEngine.UI", true);
                Type mask = Type.GetType("UnityEngine.UI.Mask, UnityEngine.UI", true);
                var maskRoot = new GameObject("Status stencil canary", typeof(RectTransform), image, mask);
                maskRoot.layer = layer; maskRoot.transform.SetParent(entryParent, false);
                ((RectTransform)maskRoot.transform).sizeDelta = new Vector2(32f, 48f);
                mask.GetProperty("showMaskGraphic").SetValue(maskRoot.GetComponent(mask), false);
                entryParent = maskRoot.transform;
            }
        }

        public (GameObject go, Component text, Component binding, BindEntry bind) CreateEntry()
        {
            var fixture = owner.Create();
            fixture.go.transform.SetParent(entryParent, false);
            var rect = (RectTransform)fixture.go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition3D = Vector3.zero; rect.localScale = Vector3.one;
            foreach (Transform child in fixture.go.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
            // Keep the authored text, view and layout; isolate glyph pixels from the other entry graphics.
            foreach (Component graphic in fixture.go.GetComponentsInChildren(Graphic, true))
                if (graphic != fixture.text) ((Behaviour)graphic).enabled = false;
            entries.Add(fixture.go);
            return fixture;
        }

        public Color32[] Capture(GameObject entry, Component text)
        {
            foreach (GameObject other in entries) if (other != entry) other.SetActive(false);
            entry.SetActive(true);
            text.GetType().GetMethod("ForceMeshUpdate", new[] { typeof(bool), typeof(bool) }).Invoke(text, new object[] { false, false });
            Canvas.ForceUpdateCanvases();
            if (GraphicsSettings.currentRenderPipeline != null)
            {
                var request = new RenderPipeline.StandardRequest { destination = target };
                Assert.That(RenderPipeline.SupportsRenderRequest(camera, request), Is.True, "The active pipeline must support a Camera render request.");
                RenderPipeline.SubmitRenderRequest(camera, request);
            }
            else camera.Render();
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, 128, 128), 0, 0);
                readback.Apply();
                return readback.GetPixels32();
            }
            finally { RenderTexture.active = previous; }
        }

        public void AssertTextureOverride(GameObject entry, Component text, Object font, string context)
        {
            Color32[] actual = Capture(entry, text);
            AssertRendererAtlas(text, font);
            // GetMaterial alone cannot reveal CanvasRenderer's independent texture override.
            // Compare what it rendered against the same geometry with the canonical atlas.
            text.GetComponent<CanvasRenderer>().SetTexture(Material(font).mainTexture);
            Color32[] canonical = Capture(entry, text);
            AssertPixelsEqual(canonical, actual, context + " CanvasRenderer texture override");
        }
    }

    (GameObject go, Component text, Component binding, BindEntry bind) Create()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/status_HUD_EntryView.prefab");
        GameObject go = Object.Instantiate(prefab); objects.Add(go);
        Component view = go.GetComponent(View);
        Assert.That(View.GetField("progressTextMaterial", Fields).GetValue(view), Is.Not.Null, "Author approved shared style before tests.");
        Component text = (Component)View.GetField("stackText", Fields).GetValue(view);
        Component binding = text.GetComponent(FontBinding);
        Assert.That(binding, Is.Not.Null);
        return (go, text, binding, (BindEntry)Delegate.CreateDelegate(typeof(BindEntry), view, View.GetMethod("Bind")));
    }
    static void ApplyFont(Component binding, Object font) => FontBinding.GetMethod("ApplyLocalizedFont").Invoke(binding, new object[] { font });
    static Material Material(Object owner) => (Material)owner.GetType().GetProperty(owner is Component ? "fontSharedMaterial" : "material").GetValue(owner);
}
#endif
