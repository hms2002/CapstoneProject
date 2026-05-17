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
    private const string DeletedBossRewardSpawnerScriptGuid = "73a45414134245b49d9f61de3dff0e94";
    private const string DeletedBossExitPortalActivatorScriptGuid = "58b385d66ce4413a802c458a0e883b5e";
    private const string DeletedBossBattleEndAnchorsScriptGuid = "b06f9366df614c44ab34d56e96a94e63";
    private const string DeletedBattleEndPrefabCatalogScriptGuid = "f4a9c1e9db2d4d4fa7b9f8a1d3c6b520";
    private const string DeletedBattleEndPrefabCatalogAssetGuid = "a38103ce9c534763ba870c9573d7df68";
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
        public int ObjectsCreated;
        public int ReferencesAssigned;
        public int PrefabsSaved;
        public int ScenesSaved;

        public bool HasChanges =>
            AssetsCreated > 0 ||
            ComponentsAdded > 0 ||
            ObjectsCreated > 0 ||
            ReferencesAssigned > 0 ||
            PrefabsSaved > 0 ||
            ScenesSaved > 0;

        public void Add(AutoFixStats other)
        {
            if (other == null)
                return;

            AssetsCreated += other.AssetsCreated;
            ComponentsAdded += other.ComponentsAdded;
            ObjectsCreated += other.ObjectsCreated;
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
            if (GUILayout.Button("Auto Fix Active Scene", EditorStyles.toolbarButton))
                AutoFixActiveScene();

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
        ValidateDeletedBattleEndComponentReferences();
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
                    "Deleted battle-end definition, reward profile, or portal offset data is still serialized here. Remove stale fields and use BossSpecialRewardPreset plus scene-authored chest/portal activation instead.",
                    AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path),
                    string.Empty);
            }
        }
    }

    private void ValidateDeletedBattleEndComponentReferences()
    {
        foreach (string path in FindSerializedAssetPaths())
        {
            string text = TryReadText(path);
            if (string.IsNullOrEmpty(text))
                continue;

            if (text.IndexOf(DeletedBossRewardSpawnerScriptGuid, StringComparison.Ordinal) >= 0 ||
                text.IndexOf(DeletedBossExitPortalActivatorScriptGuid, StringComparison.Ordinal) >= 0 ||
                text.IndexOf(DeletedBossBattleEndAnchorsScriptGuid, StringComparison.Ordinal) >= 0 ||
                text.IndexOf(DeletedBattleEndPrefabCatalogScriptGuid, StringComparison.Ordinal) >= 0 ||
                text.IndexOf(DeletedBattleEndPrefabCatalogAssetGuid, StringComparison.Ordinal) >= 0)
            {
                AddResult(
                    path,
                    Severity.Error,
                    "Deleted boss battle-end component/catalog data is still serialized here. Remove BossRewardSpawner, BossExitPortalActivator, BossBattleEndAnchors, and BossBattleEndPrefabCatalog references.",
                    AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path),
                    string.Empty);
            }
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

            ValidateBossBattleEndAuthoring(path, new[] { prefab }, false);
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
        ValidateBossBattleEndAuthoring(scenePath, scene.GetRootGameObjects(), true);
        ValidateScenePortalSemantics(scenePath, scene);
    }

    private void ValidateScenePortalSemantics(string scenePath, Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (ScenePortal portal in root.GetComponentsInChildren<ScenePortal>(true))
                ValidateScenePortalSemantic(scenePath, portal);
        }
    }

    private void ValidateBossBattleEndAuthoring(
        string path,
        IReadOnlyList<GameObject> roots,
        bool requireBossHandlerCoverage)
    {
        if (roots == null)
            return;

        List<BossControllerBase> bosses = CollectComponents<BossControllerBase>(roots);
        List<BossBattleEndHandler> handlers = CollectComponents<BossBattleEndHandler>(roots);

        for (int i = 0; i < handlers.Count; i++)
            ValidateBossBattleEndHandler(path, handlers[i]);

        if (requireBossHandlerCoverage)
        {
            for (int i = 0; i < bosses.Count; i++)
                ValidateBossHandlerCoverage(path, bosses[i], handlers);
        }
    }

    private void ValidateBossHandlerCoverage(
        string path,
        BossControllerBase boss,
        IReadOnlyList<BossBattleEndHandler> handlers)
    {
        if (boss == null)
            return;

        int matchCount = 0;
        BossBattleEndHandler firstMatch = null;
        for (int i = 0; i < handlers.Count; i++)
        {
            BossBattleEndHandler handler = handlers[i];
            if (handler == null)
                continue;

            if (!ReferenceEquals(GetHandlerBoss(handler), boss))
                continue;

            firstMatch ??= handler;
            matchCount++;
        }

        if (matchCount == 0)
        {
            AddResult(
                path,
                Severity.Error,
                "Boss has no BossBattleEndHandler referencing it. Boss reward and portal handling should be authored on a scene BattleEnd object.",
                boss,
                GetObjectPath(boss.transform));
            return;
        }

        if (matchCount > 1)
        {
            AddResult(
                path,
                Severity.Error,
                "Multiple BossBattleEndHandler components reference the same boss. This can duplicate reward or portal handling.",
                firstMatch,
                firstMatch != null ? GetObjectPath(firstMatch.transform) : GetObjectPath(boss.transform));
        }
    }

    private void ValidateBossBattleEndHandler(string path, BossBattleEndHandler handler)
    {
        if (handler == null)
            return;

        SerializedObject serializedHandler = new SerializedObject(handler);
        bool isFinalRouteBossScene = IsFinalRouteBossScene(path);
        ValidateObjectReference(path, handler, serializedHandler, "boss", Severity.Error, "BossBattleEndHandler.boss is missing.");
        if (!isFinalRouteBossScene)
            ValidateObjectReference(path, handler, serializedHandler, "treasureChest", Severity.Error, "BossBattleEndHandler.treasureChest is missing. Boss chest rewards require an authored inactive TreasureChest object.");
        ValidateObjectReference(path, handler, serializedHandler, "exitPortal", Severity.Error, "BossBattleEndHandler.exitPortal is missing. Boss exit portals must be authored scene objects and activated at battle end.");

        ValidatePortalObjectSemantic(
            path,
            GetObjectReference<GameObject>(serializedHandler, "exitPortal"),
            "BossBattleEndHandler.exitPortal");
    }

    private static bool IsFinalRouteBossScene(string path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            !path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string sceneName = Path.GetFileNameWithoutExtension(path);
        if (string.IsNullOrWhiteSpace(sceneName))
            return false;

        foreach (string catalogPath in FindAssetPaths("t:RunRouteCatalogSO"))
        {
            RunRouteCatalogSO catalog = AssetDatabase.LoadAssetAtPath<RunRouteCatalogSO>(catalogPath);
            CorridorBossRouteSetSO finalRouteSet = catalog != null ? catalog.FinalRouteSet : null;
            string finalBossSceneName = finalRouteSet != null ? finalRouteSet.BossSceneName : null;
            if (string.Equals(finalBossSceneName, sceneName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private void ValidateScenePortalSemantic(string path, ScenePortal portal)
    {
        if (portal == null)
            return;

        if (portal.PortalTransitionType == TransitionType.HubToRunStart)
        {
            if (portal.StartRunRouteCatalog == null)
            {
                AddResult(
                    path,
                    Severity.Error,
                    "Hub start ScenePortal is missing StartRunRouteCatalog. Hub portals must own the RunRouteCatalogSO after the common portal prefab became semantic-neutral.",
                    portal,
                    GetObjectPath(portal.transform));
            }

            return;
        }

        if (portal.StartRunRouteCatalog != null)
        {
            AddResult(
                path,
                Severity.Warning,
                "Non-hub ScenePortal carries StartRunRouteCatalog. Clear it so RunRouteCatalog remains owned by hub-start portals only.",
                portal,
                GetObjectPath(portal.transform));
        }
    }

    private void ValidatePortalObjectSemantic(string path, GameObject portalObject, string ownerLabel)
    {
        if (portalObject == null)
            return;

        ScenePortal[] portals = portalObject.GetComponentsInChildren<ScenePortal>(true);
        for (int i = 0; i < portals.Length; i++)
        {
            ScenePortal portal = portals[i];
            if (portal == null)
                continue;

            if (portal.PortalTransitionType == TransitionType.HubToRunStart)
            {
                AddResult(
                    path,
                    Severity.Error,
                    $"{ownerLabel} is configured as HubToRunStart. Boss exit portals should be None, BossToCorridor, or ReturnToHubAfterRun.",
                    portal,
                    GetObjectPath(portal.transform));
            }

            if (portal.StartRunRouteCatalog != null)
            {
                AddResult(
                    path,
                    Severity.Error,
                    $"{ownerLabel} carries StartRunRouteCatalog. Boss exit portals should route from the active run plan, not a direct catalog reference.",
                    portal,
                    GetObjectPath(portal.transform));
            }
        }
    }

    private static List<T> CollectComponents<T>(IReadOnlyList<GameObject> roots)
        where T : Component
    {
        var results = new List<T>();
        if (roots == null)
            return results;

        for (int i = 0; i < roots.Count; i++)
        {
            GameObject root = roots[i];
            if (root == null)
                continue;

            results.AddRange(root.GetComponentsInChildren<T>(true));
        }

        return results;
    }

    private static BossControllerBase GetHandlerBoss(BossBattleEndHandler handler)
    {
        if (handler == null)
            return null;

        SerializedObject serializedHandler = new SerializedObject(handler);
        return GetObjectReference<BossControllerBase>(serializedHandler, "boss");
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

        GameObject owner = ResolveBattleEndOwner(path, boss, stats);
        BossBattleEndHandler handler = FindHandlerForBossInScene(boss) ?? GetOrAddComponent<BossBattleEndHandler>(owner, stats);

        SerializedObject serializedHandler = new SerializedObject(handler);
        bool changedHandler = false;
        changedHandler |= SetSerializedReferenceIfMissing(serializedHandler, "boss", boss);
        if (changedHandler)
        {
            serializedHandler.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(handler);
            stats.ReferencesAssigned++;
        }

        if (stats.HasChanges)
        {
            EditorUtility.SetDirty(handler);
            AddResult(path, Severity.Info, "Auto Fix ensured a scene BossBattleEndHandler and boss reference exist. Assign exitPortal manually, and assign TreasureChest for non-final boss routes.", boss, GetObjectPath(handler.transform));
        }

        return stats;
    }

    private static GameObject ResolveBattleEndOwner(string path, BossControllerBase boss, AutoFixStats stats)
    {
        if (boss == null)
            return null;

        if (!string.IsNullOrEmpty(path) &&
            path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) &&
            boss.gameObject.scene.IsValid())
        {
            string ownerName = $"{boss.name}_BattleEnd";
            foreach (GameObject root in boss.gameObject.scene.GetRootGameObjects())
            {
                if (root != null && string.Equals(root.name, ownerName, StringComparison.Ordinal))
                    return root;
            }

            var owner = new GameObject(ownerName);
            Undo.RegisterCreatedObjectUndo(owner, $"Create {ownerName}");
            SceneManager.MoveGameObjectToScene(owner, boss.gameObject.scene);
            EditorUtility.SetDirty(owner);
            if (stats != null)
                stats.ObjectsCreated++;

            return owner;
        }

        return boss.gameObject;
    }

    private static BossBattleEndHandler FindHandlerForBossInScene(BossControllerBase boss)
    {
        if (boss == null)
            return null;

        BossBattleEndHandler[] handlers = FindObjectsByType<BossBattleEndHandler>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < handlers.Length; i++)
        {
            BossBattleEndHandler handler = handlers[i];
            if (handler == null || handler.gameObject.scene != boss.gameObject.scene)
                continue;

            if (ReferenceEquals(GetHandlerBoss(handler), boss))
                return handler;
        }

        return null;
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

    private static T GetObjectReference<T>(SerializedObject serializedObject, string propertyName)
        where T : UnityEngine.Object
    {
        if (serializedObject == null)
            return null;

        SerializedProperty property = serializedObject.FindProperty(propertyName);
        return property != null ? property.objectReferenceValue as T : null;
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
            ? $"{title} AssetsCreated={stats.AssetsCreated}, ComponentsAdded={stats.ComponentsAdded}, ObjectsCreated={stats.ObjectsCreated}, ReferencesAssigned={stats.ReferencesAssigned}, PrefabsSaved={stats.PrefabsSaved}, ScenesSaved={stats.ScenesSaved}."
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
