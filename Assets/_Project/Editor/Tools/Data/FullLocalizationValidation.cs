using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization.Components;

internal static class FullLocalizationValidation
{
    [Serializable] private sealed class BindingRow { public string key, path, owner; }
    [Serializable] private sealed class BindingDocument { public List<BindingRow> rows; }
    private static bool buildAfterValidation;
    private static bool renderAfterValidation;

    private const string ScreenReviewFlag = "Codex.LocalizationScreens.Running";
    private const string ScreenReviewCleanupFlag = "Codex.LocalizationScreens.Cleanup";
    private const string ScreenReviewFolder = "outputs/localization-screen-review-2026-10-04";
    private const string ScreenReviewBackup = "Library/CodexFonts/screen-review-save-backup.json";
    [Serializable] private sealed class SavedReviewFile { public string path, data; public bool existed; }
    [Serializable] private sealed class SavedReviewFiles { public List<SavedReviewFile> rows = new(); }
    [Serializable] private sealed class ReviewLanguageBackup { public bool existed; public int value; }
    [Serializable] private sealed class ScreenLabel { public string owner, text, font; public bool overflow; public float size; }
    [Serializable] private sealed class ScreenSnapshot { public string scene, locale, screen, image; public List<ScreenLabel> labels = new(); }
    private static int screenReviewJob, screenReviewPhase;
    private static double screenReviewNextTime;
    private static UnityEngine.AsyncOperation screenReviewLoad;
    private static string screenReviewLoadedScene;
    private static readonly string[] ReviewLocales = { "ja", "ko", "en", "zh-Hans", "zh-Hant" };
    private static string[] ReviewScreens => SessionState.GetInt("Codex.LocalizationScreens.Group", 1) switch {
        2 => new[] { "hub", "inventory", "encyclopedia", "weapon-simple", "weapon-detail", "relic" },
        3 => new[] { "tutorial", "tutorial-info" },
        _ => new[] { "title", "profiles", "settings" }
    };
    private static string ReviewStartScene => SessionState.GetInt("Codex.LocalizationScreens.Group", 1) switch { 2 => "ProtoTypeHub", 3 => "TutorialCorridor", _ => "TitleScene" };
    public static void ReviewTitleScreensBatch() { SessionState.SetInt("Codex.LocalizationScreens.Group", 1); ReviewGameScreensBatch(); }
    public static void ReviewHubScreensBatch() { SessionState.SetInt("Codex.LocalizationScreens.Group", 2); ReviewGameScreensBatch(); }
    public static void ReviewTutorialScreensBatch() { SessionState.SetInt("Codex.LocalizationScreens.Group", 3); ReviewGameScreensBatch(); }

    public static void ReviewGameScreensBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use isolated batch Editor for screen review.");
        Directory.CreateDirectory(ScreenReviewFolder);
        const string recoveryPath = "Library/CodexFonts/screen-review-language-recovery.json";
        if (File.Exists(recoveryPath))
        {
            var recovery = JsonUtility.FromJson<ReviewLanguageBackup>(File.ReadAllText(recoveryPath));
            if (recovery.existed) PlayerPrefs.SetInt("settings.language", recovery.value);
            else PlayerPrefs.DeleteKey("settings.language");
            PlayerPrefs.Save();
            File.Delete(recoveryPath);
        }
        var backup = new SavedReviewFiles();
        for (int i = 0; i < 3; i++)
            foreach (string path in new[] { GameDataRepository.GetDefaultSavePath(i), GameDataRepository.GetInspectableSavePath(i) })
                if (!string.IsNullOrEmpty(path)) backup.rows.Add(new SavedReviewFile { path = Path.GetFullPath(path), existed = File.Exists(path), data = File.Exists(path) ? Convert.ToBase64String(File.ReadAllBytes(path)) : null });
        File.WriteAllText(ScreenReviewBackup, JsonUtility.ToJson(backup, true));
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/" + ReviewStartScene + ".unity", OpenSceneMode.Single);
        SessionState.SetBool("Codex.LocalizationScreens.Failed", false);
        SessionState.SetBool("Codex.LocalizationScreens.LanguageExisted", PlayerPrefs.HasKey("settings.language"));
        SessionState.SetInt("Codex.LocalizationScreens.Language", PlayerPrefs.GetInt("settings.language", 0));
        File.WriteAllText("Library/CodexFonts/screen-review-language-backup.json", JsonUtility.ToJson(new ReviewLanguageBackup { existed = PlayerPrefs.HasKey("settings.language"), value = PlayerPrefs.GetInt("settings.language", 0) }));
        SessionState.SetBool(ScreenReviewFlag, true);
        var gameViewType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
        var gameView = EditorWindow.GetWindow(gameViewType);
        gameView.position = new Rect(0, 0, 1920, 1108);
        gameView.Show();
        gameView.Focus();
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void ResumeGameScreenReview()
    {
        if (SessionState.GetBool(ScreenReviewFlag, false))
        {
            screenReviewNextTime = EditorApplication.timeSinceStartup + 4;
            screenReviewLoadedScene = ReviewStartScene;
            EditorApplication.update += StepGameScreenReview;
        }
        else if (SessionState.GetBool(ScreenReviewCleanupFlag, false))
            EditorApplication.delayCall += CompleteGameScreenReview;
    }

    private static T ReviewObject<T>() where T : Component =>
        Resources.FindObjectsOfTypeAll<T>().FirstOrDefault(x => x.gameObject.scene.IsValid());

