using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Unity.Cinemachine;

/// <summary>Authors the isolated test arena and its UI. Runtime code only drives saved references.</summary>
public sealed partial class TestSceneBuilder : IProcessSceneWithReport
{
    public const string ScenePath = "Assets/_Project/Scenes/Tests/TestScene.unity";
    private const string DemonKingPath = "Assets/_Project/Prefabs/Bosses/DemonKing/DemonKing.prefab";
    private const string DemonRigPath = "Assets/_Project/Prefabs/Testing/Bosses/DemonKing_TestRig.prefab";
    private static TMP_FontAsset font;

    [MenuItem("Tools/Testing/Build TestScene")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before authoring TestScene.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (File.Exists(ScenePath))
        {
            Scene existing = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!existing.GetRootGameObjects().Any(x => x.name == "TestScene Authoring"))
                throw new InvalidOperationException("Existing TestScene is not owned by this builder.");
        }

        Scene source = EditorSceneManager.OpenScene("Assets/_Project/Scenes/ProtoTypeCorridor.unity", OpenSceneMode.Single);
        Tilemap sourceFloor = source.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Tilemap>(true))
            .First(x => x.name == "Ground");
        TileBase floorTile = MostCommonTile(sourceFloor);
        if (floorTile == null) throw new InvalidOperationException("Prototype floor tile is missing.");
        GameObject lootSource = source.GetRootGameObjects().FirstOrDefault(x => x.GetComponent<LootManager>() != null);
        GameObject lootCopy = lootSource != null ? UnityEngine.Object.Instantiate(lootSource) : null;

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        if (lootCopy != null)
        {
            lootCopy.name = "LootManager";
            SceneManager.MoveGameObjectToScene(lootCopy, scene);
        }
        EditorSceneManager.CloseScene(source, true);
        new GameObject("TestScene Authoring");
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Art/Font/Galmuri9 SDF.asset");
        if (font == null) throw new InvalidOperationException("Korean test UI font is missing.");

        GameObject arena = new("Arena_30x30", typeof(Grid));
        Tilemap ground = TilemapObject("Ground", arena.transform, LayerMask.NameToLayer("Ground"), "Background");
        for (int x = -15; x < 15; x++)
            for (int y = -15; y < 15; y++) ground.SetTile(new Vector3Int(x, y, 0), floorTile);
        ground.color = new Color(.62f, .67f, .74f);
        Tilemap wallVisual = TilemapObject("Wall", arena.transform, LayerMask.NameToLayer("Wall"), "Wall");
        wallVisual.color = new Color(.20f, .28f, .36f);
        for (int x = -16; x <= 15; x++)
        {
            wallVisual.SetTile(new Vector3Int(x, -16, 0), floorTile);
            wallVisual.SetTile(new Vector3Int(x, 15, 0), floorTile);
        }
        for (int y = -15; y < 15; y++)
        {
            wallVisual.SetTile(new Vector3Int(-16, y, 0), floorTile);
            wallVisual.SetTile(new Vector3Int(15, y, 0), floorTile);
        }
        CreateWall(arena.transform, "North", new Vector2(0, 15.5f), new Vector2(32, 1));
        CreateWall(arena.transform, "South", new Vector2(0, -15.5f), new Vector2(32, 1));
        CreateWall(arena.transform, "West", new Vector2(-15.5f, 0), new Vector2(1, 30));
        CreateWall(arena.transform, "East", new Vector2(15.5f, 0), new Vector2(1, 30));
        GameObject room = new("ArenaRoom", typeof(BoxCollider2D), typeof(MonsterRoomArea2D), typeof(MonsterSpawnRoomGroup));
        var roomCollider = room.GetComponent<BoxCollider2D>();
        roomCollider.isTrigger = true;
        roomCollider.size = new Vector2(30, 30);
        room.GetComponent<MonsterRoomArea2D>().Configure(roomCollider);
        var spawnOrigin = new GameObject("ContentSpawnOrigin").transform;
        spawnOrigin.position = new Vector3(5, 2, 0);
        var spawnPoint = new GameObject("PlayerSpawnPoint", typeof(PlayerSpawnPoint)).GetComponent<PlayerSpawnPoint>();
        spawnPoint.pointId = "test-scene";
        spawnPoint.isDefault = true;
        var spawner = new GameObject("PlayerSpawner", typeof(PlayerSpawner)).GetComponent<PlayerSpawner>();
        Set(spawner, "playerPrefab", Load<GameObject>("Assets/_Project/Prefabs/Player/PF Player.prefab"));
        Set(spawner, "defaultSpawnPoint", spawnPoint);
        SetArray(spawner, "spawnPoints", new UnityEngine.Object[] { spawnPoint });
        PrefabUtility.InstantiatePrefab(Load<GameObject>("Assets/_Project/Prefabs/UI/GlobalUIRoot.prefab"), scene);

        var light = new GameObject("TestScene Global Light", typeof(Light2D)).GetComponent<Light2D>();
        light.lightType = Light2D.LightType.Global;
        light.intensity = 1;
        MonsterSceneNavigationInstaller.EnsureSceneNavigation(scene);

        BuildDemonKingRig(scene);
        var controls = BuildCanvas();
        Set(controls, "itemDatabase", Load<ItemDatabase>("Assets/_Project/Data/Items/ItemDatabase.asset"));
        Set(controls, "rewardCatalog", Load<LevelRewardCatalogSO>("Assets/_Project/Data/Progression/Leveling/Rewards/Catalog/LevelRewardCatalog.asset"));
        Set(controls, "progressionConfig", Load<LevelProgressionConfigSO>("Assets/_Project/Data/Progression/Leveling/LevelProgressionConfig.asset"));
        BuildCatalogGrid(controls);
        PrepareCatalogs(controls);
        Set(controls, "arenaRoot", arena);
        Set(controls, "roomArea", room.GetComponent<MonsterRoomArea2D>());
        Set(controls, "roomGroup", room.GetComponent<MonsterSpawnRoomGroup>());
        Set(controls, "groundTilemap", ground);
        Set(controls, "spawnOrigin", spawnOrigin);

        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Scene save failed.");
        AssetDatabase.SaveAssets();
        ValidateScene();
        Debug.Log("[TestScene] Authored arena and test controls.");
    }

    // Unity processes a temporary scene for Editor Play and for builds that include TestScene.
    public int callbackOrder => 0;

    public void OnProcessScene(Scene scene, BuildReport report)
    {
        if (scene.path != ScenePath) return;
        var controls = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<TestSceneControlPanel>(true)).Single();
        PrepareCatalogs(controls);
    }

    private static void PrepareCatalogs(TestSceneControlPanel controls)
    {
        GameObject[] mobs = FindPrefabs("Assets/_Project/Prefabs/Monsters", p => p.GetComponent<Mob>() != null);
        GameObject[] bosses = FindPrefabs("Assets/_Project/Prefabs/Bosses", p => p.GetComponent<BossControllerBase>() != null);
        for (int i = 0; i < bosses.Length; i++)
        {
            if (AssetDatabase.GetAssetPath(bosses[i]) == DemonKingPath) bosses[i] = Load<GameObject>(DemonRigPath);
        }
        GameObject[] chests = FindPrefabs("Assets/_Project/Prefabs/Items/Chests", p => p.GetComponent<TreasureChest>() != null);
        var templates = AssetDatabase.FindAssets("t:RoomTemplateSO", new[] { "Assets/_Project/Data/Dungeon/Rooms" })
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path, StringComparer.Ordinal)
            .Select(AssetDatabase.LoadAssetAtPath<RoomTemplateSO>).Where(IsTestEvent).ToArray();
        SetArray(controls, "mobPrefabs", mobs);
        SetArray(controls, "bossPrefabs", bosses);
        SetArray(controls, "chestPrefabs", chests);
        SetArray(controls, "eventTemplates", templates);
        var data = new SerializedObject(controls);
        if (data.FindProperty("eventRoomShell").objectReferenceValue == null)
            Set(controls, "eventRoomShell", CreateEventRoomShell(controls.gameObject.scene));
        var database = (ItemDatabase)data.FindProperty("itemDatabase").objectReferenceValue;
        var grid = (TestSceneCatalogGrid)data.FindProperty("catalogGrid").objectReferenceValue;
        RefreshCards(grid, database, mobs, bosses);
        controls.EditorCatalogPrepared();
    }

    private static bool IsTestEvent(RoomTemplateSO template)
    {
        if (template == null || template.LayoutData.roomType != RoomType.Event) return false;
        return template.BuildData.objectPlacements == null || !template.BuildData.objectPlacements.Any(x =>
            x.prefab != null && (x.prefab.GetComponentInChildren<RunSpecialNpcFeatureBase>(true) != null ||
                                x.prefab.GetComponentInChildren<RunSpecialNpcInteractor>(true) != null));
    }

    public static void BuildFromCommandLine() => Build();

    [MenuItem("Tools/Testing/Validate TestScene")]
    public static void ValidateScene()
    {
        var scene = SceneManager.GetActiveScene();
        var panel = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<TestSceneControlPanel>(true)).Single();
        var serialized = new SerializedObject(panel);
        foreach (string field in new[] { "itemDatabase", "rewardCatalog", "progressionConfig", "roomArea", "roomGroup", "groundTilemap", "spawnOrigin", "panelRoot", "catalogGrid", "openWeaponsButton", "openRelicsButton", "openMonstersButton", "openBossesButton", "weaponSlotDropdown", "spawnCountInput", "chestDropdown", "eventDropdown", "rewardDropdown", "appliedRewardDropdown", "statusText", "progressionText" })
            if (serialized.FindProperty(field).objectReferenceValue == null) throw new InvalidOperationException("Missing TestScene reference: " + field);
        ValidateCatalogGrid(panel);
        Tilemap ground = (Tilemap)serialized.FindProperty("groundTilemap").objectReferenceValue;
        if (ground.GetUsedTilesCount() == 0 || !ground.HasTile(new Vector3Int(-15, -15, 0)) || !ground.HasTile(new Vector3Int(14, 14, 0)))
            throw new InvalidOperationException("Arena does not cover its 30x30 bounds.");
        foreach (var root in scene.GetRootGameObjects())
            if (root.GetComponentsInChildren<MonoBehaviour>(true).Any(x => x == null)) throw new InvalidOperationException("Missing script under " + root.name);
        Debug.Log("[TestScene] Serialized scene validation passed.");
    }

    private static TestSceneControlPanel BuildCanvas()
    {
        var canvas = new GameObject("TestSceneCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.GetComponent<Canvas>().sortingOrder = 31000;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;
        RectTransform panel = Rect("Test Panel", canvas.transform);
        panel.anchorMin = new Vector2(1, 0);
        panel.anchorMax = Vector2.one;
        panel.pivot = new Vector2(1, .5f);
        panel.sizeDelta = new Vector2(440, -32);
        panel.anchoredPosition = new Vector2(-16, 0);
        panel.gameObject.AddComponent<Image>().color = new Color(.035f, .055f, .08f, .96f);
        var controls = canvas.AddComponent<TestSceneControlPanel>();
        Set(controls, "panelRoot", panel);
        RectTransform header = Rect("Header", panel);
        header.anchorMin = new Vector2(0, 1); header.anchorMax = Vector2.one;
        header.pivot = new Vector2(.5f, 1); header.sizeDelta = new Vector2(-24, 52); header.anchoredPosition = new Vector2(0, -8);
        TMP_Text title = Text("Title", header, "TEST SCENE · 30 × 30", 25);
        title.alignment = TextAlignmentOptions.MidlineLeft;
        RectTransform viewport = Rect("Viewport", panel);
        viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(12, 108); viewport.offsetMax = new Vector2(-12, -108);
        viewport.gameObject.AddComponent<RectMask2D>();
        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30;
        RectTransform content = Rect("Content", viewport);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1);
        content.sizeDelta = Vector2.zero;
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8; layout.childControlHeight = true; layout.childForceExpandHeight = false;
        layout.childControlWidth = true; layout.childForceExpandWidth = true;
        var fitter = content.gameObject.AddComponent<ContentSizeFitter>(); fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = content; scroll.viewport = viewport;
        RectTransform progression = Rect("Progression", panel);
        progression.anchorMin = new Vector2(0, 1); progression.anchorMax = Vector2.one;
        progression.pivot = new Vector2(.5f, 1); progression.sizeDelta = new Vector2(-24, 32); progression.anchoredPosition = new Vector2(0, -62);
        Set(controls, "progressionText", Text("Label", progression, "레벨 / 경험치", 19));
        Section(content, "장비 지급");
        Set(controls, "openWeaponsButton", Button(content, "무기 목록"));
        Set(controls, "openRelicsButton", Button(content, "유물 목록"));
        Section(content, "전투 대상 생성");
        Set(controls, "openMonstersButton", Button(content, "몬스터 목록"));
        Set(controls, "openBossesButton", Button(content, "보스 목록"));
        Section(content, "상자 / 이벤트 방");
        Set(controls, "chestDropdown", Dropdown(content, "Chest"));
        Set(controls, "spawnChestButton", Button(content, "상자 생성"));
        Set(controls, "eventDropdown", Dropdown(content, "Event room"));
        Set(controls, "spawnEventButton", Button(content, "이벤트 방 생성"));
        Section(content, "레벨 / 보상 효과");
        Set(controls, "levelUpButton", Button(content, "레벨 +1"));
        Set(controls, "openRewardsButton", Button(content, "일반 레벨업 선택창 열기"));
        Set(controls, "rewardDropdown", Dropdown(content, "Reward"));
        Set(controls, "applyRewardButton", Button(content, "선택 효과 적용"));
        Set(controls, "appliedRewardDropdown", Dropdown(content, "Applied reward"));
        Set(controls, "removeRewardButton", Button(content, "선택 효과 제거"));
        Set(controls, "clearRewardsButton", Button(content, "레벨 효과 모두 제거"));
        Section(content, "테스트 정리");
        Set(controls, "healButton", Button(content, "플레이어 회복"));
        Set(controls, "resetPlayerButton", Button(content, "플레이어 재생성"));
        Set(controls, "clearSpawnedButton", Button(content, "생성 대상 정리 / 기본 방 복원"));
        RectTransform status = Rect("Status", panel);
        status.anchorMin = Vector2.zero; status.anchorMax = new Vector2(1, 0);
        status.pivot = new Vector2(.5f, 0); status.sizeDelta = new Vector2(-24, 88); status.anchoredPosition = new Vector2(0, 12);
        TMP_Text statusLabel = Text("Label", status, "준비 중…", 19);
        statusLabel.alignment = TextAlignmentOptions.TopLeft;
        Set(controls, "statusText", statusLabel);
        RectTransform toggle = Rect("Toggle Test Panel", canvas.transform);
        toggle.anchorMin = toggle.anchorMax = new Vector2(1, 1); toggle.pivot = Vector2.one;
        toggle.sizeDelta = new Vector2(164, 34); toggle.anchoredPosition = new Vector2(-464, -16);
        toggle.gameObject.AddComponent<Image>().color = new Color(.10f, .22f, .30f, .98f);
        var toggleButton = toggle.gameObject.AddComponent<Button>();
        Text("Label", toggle, "테스트 패널", 20);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(toggleButton.onClick, controls.TogglePanel);
        return controls;
    }

    private static RoomPieceAuthoring CreateEventRoomShell(Scene scene)
    {
        var root = new GameObject("Test Event Room Shell", typeof(Grid));
        root.SetActive(false);
        SceneManager.MoveGameObjectToScene(root, scene);
        Tilemap Layer(string name, string sorting, int order = 0, int physicsLayer = 0)
        {
            Tilemap map = TilemapObject(name, root.transform, physicsLayer, sorting);
            map.GetComponent<TilemapRenderer>().sortingOrder = order;
            return map;
        }
        Tilemap floor = Layer("Ground", "Background", physicsLayer: LayerMask.NameToLayer("Ground"));
        Tilemap wall = Layer("Wall", "Wall", physicsLayer: LayerMask.NameToLayer("Wall"));
        Tilemap hole = Layer("Hole", "Background", physicsLayer: LayerMask.NameToLayer("HoleTrap"));
        wall.gameObject.AddComponent<TilemapCollider2D>();
        hole.gameObject.AddComponent<TilemapCollider2D>().isTrigger = true;
        var trigger = root.AddComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        root.AddComponent<MonsterRoomArea2D>().Configure(trigger);
        root.AddComponent<MonsterSpawnRoomGroup>();
        var author = root.AddComponent<RoomPieceAuthoring>();
        author.EditorAssignTilemaps(root.GetComponent<Grid>(),
            Layer("UnderFloor", "Background", -1), floor, Layer("FloorDetail", "Background", 1),
            Layer("GroundDecoration", "GroundAOE"), wall, Layer("WallDetail", "Wall", 1),
            Layer("Foreground", "ForeGround"), Layer("OverlayFX", "FloatingAOE"), hole);
        return author;
    }

    private static void BuildDemonKingRig(Scene targetScene)
    {
        // Demon King deliberately consumes a scene-authored sword and boss camera.
        // Package that companion only in a test prefab; the production prefab is untouched.
        Scene source = EditorSceneManager.OpenScene("Assets/_Project/Scenes/LeeJunmo_Boss_DemonKing.unity", OpenSceneMode.Additive);
        var swordSource = source.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<EgoSwordActor>(true)).FirstOrDefault();
        if (swordSource == null) throw new InvalidOperationException("Authored Demon King EgoSword is missing.");
        var cameraSource = source.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<CinemachineCamera>(true)).First(x => x.name == "BossCam");
        var sword = UnityEngine.Object.Instantiate(swordSource.gameObject);
        var camera = UnityEngine.Object.Instantiate(cameraSource.gameObject);
        SceneManager.MoveGameObjectToScene(sword, targetScene);
        SceneManager.MoveGameObjectToScene(camera, targetScene);
        camera.name = "BossCam";
        camera.GetComponent<CinemachineCamera>().Follow = null;
        camera.GetComponent<CinemachineCamera>().LookAt = null;
        camera.GetComponent<CinemachineCamera>().Priority = 0;
        SceneManager.SetActiveScene(targetScene);
        var demon = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(DemonKingPath), targetScene);
        demon.name = "DemonKing_TestRig";
        sword.name = "EgoSword";
        sword.transform.SetParent(demon.transform, false);
        Set(demon.GetComponent<DemonKingController>(), "egoSword", sword.GetComponent<EgoSwordActor>());
        Directory.CreateDirectory("Assets/_Project/Prefabs/Testing/Bosses");
        if (PrefabUtility.SaveAsPrefabAsset(demon, DemonRigPath) == null)
            throw new InvalidOperationException("Could not save the Demon King test rig.");
        UnityEngine.Object.DestroyImmediate(demon);
        EditorSceneManager.CloseScene(source, true);
        SceneManager.SetActiveScene(targetScene);
    }

    private static TileBase MostCommonTile(Tilemap map)
    {
        var counts = new Dictionary<TileBase, int>();
        foreach (var cell in map.cellBounds.allPositionsWithin)
        {
            TileBase tile = map.GetTile(cell); if (tile == null) continue;
            counts.TryGetValue(tile, out int count); counts[tile] = count + 1;
        }
        return counts.OrderByDescending(x => x.Value).Select(x => x.Key).FirstOrDefault();
    }
    private static void CreateWall(Transform parent, string name, Vector2 position, Vector2 size)
    {
        var wall = new GameObject("Wall " + name, typeof(BoxCollider2D)); wall.transform.SetParent(parent, false);
        wall.layer = LayerMask.NameToLayer("Wall"); wall.transform.localPosition = position;
        wall.GetComponent<BoxCollider2D>().size = size;
    }
    private static Tilemap TilemapObject(string name, Transform parent, int layer, string sorting)
    {
        var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer)); go.transform.SetParent(parent, false); go.layer = layer;
        go.GetComponent<TilemapRenderer>().sortingLayerName = sorting; return go.GetComponent<Tilemap>();
    }
    private static T Load<T>(string path) where T : UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Missing asset: " + path);
    private static GameObject[] FindPrefabs(string path, Func<GameObject, bool> predicate) => AssetDatabase.FindAssets("t:Prefab", new[] { path }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(x => x, StringComparer.Ordinal).Select(AssetDatabase.LoadAssetAtPath<GameObject>).Where(predicate).ToArray();
    private static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        var data = new SerializedObject(target); var property = data.FindProperty(field) ?? throw new InvalidOperationException("Missing field: " + field);
        property.objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void SetArray(UnityEngine.Object target, string field, UnityEngine.Object[] values)
    {
        var data = new SerializedObject(target); var property = data.FindProperty(field); property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i]; data.ApplyModifiedPropertiesWithoutUndo();
    }
    private static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); return go.GetComponent<RectTransform>();
    }
    private static TMP_Text Text(string name, Transform parent, string value, float size)
    {
        var rect = Rect(name, parent); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(8, 0); rect.offsetMax = new Vector2(-8, 0);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>(); text.font = font; text.text = value; text.fontSize = size; text.color = new Color(.91f, .94f, .98f); text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.Midline; return text;
    }
    private static TMP_Text Label(Transform parent, string value, float height, float size)
    {
        var root = Rect(value, parent); SetHeight(root.gameObject, height); return Text("Label", root, value, size);
    }
    private static void Section(Transform parent, string value) { var label = Label(parent, value, 34, 22); label.color = new Color(.38f, .79f, .92f); label.alignment = TextAlignmentOptions.MidlineLeft; }
    private static TMP_Dropdown Dropdown(Transform parent, string name)
    {
        var go = TMP_DefaultControls.CreateDropdown(new TMP_DefaultControls.Resources()); go.name = name; go.transform.SetParent(parent, false); SetHeight(go, 42); ConfigureTexts(go);
        go.GetComponent<Image>().color = new Color(.12f, .17f, .24f);
        var dropdown = go.GetComponent<TMP_Dropdown>();
        dropdown.template.GetComponent<Image>().color = new Color(.08f, .12f, .18f);
        Transform arrow = go.transform.Find("Arrow");
        if (arrow != null)
        {
            arrow.GetComponent<Image>().enabled = false;
            Text("Arrow Label", arrow, "v", 18);
        }
        dropdown.options = new List<TMP_Dropdown.OptionData> { new("준비 중") }; return dropdown;
    }
    private static Button Button(Transform parent, string label)
    {
        var rect = Rect(label, parent); SetHeight(rect.gameObject, 40);
        rect.gameObject.AddComponent<Image>().color = new Color(.10f, .28f, .36f);
        var button = rect.gameObject.AddComponent<Button>(); Text("Label", rect, label, 21); return button;
    }
    private static void ConfigureTexts(GameObject go)
    {
        foreach (TMP_Text text in go.GetComponentsInChildren<TMP_Text>(true)) { text.font = font; text.fontSize = 20; text.color = new Color(.9f, .94f, .98f); }
    }
    private static void SetHeight(GameObject go, float height) { var element = go.AddComponent<LayoutElement>(); element.minHeight = element.preferredHeight = height; }
}
