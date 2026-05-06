using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class MerchantRefreshInteractable : InteractableBase
{
    [Header("Ownership")]
    [SerializeField] private MerchantNPC owner;

    [Header("Prompt")]
    [SerializeField] private Transform promptAnchor;
    [SerializeField] private string interactPromptText = "새로고침";

    private void Awake()
    {
        if (owner == null)
            owner = GetComponentInParent<MerchantNPC>();

        Collider2D collider2D = GetComponent<Collider2D>();
        if (collider2D != null)
            collider2D.isTrigger = true;
    }

    private void OnValidate()
    {
        if (owner == null)
            owner = GetComponentInParent<MerchantNPC>();
    }

    public override bool CanInteract(IPlayerInteractor player)
    {
        return player != null &&
               player.CurrentState == InteractState.Idle &&
               owner != null &&
               owner.CanRefreshStock();
    }

    public override void OnPlayerInteract(IPlayerInteractor player)
    {
        if (CanInteract(player))
            owner.TryRefreshStock();
    }

    public override InteractState GetInteractType() => InteractState.Idle;

    public override string GetInteractDescription()
    {
        return owner != null && owner.CanRefreshStock()
            ? interactPromptText
            : string.Empty;
    }

    public override Transform GetPromptAnchor() => promptAnchor != null ? promptAnchor : transform;
}
