using System.Collections;
using System.Globalization;
using UnityEngine;

public sealed class RunConstructionNpcFeature : RunSpecialNpcFeatureBase
{
    [Header("Stable ID")]
    [SerializeField] private string constructionId;

    [Header("Cost / Progress")]
    [SerializeField, Min(0)] private int magicStoneCost = 50;
    [SerializeField, Min(0)] private int requiredRunCompletions = 3;

    [Header("Construction Site")]
    [SerializeField] private ConstructionSiteTilemapModule constructionSiteModule;

    [Header("Shortcut Target")]
    [SerializeField] private DoorObject targetDoor;
    [SerializeField] private bool openTargetDoorOnCompletion = true;
    [SerializeField] private bool saveShortcutOnCompletion = true;
    [SerializeField] private bool playDoorPresentationOnCompletion;

    [Header("Authored Visual State")]
    [SerializeField] private GameObject blockedStateRoot;
    [SerializeField] private GameObject openStateRoot;
    [SerializeField] private bool toggleAuthoredStateRoots = true;

    private const string RemainingDaysToken = "N\uC77C";

    public override RunSpecialNpcFeatureKind DialogueFeatureKind => RunSpecialNpcFeatureKind.Construction;

    private void OnEnable()
    {
        RefreshProgressVisuals();
        if (IsConstructionComplete())
            CompleteConstruction(null);
    }

    public override RunSpecialNpcDialogueBranchKey GetDialogueBranchKey(RunSpecialNpcFeatureContext context)
    {
        if (HasConstructionStarted() && !IsConstructionComplete())
            return RunSpecialNpcDialogueBranchKey.ConstructionPending;

        if (IsConstructionComplete())
            return RunSpecialNpcDialogueBranchKey.ConstructionCompleted;

        return RunSpecialNpcDialogueBranchKey.ConstructionNotStarted;
    }

    public override bool CanExecute(RunSpecialNpcFeatureContext context)
    {
        return base.CanExecute(context) &&
               !string.IsNullOrWhiteSpace(constructionId) &&
               (!HasConstructionStarted() || IsConstructionComplete()) &&
               (HasConstructionStarted() || HasEnoughMagicStone());
    }

    public override string GetUnavailableReason(RunSpecialNpcFeatureContext context)
    {
        if (string.IsNullOrWhiteSpace(constructionId))
            return "Construction ID is missing.";

        if (HasConstructionStarted() && !IsConstructionComplete())
            return $"Construction is pending. {GetRemainingRunCompletions()} run completions remain.";

        if (!HasEnoughMagicStone())
            return $"Need {magicStoneCost} magic stones.";

        return string.Empty;
    }

    public override bool ShouldShowUnavailableChoice(
        RunSpecialNpcChoiceAction action,
        RunSpecialNpcFeatureContext context)
    {
        return action == RunSpecialNpcChoiceAction.ExecutePrimaryFeature &&
               IsPaymentShortageBeforeConstructionStart();
    }

    public override string ResolveDialogueLineText(string text, RunSpecialNpcFeatureContext context)
    {
        if (string.IsNullOrWhiteSpace(text) ||
            text.IndexOf(RemainingDaysToken, System.StringComparison.Ordinal) < 0)
        {
            return text;
        }

        string remainingRuns = GetRemainingRunCompletions()
            .ToString(CultureInfo.InvariantCulture);
        return text.Replace(RemainingDaysToken, remainingRuns + "\uC77C");
    }

    public override IEnumerator Execute(RunSpecialNpcFeatureContext context)
    {
        RefreshProgressVisuals();

        if (IsConstructionComplete())
        {
            CompleteConstruction(context);
            yield break;
        }

        if (!HasConstructionStarted())
            TryStartConstruction();

        if (IsConstructionComplete())
            CompleteConstruction(context);

        RefreshProgressVisuals();
        yield break;
    }

    public bool HasConstructionStarted()
    {
        return RunSpecialNpcConstructionProgress.HasStarted(constructionId);
    }

    public bool IsConstructionComplete()
    {
        return RunSpecialNpcConstructionProgress.IsCompleted(constructionId, requiredRunCompletions);
    }

    public int GetRemainingRunCompletions()
    {
        return RunSpecialNpcConstructionProgress.GetRemainingRunCompletions(
            constructionId,
            requiredRunCompletions);
    }

    public bool TryStartConstruction()
    {
        if (string.IsNullOrWhiteSpace(constructionId))
        {
            Debug.LogWarning("[RunConstructionNpcFeature] Missing constructionId.", this);
            return false;
        }

        if (HasConstructionStarted())
            return false;

        if (magicStoneCost > 0)
        {
            if (CurrencyManager.Instance == null || !CurrencyManager.Instance.SpendMagicStone(magicStoneCost))
                return false;
        }

        bool started = RunSpecialNpcConstructionProgress.TryStart(constructionId, out _);
        if (!started)
            Debug.LogWarning($"[RunConstructionNpcFeature] Could not start construction '{constructionId}'.", this);

        return started;
    }

    private bool HasEnoughMagicStone()
    {
        return magicStoneCost <= 0 ||
               CurrencyManager.Instance != null && CurrencyManager.Instance.GetMagicStone() >= magicStoneCost;
    }

    private bool IsPaymentShortageBeforeConstructionStart()
    {
        return !string.IsNullOrWhiteSpace(constructionId) &&
               !HasConstructionStarted() &&
               !IsConstructionComplete() &&
               !HasEnoughMagicStone();
    }

    public void RefreshProgressVisuals()
    {
        bool isComplete = IsConstructionComplete();

        if (constructionSiteModule != null)
        {
            constructionSiteModule.ApplyState(isComplete);
            return;
        }

        if (!toggleAuthoredStateRoots)
            return;

        if (blockedStateRoot != null)
            blockedStateRoot.SetActive(!isComplete);

        if (openStateRoot != null)
            openStateRoot.SetActive(isComplete);
    }

    private void CompleteConstruction(RunSpecialNpcFeatureContext context)
    {
        RunSpecialNpcConstructionProgress.MarkCompleted(constructionId);

        GameObject instigator = context?.Player?.Transform != null
            ? context.Player.Transform.gameObject
            : gameObject;

        if (constructionSiteModule != null)
        {
            constructionSiteModule.ApplyCompletedState(instigator);
            return;
        }

        RefreshProgressVisuals();
        OpenFallbackTargetDoor(instigator);
    }

    private void OpenFallbackTargetDoor(GameObject instigator)
    {
        if (!openTargetDoorOnCompletion || targetDoor == null)
            return;

        if (!targetDoor.IsOpen)
        {
            targetDoor.ForceOpen(
                immediate: true,
                save: saveShortcutOnCompletion,
                instigator: instigator,
                playPresentation: playDoorPresentationOnCompletion);
            return;
        }

        if (saveShortcutOnCompletion && ShortcutProgressService.Instance != null)
            ShortcutProgressService.Instance.UnlockShortcut(targetDoor.mapID, targetDoor.doorID);
    }
}
