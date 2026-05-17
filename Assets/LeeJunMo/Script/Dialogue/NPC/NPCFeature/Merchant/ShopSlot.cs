using TMPro;
using UnityEngine;

/// <summary>
/// 책임 :
/// - 상점 슬롯의 구매 상호작용, 가격 표시, 진열 아이템 표시를 관리한다.
/// - 아이템 icon의 원본 크기와 pivot 차이를 슬롯 기준 표시 규칙에 맞춰 정규화한다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public sealed class ShopSlot : InteractableBase
{
    private static readonly int OutlineEnabledID = Shader.PropertyToID("_OutlineEnabled");

    private enum ItemSpriteNormalizeMode
    {
        Height,
        FitBox
    }

    private enum ItemSpriteAnchorMode
    {
        Center,
        Bottom
    }

    private enum PriceIconPositionMode
    {
        CharacterCount,
        RenderedWidth,
        FixedDistance
    }

    [Header("Ownership")]
    [SerializeField] private MerchantNPC owner;
    [SerializeField] private int slotIndex = -1;

    [Header("Stock Filter")]
    [SerializeField] private ShopSlotItemFilter itemFilter = ShopSlotItemFilter.Any;

    [Header("Anchors")]
    [SerializeField] private Transform promptAnchor;
    [SerializeField] private Transform detailAnchor;

    [Header("Prompt")]
    [SerializeField] private string interactPromptText = "구매";
    [SerializeField] private string soldLabel = "SOLD";

    [Header("View")]
    [SerializeField] private GameObject itemVisualRoot;
    [SerializeField] private ItemDisplayVisualPresenter2D itemDisplayPresenter;
    [SerializeField] private SpriteRenderer itemSpriteRenderer;
    [SerializeField] private TMP_Text priceText;

    [Header("Legacy Item Sprite Normalization")]
    [SerializeField, HideInInspector] private bool normalizeItemSprite = true;
    [SerializeField, HideInInspector] private ItemSpriteNormalizeMode itemSpriteNormalizeMode = ItemSpriteNormalizeMode.FitBox;
    [SerializeField, HideInInspector, Min(0.01f)] private float itemSpriteTargetHeight = 0.65f;
    [SerializeField, HideInInspector] private Vector2 itemSpriteTargetBoxSize = new(1f, 1f);
    [SerializeField, HideInInspector] private ItemSpriteAnchorMode itemSpriteAnchorMode = ItemSpriteAnchorMode.Center;
    [SerializeField, HideInInspector] private bool itemSpriteCenterX = true;
    [SerializeField, HideInInspector] private Vector2 itemSpriteAnchorOffset = Vector2.zero;

    [Header("Price Icon")]
    [SerializeField] private SpriteRenderer priceIconRenderer;
    [SerializeField] private Sprite currencyIconSprite;
    [SerializeField] private Vector3 priceIconOffset = Vector3.zero;
    [SerializeField, Min(0f)] private float priceIconSpacing = 0.08f;
    [SerializeField] private PriceIconPositionMode priceIconPositionMode = PriceIconPositionMode.CharacterCount;
    [SerializeField, Min(0f)] private float priceCharacterWidth = 0.18f;
    [SerializeField, Min(0f)] private float priceIconFixedDistance = 0.32f;
    [SerializeField] private Vector3 priceIconLocalScale = Vector3.one;

    private MaterialPropertyBlock outlinePropertyBlock;
    private MerchantStockEntryState currentState;
    private ScriptableObject currentDefinition;
    private Vector3 itemSpriteBaseLocalPosition;
    private bool hasItemSpriteBaseLocalPosition;

    private void Awake()
    {
        CacheReferences();
        CaptureItemSpriteBaseLocalPosition();

        outlinePropertyBlock = new MaterialPropertyBlock();

        Collider2D collider2D = GetComponent<Collider2D>();
        if (collider2D != null)
            collider2D.isTrigger = true;

        RefreshView();
        OnUnHighlight();
    }

    private void OnValidate()
    {
        CacheReferences();
        ApplyPriceIconScale();

        if (itemSpriteTargetHeight <= 0f)
            itemSpriteTargetHeight = 0.65f;

        if (itemSpriteTargetBoxSize.x <= 0f)
            itemSpriteTargetBoxSize.x = 1f;

        if (itemSpriteTargetBoxSize.y <= 0f)
            itemSpriteTargetBoxSize.y = 1f;
    }

    private void OnDisable()
    {
        OnUnHighlight();
    }

    public ShopSlotItemFilter ItemFilter => itemFilter;

    public void AssignOwner(MerchantNPC merchant, int assignedSlotIndex)
    {
        owner = merchant;
        slotIndex = assignedSlotIndex;
    }

    public void ApplyRuntimeItemFilter(ShopSlotItemFilter filter)
    {
        itemFilter = filter;
    }

    public void ApplyState(MerchantStockEntryState state)
    {
        currentState = state;
        currentDefinition = currentState != null ? currentState.ResolveDefinition() : null;
        RefreshView();
    }

    public override bool CanInteract(IPlayerInteractor player)
    {
        if (player == null || player.CurrentState != InteractState.Idle)
            return false;

        return owner != null &&
               currentState != null &&
               !currentState.isSold &&
               currentDefinition != null;
    }

    public override void OnPlayerInteract(IPlayerInteractor player)
    {
        if (owner == null || player == null)
            return;

        owner.TryPurchase(slotIndex, player);
    }

    public override void OnHighlight()
    {
        SetOutline(true);

        if (currentDefinition == null || currentState == null || currentState.isSold)
            return;

        WorldItemDetailPresenter.Instance?.Show(GetDetailAnchor(), currentDefinition);
    }

    public override void OnUnHighlight()
    {
        SetOutline(false);
        WorldItemDetailPresenter.Instance?.Hide(GetDetailAnchor());
    }

    public override InteractState GetInteractType() => InteractState.Idle;

    public override string GetInteractDescription()
    {
        return currentDefinition != null && currentState != null && !currentState.isSold
            ? interactPromptText
            : string.Empty;
    }

    public override Transform GetPromptAnchor() => promptAnchor != null ? promptAnchor : transform;

    private Transform GetDetailAnchor()
    {
        if (detailAnchor != null)
            return detailAnchor;

        if (promptAnchor != null)
            return promptAnchor;

        return transform;
    }

    private void RefreshView()
    {
        bool hasActiveItem = currentState != null && currentState.HasItem && !currentState.isSold && currentDefinition != null;

        if (itemVisualRoot != null)
            itemVisualRoot.SetActive(hasActiveItem);

        if (itemDisplayPresenter != null)
        {
            if (hasActiveItem)
                itemDisplayPresenter.Apply(currentDefinition);
            else
                itemDisplayPresenter.ClearVisual();
        }
        else
        {
            ApplyItemSprite(hasActiveItem ? currentDefinition : null);
        }

        if (priceText != null)
        {
            if (currentState == null || !currentState.HasItem)
                priceText.text = string.Empty;
            else
                priceText.text = currentState.isSold ? soldLabel : currentState.price.ToString();
        }

        RefreshPriceIcon(hasActiveItem && currentState != null && !currentState.isSold);
    }

    private void ApplyItemSprite(ScriptableObject item)
    {
        if (itemSpriteRenderer == null)
            return;

        IInventoryItemDefinition definition = item != null ? item.AsDef() : null;
        Sprite sprite = ResolveWorldDropSprite(item, definition);
        itemSpriteRenderer.sprite = sprite;
        itemSpriteRenderer.enabled = sprite != null;

        if (sprite == null)
            return;

        ItemDisplayVisualProfileSO profile = ResolveProfile(item);
        ItemDisplaySpriteSettings settings = ResolveWorldDropSpriteSettings(profile, definition);
        ApplyNormalizedItemSpriteTransform(sprite, settings);
    }

    /// <summary>
    /// 책임 :
    /// - 상점 진열 icon의 원본 pivot과 sprite bounds 차이가 슬롯 안 표시 위치를 흔들지 않도록 보정한다.
    /// - 슬롯마다 다른 기준 위치는 유지하면서 sprite만 공통 높이/박스와 기준점에 맞춘다.
    /// </summary>
    private void ApplyNormalizedItemSpriteTransform(Sprite sprite, ItemDisplaySpriteSettings settings)
    {
        if (itemSpriteRenderer == null || sprite == null || settings == null)
            return;

        CaptureItemSpriteBaseLocalPosition();

        Bounds bounds = sprite.bounds;
        if (bounds.size.x <= 0f || bounds.size.y <= 0f)
            return;

        float uniformScale = ResolveItemSpriteUniformScale(bounds, settings);
        Transform rendererTransform = itemSpriteRenderer.transform;
        Vector3 localScale = rendererTransform.localScale;
        rendererTransform.localScale = new Vector3(uniformScale, uniformScale, localScale.z);

        Vector3 localPosition = itemSpriteBaseLocalPosition + (Vector3)settings.AnchorOffset;
        if (settings.CenterX)
            localPosition.x += -bounds.center.x * uniformScale;

        localPosition.y += settings.AnchorMode == ItemDisplayAnchorMode.Bottom
            ? -bounds.min.y * uniformScale
            : -bounds.center.y * uniformScale;

        rendererTransform.localPosition = localPosition;
    }

    private float ResolveItemSpriteUniformScale(Bounds bounds, ItemDisplaySpriteSettings settings)
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

    private static Sprite ResolveWorldDropSprite(ScriptableObject item, IInventoryItemDefinition definition)
    {
        ItemDisplayVisualProfileSO profile = ResolveProfile(item);
        Sprite profileSprite = profile != null ? profile.GetSpriteOverride(ItemDisplayContext.WorldDrop) : null;
        return profileSprite != null ? profileSprite : definition?.Icon;
    }

    private static ItemDisplaySpriteSettings ResolveWorldDropSpriteSettings(
        ItemDisplayVisualProfileSO profile,
        IInventoryItemDefinition definition)
    {
        if (profile != null && profile.TryGetSpriteSettings(ItemDisplayContext.WorldDrop, out ItemDisplaySpriteSettings settings))
            return settings;

        return ItemDisplaySpriteSettings.DefaultFor(ItemDisplayContext.WorldDrop, definition != null ? definition.Kind : (InventoryItemKind?)null);
    }

    private static ItemDisplayVisualProfileSO ResolveProfile(ScriptableObject item)
    {
        return item is WeaponDefinition weapon ? weapon.DisplayVisualProfile : null;
    }

    private void RefreshPriceIcon(bool showCurrencyIcon)
    {
        if (priceIconRenderer == null)
            return;

        bool shouldShow = showCurrencyIcon && currencyIconSprite != null && priceText != null;
        priceIconRenderer.enabled = shouldShow;

        if (!shouldShow)
            return;

        priceIconRenderer.sprite = currencyIconSprite;
        ApplyPriceIconScale();
        priceText.ForceMeshUpdate();

        float iconDistanceFromTextCenter = ResolvePriceIconDistanceFromTextCenter();
        Vector3 targetLocalPosition =
            priceText.transform.localPosition +
            priceIconOffset +
            (Vector3.left * iconDistanceFromTextCenter);

        priceIconRenderer.transform.localPosition = targetLocalPosition;
    }

    private void CacheReferences()
    {
        if (owner == null)
            owner = GetComponentInParent<MerchantNPC>();

        if (itemDisplayPresenter == null)
            itemDisplayPresenter = GetComponentInChildren<ItemDisplayVisualPresenter2D>(includeInactive: true);

        if (itemDisplayPresenter != null)
            itemSpriteRenderer = itemDisplayPresenter.FallbackRenderer;

        if (itemSpriteRenderer == null)
            itemSpriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (priceIconRenderer == null)
        {
            Transform iconTransform = transform.Find("Icon");
            if (iconTransform != null)
                priceIconRenderer = iconTransform.GetComponent<SpriteRenderer>();
        }
    }

    private void CaptureItemSpriteBaseLocalPosition()
    {
        if (hasItemSpriteBaseLocalPosition || itemSpriteRenderer == null)
            return;

        itemSpriteBaseLocalPosition = itemSpriteRenderer.transform.localPosition;
        hasItemSpriteBaseLocalPosition = true;
    }

    private void ApplyPriceIconScale()
    {
        if (priceIconRenderer == null)
            return;

        priceIconRenderer.transform.localScale = priceIconLocalScale;
    }

    private float ResolvePriceIconDistanceFromTextCenter()
    {
        switch (priceIconPositionMode)
        {
            case PriceIconPositionMode.RenderedWidth:
                return Mathf.Max(0f, priceText.preferredWidth * 0.5f) + ResolvePriceIconHalfWidth() + priceIconSpacing;

            case PriceIconPositionMode.FixedDistance:
                return Mathf.Max(0f, priceIconFixedDistance);

            case PriceIconPositionMode.CharacterCount:
            default:
                return ResolvePriceCharacterCountHalfWidth() + ResolvePriceIconHalfWidth() + priceIconSpacing;
        }
    }

    private float ResolvePriceCharacterCountHalfWidth()
    {
        if (priceText == null || string.IsNullOrEmpty(priceText.text))
            return 0f;

        int visibleCharacterCount = 0;
        string label = priceText.text;
        for (int i = 0; i < label.Length; i++)
        {
            if (!char.IsWhiteSpace(label[i]))
                visibleCharacterCount++;
        }

        return visibleCharacterCount * priceCharacterWidth * 0.5f;
    }

    private float ResolvePriceIconHalfWidth()
    {
        Sprite iconSprite = priceIconRenderer != null ? priceIconRenderer.sprite : null;
        if (iconSprite == null)
            return 0f;

        Vector3 localScale = priceIconLocalScale;
        float scaleX = Mathf.Abs(localScale.x);
        if (scaleX <= Mathf.Epsilon)
            scaleX = 1f;

        return iconSprite.bounds.extents.x * scaleX;
    }

    private void SetOutline(bool enabled)
    {
        if (itemDisplayPresenter != null)
        {
            itemDisplayPresenter.SetOutline(enabled);
            return;
        }

        if (itemSpriteRenderer == null)
            return;

        itemSpriteRenderer.GetPropertyBlock(outlinePropertyBlock);
        outlinePropertyBlock.SetFloat(OutlineEnabledID, enabled ? 1f : 0f);
        itemSpriteRenderer.SetPropertyBlock(outlinePropertyBlock);
    }
}
