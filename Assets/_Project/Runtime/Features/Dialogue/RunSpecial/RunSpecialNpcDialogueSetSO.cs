using UnityEngine;

[CreateAssetMenu(
    fileName = "RunSpecialNpcDialogueSet",
    menuName = "Dialogue/Run Special NPC Dialogue Set")]
public sealed class RunSpecialNpcDialogueSetSO : ScriptableObject
{
    private const string DefaultConstructionInsufficientFundsLine =
        "공사 대금이 부족한 것 같아. 마정석을 더 모아 와.";

    [SerializeField] private RunSpecialNpcFeatureKind featureKind;

    [Header("Construction")]
    [SerializeField] private RunSpecialNpcDialogueBranchDefinition constructionNotStarted = new();
    [SerializeField] private RunSpecialNpcDialogueBranchDefinition constructionInsufficientFunds =
        RunSpecialNpcDialogueBranchDefinition.LinesOnly(
            new RunSpecialNpcLine(DefaultConstructionInsufficientFundsLine));
    [SerializeField] private RunSpecialNpcDialogueBranchDefinition constructionPending = new();
    [SerializeField] private RunSpecialNpcDialogueBranchDefinition constructionCompleted = new();

    [Header("Same Scene Teleport")]
    [SerializeField] private RunSpecialNpcDialogueBranchDefinition teleportAvailable = new();
    [SerializeField] private RunSpecialNpcDialogueBranchDefinition teleportLocked = new();
    [SerializeField] private RunSpecialNpcDialogueBranchDefinition teleportUnavailable = new();

    private static readonly RunSpecialNpcDialogueBranchDefinition EmptyBranch = new();

    public RunSpecialNpcFeatureKind FeatureKind => featureKind;

    public RunSpecialNpcDialogueBranchDefinition GetBranch(RunSpecialNpcDialogueBranchKey key)
    {
        var branch = key switch
        {
            RunSpecialNpcDialogueBranchKey.ConstructionNotStarted => constructionNotStarted ?? EmptyBranch,
            RunSpecialNpcDialogueBranchKey.ConstructionInsufficientFunds => constructionInsufficientFunds ?? EmptyBranch,
            RunSpecialNpcDialogueBranchKey.ConstructionPending => constructionPending ?? EmptyBranch,
            RunSpecialNpcDialogueBranchKey.ConstructionCompleted => constructionCompleted ?? EmptyBranch,
            RunSpecialNpcDialogueBranchKey.TeleportAvailable => teleportAvailable ?? EmptyBranch,
            RunSpecialNpcDialogueBranchKey.TeleportLocked => teleportLocked ?? EmptyBranch,
            RunSpecialNpcDialogueBranchKey.TeleportUnavailable => teleportUnavailable ?? EmptyBranch,
            _ => EmptyBranch,
        };
        string field = key switch
        {
            RunSpecialNpcDialogueBranchKey.ConstructionNotStarted => "constructionNotStarted",
            RunSpecialNpcDialogueBranchKey.ConstructionInsufficientFunds => "constructionInsufficientFunds",
            RunSpecialNpcDialogueBranchKey.ConstructionPending => "constructionPending",
            RunSpecialNpcDialogueBranchKey.ConstructionCompleted => "constructionCompleted",
            RunSpecialNpcDialogueBranchKey.TeleportAvailable => "teleportAvailable",
            RunSpecialNpcDialogueBranchKey.TeleportLocked => "teleportLocked",
            RunSpecialNpcDialogueBranchKey.TeleportUnavailable => "teleportUnavailable",
            _ => null
        };
        if (field != null && branch != EmptyBranch) branch.BindLocalization(this, field);
        return branch;
    }
}
