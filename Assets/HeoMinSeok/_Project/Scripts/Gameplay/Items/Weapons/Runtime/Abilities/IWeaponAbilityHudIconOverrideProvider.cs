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

/// <summary>
/// HUD-only active duration projection for weapon abilities whose execution
/// time is separate from the ability cooldown timer.
/// </summary>
public readonly struct WeaponAbilityHudDurationOverride
{
    public readonly float RemainingSeconds;
    public readonly float MaxSeconds;
    public readonly bool FillBottomToTop;
    public readonly bool ShowText;

    public WeaponAbilityHudDurationOverride(
        float remainingSeconds,
        float maxSeconds,
        bool fillBottomToTop,
        bool showText)
    {
        RemainingSeconds = Mathf.Max(0f, remainingSeconds);
        MaxSeconds = Mathf.Max(0.0001f, maxSeconds);
        FillBottomToTop = fillBottomToTop;
        ShowText = showText;
    }
}

public interface IWeaponAbilityHudDurationOverrideProvider
{
    bool TryGetHudDurationOverride(
        WeaponAbilitySlot slot,
        AbilityDefinition ability,
        out WeaponAbilityHudDurationOverride duration);
}
