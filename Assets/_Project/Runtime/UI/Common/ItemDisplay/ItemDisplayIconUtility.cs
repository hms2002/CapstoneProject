using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 책임 : 아이템 아이콘 UI Image의 기본 RectTransform/보존 비율 상태를 캡처하고 복원한다.
/// </summary>
public readonly struct ItemDisplayIconDefaultState
{
    private readonly bool hasValue;
    private readonly Vector2 anchorMin;
    private readonly Vector2 anchorMax;
    private readonly Vector2 anchoredPosition;
    private readonly Vector2 sizeDelta;
    private readonly Vector2 pivot;
    private readonly Quaternion localRotation;
    private readonly Vector3 localScale;
    private readonly bool preserveAspect;

    private ItemDisplayIconDefaultState(
        bool hasValue,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        Vector2 pivot,
        Quaternion localRotation,
        Vector3 localScale,
        bool preserveAspect)
    {
        this.hasValue = hasValue;
        this.anchorMin = anchorMin;
        this.anchorMax = anchorMax;
        this.anchoredPosition = anchoredPosition;
        this.sizeDelta = sizeDelta;
        this.pivot = pivot;
        this.localRotation = localRotation;
        this.localScale = localScale;
        this.preserveAspect = preserveAspect;
    }

    public ItemDisplayIconDefaultState(Image image)
    {
        RectTransform rect = image != null ? image.rectTransform : null;
        hasValue = rect != null;
        anchorMin = rect != null ? rect.anchorMin : Vector2.zero;
        anchorMax = rect != null ? rect.anchorMax : Vector2.zero;
        anchoredPosition = rect != null ? rect.anchoredPosition : Vector2.zero;
        sizeDelta = rect != null ? rect.sizeDelta : Vector2.zero;
        pivot = rect != null ? rect.pivot : new Vector2(0.5f, 0.5f);
        localRotation = rect != null ? rect.localRotation : Quaternion.identity;
        localScale = rect != null ? rect.localScale : Vector3.one;
        preserveAspect = image != null && image.preserveAspect;
    }

    public static ItemDisplayIconDefaultState Stretch(Image image, bool preserveAspect = false)
    {
        return new ItemDisplayIconDefaultState(
            image != null && image.rectTransform != null,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero,
            new Vector2(0.5f, 0.5f),
            Quaternion.identity,
            Vector3.one,
            preserveAspect);
    }

    public void ApplyTo(Image image)
    {
        if (image == null || !hasValue)
            return;

        RectTransform rect = image.rectTransform;
        if (rect != null)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            rect.pivot = pivot;
            rect.localRotation = localRotation;
            rect.localScale = localScale;
        }

        image.preserveAspect = preserveAspect;
    }

    public Vector2 AnchoredPosition => anchoredPosition;
}

/// <summary>
/// 책임 : 인벤토리와 도감 UI에서 아이템 정의와 표시 프로필을 Image 아이콘 상태로 투영한다.
/// </summary>
public static class ItemDisplayIconUtility
{
    public static void Apply(
        Image image,
        ScriptableObject item,
        ItemDisplayIconContext context,
        ItemDisplayIconDefaultState defaultState,
        bool applyAnchoredPosition = true,
        bool applyCustomTransform = true)
    {
        if (item == null)
        {
            Clear(image, defaultState);
            return;
        }

        IInventoryItemDefinition definition = item.AsDef();
        if (definition == null || definition.Icon == null)
        {
            Clear(image, defaultState);
            return;
        }

        ItemDisplayIconSettings settings = ResolveSettings(item, context);
        if (settings == null)
        {
            ApplyRaw(image, definition.Icon, defaultState);
            return;
        }

        Sprite sprite = settings.SpriteOverride != null ? settings.SpriteOverride : definition.Icon;
        ApplyCustom(image, sprite, settings, defaultState, applyAnchoredPosition, applyCustomTransform);
    }

    public static Vector2 GetAnchoredPositionOffset(ScriptableObject item, ItemDisplayIconContext context)
    {
        if (item == null)
            return Vector2.zero;

        ItemDisplayIconSettings settings = ResolveSettings(item, context);
        return settings != null ? settings.AnchoredPosition : Vector2.zero;
    }

    public static void ApplyRaw(Image image, Sprite sprite, ItemDisplayIconDefaultState defaultState)
    {
        if (image == null)
            return;

        defaultState.ApplyTo(image);
        image.sprite = sprite;
        image.enabled = sprite != null;
    }

    public static void Clear(Image image, ItemDisplayIconDefaultState defaultState)
    {
        if (image == null)
            return;

        defaultState.ApplyTo(image);
        image.sprite = null;
        image.enabled = false;
    }

    private static void ApplyCustom(
        Image image,
        Sprite sprite,
        ItemDisplayIconSettings settings,
        ItemDisplayIconDefaultState defaultState,
        bool applyAnchoredPosition,
        bool applyCustomTransform)
    {
        if (image == null)
            return;

        defaultState.ApplyTo(image);
        image.sprite = sprite;
        image.enabled = sprite != null;
        image.preserveAspect = settings.PreserveAspect;

        RectTransform rect = image.rectTransform;
        if (rect == null)
            return;

        if (!applyCustomTransform)
            return;

        rect.pivot = settings.Pivot;
        if (applyAnchoredPosition)
            rect.anchoredPosition = defaultState.AnchoredPosition + settings.AnchoredPosition;

        rect.localRotation = Quaternion.Euler(0f, 0f, settings.RotationDegrees);
        rect.localScale = settings.LocalScale;
    }

    private static ItemDisplayIconSettings ResolveSettings(ScriptableObject item, ItemDisplayIconContext context)
    {
        if (item is not WeaponDefinition weapon || weapon.DisplayVisualProfile == null)
            return null;

        return weapon.DisplayVisualProfile.TryGetIconSettings(context, out ItemDisplayIconSettings settings)
            ? settings
            : null;
    }
}
