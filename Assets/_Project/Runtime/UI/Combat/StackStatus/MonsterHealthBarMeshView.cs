using UnityEngine;
using UnityEngine.UI;

/// <summary>Projects one actor's HP/trail into its authored shared-quad renderer; owns only its property block.</summary>
[DisallowMultipleComponent]
public sealed class MonsterHealthBarMeshView : MonoBehaviour
{
    private static readonly int HealthDataId = Shader.PropertyToID("_HealthData");
    [SerializeField] private MeshRenderer targetRenderer;
    [SerializeField] private MeshFilter targetMeshFilter;
    [SerializeField] private Image[] legacyGraphics;
    private MaterialPropertyBlock properties;
    private Vector4 lastHealthData;
    private bool hasHealthData;
    private bool usesLegacyProjection;

    public bool IsConfigured => targetRenderer != null && targetMeshFilter != null &&
        targetMeshFilter.sharedMesh != null && targetRenderer.sharedMaterial != null &&
        legacyGraphics != null && legacyGraphics.Length == 6;
    public bool IsVisible => targetRenderer != null && targetRenderer.enabled;

    private void Awake()
    {
        if (targetRenderer == null) targetRenderer = GetComponent<MeshRenderer>();
        if (targetMeshFilter == null) targetMeshFilter = GetComponent<MeshFilter>();
        Hide();
    }

    public void Project(float width, float height, float health, float trail, int sortingLayerId, int sortingOrder, bool visible)
    {
        if (!IsConfigured || !visible)
        {
            Hide();
            return;
        }
        width = Mathf.Max(0f, width);
        height = Mathf.Max(0f, height);
        var scale = new Vector3(width, height, 1f);
        if (transform.localScale != scale) transform.localScale = scale;
        if (targetRenderer.sortingLayerID != sortingLayerId) targetRenderer.sortingLayerID = sortingLayerId;
        if (targetRenderer.sortingOrder != sortingOrder) targetRenderer.sortingOrder = sortingOrder;
        var data = new Vector4(width, height, Mathf.Clamp01(health), Mathf.Clamp01(trail));
        if (!hasHealthData || data != lastHealthData)
        {
            properties ??= new MaterialPropertyBlock();
            properties.SetVector(HealthDataId, data);
            targetRenderer.SetPropertyBlock(properties);
            lastHealthData = data;
            hasHealthData = true;
        }
        bool enabled = !usesLegacyProjection && width > 0f && height > 0f;
        if (targetRenderer.enabled != enabled) targetRenderer.enabled = enabled;
    }

    public void SetLegacyProjection(bool enabled)
    {
        usesLegacyProjection = enabled;
        if (legacyGraphics != null) foreach (Image image in legacyGraphics)
            if (image != null && image.enabled != enabled) image.enabled = enabled;
        if (enabled) Hide();
    }

    public void Hide()
    {
        if (targetRenderer != null) targetRenderer.enabled = false;
    }

    private void OnDisable() => Hide();

    private void OnDestroy()
    {
        if (targetRenderer != null) targetRenderer.SetPropertyBlock(null);
        properties = null;
        // The mesh and material are authored shared assets, not actor-owned allocations.
    }
}
