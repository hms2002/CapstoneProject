using UnityEngine;
using UnityGAS;

[CreateAssetMenu(fileName = "PlayerAttributeUpgradeEffect", menuName = "Upgrade/Effect/Player Attribute")]
public sealed class PlayerAttributeUpgradeEffectSO : UpgradeEffectSO
{
    [Header("Attribute Bonus")]
    [SerializeField] private AttributeDefinition attribute;
    [SerializeField] private ModifierType modifierType = ModifierType.Flat;
    [SerializeField] private float value;
    [SerializeField] private bool useModifierWhenAllowed = true;

    [Header("Purchase Heal")]
    [SerializeField] private AttributeDefinition purchaseHealAttribute;
    [SerializeField] private bool healByPositiveBonusAmount = true;
    [SerializeField] private float purchaseHealAmount;

    public override UpgradeEffectKind EffectKind => UpgradeEffectKind.Player;

    public override void ApplyOnPurchase(PlayerInteractor2D player)
    {
        ApplyAttribute(player, isPurchase: true);
    }

    public override void ReapplyForPlayer(PlayerInteractor2D player)
    {
        ApplyAttribute(player, isPurchase: false);
    }

    private void ApplyAttribute(PlayerInteractor2D player, bool isPurchase)
    {
        if (player == null || attribute == null)
            return;

        AttributeSet attributeSet = player.GetComponent<AttributeSet>();
        if (attributeSet == null)
            return;

        if (useModifierWhenAllowed && attribute.AllowsModifier())
        {
            attributeSet.RemoveModifiersFromSource(this);
            attributeSet.TryAddModifier(attribute, new AttributeModifier(modifierType, value, this));
        }
        else if (isPurchase)
        {
            attributeSet.TryModifyAttributeValue(attribute, value, this);
        }
        else
        {
            Debug.LogWarning(
                $"[PlayerAttributeUpgradeEffectSO] '{attribute.name}' does not allow modifiers; reapply is skipped to avoid duplicate base-value grants.",
                this);
        }

        if (isPurchase)
            ApplyPurchaseHeal(attributeSet);
    }

    private void ApplyPurchaseHeal(AttributeSet attributeSet)
    {
        if (attributeSet == null || purchaseHealAttribute == null)
            return;

        float amount = healByPositiveBonusAmount ? Mathf.Max(0f, value) : purchaseHealAmount;
        if (amount <= 0f)
            return;

        attributeSet.TryModifyAttributeValue(purchaseHealAttribute, amount, this);
    }
}
