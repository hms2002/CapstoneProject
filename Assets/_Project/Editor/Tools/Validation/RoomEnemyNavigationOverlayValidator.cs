using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RoomEnemyNavigationOverlayValidator
{
    private const string ArrowPrefabPath = "Assets/_Project/Prefabs/Map/Navigation/KillLockMonsterNavigationArrow.prefab";
    private const string RouteCatalogPath = "Assets/_Project/Data/SceneFlow/Routes/RunRouteCatalog.asset";
    private const string OverlayRootName = "RoomEnemyNavigationOverlay";
    private const int DefaultShowThreshold = 4;
    private const float DefaultViewportPadding = 0.08f;

    // Scene infrastructure: keep outside the generated room roots so rebuilding rooms cannot remove it.
    public static void EnsureProceduralSceneOverlay(Scene scene)
    {
        if (FindSceneComponents<DungeonGenerator>(scene, includeInactive: true).Count == 0)
            throw new System.InvalidOperationException($"Not a procedural dungeon scene: {scene.path}");

        List<RoomEnemyNavigationOverlay> overlays = FindSceneComponents<RoomEnemyNavigationOverlay>(scene, true);
        if (overlays.Count > 1)
            throw new System.InvalidOperationException($"Duplicate room navigation overlays in {scene.path}; resolve them before wiring.");

        GameObject arrowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArrowPrefabPath);
        if (arrowPrefab == null)
            throw new System.InvalidOperationException($"Missing arrow prefab: {ArrowPrefabPath}");

        RoomEnemyNavigationOverlay overlay = EnsureSingleOverlay(scene);
        if (overlay.transform.parent != null)
            throw new System.InvalidOperationException($"Room navigation must be a scene root: {scene.path}");
        WireOverlay(overlay, arrowPrefab);
        overlay.enabled = true;
        overlay.gameObject.SetActive(true);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    [MenuItem("Tools/Validation/Install Navigation In Procedural Corridor Scenes")]
    public static void InstallProceduralCorridorScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new System.InvalidOperationException("Exit Play Mode before wiring navigation.");

        int installed = 0;
        foreach (string path in CollectRouteCatalogCorridorScenePaths())
        {
            Scene scene = FindLoadedSceneByPath(path);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (!openedHere && scene.isDirty)
                throw new System.InvalidOperationException($"Save scene edits first: {path}");
            if (openedHere)
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                if (FindSceneComponents<DungeonGenerator>(scene, true).Count == 0)
                    continue;
                EnsureProceduralSceneOverlay(scene);
                if (!EditorSceneManager.SaveScene(scene))
                    throw new System.InvalidOperationException($"Could not save scene: {path}");
                installed++;
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }
        if (installed == 0)
            throw new System.InvalidOperationException("No procedural Corridor scenes were resolved from the route catalog.");
        Debug.Log($"[RoomEnemyNavigationOverlayValidator] Installed procedural navigation: scenes={installed}.");
    }

    public static void InstallProceduralCorridorScenesBatch()
    {
        try { InstallProceduralCorridorScenes(); }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    [MenuItem("Tools/Validation/Validate Room Enemy Navigation Overlay")]
    public static void ValidateOpenScenes()
    {
        GameObject expectedArrowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArrowPrefabPath);
        int scenesChecked = 0;
        int issueCount = expectedArrowPrefab == null ? 1 : 0;

        if (expectedArrowPrefab == null)
            Debug.LogError($"[RoomEnemyNavigationOverlayValidator] Arrow prefab not found at '{ArrowPrefabPath}'.");

        foreach (Scene scene in GetLoadedScenes())
        {
            scenesChecked++;
            issueCount += ValidateScene(scene, expectedArrowPrefab);
        }

        Debug.Log($"[RoomEnemyNavigationOverlayValidator] Validation complete. scenes={scenesChecked}, issues={issueCount}");
    }

    [MenuItem("Tools/Validation/Auto Wire Room Enemy Navigation Overlay In Open Scenes")]
    public static void AutoWireOpenScenes()
    {
        GameObject arrowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArrowPrefabPath);
        if (arrowPrefab == null)
        {
            Debug.LogError($"[RoomEnemyNavigationOverlayValidator] Cannot auto-wire because the arrow prefab was not found at '{ArrowPrefabPath}'.");
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Auto Wire Room Enemy Navigation Overlay");

        int wiredScenes = 0;
        foreach (Scene scene in GetLoadedScenes())
        {
            if (!SceneNeedsOverlay(scene))
                continue;

            RoomEnemyNavigationOverlay overlay = EnsureSingleOverlay(scene);
            if (overlay == null)
                continue;

            WireOverlay(overlay, arrowPrefab);
            EditorSceneManager.MarkSceneDirty(scene);
            wiredScenes++;
        }

        Undo.CollapseUndoOperations(undoGroup);
        Debug.Log($"[RoomEnemyNavigationOverlayValidator] Auto-wire complete. wiredScenes={wiredScenes}");
        ValidateOpenScenes();
    }

    [MenuItem("Tools/Validation/Validate Room Enemy Navigation Overlay In Route Catalog Corridor Scenes")]
    public static void ValidateRouteCatalogCorridorScenes()
    {
        ProcessRouteCatalogCorridorScenes(autoWire: false);
    }

    [MenuItem("Tools/Validation/Auto Wire Room Enemy Navigation Overlay In Route Catalog Corridor Scenes")]
    public static void AutoWireRouteCatalogCorridorScenes()
    {
        ProcessRouteCatalogCorridorScenes(autoWire: true);
    }

    private static void ProcessRouteCatalogCorridorScenes(bool autoWire)
    {
        GameObject arrowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArrowPrefabPath);
        if (arrowPrefab == null)
        {
            Debug.LogError($"[RoomEnemyNavigationOverlayValidator] Arrow prefab not found at '{ArrowPrefabPath}'.");
            return;
        }

        List<string> scenePaths = CollectRouteCatalogCorridorScenePaths();
        if (scenePaths.Count == 0)
        {
            Debug.LogWarning($"[RoomEnemyNavigationOverlayValidator] No route catalog corridor scenes were resolved from '{RouteCatalogPath}'.");
            return;
        }

        int sceneCount = 0;
        int issueCount = 0;
        int wiredCount = 0;
        for (int i = 0; i < scenePaths.Count; i++)
        {
            string scenePath = scenePaths[i];
            Scene scene = FindLoadedSceneByPath(scenePath);
            bool openedForValidation = !scene.IsValid() || !scene.isLoaded;

            if (openedForValidation)
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            try
            {
                sceneCount++;
                if (autoWire && SceneNeedsOverlay(scene))
                {
                    RoomEnemyNavigationOverlay overlay = EnsureSingleOverlay(scene);
                    WireOverlay(overlay, arrowPrefab);
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (openedForValidation)
                        EditorSceneManager.SaveScene(scene);
                    wiredCount++;
                }

                issueCount += ValidateScene(scene, arrowPrefab);
            }
            finally
            {
                if (openedForValidation && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }

        string action = autoWire ? "Auto-wire" : "Validation";
        Debug.Log($"[RoomEnemyNavigationOverlayValidator] Route catalog corridor {action} complete. scenes={sceneCount}, wired={wiredCount}, issues={issueCount}");
    }

    private static int ValidateScene(Scene scene, GameObject expectedArrowPrefab)
    {
        if (!SceneNeedsOverlay(scene))
        {
            Debug.Log($"[RoomEnemyNavigationOverlayValidator] {scene.path}: no procedural dungeon or room spawn/lock groups found; overlay not required.");
            return 0;
        }

        int issueCount = 0;
        List<RoomEnemyNavigationOverlay> overlays = FindSceneComponents<RoomEnemyNavigationOverlay>(scene, includeInactive: true);
        if (overlays.Count == 0)
        {
            Debug.LogWarning($"[RoomEnemyNavigationOverlayValidator] {scene.path}: missing RoomEnemyNavigationOverlay for room enemy navigation.");
            return 1;
        }

        if (overlays.Count > 1)
        {
            issueCount += overlays.Count - 1;
            for (int i = 0; i < overlays.Count; i++)
            {
                RoomEnemyNavigationOverlay overlay = overlays[i];
                Debug.LogWarning(
                    $"[RoomEnemyNavigationOverlayValidator] {scene.path}: duplicate RoomEnemyNavigationOverlay at '{GetObjectPath(overlay.transform)}'. Keep only one scene-level overlay.",
                    overlay);
            }
        }

        for (int i = 0; i < overlays.Count; i++)
        {
            issueCount += ValidateOverlay(scene, overlays[i], expectedArrowPrefab);
        }

        return issueCount;
    }

    private static int ValidateOverlay(Scene scene, RoomEnemyNavigationOverlay overlay, GameObject expectedArrowPrefab)
    {
        if (overlay == null)
            return 1;

        int issueCount = 0;
        SerializedObject serializedOverlay = new SerializedObject(overlay);
        if (!overlay.isActiveAndEnabled)
        {
            Debug.LogWarning($"[RoomEnemyNavigationOverlayValidator] {scene.path}: navigation overlay is inactive.", overlay);
            issueCount++;
        }
        SerializedProperty arrowPrefabProperty = serializedOverlay.FindProperty("arrowPrefab");
        SerializedProperty showThresholdProperty = serializedOverlay.FindProperty("showThreshold");
        SerializedProperty viewportPaddingProperty = serializedOverlay.FindProperty("viewportPadding");

        if (arrowPrefabProperty == null)
        {
            Debug.LogError($"[RoomEnemyNavigationOverlayValidator] {scene.path}: arrowPrefab serialized field was not found on overlay.", overlay);
            issueCount++;
        }
        else if (arrowPrefabProperty.objectReferenceValue == null)
        {
            Debug.LogWarning(
                $"[RoomEnemyNavigationOverlayValidator] {scene.path}: RoomEnemyNavigationOverlay at '{GetObjectPath(overlay.transform)}' has no arrow prefab assigned.",
                overlay);
            issueCount++;
        }
        else if (expectedArrowPrefab != null && arrowPrefabProperty.objectReferenceValue != expectedArrowPrefab)
        {
            Debug.LogWarning(
                $"[RoomEnemyNavigationOverlayValidator] {scene.path}: RoomEnemyNavigationOverlay at '{GetObjectPath(overlay.transform)}' uses a prefab other than '{ArrowPrefabPath}'.",
                overlay);
            issueCount++;
        }

        if (showThresholdProperty == null || showThresholdProperty.intValue < 1)
        {
            Debug.LogWarning(
                $"[RoomEnemyNavigationOverlayValidator] {scene.path}: RoomEnemyNavigationOverlay at '{GetObjectPath(overlay.transform)}' has an invalid showThreshold.",
                overlay);
            issueCount++;
        }

        if (viewportPaddingProperty == null || viewportPaddingProperty.floatValue < 0f || viewportPaddingProperty.floatValue > 0.45f)
        {
            Debug.LogWarning(
                $"[RoomEnemyNavigationOverlayValidator] {scene.path}: RoomEnemyNavigationOverlay at '{GetObjectPath(overlay.transform)}' has an invalid viewportPadding.",
                overlay);
            issueCount++;
        }

        return issueCount;
    }

    private static RoomEnemyNavigationOverlay EnsureSingleOverlay(Scene scene)
    {
        List<RoomEnemyNavigationOverlay> overlays = FindSceneComponents<RoomEnemyNavigationOverlay>(scene, includeInactive: true);
        if (overlays.Count > 0)
        {
            if (overlays.Count > 1)
            {
                Debug.LogWarning(
                    $"[RoomEnemyNavigationOverlayValidator] {scene.path}: multiple overlays exist; auto-wire updates the first and leaves duplicate cleanup to scene authoring.",
                    overlays[0]);
            }

            return overlays[0];
        }

        GameObject overlayObject = new GameObject(BuildUniqueRootName(scene, OverlayRootName));
        Undo.RegisterCreatedObjectUndo(overlayObject, "Create Room Enemy Navigation Overlay");
        SceneManager.MoveGameObjectToScene(overlayObject, scene);

        RoomEnemyNavigationOverlay overlay = Undo.AddComponent<RoomEnemyNavigationOverlay>(overlayObject);
        return overlay;
    }

    private static void WireOverlay(RoomEnemyNavigationOverlay overlay, GameObject arrowPrefab)
    {
        if (overlay == null)
            return;

        Undo.RecordObject(overlay, "Wire Room Enemy Navigation Overlay");
        SerializedObject serializedOverlay = new SerializedObject(overlay);

        AssignObjectReference(serializedOverlay, "arrowPrefab", arrowPrefab);
        AssignIntIfInvalid(serializedOverlay, "showThreshold", DefaultShowThreshold, minimumValue: 1);
        AssignFloatIfInvalid(serializedOverlay, "viewportPadding", DefaultViewportPadding, minimumValue: 0f, maximumValue: 0.45f);
        AssignVector2IfZero(serializedOverlay, "arrowVisualForward", Vector2.left);

        serializedOverlay.ApplyModifiedProperties();
        EditorUtility.SetDirty(overlay);
    }

    private static void AssignObjectReference(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private static void AssignIntIfInvalid(SerializedObject serializedObject, string propertyName, int value, int minimumValue)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null && property.intValue < minimumValue)
            property.intValue = value;
    }

    private static void AssignFloatIfInvalid(
        SerializedObject serializedObject,
        string propertyName,
        float value,
        float minimumValue,
        float maximumValue)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null && (property.floatValue < minimumValue || property.floatValue > maximumValue))
            property.floatValue = value;
    }

    private static void AssignVector2IfZero(SerializedObject serializedObject, string propertyName, Vector2 value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null && property.vector2Value.sqrMagnitude <= 0.0001f)
            property.vector2Value = value;
    }

    private static bool SceneNeedsOverlay(Scene scene)
    {
        return FindSceneComponents<DungeonGenerator>(scene, includeInactive: true).Count > 0
            || FindSceneComponents<MonsterSpawnRoomGroup>(scene, includeInactive: true).Count > 0
            || FindSceneComponents<RoomDoorMonsterKillLock>(scene, includeInactive: true).Count > 0;
    }

    private static IEnumerable<Scene> GetLoadedScenes()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.IsValid() && scene.isLoaded)
                yield return scene;
        }
    }

    private static List<string> CollectRouteCatalogCorridorScenePaths()
    {
        List<string> scenePaths = new List<string>();
        RunRouteCatalogSO catalog = AssetDatabase.LoadAssetAtPath<RunRouteCatalogSO>(RouteCatalogPath);
        if (catalog == null)
        {
            Debug.LogError($"[RoomEnemyNavigationOverlayValidator] RunRouteCatalog asset not found at '{RouteCatalogPath}'.");
            return scenePaths;
        }

        IReadOnlyList<CorridorBossRouteSetSO> normalRouteSets = catalog.NormalRouteSets;
        if (normalRouteSets != null)
        {
            for (int i = 0; i < normalRouteSets.Count; i++)
                AddCorridorScenePath(normalRouteSets[i], scenePaths);
        }

        AddCorridorScenePath(catalog.FinalRouteSet, scenePaths);
        return scenePaths;
    }

    private static void AddCorridorScenePath(CorridorBossRouteSetSO routeSet, List<string> scenePaths)
    {
        if (routeSet == null || string.IsNullOrWhiteSpace(routeSet.CorridorSceneName))
            return;

        if (!TryResolveSceneAssetPath(routeSet.CorridorSceneName, out string scenePath))
        {
            Debug.LogError($"[RoomEnemyNavigationOverlayValidator] Corridor scene '{routeSet.CorridorSceneName}' could not be resolved.");
            return;
        }

        if (!scenePaths.Contains(scenePath))
            scenePaths.Add(scenePath);
    }

    private static bool TryResolveSceneAssetPath(string sceneName, out string scenePath)
    {
        scenePath = null;
        if (string.IsNullOrWhiteSpace(sceneName))
            return false;

        string[] guids = AssetDatabase.FindAssets($"{sceneName} t:Scene");
        for (int i = 0; i < guids.Length; i++)
        {
            string candidatePath = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (string.IsNullOrWhiteSpace(candidatePath))
                continue;

            string candidateName = Path.GetFileNameWithoutExtension(candidatePath);
            if (!string.Equals(candidateName, sceneName, System.StringComparison.OrdinalIgnoreCase))
                continue;

            scenePath = candidatePath;
            return true;
        }

        return false;
    }

    private static Scene FindLoadedSceneByPath(string scenePath)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.IsValid() &&
                scene.isLoaded &&
                string.Equals(scene.path, scenePath, System.StringComparison.OrdinalIgnoreCase))
            {
                return scene;
            }
        }

        return default;
    }

    private static List<T> FindSceneComponents<T>(Scene scene, bool includeInactive) where T : Component
    {
        List<T> results = new List<T>();
        if (!scene.IsValid() || !scene.isLoaded)
            return results;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root == null)
                continue;

            T[] components = root.GetComponentsInChildren<T>(includeInactive);
            for (int j = 0; j < components.Length; j++)
            {
                T component = components[j];
                if (component != null && component.gameObject.scene == scene)
                    results.Add(component);
            }
        }

        return results;
    }

    private static string BuildUniqueRootName(Scene scene, string baseName)
    {
        HashSet<string> existingNames = new HashSet<string>();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] != null)
                existingNames.Add(roots[i].name);
        }

        if (!existingNames.Contains(baseName))
            return baseName;

        int index = 1;
        string candidate;
        do
        {
            candidate = $"{baseName}_{index}";
            index++;
        }
        while (existingNames.Contains(candidate));

        return candidate;
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

