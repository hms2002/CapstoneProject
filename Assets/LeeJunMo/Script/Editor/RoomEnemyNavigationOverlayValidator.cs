using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RoomEnemyNavigationOverlayValidator
{
    private const string ArrowPrefabPath = "Assets/HeoMinSeok/_Project/Prefabs/Gameplay/Items/KillLockMonsterNavigationArrow.prefab";
    private const string OverlayRootName = "RoomEnemyNavigationOverlay";
    private const int DefaultShowThreshold = 4;
    private const float DefaultViewportPadding = 0.08f;

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

    private static int ValidateScene(Scene scene, GameObject expectedArrowPrefab)
    {
        if (!SceneNeedsOverlay(scene))
        {
            Debug.Log($"[RoomEnemyNavigationOverlayValidator] {scene.path}: no room spawn/lock groups found; overlay not required.");
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
        return FindSceneComponents<MonsterSpawnRoomGroup>(scene, includeInactive: true).Count > 0
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
