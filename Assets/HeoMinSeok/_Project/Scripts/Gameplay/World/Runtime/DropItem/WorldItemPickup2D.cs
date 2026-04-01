using UnityEngine;

/// <summary>
/// 책임 : 월드에 떨어진 일반 아이템(무기/유물)을 상호작용 대상으로 노출하고,
/// 플레이어가 획득을 시도하면 적절한 장착 인벤토리로 즉시 전달한다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class WorldItemPickup2D : InteractableBase
{
    private static readonly int OutlineEnabledID = Shader.PropertyToID("_OutlineEnabled");

    [SerializeField] private ScriptableObject item;
    [SerializeField] private Transform promptAnchor;
    [SerializeField] private string interactPromptText = "획득하기";

    [Header("Visual (optional)")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    private MaterialPropertyBlock outlinePropertyBlock;

    public ScriptableObject Item => item;
    [SerializeField] private int relicLevel = 0;
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
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        outlinePropertyBlock = new MaterialPropertyBlock();

        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;

        RefreshVisual();
        OnUnHighlight();
    }

    private void OnEnable() => WorldItemRegistry.Register(this);
    private void OnDisable() => WorldItemRegistry.Unregister(this);

    public override bool CanInteract(IPlayerInteractor player)
    {
        return item != null;
    }

    public override void OnHighlight()
    {
        if (spriteRenderer == null || item == null)
            return;

        spriteRenderer.GetPropertyBlock(outlinePropertyBlock);
        outlinePropertyBlock.SetFloat(OutlineEnabledID, 1f);
        spriteRenderer.SetPropertyBlock(outlinePropertyBlock);
    }

    public override void OnUnHighlight()
    {
        if (spriteRenderer == null)
            return;

        spriteRenderer.GetPropertyBlock(outlinePropertyBlock);
        outlinePropertyBlock.SetFloat(OutlineEnabledID, 0f);
        spriteRenderer.SetPropertyBlock(outlinePropertyBlock);
    }

    public override void OnPlayerInteract(IPlayerInteractor player)
    {
        if (item == null)
            return;

        bool pickedUp = item switch
        {
            WeaponDefinition weapon => TryPickupWeapon(player, weapon),
            RelicDefinition relic => TryPickupRelic(player, relic),
            _ => false
        };

        if (pickedUp)
        {
            Destroy(gameObject);
            return;
        }

        SpeakPickupFailed(player);
    }

    public override InteractState GetInteractType() => InteractState.Idle;

    public override string GetInteractDescription()
    {
        return item != null ? interactPromptText : string.Empty;
    }

    public override Transform GetPromptAnchor() => promptAnchor != null ? promptAnchor : transform;

    private bool TryPickupWeapon(IPlayerInteractor player, WeaponDefinition weapon)
    {
        var weaponInventory = ResolveWeaponInventory(player);
        return weaponInventory != null && weaponInventory.TryPickupWeapon(weapon);
    }

    private bool TryPickupRelic(IPlayerInteractor player, RelicDefinition relic)
    {
        var relicInventory = ResolveRelicInventory(player);
        if (relicInventory == null)
            return false;

        int levelOverride = RelicLevel > 0 ? RelicLevel : -1;
        return relicInventory.TryAcquireOrUpgrade(relic, levelOverride);
    }

    private static WeaponInventory2D ResolveWeaponInventory(IPlayerInteractor player)
    {
        if (player is Component component)
            return component.GetComponent<WeaponInventory2D>();

        return null;
    }

    private static RelicInventory ResolveRelicInventory(IPlayerInteractor player)
    {
        if (player is Component component)
            return component.GetComponent<RelicInventory>();

        return null;
    }

    private static void SpeakPickupFailed(IPlayerInteractor player)
    {
        if (player is PlayerInteractor2D playerInteractor)
            playerInteractor.SpeakSituation(PlayerSpeechSituationEnum.InventoryFull);
    }

    private void RefreshVisual()
    {
        if (spriteRenderer == null) return;
        var def = item != null ? item.AsDef() : null;

        // Uses UI icon as a simple world sprite (good enough for prototyping).
        spriteRenderer.sprite = def != null ? def.Icon : null;
        spriteRenderer.enabled = spriteRenderer.sprite != null;
    }
}
