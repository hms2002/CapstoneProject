using UnityEngine;

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
