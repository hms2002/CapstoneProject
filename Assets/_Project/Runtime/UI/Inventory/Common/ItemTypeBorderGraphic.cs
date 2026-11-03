using UnityEngine;
using UnityEngine.UI;

/// <summary>Projects item type or chest relic rarity into the authored slot's frame.</summary>
[DisallowMultipleComponent]
public sealed class ItemTypeBorderGraphic : MaskableGraphic
{
    [SerializeField] private Color weaponColor = new Color(0.55f, 0.75f, 0.88f, 1f);
    [SerializeField] private Color relicColor = new Color(0.95f, 0.73f, 0.32f, 1f);
    private int itemKind;
    private bool showChestRarity;
    private Color rarityColor;

    public void SetItem(ScriptableObject item, bool useChestRarityBorder = false)
    {
        int next = item is WeaponDefinition ? 1 : item is RelicDefinition ? 2 : 0;
        bool nextShowRarity = useChestRarityBorder && item is RelicDefinition;
        Color nextColor = nextShowRarity ? ChestRelicColor((RelicDefinition)item) : Color.clear;
        if (next == itemKind && nextShowRarity == showChestRarity && nextColor == rarityColor) return;
        itemKind = next;
        showChestRarity = nextShowRarity;
        rarityColor = nextColor;
        SetVerticesDirty();
    }

    private static Color ChestRelicColor(RelicDefinition relic)
    {
        // Exclusive relics use their authored logic; names and localized text are not identity.
        if (relic.logic is RelicLogic_WeaponExclusive)
            return new Color32(155, 227, 74, 255);
        return relic.rarity switch
        {
            ItemRarity.Rare => new Color32(64, 156, 255, 255),
            ItemRarity.Epic => new Color32(242, 193, 78, 255),
            _ => new Color32(200, 205, 214, 255)
        };
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (itemKind == 0) return;
        Rect rect = GetPixelAdjustedRect();
        if (showChestRarity)
        {
            DrawChestRarityFrame(vh, rect);
            return;
        }
        float unit = Mathf.Min(rect.width, rect.height) / 100f;
        if (unit <= 0f) return;
        float left = rect.xMin + 2f * unit, right = rect.xMax - 2f * unit;
        float bottom = rect.yMin + 2f * unit, top = rect.yMax - 2f * unit;
        Color tint = (itemKind == 1 ? weaponColor : relicColor) * color;
        float cut = itemKind == 1 ? 0f : 10f * unit;
        Line(vh, new Vector2(left + cut, top), new Vector2(right - cut, top), 2f * unit, tint);
        Line(vh, new Vector2(left + cut, bottom), new Vector2(right - cut, bottom), 2f * unit, tint);
        Line(vh, new Vector2(left, bottom + cut), new Vector2(left, top - cut), 2f * unit, tint);
        Line(vh, new Vector2(right, bottom + cut), new Vector2(right, top - cut), 2f * unit, tint);
        for (int x = 0; x < 2; x++)
        for (int y = 0; y < 2; y++)
        {
            Vector2 corner = new Vector2(x == 0 ? left : right, y == 0 ? bottom : top);
            Vector2 horizontal = new Vector2(x == 0 ? 1f : -1f, 0f);
            Vector2 vertical = new Vector2(0f, y == 0 ? 1f : -1f);
            if (itemKind == 1)
            {
                Line(vh, corner, corner + horizontal * 12f * unit, 5f * unit, tint);
                Line(vh, corner, corner + vertical * 12f * unit, 5f * unit, tint);
            }
            else
            {
                Line(vh, corner + horizontal * cut, corner + vertical * cut, 2f * unit, tint);
                Vector2 gem = corner + (horizontal + vertical) * 5f * unit;
                Quad(vh, gem + Vector2.up * 4f * unit, gem + Vector2.right * 4f * unit,
                    gem + Vector2.down * 4f * unit, gem + Vector2.left * 4f * unit, tint);
            }
        }
    }

    private void DrawChestRarityFrame(VertexHelper vh, Rect rect)
    {
        float pixel = Mathf.Min(rect.width, rect.height) / 64f;
        if (pixel <= 0f) return;
        Color tint = rarityColor * color;
        Color dark = new Color(0.035f, 0.047f, 0.063f, 1f) * color;
        Color light = Color.Lerp(rarityColor, Color.white, 0.28f) * color;
        Color shade = new Color(rarityColor.r * 0.6f, rarityColor.g * 0.6f, rarityColor.b * 0.6f, 1f) * color;

        // Two plain stepped rings. The dark gutter separates them from the icon and slot art.
        PixelRing(vh, rect, 0f, pixel * 3f, pixel * 3f, dark, dark);
        PixelRing(vh, rect, pixel, pixel * 2f, pixel * 2f, tint, shade);
        PixelRing(vh, rect, pixel, pixel, pixel * 2f, light, tint);
        PixelRing(vh, rect, pixel * 4f, pixel * 3f, pixel * 2f, dark, dark);
        PixelRing(vh, rect, pixel * 5f, pixel, pixel, tint, shade);
    }

    private static void PixelRing(VertexHelper vh, Rect rect, float inset, float width, float cut,
        Color upperLeft, Color lowerRight)
    {
        float left = rect.xMin + inset, right = rect.xMax - inset;
        float bottom = rect.yMin + inset, top = rect.yMax - inset;
        Box(vh, left + cut, top - width, right - cut, top, upperLeft);
        Box(vh, left + cut, bottom, right - cut, bottom + width, lowerRight);
        Box(vh, left, bottom + cut, left + width, top - cut, upperLeft);
        Box(vh, right - width, bottom + cut, right, top - cut, lowerRight);
        Box(vh, left + cut * 0.5f, top - cut - width * 0.5f, left + cut + width * 0.5f, top - cut * 0.5f, upperLeft);
        Box(vh, right - cut - width * 0.5f, top - cut - width * 0.5f, right - cut * 0.5f, top - cut * 0.5f, upperLeft);
        Box(vh, left + cut * 0.5f, bottom + cut * 0.5f, left + cut + width * 0.5f, bottom + cut + width * 0.5f, lowerRight);
        Box(vh, right - cut - width * 0.5f, bottom + cut * 0.5f, right - cut * 0.5f, bottom + cut + width * 0.5f, lowerRight);
    }

    private static void Box(VertexHelper vh, float left, float bottom, float right, float top, Color tint)
        => Quad(vh, new Vector2(left, bottom), new Vector2(left, top),
            new Vector2(right, top), new Vector2(right, bottom), tint);

    private static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color tint)
    {
        Vector2 direction = (b - a).normalized;
        Vector2 offset = new Vector2(-direction.y, direction.x) * width * 0.5f;
        Quad(vh, a - offset, a + offset, b + offset, b - offset, tint);
    }

    private static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color tint)
    {
        int start = vh.currentVertCount;
        vh.AddVert(a, tint, Vector2.zero);
        vh.AddVert(b, tint, Vector2.zero);
        vh.AddVert(c, tint, Vector2.zero);
        vh.AddVert(d, tint, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }
}
