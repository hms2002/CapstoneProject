using System;
using UnityEngine;

public enum ItemDisplayContext
{
    ShopDisplay,
    WorldDrop
}

public enum ItemDisplayNormalizeMode
{
    Height = 0,
    FitBox = 1,
    RawSpriteSize = 2
}

public enum ItemDisplayAnchorMode
{
    Center,
    Bottom
}

[Serializable]
public sealed class ItemDisplaySpriteSettings
{
    [SerializeField] private bool normalize = true;
    [SerializeField] private ItemDisplayNormalizeMode normalizeMode = ItemDisplayNormalizeMode.FitBox;
    [SerializeField, Min(0.01f)] private float targetHeight = 0.65f;
    [SerializeField] private Vector2 targetBoxSize = new(1f, 1f);
    [SerializeField] private ItemDisplayAnchorMode anchorMode = ItemDisplayAnchorMode.Bottom;
    [SerializeField] private bool centerX = true;
    [SerializeField] private Vector2 anchorOffset = new(0f, 0.08f);

    public bool Normalize => normalize;
    public ItemDisplayNormalizeMode NormalizeMode => normalizeMode;
    public float TargetHeight => Mathf.Max(0.01f, targetHeight);
    public Vector2 TargetBoxSize => new(Mathf.Max(0.01f, targetBoxSize.x), Mathf.Max(0.01f, targetBoxSize.y));
    public ItemDisplayAnchorMode AnchorMode => anchorMode;
    public bool CenterX => centerX;
    public Vector2 AnchorOffset => anchorOffset;

    public static ItemDisplaySpriteSettings DefaultFor(ItemDisplayContext context, InventoryItemKind? kind)
    {
        bool isWeapon = kind == InventoryItemKind.Weapon;
        bool isShop = context == ItemDisplayContext.ShopDisplay;

        return isWeapon
            ? Raw(isShop ? ItemDisplayAnchorMode.Center : ItemDisplayAnchorMode.Bottom, isShop ? Vector2.zero : new Vector2(0f, 0.08f))
            : FitBox(isShop ? ItemDisplayAnchorMode.Center : ItemDisplayAnchorMode.Bottom, isShop ? Vector2.zero : new Vector2(0f, 0.08f));
    }

    public static ItemDisplaySpriteSettings Raw(ItemDisplayAnchorMode anchorMode, Vector2 anchorOffset)
    {
        return new ItemDisplaySpriteSettings
        {
            normalize = true,
            normalizeMode = ItemDisplayNormalizeMode.RawSpriteSize,
            targetHeight = 0.65f,
            targetBoxSize = new Vector2(1f, 1f),
            anchorMode = anchorMode,
            centerX = true,
            anchorOffset = anchorOffset
        };
    }

    public static ItemDisplaySpriteSettings FitBox(ItemDisplayAnchorMode anchorMode, Vector2 anchorOffset)
    {
        return new ItemDisplaySpriteSettings
        {
            normalize = true,
            normalizeMode = ItemDisplayNormalizeMode.FitBox,
            targetHeight = 0.65f,
            targetBoxSize = new Vector2(1f, 1f),
            anchorMode = anchorMode,
            centerX = true,
            anchorOffset = anchorOffset
        };
    }

    public void OnValidate()
    {
        if (targetHeight <= 0f)
            targetHeight = 0.65f;

        if (targetBoxSize.x <= 0f)
            targetBoxSize.x = 1f;

        if (targetBoxSize.y <= 0f)
            targetBoxSize.y = 1f;
    }
}
