using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BossBattleEndMigrationValidatorWindow : EditorWindow
{
    private const string LegacyBossRewardAdapterScriptGuid = "42667dfbf3529e64" + "89882ac43545fe3b";
    private const string DeletedBattleEndDefinitionScriptGuid = "52e1ad6841f446b9" + "ba7e7383d2e07849";
    private const string DefaultPrefabCatalogPath = "Assets/LeeJunMo/Datas/Scene/BossBattleEndPrefabCatalog.asset";
    private const string DefaultTreasureChestPrefabPath = "Assets/HeoMinSeok/_Project/Prefabs/Gameplay/Items/TreasureChest.prefab";
    private const string DefaultMagicStonePrefabPath = "Assets/LeeJunMo/Prefab/Looting/MagicStonePrefab.prefab";
    private const string DefaultPortalPrefabPath = "Assets/LeeJunMo/Prefab/Map/Portal/ScenePortal.prefab";
    private static readonly string DeprecatedDefinitionTypeName = "BossBattleEnd" + "DefinitionSO";
    private static readonly string DeprecatedRouteDefinitionFieldName = "bossBattleEnd" + "Definition";
    private static readonly string DeprecatedPortalOffsetFieldName = "portalSpawn" + "Offset";
    private static readonly string DeprecatedRewardProfileTypeName = "BossReward" + "ProfileSO";
    private static readonly string DeprecatedRewardProfileFieldName = "bossReward" + "Profile";

    private enum Severity
    {
        Info,
        Warning,
        Error
    }

    private sealed class ValidationResult
    {
        public string Path;
        public Severity SeverityLevel;
        public string Message;
        public UnityEngine.Object Context;
        public string ObjectPath;
    }

    private sealed class AutoFixStats
    {
        public int AssetsCreated;
        public int ComponentsAdded;
        public int AnchorsCreated;
        public int ReferencesAssigned;
        public int PrefabsSaved;
        public int ScenesSaved;

        public bool HasChanges =>
            AssetsCreated > 0 ||
            ComponentsAdded > 0 ||
            AnchorsCreated > 0 ||
            ReferencesAssigned > 0 ||
            PrefabsSaved > 0 ||
            ScenesSaved > 0;

        public void Add(AutoFixStats other)
        {
            if (other == null)
                return;

            AssetsCreated += other.AssetsCreated;
            ComponentsAdded += other.ComponentsAdded;
            AnchorsCreated += other.AnchorsCreated;
            ReferencesAssigned += other.ReferencesAssigned;
            PrefabsSaved += other.PrefabsSaved;
            ScenesSaved += other.ScenesSaved;
        }
    }

    private readonly List<ValidationResult> results = new List<ValidationResult>();
    private Vector2 scrollPosition;

    [MenuItem("Tools/Validation/Boss Battle-End Migration Validator")]
    public static void ShowWindow()
    {
        GetWindow<BossBattleEndMigrationValidatorWindow>("Boss BattleEnd Validator");
    }

    private void OnGUI()
    {
        DrawToolbar();
        DrawSummary();
        DrawResults();
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("Validate Project", EditorStyles.toolbarButton))
                ValidateProject();

            if (GUILayout.Button("Validate RouteSets", EditorStyles.toolbarButton))
                ValidateRouteSetsOnly();

            if (GUILayout.Button("Clear", EditorStyles.toolbarButton))
                results.Clear();
        }

        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("Auto Fix Common Assets", EditorStyles.toolbarButton))
                AutoFixCommonAssets();

            if (GUILayout.Button("Auto Fix Active Scene", EditorStyles.toolbarButton))
                AutoFixActiveScene();

            if (GUILayout.Button("Auto Fix Boss Prefabs", EditorStyles.toolbarButton))
                AutoFixPrefabs();

            if (GUILayout.Button("Auto Fix All Scenes", EditorStyles.toolbarButton))
                AutoFixAllScenes();
        }
    }

    private void DrawSummary()
    {
        int errorCount = results.Count(result => result.SeverityLevel == Severity.Error);
        int warningCount = results.Count(result => result.SeverityLevel == Severity.Warning);
        int infoCount = results.Count(result => result.SeverityLevel == Severity.Info);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Errors", errorCount.ToString());
        EditorGUILayout.LabelField("Warnings", warningCount.ToString());
        EditorGUILayout.LabelField("Infos", infoCount.ToString());
        EditorGUILayout.Space(6f);
    }

    private void DrawResults()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        if (results.Count == 0)
        {
            EditorGUILayout.HelpBox("No results yet. Run validation to inspect boss battle-end authoring.", MessageType.Info);
            EditorGUILayout.EndScrollView();
            return;
        }

        foreach (ValidationResult result in results)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"{result.SeverityLevel} - {result.Path}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(result.Message, EditorStyles.wordWrappedLabel);

                if (!string.IsNullOrEmpty(result.ObjectPath))
                    EditorGUILayout.LabelField("Object", result.ObjectPath);

                if (result.Context != null && GUILayout.Button("Ping", GUILayout.Width(64f)))
                    EditorGUIUtility.PingObject(result.Context);
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void ValidateProject()
    {
        results.Clear();
        ValidateLegacyBossRewardAdapterGuidReferences();
        ValidateDeletedBattleEndDefinitionReferences();
        ValidatePrefabCatalogAssets();
        ValidateRouteSets();
        ValidatePrefabs();
        ValidateScenes();

        if (results.Count == 0)
        {
            AddResult(
                "Project",
                Severity.Info,
                "No boss battle-end authoring issues found by source asset scan. Still run a boss death play check.",
                null,
                string.Empty);
        }
    }

    private void ValidateRouteSetsOnly()
    {
        results.Clear();
        ValidateDeletedBattleEndDefinitionReferences();
        ValidateRouteSets();
    }

    private void AutoFixCommonAssets()
    {
        results.Clear();
        AutoFixStats stats = new AutoFixStats();
        EnsureDefaultPrefabCatalog(stats);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        AddAutoFixSummary("Common asset auto fix complete.", stats);
        ValidatePrefabCatalogAssets();
        ValidateRouteSets();
    }

    private void AutoFixActiveScene()
    {
        results.Clear();
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
        {
            AddResult(string.Empty, Severity.Error, "No active scene is loaded.", null, string.Empty);
            return;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Auto Fix Boss Battle-End Wiring");
        AutoFixStats stats = AutoFixSceneObjects(activeScene);
        if (stats.HasChanges)
            EditorSceneManager.MarkSceneDirty(activeScene);

        AddAutoFixSummary("Active scene auto fix complete. Save the scene after review.", stats);
        ValidateSceneObjects(activeScene.path, activeScene);
    }

    private void AutoFixPrefabs()
    {
        if (!EditorUtility.DisplayDialog(
                "Auto Fix Boss Prefabs",
                "This will edit and save prefab assets that contain BossControllerBase wiring. Continue?",
                "Auto Fix Prefabs",
                "Cancel"))
        {
            return;
        }

        results.Clear();
        AutoFixStats totalStats = new AutoFixStats();
        foreach (string path in FindAssetPaths("t:Prefab"))
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(path);
            try
            {
                AutoFixStats stats = AutoFixBossObject(path, prefabRoot);
                if (!stats.HasChanges)
                    continue;

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                stats.PrefabsSaved++;
                totalStats.Add(stats);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        AssetDatabase.SaveAssets();
        AddAutoFixSummary("Boss prefab auto fix complete.", totalStats);
        ValidatePrefabs();
    }

    private void AutoFixAllScenes()
    {
        if (!EditorUtility.DisplayDialog(
                "Auto Fix All Scenes",
                "This will open, edit, and save scene assets that contain BossControllerBase wiring. Continue?",
                "Auto Fix All Scenes",
                "Cancel"))
        {
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        results.Clear();
        string[] scenePaths = FindScenePaths();
        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
        AutoFixStats totalStats = new AutoFixStats();

        try
        {
            foreach (string scenePath in scenePaths)
            {
                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                AutoFixStats stats = AutoFixSceneObjects(scene);
                if (!stats.HasChanges)
                    continue;

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                stats.ScenesSaved++;
                totalStats.Add(stats);
            }
        }
        finally
        {
            if (originalSetup != null && originalSetup.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
        }

        AddAutoFixSummary("All scene auto fix complete.", totalStats);
        ValidateScenes();
    }

    private void ValidateLegacyBossRewardAdapterGuidReferences()
    {
        foreach (string path in FindSerializedAssetPaths())
        {
            string text = TryReadText(path);
            if (string.IsNullOrEmpty(text) ||
                text.IndexOf(LegacyBossRewardAdapterScriptGuid, StringComparison.Ordinal) < 0)
            {
                continue;
            }

            AddResult(
                path,
                Severity.Error,
                "Legacy boss reward adapter script GUID is still serialized in this asset. Remove the component reference before completing migration.",
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path),
                string.Empty);
        }
    }

    private void ValidateDeletedBattleEndDefinitionReferences()
    {
        foreach (string path in FindSerializedAssetPaths())
        {
            string text = TryReadText(path);
            if (string.IsNullOrEmpty(text))
                continue;

            if (text.IndexOf(DeletedBattleEndDefinitionScriptGuid, StringComparison.Ordinal) >= 0 ||
                text.IndexOf(DeprecatedDefinitionTypeName, StringComparison.Ordinal) >= 0 ||
                text.IndexOf(DeprecatedRouteDefinitionFieldName, StringComparison.Ordinal) >= 0 ||
                text.IndexOf(DeprecatedPortalOffsetFieldName, StringComparison.Ordinal) >= 0 ||
                text.IndexOf(DeprecatedRewardProfileTypeName, StringComparison.Ordinal) >= 0 ||
                text.IndexOf(DeprecatedRewardProfileFieldName, StringComparison.Ordinal) >= 0)
            {
                AddResult(
                    path,
                    Severity.Warning,
                    "Deleted battle-end definition, reward profile, or portal offset data is still serialized here. Remove stale fields and use BossSpecialRewardPreset plus scene-authored anchors instead.",
                    AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path),
                    string.Empty);
            }
        }
    }

    private void ValidatePrefabCatalogAssets()
    {
        string[] catalogPaths = FindAssetPaths("t:BossBattleEndPrefabCatalogSO");
        if (catalogPaths.Length == 0)
        {
            AddResult(
                DefaultPrefabCatalogPath,
                Severity.Error,
                "No BossBattleEndPrefabCatalogSO asset was found. Boss reward/portal components need a common prefab catalog.",
                null,
                string.Empty);
            return;
        }

        foreach (string path in catalogPaths)
        {
            BossBattleEndPrefabCatalogSO catalog = AssetDatabase.LoadAssetAtPath<BossBattleEndPrefabCatalogSO>(path);
            if (catalog == null)
                continue;

            SerializedObject serializedCatalog = new SerializedObject(catalog);
            ValidateObjectReference(path, catalog, serializedCatalog, "treasureChestPrefab", Severity.Error, "BossBattleEndPrefabCatalogSO.treasureChestPrefab is missing.");
            ValidateObjectReference(path, catalog, serializedCatalog, "magicStonePrefab", Severity.Error, "BossBattleEndPrefabCatalogSO.magicStonePrefab is missing.");
            ValidateObjectReference(path, catalog, serializedCatalog, "portalPrefab", Severity.Error, "BossBattleEndPrefabCatalogSO.portalPrefab is missing.");
        }
    }

    private void ValidateRouteSets()
    {
        foreach (string path in FindAssetPaths("t:CorridorBossRouteSetSO"))
        {
            CorridorBossRouteSetSO routeSet = AssetDatabase.LoadAssetAtPath<CorridorBossRouteSetSO>(path);
            if (routeSet == null)
                continue;

            if (routeSet.BossSpecialRewardPreset == null)
            {
                AddResult(
                    path,
                    Severity.Info,
                    "RouteSet has no BossSpecialRewardPresetSO. This is valid when the boss only uses base stage rewards and runtime modifiers.",
                    routeSet,
                    routeSet.name);
            }
        }
    }

    private void ValidatePrefabs()
    {
        foreach (string path in FindAssetPaths("t:Prefab"))
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                continue;

            ValidateBossObject(path, prefab.transform);
        }
    }

    private void ValidateScenes()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        string[] scenePaths = FindScenePaths();
        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach (string scenePath in scenePaths)
            {
                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                ValidateSceneObjects(scenePath, scene);
            }
        }
        finally
        {
            if (originalSetup != null && originalSetup.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
        }
    }

    private void ValidateSceneObjects(string scenePath, Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            ValidateBossObject(scenePath, root.transform);
    }

    private void ValidateBossObject(string path, Transform root)
    {
        if (root == null)
            return;

        foreach (BossControllerBase boss in root.GetComponentsInChildren<BossControllerBase>(true))
            ValidateBossBattleEndComponents(path, boss);
    }

    private void ValidateBossBattleEndComponents(string path, BossControllerBase boss)
    {
        if (boss == null)
            return;

        BossRewardSpawner rewardSpawner = boss.GetComponentInChildren<BossRewardSpawner>(true)
            ?? boss.GetComponentInParent<BossRewardSpawner>();
        BossExitPortalActivator portalActivator = boss.GetComponentInChildren<BossExitPortalActivator>(true)
            ?? boss.GetComponentInParent<BossExitPortalActivator>();
        BossBattleEndAnchors anchors = boss.GetComponentInChildren<BossBattleEndAnchors>(true)
            ?? boss.GetComponentInParent<BossBattleEndAnchors>();

        if (rewardSpawner == null)
        {
            AddResult(path, Severity.Warning, "Boss has no nearby BossRewardSpawner. Boss rewards will not be handled.", boss, GetObjectPath(boss.transform));
        }
        else
        {
            SerializedObject serializedSpawner = new SerializedObject(rewardSpawner);
            ValidateObjectReference(path, rewardSpawner, serializedSpawner, "prefabCatalog", Severity.Error, "BossRewardSpawner.prefabCatalog is missing.");
        }

        if (portalActivator == null)
        {
            AddResult(path, Severity.Warning, "Boss has no nearby BossExitPortalActivator. Boss exit portal will not be handled.", boss, GetObjectPath(boss.transform));
        }
        else
        {
            SerializedObject serializedPortal = new SerializedObject(portalActivator);
            ValidateObjectReference(path, portalActivator, serializedPortal, "prefabCatalog", Severity.Error, "BossExitPortalActivator.prefabCatalog is missing.");
        }

        if (anchors == null)
        {
            AddResult(path, Severity.Error, "Boss has no nearby BossBattleEndAnchors. Reward and dynamically spawned portal placement must be scene/prefab authored.", boss, GetObjectPath(boss.transform));
            return;
        }

        SerializedObject serializedAnchors = new SerializedObject(anchors);
        ValidateObjectReference(path, anchors, serializedAnchors, "rewardSpawnPoint", Severity.Error, "BossBattleEndAnchors.rewardSpawnPoint is missing. Boss rewards require an authored reward anchor.");
        ValidateObjectReference(path, anchors, serializedAnchors, "scatterOrigin", Severity.Info, "BossBattleEndAnchors.scatterOrigin is missing. Reward scatter will use rewardSpawnPoint.");

        bool hasScenePortal = portalActivator != null && HasObjectReference(new SerializedObject(portalActivator), "portalObj");
        Severity portalAnchorSeverity = hasScenePortal ? Severity.Info : Severity.Error;
        string portalAnchorMessage = hasScenePortal
            ? "BossBattleEndAnchors.portalSpawnPoint is missing. The authored scene portal will keep its existing position."
            : "BossBattleEndAnchors.portalSpawnPoint is missing. Catalog portal prefab spawning requires an authored portal anchor.";
        ValidateObjectReference(path, anchors, serializedAnchors, "portalSpawnPoint", portalAnchorSeverity, portalAnchorMessage);
    }

    private AutoFixStats AutoFixSceneObjects(Scene scene)
    {
        AutoFixStats totalStats = new AutoFixStats();
        foreach (GameObject root in scene.GetRootGameObjects())
            totalStats.Add(AutoFixBossObject(scene.path, root));

        return totalStats;
    }

    private AutoFixStats AutoFixBossObject(string path, GameObject root)
    {
        AutoFixStats totalStats = new AutoFixStats();
        if (root == null)
            return totalStats;

        foreach (BossControllerBase boss in root.GetComponentsInChildren<BossControllerBase>(true))
            totalStats.Add(AutoFixBoss(path, boss));

        return totalStats;
    }

    private AutoFixStats AutoFixBoss(string path, BossControllerBase boss)
    {
        AutoFixStats stats = new AutoFixStats();
        if (boss == null)
            return stats;

        BossBattleEndPrefabCatalogSO catalog = EnsureDefaultPrefabCatalog(stats);
        GameObject owner = boss.gameObject;
        BossRewardSpawner spawner = GetOrAddComponent<BossRewardSpawner>(owner, stats);
        BossExitPortalActivator portalActivator = GetOrAddComponent<BossExitPortalActivator>(owner, stats);
        BossBattleEndAnchors anchors = GetOrAddComponent<BossBattleEndAnchors>(owner, stats);

        bool changedCatalogReferences = false;
        changedCatalogReferences |= AssignCatalogIfMissing(spawner, catalog);
        changedCatalogReferences |= AssignCatalogIfMissing(portalActivator, catalog);
        if (changedCatalogReferences)
            stats.ReferencesAssigned++;

        SerializedObject serializedAnchors = new SerializedObject(anchors);
        bool changedAnchors = false;
        Transform rewardAnchor = GetOrCreateAnchor(owner.transform, "BossRewardSpawnPoint", owner.transform.position, stats);
        Transform portalAnchor = GetOrCreateAnchor(owner.transform, "BossPortalSpawnPoint", owner.transform.position, stats);
        changedAnchors |= SetSerializedReferenceIfMissing(serializedAnchors, "rewardSpawnPoint", rewardAnchor);
        changedAnchors |= SetSerializedReferenceIfMissing(serializedAnchors, "scatterOrigin", rewardAnchor);
        changedAnchors |= SetSerializedReferenceIfMissing(serializedAnchors, "portalSpawnPoint", portalAnchor);
        if (changedAnchors)
        {
            serializedAnchors.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(anchors);
            stats.ReferencesAssigned++;
        }

        if (stats.HasChanges)
        {
            EditorUtility.SetDirty(spawner);
            EditorUtility.SetDirty(portalActivator);
            AddResult(path, Severity.Info, "Auto Fix ensured BossRewardSpawner, BossExitPortalActivator, BossBattleEndAnchors, and common prefab catalog references exist. Review anchor placement in the Inspector.", boss, GetObjectPath(boss.transform));
        }

        return stats;
    }

    private static BossBattleEndPrefabCatalogSO EnsureDefaultPrefabCatalog(AutoFixStats stats)
    {
        BossBattleEndPrefabCatalogSO catalog = AssetDatabase.LoadAssetAtPath<BossBattleEndPrefabCatalogSO>(DefaultPrefabCatalogPath);
        if (catalog == null)
        {
            EnsureAssetFolder(Path.GetDirectoryName(DefaultPrefabCatalogPath)?.Replace('\\', '/'));
            catalog = CreateInstance<BossBattleEndPrefabCatalogSO>();
            AssetDatabase.CreateAsset(catalog, DefaultPrefabCatalogPath);
            if (stats != null)
                stats.AssetsCreated++;
        }

        SerializedObject serializedCatalog = new SerializedObject(catalog);
        bool changed = false;
        changed |= SetSerializedReferenceIfMissing(serializedCatalog, "treasureChestPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(DefaultTreasureChestPrefabPath));
        changed |= SetSerializedReferenceIfMissing(serializedCatalog, "magicStonePrefab", AssetDatabase.LoadAssetAtPath<GameObject>(DefaultMagicStonePrefabPath));
        changed |= SetSerializedReferenceIfMissing(serializedCatalog, "portalPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(DefaultPortalPrefabPath));
        if (changed)
        {
            serializedCatalog.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            if (stats != null)
                stats.ReferencesAssigned++;
        }

        return catalog;
    }

    private static bool AssignCatalogIfMissing(Component target, BossBattleEndPrefabCatalogSO catalog)
    {
        if (target == null || catalog == null)
            return false;

        SerializedObject serializedObject = new SerializedObject(target);
        bool changed = SetSerializedReferenceIfMissing(serializedObject, "prefabCatalog", catalog);
        if (!changed)
            return false;

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
        return true;
    }

    private void ValidateObjectReference(string path, UnityEngine.Object context, SerializedObject serializedObject, string propertyName, Severity severity, string message)
    {
        if (HasObjectReference(serializedObject, propertyName))
            return;

        AddResult(path, severity, message, context, GetContextPath(context));
    }

    private static bool HasObjectReference(SerializedObject serializedObject, string propertyName)
    {
        if (serializedObject == null)
            return false;

        SerializedProperty property = serializedObject.FindProperty(propertyName);
        return property != null && property.objectReferenceValue != null;
    }

    private void AddResult(string path, Severity severity, string message, UnityEngine.Object context, string objectPath)
    {
        results.Add(new ValidationResult
        {
            Path = string.IsNullOrEmpty(path) ? "Project" : path,
            SeverityLevel = severity,
            Message = message,
            Context = context,
            ObjectPath = objectPath ?? string.Empty
        });
    }

    private void AddAutoFixSummary(string title, AutoFixStats stats)
    {
        string message = stats != null && stats.HasChanges
            ? $"{title} AssetsCreated={stats.AssetsCreated}, ComponentsAdded={stats.ComponentsAdded}, AnchorsCreated={stats.AnchorsCreated}, ReferencesAssigned={stats.ReferencesAssigned}, PrefabsSaved={stats.PrefabsSaved}, ScenesSaved={stats.ScenesSaved}."
            : $"{title} No changes were needed.";

        AddResult("Auto Fix", Severity.Info, message, null, string.Empty);
    }

    private static T GetOrAddComponent<T>(GameObject owner, AutoFixStats stats) where T : Component
    {
        T component = owner.GetComponent<T>();
        if (component != null)
            return component;

        component = Undo.AddComponent<T>(owner);
        EditorUtility.SetDirty(owner);
        if (stats != null)
            stats.ComponentsAdded++;

        return component;
    }

    private static Transform GetOrCreateAnchor(Transform parent, string anchorName, Vector3 worldPosition, AutoFixStats stats)
    {
        Transform existing = parent.Find(anchorName);
        if (existing != null)
            return existing;

        var anchor = new GameObject(anchorName);
        Undo.RegisterCreatedObjectUndo(anchor, $"Create {anchorName}");
        anchor.transform.SetParent(parent, true);
        anchor.transform.position = worldPosition;
        EditorUtility.SetDirty(anchor);
        EditorUtility.SetDirty(parent.gameObject);
        if (stats != null)
            stats.AnchorsCreated++;

        return anchor.transform;
    }

    private static bool SetSerializedReferenceIfMissing(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
    {
        if (value == null)
            return false;

        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null || property.objectReferenceValue != null)
            return false;

        property.objectReferenceValue = value;
        return true;
    }

    private static void EnsureAssetFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            return;

        string[] parts = folderPath.Split('/');
        if (parts.Length == 0 || !string.Equals(parts[0], "Assets", StringComparison.Ordinal))
            return;

        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);

            current = next;
        }
    }

    private static string[] FindAssetPaths(string filter)
    {
        return AssetDatabase.FindAssets(filter, new[] { "Assets" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => !string.IsNullOrEmpty(path))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] FindScenePaths()
    {
        return FindAssetPaths("t:SceneAsset")
            .Where(path => path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private static string[] FindSerializedAssetPaths()
    {
        return AssetDatabase.FindAssets(string.Empty, new[] { "Assets" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path =>
                path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
    }

    private static string TryReadText(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (IOException)
        {
            return string.Empty;
        }
        catch (UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    private static string GetContextPath(UnityEngine.Object context)
    {
        return context switch
        {
            Component component => GetObjectPath(component.transform),
            GameObject gameObject => GetObjectPath(gameObject.transform),
            ScriptableObject scriptableObject => scriptableObject.name,
            _ => string.Empty
        };
    }

    private static string GetObjectPath(Transform transform)
    {
        if (transform == null)
            return string.Empty;

        List<string> parts = new List<string>();
        Transform current = transform;
        while (current != null)
        {
            parts.Add(current.name);
            current = current.parent;
        }

        parts.Reverse();
        return string.Join("/", parts);
    }
}
