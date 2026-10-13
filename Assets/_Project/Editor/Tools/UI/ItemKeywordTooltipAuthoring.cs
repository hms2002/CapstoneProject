#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Scoped live Editor authoring and disposable validation for item keyword cards.</summary>
[InitializeOnLoad]
public static class ItemKeywordTooltipAuthoring
{
    public const string PrefabPath = "Assets/_Project/Prefabs/UI/GlobalUIRoot.prefab";
    public const string AbilityPrefabPath = "Assets/_Project/Prefabs/UI/PopupUI/PlayerWeaponInfoUI/Panel_AbilityBlock.prefab";
    const string PanelPath = "HoverCanvas/ItemDetailPanel";
    const string Marker = "Temp/ItemKeywordTooltip.runonce";
    const string ReportPath = "Library/CodexItemKeywords/authoring-report.json";
    const string FontPath = "Assets/_Project/Art/Font/Galmuri9 SDF.asset";
    const string FontKey = "font.698a407b32ed1874bb4cb7324dfddce2";
    const float CardWidth = 400f;
    static readonly string[] Keys = { "term.감전", "ui.item_keyword.electrocuted.description", "ui.item_keyword.burn.description" };
    static readonly string[] Codes = { "ko", "en", "ja", "zh-Hans", "zh-Hant" };

    [Serializable] public sealed class Report
    {
        public string operation, status, error, activeScene, scenePath, locale, screenshot;
        public bool playing, compiling, sceneDirty;
        public List<string> checks = new();
    }

    static ItemKeywordTooltipAuthoring() => EditorApplication.update += Poll;

    public static void ApplyStyleBatch()
    {
        Report report = Status("approved-skill-layout");
        try
        {
            AuthorAbilityFrame(report);
            AuthorStyle(report);
            ValidatePreview(report);
            GalaronTooltipPreviewCapture.Capture();
            report.status = "PASS"; WriteReport(report);
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            report.status = "FAIL"; report.error = error.ToString(); WriteReport(report); Debug.LogException(error);
            if (Application.isBatchMode) EditorApplication.Exit(1); else throw;
        }
    }

    public static void BuildBatch()
    {
        Report report = Status("author-and-preview");
        try
        {
            Author(report); ValidatePreview(report); report.status = "PASS"; WriteReport(report);
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            report.status = "FAIL"; report.error = error.ToString(); WriteReport(report); Debug.LogException(error);
            if (Application.isBatchMode) EditorApplication.Exit(1); else throw;
        }
    }

    static void Poll()
    {
        if (!File.Exists(Marker) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        string operation = File.ReadAllText(Marker).Trim();
        if (operation != "status" && EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Marker);
        Report report = Status(operation);
        try
        {
            switch (operation)
            {
                case "status": break;
                case "author": Author(report); break;
                case "preview": ValidatePreview(report); break;
                default: throw new ArgumentException("Expected status, author, or preview marker contents.");
            }
            report.status = "PASS";
        }
        catch (Exception error) { report.status = "FAIL"; report.error = error.ToString(); Debug.LogException(error); }
        WriteReport(report);
    }

    public static Report Status(string operation = "status")
    {
        Scene scene = SceneManager.GetActiveScene();
        return new Report { operation = operation, activeScene = scene.name, scenePath = scene.path,
            playing = EditorApplication.isPlaying, compiling = EditorApplication.isCompiling,
            sceneDirty = scene.isDirty, locale = LocalizationSettings.SelectedLocale?.Identifier.Code };
    }

    public static void Author(Report report)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Author only outside Play Mode.");
        Scene originalScene = SceneManager.GetActiveScene();
        bool dirty = originalScene.isDirty;
        ImportKeywordRows(report);
        GameObject persistent = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Require(persistent != null, "Main GlobalUIRoot prefab exists.", report);
        var preservation = UiPrefabPresentationPreservation.Capture(persistent, PanelPath);
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            preservation.BindLoadedRoot(root);
            ItemDetailPanel panel = root.transform.Find(PanelPath)?.GetComponent<ItemDetailPanel>();
            Require(panel != null, "Authored main item detail panel exists.", report);
            ItemKeywordTooltipView view = panel.GetComponent<ItemKeywordTooltipView>();
            if (view == null)
            {
                view = panel.gameObject.AddComponent<ItemKeywordTooltipView>();
                TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
                Require(font != null, "Existing Galmuri9 font exists.", report);
                RectTransform stack = Rect("KeywordTooltipStack", panel.transform, new Vector2(CardWidth, 0));
                stack.anchorMin = stack.anchorMax = Vector2.one; stack.pivot = new Vector2(0, 1);
                stack.anchoredPosition = new Vector2(16, 0);
                stack.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                VerticalLayoutGroup layout = Vertical(stack, new RectOffset(), 12);
                layout.childControlWidth = true; layout.childControlHeight = true;
                Fitter(stack);
                var shock = Card("ElectrocutedCard", stack, panel.transform, font, new Color(0.45f, 0.85f, 1f));
                var burn = Card("BurnCard", stack, panel.transform, font, new Color(1f, 0.59f, 0.30f));
                var serialized = new SerializedObject(view);
                Set(serialized, "stackRoot", stack); Set(serialized, "electrocutedCard", shock.root); Set(serialized, "burnCard", burn.root);
                Set(serialized, "electrocutedTitle", shock.title); Set(serialized, "electrocutedBody", shock.body);
                Set(serialized, "burnTitle", burn.title); Set(serialized, "burnBody", burn.body);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                shock.root.gameObject.SetActive(false); burn.root.gameObject.SetActive(false); stack.gameObject.SetActive(false);
                report.checks.Add("Two keyword cards authored once; no runtime hierarchy construction.");
            }
            ApplyApprovedStyle(panel);
            var panelData = new SerializedObject(panel);
            Set(panelData, "keywordTooltips", view); panelData.ApplyModifiedPropertiesWithoutUndo();
            ValidateReferences(panel, report);
            preservation.RestoreBeforeSave();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            report.checks.Add("Scoped prefab saved with existing TMP/material/scrollbar state preserved.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        Require(SceneManager.GetActiveScene() == originalScene && originalScene.isDirty == dirty,
            "Active scene and its dirty state retained; no scene saved or switched.", report);
    }

