using System;

public sealed class RunSpecialNpcBranch
{
    public static readonly RunSpecialNpcBranch Empty = new(
        Array.Empty<RunSpecialNpcLine>(),
        Array.Empty<RunSpecialNpcChoiceDefinition>(),
        endAfterLines: true);

    private RunSpecialNpcBranch(
        RunSpecialNpcLine[] lines,
        RunSpecialNpcChoiceDefinition[] choices,
        bool endAfterLines)
    {
        Lines = lines ?? Array.Empty<RunSpecialNpcLine>();
        Choices = choices ?? Array.Empty<RunSpecialNpcChoiceDefinition>();
        EndAfterLines = endAfterLines;
    }

    public RunSpecialNpcLine[] Lines { get; }
    public RunSpecialNpcChoiceDefinition[] Choices { get; }
    public bool EndAfterLines { get; }

    public static RunSpecialNpcBranch LinesOnly(RunSpecialNpcLine[] lines)
    {
        return new RunSpecialNpcBranch(lines, Array.Empty<RunSpecialNpcChoiceDefinition>(), endAfterLines: true);
    }

    public static RunSpecialNpcBranch WithChoices(
        RunSpecialNpcLine[] lines,
        RunSpecialNpcChoiceDefinition[] choices)
    {
        return new RunSpecialNpcBranch(lines, choices, endAfterLines: false);
    }

    public static RunSpecialNpcBranch FromDefinition(RunSpecialNpcDialogueBranchDefinition definition)
    {
        if (definition == null)
            return Empty;

        return definition.Choices == null || definition.Choices.Length == 0
            ? LinesOnly(definition.Lines)
            : WithChoices(definition.Lines, definition.Choices);
    }
}
