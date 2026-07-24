using UnityEngine;

[CreateAssetMenu(fileName = "RelicInventoryCapacityEffect", menuName = "Upgrade/Effect/Relic Inventory Capacity")]
public sealed class RelicInventoryCapacityUpgradeEffectSO : UpgradeEffectSO
{
    [SerializeField, Min(0)] private int slotBonus = 1;

    public override UpgradeEffectKind EffectKind => UpgradeEffectKind.Player;

    public override void ApplyOnPurchase(PlayerInteractor2D player)
    {
        ApplyCapacityBonus(player);
    }

    public override void ReapplyForPlayer(PlayerInteractor2D player)
    {
        ApplyCapacityBonus(player);
    }

    private void ApplyCapacityBonus(PlayerInteractor2D player)
    {
        if (player == null || slotBonus <= 0)
            return;

        RelicInventory inventory = player.GetComponent<RelicInventory>();
        if (inventory == null)
            return;

        inventory.SetRuntimeCapacityBonus(this, slotBonus);
    }
}
