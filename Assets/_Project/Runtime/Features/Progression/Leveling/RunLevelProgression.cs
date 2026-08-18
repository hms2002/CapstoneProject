using System;

/// <summary>
/// 책임: UI와 게임플레이 호출자가 GamePlayData 세부 구조를 직접 수정하지 않고 현재 런의 레벨 진행을 조회/변경한다.
/// </summary>
public static class RunLevelProgression
{
    static RunLevelProgression()
    {
        RunSessionStore.OnRunStarted += NotifyStateChanged;
        RunSessionStore.OnRunEnded += HandleRunEnded;
    }

    public static event Action StateChanged;
    public static event Action<LevelProgressionGrantResult> ExperienceGranted;

    public static LevelProgressionState State
    {
        get
        {
            GamePlayData data = RunSessionStore.Data;
            if (data == null)
                return null;

            data.levelProgression ??= new LevelProgressionState();
            return data.levelProgression;
        }
    }

    public static bool TryGrantExperience(
        LevelProgressionConfigSO config,
        int amount,
        out LevelProgressionGrantResult result)
    {
        result = default;
        if (!RunSessionStore.IsRunActive || config == null || amount <= 0)
            return false;

        LevelProgressionState state = State;
        if (state == null)
            return false;

        result = LevelProgressionCalculator.GrantExperience(
            state,
            amount,
            config.NextLevelRequirements);

        ExperienceGranted?.Invoke(result);
        StateChanged?.Invoke();
        return true;
    }

    public static bool TryConsumePendingReward()
    {
        LevelProgressionState state = State;
        if (!RunSessionStore.IsRunActive || !LevelProgressionCalculator.TryConsumePendingReward(state))
            return false;

        StateChanged?.Invoke();
        return true;
    }

    private static void NotifyStateChanged()
    {
        StateChanged?.Invoke();
    }

    private static void HandleRunEnded(RunEndReason reason)
    {
        StateChanged?.Invoke();
    }
}
