using UnityEngine;

[DisallowMultipleComponent]
public sealed class ShadowMonsterLightVisibility : MonoBehaviour, IMonsterGaugeVisibilityFilter
{
    private const int MaxAreas = 32;
    private static readonly int CountId = Shader.PropertyToID("_RevealCount");
    private static readonly int AreasId = Shader.PropertyToID("_RevealAreas");

    [SerializeField] private SpriteRenderer[] targetRenderers;
    [SerializeField] private Material revealMaterial;
    [SerializeField] private ShadowMonsterGaugeVisibilityFilter gaugeFilter;

    private readonly Vector4[] areas = new Vector4[MaxAreas];
    private MaterialPropertyBlock properties;
    private SceneLightVisionPresentation owner;
    private Material[] originalMaterials;
    private SpriteMaskInteraction[] originalMasks;
    private int areaCount;

    private void Awake() => properties = new MaterialPropertyBlock();

    private void OnEnable()
    {
        owner = SceneLightVisionPresentation.FindForScene(gameObject.scene);
        if (owner == null || revealMaterial == null)
            return;

        originalMaterials = new Material[targetRenderers.Length];
        originalMasks = new SpriteMaskInteraction[targetRenderers.Length];
        for (int i = 0; i < targetRenderers.Length; i++)
        {
            var target = targetRenderers[i];
            if (target == null) continue;
            originalMaterials[i] = target.sharedMaterial;
            originalMasks[i] = target.maskInteraction;
            target.sharedMaterial = revealMaterial;
            target.maskInteraction = SpriteMaskInteraction.None;
        }
        gaugeFilter.SetVisibilityOverride(this);
        RefreshAreas();
    }

    private void LateUpdate() => RefreshAreas();

    private void RefreshAreas()
    {
        if (originalMaterials == null)
            return;
        Bounds bounds = new(gaugeFilter.SamplePoint, Vector3.zero);
        foreach (var target in targetRenderers)
            if (target != null) bounds.Encapsulate(target.bounds);
        areaCount = owner != null ? owner.CopyAreas(bounds, areas) : 0;
        foreach (var target in targetRenderers)
        {
            if (target == null) continue;
            target.GetPropertyBlock(properties);
            properties.SetInt(CountId, areaCount);
            properties.SetVectorArray(AreasId, areas);
            target.SetPropertyBlock(properties);
        }
    }

    public bool ShouldShowGauge()
    {
        RefreshAreas();
        for (int i = 0; i < areaCount; i++)
            if (SceneLightVisionPresentation.Contains(areas[i], gaugeFilter.SamplePoint))
                return true;
        return false;
    }

    private void OnDisable()
    {
        if (originalMaterials == null)
            return;
        gaugeFilter.SetVisibilityOverride(null);
        for (int i = 0; i < targetRenderers.Length; i++)
        {
            var target = targetRenderers[i];
            if (target == null) continue;
            target.sharedMaterial = originalMaterials[i];
            target.maskInteraction = originalMasks[i];
        }
        originalMaterials = null;
        owner = null;
        areaCount = 0;
    }
}
