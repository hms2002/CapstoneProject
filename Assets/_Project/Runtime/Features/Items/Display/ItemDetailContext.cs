using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 아이템 상세 계산에 필요한 현재 플레이어/컨테이너/검사 상태를 전달한다.
/// - 무기, 유물, 소비품 tooltip 생성 로직이 UI 패널 구현을 모르고도 능력치와 출처별 조작 힌트를 계산하게 한다.
/// </summary>
public sealed class ItemDetailContext
{
    public GameObject owner;
    public AbilitySystem abilitySystem;
    public TagSystem tagSystem;
    public GameplayEffectRunner effectRunner;
    public AttributeSet attributeSet;
    public IItemContainer sourceContainer;
    public int sourceIndex = -1;
    public bool IsInspectionOnly;
    public int relicLevelOverride = 0; // 있으면 이 값을 우선 사용

    public bool IsFromChest =>
        sourceContainer != null &&
        ReferenceEquals(sourceContainer, ItemContainerGroupRegistry.Chest);

    public bool IsFromPlayerInventory =>
        sourceContainer != null &&
        (ReferenceEquals(sourceContainer, ItemContainerGroupRegistry.ConsumableEquip) ||
         ReferenceEquals(sourceContainer, ItemContainerGroupRegistry.WeaponEquip) ||
         ReferenceEquals(sourceContainer, ItemContainerGroupRegistry.RelicEquip));

    public bool IsChestUiActive => ItemContainerGroupRegistry.Chest != null;

    /// <summary>
    /// 책임 :
    /// - 상세 패널이 현재 아이템 출처에 맞는 조작 힌트를 표시할 수 있게 한다.
    /// - 매핑 액션의 실제 키는 UI 입력 서비스에서 조회한다.
    /// </summary>
    public ItemDetailActionHint ResolvePrimaryActionHint()
    {
        if (IsInspectionOnly || sourceContainer == null || sourceIndex < 0)
            return ItemDetailActionHint.Hidden;

        if (IsFromChest)
            return ItemDetailActionHint.Show(KeyCode.Mouse0, "인벤토리로 가져오기");

        if (!IsFromPlayerInventory)
            return ItemDetailActionHint.Hidden;

        if (InventoryWeaponRetentionPolicy.WouldRemoveLastPlayerWeapon(sourceContainer, sourceIndex))
            return ItemDetailActionHint.Hidden;

        return IsChestUiActive
            ? ItemDetailActionHint.Hidden
            : ItemDetailActionHint.Show(InputActionId.InventoryDrop, "버리기");
    }

    public static ItemDetailContext FromOwner(GameObject owner)
    {
        var ctx = new ItemDetailContext();
        ctx.owner = owner;
        if (owner != null)
        {
            ctx.abilitySystem = owner.GetComponent<AbilitySystem>();
            ctx.tagSystem = owner.GetComponent<TagSystem>();
            ctx.effectRunner = owner.GetComponent<GameplayEffectRunner>();
            ctx.attributeSet = owner.GetComponent<AttributeSet>();
        }
        return ctx;
    }
}

/// <summary>
/// 책임 :
/// - 아이템 상세 패널 하단에 표시할 조작 힌트 데이터를 전달한다.
/// - 실제 입력 실행 책임과 분리해, UI는 키 글리프와 문구만 렌더링하게 한다.
/// </summary>
public readonly struct ItemDetailActionHint
{
    public static ItemDetailActionHint Hidden => new(false, KeyCode.None, string.Empty);

    public bool Visible { get; }
    public KeyCode Key { get; }
    public InputActionId? Action { get; }
    public string Label { get; }

    private ItemDetailActionHint(bool visible, KeyCode key, string label, InputActionId? action = null)
    {
        Visible = visible;
        Key = key;
        Action = action;
        Label = label ?? string.Empty;
    }

    public static ItemDetailActionHint Show(KeyCode key, string label)
    {
        return new ItemDetailActionHint(key != KeyCode.None, key, label);
    }

    public static ItemDetailActionHint Show(InputActionId action, string label)
    {
        return new ItemDetailActionHint(true, KeyCode.None, label, action);
    }
}
