using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.TextCore.LowLevel;

internal static class FullLocalizationAuthoring
{
    [Serializable] private sealed class Row { public string key, path, owner; }
    [Serializable] private sealed class Document { public List<Row> rows; }
    private const string Folder = "Assets/_Project/Data/Localization";
    private static readonly Dictionary<string, TMP_FontAsset> Fonts = new();
    private static UnityEditor.Localization.AssetTableCollection fontTables;
    private static Dictionary<string, string> bindings;
    private static int textBindings, fontBindings;
    private static bool validateAfterAuthoring;
    [Serializable] private sealed class StyleRow { public string path, owner, guid; public long localId; }
    [Serializable] private sealed class StyleDocument { public List<StyleRow> rows; }
    private static Dictionary<string, StyleRow> originalStyles;

    internal static readonly string[] FlowChestPaths = {
        "Assets/_Project/Prefabs/Items/Chests/KillLockTreasuteChest 1.prefab",
        "Assets/_Project/Prefabs/Items/Chests/KillLockTreasuteChest.prefab",
        "Assets/_Project/Prefabs/Items/Chests/KillLockTresureChest.prefab",
        "Assets/_Project/Prefabs/Items/Chests/LowRewardKillLockTreasureChest.prefab"
    };

    public static void ApplyFlowBindingsBatch()
    {
        try
        {
            fontTables = AssetDatabase.LoadAssetAtPath<AssetTableCollection>(Folder + "/GameFonts.asset");
            if (fontTables == null) throw new InvalidOperationException("Existing font collection required.");
            foreach (var pair in new[] { ("ja", "jp"), ("zh-Hans", "sc"), ("zh-Hant", "tc") })
                Fonts[pair.Item1] = EnsureFont(pair.Item2);
            bindings = new Dictionary<string, string>(); // Runtime count remains owned by the chest view.
            foreach (string path in FlowChestPaths)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (root.GetComponentInChildren<ChestMonsterKillLockView>(true) == null)
                        throw new InvalidOperationException("Chest view missing: " + path);
                    Bind(path, root.GetComponentsInChildren<TMP_Text>(true));
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            EditorUtility.SetDirty(fontTables);
            EditorUtility.SetDirty(fontTables.SharedData);
            AssetDatabase.SaveAssets();
            Debug.Log("[FlowLocalization] Chest font bindings authored: " + fontBindings);
            FullLocalizationValidation.RunAndBuildBatch();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorApplication.delayCall += () => EditorApplication.Exit(1);
        }
    }

