using TMPro;
using UnityEngine;

/// <summary>Displays the equipped Odd Iron slot's ammo without owning ammo state.</summary>
[DisallowMultipleComponent]
public sealed class OddIronAmmoWorldHUD : MonoBehaviour
{
    [SerializeField] private WeaponInventory2D inventory;
    [SerializeField] private Canvas worldCanvas;
    [SerializeField] private TMP_Text ammoText;

    private int displayedAmmo = -1;
    private int displayedCapacity = -1;

    private void OnEnable() => Refresh();
    private void LateUpdate() => Refresh();

    private void OnDisable()
    {
        if (worldCanvas != null)
            worldCanvas.enabled = false;
    }

    private void Refresh()
    {
        if (worldCanvas == null || ammoText == null)
            return;

        var ammo = inventory != null && inventory.HasEquippedWeapon
            ? inventory.ActiveRuntimeData as OddIronRuntimeData
            : null;
        worldCanvas.enabled = ammo != null;
        if (ammo == null)
            return;

        // Keep the authored overhead label upright while its parent moves.
        transform.rotation = Quaternion.identity;
        int reloads = 0;
        if (inventory.TryGetComponent<RelicInventory>(out var relics))
            relics.TryGetRelicLevelById(WeaponExclusiveRelics.OddIronMagazine, out reloads);

        // One current magazine plus one full reserve magazine per relic level.
        int capacityWithReserves = ammo.MaxAmmo * (1 + Mathf.Max(0, reloads));
        if (ammo.CurrentAmmo == displayedAmmo && capacityWithReserves == displayedCapacity)
            return;

        displayedAmmo = ammo.CurrentAmmo;
        displayedCapacity = capacityWithReserves;
        ammoText.SetText("{0} / {1}", displayedAmmo, displayedCapacity);
    }
}
