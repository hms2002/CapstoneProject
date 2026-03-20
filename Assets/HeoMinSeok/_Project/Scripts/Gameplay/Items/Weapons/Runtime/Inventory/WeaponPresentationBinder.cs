using UnityGAS;

public sealed class WeaponPresentationBinder
{
    private readonly TagSystem tagSystem;
    private readonly WeaponEquipController equipController;

    public WeaponPresentationBinder(TagSystem tagSystem, WeaponEquipController equipController)
    {
        this.tagSystem = tagSystem;
        this.equipController = equipController;
    }

    public void Apply(WeaponDefinition weapon)
    {
        if (weapon == null) return;

        if (weapon.equippedTag != null && tagSystem != null)
            tagSystem.AddTag(weapon.equippedTag);

        if (equipController != null)
        {
            if (weapon.weaponPrefab != null)
                equipController.Equip(weapon.weaponPrefab);
            else
                equipController.Clear();
        }
    }

    public void Remove(WeaponDefinition weapon)
    {
        if (weapon != null && weapon.equippedTag != null && tagSystem != null)
            tagSystem.RemoveTag(weapon.equippedTag);

        if (equipController != null)
            equipController.Clear();
    }
}