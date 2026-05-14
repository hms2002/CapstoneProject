using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

public sealed class BossRewardContext
{
    public BossRewardContext(
        BossControllerBase boss,
        BossDrop legacyBossDrop,
        CorridorBossRouteSetSO routeSet,
        int routeSetKey,
        bool isFinalRouteSet,
        BossRewardModifierAggregate rewardModifiers)
    {
        Boss = boss;
        LegacyBossDrop = legacyBossDrop;
        RouteSet = routeSet;
        RouteSetKey = routeSetKey;
        IsFinalRouteSet = isFinalRouteSet;
        RewardModifiers = rewardModifiers;
    }

    public BossControllerBase Boss { get; }
    public BossDrop LegacyBossDrop { get; }
    public CorridorBossRouteSetSO RouteSet { get; }
    public int RouteSetKey { get; }
    public bool IsFinalRouteSet { get; }
    public BossRewardModifierAggregate RewardModifiers { get; }
    public bool RewardsHandled { get; private set; }
    public bool PortalHandled { get; private set; }

    public void MarkRewardsHandled()
    {
        RewardsHandled = true;
    }

    public void MarkPortalHandled()
    {
        PortalHandled = true;
    }
}

[DisallowMultipleComponent]
public sealed class RunProgressCoordinator : MonoBehaviour
{
    public static RunProgressCoordinator Instance { get; private set; }

    public event Action<BossRewardContext> BossRewardsReady;

    private static bool s_isQuitting;

    private readonly HashSet<int> defeatedRouteKeys = new HashSet<int>();
    private readonly HashSet<int> rewardsReadyRouteKeys = new HashSet<int>();
    private readonly HashSet<int> finalBossCombatOwners = new HashSet<int>();

    private bool holdsFinalBossCombatTimerPause;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoBootstrap()
    {
        if (s_isQuitting || Instance != null)
            return;

        EnsureInstance();
    }

    public static RunProgressCoordinator EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        RunProgressCoordinator existing = FindFirstObjectByType<RunProgressCoordinator>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        if (s_isQuitting)
            return null;

        var go = new GameObject(nameof(RunProgressCoordinator));
        return go.AddComponent<RunProgressCoordinator>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        if (Instance != this)
            return;

        GamePlayDataManager gameplay = GamePlayDataManager.EnsureInstance();
        if (gameplay == null)
            return;

