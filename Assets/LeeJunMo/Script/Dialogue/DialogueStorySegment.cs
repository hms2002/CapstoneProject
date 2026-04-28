using UnityEngine;

public struct DialogueStorySegment
{
    public TextAsset InkJSON { get; }
    public string StartPath { get; }

    public DialogueStorySegment(TextAsset inkJSON, string startPath = null)
    {
        InkJSON = inkJSON;
        StartPath = startPath;
    }

    public bool IsValid => InkJSON != null;
}
