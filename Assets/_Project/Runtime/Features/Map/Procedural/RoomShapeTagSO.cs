using UnityEngine;

/// <summary>
/// 책임:
/// - 기획자가 방 템플릿의 시각적/체감 모양을 직접 묶어 반복 배치 회피 기준으로 사용하게 한다.
/// - 소켓만으로 판별하기 어려운 ㄴ자/일자/특수 구조의 의도상 동일성을 데이터로 표현한다.
/// </summary>
[CreateAssetMenu(fileName = "RoomShapeTag", menuName = "Gameplay/Dungeon/Room Shape Tag")]
public sealed class RoomShapeTagSO : ScriptableObject
{
    [SerializeField, TextArea] private string description;

    public string Description => description;
}
