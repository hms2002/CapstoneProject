using System;
using UnityEngine;

public enum RunSpecialNpcFeatureKind
{
    Construction = 0,
    SameSceneTeleport = 1,
}

public enum RunSpecialNpcDialogueBranchKey
{
    ConstructionNotStarted = 0,
    ConstructionPending = 1,
    ConstructionCompleted = 2,
    ConstructionInsufficientFunds = 3,
    TeleportAvailable = 10,
    TeleportLocked = 11,
    TeleportUnavailable = 12,
}

public enum RunSpecialNpcChoiceAction
{
    None = 0,
    ExecutePrimaryFeature = 1,
}

[Serializable]
public sealed class RunSpecialNpcLine
{
    [NonSerialized] private ScriptableObject localizationOwner;
    [NonSerialized] private string localizationPath;
    internal void BindLocalization(ScriptableObject owner, string path) { localizationOwner = owner; localizationPath = path; }
    [SerializeField, TextArea] private string text;
    [SerializeField, Min(0.05f)] private float duration = 2.5f;
    [SerializeField] private SpeechBubbleThemeSettings theme;

    public RunSpecialNpcLine()
    {
    }

    public RunSpecialNpcLine(
        string text,
        float duration = 2.5f,
        SpeechBubbleThemeSettings theme = null)
    {
        this.text = text;
        this.duration = Mathf.Max(0.05f, duration);
        this.theme = theme ?? new SpeechBubbleThemeSettings();
    }

    public string Text => GameText.Asset(localizationOwner, localizationPath + ".text", text);
    public float Duration => duration;
    public SpeechBubbleThemeSettings Theme => theme;
}

[Serializable]
public sealed class RunSpecialNpcChoiceDefinition
{
    [NonSerialized] private ScriptableObject localizationOwner;
    [NonSerialized] private string localizationPath;
    internal void BindLocalization(ScriptableObject owner, string path)
    {
        localizationOwner = owner;
        localizationPath = path;
        BindLines(responseLines, owner, path + ".responseLines");
        BindLines(unavailableResponseLines, owner, path + ".unavailableResponseLines");
    }
    private static void BindLines(RunSpecialNpcLine[] lines, ScriptableObject owner, string path)
    {
        if (lines == null) return;
        for (int i = 0; i < lines.Length; i++) lines[i]?.BindLocalization(owner, $"{path}.Array.data[{i}]");
    }
    [SerializeField, TextArea] private string label = "Choice";
    [SerializeField] private bool hideWhenActionUnavailable;
    [SerializeField] private RunSpecialNpcChoiceAction action;
    [SerializeField] private RunSpecialNpcLine[] responseLines = Array.Empty<RunSpecialNpcLine>();
    [SerializeField] private RunSpecialNpcLine[] unavailableResponseLines = Array.Empty<RunSpecialNpcLine>();

    public RunSpecialNpcChoiceDefinition()
    {
    }

    private RunSpecialNpcChoiceDefinition(
        string label,
        bool hideWhenActionUnavailable,
        RunSpecialNpcChoiceAction action,
        RunSpecialNpcLine[] responseLines,
        RunSpecialNpcLine[] unavailableResponseLines)
    {
        this.label = label;
        this.hideWhenActionUnavailable = hideWhenActionUnavailable;
        this.action = action;
        this.responseLines = responseLines ?? Array.Empty<RunSpecialNpcLine>();
        this.unavailableResponseLines = unavailableResponseLines ?? Array.Empty<RunSpecialNpcLine>();
    }

    public string Label => GameText.Asset(localizationOwner, localizationPath + ".label", label);
    public bool HideWhenActionUnavailable => hideWhenActionUnavailable;
    public RunSpecialNpcChoiceAction Action => action;
    public RunSpecialNpcLine[] ResponseLines => responseLines;
    public RunSpecialNpcLine[] UnavailableResponseLines => unavailableResponseLines;

