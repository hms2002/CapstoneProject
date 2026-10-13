using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using TMPro;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Cinemachine;
using UnityGAS;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Runs authored test controls in the real Editor player loop and captures the result.</summary>
[InitializeOnLoad]
public static class TestSceneValidation
{
    private const string Pending = "TestSceneValidation.Pending";
    private const string Finished = "TestSceneValidation.Finished";
    private const string ExitEditor = "TestSceneValidation.ExitEditor";
    private const string GridOnly = "TestSceneValidation.GridOnly";
    private static readonly List<string> checks = new();
    private static readonly List<string> errors = new();
    private static int stage;
    private static int waitFrame;
    private static double started;
    private static string originalProfile;
    private static string savePath;
    private static string saveHash;
    private static string mirrorPath;
    private static string mirrorHash;
    private static TestSceneControlPanel panel;
    private static IEnumerator catalogChecks;

    static TestSceneValidation()
    {
        EditorApplication.update += Update;
        Application.logMessageReceived += (_, trace, type) =>
        {
            if (SessionState.GetBool(Pending, false) && (type == LogType.Exception || type == LogType.Error))
                errors.Add(trace);
        };
    }

    public static void Run()
    {
        TestSceneBuilder.Build();
        BeginValidation(false);
    }

    [MenuItem("Tools/Testing/Update and Validate TestScene Catalog Grid")]
    public static void RunCatalogGrid()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before validating the catalog grid.");
        if (Application.isBatchMode)
            EditorSceneManager.OpenScene(TestSceneBuilder.ScenePath, OpenSceneMode.Single);
        TestSceneBuilder.UpdateCatalogGrid();
        BeginValidation(true);
    }

    [MenuItem("Tools/Testing/Validate TestScene Boss Combat")]
    public static void RunBossCombat()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before validating boss combat.");
        if (Application.isBatchMode)
            EditorSceneManager.OpenScene(TestSceneBuilder.ScenePath, OpenSceneMode.Single);
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != TestSceneBuilder.ScenePath)
            throw new InvalidOperationException("Open TestScene before validating boss combat.");
        TestSceneBuilder.ValidateScene();
        BeginValidation(true);
    }

    private static void BeginValidation(bool gridOnly)
    {
        checks.Clear(); errors.Clear(); stage = 0; waitFrame = 0; started = 0; catalogChecks = null;
        SessionState.SetBool(Pending, true);
        SessionState.SetBool(Finished, false);
        SessionState.SetBool(ExitEditor, Application.isBatchMode);
        SessionState.SetBool(GridOnly, gridOnly);
        TestScenePresentationTraceIsolation.Enter();
        EditorApplication.isPlaying = true;
    }

    private static void Update()
    {
        if (SessionState.GetBool(Finished, false) && !EditorApplication.isPlaying)
        {
            SessionState.SetBool(Finished, false);
            if (SessionState.GetBool(ExitEditor, false)) EditorApplication.Exit(errors.Count == 0 ? 0 : 1);
            return;
        }
        if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        if (started == 0) started = EditorApplication.timeSinceStartup;
        if (EditorApplication.timeSinceStartup - started > 120) { Finish(new Exception("Play validation timed out.")); return; }
        if (Time.frameCount < waitFrame) return;
        try
        {
            if (stage == 0)
            {
                panel = UnityEngine.Object.FindAnyObjectByType<TestSceneControlPanel>();
                if (panel == null || PlayerRuntimeRegistry.CurrentPlayer == null || Time.frameCount < 12) return;
                originalProfile = JsonUtility.ToJson(GameDataManager.Instance.Data);
                savePath = GameDataRepository.GetDefaultSavePath(GameDataManager.Instance.ActiveSlotIndex);
                mirrorPath = GameDataRepository.GetInspectableSavePath(GameDataManager.Instance.ActiveSlotIndex);
                saveHash = Hash(savePath); mirrorHash = Hash(mirrorPath);
                Check(GameDataStore.IsAvailable && RunSessionStore.IsRunActive, "Scene runtime and disposable profile are ready");
                VerifyGridControls();
                TestSceneCatalogGrid grid = Get<TestSceneCatalogGrid>("catalogGrid");
                Check(grid.Count(TestSceneCatalogGrid.WeaponCategory) == 6, "All six ItemDatabase weapons are listed");
                Check(grid.Count(TestSceneCatalogGrid.BossCategory) >= 4, "Boss catalog populated");
                Check(grid.Count(TestSceneCatalogGrid.MonsterCategory) >= 10, "Monster catalog populated");
                Check(Get<TMP_Dropdown>("eventDropdown").options.Count > 0, "Event room catalog populated");
                for (int i = 0; i < 6; i++)
                {
                    grid.Open(TestSceneCatalogGrid.WeaponCategory); grid.Select(TestSceneCatalogGrid.WeaponCategory, i);
                    GridField<UnityEngine.UI.Button>(grid, "executeButton").onClick.Invoke();
                    Check(PlayerRuntimeRegistry.CurrentPlayer.GetComponent<WeaponInventory2D>().GetWeaponInSlot(0) == Get<ItemDatabase>("itemDatabase").allWeapons[i], "Icon-grid weapon grant " + i);
                }
                grid.Open(TestSceneCatalogGrid.RelicCategory); grid.Select(TestSceneCatalogGrid.RelicCategory, 0);
                GridField<UnityEngine.UI.Button>(grid, "executeButton").onClick.Invoke();
                Check(PlayerRuntimeRegistry.CurrentPlayer.GetComponent<RelicInventory>().GetRelicInSlot(0) != null, "Relic grant");
                Check(GridField<TMP_Text>(grid, "feedbackText").text == Get<TMP_Text>("statusText").text, "Central footer projects command result feedback");
                panel.LevelUp();
                Check(RunLevelProgression.State.level > 1 && RunLevelProgression.State.pendingRewardCount > 0, "Level up and pending choice");
                stage++; waitFrame = Time.frameCount + 3;
            }
            else if (stage == 1)
            {
                var rewards = (List<LevelRewardDefinitionSO>)typeof(TestSceneControlPanel).GetField("rewards", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);
                var eligibility = new LevelRewardEligibilityContext(PlayerRuntimeRegistry.CurrentPlayer, RunLevelProgression.State);
                int rewardIndex = rewards.FindIndex(x => x.Effects.All(e => e.Lifetime == LevelRewardEffectLifetime.Persistent) && x.CanSelect(eligibility, out _));
                Check(rewardIndex >= 0, "Eligible persistent reward available");
                Get<TMP_Dropdown>("rewardDropdown").value = rewardIndex;
                panel.ApplyReward();
                Check(RunLevelProgression.State.selectedRewards.Count == 1, "Direct level reward selection");
                panel.RemoveReward();
                Check(RunLevelProgression.State.selectedRewards.Count == 0, "Level reward removal");
                TestInstantRewards(rewards);
                panel.LevelUp(); panel.OpenRewards();
                var session = UnityEngine.Object.FindAnyObjectByType<LevelRewardSessionController>(FindObjectsInactive.Include);
                Check(session != null && session.IsSessionOpen && !Get<RectTransform>("panelRoot").gameObject.activeSelf, "Normal reward selection UI opens without test panel overlap: " + Get<TMP_Text>("statusText").text);
                session.CloseSession(); panel.TogglePanel();
                panel.SpawnMonster();
                stage++; waitFrame = Time.frameCount + 5;
            }
            else if (stage == 2)
            {
                Check(UnityEngine.Object.FindObjectsByType<Mob>(FindObjectsInactive.Exclude).Length > 0, "Monster spawned through UI command");
                panel.ClearSpawned(); panel.SpawnBoss();
                stage++; waitFrame = Time.frameCount + 5;
            }
            else if (stage == 3)
            {
                Check(UnityEngine.Object.FindObjectsByType<BossControllerBase>(FindObjectsInactive.Exclude).Length > 0, "Boss spawned through UI command");
                string previousCount = Get<TMP_InputField>("spawnCountInput").text;
                Get<TMP_InputField>("spawnCountInput").text = "99";
                panel.SpawnBoss();
                Check(UnityEngine.Object.FindObjectsByType<BossControllerBase>(FindObjectsInactive.Exclude).Count(x => x.gameObject.scene.name == "TestScene") == 1,
                    "Boss spawn ignores monster quantity and replaces the previous test boss");
                Check(!Get<TestSceneCatalogGrid>("catalogGrid").IsOpen && !InputActionQuery.IsPressBlocked(InputActionId.PrimaryAttack),
                    "Boss spawn closes catalog and releases attack input");
                Get<TMP_InputField>("spawnCountInput").text = previousCount;
                panel.ClearSpawned(); panel.SpawnChest();
                stage++; waitFrame = Time.frameCount + 5;
            }
            else if (stage == 4)
            {
                Check(UnityEngine.Object.FindObjectsByType<TreasureChest>(FindObjectsInactive.Exclude).Length > 0, "Chest spawned through UI command");
                panel.ClearSpawned(); panel.SpawnEvent();
                stage++; waitFrame = Time.frameCount + 8;
            }
            else if (stage == 5)
            {
                Check(UnityEngine.Object.FindObjectsByType<RoomPieceAuthoring>(FindObjectsInactive.Exclude).Length > 0, "Event room built and bound");
                panel.ClearSpawned(); panel.Heal();
                var database = Get<ItemDatabase>("itemDatabase");
                int spear = database.allWeapons.FindIndex(x => x.weaponId == "Weapon.LightningSpear");
                Get<TestSceneCatalogGrid>("catalogGrid").Select(TestSceneCatalogGrid.WeaponCategory, spear); panel.GiveWeapon();
                panel.TogglePanel(); panel.TogglePanel();
                Check(GameDataStore.IsAvailable && RunSessionStore.IsRunActive, "Panel hide does not release scene ownership");
                stage++; waitFrame = Time.frameCount + 5;
            }
            else if (stage == 6)
            {
                catalogChecks ??= VerifyCatalogs();
                if (catalogChecks.MoveNext()) { waitFrame = Time.frameCount + 1; return; }
                Check(UnityEngine.Object.FindObjectsByType<Candlestick>(FindObjectsInactive.Include).Length == 0 && UnityEngine.Object.FindObjectsByType<EgoSwordActor>(FindObjectsInactive.Include).Length == 0, "Boss companion actors cleared with their test spawn");
                stage++; waitFrame = Time.frameCount + 2;
            }
            else if (stage == 7)
            {
                if (!SessionState.GetBool(GridOnly, false) && !Environment.GetCommandLineArgs().Contains("-skipPlacementBenchmark"))
                    LightningSpearPlacementBenchmark.RunFixtureSuite();
                Check(originalProfile == JsonUtility.ToJson(GameDataManager.Instance.Data), "Real profile remained unchanged during test controls");
                RunSessionStore.AddPendingRunMagicStoneDelta(123);
                GamePlayDataManager.Instance.EndRun(RunEndReason.None);
                Check(originalProfile == JsonUtility.ToJson(GameDataManager.Instance.Data), "Direct legacy EndRun intercepted without profile commit");
                Check(saveHash == Hash(savePath) && mirrorHash == Hash(mirrorPath), "Persistent and inspectable save file hashes unchanged");
                RunSessionStore.StartRun();
                Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
                stage++; waitFrame = Time.frameCount + 8;
            }
            else if (stage == 8)
            {
                Directory.CreateDirectory("Library/TestSceneValidation");
                TestSceneCatalogGrid grid = Get<TestSceneCatalogGrid>("catalogGrid");
                for (int category = 0; category < 4; category++)
                {
                    grid.Open(category); grid.Select(category, 0);
                    CapturePreview("Catalog-" + category + ".png");
                }
                grid.Close(); CapturePreview();
                stage++; waitFrame = Time.frameCount + 10;
            }
            else Finish(null);
        }
        catch (Exception exception) { Finish(exception); }
    }

    private static T Get<T>(string field) => (T)typeof(TestSceneControlPanel).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);
    private static void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
        checks.Add(description); Debug.Log("[TestSceneValidation] PASS " + description);
    }
    private static void Finish(Exception exception)
    {
        if (exception != null) { errors.Add(exception.ToString()); Debug.LogError("[TestSceneValidation] " + exception); }
        if (panel != null && exception == null)
        {
            panel.enabled = false;
            Check(RunSessionStore.IsBackend(GamePlayDataManager.Instance), "Original run backend restored on scene control shutdown");
            Check(originalProfile == JsonUtility.ToJson(GameDataManager.Instance.Data), "Original profile restored unchanged");
        }
        Directory.CreateDirectory("Library/TestSceneValidation");
        File.WriteAllText("Library/TestSceneValidation/result.json", JsonUtility.ToJson(new Report { passed = exception == null && errors.Count == 0, checks = checks.ToArray(), errors = errors.ToArray(), savePath = savePath, saveHash = saveHash, mirrorPath = mirrorPath, mirrorHash = mirrorHash }, true));
        SessionState.SetBool(Pending, false); SessionState.SetBool(Finished, true);
        EditorApplication.isPlaying = false;
    }
    private static string Hash(string path)
    {
        if (!File.Exists(path)) return "missing";
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "");
    }
    private static void CapturePreview(string fileName = "TestScene.png")
    {
        Camera camera = Camera.main;
        if (camera == null) throw new InvalidOperationException("Gameplay camera is missing.");
        var brain = camera.GetComponent<CinemachineBrain>();
        bool brainEnabled = brain != null && brain.enabled;
        Vector3 previousPosition = camera.transform.position;
        Quaternion previousRotation = camera.transform.rotation;
        bool previousOrthographic = camera.orthographic;
        float previousSize = camera.orthographicSize;
        if (brain != null) brain.enabled = false;
        camera.transform.SetPositionAndRotation(new Vector3(3, 0, -10), Quaternion.identity);
        camera.orthographic = true; camera.orthographicSize = 17;
        var target = new RenderTexture(1920, 1080, 24);
        target.Create();
        RenderTexture previousTarget = camera.targetTexture;
        float previousAspect = camera.aspect;
        camera.targetTexture = target;
        camera.aspect = (float)target.width / target.height;
        var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude).Where(x => x.isRootCanvas && x.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
        int[] layers = canvases.Select(x => x.sortingLayerID).ToArray();
        foreach (Canvas canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1; canvas.sortingLayerName = "UI"; }
        Canvas.ForceUpdateCanvases();
        RenderTexture previous = RenderTexture.active;
        try
        {
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target;
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); texture.Apply();
            File.WriteAllBytes("Library/TestSceneValidation/" + fileName, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }
        finally
        {
            RenderTexture.active = previous;
            for (int i = 0; i < canvases.Length; i++) { canvases[i].renderMode = RenderMode.ScreenSpaceOverlay; canvases[i].worldCamera = null; canvases[i].sortingLayerID = layers[i]; }
            camera.transform.SetPositionAndRotation(previousPosition, previousRotation);
            camera.orthographic = previousOrthographic; camera.orthographicSize = previousSize;
            camera.targetTexture = previousTarget; camera.aspect = previousAspect;
            if (brain != null) brain.enabled = brainEnabled;
            target.Release(); UnityEngine.Object.DestroyImmediate(target);
        }
    }
    private static void TestInstantRewards(List<LevelRewardDefinitionSO> rewards)
    {
        var attributes = PlayerRuntimeRegistry.CurrentPlayer.GetComponent<AttributeSet>();
        int soulIndex = rewards.FindIndex(x => x.Effects.Any(e => e is SoulHeartGrantLevelRewardEffectSO));
        Check(soulIndex >= 0, "Instant soul-heart reward present");
        var soul = rewards[soulIndex].Effects.OfType<SoulHeartGrantLevelRewardEffectSO>().First();
        float before = attributes.GetCurrentValue(soul.GrantedAttribute);
        panel.LevelUp(); Get<TMP_Dropdown>("rewardDropdown").value = soulIndex;
        panel.ApplyReward();
        Check(RunLevelProgression.State.selectedRewards.Count == 1, "Instant soul-heart reward selected");
        panel.RemoveReward();
        Check(RunLevelProgression.State.selectedRewards.Count == 0 && attributes.GetCurrentValue(soul.GrantedAttribute) == before, "Instant soul-heart resource safely reverted");
        var database = Get<ItemDatabase>("itemDatabase");
        int relicIndex = database.allRelics.FindIndex(x => x != null && x.maxLevel > 1);
        Get<TestSceneCatalogGrid>("catalogGrid").Select(TestSceneCatalogGrid.RelicCategory, relicIndex); panel.GiveRelic();
        var inventory = PlayerRuntimeRegistry.CurrentPlayer.GetComponent<RelicInventory>();
        int[] levels = Enumerable.Range(0, inventory.Capacity).Select(inventory.GetRelicLevelInSlot).ToArray();
        int upgradeIndex = rewards.FindIndex(x => x.Effects.Any(e => e is RandomRelicUpgradeLevelRewardEffectSO));
        Check(upgradeIndex >= 0, "Instant relic-upgrade reward present");
        panel.LevelUp(); Get<TMP_Dropdown>("rewardDropdown").value = upgradeIndex;
        panel.ApplyReward();
        Check(RunLevelProgression.State.selectedRewards.Count == 1, "Instant relic upgrade selected");
        panel.RemoveReward();
        Check(RunLevelProgression.State.selectedRewards.Count == 0 && levels.SequenceEqual(Enumerable.Range(0, inventory.Capacity).Select(inventory.GetRelicLevelInSlot)), "Instant relic levels safely reverted");
    }
    private static IEnumerator VerifyCatalogs()
    {
        TestSceneCatalogGrid grid = Get<TestSceneCatalogGrid>("catalogGrid");
        foreach (int category in new[] { TestSceneCatalogGrid.BossCategory, TestSceneCatalogGrid.MonsterCategory, -1 })
        {
            TMP_Dropdown dropdown = category < 0 ? Get<TMP_Dropdown>("eventDropdown") : null;
            int count = category < 0 ? dropdown.options.Count : grid.Count(category);
            for (int i = 0; i < count; i++)
            {
                string label;
                if (category < 0)
                {
                    dropdown.value = i; label = dropdown.options[i].text; panel.SpawnEvent();
                }
                else
                {
                    grid.Open(category); grid.Select(category, i);
                    label = GridField<TMP_Text>(grid, "selectionText").text;
                    GridField<UnityEngine.UI.Button>(grid, "executeButton").onClick.Invoke(); grid.Close();
                }
                yield return null; yield return null;
                bool found = category < 0
                    ? UnityEngine.Object.FindObjectsByType<RoomPieceAuthoring>(FindObjectsInactive.Exclude).Length > 0
                    : UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsInactive.Exclude).Any(x => x.gameObject.scene.name == "TestScene");
                Check(found, "Catalog spawn: " + label + " / " + Get<TMP_Text>("statusText").text);
                if (category == TestSceneCatalogGrid.BossCategory)
                    VerifyBossCombat();
                panel.ClearSpawned(); panel.Heal();
                yield return null; yield return null;
            }
        }
    }

    private static T GridField<T>(object target, string field) =>
        (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

    private static void VerifyBossCombat()
    {
        BossControllerBase boss = UnityEngine.Object.FindObjectsByType<BossControllerBase>(FindObjectsInactive.Exclude)
            .Single(x => x.gameObject.scene.name == "TestScene");
        Check(boss.IsCombatActive && !boss.CanEnterEncounterIntroState(), "Boss starts direct combat without intro: " + boss.name);
        Check(boss.GetComponentsInChildren<BossEncounterDirector>(true).All(x => !x.enabled && !x.IsSequenceRunning) &&
            boss.GetComponentsInChildren<BossTalkManager>(true).All(x => !x.enabled && !x.IsSequenceRunning) &&
            boss.GetComponentsInChildren<BossDeathPresentation>(true).All(x => !x.HandlesDeathFlow),
            "Test boss intro/death cinematics are disabled: " + boss.name);
        Check(PlayerRuntimeRegistry.CurrentPlayer.CurrentState != InteractState.Talking && !DialoguePlayback.IsPlaying,
            "Boss does not leave player in dialogue: " + boss.name);
        Physics2D.SyncTransforms();
        Collider2D hurtbox = boss.GetComponentsInChildren<CombatHurtbox2D>()
            .SelectMany(x => x.GetComponents<Collider2D>()).FirstOrDefault(x => x.enabled && x.gameObject.activeInHierarchy &&
                CombatTargetResolver2D.ResolveDamageTarget(x) == boss.gameObject);
        Check(hurtbox != null, "Boss has a live authored damage hurtbox: " + boss.name);
        Collider2D contact = Physics2D.OverlapBoxAll(hurtbox.bounds.center, hurtbox.bounds.size, 0f)
            .FirstOrDefault(x => x == hurtbox);
        Check(contact != null && CombatTargetResolver2D.ResolveDamageTarget(contact) == boss.gameObject,
            "Physics contact resolves the boss damage target: " + boss.name);
        float before = boss.CurrentHealthValue;
        var damage = AssetDatabase.LoadAssetAtPath<GE_Damage_Spec>("Assets/_Project/Data/Abilities/Effects/GE_Damage_Spec.asset");
        GameObject player = PlayerRuntimeRegistry.CurrentPlayer.gameObject;
        CombatDamageAction.ApplyDamageAndEmitHit(player.GetComponent<AbilitySystem>(), null, damage,
            CombatTargetResolver2D.ResolveDamageTarget(contact), 1f, null, player);
        Check(boss.CurrentHealthValue < before, "Real GAS damage reduces boss HP: " + boss.name);
    }

    private static void VerifyGridControls()
    {
        TestSceneCatalogGrid grid = Get<TestSceneCatalogGrid>("catalogGrid");
        ItemDatabase database = Get<ItemDatabase>("itemDatabase");
        Check(grid.IsOpen && grid.Count(0) == database.allWeapons.Count(x => x != null) &&
            grid.Count(1) == database.allRelics.Count(x => x != null), "Centered grid starts open with complete ItemDatabase catalogs");
        string[] cellFields = { "weaponCells", "relicCells", "monsterCells", "bossCells" };
        string[] openFields = { "openWeaponsButton", "openRelicsButton", "openMonstersButton", "openBossesButton" };
        var tabs = GridField<UnityEngine.UI.Button[]>(grid, "categoryButtons");
        var roots = GridField<GameObject[]>(grid, "categoryRoots");
        var window = (RectTransform)GridField<GameObject>(grid, "panelRoot").transform.Find("CatalogWindow");
        Check(window.anchorMin == new Vector2(.5f, .5f) && window.anchorMax == window.anchorMin && window.anchoredPosition == Vector2.zero,
            "Catalog window is anchored at screen center");
        for (int category = 0; category < 4; category++)
        {
            tabs[category].onClick.Invoke();
            Check(roots[category].activeSelf && roots.Count(x => x.activeSelf) == 1, "Category tab switches grid " + category);
            Check(GridField<GameObject>(grid, "monsterOptions").activeSelf == (category == TestSceneCatalogGrid.MonsterCategory),
                "Quantity input belongs only to monsters " + category);
            TestSceneCatalogCell[] cells = GridField<TestSceneCatalogCell[]>(grid, cellFields[category]);
            Check(cells.All(x => x.Icon != null && !string.IsNullOrWhiteSpace(x.DisplayName)), "Every catalog card has an authored icon and name " + category);
            int selected = cells.Length - 1;
            GridField<UnityEngine.UI.Button>(cells[selected], "button").onClick.Invoke();
            Check(grid.GetSelectedIndex(category) == selected && GridField<TMP_Text>(grid, "selectionText").text.Contains(cells[selected].DisplayName.Replace("\n", " · ")),
                "Card click updates selected index and label " + category);
            Check(cells.Count(x => GridField<UnityEngine.UI.Image>(x, "selectionBorder").enabled) == 1 &&
                GridField<UnityEngine.UI.Image>(cells[selected], "selectionBorder").enabled, "Only selected card is highlighted " + category);
            if (cells.Length > 18)
            {
                var scroll = roots[category].GetComponent<UnityEngine.UI.ScrollRect>();
                Canvas.ForceUpdateCanvases(); scroll.verticalNormalizedPosition = 0; Canvas.ForceUpdateCanvases();
                Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, cells[selected].transform);
                Check(bounds.min.y >= scroll.viewport.rect.yMin - 1 && bounds.max.y <= scroll.viewport.rect.yMax + 1,
                    "Scrollable catalog reveals its last card " + category);
                scroll.verticalNormalizedPosition = 1;
            }
            GridField<UnityEngine.UI.Button>(grid, "closeButton").onClick.Invoke();
            Check(!grid.IsOpen, "Close button hides grid " + category);
            Get<UnityEngine.UI.Button>(openFields[category]).onClick.Invoke();
            Check(grid.IsOpen && roots[category].activeSelf && grid.GetSelectedIndex(category) == selected, "Side button reopens category and preserves selection " + category);
        }
        grid.Open(0); grid.Select(0, 0);
        int executions = 0;
        Action<int> countExecution = _ => executions++;
        grid.ExecuteRequested += countExecution;
        grid.enabled = false; grid.enabled = true; grid.enabled = false; grid.enabled = true;
        GridField<UnityEngine.UI.Button>(grid, "executeButton").onClick.Invoke();
        grid.ExecuteRequested -= countExecution;
        Check(executions == 1, "Grid re-enable does not duplicate execution listeners");
        grid.Close();
    }
    [Serializable] private sealed class Report { public bool passed; public string[] checks; public string[] errors; public string savePath, saveHash, mirrorPath, mirrorHash; }
}
