using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임 :
/// - 플레이어 말풍선 대사를 선택할 때 사용하는 gameplay 상황 키를 정의한다.
/// - 상호작용/획득/문 잠김 같은 플레이어 피드백 코드를 UI 구현에서 분리한다.
/// </summary>
public enum PlayerSpeechSituationEnum
{
    Attack,
    Hit,
    Death,
    DoorLocked,
    InventoryFull,
    NotEnoughMana,
    OneWayDoorLocked
}

/// <summary>
/// 책임 :
/// - 특정 플레이어 상황에서 무작위로 선택할 수 있는 대사 목록을 직렬화한다.
/// </summary>
[System.Serializable]
public class PlayerSpeechEntry
{
    public PlayerSpeechSituationEnum situation;
    [TextArea(1, 3)] public string[] lines;
}

/// <summary>
/// 책임 :
/// - 플레이어 말풍선 테마와 상황별 대사 목록을 보관한다.
/// - gameplay 코드는 상황 키로 문장을 요청하고, UI 계층은 같은 데이터로 말풍선을 표시한다.
/// </summary>
[CreateAssetMenu(fileName = "NewPlayerSpeechData", menuName = "GameData/Player Speech Data")]
public class PlayerSpeechData : ScriptableObject
{
    [Header("Bubble Theme")]
    public SpeechBubbleThemeSettings bubbleTheme = new SpeechBubbleThemeSettings();

    [Header("Speech Lines")]
    public List<PlayerSpeechEntry> entries = new List<PlayerSpeechEntry>();

    public SpeechBubbleThemeSettings BubbleTheme => bubbleTheme;

    public string GetLine(PlayerSpeechSituationEnum situation)
    {
        var entry = entries.Find(x => x.situation == situation);
        if (entry != null && entry.lines != null && entry.lines.Length > 0)
        {
            return entry.lines[Random.Range(0, entry.lines.Length)];
        }
        return "";
    }
}
