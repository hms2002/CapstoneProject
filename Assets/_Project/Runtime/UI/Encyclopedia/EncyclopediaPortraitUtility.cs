using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Portrait selection and the authored three-dot window shared by creature views.</summary>
public static class EncyclopediaPortraitUtility
{
    public static int GetDefaultIndex(IReadOnlyList<EncyclopediaPortrait> portraits)
    {
        if (portraits == null || portraits.Count == 0)
            return -1;

        for (int i = 0; i < portraits.Count; i++)
        {
            EncyclopediaPortrait portrait = portraits[i];
            if (portrait?.sprite != null &&
                (string.Equals(portrait.label, "Idle", StringComparison.OrdinalIgnoreCase) ||
                 (portrait.label != null && portrait.label.EndsWith("_Idle", StringComparison.OrdinalIgnoreCase))))
                return i;
        }

        for (int i = 0; i < portraits.Count; i++)
            if (portraits[i]?.sprite != null && string.Equals(portraits[i].label, "Normal", StringComparison.OrdinalIgnoreCase))
                return i;

        for (int i = 0; i < portraits.Count; i++)
            if (portraits[i]?.sprite != null)
                return i;

        return -1;
    }

    public static int GetDotCount(int portraitCount) => Math.Min(3, Math.Max(0, portraitCount));

    public static int GetDotPortraitIndex(int portraitCount, int selectedIndex, int dotIndex)
    {
        int count = GetDotCount(portraitCount);
        if (dotIndex < 0 || dotIndex >= count)
            return -1;

        int selected = Math.Max(0, Math.Min(selectedIndex, portraitCount - 1));
        int start = Math.Max(0, Math.Min(selected - 1, portraitCount - count));
        return start + dotIndex;
    }

    public static int GetDotOffset(int portraitCount, int selectedIndex, int dotIndex)
    {
        if (GetDotPortraitIndex(portraitCount, selectedIndex, dotIndex) < 0)
            return 0;
        if (portraitCount == 1)
            return 0;
        if (portraitCount == 2)
            return dotIndex + (selectedIndex <= 0 ? -1 : 0);
        return dotIndex - 1;
    }

    public static void SetSprite(UnityEngine.UI.Image image, Sprite sprite)
    {
        if (image == null)
            return;

        image.sprite = sprite;
        image.enabled = sprite != null;
        if (sprite == null)
            return;

        UnityEngine.UI.AspectRatioFitter fitter = image.GetComponent<UnityEngine.UI.AspectRatioFitter>();
        if (fitter != null && sprite.rect.height > 0f)
            fitter.aspectRatio = sprite.rect.width / sprite.rect.height;
    }
}
