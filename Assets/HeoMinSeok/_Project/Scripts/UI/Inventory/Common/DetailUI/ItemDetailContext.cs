using UnityEngine;
using UnityGAS;

/// <summary>
/// Detail panel context: allows showing values based on the current player state.
/// (e.g., final attack power after buffs)
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
    /// - 상세 패널이 현재 아이템 출처에 맞는 고정 조작 힌트를 표시할 수 있게 한다.
    /// - 키 설정 대상이 아닌 UI 전용 고정 입력 안내를 컨텍스트 판정과 함께 캡슐화한다.
    /// </summary>
    public ItemDetailActionHint ResolvePrimaryActionHint()
    {
        if (sourceContainer == null || sourceIndex < 0)
            return ItemDetailActionHint.Hidden;

        if (IsFromChest)
            return ItemDetailActionHint.Show(KeyCode.Mouse1, "인벤토리로 가져오기");

        if (!IsFromPlayerInventory)
            return ItemDetailActionHint.Hidden;

        return IsChestUiActive
            ? ItemDetailActionHint.Show(KeyCode.Mouse1, "상자로 옮기기")
            : ItemDetailActionHint.Show(KeyCode.F, "버리기");
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
/// - 아이템 상세 패널 하단에 표시할 고정 조작 힌트 데이터를 전달한다.
/// - 실제 입력 실행 책임과 분리해, UI는 키 글리프와 문구만 렌더링하게 한다.
/// </summary>
public readonly struct ItemDetailActionHint
{
    public static ItemDetailActionHint Hidden => new(false, KeyCode.None, string.Empty);

    public bool Visible { get; }
    public KeyCode Key { get; }
    public string Label { get; }

    private ItemDetailActionHint(bool visible, KeyCode key, string label)
    {
        Visible = visible;
        Key = key;
        Label = label ?? string.Empty;
    }

    public static ItemDetailActionHint Show(KeyCode key, string label)
    {
        return new ItemDetailActionHint(key != KeyCode.None, key, label);
    }
}
