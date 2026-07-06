using System;

/// <summary>
/// 책임 : 보스 전투/처치/보상 준비 진행 이벤트를 구체 runtime coordinator 없이 전달하는 Gameplay backend 계약이다.
/// </summary>
public interface IRunProgressBackend
{
    event Action<BossRewardContext> BossRewardsReady;

    void NotifyBossCombatStarted(BossControllerBase boss);
    void NotifyBossCombatEnded(BossControllerBase boss);
    void NotifyBossDefeated(BossControllerBase boss);
    void NotifyBossRewardsReady(BossControllerBase boss);
}

/// <summary>
/// 책임 : 보스 Gameplay 코드가 Infrastructure run progress coordinator 타입 없이 런 진행 이벤트를 발행/구독하게 한다.
/// </summary>
public static class RunProgressPlayback
{
    private static IRunProgressBackend backend;

    public static event Action<BossRewardContext> BossRewardsReady;

    public static void RegisterBackend(IRunProgressBackend progressBackend)
    {
        if (backend != null)
            backend.BossRewardsReady -= HandleBossRewardsReady;

        backend = progressBackend;

        if (backend != null)
            backend.BossRewardsReady += HandleBossRewardsReady;
    }

    public static void UnregisterBackend(IRunProgressBackend progressBackend)
    {
        if (!ReferenceEquals(backend, progressBackend))
            return;

        backend.BossRewardsReady -= HandleBossRewardsReady;
        backend = null;
    }

    public static void NotifyBossCombatStarted(BossControllerBase boss)
    {
        backend?.NotifyBossCombatStarted(boss);
    }

    public static void NotifyBossCombatEnded(BossControllerBase boss)
    {
        backend?.NotifyBossCombatEnded(boss);
    }

    public static void NotifyBossDefeated(BossControllerBase boss)
    {
        backend?.NotifyBossDefeated(boss);
    }

    public static void NotifyBossRewardsReady(BossControllerBase boss)
    {
        backend?.NotifyBossRewardsReady(boss);
    }

    private static void HandleBossRewardsReady(BossRewardContext context)
    {
        BossRewardsReady?.Invoke(context);
    }
}