    public static RunSpecialNpcChoiceDefinition FromLegacy(RunSpecialNpcChoice choice)
    {
        if (choice == null)
            return null;

        RunSpecialNpcChoiceAction legacyAction = choice.Feature != null
            ? RunSpecialNpcChoiceAction.ExecutePrimaryFeature
            : RunSpecialNpcChoiceAction.None;

        return new RunSpecialNpcChoiceDefinition(
            choice.Label,
            choice.HideWhenFeatureUnavailable,
            legacyAction,
            choice.ResponseLines,
            Array.Empty<RunSpecialNpcLine>());
    }

    public bool ShouldShow(RunSpecialNpcFeatureBase primaryFeature, RunSpecialNpcFeatureContext context)
    {
        return !hideWhenActionUnavailable ||
               action == RunSpecialNpcChoiceAction.None ||
               primaryFeature != null &&
               (primaryFeature.CanExecute(context) ||
                HasAnyLine(unavailableResponseLines) &&
                primaryFeature.ShouldShowUnavailableChoice(action, context));
    }

    private static bool HasAnyLine(RunSpecialNpcLine[] lines)
    {
        if (lines == null)
            return false;

        for (int i = 0; i < lines.Length; i++)
        {
            RunSpecialNpcLine line = lines[i];
            if (line != null && !string.IsNullOrWhiteSpace(line.Text))
                return true;
        }

        return false;
    }
}

[Serializable]
public sealed class RunSpecialNpcDialogueBranchDefinition
{
    internal void BindLocalization(ScriptableObject owner, string path)
    {
        if (lines != null)
            for (int i = 0; i < lines.Length; i++) lines[i]?.BindLocalization(owner, $"{path}.lines.Array.data[{i}]");
        if (choices != null)
            for (int i = 0; i < choices.Length; i++) choices[i]?.BindLocalization(owner, $"{path}.choices.Array.data[{i}]");
    }
    [SerializeField] private RunSpecialNpcLine[] lines = Array.Empty<RunSpecialNpcLine>();
    [SerializeField] private RunSpecialNpcChoiceDefinition[] choices = Array.Empty<RunSpecialNpcChoiceDefinition>();

    public RunSpecialNpcDialogueBranchDefinition()
    {
    }

    private RunSpecialNpcDialogueBranchDefinition(
        RunSpecialNpcLine[] lines,
        RunSpecialNpcChoiceDefinition[] choices)
    {
        this.lines = lines ?? Array.Empty<RunSpecialNpcLine>();
        this.choices = choices ?? Array.Empty<RunSpecialNpcChoiceDefinition>();
    }

    public RunSpecialNpcLine[] Lines => lines;
    public RunSpecialNpcChoiceDefinition[] Choices => choices;

    public static RunSpecialNpcDialogueBranchDefinition LinesOnly(params RunSpecialNpcLine[] lines)
    {
        return new RunSpecialNpcDialogueBranchDefinition(
            lines,
            Array.Empty<RunSpecialNpcChoiceDefinition>());
    }
}

[Serializable]
public sealed class RunSpecialNpcChoice
{
    [SerializeField, TextArea] private string label = "Choice";
    [SerializeField] private bool hideWhenFeatureUnavailable;
    [SerializeField] private RunSpecialNpcLine[] responseLines = Array.Empty<RunSpecialNpcLine>();
    [SerializeField] private RunSpecialNpcFeatureBase feature;

    public string Label => label;
    public RunSpecialNpcLine[] ResponseLines => responseLines;
    public RunSpecialNpcFeatureBase Feature => feature;
    public bool HideWhenFeatureUnavailable => hideWhenFeatureUnavailable;

    public bool ShouldShow(RunSpecialNpcFeatureContext context)
    {
        return !hideWhenFeatureUnavailable ||
               feature == null ||
               feature.CanExecute(context);
    }
}
