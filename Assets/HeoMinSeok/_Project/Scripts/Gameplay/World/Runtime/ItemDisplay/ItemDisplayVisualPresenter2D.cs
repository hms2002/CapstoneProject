using UnityEngine;

[DisallowMultipleComponent]
public sealed class ItemDisplayVisualPresenter2D : MonoBehaviour
{
    private static readonly int OutlineEnabledID = Shader.PropertyToID("_OutlineEnabled");
    private static readonly SpriteRenderer[] EmptyRenderers = new SpriteRenderer[0];

    [SerializeField] private ItemDisplayContext context = ItemDisplayContext.WorldDrop;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private SpriteRenderer fallbackSpriteRenderer;
    [SerializeField] private bool copyHostSortingToCustomVisual = true;

    private GameObject activeCustomVisual;
    private SpriteRenderer[] outlineRenderers = EmptyRenderers;
    private MaterialPropertyBlock outlinePropertyBlock;
    private Vector3 fallbackBaseLocalPosition;
    private bool hasFallbackBaseLocalPosition;
    private bool outlineEnabled;

    public SpriteRenderer FallbackRenderer
    {
        get
        {
            ResolveReferences();
            return fallbackSpriteRenderer;
        }
    }

    public void Apply(ScriptableObject item)
    {
        ResolveReferences();

        if (item == null)
        {
            ClearVisual();
            return;
        }

        IInventoryItemDefinition definition = item.AsDef();
        Sprite sprite = definition != null ? definition.Icon : null;
        ItemDisplayVisualProfileSO profile = ResolveProfile(item);

        if (profile != null)
        {
            GameObject customPrefab = profile.GetVisualPrefab(context);
            if (customPrefab != null)
            {
                ApplyCustomPrefab(customPrefab);
                return;
            }

            Sprite spriteOverride = profile.GetSpriteOverride(context);
            if (spriteOverride != null)
                sprite = spriteOverride;
        }

        ItemDisplaySpriteSettings settings = ResolveSpriteSettings(profile, definition);
        ApplySprite(sprite, settings);
    }

    public void SetOutline(bool enabled)
    {
        outlineEnabled = enabled;

        if (outlinePropertyBlock == null)
            outlinePropertyBlock = new MaterialPropertyBlock();

        for (int i = 0; i < outlineRenderers.Length; i++)
        {
            SpriteRenderer renderer = outlineRenderers[i];
            if (renderer == null)
                continue;

            renderer.GetPropertyBlock(outlinePropertyBlock);
            outlinePropertyBlock.SetFloat(OutlineEnabledID, enabled ? 1f : 0f);
            renderer.SetPropertyBlock(outlinePropertyBlock);
        }
    }

    public void ClearVisual()
    {
        ClearCustomVisual();

        if (fallbackSpriteRenderer != null)
        {
            fallbackSpriteRenderer.sprite = null;
            fallbackSpriteRenderer.enabled = false;
        }

        outlineRenderers = fallbackSpriteRenderer != null
            ? new[] { fallbackSpriteRenderer }
            : EmptyRenderers;

        SetOutline(false);
    }

    public bool TryResolveVisualBoundsWorld(out Bounds bounds)
    {
        if (activeCustomVisual != null)
            return TryResolveRendererBounds(activeCustomVisual.GetComponentsInChildren<SpriteRenderer>(includeInactive: true), out bounds);

        if (fallbackSpriteRenderer != null && fallbackSpriteRenderer.enabled && fallbackSpriteRenderer.sprite != null)
        {
            bounds = fallbackSpriteRenderer.bounds;
            return true;
        }

        bounds = default;
        return false;
    }

    private void ApplyCustomPrefab(GameObject prefab)
    {
        ClearCustomVisual();

        if (fallbackSpriteRenderer != null)
            fallbackSpriteRenderer.enabled = false;

        Transform parent = visualRoot != null ? visualRoot : transform;
        activeCustomVisual = Instantiate(prefab, parent, false);
        activeCustomVisual.transform.localPosition = Vector3.zero;
        activeCustomVisual.transform.localRotation = Quaternion.identity;
        activeCustomVisual.transform.localScale = Vector3.one;

        if (copyHostSortingToCustomVisual)
            ApplyHostSorting(activeCustomVisual);

        ItemDisplayVisualInstance2D instance = activeCustomVisual.GetComponent<ItemDisplayVisualInstance2D>()
            ?? activeCustomVisual.GetComponentInChildren<ItemDisplayVisualInstance2D>(includeInactive: true);

        outlineRenderers = instance != null
            ? instance.ResolveOutlineRenderers()
            : activeCustomVisual.GetComponentsInChildren<SpriteRenderer>(includeInactive: true);

        SetOutline(outlineEnabled);
    }

