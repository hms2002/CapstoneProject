using UnityEngine;

/// <summary>
/// 책임 :
/// - 아이템 툴팁 본문이 사용할 의미 기반 텍스트 색상을 한 곳에 정의한다.
/// - glossary, 긍정/부정, 강조, 수치 텍스트의 공통 팔레트를 제공한다.
/// </summary>
[CreateAssetMenu(menuName = "Game/UI/Tooltip Color Palette", fileName = "TooltipColorPalette")]
public class TooltipColorPalette : ScriptableObject
{
    [SerializeField] private string glossaryColorHex = "5EC8FF";
    [SerializeField] private string positiveColorHex = "66FF66";
    [SerializeField] private string negativeColorHex = "FF5050";
    [SerializeField] private string emphasisColorHex = "FF3296";
    [SerializeField] private string valueColorHex = "FFBE00";

    public string GlossaryColorHex => glossaryColorHex;
    public string PositiveColorHex => positiveColorHex;
    public string NegativeColorHex => negativeColorHex;
    public string EmphasisColorHex => emphasisColorHex;
    public string ValueColorHex => valueColorHex;
}
