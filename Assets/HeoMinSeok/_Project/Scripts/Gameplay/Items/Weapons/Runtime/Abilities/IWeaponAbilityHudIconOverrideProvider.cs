using UnityEngine;
using UnityGAS;

/// <summary>
/// Lets an equipped weapon runtime state project a temporary HUD-only icon
/// without changing the AbilityDefinition icon used by inventory/detail UI.
/// </summary>
public interface IWeaponAbilityHudIconOverrideProvider
{
    bool TryGetHudIconOverride(WeaponAbilitySlot slot, AbilityDefinition ability, out Sprite icon);
}
