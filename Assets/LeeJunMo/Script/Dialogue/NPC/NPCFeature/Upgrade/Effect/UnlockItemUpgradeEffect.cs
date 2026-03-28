using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Effect_Upgrade_Unlock", menuName = "Game/Upgrade Effects/Unlock Item")]
public class UnlockItemUpgradeEffect : ItemUnlockUpgradeEffectSO
{
    [Header("Unlocked Items")]
    public List<WeaponDefinition> weapons;
    public List<RelicDefinition> relics;

    public override IReadOnlyList<WeaponDefinition> Weapons => weapons;
    public override IReadOnlyList<RelicDefinition> Relics => relics;

    protected override void ApplyUnlocks()
    {
        if (ItemManager.Instance == null)
        {
            Debug.LogError("[UnlockItemUpgradeEffect] ItemManager instance was not found.");
            return;
        }

        if (weapons != null)
        {
            foreach (WeaponDefinition weapon in weapons)
            {
                if (weapon != null)
                    ItemManager.Instance.UnlockWeapon(weapon.weaponId);
            }
        }

        if (relics != null)
        {
            foreach (RelicDefinition relic in relics)
            {
                if (relic != null)
                    ItemManager.Instance.UnlockRelic(relic.relicId);
            }
        }
    }
}
