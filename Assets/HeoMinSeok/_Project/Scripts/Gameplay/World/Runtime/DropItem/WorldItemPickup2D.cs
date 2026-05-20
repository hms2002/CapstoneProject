using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class WorldItemPickup2D : InteractableBase
{
    private static readonly int OutlineEnabledID = Shader.PropertyToID("_OutlineEnabled");

    [SerializeField] private ScriptableObject item;
    [SerializeField] private Transform promptAnchor;
    [SerializeField] private string interactPromptText = "획득하기";

    [Header("Visual (optional)")]
    [SerializeField] private ItemDisplayVisualPresenter2D itemDisplayPresenter;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private WorldDropSpritePresenter2D dropSpritePresenter;

    [SerializeField] private bool interactionLocked;
    [SerializeField] private int relicLevel = 0;

    private MaterialPropertyBlock outlinePropertyBlock;
    private Collider2D triggerCollider;

    public ScriptableObject Item => item;
    public int RelicLevel => relicLevel;

    public void SetItem(ScriptableObject so, int relicLevelOverride = 0)
    {
        item = so;
        relicLevel = relicLevelOverride;
        RefreshVisual();
    }

    public void SetItem(ScriptableObject so)
    {
        item = so;
        RefreshVisual();
    }

    private void Awake()
    {
        ResolveVisualRefs();
        outlinePropertyBlock = new MaterialPropertyBlock();

        triggerCollider = GetComponent<Collider2D>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
            triggerCollider.enabled = !interactionLocked;
        }

        RefreshVisual();
        OnUnHighlight();
    }

    private void OnEnable() => WorldItemRegistry.Register(this);

    private void OnDisable()
    {
        WorldItemRegistry.Unregister(this);
        WorldItemDetailPresenter.Instance?.Hide(GetDetailAnchor());
    }

    public override bool CanInteract(IPlayerInteractor player)
    {
        return item != null && !interactionLocked;
    }

    public override void OnHighlight()
    {
        if (item == null || interactionLocked)
            return;

        if (itemDisplayPresenter != null)
        {
            itemDisplayPresenter.SetOutline(true);
        }
        else if (spriteRenderer != null)
        {
            spriteRenderer.GetPropertyBlock(outlinePropertyBlock);
            outlinePropertyBlock.SetFloat(OutlineEnabledID, 1f);
            spriteRenderer.SetPropertyBlock(outlinePropertyBlock);
        }

        WorldItemDetailPresenter.Instance?.Show(GetDetailAnchor(), item, RelicLevel);
    }

    public override void OnUnHighlight()
    {
        if (itemDisplayPresenter != null)
        {
            itemDisplayPresenter.SetOutline(false);
        }
        else if (spriteRenderer != null)
        {
            spriteRenderer.GetPropertyBlock(outlinePropertyBlock);
            outlinePropertyBlock.SetFloat(OutlineEnabledID, 0f);
            spriteRenderer.SetPropertyBlock(outlinePropertyBlock);
        }

        WorldItemDetailPresenter.Instance?.Hide(GetDetailAnchor());
    }

    public override void OnPlayerInteract(IPlayerInteractor player)
    {
        if (item == null)
            return;

        WorldPickupDeliveryResult result = WorldPickupDeliveryService.TryDeliver(
            new WorldPickupDeliveryRequest(player, item, RelicLevel, transform.position));

        if (result.Succeeded)
        {
            Destroy(gameObject);
            return;
        }

        ShowPickupWarning(result.WarningCode);
        SpeakPickupFailed(player);
    }

    public override InteractState GetInteractType() => InteractState.Idle;

    public override string GetInteractDescription()
    {
        return item != null && !interactionLocked ? interactPromptText : string.Empty;
    }

    public override Transform GetPromptAnchor() => promptAnchor != null ? promptAnchor : transform;

    private Transform GetDetailAnchor() => promptAnchor != null ? promptAnchor : transform;

    public void SetInteractionLocked(bool locked)
    {
        interactionLocked = locked;
        if (locked)
            OnUnHighlight();

        if (triggerCollider != null)
            triggerCollider.enabled = !locked;
    }

    private static void ShowPickupWarning(WarningPopupCode code)
    {
        if (code != WarningPopupCode.None)
            UIManager.Instance?.ShowWarning(code);
    }

    private static void SpeakPickupFailed(IPlayerInteractor player)
    {
        if (player is PlayerInteractor2D playerInteractor)
            playerInteractor.SpeakSituation(PlayerSpeechSituationEnum.InventoryFull);
    }

    private void RefreshVisual()
    {
        if (itemDisplayPresenter != null)
        {
            itemDisplayPresenter.Apply(item);
            spriteRenderer = itemDisplayPresenter.FallbackRenderer;
            return;
        }

        var def = item != null ? item.AsDef() : null;
        Sprite sprite = def != null ? def.Icon : null;

        if (dropSpritePresenter != null)
        {
            dropSpritePresenter.Apply(sprite, item is WeaponDefinition);
            spriteRenderer = dropSpritePresenter.Renderer;
            return;
        }

        if (spriteRenderer == null)
            return;

        spriteRenderer.sprite = sprite;
        spriteRenderer.enabled = spriteRenderer.sprite != null;
    }

    private void ResolveVisualRefs()
    {
        if (itemDisplayPresenter == null)
            itemDisplayPresenter = GetComponentInChildren<ItemDisplayVisualPresenter2D>(includeInactive: true);

        if (itemDisplayPresenter != null)
            spriteRenderer = itemDisplayPresenter.FallbackRenderer;

        if (dropSpritePresenter == null)
            dropSpritePresenter = GetComponentInChildren<WorldDropSpritePresenter2D>(includeInactive: true);

        if (itemDisplayPresenter == null && dropSpritePresenter != null)
            spriteRenderer = dropSpritePresenter.Renderer;

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(includeInactive: true);
    }
}
