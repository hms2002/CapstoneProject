using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerSpeechController : MonoBehaviour
{
    [SerializeField] private SpeechBubbleComponent speechBubble;
    [SerializeField] private PlayerSpeechData speechData;

    private void Awake()
    {
        ResolveSpeechBubble();
    }

    public void SetSpeechDependencies(SpeechBubbleComponent bubble, PlayerSpeechData data)
    {
        if (bubble != null)
            speechBubble = bubble;

        if (data != null)
            speechData = data;
    }

    public void SpeakSituation(PlayerSpeechSituationEnum situation, float duration = 2f)
    {
        ResolveSpeechBubble();

        if (speechData == null || speechBubble == null)
        {
            Debug.LogWarning("[PlayerSpeechController] Missing SpeechData or SpeechBubbleComponent.", this);
            return;
        }

        string line = speechData.GetLine(situation);
        if (!string.IsNullOrEmpty(line))
            speechBubble.Speak(line, duration);
    }

    private void ResolveSpeechBubble()
    {
        if (speechBubble == null)
            speechBubble = GetComponentInChildren<SpeechBubbleComponent>(includeInactive: true);
    }
}
