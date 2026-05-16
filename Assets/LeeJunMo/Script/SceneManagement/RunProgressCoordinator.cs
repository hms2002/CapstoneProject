using System;
using System.Collections.Generic;
using UnityEngine;

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
        if (boss == null)
            return;

        BossRunProgressResult progress = BuildProgressResult(boss, null);
        if (!BossRunProgressPolicy.ShouldTrackFinalBossCombat(progress))
            return;

        finalBossCombatOwners.Add(progress.BossIdentityKey);
        UpdateFinalBossCombatTimerPause();
    }

    public void NotifyBossCombatEnded(BossControllerBase boss)
    {
        if (boss == null)
            return;

        finalBossCombatOwners.Remove(BossRunProgressPolicy.GetObjectIdentityKey(boss));
        UpdateFinalBossCombatTimerPause();
    }

    public void NotifyBossDefeated(BossControllerBase boss)
    {
        NotifyBossCombatEnded(boss);

        BossDrop legacyDrop = boss != null ? boss.GetComponent<BossDrop>() : null;
        BossRunProgressResult progress = BuildProgressResult(boss, legacyDrop);
        if (!defeatedRouteKeys.Add(progress.RouteSetKey))
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
            BossRunProgressResult progress = BuildProgressResult(null, legacyDrop);
            if (defeatedRouteKeys.Add(progress.RouteSetKey))
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
        BossRewardModifierAggregate modifiers = RunModifierService.CurrentRewardSnapshot.BossRewardModifiers;
        return BuildProgressResult(boss, legacyDrop, modifiers).ToRewardContext();
    }

    private BossRunProgressResult BuildProgressResult(BossControllerBase boss, BossDrop legacyDrop)
    {
        return BuildProgressResult(boss, legacyDrop, default);
    }

    private BossRunProgressResult BuildProgressResult(
        BossControllerBase boss,
        BossDrop legacyDrop,
        BossRewardModifierAggregate modifiers)
    {
        return BossRunProgressPolicy.Evaluate(
            new BossRunProgressRequest(
                boss,
                legacyDrop,
                PortalRouteManager.Instance,
                modifiers));
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
}
