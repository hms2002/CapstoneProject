using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임 :
/// - 보스 말풍선 대사를 선택할 때 사용하는 gameplay 상황 키를 정의한다.
/// - UI 말풍선 구현과 보스 패턴 코드가 같은 상황 식별자를 공유하게 한다.
/// </summary>
public enum BossSpeechSituationEnum
{
    Attack,
    Hit,
    Death,
    Phase2Start,
    UltimateWarning,
    PlayerDetected,
    AbsorbStart,
    AbsorbAlcoholOnly,
    AbsorbFireAny,
    GroggyStart,
    AlcoholThrowPrepare,
    SlimeQueenCallSlimes,
    SlimeQueenCastlingRequest,
    SlimeQueenCastlingReply,
    SlimeQueenToxicRushPitFallSlam,
    SlimeQueenFinaleShort,
    SlimeQueenFinaleLong,
    DemonKingPierceCombo,
    DemonKingHeavySlash,
    DemonKingThrowEgoSword,
    DemonKingHomingMagic,
    DemonKingBombardment,
    DemonKingExplosionJump,
    DemonKingRecallEgoSword,
    DemonKingWallBounceRush,
    DemonKingGroggyRecoverCounter,
    DemonKingFinalDesperation,
    DemonKingEgoSwordVerticalStrike,
    DemonKingEgoSwordCrossLaser,
    EgoSwordThrowEgoSword,
    EgoSwordRecallEgoSword,
    EgoSwordVerticalStrike,
    EgoSwordCrossLaser,
    EgoSwordThrowEgoSwordRelease,
    DemonKingRecallEgoSwordRetort,
    EgoSwordRecallEgoSwordRetort,
    EgoSwordThrowEgoSwordPlant
}

/// <summary>
/// 책임 :
/// - 특정 보스 상황에서 무작위로 선택할 수 있는 대사 목록을 직렬화한다.
/// </summary>
[System.Serializable]
public class BossSpeechEntry
{
    public BossSpeechSituationEnum situation;
    [TextArea(1, 3)] public string[] lines;
}

/// <summary>
/// 책임 :
/// - 보스별 말풍선 테마와 상황별 대사 목록을 보관한다.
/// - gameplay 코드는 상황 키로 문장을 요청하고, UI 계층은 같은 데이터로 말풍선을 표시한다.
/// </summary>
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
