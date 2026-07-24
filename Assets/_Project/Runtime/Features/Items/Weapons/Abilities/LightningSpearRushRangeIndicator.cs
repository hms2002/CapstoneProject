using UnityEngine;

/// <summary>
/// 번개 창 Q 돌진 가능 범위를 원형 메시 표시기로 시각화할 책임을 가집니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public sealed class LightningSpearRushRangeIndicator : MonoBehaviour
{
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    [Header("Ring")]
    [SerializeField, Min(8)] private int segmentCount = 128;
    [SerializeField, Min(0.001f)] private float lineWidth = 0.055f;
    [SerializeField] private Color ringColor = new Color(0.2f, 0.85f, 1f, 0.34f);

    [Header("Pulse")]
    [SerializeField, Range(0f, 1f)] private float alphaPulseAmount = 0.16f;
    [SerializeField, Min(0.01f)] private float pulseSpeed = 2.2f;
    [SerializeField] private bool useUnscaledTime;

    [Header("Sorting")]
    [SerializeField] private string sortingLayerName = "AttackTelegraph";
    [SerializeField] private int sortingOrder = 1;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock propertyBlock;
    private Mesh ringMesh;
    private Color[] meshColors;
    private float radius = 1f;
    private int builtSegmentCount;
    private float builtRadius = -1f;
    private float builtLineWidth = -1f;

    private void Awake()
    {
        CacheComponents();
        ApplyRendererSettings();
        RebuildIfNeeded();
        ApplyColor(1f);
    }

    private void OnEnable()
    {
        CacheComponents();
        ApplyRendererSettings();
        RebuildIfNeeded();
        ApplyColor(1f);
    }

    private void Update()
    {
        float time = useUnscaledTime ? Time.unscaledTime : Time.time;
        float phase = (Mathf.Sin(time * pulseSpeed) + 1f) * 0.5f;
        float alphaMultiplier = Mathf.Lerp(1f - alphaPulseAmount, 1f, phase);
        ApplyColor(alphaMultiplier);
    }

    private void OnValidate()
    {
        segmentCount = Mathf.Max(8, segmentCount);
        lineWidth = Mathf.Max(0.001f, lineWidth);
        alphaPulseAmount = Mathf.Clamp01(alphaPulseAmount);
        pulseSpeed = Mathf.Max(0.01f, pulseSpeed);

        CacheComponents();
        ApplyRendererSettings();
        RebuildIfNeeded(true);
        ApplyColor(1f);
    }

    private void OnDestroy()
    {
        if (ringMesh == null)
            return;

        if (Application.isPlaying)
            Destroy(ringMesh);
        else
            DestroyImmediate(ringMesh);
    }

    public void SetRadius(float newRadius)
    {
        radius = Mathf.Max(0.01f, newRadius);
        RebuildIfNeeded();
    }

    private void CacheComponents()
    {
        if (meshFilter == null)
            meshFilter = GetComponent<MeshFilter>();

        if (meshRenderer == null)
            meshRenderer = GetComponent<MeshRenderer>();

        propertyBlock ??= new MaterialPropertyBlock();
    }

    private void ApplyRendererSettings()
    {
        if (meshRenderer == null)
            return;

        meshRenderer.enabled = true;
        meshRenderer.sortingLayerName = sortingLayerName;
        meshRenderer.sortingOrder = sortingOrder;
    }

    private void RebuildIfNeeded(bool force = false)
    {
        if (meshFilter == null)
            return;

        int safeSegmentCount = Mathf.Max(8, segmentCount);
        float safeRadius = Mathf.Max(0.01f, radius);
        float safeLineWidth = Mathf.Min(Mathf.Max(0.001f, lineWidth), safeRadius);

        if (!force
            && ringMesh != null
            && builtSegmentCount == safeSegmentCount
            && Mathf.Approximately(builtRadius, safeRadius)
            && Mathf.Approximately(builtLineWidth, safeLineWidth))
            return;

        if (ringMesh == null)
        {
            ringMesh = new Mesh
            {
                name = "Lightning Spear Rush Range Ring",
                hideFlags = HideFlags.DontSave
            };
        }
        else
        {
            ringMesh.Clear();
        }

        int vertexCount = (safeSegmentCount + 1) * 2;
        Vector3[] vertices = new Vector3[vertexCount];
        Vector2[] uvs = new Vector2[vertexCount];
        meshColors = new Color[vertexCount];

        float outerRadius = safeRadius + safeLineWidth * 0.5f;
        float innerRadius = Mathf.Max(0.01f, safeRadius - safeLineWidth * 0.5f);

        for (int i = 0; i <= safeSegmentCount; i++)
        {
            float normalized = (float)i / safeSegmentCount;
            float angle = normalized * Mathf.PI * 2f;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);
            int vertexIndex = i * 2;

            vertices[vertexIndex] = new Vector3(cos * outerRadius, sin * outerRadius, 0f);
            vertices[vertexIndex + 1] = new Vector3(cos * innerRadius, sin * innerRadius, 0f);
            uvs[vertexIndex] = new Vector2(normalized, 1f);
            uvs[vertexIndex + 1] = new Vector2(normalized, 0f);
            meshColors[vertexIndex] = ringColor;
            meshColors[vertexIndex + 1] = ringColor;
        }

        int[] triangles = new int[safeSegmentCount * 6];
        for (int i = 0; i < safeSegmentCount; i++)
        {
            int vertexIndex = i * 2;
            int triangleIndex = i * 6;

            triangles[triangleIndex] = vertexIndex;
            triangles[triangleIndex + 1] = vertexIndex + 2;
            triangles[triangleIndex + 2] = vertexIndex + 1;
            triangles[triangleIndex + 3] = vertexIndex + 1;
            triangles[triangleIndex + 4] = vertexIndex + 2;
            triangles[triangleIndex + 5] = vertexIndex + 3;
        }

        ringMesh.vertices = vertices;
        ringMesh.uv = uvs;
        ringMesh.colors = meshColors;
        ringMesh.triangles = triangles;
        ringMesh.RecalculateNormals();
        ringMesh.RecalculateBounds();
        meshFilter.sharedMesh = ringMesh;

        builtSegmentCount = safeSegmentCount;
        builtRadius = safeRadius;
        builtLineWidth = safeLineWidth;
    }

    private void ApplyColor(float alphaMultiplier)
    {
        if (meshRenderer == null || ringMesh == null)
            return;

        Color color = ringColor;
        color.a *= Mathf.Clamp01(alphaMultiplier);

        if (meshColors != null)
        {
            for (int i = 0; i < meshColors.Length; i++)
                meshColors[i] = color;

            ringMesh.colors = meshColors;
        }

        meshRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(ColorId, color);
        propertyBlock.SetColor(BaseColorId, color);
        meshRenderer.SetPropertyBlock(propertyBlock);
    }
}
