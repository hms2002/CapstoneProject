using UnityEngine;
using UnityEngine.UI;

/// <summary>Draws an authored slot's item-type frame without owning item or interaction state.</summary>
[DisallowMultipleComponent]
public sealed class ItemTypeBorderGraphic : MaskableGraphic
{
    [SerializeField] private Color weaponColor = new Color(0.55f, 0.75f, 0.88f, 1f);
    [SerializeField] private Color relicColor = new Color(0.95f, 0.73f, 0.32f, 1f);
    private int itemKind;

    public void SetItem(ScriptableObject item)
    {
        int next = item is WeaponDefinition ? 1 : item is RelicDefinition ? 2 : 0;
        if (next == itemKind) return;
        itemKind = next;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (itemKind == 0) return;
        Rect rect = GetPixelAdjustedRect();
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
