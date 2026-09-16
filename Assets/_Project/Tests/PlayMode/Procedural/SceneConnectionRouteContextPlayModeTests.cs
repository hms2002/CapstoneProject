using System.Collections;
using System.Reflection;
using CapstoneAudio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// 책임 : HUB의 데이터 기반 복도 연결이 목적지 RouteSet을 전달하고 이를 활성화하면 BGM·지역명 소비자가 같은 스테이지 문맥을 읽는지 회귀 검증한다.
/// </summary>
public sealed class SceneConnectionRouteContextPlayModeTests
{
    private const string HubSceneName = "ProtoTypeHub";
    private const string ShadowEndpointId = "Lobby.shadow.Corridor";
    private const string ShadowCorridorSceneName = "ProceduralShadowCorridor";

#if UNITY_EDITOR
    [Test]
    public void CheckpointQueries_PreserveCommittedRouteAndEventCandidates_UntilDeparture()
    {
        var manager = PortalRouteManager.EnsureInstance();
        var data = GamePlayDataManager.EnsureInstance().Data;
        bool wasRunActive = data.isRunActive;
        var presentedEvents = data.presentedRunMapEventIds;
        var completedEvents = data.completedRunMapEventIds;
        var visitedRoutes = data.visitedRunMapEventRouteThemeIds;
        var pendingPlacements = data.pendingRunMapEventPlacements;
        var defeatedBosses = data.defeatedBossIds;
        Scene scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        RunRouteCatalogSO catalog = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<RunRouteCatalogSO>(
            "Assets/_Project/Data/SceneFlow/Routes/GrandHall/GrandHall_DragonRouteCatalog.asset"));
        try
        {
            data.isRunActive = true;
            data.presentedRunMapEventIds = new();
            data.completedRunMapEventIds = new();
            data.visitedRunMapEventRouteThemeIds = new();
            data.pendingRunMapEventPlacements = new();
            data.defeatedBossIds = new();
            SetPrivateField(catalog, "hubSceneName", scene.name);
            var portalObject = new GameObject("Checkpoint query regression");
            portalObject.SetActive(false);
            SceneManager.MoveGameObjectToScene(portalObject, scene);
            var portal = portalObject.AddComponent<ScenePortal>();
            SetPrivateField(portal, "portalId", "checkpoint-query-regression");
            SetPrivateField(portal, "transitionType", TransitionType.HubToRunStart);
            SetPrivateField(portal, "startRunRouteCatalog", catalog);
            manager.ClearPlan();
            Assert.That(manager.TryResolveRoute(portal, out var departure), Is.True);
            var committedStage = manager.CurrentStageSet;
            Assert.That(committedStage, Is.Not.Null);
            Assert.That(departure.TargetSceneName, Is.EqualTo("ProceduralDragonCorridor"));

            var shadowCatalog = UnityEditor.AssetDatabase.LoadAssetAtPath<RunRouteCatalogSO>(
                "Assets/_Project/Data/SceneFlow/Routes/GrandHall/GrandHall_ShadowRouteCatalog.asset");
            SetPrivateField(catalog, "finalRouteSet", shadowCatalog.FinalRouteSet);
            for (int i = 0; i < 3; i++)
            {
                Assert.That(manager.CanResolveRoute(portal), Is.True);
                Assert.That(manager.GetTravelBlockWarning(portal), Is.EqualTo(WarningPopupCode.None));
                Assert.That(manager.CurrentStageSet, Is.SameAs(committedStage));
                Assert.That(manager.LastLoadPresentationTargetSceneName, Is.EqualTo(departure.TargetSceneName));
            }

            Assert.That(RunMapEventProgress.TryResolveCurrentBossRouteThemeId(out var theme), Is.True);
            Assert.That(theme, Is.EqualTo(committedStage.StableThemeId));
            var eventProfile = UnityEditor.AssetDatabase.LoadAssetAtPath<RunMapEventGenerationProfileSO>(
                "Assets/_Project/Data/Dungeon/MapEvents/ParcelDelivery/Dragon_ParcelEventGenerationProfile.asset");
            var eventPlan = RunMapEventGenerationResolver.CreatePlan(eventProfile, null, 1234);
            Assert.That(eventPlan.PresentedEventIds.Count, Is.EqualTo(1));
            Assert.That(eventPlan.GuaranteedRoomTemplates.Count, Is.EqualTo(1));

            data.pendingRunMapEventPlacements.Add(new PendingRunMapEventPlacement(
                "parcel_delivery", "parcel_delivery_destination", "previous_route", theme, false, 1));
            var deliveryPlan = RunMapEventGenerationResolver.CreatePlan(eventProfile, null, 1234);
            Assert.That(deliveryPlan.ConsumedPendingPlacements.Count, Is.EqualTo(1));
            Assert.That(deliveryPlan.PresentedEventIds.Count, Is.EqualTo(1),
                "A parcel destination must leave the start-event selection quota available.");
            Assert.That(deliveryPlan.GuaranteedRoomTemplates.Count, Is.EqualTo(2));
            data.pendingRunMapEventPlacements.Clear();

            data.defeatedBossIds.Add(shadowCatalog.FinalRouteSet.StableThemeId);
            Assert.That(manager.GetTravelBlockWarning(portal), Is.EqualTo(WarningPopupCode.BossAlreadyDefeatedThisRun));
            Assert.That(manager.CurrentStageSet, Is.SameAs(committedStage));
            data.defeatedBossIds.Clear();
            Assert.That(manager.TryResolveRoute(portal, out var nextDeparture), Is.True);
            Assert.That(nextDeparture.TargetSceneName, Is.EqualTo(ShadowCorridorSceneName));
            Assert.That(manager.CurrentStageSet, Is.SameAs(shadowCatalog.FinalRouteSet));
        }
        finally
        {
            manager.ClearPlan();
            data.isRunActive = wasRunActive;
            data.presentedRunMapEventIds = presentedEvents;
            data.completedRunMapEventIds = completedEvents;
            data.visitedRunMapEventRouteThemeIds = visitedRoutes;
            data.pendingRunMapEventPlacements = pendingPlacements;
            data.defeatedBossIds = defeatedBosses;
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            Object.DestroyImmediate(catalog);
        }
    }

    private static void SetPrivateField(object target, string name, object value)
    {
        var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(target, value);
    }