        gameplay.OnRunStarted += HandleRunStarted;
        gameplay.OnRunEnded += HandleRunEnded;
    }

    private void OnDisable()
    {
        if (GamePlayDataManager.Instance != null)
        {
            GamePlayDataManager.Instance.OnRunStarted -= HandleRunStarted;
            GamePlayDataManager.Instance.OnRunEnded -= HandleRunEnded;
        }

        if (Instance == this)
            ReleaseFinalBossCombatTimerPause();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnApplicationQuit()
    {
        s_isQuitting = true;
    }

    public void NotifyBossCombatStarted(BossControllerBase boss)
    {
        if (boss == null || !IsCurrentRouteFinalBoss())
            return;

        finalBossCombatOwners.Add(GetObjectIdentityKey(boss));
        UpdateFinalBossCombatTimerPause();
    }

    public void NotifyBossCombatEnded(BossControllerBase boss)
    {
        if (boss == null)
            return;

        finalBossCombatOwners.Remove(GetObjectIdentityKey(boss));
        UpdateFinalBossCombatTimerPause();
    }

    public void NotifyBossDefeated(BossControllerBase boss)
    {
        NotifyBossCombatEnded(boss);

        BossDrop legacyDrop = boss != null ? boss.GetComponent<BossDrop>() : null;
        int routeKey = ResolveRouteSetKey(boss, legacyDrop);
        if (!defeatedRouteKeys.Add(routeKey))
            return;

        RunTimeLimitSystem.Instance?.SetRunCompletionPaused(true);
    }

    public void NotifyBossRewardsReady(BossControllerBase boss)
    {
        BossDrop legacyDrop = boss != null ? boss.GetComponent<BossDrop>() : null;
        DispatchBossRewardsReady(BuildContext(boss, legacyDrop));
    }

    public void NotifyLegacyBossRewardsReady(BossDrop legacyDrop)
    {
        if (legacyDrop == null)
            return;

        BossControllerBase boss = legacyDrop.GetComponent<BossControllerBase>();
        if (boss != null)
        {
            NotifyBossDefeated(boss);
        }
        else
        {
            int routeKey = ResolveRouteSetKey(null, legacyDrop);
            if (defeatedRouteKeys.Add(routeKey))
                RunTimeLimitSystem.Instance?.SetRunCompletionPaused(true);
        }

        DispatchBossRewardsReady(BuildContext(boss, legacyDrop));
    }

    private void DispatchBossRewardsReady(BossRewardContext context)
    {
        if (context == null || !rewardsReadyRouteKeys.Add(context.RouteSetKey))
            return;

        BossRewardsReady?.Invoke(context);

        BossDrop legacyDrop = context.LegacyBossDrop;
        if (legacyDrop == null && context.Boss != null)
            legacyDrop = context.Boss.GetComponent<BossDrop>();

        if (!context.RewardsHandled && BossRewardSpawner.SpawnFromLegacyDrop(legacyDrop, context))
            context.MarkRewardsHandled();

        if (!context.PortalHandled && BossExitPortalActivator.ActivateFromLegacyDrop(legacyDrop, context))
            context.MarkPortalHandled();
    }

    private BossRewardContext BuildContext(BossControllerBase boss, BossDrop legacyDrop)
    {
        PortalRouteManager routeManager = PortalRouteManager.Instance;
        CorridorBossRouteSetSO routeSet = routeManager != null ? routeManager.CurrentStageSet : null;
        int routeKey = ResolveRouteSetKey(boss, legacyDrop);
        bool isFinalRouteSet = IsCurrentRouteFinalBoss(routeManager, routeSet);
        BossRewardModifierAggregate modifiers = RunModifierService.Instance != null
            ? RunModifierService.Instance.BossRewardModifiers
            : default;

        return new BossRewardContext(boss, legacyDrop, routeSet, routeKey, isFinalRouteSet, modifiers);
    }

    private static int ResolveRouteSetKey(BossControllerBase boss, BossDrop legacyDrop)
    {
        PortalRouteManager routeManager = PortalRouteManager.Instance;
        if (routeManager != null && routeManager.HasActivePlan)
        {
            CorridorBossRouteSetSO routeSet = routeManager.CurrentStageSet;
            if (routeSet != null)
                return GetObjectIdentityKey(routeSet);

            return routeManager.CurrentStageIndex + 1;
        }

        if (boss != null)
            return GetObjectIdentityKey(boss);

        if (legacyDrop != null)
            return GetObjectIdentityKey(legacyDrop);

        return 0;
    }

    private static int GetObjectIdentityKey(UnityEngine.Object unityObject)
    {
        return unityObject != null ? RuntimeHelpers.GetHashCode(unityObject) : 0;
    }

    private void HandleRunStarted()
    {
        ClearRunScopedState();
    }

    private void HandleRunEnded(RunEndReason reason)
    {
        ClearRunScopedState();
    }

    private void ClearRunScopedState()
    {
        defeatedRouteKeys.Clear();
        rewardsReadyRouteKeys.Clear();
        finalBossCombatOwners.Clear();
        ReleaseFinalBossCombatTimerPause();
    }

    private void UpdateFinalBossCombatTimerPause()
    {
        if (finalBossCombatOwners.Count > 0)
        {
            AcquireFinalBossCombatTimerPause();
            return;
        }

        ReleaseFinalBossCombatTimerPause();
    }

    private void AcquireFinalBossCombatTimerPause()
    {
        if (holdsFinalBossCombatTimerPause || RunTimeLimitSystem.Instance == null)
            return;

        RunTimeLimitSystem.Instance.SetExternalPause(this, true);
        holdsFinalBossCombatTimerPause = true;
    }

    private void ReleaseFinalBossCombatTimerPause()
    {
        if (!holdsFinalBossCombatTimerPause)
            return;

        if (RunTimeLimitSystem.Instance != null)
            RunTimeLimitSystem.Instance.SetExternalPause(this, false);

        holdsFinalBossCombatTimerPause = false;
    }

    private static bool IsCurrentRouteFinalBoss()
    {
        PortalRouteManager routeManager = PortalRouteManager.Instance;
        return IsCurrentRouteFinalBoss(routeManager, routeManager != null ? routeManager.CurrentStageSet : null);
    }

    private static bool IsCurrentRouteFinalBoss(PortalRouteManager routeManager, CorridorBossRouteSetSO currentStage)
    {
        if (routeManager == null || !routeManager.HasActivePlan)
            return false;

        RunRouteCatalogSO activeCatalog = routeManager.ActiveRouteCatalog;
        if (activeCatalog != null && currentStage != null)
            return ReferenceEquals(activeCatalog.FinalRouteSet, currentStage);

        return routeManager.TotalStageCount > 0 &&
               routeManager.CurrentStageIndex == routeManager.TotalStageCount - 1;
    }
}
