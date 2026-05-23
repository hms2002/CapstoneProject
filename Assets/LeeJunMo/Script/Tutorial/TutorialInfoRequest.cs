using UnityEngine;

[System.Serializable]
public struct TutorialInfoRequest
{
    public string tutorialId;
    public string title;
    [TextArea(2, 8)] public string body;
    public Sprite contentSprite;
    public Sprite windowSprite;
    public Sprite titleSprite;
    public float holdSeconds;
    public bool usePersistentCompletion;
    public bool markCompletedOnClose;
    public bool allowReplayWhenCompleted;
}
