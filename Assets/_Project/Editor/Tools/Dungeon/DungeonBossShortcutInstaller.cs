using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Authors the BluePortal shortcut prefab and connects it to corridor builders that already support same-scene return travel.</summary>
public static class DungeonBossShortcutInstaller
{
    public const string PrefabPath = DungeonReturnPortalAuthoringUtility.Folder + "/DungeonBossShortcut.prefab";
    public const string TravelPath = DungeonReturnPortalAuthoringUtility.Folder + "/DungeonBossShortcutTravelRig.prefab";
    private const string ControllerPath = "Assets/_Project/Art/Sprites/Map/Shortcuts/Portal_To_Start_Animations/BluePortal.controller";

    [MenuItem("Tools/Dungeon/Install Boss Room Shortcut")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        if (controller == null) throw new InvalidOperationException("BluePortal controller is missing.");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            var root = new GameObject("DungeonBossShortcut");
            try
            {
                var collider = root.AddComponent<CapsuleCollider2D>();
                collider.isTrigger = true;
                collider.direction = CapsuleDirection2D.Horizontal;
                collider.size = new Vector2(2.2f, 1.2f);
                var portal = root.AddComponent<DungeonReturnPortal>();
                var prompt = new GameObject("PromptAnchor").transform;
                prompt.SetParent(root.transform, false);
                prompt.localPosition = Vector3.up * 0.7f;
                var view = root.AddComponent<DungeonReturnPortalView>();
                var art = new GameObject("BluePortal");
                art.transform.SetParent(root.transform, false);
                var renderer = art.AddComponent<SpriteRenderer>();
                renderer.sortingOrder = 62;
                art.AddComponent<Animator>().runtimeAnimatorController = controller;
                view.EditorConfigure(art.transform, null, null, null);
                var viewData = new SerializedObject(view);
                viewData.FindProperty("openState").stringValue = "";
                viewData.FindProperty("idleState").stringValue = "BluePortal";
                viewData.FindProperty("closeState").stringValue = "";
                viewData.ApplyModifiedPropertiesWithoutUndo();
                art.SetActive(false);
                portal.EditorConfigure(view, prompt);
                var data = new SerializedObject(portal);
                data.FindProperty("prompt").stringValue = "보스 방으로 이동하기";
                data.ApplyModifiedPropertiesWithoutUndo();
                prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (prefab == null) throw new InvalidOperationException("Could not save shortcut prefab.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        var travelPrefab = CreateBlueArrivalRig(controller);
        int configured = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/_Project/Scenes" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!System.IO.Path.GetFileNameWithoutExtension(path).StartsWith("Procedural", StringComparison.Ordinal)) continue;
            Scene scene = SceneManager.GetSceneByPath(path);
            bool loaded = scene.IsValid() && scene.isLoaded;
            if (loaded && scene.isDirty) throw new InvalidOperationException("Save scene first: " + path);
            if (!loaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                bool changed = false;
                foreach (GameObject root in scene.GetRootGameObjects())
                    foreach (DungeonRoomBuilder builder in root.GetComponentsInChildren<DungeonRoomBuilder>(true))
                    {
                        var data = new SerializedObject(builder);
                        if (data.FindProperty("returnTravelPrefab").objectReferenceValue == null) continue;
                        builder.EditorConfigureBossShortcut(prefab.GetComponent<DungeonReturnPortal>(), travelPrefab);
                        EditorUtility.SetDirty(builder);
                        changed = true;
                        configured++;
                    }
                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save " + path);
                }
            }
            finally { if (!loaded) EditorSceneManager.CloseScene(scene, true); }
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[BossShortcut] Installed BluePortal and configured {configured} corridor builders.");
    }

    /// <summary>Creates a boss-only arrival rig, preserving return timing and transforms without changing the yellow return prefab.</summary>
    private static DungeonReturnTravel CreateBlueArrivalRig(RuntimeAnimatorController controller)
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(TravelPath);
        if (existing != null) return existing.GetComponent<DungeonReturnTravel>();
        GameObject root = PrefabUtility.LoadPrefabContents(DungeonReturnPortalAuthoringUtility.TravelPath);
        try
        {
            root.name = "DungeonBossShortcutTravelRig";
            var travel = root.GetComponent<DungeonReturnTravel>();
            var travelData = new SerializedObject(travel);
            var view = (DungeonReturnPortalView)travelData.FindProperty("arrivalPortal").objectReferenceValue;
            foreach (Animator animator in view.GetComponentsInChildren<Animator>(true))
                animator.runtimeAnimatorController = controller;
            var viewData = new SerializedObject(view);
            viewData.FindProperty("openState").stringValue = "";
            viewData.FindProperty("idleState").stringValue = "BluePortal";
            viewData.FindProperty("closeState").stringValue = "";
            viewData.ApplyModifiedPropertiesWithoutUndo();
            var saved = PrefabUtility.SaveAsPrefabAsset(root, TravelPath);
            if (saved == null) throw new InvalidOperationException("Could not save boss arrival rig.");
            return saved.GetComponent<DungeonReturnTravel>();
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
