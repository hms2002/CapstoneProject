public interface IDetailProvider
{
    ItemDetailBlock BuildDetailBlock(ItemDetailContext ctx);
}

public struct ItemDetailBlock
{
    public string title;     // "일반공격", "스킬1" 등
    public string body;      // TMP 리치텍스트/[[용어]] 포함 가능
}
