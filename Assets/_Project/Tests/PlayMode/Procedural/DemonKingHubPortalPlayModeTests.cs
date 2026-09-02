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
