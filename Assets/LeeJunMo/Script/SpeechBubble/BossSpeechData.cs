using System.Collections.Generic;
using UnityEngine;

// 보스 전용 상황 Enum
public enum BossSpeechSituationEnum
{
    Attack,
    Hit,
    Death,
    Phase2Start,
    UltimateWarning,
    PlayerDetected
}

[System.Serializable]
public class BossSpeechEntry
{
    public BossSpeechSituationEnum situation;
    [TextArea(1, 3)] public string[] lines;
}

[CreateAssetMenu(fileName = "NewBossSpeechData", menuName = "GameData/Boss Speech Data")]
public class BossSpeechData : ScriptableObject
{
    [Header("Bubble Theme")]
    public SpeechBubbleThemeSettings bubbleTheme = new SpeechBubbleThemeSettings();

    [Header("Speech Lines")]
    public List<BossSpeechEntry> entries = new List<BossSpeechEntry>();

    public SpeechBubbleThemeSettings BubbleTheme => bubbleTheme;

    public string GetLine(BossSpeechSituationEnum situation)
    {
        var entry = entries.Find(x => x.situation == situation);
        if (entry != null && entry.lines != null && entry.lines.Length > 0)
        {
            return entry.lines[Random.Range(0, entry.lines.Length)];
        }
        return "";
    }
}
