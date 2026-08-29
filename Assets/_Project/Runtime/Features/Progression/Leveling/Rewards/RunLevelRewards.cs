using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임:
/// - 선택한 레벨업 보상 ID/상태는 GamePlayData에 남기고, 플레이어 오브젝트에 붙는 live 효과만 재구축한다.
/// - 씬 전환 시 기존 handle을 정리하고 새 플레이어 등록 시 Persistent 효과를 재적용한다.
/// </summary>
public static class RunLevelRewards
{
    private static readonly Dictionary<string, LevelRewardDefinitionSO> DefinitionsById =
        new Dictionary<string, LevelRewardDefinitionSO>(StringComparer.Ordinal);

    private static readonly List<ILevelRewardEffectHandle> ActiveHandles =
        new List<ILevelRewardEffectHandle>();

    static RunLevelRewards()
    {
        PlayerRuntimeRegistry.PlayerRegistered += HandlePlayerRegistered;
        PlayerRuntimeRegistry.PlayerUnregistered += HandlePlayerUnregistered;
        RunSessionStore.OnRunStarted += HandleRunStarted;
        RunSessionStore.OnRunEnded += HandleRunEnded;
    }

    public static event Action RewardsChanged;

    public static void RegisterCatalog(LevelRewardCatalogSO catalog)
    {
        if (catalog == null)
            return;

        IReadOnlyList<LevelRewardDefinitionSO> rewards = catalog.Rewards;
        for (int i = 0; i < rewards.Count; i++)
            RegisterDefinition(rewards[i]);

        RebuildActiveEffects();
    }

    public static void RegisterDefinition(LevelRewardDefinitionSO definition)
    {
        if (definition == null || string.IsNullOrWhiteSpace(definition.RewardId))
            return;

        if (DefinitionsById.TryGetValue(definition.RewardId, out LevelRewardDefinitionSO existing) &&
            existing != null && existing != definition)
        {
            Debug.LogWarning(
                $"[RunLevelRewards] Duplicate rewardId '{definition.RewardId}' was ignored. " +
                $"existing={existing.name}, incoming={definition.name}",
                definition);
            return;
        }

        DefinitionsById[definition.RewardId] = definition;
    }

    public static bool TryGetDefinition(string rewardId, out LevelRewardDefinitionSO definition)
    {
        definition = null;
        return !string.IsNullOrWhiteSpace(rewardId) &&
               DefinitionsById.TryGetValue(rewardId, out definition) &&
               definition != null;
    }

    public static bool HasSelected(string rewardId)
    {
        LevelProgressionState progression = RunLevelProgression.State;
        if (progression?.selectedRewards == null || string.IsNullOrWhiteSpace(rewardId))
            return false;

        return progression.selectedRewards.Exists(x => x != null && x.rewardId == rewardId);
    }

    public static void CollectEligibleDefinitions(List<LevelRewardDefinitionSO> results)
    {
        if (results == null)
            throw new ArgumentNullException(nameof(results));

        results.Clear();
        PlayerInteractor2D player = PlayerRuntimeRegistry.CurrentPlayer;
        LevelProgressionState progression = RunLevelProgression.State;
        if (!RunSessionStore.IsRunActive || player == null || progression == null)
            return;

        var context = new LevelRewardEligibilityContext(player, progression);
        foreach (LevelRewardDefinitionSO definition in DefinitionsById.Values)
        {
            if (definition == null) continue;
            if (!definition.AllowMultipleSelections && HasSelected(definition.RewardId)) continue;
            if (definition.CanSelect(context, out _))
                results.Add(definition);
        }

        results.Sort((left, right) => string.CompareOrdinal(left.RewardId, right.RewardId));
    }

