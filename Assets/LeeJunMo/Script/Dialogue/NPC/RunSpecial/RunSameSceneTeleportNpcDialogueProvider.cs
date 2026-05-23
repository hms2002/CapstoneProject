using UnityEngine;

[System.Obsolete("Use RunSpecialNpcDialogueSetSO on RunSpecialNpcInteractor. This provider type is kept only for migration.")]
[AddComponentMenu("")]
[DisallowMultipleComponent]
public sealed class RunSameSceneTeleportNpcDialogueProvider : RunSpecialNpcDialogueProviderBase
{
    [SerializeField] private RunSameSceneTeleportNpcFeature feature;
    [SerializeField] private RunSpecialNpcLine[] availableLines = System.Array.Empty<RunSpecialNpcLine>();
    [SerializeField] private RunSpecialNpcLine[] lockedLines = System.Array.Empty<RunSpecialNpcLine>();
    [SerializeField] private RunSpecialNpcLine[] unavailableLines = System.Array.Empty<RunSpecialNpcLine>();
    [SerializeField] private RunSpecialNpcChoice[] availableChoices = System.Array.Empty<RunSpecialNpcChoice>();

    private void Awake()
    {
        ResolveFeature();
    }

    public override RunSpecialNpcBranch BuildBranch(RunSpecialNpcFeatureContext context)
    {
        ResolveFeature();

        if (feature == null)
        {
            Debug.LogWarning("[RunSameSceneTeleportNpcDialogueProvider] Teleport feature is missing.", this);
            return RunSpecialNpcBranch.LinesOnly(unavailableLines);
        }

        if (!feature.HasRequiredAffection())
            return RunSpecialNpcBranch.LinesOnly(lockedLines);

        if (!feature.HasDestination || !feature.CanExecute(context))
        {
            string reason = feature.GetUnavailableReason(context);
            if (!string.IsNullOrWhiteSpace(reason))
                Debug.LogWarning($"[RunSameSceneTeleportNpcDialogueProvider] {reason}", this);

            return RunSpecialNpcBranch.LinesOnly(unavailableLines);
        }

        return RunSpecialNpcBranch.WithChoices(
            availableLines,
            FilterAvailableChoices(availableChoices, context));
    }

    private void ResolveFeature()
    {
        if (feature != null)
            return;

        feature = GetComponent<RunSameSceneTeleportNpcFeature>();
    }
}
