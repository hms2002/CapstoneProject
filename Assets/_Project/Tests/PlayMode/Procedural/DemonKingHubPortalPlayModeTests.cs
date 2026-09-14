using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// 책임 : HUB의 기존 상호작용 포탈이 일반 테마를 거치지 않고 고정 DemonKing 휴식 복도를 첫 목적지로 해석하는지 회귀 검증한다.
/// </summary>
public sealed class DemonKingHubPortalPlayModeTests
{
    private const string HubSceneName = "ProtoTypeHub";
    private const string DemonKingCorridorSceneName = "DemonkingCorridor";

#if UNITY_EDITOR
    // Checks authored scenes and real portal prefabs without running their gameplay Awake/Start flow.
    [TestCase("DemonkingCorridor")]
    [TestCase("ProceduralDemonkingCorridor")]
    public void BossEntrance_ResolvesWithoutRunRouteBackend(string sceneName)
    {
        PortalRouteManager manager = PortalRouteManager.Instance;
        manager?.ClearPlan();
        RunRoutePlayback.RegisterBackend(null);
        Scene scene = default;
        Scene bossScene = default;
        var originalRoots = new HashSet<GameObject>(SceneManager.GetActiveScene().GetRootGameObjects());
        try
        {
            // Existing MerchantNPC.OnValidate attempts to create four slots while preview-loading
            // this scene. Account for those exact unrelated errors, not arbitrary travel errors.
            if (sceneName == "ProceduralDemonkingCorridor")
            {
                for (int i = 0; i < 4; i++)
                {
                    LogAssert.Expect(LogType.Error, "Cannot instantiate objects with a parent which is persistent. New object will be created without a parent.");
                    LogAssert.Expect(LogType.Error, "Setting the parent of a transform which resides in a Prefab Asset is disabled to prevent data corruption");
                }
            }
            scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene($"Assets/_Project/Scenes/{sceneName}.unity");
            SceneTravelEndpoint endpoint = null;
            if (sceneName == DemonKingCorridorSceneName)
            {
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var legacy in root.GetComponentsInChildren<ScenePortal>(true))
                        Assert.That(legacy.PortalTransitionType, Is.Not.EqualTo(TransitionType.CorridorToBoss));
                    foreach (var candidate in root.GetComponentsInChildren<SceneTravelEndpoint>(true))
                        if (candidate.EndpointId == "Corridor.demon_king.RestBoss") endpoint = candidate;
                }
            }
            else
            {
                DungeonRoomBuilder builder = null;
                foreach (var root in scene.GetRootGameObjects())
                    if (root.GetComponentInChildren<DungeonRoomBuilder>(true) is { } found) builder = found;
                Assert.That(builder, Is.Not.Null);
                var bindings = new UnityEditor.SerializedObject(builder).FindProperty("travelEndpointBindings");
                SceneConnectionSO connection = null;
                int side = -1;
                for (int i = 0; i < bindings.arraySize; i++)
                {
                    var binding = bindings.GetArrayElementAtIndex(i);
                    if (binding.FindPropertyRelative("roomId").stringValue != "DemonKing_Boss" ||
                        binding.FindPropertyRelative("slotId").stringValue != "BossGate") continue;
                    connection = binding.FindPropertyRelative("connection").objectReferenceValue as SceneConnectionSO;
                    side = binding.FindPropertyRelative("connectionSide").enumValueIndex;
                }
                Assert.That(connection, Is.Not.Null);
                Assert.That(side, Is.EqualTo(0));
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/Prefabs/Map/Procedural/ProceduralSceneTravelPortal.prefab");
                var portal = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, scene);
                endpoint = portal.GetComponent<SceneTravelEndpoint>();
                endpoint.EditorConfigure(connection.EndpointA.EndpointId, "BossGate", connection, SceneConnectionEndpointSide.A);
            }
            Assert.That(endpoint, Is.Not.Null);
            Assert.That(endpoint.isActiveAndEnabled, Is.True);
            Assert.That(endpoint.GetComponent<SceneTravelInteractable>(), Is.Not.Null);
            Assert.That(endpoint.RegisterAsArrivalOnly, Is.False);
            Assert.That(endpoint.TryResolveDirection(out var direction), Is.True);
            Assert.That(direction.Destination.SceneName, Is.EqualTo("LeeJunmo_Boss_DemonKing"));
            Assert.That(direction.Destination.EndpointId, Is.EqualTo("Boss.demon_king.Corridor"));
            Assert.That(endpoint.Connection.AToB.Enabled, Is.True);
            Assert.That(endpoint.Connection.BToA.Enabled, Is.False);

            bossScene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/LeeJunmo_Boss_DemonKing.unity");
            int arrivals = 0;
            foreach (var root in bossScene.GetRootGameObjects())
                foreach (var arrival in root.GetComponentsInChildren<SceneTravelEndpoint>(true))
                    if (arrival.EndpointId == direction.Destination.EndpointId && arrival.isActiveAndEnabled) arrivals++;
            Assert.That(arrivals, Is.EqualTo(1), "Both sources use one active boss arrival endpoint.");
        }
        finally
        {
            if (bossScene.IsValid()) UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(bossScene);
            if (scene.IsValid()) UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                if (!originalRoots.Contains(root)) Object.DestroyImmediate(root);
            RunRoutePlayback.RegisterBackend(manager);
        }
    }
