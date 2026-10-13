#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Preview-only native rendering. The authored prefab, scenes, tables, and gameplay
// state are never saved by this tool. Text comes from ItemDetailPanel.ShowHover.
public static class GalaronTooltipPreviewCapture
{
    const string PrefabPath = "Assets/_Project/Prefabs/UI/GlobalUIRoot.prefab";
    const string WeaponPath = "Assets/_Project/Data/Items/Weapons/Definitions/WD_LightningSpear.asset";
    const string Folder = "Library/CodexItemKeywords";
    const int Width = 1800, Height = 1200;
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Serializable] public sealed class Label
    {
        public string path, text, font;
        public float size, preferredHeight;
        public bool active, overflow;
        public Rect rect;
    }
    [Serializable] public sealed class Layout
    {
        public string path, imageSprite, padding;
        public bool active;
        public Rect rect;
        public float spacing;
        public Vector2 anchoredPosition;
    }
    [Serializable] public sealed class Snapshot
    {
        public string variant, screenshot, definition, locale, sourcePrefabHash;
        public Rect combinedBounds;
        public Rect titleGlyphBounds, captionGlyphBounds, keyIconBounds;
        public float statSkillGap, skillCardGap;
        public int skillFrames;
        public bool detailed;
        public List<Label> labels = new();
        public List<Layout> layouts = new();
    }
    [Serializable] public sealed class Report
    {
        public string status, error, originalScene, originalLocale;
        public bool scenePreserved, localePreserved, sourcePreserved;
    }

    public static void CaptureBatch()
    {
        int code = 0;
        try { Capture(); }
        catch (Exception error) { Debug.LogException(error); code = 1; }
        if (Application.isBatchMode) EditorApplication.Exit(code);
    }

    [MenuItem("Tools/UI/Capture Galaron Tooltip Preview")]
    public static void Capture()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("The tooltip preview must run in Edit Mode.");
        if (GlobalUIRoot.Instance != null || Object.FindFirstObjectByType<GlobalUIRoot>() != null || ItemDetailPanel.Instance != null)
            throw new InvalidOperationException("A live UI singleton exists. Use an isolated batch Editor for the preview.");
        Directory.CreateDirectory(Folder);
        Scene original = SceneManager.GetActiveScene();
        bool dirty = original.isDirty;
        Locale originalLocale = LocalizationSettings.SelectedLocale;
        InputBindingService originalInput = InputBindingService.Instance;
        string sourceHash = Hash(PrefabPath);
        Report report = new() { status = "RUNNING", originalScene = original.path, originalLocale = originalLocale?.Identifier.Code };
        Scene preview = EditorSceneManager.NewPreviewScene();
        RenderTexture texture = new(Width, Height, 24);
        RenderTexture previous = RenderTexture.active;
        GameObject canvasRoot = null;
        try
        {
            LocalizationSettings.InitializationOperation.WaitForCompletion();
            Locale korean = LocalizationSettings.AvailableLocales.GetLocale("ko") ?? throw new InvalidOperationException("Korean locale is missing.");
            LocalizationSettings.SelectedLocale = korean;
            LocalizationSettings.StringDatabase.GetTableAsync(GameText.TableName, korean).WaitForCompletion();
            LocalizationSettings.AssetDatabase.GetTableAsync("GameFonts", korean).WaitForCompletion();

            canvasRoot = new GameObject("Galaron tooltip preview", typeof(RectTransform), typeof(Canvas));
            SceneManager.MoveGameObjectToScene(canvasRoot, preview);
            canvasRoot.SetActive(false);
            Canvas canvas = canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(Width, Height);
            canvas.transform.localScale = Vector3.one * 0.01f;
            GameObject cameraRoot = new("Galaron preview camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraRoot, preview);
            Camera camera = cameraRoot.GetComponent<Camera>();
            camera.scene = preview;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true;
            camera.orthographicSize = Height * 0.005f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.047f, 0.071f);
            camera.targetTexture = texture;
            canvas.worldCamera = camera;

            // Always use a separate read-only view of the configured bindings;
            // an existing service must not be initialized or altered by capture.
            {
                GameObject inputRoot = new("Preview read-only input binding", typeof(InputBindingService));
                SceneManager.MoveGameObjectToScene(inputRoot, preview);
                InputBindingService input = inputRoot.GetComponent<InputBindingService>();
                // The ordinary initialization migrates PlayerPrefs. Read the current
                // binding values directly instead, then use the normal glyph APIs.
                Set(input, "initialized", true);
                Dictionary<InputActionId, InputBinding> bindings = Get<Dictionary<InputActionId, InputBinding>>(input, "bindings");
                InputBindingDefaultsSO defaults = AssetDatabase.LoadAssetAtPath<InputBindingDefaultsSO>("Assets/_Project/Resources/InputBindingDefaults.asset")
                    ?? throw new InvalidOperationException("Authored input defaults are missing.");
                foreach (InputActionId action in defaults.GetRemappableActions())
                {
                    InputBinding binding = defaults.GetDefaultBinding(action);
                    string prefix = "settings.input." + action;
                    binding.primary = (KeyCode)PlayerPrefs.GetInt(prefix + ".primary", (int)binding.primary);
                    binding.secondary = (KeyCode)PlayerPrefs.GetInt(prefix + ".secondary", (int)binding.secondary);
                    bindings[action] = binding;
                }
                SetStaticInstance(typeof(InputBindingService), input);
            }

            Transform authored = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).transform.Find("HoverCanvas/ItemDetailPanel");
            GameObject copy = Object.Instantiate(authored.gameObject, canvas.transform, false);
            ItemDetailPanel panel = copy.GetComponent<ItemDetailPanel>();
            Set(panel, "openDuration", 0f);
            Set(panel, "closeDuration", 0f);
            canvasRoot.SetActive(true);
            typeof(ItemDetailPanel).GetMethod("Awake", Private).Invoke(panel, null);
            WeaponDefinition weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(WeaponPath)
                ?? throw new InvalidOperationException("Galaron definition is missing.");
            panel.ShowHover(weapon, null);
            ApplyLocalizedFonts(copy);
            Rebuild(panel);
            CaptureVariant(panel, camera, texture, "current", sourceHash);
            CompactThinVariant(panel);
            Rebuild(panel);
            CaptureVariant(panel, camera, texture, "thin", sourceHash);
            Object.DestroyImmediate(copy);
            CaptureLocalizedItems(authored, canvas, camera, texture, sourceHash);
            report.status = "PASS";
        }
        catch (Exception error)
        {
            report.status = "FAIL";
            report.error = error.ToString();
            throw;
        }
        finally
        {
            RenderTexture.active = previous;
            EditorSceneManager.ClosePreviewScene(preview);
            SetStaticInstance(typeof(InputBindingService), originalInput);
            LocalizationSettings.SelectedLocale = originalLocale;
            texture.Release();
            Object.DestroyImmediate(texture);
            report.scenePreserved = SceneManager.GetActiveScene() == original && original.isDirty == dirty;
            report.localePreserved = LocalizationSettings.SelectedLocale == originalLocale;
            report.sourcePreserved = Hash(PrefabPath) == sourceHash;
            File.WriteAllText(Folder + "/galaron-capture-report.json", JsonUtility.ToJson(report, true));
        }
        if (!report.scenePreserved || !report.localePreserved || !report.sourcePreserved)
            throw new InvalidOperationException("Preview preservation check failed.");
        Debug.Log("[GalaronTooltipPreview] Native current and thin previews captured without asset or gameplay saves.");
    }

    static void ApplyLocalizedFonts(GameObject root)
    {
        foreach (LocalizeTmpFontEvent binding in root.GetComponentsInChildren<LocalizeTmpFontEvent>(true))
        {
            TMP_FontAsset font = binding.AssetReference.LoadAssetAsync().WaitForCompletion();
            if (font == null) throw new InvalidOperationException("Localized font failed: " + PathOf(binding.transform, root.transform));
            binding.ApplyLocalizedFont(font);
            binding.FlushPendingLayout();
        }
    }

    static void Rebuild(ItemDetailPanel panel)
    {
        for (int pass = 0; pass < 4; pass++)
        {
            foreach (RectTransform rect in panel.GetComponentsInChildren<RectTransform>(true).Reverse())
                if (rect.gameObject.activeInHierarchy) LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            panel.GetComponent<ItemKeywordTooltipView>().Refresh();
            Canvas.ForceUpdateCanvases();
        }
    }

    static void CaptureLocalizedItems(Transform authored, Canvas canvas, Camera camera, RenderTexture texture, string sourceHash)
    {
        const string relicPath = "Assets/_Project/Data/Items/Relics/Definitions/RD_BurningFeather.asset";
        foreach (string code in new[] { "ko", "en", "ja", "zh-Hans", "zh-Hant" })
        {
            Locale locale = LocalizationSettings.AvailableLocales.GetLocale(code)
                ?? throw new InvalidOperationException("Preview locale missing: " + code);
            LocalizationSettings.SelectedLocale = locale;
            LocalizationSettings.StringDatabase.GetTableAsync(GameText.TableName, locale).WaitForCompletion();
            LocalizationSettings.AssetDatabase.GetTableAsync("GameFonts", locale).WaitForCompletion();
            foreach (string path in new[] { WeaponPath, relicPath })
            {
                bool weapon = path == WeaponPath;
                foreach (bool detailed in weapon ? new[] { false, true } : new[] { false })
                {
                    GameObject copy = Object.Instantiate(authored.gameObject, canvas.transform, false);
                    try
                    {
                        ItemDetailPanel panel = copy.GetComponent<ItemDetailPanel>();
                        Set(panel, "openDuration", 0f); Set(panel, "closeDuration", 0f);
                        typeof(ItemDetailPanel).GetMethod("Awake", Private).Invoke(panel, null);
                        Object definition = AssetDatabase.LoadAssetAtPath<Object>(path)
                            ?? throw new InvalidOperationException("Preview item missing: " + path);
                        if (detailed)
                        {
                            // Seed a retained detailed selection before the single
                            // ShowHover call; repeated Edit Mode projection destroys
                            // runtime rows asynchronously and is not a safe preview.
                            copy.SetActive(true);
                            Get<CanvasGroup>(panel, "canvasGroup").alpha = 1;
                            Set(panel, "currentDefinition", definition);
                            Set(panel, "showDetailedDescription", true);
                        }
                        panel.ShowHover(definition, null);
                        ApplyLocalizedFonts(copy); Rebuild(panel);
                        if (!copy.transform.Find("KeywordTooltipStack/" + (weapon ? "ElectrocutedCard" : "BurnCard")).gameObject.activeInHierarchy)
                            throw new InvalidOperationException("Actual item keyword card missing: " + path + "/" + code);
                        CaptureVariant(panel, camera, texture, (weapon ? (detailed ? "weapon-detail-" : "weapon-") : "relic-") + code,
                            sourceHash, path, code, detailed);
                    }
                    finally { Object.DestroyImmediate(copy); }
                }
            }
        }
    }

    static void CaptureVariant(ItemDetailPanel panel, Camera camera, RenderTexture texture, string variant, string sourceHash,
        string definition = WeaponPath, string locale = "ko", bool detailed = false)
    {
        Rect bounds = panel.GetHoverLocalBounds();
        // Keep both previews at the same native pixel size, centered as a group.
        panel.Rect.anchoredPosition = -bounds.center;
        Canvas.ForceUpdateCanvases();
        Snapshot snapshot = new() { variant = variant, screenshot = Path.GetFullPath(Folder + "/galaron-" + variant + ".png"),
            definition = definition, locale = locale, sourcePrefabHash = sourceHash, combinedBounds = bounds, detailed = detailed };
        ValidateSkillLayout(panel, snapshot);
        foreach (TMP_Text text in panel.GetComponentsInChildren<TMP_Text>(true))
        {
            text.ForceMeshUpdate(true);
            snapshot.labels.Add(new Label { path = PathOf(text.transform, panel.transform), text = text.text,
                font = text.font?.name, size = text.fontSize, active = text.isActiveAndEnabled,
                overflow = text.isTextOverflowing, rect = text.rectTransform.rect, preferredHeight = text.preferredHeight });
        }
        foreach (RectTransform rect in panel.GetComponentsInChildren<RectTransform>(true))
        {
            LayoutGroup group = rect.GetComponent<LayoutGroup>();
            HorizontalOrVerticalLayoutGroup linear = group as HorizontalOrVerticalLayoutGroup;
            Image image = rect.GetComponent<Image>();
            Sprite sprite = image != null ? image.sprite : null;
            snapshot.layouts.Add(new Layout { path = PathOf(rect, panel.transform), active = rect.gameObject.activeInHierarchy,
                rect = rect.rect, anchoredPosition = rect.anchoredPosition, imageSprite = sprite != null ? sprite.name : null,
                padding = group == null ? null : $"{group.padding.left},{group.padding.right},{group.padding.top},{group.padding.bottom}", spacing = linear != null ? linear.spacing : 0 });
        }
        if (snapshot.labels.Any(label => label.active && !string.IsNullOrEmpty(label.text) && label.overflow))
            throw new InvalidOperationException("Visible text overflow: " + variant);
        if (bounds.width > Width || bounds.height > Height)
            throw new InvalidOperationException("Tooltip exceeds the native preview viewport: " + variant);
        camera.Render();
        RenderTexture.active = texture;
        Texture2D pixels = new(Width, Height, TextureFormat.RGB24, false);
        try
        {
            pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            pixels.Apply();
            File.WriteAllBytes(snapshot.screenshot, pixels.EncodeToPNG());
        }
        finally { Object.DestroyImmediate(pixels); }
        File.WriteAllText(Folder + "/galaron-" + variant + ".json", JsonUtility.ToJson(snapshot, true));
    }

    static void CompactThinVariant(ItemDetailPanel panel) => ItemKeywordTooltipAuthoring.ApplyApprovedStyle(panel);

    static void ValidateSkillLayout(ItemDetailPanel panel, Snapshot snapshot)
    {
        var data = new SerializedObject(panel);
        RectTransform hint = ((GameObject)data.FindProperty("weaponDescriptionHint.root").objectReferenceValue).transform as RectTransform;
        if (snapshot.definition != WeaponPath)
        {
            if (hint.gameObject.activeSelf) throw new InvalidOperationException("Relic retained the weapon description guide.");
            return;
        }
        TMP_Text title = data.FindProperty("titleText").objectReferenceValue as TMP_Text;
        TMP_Text caption = data.FindProperty("weaponDescriptionHint.label").objectReferenceValue as TMP_Text;
        UnityEngine.UI.Image key = data.FindProperty("weaponDescriptionHint.keyIcon").objectReferenceValue as UnityEngine.UI.Image;
        Rect titleBounds = GlyphBounds(title, panel.Rect);
        snapshot.titleGlyphBounds = titleBounds;
        snapshot.captionGlyphBounds = GlyphBounds(caption, panel.Rect);
        snapshot.keyIconBounds = LocalBounds(key.rectTransform, panel.Rect);
        if (!hint.gameObject.activeInHierarchy || titleBounds.Overlaps(snapshot.captionGlyphBounds) ||
            (key.gameObject.activeInHierarchy && titleBounds.Overlaps(snapshot.keyIconBounds)))
            throw new InvalidOperationException("Weapon title and upper-right guide overlap: " + snapshot.variant +
                "; title=" + titleBounds + "; caption=" + snapshot.captionGlyphBounds + "; key=" + snapshot.keyIconBounds);
        Rect hintBounds = LocalBounds(hint, panel.Rect);
        if (!Contains(panel.Rect.rect, hintBounds)) throw new InvalidOperationException("Description guide leaves the main panel.");
        Transform container = panel.transform.Find("Body_parent/ContentPanel/Content/AbilityBlockContainer");
        Rect previous = default;
        foreach (WeaponAbilityBlockView block in container.GetComponentsInChildren<WeaponAbilityBlockView>())
        {
            RectTransform motion = block.transform.Find("CardMotionRoot") as RectTransform;
            RectTransform frame = motion.Find("ThinFrame") as RectTransform;
            if (motion.rect.width <= 0 || frame == null || frame.childCount != 4)
                throw new InvalidOperationException("Whole skill frame has no authored width: " + snapshot.variant);
            Rect frameBounds = LocalBounds(frame, panel.Rect);
            if (!Contains(panel.Rect.rect, frameBounds)) throw new InvalidOperationException("Skill frame leaves the main item panel.");
            foreach (Graphic graphic in motion.GetComponentsInChildren<Graphic>())
                if (!graphic.transform.IsChildOf(frame) && graphic.rectTransform.rect.width > 0 && graphic.rectTransform.rect.height > 0 &&
                    !Contains(frameBounds, LocalBounds(graphic.rectTransform, panel.Rect)))
                    throw new InvalidOperationException("Skill frame does not enclose " + graphic.name + ": " + snapshot.variant);
            if (snapshot.skillFrames == 0)
            {
                WeaponDetailViewV2 view = data.FindProperty("weaponViewV2").objectReferenceValue as WeaponDetailViewV2;
                RectTransform stats = new SerializedObject(view).FindProperty("statRoot").objectReferenceValue as RectTransform;
                snapshot.statSkillGap = LocalBounds(stats, panel.Rect).yMin - frameBounds.yMax;
                if (Mathf.Abs(snapshot.statSkillGap - 15) > 0.5f)
                    throw new InvalidOperationException("Stat-to-skill gap differs from the selected small gap: " + snapshot.statSkillGap);
            }
            else
            {
                snapshot.skillCardGap = previous.yMin - frameBounds.yMax;
                if (Mathf.Abs(snapshot.skillCardGap - 10) > 0.5f)
                    throw new InvalidOperationException("Skill borders do not retain their separate gap: " + snapshot.skillCardGap);
            }
            previous = frameBounds; snapshot.skillFrames++;
        }
        if (snapshot.skillFrames != 3) throw new InvalidOperationException("Galaron must display three framed skill groups.");
    }

    static Rect GlyphBounds(TMP_Text text, RectTransform origin)
    {
        text.ForceMeshUpdate(true);
        Vector2 min = new(float.PositiveInfinity, float.PositiveInfinity), max = new(float.NegativeInfinity, float.NegativeInfinity);
        foreach (TMP_CharacterInfo character in text.textInfo.characterInfo.Take(text.textInfo.characterCount))
        {
            if (!character.isVisible) continue;
            Vector2 low = origin.InverseTransformPoint(text.transform.TransformPoint(character.bottomLeft));
            Vector2 high = origin.InverseTransformPoint(text.transform.TransformPoint(character.topRight));
            min = Vector2.Min(min, low); max = Vector2.Max(max, high);
        }
        return float.IsPositiveInfinity(min.x) ? default : Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    static Rect LocalBounds(RectTransform source, RectTransform origin)
    {
        Vector3[] corners = new Vector3[4]; source.GetWorldCorners(corners);
        Vector2 min = new(float.PositiveInfinity, float.PositiveInfinity), max = new(float.NegativeInfinity, float.NegativeInfinity);
        foreach (Vector3 corner in corners)
        {
            Vector2 point = origin.InverseTransformPoint(corner);
            min = Vector2.Min(min, point); max = Vector2.Max(max, point);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    static bool Contains(Rect outer, Rect inner) => inner.xMin >= outer.xMin - 0.5f && inner.xMax <= outer.xMax + 0.5f &&
        inner.yMin >= outer.yMin - 0.5f && inner.yMax <= outer.yMax + 0.5f;

    static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
    static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    static void SetStaticInstance(Type type, object value) => type.GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, value);
    static string PathOf(Transform transform, Transform root) => transform == root ? root.name : PathOf(transform.parent, root) + "/" + transform.name;
    static string Hash(string path)
    {
        using SHA256 sha = SHA256.Create();
        return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));
    }
}
#endif
