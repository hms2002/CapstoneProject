using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class UILakeRippleGraphic : MaskableGraphic
{
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField, Range(16, 128)] private int segmentCount = 64;
    [SerializeField] private Color defaultRippleColor = new Color(0.72f, 0.95f, 1f, 0.5f);
    [SerializeField, Min(0.05f)] private float defaultDuration = 1.05f;
    [SerializeField, Min(0f)] private float defaultStartRadius = 14f;
    [SerializeField, Min(1f)] private float defaultEndRadius = 210f;
    [SerializeField, Min(0.5f)] private float defaultThickness = 5.5f;

    private readonly List<Ripple> ripples = new List<Ripple>();

    private struct Ripple
    {
        public Vector2 Center;
        public float Age;
        public float Duration;
        public float StartRadius;
        public float EndRadius;
        public float Thickness;
        public float Intensity;
        public Color Color;
    }

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    private void Update()
    {
        if (ripples.Count == 0)
            return;

        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (deltaTime <= 0f)
            return;

        for (int i = ripples.Count - 1; i >= 0; i--)
        {
            Ripple ripple = ripples[i];
            ripple.Age += deltaTime;
            if (ripple.Age >= ripple.Duration)
            {
                ripples.RemoveAt(i);
                continue;
            }

            ripples[i] = ripple;
        }

        SetVerticesDirty();
    }

    public void Configure(
        bool useUnscaled,
        Color rippleColor,
        float duration,
        float startRadius,
        float endRadius,
        float thickness)
    {
        useUnscaledTime = useUnscaled;
        defaultRippleColor = rippleColor;
        defaultDuration = Mathf.Max(0.05f, duration);
        defaultStartRadius = Mathf.Max(0f, startRadius);
        defaultEndRadius = Mathf.Max(defaultStartRadius + 1f, endRadius);
        defaultThickness = Mathf.Max(0.5f, thickness);
        SetVerticesDirty();
    }

    public void Emit(Vector2 localPosition, float intensity = 1f)
    {
        if (!isActiveAndEnabled)
            return;

        ripples.Add(new Ripple
        {
            Center = localPosition,
            Age = 0f,
            Duration = defaultDuration,
            StartRadius = defaultStartRadius,
            EndRadius = defaultEndRadius,
            Thickness = defaultThickness,
            Intensity = Mathf.Max(0f, intensity),
            Color = defaultRippleColor,
        });

        SetVerticesDirty();
    }

    public void Clear()
    {
        if (ripples.Count == 0)
            return;

        ripples.Clear();
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();

        int segments = Mathf.Clamp(segmentCount, 16, 128);
        for (int i = 0; i < ripples.Count; i++)
            AddRippleMesh(vertexHelper, ripples[i], segments);
    }

    private static void AddRippleMesh(VertexHelper vertexHelper, Ripple ripple, int segments)
    {
        float t = Mathf.Clamp01(ripple.Age / Mathf.Max(0.0001f, ripple.Duration));
        float eased = 1f - Mathf.Pow(1f - t, 3f);
        float radius = Mathf.LerpUnclamped(ripple.StartRadius, ripple.EndRadius, eased);
        float thickness = Mathf.Max(0.5f, ripple.Thickness * Mathf.Lerp(1f, 0.36f, t));
        float innerRadius = Mathf.Max(0f, radius - thickness);
        float outerRadius = radius + thickness;
        float alpha = Mathf.Pow(Mathf.Sin(t * Mathf.PI), 0.7f) * (1f - t * 0.18f) * ripple.Intensity;

        if (alpha <= 0.001f || outerRadius <= 0.001f)
            return;

        Color color = ripple.Color;
        color.a *= Mathf.Clamp01(alpha);
        Color32 color32 = color;

        int baseIndex = vertexHelper.currentVertCount;
        for (int i = 0; i <= segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 inner = ripple.Center + direction * innerRadius;
            Vector2 outer = ripple.Center + direction * outerRadius;

            vertexHelper.AddVert(inner, color32, new Vector2(0f, 0f));
            vertexHelper.AddVert(outer, color32, new Vector2(1f, 1f));

            if (i >= segments)
                continue;

            int index = baseIndex + i * 2;
            vertexHelper.AddTriangle(index, index + 1, index + 3);
            vertexHelper.AddTriangle(index, index + 3, index + 2);
        }
    }
}
