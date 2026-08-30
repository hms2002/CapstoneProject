using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 책임 : HUB의 기존 상호작용 포탈이 일반 테마 런 계획과 분리된 DemonKing 전용 계획을 시작해 고정 휴식 복도로 이동하도록 설치하고 검증한다.
/// </summary>
public static class DemonKingHubPortalInstaller
{
    public const string HubPortalRouteCatalogPath =
        "Assets/_Project/Data/SceneFlow/Routes/DemonkingHubRouteCatalog.asset";

    private const string HubSceneName = "ProtoTypeHub";
    private const string HubScenePath = "Assets/_Project/Scenes/ProtoTypeHub.unity";
    private const string SourceRunRouteCatalogPath =
        "Assets/_Project/Data/SceneFlow/Routes/RunRouteCatalog.asset";
    private const string DemonKingRouteSetPath =
        "Assets/_Project/Data/SceneFlow/Routes/DemonkingRouteSet.asset";
    private const string DemonKingCorridorSceneName = "DemonkingCorridor";

    [MenuItem("Tools/Dungeon/Connect HUB Portal To DemonKing Rest Corridor")]
    public static void InstallFromMenu()
    {
        if (!Application.isBatchMode &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        Install();
    }

    /// <summary>
    /// 책임 : DemonKing 전용 카탈로그와 HUB ScenePortal 참조를 멱등적으로 구성하고 씬을 저장한다.
    /// </summary>
    public static void Install()
    {
        RunRouteCatalogSO catalog = CreateOrUpdateDemonKingCatalog();
        WithHubScene(scene => ConfigureHubPortal(scene, catalog));
        AssetDatabase.SaveAssets();
        Validate();
        Debug.Log(
            "Connected the ProtoTypeHub ScenePortal directly to the fixed DemonkingCorridor " +
            "through a DemonKing-only run route catalog.");
    }

    [MenuItem("Tools/Dungeon/Validate HUB Portal To DemonKing Rest Corridor")]
    public static void ValidateFromMenu()
    {
        Validate();
    }

    /// <summary>
    /// 책임 : 전용 카탈로그가 최종 RouteSet 하나만 계획하고 HUB의 유일한 시작 포탈이 그 카탈로그를 참조하는지 확인한다.
    /// </summary>
    public static void Validate()
    {
        RunRouteCatalogSO catalog =
            AssetDatabase.LoadAssetAtPath<RunRouteCatalogSO>(HubPortalRouteCatalogPath);
        CorridorBossRouteSetSO routeSet =
            AssetDatabase.LoadAssetAtPath<CorridorBossRouteSetSO>(DemonKingRouteSetPath);
        if (catalog == null ||
            routeSet == null ||
            catalog.NormalStageCount != 0 ||
            catalog.NormalRouteSets.Count != 0 ||
            catalog.FinalRouteSet != routeSet ||
            !string.Equals(
                routeSet.CorridorSceneName,
                DemonKingCorridorSceneName,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The HUB DemonKing portal catalog must contain only the fixed DemonKing rest Corridor route.");
        }

        WithHubScene(scene =>
        {
            List<ScenePortal> portals = FindHubStartPortals(scene);
            if (portals.Count != 1 || portals[0].StartRunRouteCatalog != catalog)
            {
                throw new InvalidOperationException(
                    $"ProtoTypeHub must contain exactly one HubToRunStart ScenePortal bound to " +
                    $"'{catalog.name}'. Found={portals.Count}.");
            }
        }, saveScene: false);

        Debug.Log(
            $"Verified ProtoTypeHub ScenePortal -> {DemonKingCorridorSceneName}. " +
            "NormalStages=0, FinalRoute=DemonkingRouteSet.");
    }

    /// <summary>
    /// 책임 : 공용 런 카탈로그의 HUB·로딩 설정을 재사용하되 DemonKing 최종 RouteSet만 포함하는 전용 카탈로그를 만든다.
    /// </summary>
    private static RunRouteCatalogSO CreateOrUpdateDemonKingCatalog()
    {
        RunRouteCatalogSO sourceCatalog =
            AssetDatabase.LoadAssetAtPath<RunRouteCatalogSO>(SourceRunRouteCatalogPath);
        CorridorBossRouteSetSO routeSet =
            AssetDatabase.LoadAssetAtPath<CorridorBossRouteSetSO>(DemonKingRouteSetPath);
        if (sourceCatalog == null || routeSet == null || !routeSet.IsValid)
        {
            throw new InvalidOperationException(
                "RunRouteCatalog or DemonkingRouteSet is missing or invalid.");
        }

        RunRouteCatalogSO catalog =
            AssetDatabase.LoadAssetAtPath<RunRouteCatalogSO>(HubPortalRouteCatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<RunRouteCatalogSO>();
            AssetDatabase.CreateAsset(catalog, HubPortalRouteCatalogPath);
        }

        SerializedObject source = new(sourceCatalog);
        SerializedObject target = new(catalog);
        target.FindProperty("normalStageCount").intValue = 0;
        target.FindProperty("normalRouteSets").ClearArray();
        target.FindProperty("useFixedNormalRouteOrder").boolValue = false;
        target.FindProperty("allowDuplicateNormalRoutes").boolValue = false;
        target.FindProperty("finalRouteSet").objectReferenceValue = routeSet;
        target.CopyFromSerializedProperty(source.FindProperty("hubBgm"));
        target.FindProperty("runCommonLoadManifest").objectReferenceValue =
            sourceCatalog.RunCommonLoadManifest;
        target.FindProperty("hubSceneName").stringValue = HubSceneName;
        target.FindProperty("hubEntryPointId").stringValue =
            string.IsNullOrWhiteSpace(sourceCatalog.HubEntryPointId)
                ? "Default"
                : sourceCatalog.HubEntryPointId;
        target.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
        return catalog;
    }

    /// <summary>
    /// 책임 : HUB의 유일한 HubToRunStart 포탈을 찾아 전용 카탈로그만 교체하고 위치·외형·상호작용 설정은 보존한다.
    /// </summary>
    private static void ConfigureHubPortal(Scene scene, RunRouteCatalogSO catalog)
    {
        List<ScenePortal> portals = FindHubStartPortals(scene);
        if (portals.Count != 1)
        {
            throw new InvalidOperationException(
                $"ProtoTypeHub must contain exactly one HubToRunStart ScenePortal. Found={portals.Count}.");
        }

        ScenePortal portal = portals[0];
        SerializedObject serializedPortal = new(portal);
        serializedPortal.FindProperty("startRunRouteCatalog").objectReferenceValue = catalog;
        serializedPortal.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(portal);
    }

    /// <summary>
    /// 책임 : 데이터 기반 일반 테마 게이트와 혼동하지 않고 HUB의 레거시 상호작용 시작 포탈만 수집한다.
    /// </summary>
    private static List<ScenePortal> FindHubStartPortals(Scene scene)
    {
        var portals = new List<ScenePortal>();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            ScenePortal[] candidates =
                roots[rootIndex].GetComponentsInChildren<ScenePortal>(includeInactive: true);
            for (int portalIndex = 0; portalIndex < candidates.Length; portalIndex++)
            {
                ScenePortal candidate = candidates[portalIndex];
                if (candidate.PortalTransitionType == TransitionType.HubToRunStart)
                    portals.Add(candidate);
            }
        }

        return portals;
    }

    /// <summary>
    /// 책임 : HUB 씬의 기존 로드 상태를 보존하면서 필요한 편집 작업을 수행하고 새로 연 씬만 닫는다.
    /// </summary>
    private static void WithHubScene(Action<Scene> action, bool saveScene = true)
    {
        Scene scene = SceneManager.GetSceneByPath(HubScenePath);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        Scene previousActiveScene = SceneManager.GetActiveScene();
        if (openedHere)
            scene = EditorSceneManager.OpenScene(HubScenePath, OpenSceneMode.Additive);

        try
        {
            action(scene);
            if (saveScene)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                    throw new InvalidOperationException($"Failed to save HUB scene: {HubScenePath}");
            }
        }
        finally
        {
            if (openedHere && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, removeScene: true);

            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                SceneManager.SetActiveScene(previousActiveScene);
        }
    }
}