    private void ApplySprite(Sprite sprite, ItemDisplaySpriteSettings settings)
    {
        ClearCustomVisual();

        if (fallbackSpriteRenderer == null)
            return;

        fallbackSpriteRenderer.sprite = sprite;
        fallbackSpriteRenderer.enabled = sprite != null;
        outlineRenderers = new[] { fallbackSpriteRenderer };

        if (sprite != null)
            ApplySpriteTransform(sprite, settings);

        SetOutline(outlineEnabled);
    }

    private void ApplySpriteTransform(Sprite sprite, ItemDisplaySpriteSettings settings)
    {
        if (sprite == null || fallbackSpriteRenderer == null || settings == null)
            return;

        CaptureFallbackBaseLocalPosition();

        Bounds bounds = sprite.bounds;
        if (bounds.size.x <= 0f || bounds.size.y <= 0f)
            return;

        float uniformScale = ResolveUniformScale(bounds, settings);
        Transform rendererTransform = fallbackSpriteRenderer.transform;
        Vector3 localScale = rendererTransform.localScale;
        rendererTransform.localScale = new Vector3(uniformScale, uniformScale, localScale.z);

        Vector3 localPosition = fallbackBaseLocalPosition + (Vector3)settings.AnchorOffset;
        if (settings.CenterX)
            localPosition.x += -bounds.center.x * uniformScale;

        localPosition.y += settings.AnchorMode == ItemDisplayAnchorMode.Bottom
            ? -bounds.min.y * uniformScale
            : -bounds.center.y * uniformScale;

        rendererTransform.localPosition = localPosition;
    }

    private static float ResolveUniformScale(Bounds bounds, ItemDisplaySpriteSettings settings)
    {
        if (!settings.Normalize || settings.NormalizeMode == ItemDisplayNormalizeMode.RawSpriteSize)
            return 1f;

        if (settings.NormalizeMode == ItemDisplayNormalizeMode.FitBox)
        {
            Vector2 targetBox = settings.TargetBoxSize;
            float widthScale = targetBox.x / bounds.size.x;
            float heightScale = targetBox.y / bounds.size.y;
            return Mathf.Min(widthScale, heightScale);
        }

        return settings.TargetHeight / bounds.size.y;
    }

    private ItemDisplaySpriteSettings ResolveSpriteSettings(
        ItemDisplayVisualProfileSO profile,
        IInventoryItemDefinition definition)
    {
        if (profile != null && profile.TryGetSpriteSettings(context, out ItemDisplaySpriteSettings profileSettings))
            return profileSettings;

        return ItemDisplaySpriteSettings.DefaultFor(context, definition != null ? definition.Kind : (InventoryItemKind?)null);
    }

    private static ItemDisplayVisualProfileSO ResolveProfile(ScriptableObject item)
    {
        return item is WeaponDefinition weapon ? weapon.DisplayVisualProfile : null;
    }

    private void ApplyHostSorting(GameObject instance)
    {
        if (instance == null || fallbackSpriteRenderer == null)
            return;

        int sortingLayerId = fallbackSpriteRenderer.sortingLayerID;
        int baseSortingOrder = fallbackSpriteRenderer.sortingOrder;

        SpriteRenderer[] renderers = instance.GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null)
                continue;

            renderer.sortingLayerID = sortingLayerId;
            renderer.sortingOrder += baseSortingOrder;
        }

        SpriteMask[] masks = instance.GetComponentsInChildren<SpriteMask>(includeInactive: true);
        for (int i = 0; i < masks.Length; i++)
        {
            SpriteMask mask = masks[i];
            if (mask == null)
                continue;

            mask.frontSortingLayerID = sortingLayerId;
            mask.backSortingLayerID = sortingLayerId;
            mask.frontSortingOrder += baseSortingOrder;
            mask.backSortingOrder += baseSortingOrder;
        }
    }

    private void ClearCustomVisual()
    {
        if (activeCustomVisual == null)
            return;

        if (Application.isPlaying)
            Destroy(activeCustomVisual);
        else
            DestroyImmediate(activeCustomVisual);

        activeCustomVisual = null;
    }

    private void CaptureFallbackBaseLocalPosition()
    {
        if (hasFallbackBaseLocalPosition || fallbackSpriteRenderer == null)
            return;

        fallbackBaseLocalPosition = fallbackSpriteRenderer.transform.localPosition;
        hasFallbackBaseLocalPosition = true;
    }

    private void ResolveReferences()
    {
        if (visualRoot == null)
            visualRoot = transform;

        if (fallbackSpriteRenderer == null)
            fallbackSpriteRenderer = GetComponentInChildren<SpriteRenderer>(includeInactive: true);

        CaptureFallbackBaseLocalPosition();
    }

    private static bool TryResolveRendererBounds(SpriteRenderer[] renderers, out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;

        if (renderers == null)
            return false;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled || renderer.sprite == null)
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();
        outlinePropertyBlock = new MaterialPropertyBlock();

        if (fallbackSpriteRenderer != null)
            outlineRenderers = new[] { fallbackSpriteRenderer };
    }

    private void OnValidate()
    {
        ResolveReferences();
    }

    private void OnDestroy()
    {
        ClearCustomVisual();
    }
}
