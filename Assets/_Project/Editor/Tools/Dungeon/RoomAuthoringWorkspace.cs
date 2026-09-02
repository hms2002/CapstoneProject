using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 책임:
/// - 저장되지 않는 방 제작 전용 additive 씬을 식별한다.
/// - 게임 씬의 Hierarchy 오브젝트와 방 제작용 임시 오브젝트가 섞이지 않게 한다.
/// </summary>
[DisallowMultipleComponent]
internal sealed class RoomAuthoringWorkspaceMarker : MonoBehaviour
{
}

/// <summary>
/// 책임:
/// - 방 제작용 임시 additive 씬의 생성, 탐색, 활성화와 폐기를 관리한다.
/// - RoomPieceAuthoring 오브젝트가 게임 씬이 아닌 임시 작업 공간에만 생성되도록 보장한다.
/// - 미리보기 전용 오브젝트 변경이 편집 중인 방의 저장 필요 상태로 기록되지 않게 격리한다.
/// - 작업 공간을 닫은 뒤 사용자가 원래 보고 있던 씬을 다시 활성화한다.
/// </summary>
internal static class RoomAuthoringWorkspace
{
    private const string PreviousActiveSceneHandleKey =
        "Dungeon.RoomAuthoring.PreviousActiveSceneHandle";
    private const string UnsavedChangesKey =
        "Dungeon.RoomAuthoring.HasUnsavedChanges";
    private static int previewMutationDepth;

    static RoomAuthoringWorkspace()
    {
        EditorSceneManager.sceneDirtied -= HandleSceneDirtied;
        EditorSceneManager.sceneDirtied += HandleSceneDirtied;
    }

    public static bool IsOpen => TryGetScene(out _);
    public static bool HasUnsavedChanges =>
        IsOpen &&
        SessionState.GetBool(UnsavedChangesKey, false) &&
        (FindAuthoring() != null || FindCorridorAuthoring() != null);

    public static Scene Open()
    {
        if (TryGetScene(out Scene existingScene))
        {
            SceneManager.SetActiveScene(existingScene);
            return existingScene;
        }

        if (HasUnsavedUntitledScene())
        {
            const string message =
                "Room Authoring 작업 공간을 열기 전에 현재 Untitled 씬을 저장하거나 닫아 주세요. " +
                "툴은 안전을 위해 현재 씬을 자동 저장하거나 교체하지 않습니다.";
            if (Application.isBatchMode)
                Debug.LogError(message);
            else
                EditorUtility.DisplayDialog("방 제작 작업 공간을 열 수 없음", message, "확인");

            return default;
        }

        Scene previousActiveScene = SceneManager.GetActiveScene();
        SessionState.SetString(
            PreviousActiveSceneHandleKey,
            previousActiveScene.IsValid()
                ? previousActiveScene.handle.GetRawData().ToString()
                : string.Empty);

        Scene workspaceScene = EditorSceneManager.NewScene(
            NewSceneSetup.EmptyScene,
            NewSceneMode.Additive);
        SceneManager.SetActiveScene(workspaceScene);

        GameObject markerObject = new("Room Authoring Workspace");
        markerObject.AddComponent<RoomAuthoringWorkspaceMarker>();
        markerObject.hideFlags = HideFlags.HideInHierarchy;
        if (markerObject.scene != workspaceScene)
            SceneManager.MoveGameObjectToScene(markerObject, workspaceScene);
        SessionState.SetBool(UnsavedChangesKey, false);
        return workspaceScene;
    }

    public static bool Close(bool confirmDiscard)
    {
        if (!TryGetScene(out Scene workspaceScene))
            return true;

        RoomPieceAuthoring authoring = FindAuthoring();
        CorridorDecorationModuleAuthoring corridorAuthoring = FindCorridorAuthoring();
        if (confirmDiscard &&
            (authoring != null || corridorAuthoring != null) &&
            HasUnsavedChanges &&
            !EditorUtility.DisplayDialog(
                "던전 제작 작업 공간 닫기",
                "저장되지 않은 방 또는 복도 장식 편집 내용이 있습니다. 작업 공간을 닫고 변경 내용을 버릴까요?",
                "변경 내용 버리기",
                "계속 편집"))
        {
            return false;
        }

        if (Selection.activeGameObject != null &&
            Selection.activeGameObject.scene == workspaceScene)
        {
            Selection.activeObject = null;
        }

        EditorSceneManager.CloseScene(workspaceScene, true);
        SessionState.SetBool(UnsavedChangesKey, false);
        RestorePreviousActiveScene();
        return true;
    }

