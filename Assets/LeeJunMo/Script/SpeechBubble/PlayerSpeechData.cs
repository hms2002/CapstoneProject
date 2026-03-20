using System.Collections.Generic;
using UnityEngine;

// 플레이어 전용 상황 Enum
public enum PlayerSpeechSituationEnum
{
    Attack,
    Hit,
    Death,
    DoorLocked,
    InventoryFull,
    NotEnoughMana
}

[System.Serializable]
public class PlayerSpeechEntry
{
    public PlayerSpeechSituationEnum situation;
    [TextArea(1, 3)] public string[] lines;
}

[CreateAssetMenu(fileName = "NewPlayerSpeechData", menuName = "GameData/Player Speech Data")]
public class PlayerSpeechData : ScriptableObject
{
    public List<PlayerSpeechEntry> entries = new List<PlayerSpeechEntry>();

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