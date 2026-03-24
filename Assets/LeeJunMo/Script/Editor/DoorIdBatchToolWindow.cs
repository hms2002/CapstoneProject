using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class DoorIdBatchToolWindow : EditorWindow
{
    private enum DoorSearchScope
    {
        ActiveScene,
        LoadedScenes
    }

    private DoorSearchScope searchScope = DoorSearchScope.ActiveScene;
    private readonly List<DoorObject> cachedDoors = new();
    private Vector2 scrollPosition;

    [MenuItem("Tools/문(Door) 관리/Door ID 일괄 재발급 툴")]
    private static void OpenWindow()
    {
        var window = GetWindow<DoorIdBatchToolWindow>("Door ID Tool");
        window.minSize = new Vector2(420f, 320f);
        window.RefreshDoors();
    }

    private void OnEnable()
    {
        RefreshDoors();
        EditorSceneManager.sceneOpened += OnSceneChanged;
        EditorSceneManager.sceneClosed += OnSceneClosed;
        EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
    }

    private void OnDisable()
    {
        EditorSceneManager.sceneOpened -= OnSceneChanged;
        EditorSceneManager.sceneClosed -= OnSceneClosed;
        EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Door ID Batch Reset", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "현재 범위의 DoorObject를 찾아 doorID를 전부 새로 발급합니다. 저장 데이터와 연결된 기존 doorID는 바뀌므로, 이미 저장된 숏컷 데이터가 있다면 함께 리셋하는 편이 안전합니다.",
            MessageType.Warning);

        EditorGUI.BeginChangeCheck();
        searchScope = (DoorSearchScope)EditorGUILayout.EnumPopup("Scope", searchScope);
        if (EditorGUI.EndChangeCheck())
            RefreshDoors();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Refresh", GUILayout.Height(28f)))
                RefreshDoors();

            using (new EditorGUI.DisabledScope(cachedDoors.Count == 0))
            {
                if (GUILayout.Button("Reset All Door IDs", GUILayout.Height(28f)))
                    ResetAllDoorIds();
            }
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField($"Target Doors: {cachedDoors.Count}");

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        for (int i = 0; i < cachedDoors.Count; i++)
        {
            var door = cachedDoors[i];
            if (door == null)
                continue;

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.ObjectField(door, typeof(DoorObject), true);
                EditorGUILayout.SelectableLabel(
                    string.IsNullOrWhiteSpace(door.doorID) ? "<empty>" : door.doorID,
                    GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }
        }
        EditorGUILayout.EndScrollView();
    }

    private void ResetAllDoorIds()
    {
        if (cachedDoors.Count == 0)
        {
            Debug.LogWarning("[DoorIdBatchTool] No DoorObject found in the selected scope.");
            return;
        }

        bool confirmed = EditorUtility.DisplayDialog(
            "Reset Door IDs",
            $"선택한 범위의 DoorObject {cachedDoors.Count}개 ID를 전부 새로 발급합니다.\n기존 doorID를 기준으로 저장한 숏컷 데이터와 연결이 끊길 수 있습니다.\n계속할까요?",
            "Reset",
            "Cancel");

        if (!confirmed)
            return;

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        int resetCount = 0;

        for (int i = 0; i < cachedDoors.Count; i++)
        {
            var door = cachedDoors[i];
            if (door == null || !door.gameObject.scene.IsValid())
                continue;

            Undo.RecordObject(door, "Reset Door ID");
            door.GenerateID();
            EditorSceneManager.MarkSceneDirty(door.gameObject.scene);
            resetCount++;
        }

        Undo.CollapseUndoOperations(undoGroup);
        AssetDatabase.SaveAssets();
        RefreshDoors();

        Debug.Log($"[DoorIdBatchTool] Reset {resetCount} door IDs. scope={searchScope}");
    }

    private void RefreshDoors()
    {
        cachedDoors.Clear();

        var doors = FindObjectsByType<DoorObject>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        Scene activeScene = SceneManager.GetActiveScene();

        for (int i = 0; i < doors.Length; i++)
        {
            var door = doors[i];
            if (door == null || !door.gameObject.scene.IsValid())
                continue;

            if (searchScope == DoorSearchScope.ActiveScene &&
                door.gameObject.scene.handle != activeScene.handle)
            {
                continue;
            }

            cachedDoors.Add(door);
        }

        cachedDoors.Sort(CompareDoors);
        Repaint();
    }

    private static int CompareDoors(DoorObject a, DoorObject b)
    {
        if (a == null && b == null)
            return 0;
        if (a == null)
            return 1;
        if (b == null)
            return -1;

        int sceneCompare = string.Compare(
            a.gameObject.scene.name,
            b.gameObject.scene.name,
            System.StringComparison.Ordinal);

        if (sceneCompare != 0)
            return sceneCompare;

        return string.Compare(a.name, b.name, System.StringComparison.Ordinal);
    }

    private void OnSceneChanged(Scene _, OpenSceneMode __)
    {
        RefreshDoors();
    }

    private void OnSceneClosed(Scene _)
    {
        RefreshDoors();
    }

    private void OnActiveSceneChanged(Scene _, Scene __)
    {
        RefreshDoors();
    }
}
