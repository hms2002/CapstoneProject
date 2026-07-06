using CapstoneAudio;
using UnityEngine;

/// <summary>
/// 책임 : 바닥에 떨어진 무기 정의와 그 무기 인스턴스의 영속 상태를 함께 보관하고,
/// 플레이어 상호작용을 통해 획득을 시도하게 한다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class WeaponDrop2D : InteractableBase
{
    private static readonly SoundRef GetItemSound = SoundRef.FromKey("sound_worldDropItem_GetItem");
    private static readonly int OutlineEnabledID = Shader.PropertyToID("_OutlineEnabled");
    [SerializeField] private WeaponDefinition weapon;
    [SerializeField] private Transform promptAnchor;
    [SerializeField] private string interactPromptText = "획득하기";

    [Header("Runtime Payload")]
    [SerializeField] private WeaponPersistentStatePayload payload;

    [Header("Visual (optional)")]
    [SerializeField] private Component itemDisplayPresenter;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Component dropSpritePresenter;

    private MaterialPropertyBlock outlinePropertyBlock;
    private Collider2D triggerCollider;
    private bool interactionLocked;

    public WeaponDefinition Weapon => weapon;
    public WeaponPersistentStatePayload Payload => payload;
    private IItemDisplayVisualPresenter ItemDisplayPresenter => itemDisplayPresenter as IItemDisplayVisualPresenter;
    private IWorldDropSpritePresenter DropSpritePresenter => dropSpritePresenter as IWorldDropSpritePresenter;

    public void SetWeapon(WeaponDefinition def, WeaponPersistentStatePayload runtimePayload = null)
    {
        weapon = def;
        payload = runtimePayload;
        RefreshVisual();
    }

    public void PlayDrop(Vector3 startPosition, Vector3 landingPosition)
    {
        SetInteractionLocked(true);

        if (WorldItemDropAnimationPlayback.TryPlayDrop(
                gameObject,
                startPosition,
                landingPosition,
                () => SetInteractionLocked(false)))
        {
            return;
        }

        transform.position = landingPosition;
        SetInteractionLocked(false);
    }

    public void SetInteractionLocked(bool locked)
    {
        interactionLocked = locked;

        if (locked)
            OnUnHighlight();

        if (triggerCollider != null)
            triggerCollider.enabled = !locked;
    }

    private void Reset()
    {
        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    private void Awake()
    {
        ResolveVisualRefs();
        RefreshVisual();

        triggerCollider = GetComponent<Collider2D>();
        if (triggerCollider != null)
            triggerCollider.isTrigger = true;

        outlinePropertyBlock = new MaterialPropertyBlock();
        OnUnHighlight();
    }

    private void OnDisable()
    {
        WorldItemHoverPlayback.Hide(GetDetailAnchor());
    }

    public override bool CanInteract(IPlayerInteractor player)
    {
        return weapon != null && !interactionLocked;
    }

    public override void OnPlayerInteract(IPlayerInteractor player)
    {
        if (weapon == null || interactionLocked)
            return;

        var inventory = ResolveWeaponInventory(player);
        if (inventory != null && inventory.TryPickupWeapon(weapon, payload, transform.position))
        {
            SoundPlaybackUtility.Play(GetItemSound, causer: gameObject, position: transform.position, sourceObject: this);
            Destroy(gameObject);
            return;
        }

        if (player is PlayerInteractor2D playerInteractor)
            playerInteractor.SpeakSituation(PlayerSpeechSituationEnum.InventoryFull);
    }

    public override void OnHighlight()
    {
        if (weapon == null || interactionLocked)
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

        WorldItemHoverPlayback.Show(GetDetailAnchor(), weapon);
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

    public override InteractState GetInteractType() => InteractState.Idle;

    public override string GetInteractDescription()
    {
        return weapon != null && !interactionLocked ? interactPromptText : string.Empty;
    }

    public override Transform GetPromptAnchor() => promptAnchor != null ? promptAnchor : transform;

    private Transform GetDetailAnchor() => promptAnchor != null ? promptAnchor : transform;

    private static WeaponInventory2D ResolveWeaponInventory(IPlayerInteractor player)
    {
        if (player is Component component)
            return component.GetComponent<WeaponInventory2D>();

        return null;
    }

    private void RefreshVisual()
    {
        IItemDisplayVisualPresenter presenter = ItemDisplayPresenter;
        if (presenter != null)
        {
            presenter.Apply(weapon);
            spriteRenderer = presenter.FallbackRenderer;
            return;
        }

        Sprite sprite = weapon != null ? weapon.Icon : null;

        IWorldDropSpritePresenter dropPresenter = DropSpritePresenter;
        if (dropPresenter != null)
        {
            dropPresenter.Apply(sprite);
            spriteRenderer = dropPresenter.Renderer;
            return;
        }

        if (spriteRenderer == null)
            return;

        spriteRenderer.sprite = sprite;
        spriteRenderer.enabled = sprite != null;
    }

    /// <summary>
    /// 책임 :
    /// - 무기 드롭의 실제 아이콘 렌더러와 outline 대상 렌더러를 같은 참조로 맞춘다.
    /// - presenter가 없는 기존 프리팹도 자식 SpriteRenderer fallback으로 계속 표시되게 한다.
    /// </summary>
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
