using UnityEngine;

[CreateAssetMenu(fileName = "ShopRunModifierEffect", menuName = "Upgrade/Effect/Shop Run Modifier")]
public sealed class ShopRunModifierUpgradeEffect : RunModifierUpgradeEffectSO
{
    [Header("Shop Unlock")]
    [SerializeField] private bool shopEnabled;

    [Header("Shop Stock")]
    [SerializeField, Min(0)] private int shopSlotBonus;
    [SerializeField, Min(0)] private int shopRefreshCount;

    [Header("Shop Price")]
    [SerializeField, Range(0f, 1f)] private float discountRate;

    public ShopRunModifierDelta Delta => new ShopRunModifierDelta
    {
        shopEnabled = shopEnabled,
        shopSlotBonus = shopSlotBonus,
        discountRate = discountRate,
        shopRefreshCount = shopRefreshCount
    };

    protected override void ApplyModifier()
    {
        RunModifierService.Instance?.RebuildFromPurchasedUpgrades();
    }
}
