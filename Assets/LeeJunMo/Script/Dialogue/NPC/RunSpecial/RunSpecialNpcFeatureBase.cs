using System.Collections;
using UnityEngine;

public sealed class RunSpecialNpcFeatureContext
{
    public RunSpecialNpcFeatureContext(RunSpecialNpcInteractor owner, IPlayerInteractor player)
    {
        Owner = owner;
        Player = player;
    }

    public RunSpecialNpcInteractor Owner { get; }
    public IPlayerInteractor Player { get; }
}

public abstract class RunSpecialNpcFeatureBase : MonoBehaviour
{
    public abstract RunSpecialNpcFeatureKind DialogueFeatureKind { get; }

    public virtual bool ExecuteAfterRunSpecialPresentationClose => false;

    public abstract RunSpecialNpcDialogueBranchKey GetDialogueBranchKey(RunSpecialNpcFeatureContext context);

    public virtual bool CanExecute(RunSpecialNpcFeatureContext context)
    {
        return context != null;
    }

    public virtual bool ShouldShowUnavailableChoice(
        RunSpecialNpcChoiceAction action,
        RunSpecialNpcFeatureContext context)
    {
        return false;
    }

    public virtual string GetUnavailableReason(RunSpecialNpcFeatureContext context)
    {
        return string.Empty;
    }

    public virtual string ResolveDialogueLineText(string text, RunSpecialNpcFeatureContext context)
    {
        return text;
    }

    public abstract IEnumerator Execute(RunSpecialNpcFeatureContext context);
}
