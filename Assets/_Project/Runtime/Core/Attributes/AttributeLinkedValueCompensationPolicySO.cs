using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 : max Attribute 변화량을 어떤 current Attribute에 보상 적용할지 데이터로 정의한다.
    /// 예를 들어 MaxHealth가 변하면 Health도 같은 delta만큼 움직이는 규칙을 표현한다.
    /// </summary>
    [CreateAssetMenu(
        fileName = "AttributeLinkedValueCompensationPolicy",
        menuName = "GAS/Stats/Linked Value Compensation Policy")]
    public sealed class AttributeLinkedValueCompensationPolicySO : ScriptableObject
    {
        [SerializeField] private AttributeDefinition maxAttribute;
        [SerializeField] private AttributeDefinition currentAttribute;
        [SerializeField] private float minimumCurrentValue = 1f;
        [SerializeField] private bool applyOnPurchase = true;
        [SerializeField] private bool applyOnRelicEquip = true;
        [SerializeField] private bool applyOnRelicUnequip = true;
        [SerializeField] private bool applyOnRelicLevelChange = true;

        public AttributeDefinition MaxAttribute => maxAttribute;
        public AttributeDefinition CurrentAttribute => currentAttribute;
        public float MinimumCurrentValue => minimumCurrentValue;

        public bool Allows(AttributeLinkedValueCompensationContext context)
        {
            return context switch
            {
                AttributeLinkedValueCompensationContext.Purchase => applyOnPurchase,
                AttributeLinkedValueCompensationContext.RelicEquip => applyOnRelicEquip,
                AttributeLinkedValueCompensationContext.RelicUnequip => applyOnRelicUnequip,
                AttributeLinkedValueCompensationContext.RelicLevelChange => applyOnRelicLevelChange,
                _ => false
            };
        }
    }
}
