using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class InputGlyphVisualUtility
{
    public static string ResolveLabel(InputGlyphPresentation glyph, string fallbackLabel = "")
    {
        return !string.IsNullOrWhiteSpace(glyph.DisplayLabel)
            ? glyph.DisplayLabel
            : fallbackLabel ?? string.Empty;
    }

    public static Sprite ResolveIcon(
        InputGlyphPresentation glyph,
        string fallbackLabel = null,
        Func<string, Sprite> fallbackSpriteResolver = null,
        Sprite fallbackIcon = null)
    {
        if (glyph.Icon != null)
            return glyph.Icon;

        if (fallbackSpriteResolver != null)
        {
            string glyphLabel = ResolveLabel(glyph);
            if (!string.IsNullOrWhiteSpace(glyphLabel))
            {
                Sprite glyphMappedSprite = fallbackSpriteResolver(glyphLabel);
                if (glyphMappedSprite != null)
                    return glyphMappedSprite;
            }

            if (!string.IsNullOrWhiteSpace(fallbackLabel))
            {
                Sprite fallbackMappedSprite = fallbackSpriteResolver(fallbackLabel);
                if (fallbackMappedSprite != null)
                    return fallbackMappedSprite;
            }
        }

        return fallbackIcon;
    }

    public static void Apply(TMP_Text label, Image icon, InputGlyphPresentation glyph, string fallbackLabel = "", Sprite fallbackIcon = null)
    {
        ApplyRaw(label, icon, ResolveLabel(glyph, fallbackLabel), ResolveIcon(glyph, fallbackIcon: fallbackIcon));
    }

    public static void ApplyRaw(TMP_Text label, Image icon, string text, Sprite sprite)
    {
        bool hasIcon = icon != null && sprite != null;

        if (icon != null)
        {
            icon.sprite = sprite;
            icon.enabled = hasIcon;
            icon.gameObject.SetActive(hasIcon);
        }

        if (label != null)
        {
            label.text = hasIcon ? string.Empty : text;
            label.gameObject.SetActive(!hasIcon);
        }
    }
}
