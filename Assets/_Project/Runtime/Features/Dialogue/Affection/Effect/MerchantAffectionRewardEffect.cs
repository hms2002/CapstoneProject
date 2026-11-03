using UnityEngine;

/// <summary>Authored permanent merchant benefits, rebuilt from saved affection rather than granted once.</summary>
[CreateAssetMenu(fileName = "MerchantAffectionReward", menuName = "Affection/Effect/Merchant Reward")]
public sealed class MerchantAffectionRewardEffect : AffectionEffect
{
    [SerializeField, Range(0, 100)] private int specialDiscountMinPercent;
    [SerializeField, Range(0, 100)] private int specialDiscountMaxPercent;
    [SerializeField, Min(0)] private int goldRetentionLimit;
    [SerializeField, Range(0f, 1f)] private float rareRelicWeightBonus;
    [SerializeField, Range(0f, 1f)] private float epicRelicWeightBonus;

    public ShopRunModifierDelta Delta => new ShopRunModifierDelta
    {
        specialDiscountMinPercent = specialDiscountMinPercent,
        specialDiscountMaxPercent = specialDiscountMaxPercent,
        goldRetentionLimit = goldRetentionLimit,
        rareRelicWeightBonus = rareRelicWeightBonus,
        epicRelicWeightBonus = epicRelicWeightBonus
    };

    public override void Execute() => RunModifierService.Instance?.RebuildFromPurchasedUpgrades();
}
