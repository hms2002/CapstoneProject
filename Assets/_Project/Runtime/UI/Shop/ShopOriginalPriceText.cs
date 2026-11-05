using TMPro;
using UnityEngine;

/// <summary>Centers the original-price strike on the rendered digits of the current font.</summary>
public sealed class ShopOriginalPriceText : TextMeshPro
{
    protected override void DrawUnderlineMesh(Vector3 start, Vector3 end, ref int index,
        float startScale, float endScale, float maxScale, float sdfScale, Color32 underlineColor)
    {
        var digit = textInfo.characterInfo[0];
        if ((digit.style & FontStyles.Strikethrough) != 0)
        {
            float center = (digit.bottomLeft.y + digit.topLeft.y) * 0.5f;
            // TMP draws the stroke downward from the supplied edge.
            start.y = end.y = center + font.faceInfo.underlineThickness * maxScale * 0.5f;
        }
        base.DrawUnderlineMesh(start, end, ref index, startScale, endScale, maxScale, sdfScale, underlineColor);
    }
}
