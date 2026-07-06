using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
/// <summary>
/// 책임 : 보스 처치/보상 준비 상태를 런 route와 타이머 정지 정책에 연결하는 Infrastructure coordinator이다.
/// </summary>
public sealed class RunProgressCoordinator : MonoBehaviour, IRunProgressBackend
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
        RunProgressPlayback.RegisterBackend(this);
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
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        if (GamePlayDataManager.Instance != null)
        {
            GamePlayDataManager.Instance.OnRunStarted -= HandleRunStarted;
            GamePlayDataManager.Instance.OnRunEnded -= HandleRunEnded;
        }

        SceneManager.sceneLoaded -= HandleSceneLoaded;

        if (Instance == this)
            ReleaseFinalBossCombatTimerPause();
    }

    private void OnDestroy()
    {
        RunProgressPlayback.UnregisterBackend(this);

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

        BossRunProgressResult progress = BuildProgressResult(boss);
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

        BossRunProgressResult progress = BuildProgressResult(boss);
        if (!defeatedRouteKeys.Add(progress.RouteSetKey))
            return;

        RunTimeLimitSystem.Instance?.SetRunCompletionPaused(true);
    }

    public void NotifyBossRewardsReady(BossControllerBase boss)
    {
        DispatchBossRewardsReady(BuildContext(boss));
    }

    private void DispatchBossRewardsReady(BossRewardContext context)
    {
        if (context == null || !rewardsReadyRouteKeys.Add(context.RouteSetKey))
            return;

        BossRewardsReady?.Invoke(context);
        BossRewardFallbackService.HandleUnhandledFallbacks(context);
    }

    private BossRewardContext BuildContext(BossControllerBase boss)
    {
        BossRewardModifierAggregate modifiers = RunModifierService.CurrentRewardSnapshot.BossRewardModifiers;
        return BuildProgressResult(boss, modifiers).ToRewardContext();
    }

    private BossRunProgressResult BuildProgressResult(BossControllerBase boss)
    {
        return BuildProgressResult(boss, default);
    }

    private BossRunProgressResult BuildProgressResult(
        BossControllerBase boss,
        BossRewardModifierAggregate modifiers)
    {
        return BossRunProgressPolicy.Evaluate(
            new BossRunProgressRequest(
                boss,
                RunRoutePlayback.Backend,
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

    /// <summary>
    /// 책임:
    /// - 보스 처치 후 해당 보스 씬에서 멈춘 런 타이머를 다음 씬 진입 시 다시 흐르게 한다.
    /// - 보상/포탈 대기 중 정지는 유지하되, 씬 전환 후까지 run completion pause가 누수되지 않게 한다.
    /// </summary>
    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RunTimeLimitSystem.Instance?.SetRunCompletionPaused(false);
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