    public static void ApplyAbilityTitleFitBatch()
    {
        try
        {
            int applied = 0;
            foreach (string path in new[] {
                "Assets/_Project/Prefabs/UI/PopupUI/PlayerWeaponInfoUI/Panel_AbilityBlock.prefab",
                "Assets/_Project/Prefabs/UI/PopupUI/Encyclopedia/Panel_AbilityBlock_Encyclopedia.prefab",
                "Assets/_Project/Prefabs/UI/PopupUI/PlayerStatUI/PlayerStatSectionView.prefab",
                "Assets/_Project/Prefabs/UI/PopupUI/PlayerStatUI/PlayerStatRowView.prefab",
                "Assets/_Project/Prefabs/UI/GlobalUIRoot.prefab",
                "Assets/_Project/Prefabs/UI/PopupUI/Encyclopedia/EncyclopediaUI.prefab" })
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (var block in root.GetComponentsInChildren<WeaponAbilityBlockView>(true))
                    {
                        var serialized = new SerializedObject(block);
                        foreach (string field in new[] { "titleText", "nextTitleText" })
                        {
                            var title = serialized.FindProperty(field).objectReferenceValue as TMP_Text;
                            if (title == null) continue;
                            title.fontSizeMax = title.fontSize;
                            title.fontSizeMin = title.fontSize * 0.75f;
                            title.enableAutoSizing = true;
                            title.textWrappingMode = TextWrappingModes.NoWrap;
                            EditorUtility.SetDirty(title);
                            applied++;
                        }
                    }
                    foreach (var section in root.GetComponentsInChildren<PlayerStatSectionView>(true))
                    {
                        var title = new SerializedObject(section).FindProperty("titleText").objectReferenceValue as TMP_Text;
                        if (title == null) continue;
                        title.fontSizeMax = title.fontSize;
                        title.fontSizeMin = title.fontSize * 0.75f;
                        title.enableAutoSizing = true;
                        title.textWrappingMode = TextWrappingModes.NoWrap;
                        EditorUtility.SetDirty(title);
                        var rowRoot = new SerializedObject(section).FindProperty("rowRoot").objectReferenceValue as Transform;
                        var layout = rowRoot.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
                        if (layout == null) throw new InvalidOperationException("Stat row layout missing.");
                        layout.childControlWidth = true;
                        EditorUtility.SetDirty(layout);
                        applied++;
                    }
                    foreach (var row in root.GetComponentsInChildren<PlayerStatRowView>(true))
                    {
                        var title = new SerializedObject(row).FindProperty("labelText").objectReferenceValue as TMP_Text;
                        title.fontSizeMax = title.fontSize;
                        title.fontSizeMin = title.fontSize * 0.75f;
                        title.enableAutoSizing = true;
                        title.textWrappingMode = TextWrappingModes.NoWrap;
                        var fitter = row.GetComponent<UnityEngine.UI.ContentSizeFitter>();
                        fitter.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.Unconstrained;
                        EditorUtility.SetDirty(fitter);
                        EditorUtility.SetDirty(title);
                        applied++;
                    }
                    foreach (var presenter in root.GetComponentsInChildren<MonoBehaviour>(true).Where(x => x is ItemDetailPanel || x is EncyclopediaDetailPanel))
                    {
                        var hint = new SerializedObject(presenter).FindProperty("weaponDescriptionHint");
                        var label = hint.FindPropertyRelative("label").objectReferenceValue as TMP_Text;
                        if (label == null) throw new InvalidOperationException("Authored description hint missing.");
                        label.fontSizeMax = label.fontSize;
                        label.fontSizeMin = label.fontSize * 0.75f;
                        label.enableAutoSizing = true;
                        label.textWrappingMode = TextWrappingModes.NoWrap;
                        EditorUtility.SetDirty(label);
                        applied++;
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            if (applied == 0) throw new InvalidOperationException("Ability title fields missing.");
            const string titlePath = "Assets/_Project/Scenes/TitleScene.unity";
            var slotOwners = JsonUtility.FromJson<Document>(File.ReadAllText("DataSheets/Localization/StaticUiBindings.json")).rows
                .Where(r => r.path == titlePath && r.key.StartsWith("title.profile.slot.")).Select(r => r.owner).ToHashSet();
            var scene = EditorSceneManager.OpenScene(titlePath, OpenSceneMode.Single);
            int slots = 0;
            foreach (var title in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TMP_Text>(true)))
            {
                if (!slotOwners.Contains(Hierarchy(title.transform))) continue;
                title.fontSizeMax = title.fontSize;
                title.fontSizeMin = title.fontSize * 0.75f;
                title.enableAutoSizing = true;
                title.textWrappingMode = TextWrappingModes.NoWrap;
                EditorUtility.SetDirty(title);
                slots++;
            }
            if (slots != 3) throw new InvalidOperationException("Three slot title labels required.");
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[AbilityTitleFit] Authored " + applied + " prefab titles and " + slots + " slot titles.");
            EditorApplication.delayCall += FullLocalizationValidation.RunAndBuildBatch;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorApplication.delayCall += () => EditorApplication.Exit(1);
        }
    }

