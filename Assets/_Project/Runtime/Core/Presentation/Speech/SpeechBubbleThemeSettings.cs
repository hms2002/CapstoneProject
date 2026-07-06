using UnityEngine;

/// <summary>
/// 책임 :
/// - 말풍선의 테두리, 배경, 글자 색상 override 설정을 직렬화한다.
/// - speech data와 UI 말풍선 구현 사이에서 공유되는 순수 표시 설정이다.
/// </summary>
[System.Serializable]
public sealed class SpeechBubbleThemeSettings
{
    [SerializeField] private bool useCustomColors;
    [SerializeField] private Color borderColor = Color.black;
    [SerializeField] private Color fillColor = new Color(1f, 1f, 1f, 0.52f);
    [SerializeField] private Color fontColor = Color.black;

    public bool UseCustomColors => useCustomColors;
    public Color BorderColor => borderColor;
    public Color FillColor => fillColor;
    public Color FontColor => fontColor;
}
