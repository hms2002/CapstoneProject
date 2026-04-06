using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 인벤토리에서 사용하는 1회용 아이템의 정체성과 사용 효과 데이터를 정의한다.
/// - 현재는 체력 회복형 consumable 하나를 우선 지원한다.
/// </summary>
[CreateAssetMenu(fileName = "CD_NewConsumable", menuName = "Game/Consumable Definition")]
public class ConsumableDefinition : ScriptableObject, IInventoryItemDefinition
{
    [Header("Identity")]
    public string consumableId = "Consumable.New";
    public string displayName = "New Consumable";
    public Sprite icon;

    [TextArea] public string description;

    [Header("Use Effect")]
    [SerializeField] private AttributeDefinition targetAttribute;
    [SerializeField] private int restoreAmount = 1;

    public InventoryItemKind Kind => InventoryItemKind.Consumable;
    public string ItemId => consumableId;
    public string DisplayName => displayName;
    public Sprite Icon => icon;
    public AttributeDefinition TargetAttribute => targetAttribute;
    public int RestoreAmount => restoreAmount;

    public bool TryUse(GameObject owner)
    {
        if (owner == null || targetAttribute == null || restoreAmount <= 0)
            return false;

        var attributeSet = owner.GetComponent<AttributeSet>();
        if (attributeSet == null)
            return false;

        float before = attributeSet.GetCurrentValue(targetAttribute);
        if (!attributeSet.TryModifyAttributeValue(targetAttribute, restoreAmount, this))
            return false;

        float after = attributeSet.GetCurrentValue(targetAttribute);
        return after > before;
    }
}