    public static bool IsInWorkspace(GameObject target)
    {
        return target != null &&
               TryGetScene(out Scene workspaceScene) &&
               target.scene == workspaceScene;
    }

    public static void MoveToWorkspace(GameObject target)
    {
        if (target == null)
            return;

        Scene workspaceScene = Open();
        if (!workspaceScene.IsValid())
        {
            throw new InvalidOperationException(
                "Room Authoring Workspace could not be opened safely.");
        }

        if (target.scene != workspaceScene)
            SceneManager.MoveGameObjectToScene(target, workspaceScene);

        SceneManager.SetActiveScene(workspaceScene);
        EditorSceneManager.MarkSceneDirty(workspaceScene);
    }

    public static bool ExecutePreviewMutation(Action<Scene> mutation)
    {
        if (mutation == null)
            return false;

        Scene workspaceScene = Open();
        if (!workspaceScene.IsValid())
            return false;

        previewMutationDepth++;
        try
        {
            mutation(workspaceScene);
            return true;
        }
        finally
        {
            previewMutationDepth--;
        }
    }

    public static RoomPieceAuthoring FindAuthoring()
    {
        if (!TryGetScene(out Scene workspaceScene))
            return null;

        GameObject[] roots = workspaceScene.GetRootGameObjects();
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            RoomPieceAuthoring authoring =
                roots[rootIndex].GetComponentInChildren<RoomPieceAuthoring>(true);
            if (authoring != null)
                return authoring;
        }

        return null;
    }

    /// <summary>
    /// 책임 : 현재 임시 작업 공간에서 편집 중인 복도 장식 모듈 루트를 찾는다.
    /// </summary>
    public static CorridorDecorationModuleAuthoring FindCorridorAuthoring()
    {
        if (!TryGetScene(out Scene workspaceScene))
            return null;

        GameObject[] roots = workspaceScene.GetRootGameObjects();
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            CorridorDecorationModuleAuthoring authoring =
                roots[rootIndex].GetComponentInChildren<CorridorDecorationModuleAuthoring>(true);
            if (authoring != null)
                return authoring;
        }

        return null;
    }

    public static void MarkSaved()
    {
        if (IsOpen)
            SessionState.SetBool(UnsavedChangesKey, false);
    }

    public static void MarkDirty()
    {
        if (TryGetScene(out Scene workspaceScene))
        {
            SessionState.SetBool(UnsavedChangesKey, true);
            EditorSceneManager.MarkSceneDirty(workspaceScene);
        }
    }

    private static bool TryGetScene(out Scene workspaceScene)
    {
        RoomAuthoringWorkspaceMarker[] markers =
            Resources.FindObjectsOfTypeAll<RoomAuthoringWorkspaceMarker>();
        for (int markerIndex = 0; markerIndex < markers.Length; markerIndex++)
        {
            RoomAuthoringWorkspaceMarker marker = markers[markerIndex];
            if (marker == null || !marker.gameObject.scene.IsValid() || !marker.gameObject.scene.isLoaded)
                continue;

            workspaceScene = marker.gameObject.scene;
            return true;
        }

        workspaceScene = default;
        return false;
    }

    private static bool HasUnsavedUntitledScene()
    {
        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);
            if (scene.IsValid() &&
                scene.isLoaded &&
                scene.isDirty &&
                string.IsNullOrWhiteSpace(scene.path))
                return true;
        }

        return false;
    }

    private static void RestorePreviousActiveScene()
    {
        string previousHandleText =
            SessionState.GetString(PreviousActiveSceneHandleKey, string.Empty);
        SessionState.SetString(PreviousActiveSceneHandleKey, string.Empty);
        ulong.TryParse(previousHandleText, out ulong previousHandle);

        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene candidate = SceneManager.GetSceneAt(sceneIndex);
            if (!candidate.IsValid() || !candidate.isLoaded)
                continue;

            if (candidate.handle.GetRawData() == previousHandle)
            {
                SceneManager.SetActiveScene(candidate);
                return;
            }
        }

        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene candidate = SceneManager.GetSceneAt(sceneIndex);
            if (candidate.IsValid() && candidate.isLoaded)
            {
                SceneManager.SetActiveScene(candidate);
                return;
            }
        }
    }

    private static void HandleSceneDirtied(Scene dirtiedScene)
    {
        if (previewMutationDepth > 0 ||
            !TryGetScene(out Scene workspaceScene) ||
            dirtiedScene != workspaceScene)
            return;

        SessionState.SetBool(UnsavedChangesKey, true);
    }
}
