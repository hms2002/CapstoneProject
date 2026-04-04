using TMPro;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class ShopSlot : InteractableBase
{
    private static readonly int OutlineEnabledID = Shader.PropertyToID("_OutlineEnabled");

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

    private MaterialPropertyBlock outlinePropertyBlock;
    private MerchantStockEntryState currentState;
    private ScriptableObject currentDefinition;

    private void Awake()
    {
        if (owner == null)
            owner = GetComponentInParent<MerchantNPC>();

        if (itemSpriteRenderer == null)
            itemSpriteRenderer = GetComponentInChildren<SpriteRenderer>();

        outlinePropertyBlock = new MaterialPropertyBlock();

        Collider2D collider2D = GetComponent<Collider2D>();
        if (collider2D != null)
            collider2D.isTrigger = true;

        RefreshView();
        OnUnHighlight();
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
