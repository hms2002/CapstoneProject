using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Creates art-free return prefabs and wires only the three production corridor builders and the player's visual references.</summary>
public static class DungeonReturnPortalAuthoringUtility
{
    public const string Folder = "Assets/_Project/Prefabs/Map/Procedural/ReturnPortals";
    public const string PortalPath = Folder + "/DungeonReturnPortal.prefab";
    public const string TravelPath = Folder + "/DungeonReturnTravelRig.prefab";
    public const string PlayerPath = "Assets/_Project/Prefabs/Player/PF Player.prefab";
    private static readonly string[] Themes = { "Shadow", "Dragon", "Slime" };

    [MenuItem("Tools/Dungeon/Install Dead End Return Portals")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before installing return portals.");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/_Project/Prefabs/Map/Procedural", "ReturnPortals");
        CreatePortal();
        CreateTravelRig();
        CreateAnchor("ReturnLanding");
        foreach (RoomSocketDirection direction in Enum.GetValues(typeof(RoomSocketDirection))) CreateAnchor("ReturnPortal_" + direction);
        ConfigurePlayer();
        var portal = AssetDatabase.LoadAssetAtPath<GameObject>(PortalPath).GetComponent<DungeonReturnPortal>();
        var travel = AssetDatabase.LoadAssetAtPath<GameObject>(TravelPath).GetComponent<DungeonReturnTravel>();
        foreach (string theme in Themes)
        {
            string path = $"Assets/_Project/Scenes/Procedural{theme}Corridor.unity";
            Scene scene = SceneManager.GetSceneByPath(path);
            bool alreadyLoaded = scene.IsValid() && scene.isLoaded;
            if (alreadyLoaded && scene.isDirty) throw new InvalidOperationException("Save the scene before installing: " + path);
            if (!alreadyLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                int configured = 0;
                foreach (GameObject root in scene.GetRootGameObjects())
                    foreach (DungeonRoomBuilder builder in root.GetComponentsInChildren<DungeonRoomBuilder>(true))
                    {
                        builder.EditorConfigureReturnPortals(portal, travel);
                        EditorUtility.SetDirty(builder);
                        configured++;
                    }
                if (configured != 1) throw new InvalidOperationException($"Expected one builder in {path}, found {configured}.");
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally { if (!alreadyLoaded) EditorSceneManager.CloseScene(scene, true); }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[ReturnPortal] Installed art-free prefabs, player visual binding and three corridor scene bindings.");
    }

    private static void CreatePortal()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PortalPath) != null) return;
        var root = new GameObject("DungeonReturnPortal");
        try
        {
            var collider = root.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = 0.6f;
            var portal = root.AddComponent<DungeonReturnPortal>();
            var prompt = new GameObject("PromptAnchor").transform;
            prompt.SetParent(root.transform, false);
            prompt.localPosition = Vector3.up * 0.7f;
            portal.EditorConfigure(CreateView(root.transform), prompt);
            Save(root, PortalPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void CreateTravelRig()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(TravelPath) != null) return;
        var root = new GameObject("DungeonReturnTravelRig");
        try
        {
            var landing = new GameObject("LandingPoint").transform;
            landing.SetParent(root.transform, false);
            DungeonReturnPortalView view = CreateView(root.transform);
            view.name = "ArrivalPortal";
            root.AddComponent<DungeonReturnTravel>().EditorConfigure(landing, view);
            Save(root, TravelPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static DungeonReturnPortalView CreateView(Transform parent)
    {
        var root = new GameObject("DirectionalVisuals");
        root.transform.SetParent(parent, false);
        var view = root.AddComponent<DungeonReturnPortalView>();
        var directions = new Transform[4];
        for (int i = 0; i < 4; i++)
        {
            var child = new GameObject(((RoomSocketDirection)i).ToString());
            child.transform.SetParent(root.transform, false);
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sortingLayerName = "Default";
            renderer.sortingOrder = 62;
            child.AddComponent<Animator>();
            directions[i] = child.transform;
            child.SetActive(false);
        }
        view.EditorConfigure(directions[0], directions[1], directions[2], directions[3]);
        return view;
    }

    private static void CreateAnchor(string slot)
    {
        string path = Folder + "/" + slot + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        var root = new GameObject(slot);
        try
        {
            root.AddComponent<ProceduralRoomAnchor>().EditorConfigure(slot, ProceduralRoomAnchorScope.LocalRoom);
            Save(root, path);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void ConfigurePlayer()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPath);
        try
        {
            PlayerPortalArrivalVisual2D visual = root.GetComponent<PlayerPortalArrivalVisual2D>();
            if (visual != null && visual.IsConfigured) return;
            Transform render = root.transform.Find("PlayerRender");
            Transform weapon = root.transform.Find("WeaponPresentationRig");
            Transform shadow = root.transform.Find("Shadow");
            if (render == null || weapon == null || shadow == null) throw new InvalidOperationException("Player render/weapon/shadow hierarchy changed; review bindings manually.");
            if (visual == null) visual = root.AddComponent<PlayerPortalArrivalVisual2D>();
            visual.EditorConfigure(new[] { render, weapon }, shadow);
            Save(root, PlayerPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void Save(GameObject root, string path)
    {
        if (PrefabUtility.SaveAsPrefabAsset(root, path) == null) throw new InvalidOperationException("Could not save " + path);
    }
}
