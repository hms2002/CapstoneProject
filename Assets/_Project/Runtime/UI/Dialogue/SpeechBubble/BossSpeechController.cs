using System;
using UnityEngine;

/// <summary>
/// 책임 :
/// - BossSpeechData 상황 키를 실제 말풍선 UI 출력으로 변환한다.
/// - gameplay 계층에는 IBossSpeechPlayback 계약만 노출하고, SpeechBubbleComponent 구체 호출은 UI 계층 안에 가둔다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BossSpeechController : MonoBehaviour, IBossSpeechPlayback
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
        if (!TryGetLine(situation, out string line))
            return false;

        speechBubble.Speak(line, duration, speechData.BubbleTheme, onBubbleHidden);
        return true;
    }

    public bool TrySpeakSituationParallelAt(
        BossSpeechSituationEnum situation,
        float duration,
        Transform anchor,
        Vector3 offsetDelta)
    {
        return TrySpeakSituationParallelAt(situation, duration, null, anchor, offsetDelta);
    }

    public bool TrySpeakSituationParallelAt(
        BossSpeechSituationEnum situation,
        float duration,
        Action onBubbleHidden,
        Transform anchor,
        Vector3 offsetDelta)
    {
        if (anchor == null || !TryGetLine(situation, out string line))
            return false;

        speechBubble.SpeakParallelAt(line, duration, speechData.BubbleTheme, onBubbleHidden, anchor, offsetDelta);
        return true;
    }

    public bool TrySpeakSituationParallelAt(
        BossSpeechSituationEnum situation,
        float duration,
        Func<Vector3> anchorPositionResolver,
        Vector3 offsetDelta)
    {
        return TrySpeakSituationParallelAt(situation, duration, null, anchorPositionResolver, offsetDelta);
    }

    public bool TrySpeakSituationParallelAt(
        BossSpeechSituationEnum situation,
        float duration,
        Action onBubbleHidden,
        Func<Vector3> anchorPositionResolver,
        Vector3 offsetDelta)
    {
        if (anchorPositionResolver == null || !TryGetLine(situation, out string line))
            return false;

        speechBubble.SpeakParallelAt(line, duration, speechData.BubbleTheme, onBubbleHidden, anchorPositionResolver, offsetDelta);
        return true;
    }

    public bool TrySpeakSituationParallelAt(
        BossSpeechSituationEnum situation,
        float duration,
        Func<Vector3> anchorPositionResolver,
        Func<Quaternion> anchorRotationResolver,
        Vector3 offsetDelta)
    {
        return TrySpeakSituationParallelAt(situation, duration, null, anchorPositionResolver, anchorRotationResolver, offsetDelta);
    }

    public bool TrySpeakSituationParallelAt(
        BossSpeechSituationEnum situation,
        float duration,
        Action onBubbleHidden,
        Func<Vector3> anchorPositionResolver,
        Func<Quaternion> anchorRotationResolver,
        Vector3 offsetDelta)
    {
        if (anchorPositionResolver == null || anchorRotationResolver == null || !TryGetLine(situation, out string line))
            return false;

        speechBubble.SpeakParallelAt(
            line,
            duration,
            speechData.BubbleTheme,
            onBubbleHidden,
            anchorPositionResolver,
            anchorRotationResolver,
            offsetDelta);
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
        if (!TryGetLine(situation, out string line, lineFormatter))
            return false;

        speechBubble.SpeakAnimated(line, duration, speechData.BubbleTheme, onBubbleHidden, animType);
        return true;
    }

    private bool TryGetLine(
        BossSpeechSituationEnum situation,
        out string line,
        Func<string, string> lineFormatter = null)
    {
        ResolveSpeechBubble();

        if (speechData == null || speechBubble == null)
        {
            Debug.LogWarning("[BossSpeechController] Missing BossSpeechData or SpeechBubbleComponent.", this);
            line = string.Empty;
            return false;
        }

        line = speechData.GetLine(situation);
        if (lineFormatter != null)
            line = lineFormatter(line);

        return !string.IsNullOrWhiteSpace(line);
    }

    private void ResolveSpeechBubble()
    {
        if (speechBubble == null)
            speechBubble = GetComponentInChildren<SpeechBubbleComponent>(includeInactive: true);
    }
}
