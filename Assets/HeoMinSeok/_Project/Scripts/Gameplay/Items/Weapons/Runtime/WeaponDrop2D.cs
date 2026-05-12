using UnityEngine;

/// <summary>
/// 책임 : 바닥에 떨어진 무기 정의와 그 무기 인스턴스의 영속 상태를 함께 보관하고,
/// 플레이어 상호작용을 통해 획득을 시도하게 한다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class WeaponDrop2D : InteractableBase
{
    private static readonly int OutlineEnabledID = Shader.PropertyToID("_OutlineEnabled");
    [SerializeField] private WeaponDefinition weapon;
    [SerializeField] private Transform promptAnchor;
    [SerializeField] private string interactPromptText = "획득하기";

    [Header("Runtime Payload")]
    [SerializeField] private WeaponPersistentStatePayload payload;

    [Header("Visual (optional)")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private WorldDropSpritePresenter2D dropSpritePresenter;

    private MaterialPropertyBlock outlinePropertyBlock;

    public WeaponDefinition Weapon => weapon;
    public WeaponPersistentStatePayload Payload => payload;

    public void SetWeapon(WeaponDefinition def, WeaponPersistentStatePayload runtimePayload = null)
    {
        weapon = def;
        payload = runtimePayload;
        RefreshVisual();
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

        outlinePropertyBlock = new MaterialPropertyBlock();
        OnUnHighlight();
    }

    private void OnDisable()
    {
        WorldItemDetailPresenter.Instance?.Hide(GetDetailAnchor());
    }

    public override bool CanInteract(IPlayerInteractor player)
    {
        return weapon != null;
    }

    public override void OnPlayerInteract(IPlayerInteractor player)
    {
        if (weapon == null)
            return;

        var inventory = ResolveWeaponInventory(player);
        if (inventory != null && inventory.TryPickupWeapon(weapon, payload))
        {
            Destroy(gameObject);
            return;
        }

        if (player is PlayerInteractor2D playerInteractor)
            playerInteractor.SpeakSituation(PlayerSpeechSituationEnum.InventoryFull);
    }

    public override void OnHighlight()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.GetPropertyBlock(outlinePropertyBlock);
            outlinePropertyBlock.SetFloat(OutlineEnabledID, 1f);
            spriteRenderer.SetPropertyBlock(outlinePropertyBlock);
        }

        WorldItemDetailPresenter.Instance?.Show(GetDetailAnchor(), weapon);
    }

    public override void OnUnHighlight()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.GetPropertyBlock(outlinePropertyBlock);
            outlinePropertyBlock.SetFloat(OutlineEnabledID, 0f);
            spriteRenderer.SetPropertyBlock(outlinePropertyBlock);
        }

        WorldItemDetailPresenter.Instance?.Hide(GetDetailAnchor());
    }

    public override InteractState GetInteractType() => InteractState.Idle;

    public override string GetInteractDescription()
    {
        return weapon != null ? interactPromptText : string.Empty;
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
        Sprite sprite = weapon != null ? weapon.Icon : null;

        if (dropSpritePresenter != null)
        {
            dropSpritePresenter.Apply(sprite);
            spriteRenderer = dropSpritePresenter.Renderer;
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
        if (dropSpritePresenter == null)
            dropSpritePresenter = GetComponentInChildren<WorldDropSpritePresenter2D>(includeInactive: true);

        if (dropSpritePresenter != null)
            spriteRenderer = dropSpritePresenter.Renderer;

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(includeInactive: true);
    }
}
