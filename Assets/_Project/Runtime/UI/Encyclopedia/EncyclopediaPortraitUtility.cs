using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Portrait selection and the authored five-dot window shared by creature views.</summary>
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

    public static int GetDotCount(int portraitCount) => Math.Min(5, Math.Max(0, portraitCount));

    public static int GetDotPortraitIndex(int portraitCount, int selectedIndex, int dotIndex)
    {
        int count = GetDotCount(portraitCount);
        if (dotIndex < 0 || dotIndex >= count)
            return -1;

        int selected = Math.Max(0, Math.Min(selectedIndex, portraitCount - 1));
        int start = Math.Max(0, Math.Min(selected - count / 2, portraitCount - count));
        return start + dotIndex;
    }

    public static float GetDotOffset(int portraitCount, int selectedIndex, int dotIndex)
    {
        if (GetDotPortraitIndex(portraitCount, selectedIndex, dotIndex) < 0)
            return 0;
        return dotIndex - (GetDotCount(portraitCount) - 1) * .5f;
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