    public static void ApplyJapanesePixelBatch()
    {
        try
        {
            const string path = Folder + "/Fonts/Galmuri/Galmuri9 Japanese SDF.asset";
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<Font>(Folder + "/Fonts/Galmuri/Galmuri9.ttf");
                if (source == null) throw new InvalidOperationException("Galmuri9 source missing.");
                // Galmuri9 has a 10px design grid: rasterize at an integer multiple.
                font = TMP_FontAsset.CreateFontAsset(source, 40, 5, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
                font.name = "Galmuri9 Japanese SDF";
                AssetDatabase.CreateAsset(font, path);
                font.material.name = font.name + " Material";
                AssetDatabase.AddObjectToAsset(font.material, font);
                foreach (var texture in font.atlasTextures) AssetDatabase.AddObjectToAsset(texture, font);
                var serialized = new SerializedObject(font);
                serialized.FindProperty("m_ClearDynamicDataOnBuild").boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            var style = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Art/Font/Galmuri9 SDF BlackOutline.asset").material;
            TMP_MaterialManager.CopyMaterialPresetProperties(style, font.material);
            var collection = AssetDatabase.LoadAssetAtPath<AssetTableCollection>(Folder + "/GameFonts.asset");
            var table = collection.AssetTables.First(t => t.LocaleIdentifier.Code == "ja");
            foreach (var entry in collection.SharedData.Entries.ToArray()) collection.AddAssetToTable(table, entry.Id, font);
            EditorUtility.SetDirty(font.material);
            EditorUtility.SetDirty(font);
            EditorUtility.SetDirty(table);
            EditorUtility.SetDirty(collection);
            EditorUtility.SetDirty(collection.SharedData);
            LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null, collection);
            AssetDatabase.SaveAssets();
            if (!AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>().Any()) throw new InvalidOperationException("Font material not persisted.");
            Debug.Log("[JapanesePixel] Updated " + table.Count + " locale font entries to " + font.name);
            FullLocalizationValidation.RunAndBuildBatch();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorApplication.delayCall += () => EditorApplication.Exit(1);
        }
    }

