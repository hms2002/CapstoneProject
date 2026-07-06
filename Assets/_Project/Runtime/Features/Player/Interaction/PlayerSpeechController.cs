using UnityEngine;

/// <summary>
/// 책임 :
/// - PlayerSpeechData 상황 키를 플레이어 말풍선 출력으로 변환한다.
/// - 실제 말풍선 UI 구현은 ISpeechBubblePlayback 계약으로만 접근한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerSpeechController : MonoBehaviour
{
    [SerializeField] private MonoBehaviour speechBubble;
    [SerializeField] private PlayerSpeechData speechData;

    private void Awake()
    {
        ResolveSpeechBubble();
    }

    public void SetSpeechDependencies(MonoBehaviour bubble, PlayerSpeechData data)
    {
        if (bubble is ISpeechBubblePlayback)
            speechBubble = bubble;

        if (data != null)
            speechData = data;
    }

    public void SpeakSituation(PlayerSpeechSituationEnum situation, float duration = 2f)
    {
        ResolveSpeechBubble();

        ISpeechBubblePlayback bubblePlayback = speechBubble as ISpeechBubblePlayback;
        if (speechData == null || bubblePlayback == null)
        {
            Debug.LogWarning("[PlayerSpeechController] Missing SpeechData or speech bubble playback.", this);
            return;
        }

        string line = speechData.GetLine(situation);
        if (!string.IsNullOrEmpty(line))
            bubblePlayback.Speak(line, duration, speechData.BubbleTheme);
    }

    private void ResolveSpeechBubble()
    {
        if (speechBubble is ISpeechBubblePlayback)
            return;

        speechBubble = ResolveSpeechBubbleBehaviour();
    }

    private MonoBehaviour ResolveSpeechBubbleBehaviour()
    {
        MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(includeInactive: true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour is ISpeechBubblePlayback)
                return behaviour;
        }

        return null;
    }
}
