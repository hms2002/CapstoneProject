using System.Collections.Generic;
using UnityEngine;

public enum UpgradeEffectKind
{
    Generic,
    Player,
    ItemUnlock,
    RunModifier
}

public abstract class UpgradeEffectSO : ScriptableObject
{
    [Header("Reward UI Display")]
    public string rewardText;
    public Sprite rewardIcon;

    public virtual UpgradeEffectKind EffectKind => UpgradeEffectKind.Generic;

    public virtual void ApplyOnPurchase(PlayerInteractor2D player) { }

    public virtual void ReapplyForPlayer(PlayerInteractor2D player) { }
}

public abstract class PlayerUpgradeEffectSO : UpgradeEffectSO
{
    public sealed override UpgradeEffectKind EffectKind => UpgradeEffectKind.Player;

    public sealed override void ApplyOnPurchase(PlayerInteractor2D player)
    {
        if (player != null)
            ApplyToPlayer(player);
    }

    public sealed override void ReapplyForPlayer(PlayerInteractor2D player)
    {
        if (player != null)
            ApplyToPlayer(player);
    }

    protected abstract void ApplyToPlayer(PlayerInteractor2D player);
}

public abstract class ItemUnlockUpgradeEffectSO : UpgradeEffectSO
{
    public sealed override UpgradeEffectKind EffectKind => UpgradeEffectKind.ItemUnlock;

    public sealed override void ApplyOnPurchase(PlayerInteractor2D player)
    {
        ApplyUnlocks();
    }

    public virtual IReadOnlyList<WeaponDefinition> Weapons => null;
    public virtual IReadOnlyList<RelicDefinition> Relics => null;

    protected abstract void ApplyUnlocks();
}

public abstract class RunModifierUpgradeEffectSO : UpgradeEffectSO
{
    public sealed override UpgradeEffectKind EffectKind => UpgradeEffectKind.RunModifier;

    public sealed override void ApplyOnPurchase(PlayerInteractor2D player)
    {
        ApplyModifier();
    }

    protected abstract void ApplyModifier();
}
