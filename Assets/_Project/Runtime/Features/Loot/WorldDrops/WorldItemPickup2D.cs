using CapstoneAudio;
using UnityEngine;

/// <summary>
/// 책임 : 월드에 떨어진 아이템의 상호작용, 획득 전달, outline과 상세 hover 요청을 관리한다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class WorldItemPickup2D : InteractableBase
{
    private static readonly SoundRef GetItemSound = SoundRef.FromKey("sound_worldDropItem_GetItem");
    private static readonly int OutlineEnabledID = Shader.PropertyToID("_OutlineEnabled");

    [SerializeField] private ScriptableObject item;
    [SerializeField] private Transform promptAnchor;
    [SerializeField] private string interactPromptText = "획득하기";

    [Header("Visual (optional)")]
    [SerializeField] private Component itemDisplayPresenter;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Component dropSpritePresenter;

    [SerializeField] private bool interactionLocked;
    [SerializeField] private int relicLevel = 0;

    private MaterialPropertyBlock outlinePropertyBlock;
    private Collider2D triggerCollider;

    public ScriptableObject Item => item;
    public int RelicLevel => relicLevel;
    private IItemDisplayVisualPresenter ItemDisplayPresenter => itemDisplayPresenter as IItemDisplayVisualPresenter;
    private IWorldDropSpritePresenter DropSpritePresenter => dropSpritePresenter as IWorldDropSpritePresenter;

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
        WorldItemHoverPlayback.Hide(GetDetailAnchor());
    }

    public override bool CanInteract(IPlayerInteractor player)
    {
        return item != null && !interactionLocked;
    }

    public override void OnHighlight()
    {
        if (item == null || interactionLocked)
            return;

        IItemDisplayVisualPresenter presenter = ItemDisplayPresenter;
        if (presenter != null)
        {
            presenter.SetOutline(true);
        }
        else if (spriteRenderer != null)
        {
            spriteRenderer.GetPropertyBlock(outlinePropertyBlock);
            outlinePropertyBlock.SetFloat(OutlineEnabledID, 1f);
            spriteRenderer.SetPropertyBlock(outlinePropertyBlock);
        }

        WorldItemHoverPlayback.Show(GetDetailAnchor(), item, RelicLevel);
    }

    public override void OnUnHighlight()
    {
        IItemDisplayVisualPresenter presenter = ItemDisplayPresenter;
        if (presenter != null)
        {
            presenter.SetOutline(false);
        }
        else if (spriteRenderer != null)
        {
            spriteRenderer.GetPropertyBlock(outlinePropertyBlock);
            outlinePropertyBlock.SetFloat(OutlineEnabledID, 0f);
            spriteRenderer.SetPropertyBlock(outlinePropertyBlock);
        }

        WorldItemHoverPlayback.Hide(GetDetailAnchor());
    }

    public override void OnPlayerInteract(IPlayerInteractor player)
    {
        if (item == null)
            return;

        WorldPickupDeliveryResult result = WorldPickupDeliveryService.TryDeliver(
            new WorldPickupDeliveryRequest(player, item, RelicLevel, transform.position));

        if (result.Succeeded)
        {
            PlayGetItemSoundIfNeeded();
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
            WarningPopupPlayback.Show(code);
    }

    private static void SpeakPickupFailed(IPlayerInteractor player)
    {
        if (player is PlayerInteractor2D playerInteractor)
            playerInteractor.SpeakSituation(PlayerSpeechSituationEnum.InventoryFull);
    }

    private void PlayGetItemSoundIfNeeded()
    {
        InventoryItemKind? kind = item.KindOf();
        if (kind != InventoryItemKind.Weapon && kind != InventoryItemKind.Relic)
            return;

        SoundPlaybackUtility.Play(GetItemSound, causer: gameObject, position: transform.position, sourceObject: this);
    }

    private void RefreshVisual()
    {
        IItemDisplayVisualPresenter presenter = ItemDisplayPresenter;
        if (presenter != null)
        {
            presenter.Apply(item);
            spriteRenderer = presenter.FallbackRenderer;
            return;
        }

        var def = item != null ? item.AsDef() : null;
        Sprite sprite = def != null ? def.Icon : null;

        IWorldDropSpritePresenter dropPresenter = DropSpritePresenter;
        if (dropPresenter != null)
        {
            dropPresenter.Apply(sprite, item is WeaponDefinition);
            spriteRenderer = dropPresenter.Renderer;
            return;
        }

        if (spriteRenderer == null)
            return;

        spriteRenderer.sprite = sprite;
        spriteRenderer.enabled = spriteRenderer.sprite != null;
    }

    private void ResolveVisualRefs()
    {
        if (itemDisplayPresenter is not IItemDisplayVisualPresenter)
            itemDisplayPresenter = FindPresentationComponent<IItemDisplayVisualPresenter>();

        IItemDisplayVisualPresenter presenter = ItemDisplayPresenter;
        if (presenter != null)
            spriteRenderer = presenter.FallbackRenderer;

        if (dropSpritePresenter is not IWorldDropSpritePresenter)
            dropSpritePresenter = FindPresentationComponent<IWorldDropSpritePresenter>();

        IWorldDropSpritePresenter dropPresenter = DropSpritePresenter;
        if (presenter == null && dropPresenter != null)
            spriteRenderer = dropPresenter.Renderer;

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(includeInactive: true);
    }

    private Component FindPresentationComponent<TContract>() where TContract : class
    {
        MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(includeInactive: true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is TContract)
                return behaviours[i];
        }

        return null;
    }
}