    // Style-only entry point deliberately avoids localization table imports.
    static void AuthorAbilityFrame(Report report)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Author only outside Play Mode.");
        GameObject persistent = AssetDatabase.LoadAssetAtPath<GameObject>(AbilityPrefabPath);
        Require(persistent != null, "Existing shared weapon skill prefab exists.", report);
        var preservation = UiPrefabPresentationPreservation.Capture(persistent, "CardMotionRoot");
        GameObject root = PrefabUtility.LoadPrefabContents(AbilityPrefabPath);
        try
        {
            preservation.BindLoadedRoot(root);
            RectTransform motion = root.transform.Find("CardMotionRoot") as RectTransform;
            // The existing fixed-width header/body already supply preferred width.
            // Fit that width so the authored frame follows the whole motion root.
            motion.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            ThinFrame(motion, Color.white);
            RectTransform frame = motion.Find("ThinFrame") as RectTransform;
            frame.offsetMin = new Vector2(-8, -2); frame.offsetMax = new Vector2(36, 0);
            ValidateThinFrame(motion, Color.white, report, new Vector2(-8, -2), new Vector2(36, 0));
            preservation.RestoreBeforeSave();
            PrefabUtility.SaveAsPrefabAsset(root, AbilityPrefabPath);
            report.checks.Add("Full skill frame authored under the existing motion root; text and serialized references retained.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // Style-only entry point deliberately avoids localization table imports.
    static void AuthorStyle(Report report)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Author only outside Play Mode.");
        Scene originalScene = SceneManager.GetActiveScene(); bool dirty = originalScene.isDirty;
        GameObject persistent = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Require(persistent != null, "Main GlobalUIRoot prefab exists.", report);
        var preservation = UiPrefabPresentationPreservation.Capture(persistent, PanelPath);
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            preservation.BindLoadedRoot(root);
            ItemDetailPanel panel = root.transform.Find(PanelPath)?.GetComponent<ItemDetailPanel>();
            Require(panel != null && panel.GetComponent<ItemKeywordTooltipView>() != null,
                "Existing item detail panel and both keyword cards are authored.", report);
            ApplyApprovedStyle(panel);
            ValidateReferences(panel, report);
            preservation.RestoreBeforeSave();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            report.checks.Add("Only the approved item tooltip subtree saved; existing TMP/material/scrollbar state preserved.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        Require(SceneManager.GetActiveScene() == originalScene && originalScene.isDirty == dirty,
            "Style authoring retained the active scene and dirty state.", report);
    }

    // Used on loaded prefab contents or disposable preview clones, never at runtime.
    public static void ApplyApprovedStyle(ItemDetailPanel panel)
    {
        Transform body = panel.transform.Find("Body_parent");
        panel.transform.Find("UpperFrame").gameObject.SetActive(false);
        panel.transform.Find("LowerFrame").gameObject.SetActive(false);
        body.Find("LeftEdgeHolder").gameObject.SetActive(false);
        body.Find("RightEdgeHolder").gameObject.SetActive(false);
        RectTransform content = body.Find("ContentPanel") as RectTransform;
        VerticalLayoutGroup contentLayout = content.GetComponent<VerticalLayoutGroup>();
        contentLayout.padding = new RectOffset(24, 24, 22, 22); contentLayout.spacing = 12f;
        body.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
        content.GetComponent<LayoutElement>().flexibleWidth = 1f;
        UnityEngine.UI.Image background = panel.GetComponent<UnityEngine.UI.Image>();
        background.sprite = null; background.type = UnityEngine.UI.Image.Type.Simple;
        background.color = new Color(0.075f, 0.10f, 0.15f, 0.98f);
        ThinFrame(panel.Rect, Color.white);
        StyleCard(panel.transform.Find("KeywordTooltipStack/ElectrocutedCard") as RectTransform,
            new Color(0.46f, 0.65f, 0.73f, 0.85f));
        StyleCard(panel.transform.Find("KeywordTooltipStack/BurnCard") as RectTransform,
            new Color(0.73f, 0.53f, 0.40f, 0.85f));
        RectTransform hint = new SerializedObject(panel).FindProperty("weaponDescriptionHint.root").objectReferenceValue is GameObject hintRoot
            ? hintRoot.transform as RectTransform : throw new InvalidOperationException("Authored description hint is missing.");
        hint.SetParent(panel.transform, false);
        hint.anchorMin = hint.anchorMax = hint.pivot = Vector2.one;
        hint.anchoredPosition = new Vector2(-24, -8);
        hint.GetComponent<LayoutElement>().ignoreLayout = true;
        // Only this gap separates weapon stats from the skill group.
        content.Find("Content").GetComponent<VerticalLayoutGroup>().spacing = 15f;
        // The frame extends two units below each card, leaving a ten-unit gap.
        content.Find("Content/AbilityBlockContainer").GetComponent<VerticalLayoutGroup>().spacing = 12f;
    }

    static void StyleCard(RectTransform card, Color color)
    {
        card.Find("Frame").gameObject.SetActive(false);
        VerticalLayoutGroup layout = card.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(22, 22, 20, 24); layout.spacing = 10f;
        UnityEngine.UI.Image background = card.GetComponent<UnityEngine.UI.Image>();
        background.sprite = null; background.type = UnityEngine.UI.Image.Type.Simple;
        ThinFrame(card, color);
    }

    static void ThinFrame(RectTransform parent, Color color)
    {
        RectTransform frame = parent.Find("ThinFrame") as RectTransform;
        if (frame == null)
        {
            frame = Rect("ThinFrame", parent, Vector2.zero);
            frame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        }
        frame.anchorMin = Vector2.zero; frame.anchorMax = Vector2.one; frame.offsetMin = frame.offsetMax = Vector2.zero;
        string[] names = { "Top", "Bottom", "Left", "Right" };
        for (int i = 0; i < names.Length; i++)
        {
            RectTransform edge = frame.Find(names[i]) as RectTransform;
            if (edge == null)
            {
                edge = Rect(names[i], frame, Vector2.zero);
                edge.gameObject.AddComponent<UnityEngine.UI.Image>();
            }
            UnityEngine.UI.Image image = edge.GetComponent<UnityEngine.UI.Image>();
            image.color = color; image.raycastTarget = false;
            bool horizontal = i < 2;
            edge.anchorMin = horizontal ? new Vector2(0, i == 0 ? 1 : 0) : new Vector2(i == 2 ? 0 : 1, 0);
            edge.anchorMax = horizontal ? new Vector2(1, i == 0 ? 1 : 0) : new Vector2(i == 2 ? 0 : 1, 1);
            edge.pivot = horizontal ? new Vector2(0.5f, i == 0 ? 1 : 0) : new Vector2(i == 2 ? 0 : 1, 0.5f);
            edge.anchoredPosition = Vector2.zero;
            edge.sizeDelta = horizontal ? new Vector2(0, 2) : new Vector2(2, 0);
        }
    }

    static void ImportKeywordRows(Report report)
    {
        // Reuse the project's parser, including its multiline CSV and escaped-quote handling.
        MethodInfo parser = typeof(LocalizationSheetSetup).GetMethod("ReadRows", BindingFlags.Static | BindingFlags.NonPublic);
        if (parser == null) throw new MissingMethodException("LocalizationSheetSetup.ReadRows");
        using var reader = File.OpenText("DataSheets/Localization/GameText.csv");
        var rows = ((IEnumerable<string[]>)parser.Invoke(null, new object[] { reader })).ToList();
        Require(rows[0].SequenceEqual(new[] { "key" }.Concat(Codes)), "Existing CSV has the expected five locale columns.", report);
        var selected = rows.Skip(1).Where(row => Keys.Contains(row[0])).ToDictionary(row => row[0], StringComparer.Ordinal);
        Require(selected.Count == Keys.Length && selected.Values.All(row => row.Length == 6 && row.Skip(1).All(value => !string.IsNullOrWhiteSpace(value))),
            "Only the three approved keyword rows are complete.", report);
        StringTableCollection collection = AssetDatabase.LoadAssetAtPath<StringTableCollection>(
            "Assets/_Project/Data/Localization/GameText.asset");
        Require(collection != null, "Existing GameText table collection exists.", report);
        foreach (string code in Codes)
        {
            StringTable table = collection.StringTables.Single(value => value.LocaleIdentifier.Code == code);
            int index = Array.IndexOf(Codes, code) + 1;
            foreach (string key in Keys) table.AddEntry(key, selected[key][index]);
            EditorUtility.SetDirty(table); AssetDatabase.SaveAssetIfDirty(table);
            Require(Keys.All(key => table.GetEntry(key)?.LocalizedValue == selected[key][index]), "Three localized strings match CSV: " + code, report);
        }
        EditorUtility.SetDirty(collection.SharedData); AssetDatabase.SaveAssetIfDirty(collection.SharedData);
        report.checks.Add("Only five GameText tables and their shared keys saved; no global SaveAssets or full CSV import.");
    }

    static (RectTransform root, TMP_Text title, TMP_Text body) Card(string name, RectTransform stack, Transform sourcePanel, TMP_FontAsset font, Color accent)
    {
        RectTransform card = Rect(name, stack, new Vector2(CardWidth, 0));
        Image background = card.gameObject.AddComponent<Image>();
        Image source = sourcePanel.GetComponent<Image>();
        background.sprite = source.sprite; background.type = source.type;
        background.color = new Color(0.11f, 0.16f, 0.24f, 0.98f); background.raycastTarget = false;
        Vertical(card, new RectOffset(26, 26, 36, 40), 12);
        Frame(card, sourcePanel);
        TMP_Text title = Text("Title", card, font, 28, accent);
        RectTransform line = Rect("Divider", card, new Vector2(0, 2));
        Image divider = line.gameObject.AddComponent<Image>(); divider.color = accent; divider.raycastTarget = false;
        line.gameObject.AddComponent<LayoutElement>().preferredHeight = 2;
        TMP_Text body = Text("Description", card, font, 23.2f, new Color(0.94f, 0.96f, 1f));
        return (card, title, body);
    }

    static void Frame(RectTransform card, Transform sourcePanel)
    {
        RectTransform frame = Rect("Frame", card, Vector2.zero);
        frame.anchorMin = Vector2.zero; frame.anchorMax = Vector2.one; frame.offsetMin = frame.offsetMax = Vector2.zero;
        frame.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        Edge(frame, sourcePanel, "UpperFrame/Frame_UpLeft", "TopLeft", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(32, 32));
        Edge(frame, sourcePanel, "UpperFrame/Frame_UpRight", "TopRight", Vector2.one, Vector2.one, Vector2.one, Vector2.zero, new Vector2(32, 32));
        Edge(frame, sourcePanel, "LowerFrame/Frame_DownLeft", "BottomLeft", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(32, 32));
        Edge(frame, sourcePanel, "LowerFrame/Frame_DownRight", "BottomRight", new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), Vector2.zero, new Vector2(32, 32));
        Edge(frame, sourcePanel, "UpperFrame/Frame_UpMiddle", "Top", new Vector2(0, 1), Vector2.one, new Vector2(0.5f, 1), Vector2.zero, new Vector2(-64, 32));
        Edge(frame, sourcePanel, "LowerFrame/Frame_DownMiddle", "Bottom", Vector2.zero, new Vector2(1, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(-64, 20));
        Edge(frame, sourcePanel, "Body_parent/RightEdgeHolder/RightEdgeImage", "Left", Vector2.zero, new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(8, -64));
        Edge(frame, sourcePanel, "Body_parent/RightEdgeHolder/RightEdgeImage", "Right", new Vector2(1, 0), Vector2.one, new Vector2(1, 0.5f), Vector2.zero, new Vector2(8, -64));
    }

    static void Edge(RectTransform frame, Transform sourcePanel, string path, string name, Vector2 min, Vector2 max, Vector2 pivot, Vector2 position, Vector2 size)
    {
        Image template = sourcePanel.Find(path)?.GetComponent<Image>();
        if (template == null || template.sprite == null) throw new InvalidOperationException("Existing tooltip frame sprite missing: " + path);
        RectTransform rect = Rect(name, frame, size); rect.anchorMin = min; rect.anchorMax = max; rect.pivot = pivot; rect.anchoredPosition = position;
        Image image = rect.gameObject.AddComponent<Image>(); image.sprite = template.sprite; image.type = Image.Type.Simple;
        image.color = template.color; image.raycastTarget = false;
    }

    static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, float size, Color color)
    {
        RectTransform rect = Rect(name, parent, Vector2.zero);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font; text.fontSharedMaterial = font.material; text.fontSize = size; text.enableAutoSizing = false;
        text.alignment = TextAlignmentOptions.TopLeft; text.textWrappingMode = TextWrappingModes.Normal;
        text.color = color; text.raycastTarget = false; text.text = string.Empty;
        LocalizeTmpFontEvent localized = rect.gameObject.AddComponent<LocalizeTmpFontEvent>();
        localized.AssetReference = new LocalizedTmpFont { TableReference = "GameFonts", TableEntryReference = FontKey };
        localized.SetAuthoredMaterial(font.material);
        UnityEventTools.AddPersistentListener(localized.OnUpdateAsset, (UnityAction<TMP_FontAsset>)localized.ApplyLocalizedFont);
        localized.OnUpdateAsset.SetPersistentListenerState(0, UnityEventCallState.EditorAndRuntime);
        return text;
    }

