using System.Globalization;
using UnityEngine;

[System.Obsolete("Use RunSpecialNpcDialogueSetSO on RunSpecialNpcInteractor. This provider type is kept only for migration.")]
[AddComponentMenu("")]
[DisallowMultipleComponent]
public sealed class RunConstructionNpcDialogueProvider : RunSpecialNpcDialogueProviderBase
{
    [SerializeField] private RunConstructionNpcFeature feature;
    [SerializeField] private RunSpecialNpcLine[] notStartedLines = System.Array.Empty<RunSpecialNpcLine>();
    [SerializeField] private RunSpecialNpcLine[] pendingLines = System.Array.Empty<RunSpecialNpcLine>();
    [SerializeField] private RunSpecialNpcLine[] completedLines = System.Array.Empty<RunSpecialNpcLine>();
    [SerializeField] private RunSpecialNpcChoice[] availableChoices = System.Array.Empty<RunSpecialNpcChoice>();

    private const string RemainingDaysToken = "N\uC77C";

    private void Awake()
    {
        ResolveFeature();
    }

    public override RunSpecialNpcBranch BuildBranch(RunSpecialNpcFeatureContext context)
    {
        ResolveFeature();

        if (feature == null)
        {
            CapstoneDiagnostics.EditorOnlyLog.LogWarning("[RunConstructionNpcDialogueProvider] Construction feature is missing.", this);
            return RunSpecialNpcBranch.Empty;
        }

        if (feature.HasConstructionStarted() && !feature.IsConstructionComplete())
            return RunSpecialNpcBranch.LinesOnly(pendingLines);

        if (feature.IsConstructionComplete())
            return RunSpecialNpcBranch.LinesOnly(completedLines);

        return RunSpecialNpcBranch.WithChoices(
            notStartedLines,
            FilterAvailableChoices(availableChoices, context));
    }

    public override string ResolveLineText(
        RunSpecialNpcLine line,
        string text,
        RunSpecialNpcFeatureContext context)
    {
        if (feature == null ||
            string.IsNullOrWhiteSpace(text) ||
            (text.IndexOf(RemainingDaysToken, System.StringComparison.Ordinal) < 0 &&
             text.IndexOf("{remaining_runs}", System.StringComparison.Ordinal) < 0))
        {
            return text;
        }

        string remainingRuns = feature.GetRemainingRunCompletions()
            .ToString(CultureInfo.InvariantCulture);
        string displayDays = GameText.Format("units.days.value", "{0}일", remainingRuns);
        return text.Replace(RemainingDaysToken, displayDays).Replace("{remaining_runs}", displayDays);
    }

    private void ResolveFeature()
    {
        if (feature != null)
            return;

        feature = GetComponent<RunConstructionNpcFeature>();
    }
}