    public static bool TrySelect(LevelRewardDefinitionSO definition, out string failureReason)
    {
        failureReason = null;
        if (!RunSessionStore.IsRunActive)
        {
            failureReason = "활성 런이 아닙니다.";
            return false;
        }

        LevelProgressionState progression = RunLevelProgression.State;
        if (progression == null || progression.pendingRewardCount <= 0)
        {
            failureReason = "선택 가능한 레벨업 보상이 없습니다.";
            return false;
        }

        PlayerInteractor2D player = PlayerRuntimeRegistry.CurrentPlayer;
        if (player == null)
        {
            failureReason = "현재 플레이어가 등록되지 않았습니다.";
            return false;
        }

        if (definition == null)
        {
            failureReason = "보상 정의가 없습니다.";
            return false;
        }

        RegisterDefinition(definition);
        if (!definition.AllowMultipleSelections && HasSelected(definition.RewardId))
        {
            failureReason = "이미 선택한 보상입니다.";
            return false;
        }

        var eligibility = new LevelRewardEligibilityContext(player, progression);
        if (!definition.CanSelect(eligibility, out failureReason))
            return false;

        progression.selectedRewards ??= new List<LevelRewardSelectionState>();
        var selectionState = new LevelRewardSelectionState(definition.RewardId);
        progression.selectedRewards.Add(selectionState);

        ApplySelection(player, progression, definition, selectionState, isReapply: false);

        if (!RunLevelProgression.TryConsumePendingReward())
        {
            progression.selectedRewards.Remove(selectionState);
            RebuildActiveEffects();
            failureReason = "레벨업 보상 소비에 실패했습니다.";
            return false;
        }

        RewardsChanged?.Invoke();
        return true;
    }

    public static void RebuildActiveEffects()
    {
        DisposeActiveHandles();

        if (!RunSessionStore.IsRunActive)
            return;

        PlayerInteractor2D player = PlayerRuntimeRegistry.CurrentPlayer;
        LevelProgressionState progression = RunLevelProgression.State;
        if (player == null || progression?.selectedRewards == null)
            return;

        for (int i = 0; i < progression.selectedRewards.Count; i++)
        {
            LevelRewardSelectionState selection = progression.selectedRewards[i];
            if (selection == null || !TryGetDefinition(selection.rewardId, out LevelRewardDefinitionSO definition))
                continue;

            ApplySelection(player, progression, definition, selection, isReapply: true);
        }
    }

    private static void ApplySelection(
        PlayerInteractor2D player,
        LevelProgressionState progression,
        LevelRewardDefinitionSO definition,
        LevelRewardSelectionState selectionState,
        bool isReapply)
    {
        IReadOnlyList<LevelRewardEffectSO> effects = definition.Effects;
        for (int i = 0; i < effects.Count; i++)
        {
            LevelRewardEffectSO effect = effects[i];
            if (effect == null || string.IsNullOrWhiteSpace(effect.EffectId))
                continue;

            LevelRewardEffectState effectState = selectionState.GetOrCreateEffectState(effect.EffectId);
            if (effect.Lifetime == LevelRewardEffectLifetime.InstantOnce && effectState.instantApplied)
                continue;

            var applyContext = new LevelRewardApplyContext(
                player,
                progression,
                selectionState,
                effectState,
                isReapply);

            ILevelRewardEffectHandle handle = effect.Apply(applyContext);
            if (handle != null)
                ActiveHandles.Add(handle);

            if (effect.Lifetime == LevelRewardEffectLifetime.InstantOnce)
                effectState.instantApplied = true;
        }
    }

    private static void HandlePlayerRegistered(PlayerInteractor2D player)
    {
        RebuildActiveEffects();
    }

    private static void HandlePlayerUnregistered(PlayerInteractor2D player)
    {
        DisposeActiveHandles();
    }

    private static void HandleRunStarted()
    {
        DisposeActiveHandles();
        RewardsChanged?.Invoke();
    }

    private static void HandleRunEnded(RunEndReason reason)
    {
        DisposeActiveHandles();
        RewardsChanged?.Invoke();
    }

    private static void DisposeActiveHandles()
    {
        for (int i = ActiveHandles.Count - 1; i >= 0; i--)
        {
            try
            {
                ActiveHandles[i]?.Dispose();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        ActiveHandles.Clear();
    }
}