    static void ValidateReferences(ItemDetailPanel panel, Report report)
    {
        var panelData = new SerializedObject(panel);
        var view = panelData.FindProperty("keywordTooltips").objectReferenceValue as ItemKeywordTooltipView;
        Require(view != null && view.transform == panel.transform, "Panel refers to its authored keyword view.", report);
        var serialized = new SerializedObject(view);
        foreach (string name in new[] { "stackRoot", "electrocutedCard", "burnCard", "electrocutedTitle", "electrocutedBody", "burnTitle", "burnBody" })
            Require(serialized.FindProperty(name)?.objectReferenceValue != null, "Authored reference: " + name, report);
        RectTransform stack = Get<RectTransform>(view, "stackRoot");
        Require(stack.parent == panel.transform && stack.anchorMin == Vector2.one && stack.anchorMax == Vector2.one && stack.pivot == new Vector2(0, 1) && stack.anchoredPosition == new Vector2(16, 0),
            "Stack sits outside the panel's top-right corner.", report);
        Require(stack.GetComponent<LayoutElement>().ignoreLayout && stack.GetComponent<VerticalLayoutGroup>() != null && stack.GetComponent<ContentSizeFitter>().verticalFit == ContentSizeFitter.FitMode.PreferredSize,
            "Stack excluded from item height and arranged vertically by preferred size.", report);
        Require(stack.GetComponentsInChildren<Graphic>(true).All(value => !value.raycastTarget), "All keyword graphics are nonblocking.", report);
        foreach (string field in new[] { "electrocutedCard", "burnCard" })
        {
            RectOffset padding = Get<RectTransform>(view, field).GetComponent<VerticalLayoutGroup>()?.padding;
            RectTransform card = Get<RectTransform>(view, field);
            Require(padding != null && padding.left == 22 && padding.right == 22 && padding.top == 20 && padding.bottom == 24 &&
                card.GetComponent<VerticalLayoutGroup>().spacing == 10 && !card.Find("Frame").gameObject.activeSelf,
                "Keyword text uses approved compact padding and spacing: " + field, report);
            ValidateThinFrame(card, field == "electrocutedCard" ? new Color(0.46f, 0.65f, 0.73f, 0.85f) : new Color(0.73f, 0.53f, 0.40f, 0.85f), report);
        }
        VerticalLayoutGroup contentLayout = panel.transform.Find("Body_parent/ContentPanel").GetComponent<VerticalLayoutGroup>();
        Require(contentLayout.padding.left == 24 && contentLayout.padding.right == 24 && contentLayout.padding.top == 22 &&
            contentLayout.padding.bottom == 22 && contentLayout.spacing == 12,
            "Weapon and relic content use approved compact padding and spacing.", report);
        Require(!panel.transform.Find("UpperFrame").gameObject.activeSelf && !panel.transform.Find("LowerFrame").gameObject.activeSelf &&
            !panel.transform.Find("Body_parent/LeftEdgeHolder").gameObject.activeSelf && !panel.transform.Find("Body_parent/RightEdgeHolder").gameObject.activeSelf,
            "Original decorative item frame retained inactive.", report);
        ValidateThinFrame(panel.Rect, Color.white, report);
        RectTransform hint = ((GameObject)panelData.FindProperty("weaponDescriptionHint.root").objectReferenceValue).transform as RectTransform;
        Require(hint.parent == panel.transform && hint.anchorMin == Vector2.one && hint.anchorMax == Vector2.one && hint.pivot == Vector2.one &&
            hint.anchoredPosition == new Vector2(-24, -8) && hint.GetComponent<LayoutElement>().ignoreLayout,
            "Description guide is authored inside the upper-right corner.", report);
        Transform content = panel.transform.Find("Body_parent/ContentPanel/Content");
        Require(content.GetComponent<VerticalLayoutGroup>().spacing == 15 &&
            content.Find("AbilityBlockContainer").GetComponent<VerticalLayoutGroup>().spacing == 12,
            "Accepted small stat-to-skill gap and separate skill frames retained.", report);
        Require(stack.GetComponentsInChildren<LocalizeStringEvent>(true).Length == 0 && stack.GetComponentsInChildren<LocalizeTmpFontEvent>(true).Length == 4,
            "Runtime view owns text; all four labels use existing locale font bindings.", report);
    }

