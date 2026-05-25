using UnityEngine;
using UnityEngine.Serialization;

[System.Serializable]
public struct TutorialInfoPage
{
    public string title;
    [TextArea(2, 8)] public string body;
    public Sprite contentSprite;
}

[System.Serializable]
public struct TutorialInfoRequest
{
    public string tutorialId;
    [FormerlySerializedAs("windowSprite")] public Sprite tutorialPanelSprite;
    public Sprite titleSprite;
    public TutorialInfoPage[] pages;
    public float holdSeconds;
    public bool usePersistentCompletion;
    public bool markCompletedOnClose;
    public bool allowReplayWhenCompleted;
}
