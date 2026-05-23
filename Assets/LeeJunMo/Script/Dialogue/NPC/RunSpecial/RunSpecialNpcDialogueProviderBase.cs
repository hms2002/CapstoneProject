using System.Collections.Generic;
using UnityEngine;

[System.Obsolete("Use RunSpecialNpcDialogueSetSO on RunSpecialNpcInteractor. This provider type is kept only for migration.")]
[AddComponentMenu("")]
public abstract class RunSpecialNpcDialogueProviderBase : MonoBehaviour
{
    public abstract RunSpecialNpcBranch BuildBranch(RunSpecialNpcFeatureContext context);

    public virtual string ResolveLineText(
        RunSpecialNpcLine line,
        string text,
        RunSpecialNpcFeatureContext context)
    {
        return text;
    }

    protected static RunSpecialNpcChoiceDefinition[] FilterAvailableChoices(
        RunSpecialNpcChoice[] choices,
        RunSpecialNpcFeatureContext context)
    {
        if (choices == null || choices.Length == 0)
            return System.Array.Empty<RunSpecialNpcChoiceDefinition>();

        List<RunSpecialNpcChoiceDefinition> filtered = new();
        for (int i = 0; i < choices.Length; i++)
        {
            RunSpecialNpcChoice choice = choices[i];
            if (choice != null && choice.ShouldShow(context))
                filtered.Add(RunSpecialNpcChoiceDefinition.FromLegacy(choice));
        }

        return filtered.Count > 0 ? filtered.ToArray() : System.Array.Empty<RunSpecialNpcChoiceDefinition>();
    }
}
