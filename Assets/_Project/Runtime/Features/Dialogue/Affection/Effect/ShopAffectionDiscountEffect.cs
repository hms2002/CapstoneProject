using UnityEngine;

[CreateAssetMenu(fileName = "ShopAffectionDiscountEffect", menuName = "Affection/Effect/Shop Discount")]
public sealed class ShopAffectionDiscountEffect : AffectionEffect
{
    [SerializeField, Range(0f, 1f)] private float discountRate = 0.2f;

    public ShopRunModifierDelta Delta => new ShopRunModifierDelta
    {
        affectionDiscountRate = discountRate
    };

    public override void Execute()
    {
        RunModifierService.Instance?.RebuildFromPurchasedUpgrades();
    }
}
