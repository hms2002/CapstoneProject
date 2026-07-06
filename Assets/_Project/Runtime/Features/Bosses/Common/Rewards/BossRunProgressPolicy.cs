using System.Runtime.CompilerServices;

/// <summary>
/// 책임 : 보스 진행도 평가에 필요한 보스, route backend, 보상 수정자 입력을 묶는 요청 데이터이다.
/// </summary>
public readonly struct BossRunProgressRequest
{
    public BossControllerBase Boss { get; }
    public IRunRouteBackend RouteBackend { get; }
    public BossRewardModifierAggregate RewardModifiers { get; }

    public BossRunProgressRequest(
        BossControllerBase boss,
        IRunRouteBackend routeBackend,
        BossRewardModifierAggregate rewardModifiers)
    {
        Boss = boss;
        RouteBackend = routeBackend;
        RewardModifiers = rewardModifiers;
    }
}

/// <summary>
/// 책임 : 보스 진행도 평가 결과를 보상 문맥으로 변환 가능한 불변 데이터로 전달한다.
/// </summary>
public readonly struct BossRunProgressResult
{
    public BossControllerBase Boss { get; }
    public CorridorBossRouteSetSO RouteSet { get; }
    public int RouteSetKey { get; }
    public bool IsFinalRouteSet { get; }
    public int BossIdentityKey { get; }
    public BossRewardModifierAggregate RewardModifiers { get; }

    public BossRunProgressResult(
        BossControllerBase boss,
        CorridorBossRouteSetSO routeSet,
        int routeSetKey,
        bool isFinalRouteSet,
        int bossIdentityKey,
        BossRewardModifierAggregate rewardModifiers)
    {
        Boss = boss;
        RouteSet = routeSet;
        RouteSetKey = routeSetKey;
        IsFinalRouteSet = isFinalRouteSet;
        BossIdentityKey = bossIdentityKey;
        RewardModifiers = rewardModifiers;
    }

    public BossRewardContext ToRewardContext()
    {
        return new BossRewardContext(
            Boss,
            RouteSet,
            RouteSetKey,
            IsFinalRouteSet,
            RewardModifiers);
    }
}

/// <summary>
/// 책임 : 보스 처치가 현재 route 단계와 최종 보스 여부에 어떤 영향을 갖는지 계산한다.
/// </summary>
public static class BossRunProgressPolicy
{
    public static BossRunProgressResult Evaluate(BossRunProgressRequest request)
    {
        IRunRouteBackend routeBackend = request.RouteBackend;
        CorridorBossRouteSetSO routeSet = routeBackend != null ? routeBackend.CurrentStageSet : null;
        int routeSetKey = ResolveRouteSetKey(request.Boss, routeBackend);
        bool isFinalRouteSet = IsCurrentRouteFinalBoss(routeBackend, routeSet);
        int bossIdentityKey = GetObjectIdentityKey(request.Boss);

        return new BossRunProgressResult(
            request.Boss,
            routeSet,
            routeSetKey,
            isFinalRouteSet,
            bossIdentityKey,
            request.RewardModifiers);
    }

    public static bool ShouldTrackFinalBossCombat(BossRunProgressResult result)
    {
        return result.Boss != null && result.IsFinalRouteSet;
    }

    public static int GetObjectIdentityKey(UnityEngine.Object unityObject)
    {
        return unityObject != null ? RuntimeHelpers.GetHashCode(unityObject) : 0;
    }

    private static bool IsCurrentRouteFinalBoss(
        IRunRouteBackend routeBackend,
        CorridorBossRouteSetSO currentStage)
    {
        if (routeBackend == null || !routeBackend.HasActivePlan)
            return false;

        RunRouteCatalogSO activeCatalog = routeBackend.ActiveRouteCatalog;
        if (activeCatalog != null && currentStage != null)
            return ReferenceEquals(activeCatalog.FinalRouteSet, currentStage);

        return routeBackend.TotalStageCount > 0 &&
               routeBackend.CurrentStageIndex == routeBackend.TotalStageCount - 1;
    }

    private static int ResolveRouteSetKey(
        BossControllerBase boss,
        IRunRouteBackend routeBackend)
    {
        if (routeBackend != null && routeBackend.HasActivePlan)
        {
            CorridorBossRouteSetSO routeSet = routeBackend.CurrentStageSet;
            if (routeSet != null)
                return GetObjectIdentityKey(routeSet);

            return routeBackend.CurrentStageIndex + 1;
        }

        if (boss != null)
            return GetObjectIdentityKey(boss);

        return 0;
    }
}
