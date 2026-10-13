using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static partial class TestSceneBuilder
{
    [MenuItem("Tools/Testing/Update TestScene Catalog Grid")]
    public static void UpdateCatalogGrid()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before updating the test catalog UI.");
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            throw new InvalidOperationException("Open TestScene before updating its catalog UI.");
        if (scene.isDirty)
            throw new InvalidOperationException("Save your current TestScene edits before updating its catalog UI.");
        TestSceneControlPanel controls = scene.GetRootGameObjects()
            .SelectMany(x => x.GetComponentsInChildren<TestSceneControlPanel>(true)).Single();
        font = Load<TMP_FontAsset>("Assets/_Project/Art/Font/Galmuri9 SDF.asset");
        Transform content = controls.transform.Find("Test Panel/Viewport/Content");
        if (content == null) throw new InvalidOperationException("Test panel content is missing.");
        var data = new SerializedObject(controls);
        if (data.FindProperty("catalogGrid").objectReferenceValue != null)
        {
            var existingGrid = (TestSceneCatalogGrid)data.FindProperty("catalogGrid").objectReferenceValue;
            var existingData = new SerializedObject(existingGrid);
            RectTransform footer = (RectTransform)existingGrid.transform.Find("CatalogOverlay/CatalogWindow/Selection");
            if (existingData.FindProperty("feedbackText").objectReferenceValue == null) BuildGridFeedback(existingGrid, footer);
            RefreshActorLabels(existingData, "monsterCells", PrefabReferences(data, "mobPrefabs"));
            RefreshActorLabels(existingData, "bossCells", PrefabReferences(data, "bossPrefabs"));
            ValidateScene(); EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("TestScene UI save failed.");
            return;
        }

        // Replace only the four selectors and move their options into the central UI.
        // The arena, global UI and every other test command keep their existing objects.
        string[] oldButtons = { "선택 무기 지급", "선택 유물 지급 / 강화", "몬스터 생성 (수량 1–100)", "보스 생성" };
        string[] newButtons = { "무기 목록", "유물 목록", "몬스터 목록", "보스 목록" };
        string[] fields = { "openWeaponsButton", "openRelicsButton", "openMonstersButton", "openBossesButton" };
        Button[] buttons = oldButtons.Select(name => content.Cast<Transform>().SingleOrDefault(x => x.name == name)?.GetComponent<Button>()).ToArray();
        if (buttons.Any(x => x == null)) throw new InvalidOperationException("Expected original catalog buttons were not found.");
        for (int i = 0; i < buttons.Length; i++)
        {
            buttons[i].name = newButtons[i];
            buttons[i].GetComponentInChildren<TMP_Text>(true).text = newButtons[i];
            Set(controls, fields[i], buttons[i]);
        }
        BuildCatalogGrid(controls);
        foreach (string name in new[] { "Weapon", "Relic", "Monster", "Boss", "Weapon slot", "Spawn Count" })
        {
            Transform obsolete = content.Find(name);
            if (obsolete != null) UnityEngine.Object.DestroyImmediate(obsolete.gameObject);
        }
        ValidateScene();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("TestScene UI save failed.");
        Debug.Log("[TestScene] Central catalog grid authored; existing arena and other commands preserved.");
    }

    private static void BuildCatalogGrid(TestSceneControlPanel controls)
    {
        var data = new SerializedObject(controls);
        var database = (ItemDatabase)data.FindProperty("itemDatabase").objectReferenceValue;
        GameObject[] mobs = PrefabReferences(data, "mobPrefabs");
        GameObject[] bosses = PrefabReferences(data, "bossPrefabs");
        RectTransform host = Rect("CatalogHost", controls.transform);
        Stretch(host);
        var grid = host.gameObject.AddComponent<TestSceneCatalogGrid>();
        RectTransform overlay = Rect("CatalogOverlay", host);
        Stretch(overlay);
        overlay.gameObject.AddComponent<Image>().color = new Color(.012f, .020f, .035f, .72f);
        Set(grid, "panelRoot", overlay.gameObject);
        RectTransform window = Rect("CatalogWindow", overlay);
        window.anchorMin = window.anchorMax = new Vector2(.5f, .5f);
        window.sizeDelta = new Vector2(980, 780);
        window.gameObject.AddComponent<Image>().color = new Color(.035f, .055f, .08f, 1);

        RectTransform heading = Rect("Heading", window);
        heading.anchorMin = new Vector2(0, 1); heading.anchorMax = Vector2.one;
        heading.pivot = new Vector2(.5f, 1); heading.sizeDelta = new Vector2(-48, 44); heading.anchoredPosition = new Vector2(0, -16);
        TMP_Text title = Text("Title", heading, "테스트 대상 선택", 29);
        title.alignment = TextAlignmentOptions.MidlineLeft;
        Button close = Button(window, "닫기");
        Place((RectTransform)close.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -20), new Vector2(88, 36));
        Set(grid, "closeButton", close);

        RectTransform tabs = Rect("Categories", window);
        tabs.anchorMin = new Vector2(0, 1); tabs.anchorMax = Vector2.one;
        tabs.pivot = new Vector2(.5f, 1); tabs.sizeDelta = new Vector2(-48, 46); tabs.anchoredPosition = new Vector2(0, -72);
        var row = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 10; row.childControlHeight = true; row.childControlWidth = true;
        row.childForceExpandHeight = true; row.childForceExpandWidth = true;
        string[] names = { "무기", "유물", "몬스터", "보스" };
        Button[] categoryButtons = names.Select(name => Button(tabs, name)).ToArray();
        SetArray(grid, "categoryButtons", categoryButtons);
        GameObject[] roots = new GameObject[4];
        roots[0] = BuildCategory(window, "Weapons", database.allWeapons.Where(x => x != null)
            .Select(x => (x.displayName, x.icon)).ToArray(), grid, "weaponCells");
        roots[1] = BuildCategory(window, "Relics", database.allRelics.Where(x => x != null)
            .Select(x => (x.displayName, x.icon)).ToArray(), grid, "relicCells");
        roots[2] = BuildCategory(window, "Monsters", ActorEntries(mobs), grid, "monsterCells");
        roots[3] = BuildCategory(window, "Bosses", ActorEntries(bosses), grid, "bossCells");
        for (int i = 0; i < roots.Length; i++) roots[i].SetActive(i == 0);
        SetArray(grid, "categoryRoots", roots);

        RectTransform footer = Rect("Selection", window);
        footer.anchorMin = Vector2.zero; footer.anchorMax = new Vector2(1, 0);
        footer.pivot = new Vector2(.5f, 0); footer.sizeDelta = new Vector2(-48, 100); footer.anchoredPosition = new Vector2(0, 14);
        RectTransform selected = Rect("SelectedEntry", footer);
        selected.anchorMin = new Vector2(0, 1); selected.anchorMax = Vector2.one;
        selected.pivot = new Vector2(.5f, 1); selected.sizeDelta = new Vector2(-280, 34); selected.anchoredPosition = new Vector2(-140, 0);
        TMP_Text selectedLabel = Text("Label", selected, "아이콘을 선택하세요", 22);
        selectedLabel.alignment = TextAlignmentOptions.MidlineLeft;
        Set(grid, "selectionText", selectedLabel);
        BuildGridFeedback(grid, footer);

        RectTransform weaponOptions = Rect("WeaponOptions", footer);
        Place(weaponOptions, new Vector2(0, 0), Vector2.zero, new Vector2(0, 2), new Vector2(380, 42));
        TMP_Dropdown slots = Dropdown(weaponOptions, "Weapon slot");
        Stretch((RectTransform)slots.transform);
        slots.options = new System.Collections.Generic.List<TMP_Dropdown.OptionData> { new("슬롯 1"), new("슬롯 2") };
        Set(controls, "weaponSlotDropdown", slots); Set(grid, "weaponOptions", weaponOptions.gameObject);

        RectTransform monsterOptions = Rect("SpawnOptions", footer);
        Place(monsterOptions, Vector2.zero, Vector2.zero, new Vector2(0, 2), new Vector2(400, 42));
        TMP_Text countLabel = Text("CountLabel", monsterOptions, "생성 수량 (1–100)", 20);
        countLabel.rectTransform.anchorMax = new Vector2(.63f, 1);
        countLabel.alignment = TextAlignmentOptions.MidlineLeft;
        GameObject count = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
        count.name = "Spawn Count"; count.transform.SetParent(monsterOptions, false); ConfigureTexts(count);
        var countRect = (RectTransform)count.transform;
        countRect.anchorMin = new Vector2(.65f, 0); countRect.anchorMax = Vector2.one;
        countRect.offsetMin = countRect.offsetMax = Vector2.zero;
        var input = count.GetComponent<TMP_InputField>(); input.contentType = TMP_InputField.ContentType.IntegerNumber; input.text = "1";
        count.GetComponent<Image>().color = new Color(.12f, .17f, .24f);
        Set(controls, "spawnCountInput", input); Set(grid, "monsterOptions", monsterOptions.gameObject);
        monsterOptions.gameObject.SetActive(false);
        Button execute = Button(footer, "선택 무기 지급");
        Place((RectTransform)execute.transform, new Vector2(1, 0), new Vector2(1, 0), Vector2.zero, new Vector2(256, 48));
        Set(grid, "executeButton", execute); Set(grid, "executeLabel", execute.GetComponentInChildren<TMP_Text>(true));
        Set(controls, "catalogGrid", grid);
    }

    private static GameObject BuildCategory(Transform window, string name, (string label, Sprite icon)[] entries,
        TestSceneCatalogGrid catalog, string field)
    {
        RectTransform root = Rect(name, window);
        root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
        root.offsetMin = new Vector2(24, 120); root.offsetMax = new Vector2(-24, -132);
        var scroll = root.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 45;
        RectTransform viewport = Rect("Viewport", root);
        Stretch(viewport); viewport.offsetMax = new Vector2(-12, 0);
        viewport.gameObject.AddComponent<RectMask2D>();
        viewport.gameObject.AddComponent<Image>().color = new Color(.025f, .04f, .063f, 1);
        RectTransform content = Rect("Content", viewport);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
        content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero;
        var layout = content.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(144, 146); layout.spacing = new Vector2(10, 10);
        layout.padding = new RectOffset(2, 2, 2, 2);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount; layout.constraintCount = 6;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = content; scroll.viewport = viewport;
        RectTransform bar = Rect("Scrollbar", root);
        bar.anchorMin = new Vector2(1, 0); bar.anchorMax = Vector2.one;
        bar.pivot = new Vector2(1, .5f); bar.sizeDelta = new Vector2(6, 0);
        bar.gameObject.AddComponent<Image>().color = new Color(.09f, .14f, .2f);
        var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
        RectTransform handle = Rect("Handle", bar); Stretch(handle);
        handle.gameObject.AddComponent<Image>().color = new Color(.3f, .58f, .7f);
        scrollbar.handleRect = handle; scrollbar.targetGraphic = handle.GetComponent<Image>(); scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scroll.verticalScrollbar = scrollbar; scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        TestSceneCatalogCell[] cells = new TestSceneCatalogCell[entries.Length];
        for (int i = 0; i < entries.Length; i++)
        {
            (string label, Sprite icon) entry = entries[i];
            if (entry.icon == null) throw new InvalidOperationException("Catalog icon is missing: " + entry.label);
            RectTransform card = Rect($"{i:D2} {entry.label}", content);
            card.gameObject.AddComponent<Image>().color = new Color(.085f, .12f, .18f);
            var button = card.gameObject.AddComponent<Button>();
            RectTransform selection = Rect("Selected", card); Stretch(selection);
            var border = selection.gameObject.AddComponent<Image>();
            border.color = new Color(.15f, .6f, .8f, .4f); border.raycastTarget = false;
            border.enabled = i == 0;
            RectTransform iconRect = Rect("Icon", card);
            Place(iconRect, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -8), new Vector2(116, 88));
            var image = iconRect.gameObject.AddComponent<Image>(); image.sprite = entry.icon; image.preserveAspect = true; image.raycastTarget = false;
            RectTransform caption = Rect("Name", card);
            caption.anchorMin = Vector2.zero; caption.anchorMax = new Vector2(1, 0);
            caption.pivot = new Vector2(.5f, 0); caption.sizeDelta = new Vector2(-6, 46); caption.anchoredPosition = new Vector2(0, 4);
            TMP_Text label = Text("Label", caption, entry.label, 17); label.overflowMode = TextOverflowModes.Ellipsis;
            cells[i] = card.gameObject.AddComponent<TestSceneCatalogCell>();
            Set(cells[i], "button", button); Set(cells[i], "icon", image); Set(cells[i], "label", label); Set(cells[i], "selectionBorder", border);
        }
        SetArray(catalog, field, cells);
        return root.gameObject;
    }

    private static (string label, Sprite icon) ActorEntry(GameObject prefab)
    {
        Enemy enemy = prefab.GetComponent<Enemy>();
        var serialized = new SerializedObject(enemy);
        var body = serialized.FindProperty("sprite")?.objectReferenceValue as SpriteRenderer;
        if (body == null) body = prefab.GetComponent<SpriteRenderer>();
        if (body == null)
        {
            var animator = serialized.FindProperty("animator")?.objectReferenceValue as Animator;
            if (animator == null) animator = prefab.GetComponent<Animator>();
            if (animator != null) body = animator.GetComponentInChildren<SpriteRenderer>(true);
        }
        return (string.IsNullOrWhiteSpace(enemy.EnemyName) ? prefab.name : enemy.EnemyName, body != null ? body.sprite : null);
    }

    private static (string label, Sprite icon)[] ActorEntries(GameObject[] prefabs)
    {
        var entries = prefabs.Select(ActorEntry).ToArray();
        var duplicates = entries.GroupBy(x => x.label).Where(x => x.Count() > 1).Select(x => x.Key).ToHashSet();
        for (int i = 0; i < entries.Length; i++)
            if (duplicates.Contains(entries[i].label)) entries[i].label += "\n" + prefabs[i].name;
        return entries;
    }
    private static void RefreshActorLabels(SerializedObject grid, string field, GameObject[] prefabs)
    {
        var entries = ActorEntries(prefabs);
        SerializedProperty cells = grid.FindProperty(field);
        if (cells.arraySize != entries.Length) throw new InvalidOperationException("Catalog count changed. Rebuild TestScene or author matching cards first.");
        for (int i = 0; i < cells.arraySize; i++)
        {
            var cell = new SerializedObject(cells.GetArrayElementAtIndex(i).objectReferenceValue);
            ((TMP_Text)cell.FindProperty("label").objectReferenceValue).text = entries[i].label;
        }
    }
    private static void BuildGridFeedback(TestSceneCatalogGrid grid, RectTransform footer)
    {
        RectTransform feedback = Rect("Feedback", footer);
        feedback.anchorMin = Vector2.zero; feedback.anchorMax = new Vector2(1, 0);
        feedback.pivot = new Vector2(.5f, 0); feedback.sizeDelta = new Vector2(0, 20); feedback.anchoredPosition = new Vector2(0, 46);
        TMP_Text label = Text("Label", feedback, string.Empty, 16);
        label.alignment = TextAlignmentOptions.MidlineLeft; label.color = new Color(.6f, .82f, .9f);
        label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Ellipsis;
        Set(grid, "feedbackText", label);
    }

    private static GameObject[] PrefabReferences(SerializedObject data, string field)
    {
        SerializedProperty array = data.FindProperty(field);
        return Enumerable.Range(0, array.arraySize).Select(i => (GameObject)array.GetArrayElementAtIndex(i).objectReferenceValue).ToArray();
    }
    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
    private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot; rect.anchoredPosition = position; rect.sizeDelta = size;
    }
    private static void ValidateCatalogGrid(TestSceneControlPanel controls)
    {
        var data = new SerializedObject(controls);
        var grid = (TestSceneCatalogGrid)data.FindProperty("catalogGrid").objectReferenceValue;
        var database = (ItemDatabase)data.FindProperty("itemDatabase").objectReferenceValue;
        var serialized = new SerializedObject(grid);
        int[] counts = { database.allWeapons.Count(x => x != null), database.allRelics.Count(x => x != null),
            data.FindProperty("mobPrefabs").arraySize, data.FindProperty("bossPrefabs").arraySize };
        string[] fields = { "weaponCells", "relicCells", "monsterCells", "bossCells" };
        for (int i = 0; i < fields.Length; i++)
        {
            var cells = serialized.FindProperty(fields[i]);
            if (cells.arraySize != counts[i]) throw new InvalidOperationException("Catalog grid count mismatch: " + fields[i]);
            for (int j = 0; j < cells.arraySize; j++)
            {
                var cell = new SerializedObject(cells.GetArrayElementAtIndex(j).objectReferenceValue);
                foreach (string field in new[] { "button", "icon", "label", "selectionBorder" })
                    if (cell.FindProperty(field).objectReferenceValue == null) throw new InvalidOperationException("Missing catalog cell reference: " + field);
                var icon = (Image)cell.FindProperty("icon").objectReferenceValue;
                if (icon.sprite == null) throw new InvalidOperationException("Catalog cell sprite is missing.");
            }
        }
        foreach (string field in new[] { "panelRoot", "closeButton", "executeButton", "selectionText", "feedbackText", "executeLabel", "weaponOptions", "monsterOptions" })
            if (serialized.FindProperty(field).objectReferenceValue == null) throw new InvalidOperationException("Missing catalog grid reference: " + field);
        if (controls.GetComponentsInChildren<TMP_Dropdown>(true).Any(x => new[] { "Weapon", "Relic", "Monster", "Boss" }.Contains(x.name)))
            throw new InvalidOperationException("Legacy catalog dropdown remains in TestScene.");
    }
}