    private static void StepGameScreenReview()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.timeSinceStartup < screenReviewNextTime) return;
        try
        {
            if (screenReviewJob >= ReviewLocales.Length * ReviewScreens.Length)
            {
                FinishGameScreenReview();
                return;
            }
            string code = ReviewLocales[screenReviewJob / ReviewScreens.Length];
            string screen = ReviewScreens[screenReviewJob % ReviewScreens.Length];
            string scene = screen.StartsWith("tutorial") ? "TutorialCorridor" : screen is "title" or "profiles" or "settings" ? "TitleScene" : "ProtoTypeHub";
            if (screenReviewLoadedScene != scene)
            {
                if (screenReviewLoad == null) { screenReviewLoad = EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/_Project/Scenes/" + scene + ".unity", new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
                    if (screenReviewLoad == null) throw new InvalidOperationException("Review scene load could not start: " + scene);
                    return; }
                if (!screenReviewLoad.isDone) return;
                screenReviewLoad = null;
                screenReviewLoadedScene = scene;
                screenReviewNextTime = EditorApplication.timeSinceStartup + 4;
                return;
            }
            string imagePath = ScreenReviewFolder + "/" + code + "-" + screen + ".png";
            if (screenReviewPhase == 0)
            {
                var locale = UnityEngine.Localization.Settings.LocalizationSettings.AvailableLocales.GetLocale(code);
                UnityEngine.Localization.Settings.LocalizationSettings.StringDatabase.GetTableAsync(GameText.TableName, locale).WaitForCompletion();
                GameSettingsService.EnsureInstance().SetLanguage(code switch { "ja" => GameLanguageOption.Japanese, "ko" => GameLanguageOption.Korean, "zh-Hans" => GameLanguageOption.SimplifiedChinese, "zh-Hant" => GameLanguageOption.TraditionalChinese, _ => GameLanguageOption.English });
                ReviewObject<TitleProfileSlotPanelUI>()?.CloseUI();
                ReviewObject<SettingsPanelUI>()?.CloseUI();
                var inventoryManager = ReviewObject<InventoryUIManager>();
                if (inventoryManager != null && inventoryManager.IsOpen) inventoryManager.Close();
                else ReviewObject<InventoryScreen>()?.CloseUI();
                ReviewObject<EncyclopediaScreen>()?.CloseUI();
                ReviewObject<ItemDetailPanel>()?.HideHover();
                ReviewObject<HoverUIController>()?.HideImmediate();
                ReviewObject<TutorialInfoPanel>()?.Hide();
                screenReviewPhase = 1;
                screenReviewNextTime = EditorApplication.timeSinceStartup + 1;
                return;
            }
            if (screenReviewPhase == 1)
            {
                if (screen == "title")
                {
                    var button = (UnityEngine.UI.Button)GetProbeField(ReviewObject<TitleMenuController>(), "newGameButton");
                    UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(button.gameObject);
                }
                else if (screen == "profiles") ReviewObject<TitleProfileSlotPanelUI>().OpenUI();
                else if (screen == "settings") ReviewObject<SettingsPanelUI>().OpenUI();
                else if (screen == "inventory")
                {
                    if (!ReviewObject<InventoryUIManager>().TryOpen()) throw new InvalidOperationException("Actual inventory open was blocked.");
                }
                else if (screen == "encyclopedia") ReviewObject<EncyclopediaScreen>().OpenUI();
                else if (screen is "weapon-simple" or "weapon-detail" or "relic")
                {
                    var database = AssetDatabase.FindAssets("t:ItemDatabase", new[] { "Assets/_Project/Data" }).Select(g => AssetDatabase.LoadAssetAtPath<ItemDatabase>(AssetDatabase.GUIDToAssetPath(g))).First();
                    var detail = ReviewObject<ItemDetailPanel>();
                    ReviewObject<HoverUIController>().ShowHover(detail, (RectTransform)ReviewObject<InventoryScreen>().transform,
                        screen == "relic" ? (object)database.allRelics.First(x => x != null) : database.allWeapons.First(x => x != null), null);
                    if (screen == "weapon-detail")
                    {
                        SetProbeField(detail, "showDetailedDescription", true);
                        detail.GetType().GetMethod("HandleLocaleChanged", ProbeFlags).Invoke(detail, new object[] { UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale });
                        ((WeaponDescriptionHint)GetProbeField(detail, "weaponDescriptionHint"))?.Refresh(true, true);
                    }
                }
                else if (screen == "tutorial-info")
                {
                    var trigger = Resources.FindObjectsOfTypeAll<TutorialInfoTrigger>().First(x => x.gameObject.scene.name == scene);
                    var authored = new SerializedObject(trigger);
                    string id = authored.FindProperty("tutorialId").stringValue;
                    var pages = authored.FindProperty("pages");
                    var requestPages = new TutorialInfoPage[pages.arraySize];
                    for (int i = 0; i < pages.arraySize; i++)
                    {
                        var page = pages.GetArrayElementAtIndex(i);
                        requestPages[i] = new TutorialInfoPage { title = page.FindPropertyRelative("title").stringValue, body = page.FindPropertyRelative("body").stringValue,
                            contentSprite = page.FindPropertyRelative("contentSprite").objectReferenceValue as Sprite };
                    }
                    ReviewObject<TutorialInfoPanel>().Show(new TutorialInfoRequest { tutorialId = id, pages = requestPages, holdSeconds = 2, usePersistentCompletion = false, markCompletedOnClose = false });
                }
                screenReviewPhase = 2;
                screenReviewNextTime = EditorApplication.timeSinceStartup + 3;
                return;
            }
            if (screenReviewPhase == 2)
            {
                if (UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale.Identifier.Code != code)
                {
                    GameSettingsService.EnsureInstance().SetLanguage(code switch { "ja" => GameLanguageOption.Japanese, "ko" => GameLanguageOption.Korean, "zh-Hans" => GameLanguageOption.SimplifiedChinese, "zh-Hant" => GameLanguageOption.TraditionalChinese, _ => GameLanguageOption.English });
                    UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale = UnityEngine.Localization.Settings.LocalizationSettings.AvailableLocales.GetLocale(code);
                    screenReviewNextTime = EditorApplication.timeSinceStartup + 2;
                    return;
                }
                // Direct scene-load review bypasses the portal route's normal overlay release.
                SceneFadeTransitionService.Instance?.HideOverlayImmediately();
                ReviewObject<LoadingOverlayController>()?.ForceHidePresentation();
                Canvas.ForceUpdateCanvases();
                var snapshot = new ScreenSnapshot { scene = scene, locale = code, screen = screen, image = imagePath };
                foreach (var text in Resources.FindObjectsOfTypeAll<TMP_Text>().Where(t => t.gameObject.scene.IsValid() && t.isActiveAndEnabled))
                {
                    if (text.GetComponentsInParent<CanvasGroup>().Any(g => g.alpha < 0.01f)) continue;
                    text.ForceMeshUpdate();
                    snapshot.labels.Add(new ScreenLabel { owner = Hierarchy(text.transform), text = text.text, font = text.font != null ? text.font.name : "null", overflow = text.isTextOverflowing, size = text.fontSize });
                }
                File.WriteAllLines(ScreenReviewFolder + "/" + code + "-" + screen + "-cameras.txt", Resources.FindObjectsOfTypeAll<Camera>().Where(c => c.gameObject.scene.IsValid()).Select(c => c.name + ": enabled=" + c.isActiveAndEnabled + "; rect=" + c.rect + "; target=" + c.targetTexture + "; display=" + c.targetDisplay));
                File.WriteAllText(ScreenReviewFolder + "/" + code + "-" + screen + ".json", JsonUtility.ToJson(snapshot, true));
                ScreenCapture.CaptureScreenshot(Path.GetFullPath(imagePath));
                screenReviewPhase = 3;
                screenReviewNextTime = EditorApplication.timeSinceStartup + 2;
                return;
            }
            if (!File.Exists(imagePath) || new FileInfo(imagePath).Length < 5000)
                throw new InvalidOperationException("Actual GameView capture missing: " + imagePath);
            var captured = new Texture2D(2, 2);
            try
            {
                if (!captured.LoadImage(File.ReadAllBytes(imagePath))) throw new InvalidOperationException("Screenshot is not a PNG: " + imagePath);
                var pixels = captured.GetPixels32();
                if (!pixels.Where((pixel, index) => index % Mathf.Max(1, pixels.Length / 1024) == 0).Any(pixel => pixel.r > 8 || pixel.g > 8 || pixel.b > 8))
                    throw new InvalidOperationException("Screenshot contains only black; not accepted: " + imagePath);
            }
            finally { UnityEngine.Object.DestroyImmediate(captured); }
            Debug.Log("[GameScreenReview] " + screenReviewJob + ": " + imagePath + "; PlayMode=" + Application.isPlaying + "; " + Screen.width + "x" + Screen.height);
            screenReviewJob++;
            screenReviewPhase = 0;
        }
        catch (Exception e)
        {
            File.WriteAllText(ScreenReviewFolder + "/failure.txt", e.ToString());
            Debug.LogException(e);
            SessionState.SetBool("Codex.LocalizationScreens.Failed", true);
            FinishGameScreenReview();
        }
    }

    private static void FinishGameScreenReview()
    {
        EditorApplication.update -= StepGameScreenReview;
        SessionState.SetBool(ScreenReviewFlag, false);
        SessionState.SetBool(ScreenReviewCleanupFlag, true);
        EditorApplication.update += CompleteGameScreenReview;
        EditorApplication.ExitPlaymode();
    }

    private static void CompleteGameScreenReview()
    {
        if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
        EditorApplication.update -= CompleteGameScreenReview;
        if (!SessionState.GetBool(ScreenReviewCleanupFlag, false)) return;
        var backup = JsonUtility.FromJson<SavedReviewFiles>(File.ReadAllText(ScreenReviewBackup));
        foreach (var row in backup.rows)
        {
            if (row.existed) File.WriteAllBytes(row.path, Convert.FromBase64String(row.data));
            else if (File.Exists(row.path)) File.Delete(row.path); // Only exact files created by this review run.
        }
        if (SessionState.GetBool("Codex.LocalizationScreens.LanguageExisted", false))
            PlayerPrefs.SetInt("settings.language", SessionState.GetInt("Codex.LocalizationScreens.Language", 0));
        else PlayerPrefs.DeleteKey("settings.language");
        PlayerPrefs.Save();
        SessionState.SetBool(ScreenReviewCleanupFlag, false);
        EditorApplication.Exit(SessionState.GetBool("Codex.LocalizationScreens.Failed", false) ? 1 : 0);
    }
    public static void RunAndRenderBatch()
    {
        renderAfterValidation = true;
        RunBatch();
    }
    public static void RenderOutlineBatch()
    {
        try
        {
            var source = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Art/Font/Galmuri9 SDF BlackOutline.asset").material;
            var tables = AssetDatabase.LoadAssetAtPath<AssetTableCollection>("Assets/_Project/Data/Localization/GameFonts.asset");
            const string key = "font.5cb2a964a4d75134d94a70d8c8aa34ee";
            foreach (var pair in new[] { ("ko", "한글"), ("en", "Outline"), ("ja", "日本語"), ("zh-Hans", "简体字"), ("zh-Hant", "繁體字") })
            {
                var table = tables.AssetTables.First(t => t.LocaleIdentifier.Code == pair.Item1);
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(table.GetEntry(key).Guid));
                var labelObject = new GameObject("Outline render probe", typeof(TextMeshPro));
                var cameraObject = new GameObject("Outline render camera", typeof(Camera));
                var render = new RenderTexture(512, 128, 24);
                Material material = null;
                try
                {
                    labelObject.layer = 31;
                    var label = labelObject.GetComponent<TextMeshPro>();
                    label.font = font;
                    material = new Material(TMP_MaterialManager.GetFallbackMaterial(source, font.material));
                    label.fontSharedMaterial = material;
                    label.fontSize = 24;
                    label.color = Color.white;
                    label.alignment = TextAlignmentOptions.Center;
                    label.rectTransform.sizeDelta = new Vector2(24, 6);
                    label.text = pair.Item2;
                    label.ForceMeshUpdate();
                    var camera = cameraObject.GetComponent<Camera>();
                    camera.transform.position = new Vector3(0, 0, -20);
                    camera.orthographic = true;
                    camera.orthographicSize = 3;
                    camera.cullingMask = 1 << 31;
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = new Color(0.35f, 0.45f, 0.6f, 1);
                    camera.targetTexture = render;
                    camera.Render();
                    var outlined = ReadOutlinePixels(render, "Temp/outline-" + pair.Item1 + ".png");
                    material.SetFloat("_OutlineWidth", 0);
                    label.UpdateMeshPadding();
                    label.ForceMeshUpdate();
                    camera.Render();
                    var plain = ReadOutlinePixels(render, null);
                    int changed = outlined.Zip(plain, (a, b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b)).Count(delta => delta > 0.08f);
                    if (changed < 20) throw new InvalidOperationException("Outline produced no visible pixels: " + pair.Item1 + "; changed=" + changed);
                    Debug.Log("[OutlineRender] " + pair.Item1 + ": " + changed + " visible outline pixels; Korean width=" + source.GetFloat("_OutlineWidth"));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(labelObject);
                    UnityEngine.Object.DestroyImmediate(cameraObject);
                    UnityEngine.Object.DestroyImmediate(material);
                    render.Release();
                    UnityEngine.Object.DestroyImmediate(render);
                }
            }
            BuildAddressablesBatch();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorApplication.delayCall += () => EditorApplication.Exit(1);
        }
    }

    private static Color[] ReadOutlinePixels(RenderTexture render, string path)
    {
        var previous = RenderTexture.active;
        var texture = new Texture2D(render.width, render.height, TextureFormat.RGBA32, false);
        try
        {
            RenderTexture.active = render;
            texture.ReadPixels(new Rect(0, 0, render.width, render.height), 0, 0);
            texture.Apply();
            if (path != null) File.WriteAllBytes(path, texture.EncodeToPNG());
            return texture.GetPixels();
        }
        finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(texture); }
    }
    public static void RunAndBuildBatch()
    {
        buildAfterValidation = true;
        RunBatch();
    }
    public static void RunBatch()
    {
        try
        {
            LocalizationSheetSetup.Import();
            var collection = AssetDatabase.LoadAssetAtPath<StringTableCollection>("Assets/_Project/Data/Localization/GameText.asset");
            int checkedValues = 0;
            foreach (var table in collection.StringTables)
            {
                foreach (var key in collection.SharedData.Entries)
                {
                    var entry = table.GetEntry(key.Id);
                    if (entry == null || string.IsNullOrWhiteSpace(entry.Value))
                        throw new InvalidOperationException("Missing translation: " + key.Key + "/" + table.LocaleIdentifier.Code);
                    checkedValues++;
                }
                string region = table.LocaleIdentifier.Code switch { "ja" => "jp", "zh-Hans" => "sc", "zh-Hant" => "tc", _ => null };
                if (region == null) continue;
                var fontTables = AssetDatabase.LoadAssetAtPath<AssetTableCollection>("Assets/_Project/Data/Localization/GameFonts.asset");
                var fontEntry = fontTables.AssetTables.First(t => t.LocaleIdentifier.Code == table.LocaleIdentifier.Code)
                    .GetEntry("font.5cb2a964a4d75134d94a70d8c8aa34ee");
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(fontEntry.Guid));
                if (font == null) throw new InvalidOperationException("Missing locale font: " + region);
                var addressableSettings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
                if (addressableSettings == null || addressableSettings.FindAssetEntry(fontEntry.Guid) == null)
                    throw new InvalidOperationException("Locale font not registered in Addressables: " + font.name);
                string characters = string.Concat(table.Values.Select(e => Regex.Replace(e.Value, "<[^>]*>", "")));
                characters = new string(characters.Where(c => !char.IsControl(c)).Distinct().ToArray());
                if (!font.HasCharacters(characters, out uint[] missing, false, true))
                    throw new InvalidOperationException("Missing glyphs in " + region + ": " + string.Join(",", missing.Select(c => "U+" + c.ToString("X"))));
                Debug.Log("[FullLocalizationValidation] " + region + ": " + characters.Length + " unique characters supported.");
            }
            Debug.Log("[FullLocalizationValidation] Complete: " + checkedValues + " translated table values.");
            ValidateBindings();
            ValidateDynamicDisplays();
            ValidateTitleDisplays();
            ValidateFlowDisplays();
            // Leave transient glyph additions unsaved; the fonts populate dynamically in the player.
            LocalizationValidation.RunChecks();
            if (renderAfterValidation) RenderOutlineBatch();
            else if (buildAfterValidation) BuildAddressablesBatch();
            else EditorApplication.delayCall += () => EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorApplication.delayCall += () => EditorApplication.Exit(1);
        }
    }

    private static void ValidateBindings()
    {
        var rows = JsonUtility.FromJson<BindingDocument>(File.ReadAllText("DataSheets/Localization/StaticUiBindings.json")).rows;
        var captions = rows.ToDictionary(r => r.path + "::" + r.owner, r => r.key);
        var strings = AssetDatabase.LoadAssetAtPath<StringTableCollection>("Assets/_Project/Data/Localization/GameText.asset");
        var fonts = AssetDatabase.LoadAssetAtPath<AssetTableCollection>("Assets/_Project/Data/Localization/GameFonts.asset");
        if (fonts == null || fonts.AssetTables.Count != 5) throw new InvalidOperationException("Five font tables required.");
        int textCount = 0, fontCount = 0;
        foreach (string path in rows.Select(r => r.path).Concat(FullLocalizationAuthoring.FlowChestPaths).Distinct())
        {
            GameObject prefab = null;
            IEnumerable<TMP_Text> labels;
            if (path.EndsWith(".prefab", StringComparison.Ordinal))
            {
                prefab = PrefabUtility.LoadPrefabContents(path);
                labels = prefab.GetComponentsInChildren<TMP_Text>(true);
            }
            else
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                labels = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TMP_Text>(true));
            }
            try
            {
                foreach (var label in labels)
                {
                    if (captions.TryGetValue(path + "::" + Hierarchy(label.transform), out string key))
                    {
                        var binding = label.GetComponent<LocalizeStringEvent>();
                        if (binding == null || binding.StringReference.TableEntryReference.Key != key ||
                            binding.OnUpdateString.GetPersistentEventCount() != 1 ||
                            binding.OnUpdateString.GetPersistentTarget(0) != label ||
                            binding.OnUpdateString.GetPersistentMethodName(0) != "set_text" ||
                            binding.OnUpdateString.GetPersistentListenerState(0) != UnityEngine.Events.UnityEventCallState.EditorAndRuntime)
                            throw new InvalidOperationException("Invalid text binding: " + path + "/" + key +
                                "; actual key=" + binding?.StringReference.TableEntryReference.Key +
                                "; target=" + binding?.OnUpdateString.GetPersistentTarget(0));
                        foreach (var table in strings.StringTables)
                        {
                            string expected = table.GetEntry(key).Value;
                            binding.OnUpdateString.Invoke(expected);
                            if (label.text != expected) throw new InvalidOperationException("Caption setter failed: " + key);
                        }
                        textCount++;
                    }
                    if (label.font == null) continue;
                    var fontBinding = label.GetComponent<LocalizeTmpFontEvent>();
                    if (fontBinding == null || fontBinding.AssetReference.IsEmpty ||
                        fontBinding.OnUpdateAsset.GetPersistentEventCount() != 1 ||
                        fontBinding.OnUpdateAsset.GetPersistentTarget(0) != fontBinding ||
                        fontBinding.OnUpdateAsset.GetPersistentMethodName(0) != "ApplyLocalizedFont" ||
                        fontBinding.OnUpdateAsset.GetPersistentListenerState(0) != UnityEngine.Events.UnityEventCallState.EditorAndRuntime)
                        throw new InvalidOperationException("Invalid font binding: " + path + "/" + Hierarchy(label.transform));
                    foreach (var table in fonts.AssetTables)
                    {
                        var entry = table.GetEntry(fontBinding.AssetReference.TableEntryReference.Key);
                        var font = entry == null ? null : AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(entry.Guid));
                        if (font == null) throw new InvalidOperationException("Missing font asset: " + table.LocaleIdentifier.Code);
                        fontBinding.OnUpdateAsset.Invoke(font);
                        if (label.font != font) throw new InvalidOperationException("Font setter failed.");
                        if (fontBinding.AuthoredMaterial == null) throw new InvalidOperationException("Missing authored font style.");
                        if (AssetDatabase.GetAssetPath(fontBinding.AuthoredMaterial).StartsWith("Assets/_Project/Data/Localization/Fonts/", StringComparison.Ordinal))
                            throw new InvalidOperationException("Locale preview material used as authored Korean style: " + path + "/" + Hierarchy(label.transform));
                        var material = label.fontSharedMaterial;
                        foreach (string property in new[] { "_OutlineWidth", "_OutlineColor", "_FaceDilate", "_FaceColor" })
                        {
                            if (!material.HasProperty(property) || !fontBinding.AuthoredMaterial.HasProperty(property)) continue;
                            bool isColor = property.EndsWith("Color", StringComparison.Ordinal);
                            if (isColor ? material.GetColor(property) != fontBinding.AuthoredMaterial.GetColor(property) :
                                !Mathf.Approximately(material.GetFloat(property), fontBinding.AuthoredMaterial.GetFloat(property)))
                                throw new InvalidOperationException("Font style changed: " + property);
                        }
                        if (material.GetTexture(ShaderUtilities.ID_MainTex) != font.material.GetTexture(ShaderUtilities.ID_MainTex))
                            throw new InvalidOperationException("Font atlas mismatch: " + path + "/" + Hierarchy(label.transform) + "/" + font.name);
                    }
                    fontCount++;
                }
            }
            finally { if (prefab != null) PrefabUtility.UnloadPrefabContents(prefab); }
        }
        if (textCount != rows.Count) throw new InvalidOperationException("Static caption binding coverage mismatch.");
        Debug.Log($"[FullLocalizationValidation] Read back {textCount} captions and {fontCount} font bindings; five-language setters passed.");
        // Changes above exist only in preview objects. Do not save scenes or prefabs.
    }

    private static string Hierarchy(Transform t) => t.parent == null ? t.name : Hierarchy(t.parent) + "/" + t.name;

    private static void ValidateDynamicDisplays()
    {
        var originalLocale = UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale;
        int checkedDisplays = 0;
        var npcs = AssetDatabase.FindAssets("t:NPCData", new[] { "Assets/_Project/Data/Dialogue" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<NPCData>(AssetDatabase.GUIDToAssetPath(guid))).ToArray();
        var registries = npcs.Select(npc =>
        {
            var registry = new DialogueParticipantRegistry();
            registry.Initialize(new List<NPCData> { npc });
            return registry;
        }).ToArray();
        var dialoguePrefab = PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/UI/GlobalUIRoot.prefab");
        var dialogueView = dialoguePrefab.GetComponentInChildren<DialogueView>(true);
        var speakerLabel = new SerializedObject(dialogueView).FindProperty("nameText").objectReferenceValue as TMP_Text;
        if (speakerLabel == null) throw new InvalidOperationException("Dialogue speaker label is missing.");
        var abilities = AssetDatabase.FindAssets("t:AbilityDefinition", new[] { "Assets/_Project/Data/Abilities/Definitions" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<UnityGAS.AbilityDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(ability => !string.IsNullOrWhiteSpace(ability.simpleDescription)).ToArray();
        var variants = AssetDatabase.FindAssets("t:LightningSpearSkill1Data", new[] { "Assets/_Project/Data" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<LightningSpearSkill1Data>(AssetDatabase.GUIDToAssetPath(guid))).ToArray();
        var database = AssetDatabase.LoadAssetAtPath<ItemDatabase>("Assets/_Project/Data/Items/ItemDatabase.asset");
        var relics = database.allRelics.Where(relic => relic != null && relic.logic != null).Distinct().ToArray();
        var tutorialScene = EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/TutorialCorridor.unity");
        var slimeScene = EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/SlimeCorridor.unity");
        var standPrefab = PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/Map/Interactables/EncyclopediaStand.prefab");
        var merchantPrefab = PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/Loot/RunMerchantGroup.prefab");
        if (merchantPrefab.GetComponentInChildren<MerchantRefreshInteractable>(true) == null)
            throw new InvalidOperationException("Authored merchant refresh presenter is missing.");
        var tutorialPanel = dialoguePrefab.GetComponentInChildren<TutorialInfoPanel>(true);
        if (tutorialPanel == null) throw new InvalidOperationException("Authored tutorial panel is missing.");
        try
        {
            foreach (string code in new[] { "en", "ja", "zh-Hans", "zh-Hant", "ko", "en" })
            {
                var locale = UnityEngine.Localization.Settings.LocalizationSettings.AvailableLocales.GetLocale(code);
                UnityEngine.Localization.Settings.LocalizationSettings.StringDatabase.GetTableAsync(GameText.TableName, locale).WaitForCompletion();
                UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale = locale;
                for (int npcIndex = 0; npcIndex < npcs.Length; npcIndex++)
                {
                    var npc = npcs[npcIndex];
                    var registry = registries[npcIndex];
                    registry.HandleSpeakerTag(new List<string> { "speaker:" + npc.id });
                    if (registry.CurrentSpeakerName != npc.DisplayName) throw new InvalidOperationException("NPC name projection mismatch.");
                    dialogueView.RefreshSpeakerName(registry.CurrentSpeakerName);
                    if (speakerLabel.text != npc.DisplayName) throw new InvalidOperationException("NPC name label did not refresh.");
                    CheckTranslated(registry.CurrentSpeakerName, code, npc.name);
                    checkedDisplays++;
                }
                foreach (var ability in abilities)
                {
                    CheckTranslated(DetailTextFormatter.Format(ability.SimpleDescription, null), code, ability.name + "/simple");
                    CheckTranslated(DetailTextFormatter.Format(ability.Description, null), code, ability.name + "/detail");
                    checkedDisplays += 2;
                }
                foreach (var variant in variants)
                    for (int i = 0; i < 2; i++)
                    {
                        var display = variant.BuildAbilityTooltipVariant(null, i, null);
                        CheckTranslated(display.Title, code, variant.name + "/title");
                        CheckTranslated(DetailTextFormatter.Format(display.SimpleBody, null), code, variant.name + "/simple");
                        CheckTranslated(DetailTextFormatter.Format(display.Body, null), code, variant.name + "/detail");
                        checkedDisplays += 3;
                    }
                foreach (var relic in relics)
                {
                    for (int level = 1; level <= Math.Max(1, relic.maxLevel); level++)
                    {
                        string formatted = DetailTextFormatter.Format(relic.logic.BuildTooltip(relic, level, new ItemDetailContext { IsInspectionOnly = true }).effectText, null);
                        CheckTranslated(formatted, code, relic.name + "/level" + level);
                        checkedDisplays++;
                    }
                }
                foreach (var root in tutorialScene.GetRootGameObjects())
                    foreach (var trigger in root.GetComponentsInChildren<TutorialInfoTrigger>(true))
                    {
                        var serialized = new SerializedObject(trigger);
                        string id = serialized.FindProperty("tutorialId").stringValue;
                        var pages = serialized.FindProperty("pages");
                        var authoredPages = new TutorialInfoPage[pages.arraySize];
                        for (int index = 0; index < pages.arraySize; index++)
                        {
                            var page = pages.GetArrayElementAtIndex(index);
                            authoredPages[index] = new TutorialInfoPage
                            {
                                title = page.FindPropertyRelative("title").stringValue,
                                body = page.FindPropertyRelative("body").stringValue
                            };
                        }
                        SetProbeField(tutorialPanel, "activeRequest", new TutorialInfoRequest { tutorialId = id });
                        SetProbeField(tutorialPanel, "activePages", authoredPages);
                        for (int index = 0; index < authoredPages.Length; index++)
                        {
                            SetProbeField(tutorialPanel, "currentPageIndex", index);
                            SetProbeField(tutorialPanel, "heldSeconds", 0.37f);
                            InvokeProbe(tutorialPanel, "RefreshLocalizedPageText");
                            var panelFields = new SerializedObject(tutorialPanel);
                            foreach (string field in new[] { "titleText", "bodyText" })
                            {
                                var label = panelFields.FindProperty(field).objectReferenceValue as TMP_Text;
                                if (label == null) throw new InvalidOperationException("Tutorial label is missing: " + field);
                                CheckTranslated(label.text, code, id + "/" + index + "/" + field);
                                if (string.IsNullOrWhiteSpace(label.text)) throw new InvalidOperationException("Tutorial text is empty.");
                                checkedDisplays++;
                            }
                            if ((int)GetProbeField(tutorialPanel, "currentPageIndex") != index ||
                                (float)GetProbeField(tutorialPanel, "heldSeconds") != 0.37f)
                                throw new InvalidOperationException("Localization changed tutorial navigation/hold state.");
                        }
                    }
                foreach (var root in tutorialScene.GetRootGameObjects().Concat(slimeScene.GetRootGameObjects()).Concat(new[] { standPrefab, merchantPrefab }))
                {
                    foreach (var nameplate in root.GetComponentsInChildren<NpcNameplatePresenter>(true))
                    {
                        // Simulate an already visible label after locale initialization/switch.
                        var label = new SerializedObject(nameplate).FindProperty("nameText").objectReferenceValue as TMP_Text;
                        if (label == null) throw new InvalidOperationException("Nameplate label is missing.");
                        label.text = "이전 언어";
                        InvokeProbe(nameplate, "LateUpdate");
                        CheckTranslated(label.text, code, "world nameplate/" + root.name);
                        if (label.text == "이전 언어") throw new InvalidOperationException("Nameplate did not refresh.");
                        checkedDisplays++;
                    }
                    foreach (var refresh in root.GetComponentsInChildren<MerchantRefreshInteractable>(true))
                    {
                        var label = new SerializedObject(refresh).FindProperty("remainingCountText").objectReferenceValue as TMP_Text;
                        if (label == null) throw new InvalidOperationException("Merchant count label is missing.");
                        SetProbeField(refresh, "hasAwakened", true);
                        for (var parent = label.transform; parent != null; parent = parent.parent)
                            parent.gameObject.SetActive(true);
                        label.text = "이전 언어";
                        InvokeProbe(refresh, "LateUpdate");
                        CheckTranslated(label.text, code, "merchant remaining count");
                        if (label.text == "이전 언어") throw new InvalidOperationException("Merchant count did not refresh.");
                        checkedDisplays++;
                    }
                }
                foreach (Type type in new[] { typeof(RelicLogic_BurnOnCriticalHit_Managed), typeof(RelicLogic_CritChanceAfterCriticalHit_Managed), typeof(RelicLogic_CritChanceOnNonCriticalHit_Managed) })
                {
                    var logic = ScriptableObject.CreateInstance(type) as RelicLogic;
                    try
                    {
                        CheckTranslated(DetailTextFormatter.Format(logic.BuildTooltip(null, 1, null).effectText, null), code, type.Name + "/empty template fallback");
                        checkedDisplays++;
                    }
                    finally { UnityEngine.Object.DestroyImmediate(logic); }
                }
                foreach (string key in new[] { "ui.item.take", "ui.item.drop", "world.nameplate.encyclopedia", "gameover.fire_puddle" })
                {
                    CheckTranslated(GameText.Get(key, "번역 누락"), code, key);
                    checkedDisplays++;
                }
                CheckTranslated(GameText.Format("tutorial.dummy.damage", "피해 {0}/{1}\nDPS {2}/{3}\n누적 {4}", 90, 150, 90, 152, 371), code, "training damage");
                CheckTranslated(GameText.Format("ui.level_reward.rerolls", "[R] 리롤 {0}/{1}", 2, 3), code, "reward rerolls");
                checkedDisplays += 2;
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(merchantPrefab);
            PrefabUtility.UnloadPrefabContents(standPrefab);
            EditorSceneManager.ClosePreviewScene(slimeScene);
            EditorSceneManager.ClosePreviewScene(tutorialScene);
            PrefabUtility.UnloadPrefabContents(dialoguePrefab);
            UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale = originalLocale;
        }
        Debug.Log($"[FullLocalizationValidation] Dynamic display regression passed: {checkedDisplays} NPC/weapon/all registered relic levels/tutorial projections, including locale round trips and preserved tutorial holds.");
    }

    private static void ValidateTitleDisplays()
    {
        var originalLocale = UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale;
        var scene = EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/TitleScene.unity");
        int checks = 0;
        try
        {
            var roots = scene.GetRootGameObjects();
            // Preview scenes do not run CanvasScaler; emulate a visible canvas rather than its authored zero scale.
            foreach (var canvas in roots.SelectMany(r => r.GetComponentsInChildren<Canvas>(true)))
                if (canvas.transform.localScale == Vector3.zero) canvas.transform.localScale = Vector3.one;
            var cards = roots.SelectMany(r => r.GetComponentsInChildren<TitleProfileSlotCardUI>(true)).ToArray();
            var markers = roots.SelectMany(r => r.GetComponentsInChildren<MenuButtonHighlightPresentation>(true)).ToArray();
            if (cards.Length != 3 || markers.Length != 3) throw new InvalidOperationException("Expected three title cards and menu markers.");
            var fonts = AssetDatabase.LoadAssetAtPath<AssetTableCollection>("Assets/_Project/Data/Localization/GameFonts.asset");
            foreach (string code in new[] { "en", "ja", "zh-Hans", "zh-Hant", "ko", "en" })
            {
                var locale = UnityEngine.Localization.Settings.LocalizationSettings.AvailableLocales.GetLocale(code);
                UnityEngine.Localization.Settings.LocalizationSettings.StringDatabase.GetTableAsync(GameText.TableName, locale).WaitForCompletion();
                UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale = locale;
                foreach (var label in roots.SelectMany(r => r.GetComponentsInChildren<TMP_Text>(true)))
                {
                    var binding = label.GetComponent<LocalizeTmpFontEvent>();
                    if (binding == null) throw new InvalidOperationException("Title font binding missing: " + Hierarchy(label.transform));
                    var key = binding.AssetReference.TableEntryReference.Key;
                    var entry = fonts.AssetTables.First(t => t.LocaleIdentifier.Code == code).GetEntry(key);
                    if (entry == null) throw new InvalidOperationException("Title font key missing: " + key);
                    binding.ApplyLocalizedFont(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(entry.Guid)));
                    checks++;
                }
                for (int i = 0; i < cards.Length; i++)
                {
                    var summary = new TitleProfileSlotSummary(i, true, true,
                        GameText.Get("code.titleprofileslotmodels.d2f7319b25", "슬롯 ") + (i + 1),
                        GameText.Format("code.titleprofileslotservice.2cda8aa535", "{0}시간 {1}분", 1, 5), "56%",
                        GameText.Format("code.titleprofileslotservice.b1166a37e1", "{0}개", 3),
                        GameText.Format("code.titleprofileslotservice.9ec5470102", "{0}회", 2));
                    cards[i].Bind(summary, null, null);
                    cards[i].SetInteractable(false);
                    cards[i].RefreshLocalizedText(summary);
                    var fields = new SerializedObject(cards[i]);
                    foreach (string field in new[] { "playTimeTitleText", "playTimeValueText", "upgradeProgressTitleText", "magicStoneTitleText", "magicStoneValueText", "clearCountTitleText", "clearCountValueText", "selectButtonLabelText" })
                    {
                        var label = fields.FindProperty(field).objectReferenceValue as TMP_Text;
                        if (label == null || string.IsNullOrWhiteSpace(label.text)) throw new InvalidOperationException("Title field missing: " + field);
                        if (label.GetComponent<LocalizeStringEvent>() != null) throw new InvalidOperationException("Fixed string event can overwrite runtime title value: " + field);
                        CheckTranslated(label.text, code, field);
                        checks++;
                    }
                    var select = fields.FindProperty("selectButton").objectReferenceValue as UnityEngine.UI.Button;
                    if (select.interactable) throw new InvalidOperationException("Locale refresh reset title confirmation state.");
                }
                foreach (var marker in markers)
                {
                    var label = marker.GetComponentInChildren<TMP_Text>(true);
                    var localized = label.GetComponent<LocalizeStringEvent>();
                    if (localized == null) throw new InvalidOperationException("Title menu string binding missing.");
                    for (Transform t = label.transform; t != null; t = t.parent) t.gameObject.SetActive(true);
                    label.text = localized.StringReference.GetLocalizedString();
                    Canvas.ForceUpdateCanvases();
                    marker.GetType().GetMethod("Snap", ProbeFlags).Invoke(marker, new object[] { true });
                    var sword = new SerializedObject(marker).FindProperty("swordRoot").objectReferenceValue as RectTransform;
                    var parent = sword.parent;
                    var corners = new Vector3[4];
                    sword.GetWorldCorners(corners);
                    float right = corners.Max(c => parent.InverseTransformPoint(c).x);
                    var left = parent.InverseTransformPoint(label.transform.TransformPoint(new Vector3(label.textBounds.min.x, label.textBounds.center.y, 0)));
                    if (Mathf.Abs(left.x - right - 12f) > 0.2f) throw new InvalidOperationException("Title marker gap mismatch: " + code + "/" + Hierarchy(marker.transform) + "; gap=" + (left.x - right) + "; count=" + label.textInfo.characterCount + "; cached=" + ((TMP_Text)GetProbeField(marker, "labelText"))?.name + "; text=" + label.text);
                    checks++;
                }
            }
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale = originalLocale;
        }
        Debug.Log("[FullLocalizationValidation] Title cards/font bindings/menu marker geometry passed: " + checks + " checks. Preview only; no scene saved.");
    }

    [Serializable] private sealed class FlowRow { public string key, path, type, owner, field, text; }
    [Serializable] private sealed class FlowDocument { public List<FlowRow> rows; }
    private static void ValidateFlowDisplays()
    {
        var originalLocale = UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale;
        var originalRandom = UnityEngine.Random.state;
        var rows = JsonUtility.FromJson<FlowDocument>(File.ReadAllText("DataSheets/Localization/FlowNewAssetFields.json")).rows;
        var encyclopedia = PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/UI/PopupUI/Encyclopedia/EncyclopediaUI.prefab");
        var tab = encyclopedia.GetComponentInChildren<EncyclopediaItemTab>(true);
        var left = (EncyclopediaItemLeftPage)GetProbeField(tab, "leftPage");
        var title = (TMP_Text)GetProbeField(left, "titleText");
        var ui = PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/UI/GlobalUIRoot.prefab");
        var gameOver = ui.GetComponentInChildren<GameOverPresentationController>(true);
        InvokeProbe(gameOver, "ResolveReferences");
        var deathRequest = new GameOverPresentationRequest { CauseKind = GameOverCauseKind.Monster,
            CauseName = "몬스터", CauseNameKey = "code.gameoverpresentationcontroller.b151220e53" };
        string deathKey = null;
        int checks = 0;
        try
        {
            foreach (string code in new[] { "en", "ja", "zh-Hans", "zh-Hant", "ko", "en" })
            {
                var locale = UnityEngine.Localization.Settings.LocalizationSettings.AvailableLocales.GetLocale(code);
                UnityEngine.Localization.Settings.LocalizationSettings.StringDatabase.GetTableAsync(GameText.TableName, locale).WaitForCompletion();
                UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale = locale;
                foreach (var row in rows)
                {
                    var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(row.path);
                    var serialized = new SerializedObject(asset);
                    if (serialized.FindProperty(row.field).stringValue != row.text)
                        throw new InvalidOperationException("Authored flow data changed: " + row.key);
                    string actual;
                    var indices = Regex.Matches(row.field, @"data\[(\d+)\]").Cast<Match>().Select(m => int.Parse(m.Groups[1].Value)).ToArray();
                    if (asset is BossSpeechData || asset is PlayerSpeechData)
                    {
                        int entry = indices[0], line = indices[1];
                        int length = serialized.FindProperty($"entries.Array.data[{entry}].lines").arraySize;
                        int seed = 0;
                        for (; seed < 10000; seed++)
                        {
                            UnityEngine.Random.InitState(seed);
                            if (UnityEngine.Random.Range(0, length) == line) break;
                        }
                        if (seed == 10000) throw new InvalidOperationException("Speech selection probe failed.");
                        string expectedRandom = JsonUtility.ToJson(UnityEngine.Random.state);
                        UnityEngine.Random.InitState(seed);
                        actual = asset is BossSpeechData boss ? boss.GetLine(boss.entries[entry].situation)
                            : ((PlayerSpeechData)asset).GetLine(((PlayerSpeechData)asset).entries[entry].situation);
                        if (JsonUtility.ToJson(UnityEngine.Random.state) != expectedRandom)
                            throw new InvalidOperationException("Speech selection changed RNG consumption: " + row.key);
                    }
                    else if (asset is TitleIntroSequenceSO intro) actual = intro.GetSlideText(indices[0]);
                    else if (asset is EndingOutroSequenceSO outro) actual = outro.GetSlideText(indices[0]);
                    else if (asset is CorridorBossRouteSetSO route)
                    {
                        string scene = row.field == "corridorLocationName" ? route.CorridorSceneName : route.BossSceneName;
                        if (!route.TryResolveLocationName(scene, out actual)) throw new InvalidOperationException("Route identity no longer resolves.");
                    }
                    else throw new InvalidOperationException("Unexpected flow asset: " + row.type);
                    if (actual != GameText.Get(row.key, null) || string.IsNullOrWhiteSpace(actual))
                        throw new InvalidOperationException("Flow display bypasses table: " + row.key + "/" + code);
                    CheckTranslated(actual, code, row.key);
                    checks++;
                }
                for (int category = 0; category < 3; category++)
                {
                    string key = new[] { "code.encyclopediaitemtab.8c4dac1fcc", "code.encyclopediaitemtab.b949ada423", "code.encyclopediaitemtab.fd06a09504" }[category];
                    SetProbeField(tab, "currentSubTab", (EncyclopediaItemSubTab)category);
                    InvokeProbe(tab, "RefreshTitle");
                    if (title.text != GameText.Get(key, null)) throw new InvalidOperationException("Authored encyclopedia preset bypassed translation.");
                    checks++;
                }
                foreach (string path in FullLocalizationAuthoring.FlowChestPaths)
                {
                    var chest = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        foreach (var view in chest.GetComponentsInChildren<ChestMonsterKillLockView>(true))
                        {
                            var label = (TMP_Text)GetProbeField(view, "remainingCountText");
                            if (label == null) continue;
                            view.GetType().GetMethod("RefreshText", ProbeFlags).Invoke(view, new object[] { false, 3 });
                            if (label.text != GameText.Format("chest.remaining_monsters", null, 3)) throw new InvalidOperationException("Chest count projection failed.");
                            CheckTranslated(label.text, code, path);
                            checks++;
                        }
                    }
                    finally { PrefabUtility.UnloadPrefabContents(chest); }
                }
                string before = JsonUtility.ToJson(UnityEngine.Random.state);
                string death = (string)gameOver.GetType().GetMethod("BuildDeathMessage", ProbeFlags).Invoke(gameOver, new object[] { deathRequest });
                string selected = (string)GetProbeField(gameOver, "selectedDeathMessageKey");
                if (deathKey != null && (selected != deathKey || JsonUtility.ToJson(UnityEngine.Random.state) != before))
                    throw new InvalidOperationException("Locale refresh rerolled death phrase.");
                deathKey = selected;
                CheckTranslated(death, code, "death phrase");
                var victory = new GameOverPresentationRequest { IsVictory = true, MagicStoneRewardAmount = 7 };
                gameOver.GetType().GetMethod("ApplyText", ProbeFlags).Invoke(gameOver, new object[] { victory });
                foreach (var field in new[] { "titleText", "messageText", "locationText" })
                {
                    var label = (TMP_Text)GetProbeField(gameOver, field);
                    if (label == null || string.IsNullOrWhiteSpace(label.text)) throw new InvalidOperationException("Victory field missing: " + field);
                    CheckTranslated(label.text, code, "victory/" + field);
                    checks++;
                }
                string unknown = GameOverPresentationRequest.ResolveLocationName("__unknown_scene_for_localization_probe__");
                if (unknown != GameText.Get("location.unknown", null)) throw new InvalidOperationException("Unknown scene identifier leaked into UI.");
                checks += 2;
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(encyclopedia);
            PrefabUtility.UnloadPrefabContents(ui);
            UnityEngine.Random.state = originalRandom;
            UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale = originalLocale;
        }
        Debug.Log("[FlowLocalizationValidation] Passed " + checks + " projections; 72 authored fields, speech RNG and cached death phrase preserved.");
    }

    private const System.Reflection.BindingFlags ProbeFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
    private static void SetProbeField(object target, string name, object value) => target.GetType().GetField(name, ProbeFlags).SetValue(target, value);
    private static object GetProbeField(object target, string name) => target.GetType().GetField(name, ProbeFlags).GetValue(target);
    private static void InvokeProbe(object target, string name) => target.GetType().GetMethod(name, ProbeFlags).Invoke(target, null);

    private static void CheckTranslated(string value, string locale, string owner)
    {
        if (locale == "ko" || string.IsNullOrWhiteSpace(value)) return;
        // Stable glossary IDs are deliberately retained inside TMP tags.
        string displayed = Regex.Replace(value, "<[^>]*>", "");
        if (Regex.IsMatch(displayed, "[가-힣]"))
            throw new InvalidOperationException("Untranslated visible caption: " + owner + "/" + locale + ": " + displayed);
    }

    public static void BuildAddressablesBatch()
    {
        try
        {
            UnityEditor.AddressableAssets.Settings.AddressableAssetSettings.BuildPlayerContent(out var result);
            if (!string.IsNullOrEmpty(result.Error)) throw new InvalidOperationException(result.Error);
            Debug.Log("[FullLocalizationValidation] Addressables content build passed.");
            EditorApplication.delayCall += () => EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorApplication.delayCall += () => EditorApplication.Exit(1);
        }
    }
}
