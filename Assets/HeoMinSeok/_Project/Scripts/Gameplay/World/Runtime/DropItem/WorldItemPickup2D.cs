using UnityEngine;

/// <summary>
/// 책임 : 월드에 떨어진 일반 아이템(무기/유물/1회용 아이템)을 상호작용 대상으로 노출하고,
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
    [SerializeField] private ItemDisplayVisualPresenter2D itemDisplayPresenter;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private WorldDropSpritePresenter2D dropSpritePresenter;
    private MaterialPropertyBlock outlinePropertyBlock;
    private Collider2D triggerCollider;

    public ScriptableObject Item => item;
    [SerializeField] private bool interactionLocked;
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

        bool pickedUp = item switch
        {
            WeaponDefinition weapon => TryPickupWeapon(player, weapon),
            RelicDefinition relic => TryPickupRelic(player, relic),
            ConsumableDefinition consumable => TryPickupConsumable(player, consumable),
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
        RelicInventory.AcquireResult result = relicInventory.TryAcquireOrUpgradeDetailed(relic, levelOverride);
        if (result == RelicInventory.AcquireResult.Success)
            return true;

        ShowRelicPickupWarning(result);
        return false;
    }

    private bool TryPickupConsumable(IPlayerInteractor player, ConsumableDefinition consumable)
    {
        var consumableInventory = ResolveConsumableInventory(player);
        if (consumableInventory == null)
            return false;

        PlayerConsumableInventory.AcquireResult result = consumableInventory.TryAcquireDetailed(consumable);
        if (result == PlayerConsumableInventory.AcquireResult.Success)
            return true;

        ShowConsumablePickupWarning(result);
        return false;
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

    private static PlayerConsumableInventory ResolveConsumableInventory(IPlayerInteractor player)
    {
        if (player is Component component)
            return PlayerConsumableInventory.GetOrAdd(component.transform);

        return null;
    }

    private static void SpeakPickupFailed(IPlayerInteractor player)
    {
        if (player is PlayerInteractor2D playerInteractor)
            playerInteractor.SpeakSituation(PlayerSpeechSituationEnum.InventoryFull);
    }

    /// <summary>
    /// 책임 :
    /// - 월드 유물 픽업 실패 사유를 UIManager 경고 팝업 코드로 변환해 전달한다.
    /// - 픽업 도메인 로직과 실제 경고 문구/표시 방식의 결합을 줄인다.
    /// </summary>
    private static void ShowRelicPickupWarning(RelicInventory.AcquireResult result)
    {
        WarningPopupCode code = result switch
        {
            RelicInventory.AcquireResult.InventoryFull => WarningPopupCode.RelicInventoryFull,
            RelicInventory.AcquireResult.AlreadyMaxLevel => WarningPopupCode.RelicAlreadyMaxLevel,
            _ => WarningPopupCode.None
        };

        if (code != WarningPopupCode.None)
            UIManager.Instance?.ShowWarning(code);
    }

    /// <summary>
    /// 책임 :
    /// - 월드 1회용 아이템 픽업 실패 사유를 UIManager 경고 팝업 코드로 변환해 전달한다.
    /// - 1회용 아이템 인벤토리 부족을 조용한 실패가 아니라 즉시 읽히는 피드백으로 바꾼다.
    /// </summary>
    private static void ShowConsumablePickupWarning(PlayerConsumableInventory.AcquireResult result)
    {
        WarningPopupCode code = result switch
        {
            PlayerConsumableInventory.AcquireResult.InventoryFull => WarningPopupCode.ConsumableInventoryFull,
            _ => WarningPopupCode.None
        };

        if (code != WarningPopupCode.None)
            UIManager.Instance?.ShowWarning(code);
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

        // Uses UI icon as a simple world sprite (good enough for prototyping).
        if (spriteRenderer == null) return;
        spriteRenderer.sprite = sprite;
        spriteRenderer.enabled = spriteRenderer.sprite != null;
    }

    /// <summary>
    /// 책임 :
    /// - 월드 드롭 표시와 outline 처리가 같은 SpriteRenderer를 바라보도록 presenter/renderer 참조를 동기화한다.
    /// - 구형 프리팹처럼 presenter가 없는 경우에도 기존 자식 SpriteRenderer fallback을 유지한다.
    /// </summary>
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