    static void ValidateThinFrame(RectTransform owner, Color color, Report report, Vector2 offsetMin = default, Vector2 offsetMax = default)
    {
        RectTransform frame = owner.Find("ThinFrame") as RectTransform;
        Require(frame != null && frame.childCount == 4 && frame.GetComponent<LayoutElement>().ignoreLayout &&
            frame.anchorMin == Vector2.zero && frame.anchorMax == Vector2.one && frame.offsetMin == offsetMin && frame.offsetMax == offsetMax,
            "Authored thin frame stretches without affecting layout: " + owner.name, report);
        foreach (UnityEngine.UI.Image edge in frame.GetComponentsInChildren<UnityEngine.UI.Image>(true))
        {
            bool horizontal = edge.name == "Top" || edge.name == "Bottom";
            Require(!edge.raycastTarget && edge.color == color && edge.gameObject.layer == owner.gameObject.layer &&
                edge.rectTransform.sizeDelta == (horizontal ? new Vector2(0, 2) : new Vector2(2, 0)),
                "Two-pixel nonblocking border: " + owner.name + "/" + edge.name, report);
        }
    }

    public static void ValidatePreview(Report report)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Preview validation requires Edit Mode.");
        Scene originalScene = SceneManager.GetActiveScene(); bool originalDirty = originalScene.isDirty;
        Locale originalLocale = LocalizationSettings.SelectedLocale;
        Scene scene = EditorSceneManager.NewPreviewScene();
        RenderTexture texture = new(1600, 1000, 24); Texture2D pixels = null; RenderTexture previous = RenderTexture.active;
        try
        {
            Locale korean = LocalizationSettings.AvailableLocales.GetLocale("ko");
            Require(korean != null, "Korean locale exists for the native style preview.", report);
            LocalizationSettings.SelectedLocale = korean;
            GameObject root = new("Item keyword preview", typeof(RectTransform), typeof(Canvas));
            SceneManager.MoveGameObjectToScene(root, scene);
            Canvas canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            ((RectTransform)root.transform).sizeDelta = new Vector2(1600, 1000); root.transform.localScale = Vector3.one * 0.01f;
            GameObject cameraRoot = new("Keyword preview camera", typeof(Camera)); SceneManager.MoveGameObjectToScene(cameraRoot, scene);
            Camera camera = cameraRoot.GetComponent<Camera>(); camera.scene = scene; camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true; camera.orthographicSize = 5; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.047f, 0.071f); camera.targetTexture = texture; canvas.worldCamera = camera;
            Transform persistent = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).transform.Find(PanelPath);
            GameObject copy = Object.Instantiate(persistent.gameObject, root.transform, false); copy.SetActive(true);
            ItemDetailPanel panel = copy.GetComponent<ItemDetailPanel>(); ValidateReferences(panel, report);
            var view = copy.GetComponent<ItemKeywordTooltipView>();
            foreach (LocalizeTmpFontEvent localized in copy.GetComponentsInChildren<LocalizeTmpFontEvent>(true)) localized.enabled = false;
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            foreach (TMP_Text text in copy.GetComponentsInChildren<TMP_Text>(true)) { text.font = font; text.fontSharedMaterial = font.material; text.text = string.Empty; }
            RectTransform rect = panel.Rect; rect.anchoredPosition = new Vector2(-215, 200);
            var panelData = new SerializedObject(panel);
            TMP_Text title = panelData.FindProperty("titleText").objectReferenceValue as TMP_Text;
            TMP_Text source = panelData.FindProperty("subtitleText").objectReferenceValue as TMP_Text;
            var stack = Get<RectTransform>(view, "stackRoot");
            var shock = Get<RectTransform>(view, "electrocutedCard"); var burn = Get<RectTransform>(view, "burnCard");
            source.text = "평범한 공격"; view.Refresh();
            Require(!stack.gameObject.activeSelf, "No keyword hides all companion cards.", report);
            source.text = "감전 감전"; view.Refresh();
            Require(shock.gameObject.activeSelf && !burn.gameObject.activeSelf && stack.childCount == 2, "Plain Korean shock deduplicates to one card.", report);
            source.text = "<link=\"glossary:화상\">Burn</link>"; view.Refresh();
            Require(!shock.gameObject.activeSelf && burn.gameObject.activeSelf, "Localized glossary link resolves burn by stable identity.", report);
            source.text = "<link=\"glossary:전기 속성\">Electric</link> <link=\"glossary:화상 피해\">Burn damage</link>"; view.Refresh();
            Require(shock.gameObject.activeSelf && burn.gameObject.activeSelf, "Both effect aliases show the two authored cards.", report);
            title.text = "감전 화상"; title.gameObject.SetActive(false); source.text = "평범한 공격"; view.Refresh();
            Require(!stack.gameObject.activeSelf, "Hidden old text cannot keep keyword cards open.", report);
            title.gameObject.SetActive(true); title.text = "뇌창 갈라론";
            source.text = "<link=\"glossary:감전\">감전</link>과 <link=\"glossary:화상\">화상</link> 효과를 설명하는 견본"; view.Refresh();
            Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(rect); LayoutRebuilder.ForceRebuildLayoutImmediate(stack);
            Require(shock.rect.height > 50 && burn.rect.height > 50 && shock.anchoredPosition.y > burn.anchoredPosition.y, "Cards size to content and stack downward.", report);
            Rect bounds = view.GetLocalBounds(); Require(bounds.xMax > rect.rect.xMax + 390, "Combined hover bounds include the outside stack.", report);
            ValidateHoverEdges(canvas, panel, view, report);
            foreach (TMP_Text text in stack.GetComponentsInChildren<TMP_Text>()) { text.ForceMeshUpdate(); Require(!text.isTextOverflowing, "No keyword text overflow: " + text.name, report); }
            Require(Get<TMP_Text>(view, "electrocutedTitle").text == GameText.Get("term.감전", "감전") && Get<TMP_Text>(view, "burnTitle").text == GameText.Get("term.화상", "화상"), "Keyword titles follow the selected locale.", report);
            camera.Render(); RenderTexture.active = texture;
            pixels = new Texture2D(1600, 1000, TextureFormat.RGB24, false); pixels.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0); pixels.Apply();
            report.screenshot = Path.GetFullPath("Library/CodexItemKeywords/preview.png");
            Directory.CreateDirectory(Path.GetDirectoryName(report.screenshot));
            File.WriteAllBytes(report.screenshot, pixels.EncodeToPNG());
            view.Clear(); Require(!stack.gameObject.activeSelf && !shock.gameObject.activeSelf && !burn.gameObject.activeSelf, "Clear hides all authored keyword cards.", report);
        }
        finally
        {
            RenderTexture.active = previous; if (pixels != null) Object.DestroyImmediate(pixels);
            LocalizationSettings.SelectedLocale = originalLocale;
            EditorSceneManager.ClosePreviewScene(scene); texture.Release(); Object.DestroyImmediate(texture);
        }
        Require(SceneManager.GetActiveScene() == originalScene && originalScene.isDirty == originalDirty, "Preview did not switch, save, or dirty the active scene.", report);
    }

    static void ValidateHoverEdges(Canvas canvas, ItemDetailPanel panel, ItemKeywordTooltipView view, Report report)
    {
        var canvasRect = (RectTransform)canvas.transform;
        Rect initialBounds = view.GetLocalBounds();
        Require(initialBounds.width < canvasRect.rect.width - 16 && initialBounds.height < canvasRect.rect.height - 16,
            "Combined tooltip fits the 1600 by 1000 preview canvas before edge validation.", report);
        RectTransform controllerRoot = Rect("HoverPositionValidation", canvas.transform, Vector2.zero);
        HoverUIController controller = controllerRoot.gameObject.AddComponent<HoverUIController>();
        var serialized = new SerializedObject(controller); Set(serialized, "canvas", canvas); serialized.ApplyModifiedPropertiesWithoutUndo();
        const BindingFlags members = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(HoverUIController).GetField("_currentView", members).SetValue(controller, new PreviewHover(panel.Rect, view));
        MethodInfo position = typeof(HoverUIController).GetMethod("PositionNextToTarget", members);
        if (position == null) throw new MissingMethodException("HoverUIController.PositionNextToTarget");
        RectTransform target = Rect("HoverEdgeTarget", canvas.transform, new Vector2(48, 48));
        Vector2 originalPosition = panel.Rect.anchoredPosition;
        try
        {
            foreach (Vector2 targetPosition in new[] { Vector2.zero, new Vector2(-740, 0), new Vector2(740, 0),
                new Vector2(0, 430), new Vector2(0, -430), new Vector2(-740, 430), new Vector2(740, 430),
                new Vector2(-740, -430), new Vector2(740, -430) })
            {
                target.anchoredPosition = targetPosition; position.Invoke(controller, new object[] { target });
                Rect local = view.GetLocalBounds();
                Vector3 min = canvasRect.InverseTransformPoint(panel.Rect.TransformPoint(new Vector3(local.xMin, local.yMin, 0)));
                Vector3 max = canvasRect.InverseTransformPoint(panel.Rect.TransformPoint(new Vector3(local.xMax, local.yMax, 0)));
                const float tolerance = 0.5f;
                Require(min.x >= canvasRect.rect.xMin + 8 - tolerance && min.y >= canvasRect.rect.yMin + 8 - tolerance &&
                    max.x <= canvasRect.rect.xMax - 8 + tolerance && max.y <= canvasRect.rect.yMax - 8 + tolerance,
                    "Actual hover controller keeps panel and both cards inside the padded canvas at " + targetPosition, report);
            }
        }
        finally
        {
            panel.Rect.anchoredPosition = originalPosition;
            Object.DestroyImmediate(target.gameObject); Object.DestroyImmediate(controllerRoot.gameObject);
        }
    }

    sealed class PreviewHover : IHoverView, IHoverBoundsProvider
    {
        readonly RectTransform rect;
        readonly ItemKeywordTooltipView view;
        public PreviewHover(RectTransform rect, ItemKeywordTooltipView view) { this.rect = rect; this.view = view; }
        public RectTransform Rect => rect;
        public bool IsActive => true;
        public Rect GetHoverLocalBounds() => view.GetLocalBounds();
        public void OpenUI() { }
        public void CloseUI() { }
        public void ShowHover(object data, object context = null) { }
        public void HideHover() { }
    }

    static RectTransform Rect(string name, Transform parent, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.gameObject.layer = parent.gameObject.layer;
        rect.SetParent(parent, false); rect.sizeDelta = size; return rect;
    }
    static VerticalLayoutGroup Vertical(RectTransform rect, RectOffset padding, float spacing)
    {
        var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>(); layout.padding = padding; layout.spacing = spacing;
        layout.childAlignment = TextAnchor.UpperLeft; layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false; return layout;
    }
    static void Fitter(RectTransform rect)
    {
        var fitter = rect.gameObject.AddComponent<ContentSizeFitter>(); fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }
    static void Set(SerializedObject serialized, string name, Object value)
    {
        SerializedProperty property = serialized.FindProperty(name);
        if (property == null) throw new MissingFieldException(serialized.targetObject.GetType().Name, name);
        property.objectReferenceValue = value;
    }
    static T Get<T>(Object owner, string field) where T : Object => (T)new SerializedObject(owner).FindProperty(field).objectReferenceValue;
    static void Require(bool condition, string check, Report report)
    {
        if (!condition) throw new InvalidOperationException(check); report.checks.Add(check);
    }
    static void WriteReport(Report report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
        Debug.Log("[ItemKeywordTooltip] " + report.operation + ": " + report.status + ", " + report.checks.Count + " checks.");
    }
}
#endif