    public static void ApplyTitleSlotBindingsBatch()
    {
        try
        {
            const string path = "Assets/_Project/Scenes/TitleScene.unity";
            var rows = JsonUtility.FromJson<Document>(File.ReadAllText("DataSheets/Localization/StaticUiBindings.json")).rows;
            var slots = rows.Where(r => r.path == path && r.key.StartsWith("title.profile.slot.")).ToDictionary(r => r.owner, r => r.key);
            if (slots.Count != 3) throw new InvalidOperationException("Three title slot header bindings required.");
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int applied = 0;
            foreach (var text in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TMP_Text>(true)))
            {
                if (!slots.TryGetValue(Hierarchy(text.transform), out string key)) continue;
                var localized = text.GetComponent<LocalizeStringEvent>() ?? text.gameObject.AddComponent<LocalizeStringEvent>();
                localized.StringReference = new LocalizedString(GameText.TableName, key);
                for (int i = localized.OnUpdateString.GetPersistentEventCount() - 1; i >= 0; i--)
                    UnityEventTools.RemovePersistentListener(localized.OnUpdateString, i);
                var setter = (UnityAction<string>)Delegate.CreateDelegate(typeof(UnityAction<string>), text, "set_text");
                UnityEventTools.AddPersistentListener(localized.OnUpdateString, setter);
                localized.OnUpdateString.SetPersistentListenerState(0, UnityEventCallState.EditorAndRuntime);
                EditorUtility.SetDirty(localized);
                applied++;
            }
            if (applied != 3) throw new InvalidOperationException("Title slot headers not found.");
            foreach (var card in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TitleProfileSlotCardUI>(true)))
            {
                var serialized = new SerializedObject(card);
                foreach (string field in new[] { "stateLabelText", "playTimeTitleText", "playTimeValueText", "upgradeProgressTitleText", "upgradeProgressValueText", "magicStoneTitleText", "magicStoneValueText", "clearCountTitleText", "clearCountValueText", "selectButtonLabelText" })
                {
                    var label = serialized.FindProperty(field).objectReferenceValue as TMP_Text;
                    if (label == null) continue;
                    var fixedEvent = label.GetComponent<LocalizeStringEvent>();
                    if (fixedEvent != null) UnityEngine.Object.DestroyImmediate(fixedEvent);
                }
            }
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[TitleLocalization] Applied three fixed slot header keys; existing fonts/layout preserved.");
            FullLocalizationValidation.RunAndBuildBatch();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorApplication.delayCall += () => EditorApplication.Exit(1);
        }
    }

    public static void ApplyFusionChineseBatch()
    {
        try
        {
            var collection = AssetDatabase.LoadAssetAtPath<AssetTableCollection>(Folder + "/GameFonts.asset");
            var koreanStyle = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Art/Font/Galmuri9 SDF BlackOutline.asset").material;
            foreach (var pair in new[] { ("zh-Hans", "zh_hans"), ("zh-Hant", "zh_hant") })
            {
                string name = "fusion-pixel-12px-proportional-" + pair.Item2;
                string path = Folder + "/Fonts/FusionPixel/" + name + " SDF.asset";
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (font == null)
                {
                    var source = AssetDatabase.LoadAssetAtPath<Font>(Folder + "/Fonts/FusionPixel/" + name + ".ttf");
                    if (source == null) throw new InvalidOperationException("Fusion source missing: " + name);
                    font = TMP_FontAsset.CreateFontAsset(source, 48, 5, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
                    font.name = name + " SDF";
                    AssetDatabase.CreateAsset(font, path);
                    font.material.name = font.name + " Material";
                    AssetDatabase.AddObjectToAsset(font.material, font);
                    foreach (var texture in font.atlasTextures) AssetDatabase.AddObjectToAsset(texture, font);
                    var serialized = new SerializedObject(font);
                    serialized.FindProperty("m_ClearDynamicDataOnBuild").boolValue = true;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                TMP_MaterialManager.CopyMaterialPresetProperties(koreanStyle, font.material);
                EditorUtility.SetDirty(font.material);
                EditorUtility.SetDirty(font);
                var table = collection.AssetTables.First(t => t.LocaleIdentifier.Code == pair.Item1);
                foreach (var entry in collection.SharedData.Entries.ToArray())
                    collection.AddAssetToTable(table, entry.Id, font);
                EditorUtility.SetDirty(table);
                Debug.Log("[FusionChinese] " + pair.Item1 + ": " + font.name + "; " + table.Count + " font entries updated.");
            }
            EditorUtility.SetDirty(collection);
            LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null, collection);
            AssetDatabase.SaveAssets();
            FullLocalizationValidation.RunAndRenderBatch();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorApplication.delayCall += () => EditorApplication.Exit(1);
        }
    }

    public static void ApplyKoreanFontDefaultsBatch()
    {
        try
        {
            var source = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Art/Font/Galmuri9 SDF BlackOutline.asset");
            foreach (string region in new[] { "jp", "sc", "tc" })
            {
                var font = EnsureFont(region);
                TMP_MaterialManager.CopyMaterialPresetProperties(source.material, font.material);
                EditorUtility.SetDirty(font.material);
                Debug.Log("[FontDefaults] " + region + ": Korean shader/material settings copied; outline=" + font.material.GetFloat("_OutlineWidth"));
            }
            AssetDatabase.SaveAssets();
            FullLocalizationValidation.RunAndBuildBatch();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorApplication.delayCall += () => EditorApplication.Exit(1);
        }
    }

    public static void RepairOriginalStylesBatch()
    {
        originalStyles = JsonUtility.FromJson<StyleDocument>(File.ReadAllText("Temp/original-font-styles.json"))
            .rows.GroupBy(r => r.path + "::" + r.owner)
            .Where(g => g.Select(r => r.guid + "/" + r.localId).Distinct().Count() == 1)
            .ToDictionary(g => g.Key, g => g.First());
        RunAndValidateBatch();
    }

    public static void RunAndValidateBatch()
    {
        validateAfterAuthoring = true;
        RunBatch();
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch Editor.");
        try
        {
            LocalizationSheetSetup.Import();
            if (TMP_Settings.instance == null) throw new InvalidOperationException("TMP Essential Resources are missing.");
            foreach (var pair in new[] { ("ja", "jp"), ("zh-Hans", "sc"), ("zh-Hant", "tc") })
                Fonts[pair.Item1] = EnsureFont(pair.Item2);
            fontTables = LocalizationEditorSettings.GetAssetTableCollection("GameFonts") ??
                AssetDatabase.LoadAssetAtPath<AssetTableCollection>(Folder + "/GameFonts.asset") ??
                LocalizationEditorSettings.CreateAssetTableCollection("GameFonts", Folder);
            var document = JsonUtility.FromJson<Document>(File.ReadAllText("DataSheets/Localization/StaticUiBindings.json"));
            bindings = document.rows.ToDictionary(r => r.path + "::" + r.owner, r => r.key, StringComparer.Ordinal);
            var paths = document.rows.Select(r => r.path).Distinct().ToList();
            // All TMP labels need the locale font, including labels whose text is owned by a runtime controller.
            paths.AddRange(AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs/UI" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !p.Contains("GlobalUIRoot_") && !p.Contains("_Backup")));
            foreach (string path in paths.Distinct())
            {
                Debug.Log("[FullLocalization] Authoring " + path);
                if (path.EndsWith(".prefab", StringComparison.Ordinal))
                {
                    var root = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        Bind(path, root.GetComponentsInChildren<TMP_Text>(true));
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                    }
                    finally { PrefabUtility.UnloadPrefabContents(root); }
                }
                else
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    Bind(path, scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TMP_Text>(true)));
                    EditorSceneManager.SaveScene(scene);
                }
            }
            EditorUtility.SetDirty(fontTables);
            EditorUtility.SetDirty(fontTables.SharedData);
            LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null, fontTables);
            AssetDatabase.SaveAssets();
            Debug.Log($"[FullLocalization] Bound {textBindings} static texts and {fontBindings} TMP fonts.");
            if (validateAfterAuthoring) FullLocalizationValidation.RunBatch();
            else EditorApplication.delayCall += () => EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorApplication.delayCall += () => EditorApplication.Exit(1);
        }
    }

    private static TMP_FontAsset EnsureFont(string region)
    {
        if (region == "jp")
        {
            var japanese = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Folder + "/Fonts/Galmuri/Galmuri9 Japanese SDF.asset");
            if (japanese != null) return japanese;
        }
        if (region == "sc" || region == "tc")
        {
            string suffix = region == "sc" ? "zh_hans" : "zh_hant";
            var fusion = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Folder + "/Fonts/FusionPixel/fusion-pixel-12px-proportional-" + suffix + " SDF.asset");
            if (fusion != null) return fusion;
        }
        string path = Folder + "/Fonts/NotoSansCJK" + region + "-Regular SDF.asset";
        var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (asset != null) return asset;
        var source = AssetDatabase.LoadAssetAtPath<Font>(Folder + "/Fonts/NotoSansCJK" + region + "-Regular.otf");
        if (source == null) throw new InvalidOperationException("Missing Noto font: " + region);
        asset = TMP_FontAsset.CreateFontAsset(source, 48, 5, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
        asset.name = "NotoSansCJK" + region + "-Regular SDF";
        AssetDatabase.CreateAsset(asset, path);
        asset.material.name = asset.name + " Material";
        AssetDatabase.AddObjectToAsset(asset.material, asset);
        foreach (var texture in asset.atlasTextures) AssetDatabase.AddObjectToAsset(texture, asset);
        var serialized = new SerializedObject(asset);
        serialized.FindProperty("m_ClearDynamicDataOnBuild").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
        return asset;
    }

    private static void Bind(string path, IEnumerable<TMP_Text> texts)
    {
        foreach (var text in texts)
        {
            if (bindings.TryGetValue(path + "::" + Hierarchy(text.transform), out string key))
            {
                var localized = text.GetComponent<LocalizeStringEvent>() ?? text.gameObject.AddComponent<LocalizeStringEvent>();
                localized.StringReference = new LocalizedString(GameText.TableName, key);
                for (int i = localized.OnUpdateString.GetPersistentEventCount() - 1; i >= 0; i--)
                    UnityEventTools.RemovePersistentListener(localized.OnUpdateString, i);
                var setter = (UnityAction<string>)Delegate.CreateDelegate(typeof(UnityAction<string>), text, "set_text");
                UnityEventTools.AddPersistentListener(localized.OnUpdateString, setter);
                localized.OnUpdateString.SetPersistentListenerState(0, UnityEventCallState.EditorAndRuntime);
                EditorUtility.SetDirty(localized);
                if (PrefabUtility.IsPartOfPrefabInstance(localized))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(localized);
                textBindings++;
            }
            if (text.font == null) continue;
            var fontEvent = text.GetComponent<LocalizeTmpFontEvent>();
            // Inherited prefab bindings keep their original font key when a scene is processed.
            if (fontEvent != null && !fontEvent.AssetReference.IsEmpty)
            {
                BindFontStyle(fontEvent, text, path);
                continue;
            }
            string fontGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(text.font));
            if (string.IsNullOrEmpty(fontGuid)) throw new InvalidOperationException("Font has no GUID: " + path);
            string fontKey = "font." + fontGuid;
            foreach (var locale in LocalizationEditorSettings.GetLocales())
                fontTables.AddAssetToTable(locale.Identifier, fontKey,
                    Fonts.TryGetValue(locale.Identifier.Code, out var font) ? font : text.font);
            fontEvent ??= text.gameObject.AddComponent<LocalizeTmpFontEvent>();
            fontEvent.AssetReference = new LocalizedTmpFont { TableReference = "GameFonts", TableEntryReference = fontKey };
            for (int i = fontEvent.OnUpdateAsset.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(fontEvent.OnUpdateAsset, i);
            var fontSetter = (UnityAction<TMP_FontAsset>)fontEvent.ApplyLocalizedFont;
            UnityEventTools.AddPersistentListener(fontEvent.OnUpdateAsset, fontSetter);
            fontEvent.OnUpdateAsset.SetPersistentListenerState(0, UnityEventCallState.EditorAndRuntime);
            fontEvent.SetAuthoredMaterial(text.fontSharedMaterial);
            EditorUtility.SetDirty(fontEvent);
            if (PrefabUtility.IsPartOfPrefabInstance(fontEvent))
                PrefabUtility.RecordPrefabInstancePropertyModifications(fontEvent);
            fontBindings++;
            EditorUtility.SetDirty(text.gameObject);
        }
    }

    private static void BindFontStyle(LocalizeTmpFontEvent fontEvent, TMP_Text text, string path)
    {
        if (fontEvent.AuthoredMaterial == null) fontEvent.SetAuthoredMaterial(text.fontSharedMaterial);
        // Editor locale preview can serialize a CJK material. Never use it as the Korean style baseline.
        if (AssetDatabase.GetAssetPath(fontEvent.AuthoredMaterial).StartsWith(Folder + "/Fonts/", StringComparison.Ordinal) &&
            originalStyles != null && originalStyles.TryGetValue(path + "::" + Hierarchy(text.transform), out var style))
        {
            var material = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(style.guid))
                .OfType<Material>().FirstOrDefault(m => AssetDatabase.TryGetGUIDAndLocalFileIdentifier(m, out string _, out long id) && id == style.localId);
            if (material == null) throw new InvalidOperationException("Original font material missing: " + path + "/" + style.owner);
            fontEvent.SetAuthoredMaterial(material);
        }
        if (AssetDatabase.GetAssetPath(fontEvent.AuthoredMaterial).StartsWith(Folder + "/Fonts/", StringComparison.Ordinal))
        {
            var table = fontTables.AssetTables.First(t => t.LocaleIdentifier.Code == "ko");
            var entry = table.GetEntry(fontEvent.AssetReference.TableEntryReference.Key);
            var koreanFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(entry.Guid));
            fontEvent.SetAuthoredMaterial(koreanFont.material);
        }
        for (int i = fontEvent.OnUpdateAsset.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEventTools.RemovePersistentListener(fontEvent.OnUpdateAsset, i);
        UnityEventTools.AddPersistentListener(fontEvent.OnUpdateAsset, (UnityAction<TMP_FontAsset>)fontEvent.ApplyLocalizedFont);
        fontEvent.OnUpdateAsset.SetPersistentListenerState(0, UnityEventCallState.EditorAndRuntime);
        EditorUtility.SetDirty(fontEvent);
        if (PrefabUtility.IsPartOfPrefabInstance(fontEvent)) PrefabUtility.RecordPrefabInstancePropertyModifications(fontEvent);
    }

    private static string Hierarchy(Transform t) => t.parent == null ? t.name : Hierarchy(t.parent) + "/" + t.name;
}
