/// <summary>
/// 책임 :
/// - 플레이어 스탯 패널의 단일 값 표시 형식을 정의한다.
/// - 정수, 소수, 퍼센트 등 UI 문자열 포맷 규칙을 데이터로 선택하게 한다.
/// </summary>
public enum PlayerStatDisplayFormat
{
    WholeNumber = 0,
    Decimal = 1,
    Percent = 2,
}
