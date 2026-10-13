using UnityEngine;
using UnityEngine.UI;

/// <summary>Authored UI overlay; vertex colors fade vertically without a runtime texture.</summary>
[AddComponentMenu("UI/Encyclopedia Gradient")]
[RequireComponent(typeof(CanvasRenderer))]
public sealed class EncyclopediaGradientGraphic : MaskableGraphic
{
    [SerializeField] private Color topColor = Color.clear;
    [SerializeField] private Color bottomColor = new Color(0f, 0f, 0f, .9f);

    public void Configure(Color top, Color bottom)
    {
        topColor = top;
        bottomColor = bottom;
        raycastTarget = false;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = GetPixelAdjustedRect();
        vh.AddVert(new Vector3(r.xMin, r.yMin), bottomColor * color, Vector2.zero);
        vh.AddVert(new Vector3(r.xMin, r.yMax), topColor * color, Vector2.up);
        vh.AddVert(new Vector3(r.xMax, r.yMax), topColor * color, Vector2.one);
        vh.AddVert(new Vector3(r.xMax, r.yMin), bottomColor * color, Vector2.right);
        vh.AddTriangle(0, 1, 2);
        vh.AddTriangle(2, 3, 0);
    }
}
