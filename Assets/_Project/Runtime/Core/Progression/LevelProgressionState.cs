using System;
using System.Collections.Generic;

/// <summary>
/// 책임: 현재 런에서만 유지되는 플레이어 레벨, 경험치, 미수령 레벨업 보상 수를 보관한다.
/// </summary>
[Serializable]
public sealed class LevelProgressionState
{
    public int level = 1;
    public int currentExperience;
    public int pendingRewardCount;
    public int rewardRandomSeed;
    public int rewardOfferSequence;
    public LevelRewardOfferState activeRewardOffer = new LevelRewardOfferState();
    public List<LevelRewardSelectionState> selectedRewards = new List<LevelRewardSelectionState>();

    public void Reset()
    {
        level = 1;
        currentExperience = 0;
        pendingRewardCount = 0;
        rewardRandomSeed = 0;
        rewardOfferSequence = 0;
        activeRewardOffer ??= new LevelRewardOfferState();
        activeRewardOffer.Clear();
        selectedRewards ??= new List<LevelRewardSelectionState>();
        selectedRewards.Clear();
    }
}

/// <summary>닫았다 다시 열어도 동일한 레벨업 후보와 리롤 횟수를 유지하는 런 저장 상태다.</summary>
[Serializable]
public sealed class LevelRewardOfferState
{
    public bool isActive;
    public int offerSequence;
    public int rerollsUsed;
    public int maxRerolls;
    public List<string> candidateRewardIds = new List<string>();

    public void Clear()
    {
        isActive = false;
        offerSequence = 0;
        rerollsUsed = 0;
        maxRerolls = 0;
        candidateRewardIds ??= new List<string>();
        candidateRewardIds.Clear();
    }
}

/// <summary>
/// 책임: 현재 런에서 선택한 레벨업 보상 하나와 그 보상에 포함된 효과별 런타임 상태를 저장한다.
/// </summary>
[Serializable]
public sealed class LevelRewardSelectionState
{
    public string rewardId;
    public List<LevelRewardEffectState> effectStates = new List<LevelRewardEffectState>();

    public LevelRewardSelectionState(string rewardId)
    {
        this.rewardId = rewardId;
    }

    public LevelRewardEffectState GetOrCreateEffectState(string effectId)
    {
        effectStates ??= new List<LevelRewardEffectState>();

        LevelRewardEffectState existing = effectStates.Find(x => x != null && x.effectId == effectId);
        if (existing != null)
            return existing;

        var created = new LevelRewardEffectState(effectId);
        effectStates.Add(created);
        return created;
    }
}

/// <summary>
/// 책임: 레벨업 효과의 씬 비참조 상태와 즉시 효과 적용 여부를 안정적인 effectId 기준으로 저장한다.
/// </summary>
[Serializable]
public sealed class LevelRewardEffectState
{
    public string effectId;
    public bool instantApplied;
    public string json;

    public LevelRewardEffectState(string effectId)
    {
        this.effectId = effectId;
    }
}

/// <summary>
/// 책임: 한 번의 경험치 지급으로 발생한 레벨 진행 결과를 호출자와 UI에 전달한다.
/// </summary>
public readonly struct LevelProgressionGrantResult
{
    public LevelProgressionGrantResult(
        int grantedExperience,
        int previousLevel,
        int currentLevel,
        int currentExperience,
        int pendingRewardCount,
        bool isMaxLevel)
    {
        GrantedExperience = grantedExperience;
        PreviousLevel = previousLevel;
        CurrentLevel = currentLevel;
        CurrentExperience = currentExperience;
        PendingRewardCount = pendingRewardCount;
        IsMaxLevel = isMaxLevel;
    }

    public int GrantedExperience { get; }
    public int PreviousLevel { get; }
    public int CurrentLevel { get; }
    public int LevelsGained => Math.Max(0, CurrentLevel - PreviousLevel);
    public int CurrentExperience { get; }
    public int PendingRewardCount { get; }
    public bool IsMaxLevel { get; }
    public bool Changed => GrantedExperience > 0 && (CurrentLevel != PreviousLevel || CurrentExperience > 0 || IsMaxLevel);
}

/// <summary>
/// 책임: Unity 오브젝트나 UI에 의존하지 않고 경험치 오버플로, 다중 레벨업, 최대 레벨 규칙을 계산한다.
/// </summary>
public static class LevelProgressionCalculator
{
    public static LevelProgressionGrantResult GrantExperience(
        LevelProgressionState state,
        int amount,
        IReadOnlyList<int> nextLevelRequirements)
    {
        if (state == null)
            throw new ArgumentNullException(nameof(state));

        if (nextLevelRequirements == null)
            throw new ArgumentNullException(nameof(nextLevelRequirements));

        NormalizeState(state, nextLevelRequirements.Count + 1);

        int previousLevel = state.level;
        int grantedExperience = Math.Max(0, amount);
        int maxLevel = nextLevelRequirements.Count + 1;

        if (grantedExperience == 0 || state.level >= maxLevel)
        {
            if (state.level >= maxLevel)
                state.currentExperience = 0;

            return BuildResult(state, 0, previousLevel, maxLevel);
        }

        long accumulatedExperience = (long)state.currentExperience + grantedExperience;
        while (state.level < maxLevel)
        {
            int requirementIndex = state.level - 1;
            int requirement = nextLevelRequirements[requirementIndex];
            if (requirement <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(nextLevelRequirements),
                    $"Level {state.level + 1} experience requirement must be greater than zero.");

            if (accumulatedExperience < requirement)
                break;

            accumulatedExperience -= requirement;
            state.level++;
            state.pendingRewardCount++;
        }

        state.currentExperience = state.level >= maxLevel
            ? 0
            : (int)Math.Min(int.MaxValue, accumulatedExperience);

        return BuildResult(state, grantedExperience, previousLevel, maxLevel);
    }

    public static bool TryConsumePendingReward(LevelProgressionState state)
    {
        if (state == null || state.pendingRewardCount <= 0)
            return false;

        state.pendingRewardCount--;
        return true;
    }

    private static void NormalizeState(LevelProgressionState state, int maxLevel)
    {
        state.level = Math.Max(1, Math.Min(Math.Max(1, maxLevel), state.level));
        state.currentExperience = Math.Max(0, state.currentExperience);
        state.pendingRewardCount = Math.Max(0, state.pendingRewardCount);
    }

    private static LevelProgressionGrantResult BuildResult(
        LevelProgressionState state,
        int grantedExperience,
        int previousLevel,
        int maxLevel)
    {
        return new LevelProgressionGrantResult(
            grantedExperience,
            previousLevel,
            state.level,
            state.currentExperience,
            state.pendingRewardCount,
            state.level >= maxLevel);
    }
}
