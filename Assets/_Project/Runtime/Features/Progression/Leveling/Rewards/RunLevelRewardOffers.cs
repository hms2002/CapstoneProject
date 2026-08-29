using System;
using System.Collections.Generic;

/// <summary>
/// 레벨업 후보 3개, 런 단위 결정론적 시드, 현재 오퍼와 리롤 상태를 저장 데이터 위에서 관리한다.
/// </summary>
public static class RunLevelRewardOffers
{
    private const int CandidateCount = 3;
    private static readonly List<LevelRewardDefinitionSO> EligibleBuffer = new();
    private static readonly List<LevelRewardDefinitionSO> CandidateBuffer = new();

    public static event Action OfferChanged;

    public static IReadOnlyList<LevelRewardDefinitionSO> CurrentCandidates
    {
        get
        {
            RebuildCandidateBuffer();
            return CandidateBuffer;
        }
    }

    public static int RerollsUsed => GetOffer()?.rerollsUsed ?? 0;
    public static int MaxRerolls => GetOffer()?.maxRerolls ?? 0;
    public static bool CanReroll => GetOffer() is { isActive: true } offer && offer.rerollsUsed < offer.maxRerolls;

    public static bool TryEnsureOffer(int maxRerolls, out string failureReason)
    {
        failureReason = null;
        LevelProgressionState progression = RunLevelProgression.State;
        if (!RunSessionStore.IsRunActive || progression == null || progression.pendingRewardCount <= 0)
        {
            failureReason = "선택 가능한 레벨업 보상이 없습니다.";
            return false;
        }

        progression.activeRewardOffer ??= new LevelRewardOfferState();
        LevelRewardOfferState offer = progression.activeRewardOffer;
        offer.candidateRewardIds ??= new List<string>();
        if (offer.isActive && offer.candidateRewardIds.Count > 0)
            return true;

        EnsureSeed(progression);
        offer.isActive = true;
        offer.offerSequence = progression.rewardOfferSequence;
        offer.rerollsUsed = 0;
        offer.maxRerolls = Math.Max(0, maxRerolls);
        if (!RollCandidates(progression, offer))
        {
            offer.Clear();
            failureReason = "현재 선택 가능한 레벨업 효과가 없습니다.";
            return false;
        }

        OfferChanged?.Invoke();
        return true;
    }

    public static bool TryReroll(out string failureReason)
    {
        failureReason = null;
        LevelProgressionState progression = RunLevelProgression.State;
        LevelRewardOfferState offer = progression?.activeRewardOffer;
        if (offer == null || !offer.isActive)
        {
            failureReason = "활성 레벨업 후보가 없습니다.";
            return false;
        }

        if (offer.rerollsUsed >= offer.maxRerolls)
        {
            failureReason = "리롤 횟수를 모두 사용했습니다.";
            return false;
        }

        offer.rerollsUsed++;
        if (!RollCandidates(progression, offer))
        {
            offer.rerollsUsed--;
            failureReason = "리롤할 수 있는 후보가 없습니다.";
            return false;
        }

        OfferChanged?.Invoke();
        return true;
    }

    public static bool TrySelectCandidate(string rewardId, out string failureReason)
    {
        failureReason = null;
        LevelProgressionState progression = RunLevelProgression.State;
        LevelRewardOfferState offer = progression?.activeRewardOffer;
        if (offer == null || !offer.isActive || offer.candidateRewardIds == null ||
            !offer.candidateRewardIds.Contains(rewardId))
        {
            failureReason = "현재 후보에 없는 보상입니다.";
            return false;
        }

        if (!RunLevelRewards.TryGetDefinition(rewardId, out LevelRewardDefinitionSO definition) ||
            !RunLevelRewards.TrySelect(definition, out failureReason))
        {
            return false;
        }

        offer.Clear();
        progression.rewardOfferSequence++;
        OfferChanged?.Invoke();
        return true;
    }

    public static int GetDeterministicEffectSeed(string effectId)
    {
        LevelProgressionState progression = RunLevelProgression.State;
        if (progression == null) return 1;
        EnsureSeed(progression);
        return CombineSeed(
            progression.rewardRandomSeed,
            progression.rewardOfferSequence,
            effectId != null ? StringComparer.Ordinal.GetHashCode(effectId) : 0);
    }

    private static bool RollCandidates(LevelProgressionState progression, LevelRewardOfferState offer)
    {
        RunLevelRewards.CollectEligibleDefinitions(EligibleBuffer);
        offer.candidateRewardIds.Clear();
        if (EligibleBuffer.Count == 0) return false;

        var random = new Random(CombineSeed(
            progression.rewardRandomSeed,
            offer.offerSequence,
            offer.rerollsUsed));

        for (int i = 0; i < EligibleBuffer.Count; i++)
        {
            int picked = random.Next(i, EligibleBuffer.Count);
            (EligibleBuffer[i], EligibleBuffer[picked]) = (EligibleBuffer[picked], EligibleBuffer[i]);
        }

        int count = Math.Min(CandidateCount, EligibleBuffer.Count);
        for (int i = 0; i < count; i++)
            offer.candidateRewardIds.Add(EligibleBuffer[i].RewardId);
        return true;
    }

    private static void RebuildCandidateBuffer()
    {
        CandidateBuffer.Clear();
        LevelRewardOfferState offer = GetOffer();
        if (offer?.candidateRewardIds == null) return;
        for (int i = 0; i < offer.candidateRewardIds.Count; i++)
        {
            if (RunLevelRewards.TryGetDefinition(offer.candidateRewardIds[i], out LevelRewardDefinitionSO definition))
                CandidateBuffer.Add(definition);
        }
    }

    private static LevelRewardOfferState GetOffer()
    {
        return RunLevelProgression.State?.activeRewardOffer;
    }

    private static void EnsureSeed(LevelProgressionState progression)
    {
        if (progression.rewardRandomSeed != 0) return;
        progression.rewardRandomSeed = Guid.NewGuid().GetHashCode();
        if (progression.rewardRandomSeed == 0)
            progression.rewardRandomSeed = 1;
    }

    private static int CombineSeed(int first, int second, int third)
    {
        unchecked
        {
            int hash = first;
            hash = (hash * 397) ^ second;
            hash = (hash * 397) ^ third;
            return hash;
        }
    }
}
