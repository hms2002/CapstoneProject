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
            RunRouteBgmService.EnsureInstance().ForceRefreshActiveSceneBgm();

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
