/// <summary>
/// 책임 :
/// - 대화창과 말풍선 텍스트 연출 속도/강조 방식을 식별한다.
/// - gameplay가 특정 대사 연출을 요청할 때 UI 구현 타입을 직접 참조하지 않게 하는 공유 계약이다.
/// </summary>
public enum DialogueAnimType
{
    Normal,
    Slow,
    Angry,
    Whisper,
    Cold
}
