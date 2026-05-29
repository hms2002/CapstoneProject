using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BossSpeechController : MonoBehaviour
{
    [SerializeField] private SpeechBubbleComponent speechBubble;
    [SerializeField] private BossSpeechData speechData;

    private void Awake()
    {
        ResolveSpeechBubble();
    }

    public void SetSpeechDependencies(SpeechBubbleComponent bubble, BossSpeechData data)
    {
        if (bubble != null)
            speechBubble = bubble;

        if (data != null)
            speechData = data;
    }

    public bool TrySpeakSituation(BossSpeechSituationEnum situation, float duration = 2f)
    {
        return TrySpeakSituation(situation, duration, null);
    }

    public bool TrySpeakSituation(BossSpeechSituationEnum situation, float duration, Action onBubbleHidden)
    {
        ResolveSpeechBubble();

        if (speechData == null || speechBubble == null)
        {
            Debug.LogWarning("[BossSpeechController] Missing BossSpeechData or SpeechBubbleComponent.", this);
            return false;
        }

        string line = speechData.GetLine(situation);
        if (string.IsNullOrWhiteSpace(line))
            return false;

        speechBubble.Speak(line, duration, speechData.BubbleTheme, onBubbleHidden);
        return true;
    }

    public bool TrySpeakSituationAnimated(
        BossSpeechSituationEnum situation,
        float duration,
        DialogueAnimType animType,
        Action onBubbleHidden)
    {
        return TrySpeakSituationAnimated(situation, duration, animType, onBubbleHidden, null);
    }

    public bool TrySpeakSituationAnimated(
        BossSpeechSituationEnum situation,
        float duration,
        DialogueAnimType animType,
        Action onBubbleHidden,
        Func<string, string> lineFormatter)
    {
        ResolveSpeechBubble();

        if (speechData == null || speechBubble == null)
        {
            Debug.LogWarning("[BossSpeechController] Missing BossSpeechData or SpeechBubbleComponent.", this);
            return false;
        }

        string line = speechData.GetLine(situation);
        if (string.IsNullOrWhiteSpace(line))
            return false;

        if (lineFormatter != null)
            line = lineFormatter(line);

        if (string.IsNullOrWhiteSpace(line))
            return false;

        speechBubble.SpeakAnimated(line, duration, speechData.BubbleTheme, onBubbleHidden, animType);
        return true;
    }

    private void ResolveSpeechBubble()
    {
        if (speechBubble == null)
            speechBubble = GetComponentInChildren<SpeechBubbleComponent>(includeInactive: true);
    }
}
