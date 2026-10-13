// Stage in Assets/Editor of a disposable CapstoneSpeechBubble-* Unity project or
// Library/CodexSpeechBubbleLocalization/NativeProject.
// Requires fresh production DLLs/source, the real bubble/boss assets, TMP resources,
// GameFonts tables/locales and their Addressables dependencies. Never runs in the user Editor.
// -batchmode -nographics -executeMethod SpeechBubbleLocalizationRegression.Run
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.Rendering;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class SpeechBubbleLocalizationRegression
{
    const string Pending = "SpeechBubbleLocalizationRegression.Pending";
    const string PrefabPath = "Assets/_Project/Prefabs/UI/SpeechBubble/SpeechBubblePrefab.prefab";
    const string LocalizationPath = "Assets/_Project/Data/Localization/";
    const string DragonPath = "Assets/_Project/Data/Dialogue/SpeechData/DragonBossSpeechData.asset";
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly string[] Codes = { "ko", "en", "ja", "zh-Hans", "zh-Hant" };
    static readonly List<Material> retiredMaterials = new();
    static IEnumerator cases;
    static double deadline;
    static int checks;
    static int callbacks;
    static int secondCallbacks;
    static GameObject ownerObject;
    static GameObject secondOwnerObject;
    static SpeechBubble bubble;
    static SpeechBubble secondBubble;

    static SpeechBubbleLocalizationRegression() => EditorApplication.update += Tick;

    public static void Run()
    {
        string assets = Application.dataPath.Replace('\\', '/');
        if (!assets.Contains("CapstoneSpeechBubble-") &&
            !assets.EndsWith("/CodexSpeechBubbleLocalization/NativeProject/Assets", StringComparison.Ordinal))
            throw new Exception("Disposable CapstoneSpeechBubble-* validation project required.");
        RemapIsolatedDragonScript();
        SessionState.SetBool(Pending, true);
        EditorApplication.EnterPlaymode();
    }

    static void RemapIsolatedDragonScript()
    {
        // Core is staged as a freshly compiled DLL. Change only the disposable copy's
        // MonoScript pointer; preserve every authored theme/entry and its asset GUID.
        const string sourceScript = "{fileID: 11500000, guid: 251da8bed3e9c1a4cbe56be8f469f57c, type: 3}";
        string absolute = Path.Combine(Application.dataPath, "_Project/Data/Dialogue/SpeechData/DragonBossSpeechData.asset");
        string yaml = File.ReadAllText(absolute);
        if (!yaml.Contains(sourceScript))
        {
            Check(AssetDatabase.LoadAssetAtPath<BossSpeechData>(DragonPath) != null,
                "Previously remapped isolated Dragon asset must resolve.");
            return;
        }
        foreach (string dll in Directory.GetFiles(Path.Combine(Application.dataPath, "Plugins"), "*.dll", SearchOption.AllDirectories))
        {
            string assetPath = "Assets/" + dll.Substring(Application.dataPath.Length + 1).Replace('\\', '/');
            foreach (Object item in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                if (item is not MonoScript script || script.GetClass() != typeof(BossSpeechData)) continue;
                Check(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(script, out string guid, out long id),
                    "Core DLL BossSpeechData MonoScript pointer missing.");
                File.WriteAllText(absolute, yaml.Replace(sourceScript, $"{{fileID: {id}, guid: {guid}, type: 3}}"));
                AssetDatabase.ImportAsset(DragonPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                return;
            }
        }
        throw new Exception("Staged Core DLL has no BossSpeechData MonoScript.");
    }

    static void Tick()
    {
        if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling)
            return;
        try
        {
            if (cases == null)
            {
                deadline = EditorApplication.timeSinceStartup + 120;
                cases = Exercise();
            }
            if (EditorApplication.timeSinceStartup >= deadline)
                throw new Exception("Speech bubble regression timed out.");
            if (cases.MoveNext()) return;
            Debug.Log("SPEECH_BUBBLE_LOCALIZATION_PASS: " + checks +
                " checks; actual shared prefab, five font-table entries, initial/active locale callbacks, " +
                "default/disabled-custom/authored Dragon themes, rich text, pool hide/reuse OnEnable, " +
                "two simultaneous independent colors, current face/atlas/outline, unchanged source styles and retired material cleanup.");
            Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); Exit(1); }
    }

    static void Exit(int code)
    {
        SessionState.SetBool(Pending, false);
        EditorApplication.update -= Tick;
        if (bubble != null) Object.Destroy(bubble.gameObject);
        if (ownerObject != null) Object.Destroy(ownerObject);
        if (secondBubble != null) Object.Destroy(secondBubble.gameObject);
        if (secondOwnerObject != null) Object.Destroy(secondOwnerObject);
        EditorApplication.Exit(code);
    }

    static IEnumerator Exercise()
    {
        LocalizationSettings.Instance = Load<LocalizationSettings>(LocalizationPath + "LocalizationSettings.asset");
        var initialization = LocalizationSettings.InitializationOperation;
        while (!initialization.IsDone) yield return null;
        Check(initialization.Status == AsyncOperationStatus.Succeeded, "Localization initialization failed.");
        Locale originalLocale = LocalizationSettings.SelectedLocale;
        var locales = new Locale[Codes.Length];
        var fonts = new TMP_FontAsset[Codes.Length];
        SpeechBubble prefab = Load<GameObject>(PrefabPath).GetComponent<SpeechBubble>();
        Check(prefab != null, "Authored prefab must contain SpeechBubble.");
        var prefabBinding = prefab.GetComponentInChildren<LocalizeTmpFontEvent>(true);
        Check(prefabBinding != null && prefabBinding.AuthoredMaterial != null, "Authored font binding/style missing.");
        string key = prefabBinding.AssetReference.TableEntryReference.Key;
        for (int i = 0; i < Codes.Length; i++)
        {
            locales[i] = Load<Locale>(LocalizationPath + "Locale_" + Codes[i] + ".asset");
            AssetTable table = Load<AssetTable>(LocalizationPath + "GameFonts_" + Codes[i] + ".asset");
            var entry = table.GetEntry(key);
            Check(entry != null, "Actual GameFonts entry missing for " + Codes[i]);
            fonts[i] = Load<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(entry.Guid));
        }
        LocalizationSettings.SelectedLocale = locales[0];

        ownerObject = new GameObject("Actual speech bubble pool probe");
        ownerObject.SetActive(false);
        var owner = ownerObject.AddComponent<SpeechBubbleComponent>();
        Set(owner, "bubblePrefab", prefab);
        Set(owner, "defaultUseTyping", false);
        ownerObject.SetActive(true);
        owner.Speak("A<color=purple>B</color>", 600f, null);
        bubble = Get<SpeechBubble>(owner, "activeBubble");
        Check(bubble != null, "Real Speak path did not obtain pooled prefab.");
        Set(bubble, "playTypingSound", false);
        var text = bubble.GetComponentInChildren<TextMeshProUGUI>(true);
        var binding = text.GetComponent<LocalizeTmpFontEvent>();
        binding.OnUpdateAsset.AddListener(_ => callbacks++);
        while (!Delivered(binding, text, fonts[0])) yield return null;
        AssertProjection(text, binding, fonts[0], Color.black, "Initial automatic font callback");

        var dragon = Load<BossSpeechData>(DragonPath);
        Check(dragon.BubbleTheme.UseCustomColors, "Actual Dragon theme must be custom.");
        secondOwnerObject = new GameObject("Simultaneous independent speech bubble pool probe");
        secondOwnerObject.SetActive(false);
        var secondOwner = secondOwnerObject.AddComponent<SpeechBubbleComponent>();
        Set(secondOwner, "bubblePrefab", prefab);
        Set(secondOwner, "defaultUseTyping", false);
        secondOwnerObject.SetActive(true);
        secondOwner.Speak("A<color=purple>B</color>", 600f, dragon.BubbleTheme);
        secondBubble = Get<SpeechBubble>(secondOwner, "activeBubble");
        Check(secondBubble != null && secondBubble != bubble, "Two speakers must own independent prefab instances.");
        Set(secondBubble, "playTypingSound", false);
        var secondText = secondBubble.GetComponentInChildren<TextMeshProUGUI>(true);
        var secondBinding = secondText.GetComponent<LocalizeTmpFontEvent>();
        secondBinding.OnUpdateAsset.AddListener(_ => secondCallbacks++);
        while (!Delivered(secondBinding, secondText, fonts[0])) yield return null;
        AssertIndependent(text, binding, secondText, secondBinding, fonts[0], Color.black,
            dragon.BubbleTheme.FontColor, "Initial two speakers");

        var snapshots = new List<StyleSnapshot> { new(binding.AuthoredMaterial) };
        foreach (TMP_FontAsset font in fonts) snapshots.Add(new StyleSnapshot(font.material));
        // Prevent automatic locale delivery while explicitly exercising a callback AFTER ApplyTheme.
        binding.enabled = false;
        secondBinding.enabled = false;
        for (int i = 0; i < fonts.Length; i++)
        {
            owner.Speak("A<color=purple>B</color>", 600f, null);
            ApplyAndRetire(text, binding, fonts[i]);
            ApplyAndRetire(secondText, secondBinding, fonts[i]);
            AssertIndependent(text, binding, secondText, secondBinding, fonts[i], Color.black,
                dragon.BubbleTheme.FontColor, "Default theme / " + Codes[i]);
        }

        var disabledCustom = new SpeechBubbleThemeSettings();
        Set(disabledCustom, "fontColor", Color.magenta);
        owner.Speak("A<color=purple>B</color>", 600f, disabledCustom);
        ApplyAndRetire(text, binding, fonts[2]);
        AssertProjection(text, binding, fonts[2], Color.black, "UseCustomColors=false");

        secondOwner.Speak("A<color=purple>B</color>", 600f, null);
        owner.Speak("A<color=purple>B</color>", 600f, dragon.BubbleTheme);
        for (int i = 0; i < fonts.Length; i++)
        {
            ApplyAndRetire(text, binding, fonts[i]);
            ApplyAndRetire(secondText, secondBinding, fonts[i]);
            AssertIndependent(text, binding, secondText, secondBinding, fonts[i], dragon.BubbleTheme.FontColor,
                Color.black, "Dragon theme / " + Codes[i]);
            AssertRichText(text, dragon.BubbleTheme.FontColor);
        }
        // A new theme must apply to the currently localized atlas without another font callback.
        owner.Speak("A<color=purple>B</color>", 600f, null);
        secondOwner.Speak("A<color=purple>B</color>", 600f, dragon.BubbleTheme);
        AssertProjection(text, binding, fonts[4], Color.black, "Theme after font callback");

        binding.enabled = true;
        secondBinding.enabled = true;
        while (!Delivered(binding, text, fonts[0]) || !Delivered(secondBinding, secondText, fonts[0])) yield return null;
        AssertIndependent(text, binding, secondText, secondBinding, fonts[0], Color.black,
            dragon.BubbleTheme.FontColor, "Bindings re-enabled");
        secondOwner.Speak("A<color=purple>B</color>", 600f, null);
        for (int i = 1; i < locales.Length; i++)
        {
            owner.Speak("A<color=purple>B</color>", 600f, dragon.BubbleTheme);
            int before = callbacks;
            int secondBefore = secondCallbacks;
            LocalizationSettings.SelectedLocale = locales[i];
            while (callbacks == before || secondCallbacks == secondBefore ||
                !Delivered(binding, text, fonts[i]) || !Delivered(secondBinding, secondText, fonts[i])) yield return null;
            AssertIndependent(text, binding, secondText, secondBinding, fonts[i], dragon.BubbleTheme.FontColor,
                Color.black, "Active locale change / " + Codes[i]);
        }

        for (int reuse = 0; reuse < 3; reuse++)
        {
            owner.HideActive();
            while (bubble.gameObject.activeSelf) yield return null;
            Check(Get<SpeechBubble>(owner, "activeBubble") == null, "Hide must return bubble to its pool.");
            int before = callbacks;
            SpeechBubbleThemeSettings theme = reuse % 2 == 0 ? null : dragon.BubbleTheme;
            SpeechBubbleThemeSettings contrastingTheme = theme == null ? dragon.BubbleTheme : null;
            secondOwner.Speak("A<color=purple>B</color>", 600f, contrastingTheme);
            owner.Speak("A<color=purple>B</color>", 600f, theme);
            Check(Get<SpeechBubble>(owner, "activeBubble") == bubble, "Pool must reuse the same authored instance.");
            while (callbacks == before || !Delivered(binding, text, fonts[4])) yield return null;
            Color expected = theme == null ? Color.black : theme.FontColor;
            Color secondExpected = contrastingTheme == null ? Color.black : contrastingTheme.FontColor;
            AssertIndependent(text, binding, secondText, secondBinding, fonts[4], expected,
                secondExpected, "Pool OnEnable reuse " + reuse);
            for (int repeat = 0; repeat < 3; repeat++)
            {
                ApplyAndRetire(text, binding, fonts[4]);
                AssertIndependent(text, binding, secondText, secondBinding, fonts[4], expected,
                    secondExpected, "Repeated cached font callback");
            }
        }

        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
        {
            Debug.Log("SPEECH_BUBBLE_VISUAL_SKIPPED: null graphics device; state verification only.");
        }
        else
        {
            // Display the two real prefab instances after the state assertions pass.
            ownerObject.transform.position = new Vector3(0f, .6f, 0f);
            secondOwnerObject.transform.position = new Vector3(0f, -.6f, 0f);
            owner.Speak("기본 NPC 말풍선", 600f, null);
            secondOwner.Speak("드래곤 테마 말풍선", 600f, dragon.BubbleTheme);
            LocalizationSettings.SelectedLocale = locales[0];
            while (!Delivered(binding, text, fonts[0]) || !Delivered(secondBinding, secondText, fonts[0])) yield return null;
            float settledAt = Time.unscaledTime + .35f;
            while (Time.unscaledTime < settledAt) yield return null;
            AssertIndependent(text, binding, secondText, secondBinding, fonts[0], Color.black,
                dragon.BubbleTheme.FontColor, "Visible capture fixture after settled layout");
            CaptureActualPrefabs();
        }

        int cleanupFrame = Time.frameCount;
        while (Time.frameCount < cleanupFrame + 2) yield return null;
        foreach (Material retired in retiredMaterials)
            Check(retired == null, "Replaced SpeechBubble-owned material was not destroyed.");
        foreach (StyleSnapshot snapshot in snapshots) snapshot.AssertUnchanged();
        Material finalMaterial = text.fontSharedMaterial;
        Material secondFinalMaterial = secondText.fontSharedMaterial;
        Object.Destroy(bubble.gameObject);
        Object.Destroy(ownerObject);
        Object.Destroy(secondBubble.gameObject);
        Object.Destroy(secondOwnerObject);
        cleanupFrame = Time.frameCount;
        while (Time.frameCount < cleanupFrame + 2) yield return null;
        Check(finalMaterial == null, "Destroy must release final SpeechBubble-owned material.");
        Check(secondFinalMaterial == null, "Destroy must release second speaker's final owned material.");
        Canvas.ForceUpdateCanvases();
        foreach (StyleSnapshot snapshot in snapshots) snapshot.AssertUnchanged();
        if (originalLocale != null) LocalizationSettings.SelectedLocale = originalLocale;
    }

    static void CaptureActualPrefabs()
    {
        GameObject cameraObject = null;
        RenderTexture target = null;
        Texture2D pixels = null;
        RenderTexture previousTarget = RenderTexture.active;
        try
        {
            Canvas.ForceUpdateCanvases();
            if (!bubble.TryGetWorldBounds(out Bounds first) || !secondBubble.TryGetWorldBounds(out Bounds second))
                throw new Exception("Authored world canvas bounds unavailable for visual capture.");
            first.Encapsulate(second);
            cameraObject = new GameObject("Disposable speech bubble evidence camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.aspect = 1280f / 720f;
            camera.orthographicSize = Mathf.Max(.5f, first.extents.y * 1.3f, first.extents.x / camera.aspect * 1.3f);
            camera.transform.position = first.center + new Vector3(0f, 0f, -10f);
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 20f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.18f, .21f, .25f, 1f);
            camera.allowHDR = false;
            camera.allowMSAA = false;
            foreach (Canvas canvas in bubble.GetComponentsInChildren<Canvas>(true)) canvas.worldCamera = camera;
            foreach (Canvas canvas in secondBubble.GetComponentsInChildren<Canvas>(true)) canvas.worldCamera = camera;
            target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            target.Create();
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            pixels.Apply();
            Color32[] image = pixels.GetPixels32();
            Color32 background = image[0];
            int changedPixels = 0;
            foreach (Color32 pixel in image)
                if (!pixel.Equals(background)) changedPixels++;
            if (changedPixels < 100)
                throw new Exception("Camera produced an empty visual capture.");
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "native-colors.png"));
            File.WriteAllBytes(output, pixels.EncodeToPNG());
            Debug.Log("SPEECH_BUBBLE_VISUAL_SAVED: " + output + "; actual shared prefab fixture, default black and authored Dragon theme.");
        }
        catch (Exception e)
        {
            Debug.LogWarning("SPEECH_BUBBLE_VISUAL_UNAVAILABLE: " + e.Message + "; state verification remains valid.");
        }
        finally
        {
            RenderTexture.active = previousTarget;
            if (target != null) { target.Release(); Object.Destroy(target); }
            if (pixels != null) Object.Destroy(pixels);
            if (cameraObject != null) Object.Destroy(cameraObject);
        }
    }

    static void ApplyAndRetire(TMP_Text text, LocalizeTmpFontEvent binding, TMP_FontAsset font)
    {
        Material previous = text.fontSharedMaterial;
        binding.ApplyLocalizedFont(font);
        if (previous != text.fontSharedMaterial) retiredMaterials.Add(previous);
    }

    static bool Delivered(LocalizeTmpFontEvent binding, TMP_Text text, TMP_FontAsset expected)
    {
        var operation = binding.AssetReference.CurrentLoadingOperationHandle;
        if (!operation.IsValid() || !operation.IsDone) return false;
        if (operation.Status != AsyncOperationStatus.Succeeded)
            throw new Exception("Actual package font loading failed.");
        return operation.Result == expected && text.font == expected;
    }

    static void AssertIndependent(TextMeshProUGUI first, LocalizeTmpFontEvent firstBinding,
        TextMeshProUGUI second, LocalizeTmpFontEvent secondBinding, TMP_FontAsset font,
        Color firstColor, Color secondColor, string context)
    {
        AssertProjection(first, firstBinding, font, firstColor, context + " / first speaker");
        AssertProjection(second, secondBinding, font, secondColor, context + " / second speaker");
        Check(first.fontSharedMaterial != second.fontSharedMaterial,
            context + ": simultaneous speakers must not share their mutable theme material.");
        Check(firstBinding.AuthoredMaterial == secondBinding.AuthoredMaterial,
            context + ": fixture must exercise a common authored style source.");
    }

    static void AssertProjection(TextMeshProUGUI text, LocalizeTmpFontEvent binding,
        TMP_FontAsset font, Color expectedColor, string context)
    {
        binding.FlushPendingLayout();
        text.ForceMeshUpdate();
        Canvas.ForceUpdateCanvases();
        Material displayed = text.fontSharedMaterial;
        Check(text.font == font, context + ": wrong localized font.");
        Check(displayed != null && displayed != binding.AuthoredMaterial && displayed != font.material,
            context + ": bubble must own its display material without modifying source assets.");
        Check(Close(displayed.GetColor("_FaceColor"), expectedColor), context + ": lost current theme color.");
        Check(Close(text.color, Color.white), context + ": changed existing vertex-white theme semantics.");
        Check(displayed.mainTexture == font.material.mainTexture, context + ": stale localized atlas.");
        Check(Mathf.Approximately(displayed.GetFloat("_OutlineWidth"), binding.AuthoredMaterial.GetFloat("_OutlineWidth")),
            context + ": changed authored outline width.");
        Check(Close(displayed.GetColor("_OutlineColor"), binding.AuthoredMaterial.GetColor("_OutlineColor")),
            context + ": changed authored outline color.");
        Material renderer = text.canvasRenderer.GetMaterial(0);
        Check(renderer != null && renderer.mainTexture == font.material.mainTexture,
            context + ": CanvasRenderer material uses stale atlas.");
    }

    static void AssertRichText(TextMeshProUGUI text, Color theme)
    {
        text.ForceMeshUpdate();
        Check(text.textInfo.characterCount == 2, "Rich text must remain parsed into two characters.");
        Color32 first = text.textInfo.characterInfo[0].vertex_BL.color;
        Color32 tagged = text.textInfo.characterInfo[1].vertex_BL.color;
        Check(first.Equals(new Color32(255, 255, 255, 255)), "Untagged glyph vertex must stay white.");
        Check(tagged.Equals(new Color32(160, 32, 240, 255)), "Named purple tag must retain original TMP vertex color.");
        Color face = text.fontSharedMaterial.GetColor("_FaceColor");
        Check(Close(face * (Color)tagged, theme * (Color)tagged), "Theme must continue tinting rich text through face color.");
    }

    static bool Close(Color actual, Color expected) =>
        Mathf.Abs(actual.r - expected.r) < .001f && Mathf.Abs(actual.g - expected.g) < .001f &&
        Mathf.Abs(actual.b - expected.b) < .001f && Mathf.Abs(actual.a - expected.a) < .001f;
    static T Load<T>(string path) where T : Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        Check(asset != null, "Missing staged authored asset: " + path);
        return asset;
    }
    static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, Hidden).GetValue(target);
    static void Set(object target, string field, object value) => target.GetType().GetField(field, Hidden).SetValue(target, value);
    static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
    }

    sealed class StyleSnapshot
    {
        readonly Material material;
        readonly Shader shader;
        readonly Texture atlas;
        readonly Color face, outline;
        readonly float outlineWidth, faceDilate;
        public StyleSnapshot(Material source)
        {
            material = source; shader = source.shader; atlas = source.mainTexture;
            face = source.GetColor("_FaceColor"); outline = source.GetColor("_OutlineColor");
            outlineWidth = source.GetFloat("_OutlineWidth"); faceDilate = source.GetFloat("_FaceDilate");
        }
        public void AssertUnchanged()
        {
            Check(material != null && material.shader == shader && material.mainTexture == atlas &&
                Close(material.GetColor("_FaceColor"), face) && Close(material.GetColor("_OutlineColor"), outline) &&
                Mathf.Approximately(material.GetFloat("_OutlineWidth"), outlineWidth) &&
                Mathf.Approximately(material.GetFloat("_FaceDilate"), faceDilate),
                "SpeechBubble must not mutate/destroy shared authored or font source style.");
        }
    }
}
