/// <summary>
/// 책임 :
/// - 아이템/능력 데이터가 상세 패널에 추가 설명 블록을 제공할 수 있는 계약을 정의한다.
/// - UI는 반환된 텍스트 블록만 렌더링하고, 계산 규칙은 gameplay 데이터가 소유하게 한다.
/// </summary>
public interface IDetailProvider
{
    ItemDetailBlock BuildDetailBlock(ItemDetailContext ctx);
}

/// <summary>
/// 책임 :
/// - 상세 패널에 추가로 표시할 제목/본문 텍스트 블록을 담는다.
/// - rich text나 용어 링크 텍스트를 UI 구현과 분리된 값 객체로 전달한다.
/// </summary>
public struct ItemDetailBlock
{
    public string title;     // "일반공격", "스킬1" 등
    public string body;      // TMP 리치텍스트/[[용어]] 포함 가능
}