#endif

    [UnityTest]
    public IEnumerator ShadowLobbyConnection_ActivatesSharedDestinationRouteContext()
    {
        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(HubSceneName, LoadSceneMode.Single);
        Assert.That(loadOperation, Is.Not.Null);
        while (!loadOperation.isDone)
            yield return null;

        // HUB 직접 시작 보정 coroutine이 완료된 뒤 실제 플레이 중 트리거 진입과 같은 조건에서 검증한다.
        for (int frame = 0; frame < 12; frame++)
            yield return null;

        SceneTravelEndpoint endpoint = FindEndpoint(ShadowEndpointId);
        Assert.That(endpoint, Is.Not.Null);
        Assert.That(endpoint.TryResolveDirection(out ResolvedSceneTravelDirection resolved), Is.True);
        Assert.That(resolved.Destination.SceneName, Is.EqualTo(ShadowCorridorSceneName));

        CorridorBossRouteSetSO routeSet =
            resolved.Destination.RouteContext as CorridorBossRouteSetSO;
        Assert.That(routeSet, Is.Not.Null);
        Assert.That(routeSet.MatchesCorridorScene(ShadowCorridorSceneName), Is.True);
        Assert.That(routeSet.CorridorBgm.IsSet, Is.True);

        PortalRouteManager manager = PortalRouteManager.EnsureInstance();
        manager.ClearPlan();
        try
        {
            Assert.That(
                manager.ActivateSceneConnectionRouteContext(
                    routeSet,
                    resolved.Destination.SceneName),
                Is.True);
            Assert.That(manager.CurrentStageSet, Is.SameAs(routeSet));
            Assert.That(RunRoutePlayback.CurrentStageSet, Is.SameAs(routeSet));
            Assert.That(
                RunRoutePlayback.TryResolveCurrentLocationName(
                    ShadowCorridorSceneName,
                    out string locationName),
                Is.True);
            Assert.That(locationName, Is.EqualTo(routeSet.CorridorLocationName));

            AsyncOperation corridorLoadOperation =
                SceneManager.LoadSceneAsync(ShadowCorridorSceneName, LoadSceneMode.Single);
            Assert.That(corridorLoadOperation, Is.Not.Null);
            while (!corridorLoadOperation.isDone)
                yield return null;

            yield return null;
            yield return null;
            Assert.That(manager.CurrentStageSet, Is.SameAs(routeSet));

            float musicTransitionElapsed = 0f;
            while (musicTransitionElapsed < 2f &&
                   ReadCurrentMusicKey() != routeSet.CorridorBgm.key)
            {
                musicTransitionElapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.That(ReadCurrentMusicKey(), Is.EqualTo(routeSet.CorridorBgm.key));
        }
        finally
        {
            manager.ClearPlan();
        }
    }

    /// <summary>
    /// 책임 : 현재 활성 씬에서 비활성 staging 오브젝트까지 포함해 지정 Id의 이동 endpoint를 찾는다.
    /// </summary>
    private static SceneTravelEndpoint FindEndpoint(string endpointId)
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            SceneTravelEndpoint[] endpoints =
                roots[rootIndex].GetComponentsInChildren<SceneTravelEndpoint>(includeInactive: true);
            for (int endpointIndex = 0; endpointIndex < endpoints.Length; endpointIndex++)
            {
                if (endpoints[endpointIndex].EndpointId == endpointId)
                    return endpoints[endpointIndex];
            }
        }

        return null;
    }

    private static string ReadCurrentMusicKey()
    {
        FieldInfo field = typeof(SoundManager).GetField(
            "currentMusicKey",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        return field.GetValue(SoundManager.EnsureInstance()) as string;
    }
}