#endif

    [UnityTest]
    public IEnumerator HubStartPortal_ResolvesFixedDemonKingRestCorridor()
    {
        AsyncOperation loadOperation =
            SceneManager.LoadSceneAsync(HubSceneName, LoadSceneMode.Single);
        Assert.That(loadOperation, Is.Not.Null);
        while (!loadOperation.isDone)
            yield return null;

        yield return null;
        yield return null;

        List<ScenePortal> portals = FindHubStartPortals();
        Assert.That(portals, Has.Count.EqualTo(1));
        ScenePortal portal = portals[0];
        Assert.That(portal.StartRunRouteCatalog, Is.Not.Null);
        Assert.That(portal.StartRunRouteCatalog.NormalStageCount, Is.EqualTo(0));
        Assert.That(portal.StartRunRouteCatalog.FinalRouteSet, Is.Not.Null);
        Assert.That(
            portal.StartRunRouteCatalog.FinalRouteSet.CorridorSceneName,
            Is.EqualTo(DemonKingCorridorSceneName));

        PortalRouteManager manager = PortalRouteManager.EnsureInstance();
        manager.ClearPlan();
        try
        {
            Assert.That(manager.CanResolveRoute(portal), Is.True);
            Assert.That(
                manager.TryResolveRoute(portal, out PortalRouteDecision route),
                Is.True);
            Assert.That(route.TargetSceneName, Is.EqualTo(DemonKingCorridorSceneName));
            Assert.That(route.EntryPointId, Is.EqualTo("Default"));
            Assert.That(route.TransitionType, Is.EqualTo(TransitionType.HubToRunStart));
            Assert.That(manager.CurrentStageSet, Is.SameAs(portal.StartRunRouteCatalog.FinalRouteSet));
        }
        finally
        {
            manager.ClearPlan();
        }

        Assert.That(ScenePortalTravelService.TryTravel(portal), Is.True);
        const int sceneLoadFrameLimit = 600;
        for (int frame = 0;
             frame < sceneLoadFrameLimit &&
             SceneManager.GetActiveScene().name != DemonKingCorridorSceneName;
             frame++)
        {
            yield return null;
        }

        Assert.That(
            SceneManager.GetActiveScene().name,
            Is.EqualTo(DemonKingCorridorSceneName));
    }

    /// <summary>
    /// 책임 : 데이터 기반 일반 테마 게이트를 제외하고 활성 HUB 씬의 HubToRunStart ScenePortal만 수집한다.
    /// </summary>
    private static List<ScenePortal> FindHubStartPortals()
    {
        var portals = new List<ScenePortal>();
        Scene hubScene = SceneManager.GetActiveScene();
        GameObject[] roots = hubScene.GetRootGameObjects();
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            ScenePortal[] candidates =
                roots[rootIndex].GetComponentsInChildren<ScenePortal>(includeInactive: true);
            for (int portalIndex = 0; portalIndex < candidates.Length; portalIndex++)
            {
                if (candidates[portalIndex].PortalTransitionType == TransitionType.HubToRunStart)
                    portals.Add(candidates[portalIndex]);
            }
        }

        return portals;
    }
}
