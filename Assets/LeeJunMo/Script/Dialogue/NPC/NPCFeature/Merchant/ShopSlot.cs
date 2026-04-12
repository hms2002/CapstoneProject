using TMPro;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class ShopSlot : InteractableBase
{
    private static readonly int OutlineEnabledID = Shader.PropertyToID("_OutlineEnabled");

    private enum PriceIconPositionMode
    {
        CharacterCount,
        RenderedWidth,
        FixedDistance
    }

    [Header("Ownership")]
    [SerializeField] private MerchantNPC owner;
    [SerializeField] private int slotIndex = -1;

    [Header("Anchors")]
    [SerializeField] private Transform promptAnchor;
    [SerializeField] private Transform detailAnchor;

    [Header("Prompt")]
    [SerializeField] private string interactPromptText = "구매";
    [SerializeField] private string soldLabel = "SOLD";

    [Header("View")]
    [SerializeField] private GameObject itemVisualRoot;
    [SerializeField] private SpriteRenderer itemSpriteRenderer;
    [SerializeField] private TMP_Text priceText;

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

    private void Awake()
    {
        CacheReferences();

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
    }

    private void OnDisable()
    {
        OnUnHighlight();
    }

    public void AssignOwner(MerchantNPC merchant, int assignedSlotIndex)
    {
        owner = merchant;
        slotIndex = assignedSlotIndex;
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
        IInventoryItemDefinition commonDefinition = currentDefinition != null ? currentDefinition.AsDef() : null;

        if (itemVisualRoot != null)
            itemVisualRoot.SetActive(hasActiveItem);

        if (itemSpriteRenderer != null)
        {
            itemSpriteRenderer.sprite = hasActiveItem && commonDefinition != null ? commonDefinition.Icon : null;
            itemSpriteRenderer.enabled = itemSpriteRenderer.sprite != null;
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

        if (itemSpriteRenderer == null)
            itemSpriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (priceIconRenderer == null)
        {
            Transform iconTransform = transform.Find("Icon");
            if (iconTransform != null)
                priceIconRenderer = iconTransform.GetComponent<SpriteRenderer>();
        }
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
        if (itemSpriteRenderer == null)
            return;

        itemSpriteRenderer.GetPropertyBlock(outlinePropertyBlock);
        outlinePropertyBlock.SetFloat(OutlineEnabledID, enabled ? 1f : 0f);
        itemSpriteRenderer.SetPropertyBlock(outlinePropertyBlock);
    }
}
