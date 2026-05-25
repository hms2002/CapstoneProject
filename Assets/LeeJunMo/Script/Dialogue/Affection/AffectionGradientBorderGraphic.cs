using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Full-screen shader-driven gradient border with a transparent center.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class AffectionGradientBorderGraphic : MaskableGraphic
{
    private const AdditionalCanvasShaderChannels RequiredShaderChannels =
        AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2;

    [Header("Shape")]
    [Tooltip("Border width relative to the shortest screen side.")]
    [SerializeField, Range(0.01f, 0.5f)] private float borderThicknessRatio = 0.22f;
    [Tooltip("How much neighboring edge glows blend at the corners. This does not clip the gradient.")]
    [SerializeField, Range(0f, 0.35f)] private float cornerRadiusRatio = 0f;

    [Header("Gradient")]
    [Tooltip("Multiplies the final gradient alpha.")]
    [SerializeField, Range(0f, 2f)] private float gradientStrength = 1f;
    [Tooltip("Higher values spread the gradient over a wider distance.")]
    [SerializeField, Range(0.2f, 1.6f)] private float gradientSoftness = 1.35f;
    [Tooltip("Higher values keep the center cleaner and concentrate color near the edge.")]
    [SerializeField, Range(0.25f, 3f)] private float gradientFalloff = 0.95f;

    [Header("Reveal")]
    [Tooltip("Extra off-screen travel used while the full gradient slides in or out.")]
    [SerializeField, Range(0.01f, 0.35f)] private float revealFeatherRatio = 0.24f;

    [Header("Runtime State")]
    [Tooltip("Runtime alpha multiplier. AffectionGainScreenEffect controls this during preview/play.")]
    [SerializeField, Range(0f, 1f)] private float intensity = 1f;
    [Tooltip("Runtime outside-to-inside reveal amount. 0 shows nothing, 1 shows the full border.")]
    [SerializeField, Range(0f, 1f)] private float revealProgress = 1f;

    public float Intensity
    {
        get => intensity;
        set
        {
            float clamped = Mathf.Clamp01(value);
            if (Mathf.Approximately(intensity, clamped))
                return;

            intensity = clamped;
            SetVerticesDirty();
        }
    }

    public float RevealProgress
    {
        get => revealProgress;
        set
        {
            float clamped = Mathf.Clamp01(value);
            if (Mathf.Approximately(revealProgress, clamped))
                return;

            revealProgress = clamped;
            SetVerticesDirty();
        }
    }

    public void ConfigureShape(float thicknessRatio, float cornerRatio)
    {
        float clampedThickness = Mathf.Clamp(thicknessRatio, 0.01f, 0.5f);
        float clampedCorner = Mathf.Clamp(cornerRatio, 0f, 0.35f);
        if (Mathf.Approximately(borderThicknessRatio, clampedThickness) &&
            Mathf.Approximately(cornerRadiusRatio, clampedCorner))
        {
            return;
        }

        borderThicknessRatio = clampedThickness;
        cornerRadiusRatio = clampedCorner;
        SetVerticesDirty();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        EnsureShaderChannels();
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        EnsureShaderChannels();
        SetVerticesDirty();
    }
#endif

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        EnsureShaderChannels();
        vh.Clear();

        Rect rect = GetPixelAdjustedRect();
        if (rect.width <= 0f || rect.height <= 0f)
            return;

        float aspect = rect.height > 0f ? rect.width / rect.height : 1f;
        Vector4 runtimeParams = new Vector4(0f, 0f, revealProgress, intensity);
        Vector4 gradientParams = new Vector4(borderThicknessRatio, cornerRadiusRatio, gradientStrength, gradientSoftness);
        Vector4 falloffParams = new Vector4(gradientFalloff, revealFeatherRatio, aspect, 0f);

        AddVertex(vh, rect.xMin, rect.yMin, 0f, 0f, runtimeParams, gradientParams, falloffParams);
        AddVertex(vh, rect.xMin, rect.yMax, 0f, 1f, runtimeParams, gradientParams, falloffParams);
        AddVertex(vh, rect.xMax, rect.yMax, 1f, 1f, runtimeParams, gradientParams, falloffParams);
        AddVertex(vh, rect.xMax, rect.yMin, 1f, 0f, runtimeParams, gradientParams, falloffParams);

        vh.AddTriangle(0, 1, 2);
        vh.AddTriangle(0, 2, 3);
    }

    private void AddVertex(
        VertexHelper vh,
        float x,
        float y,
        float uvX,
        float uvY,
        Vector4 runtimeParams,
        Vector4 gradientParams,
        Vector4 falloffParams)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = new Vector3(x, y, 0f);
        vertex.color = color;
        runtimeParams.x = uvX;
        runtimeParams.y = uvY;
        vertex.uv0 = runtimeParams;
        vertex.uv1 = gradientParams;
        vertex.uv2 = falloffParams;
        vh.AddVert(vertex);
    }

    private void EnsureShaderChannels()
    {
        Canvas targetCanvas = canvas;
        if (targetCanvas == null)
            return;

        targetCanvas.additionalShaderChannels |= RequiredShaderChannels;
    }
}
