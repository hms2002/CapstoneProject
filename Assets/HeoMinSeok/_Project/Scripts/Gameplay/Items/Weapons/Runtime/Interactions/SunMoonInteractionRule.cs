using UnityEngine;

/// <summary>
/// 책임 :
/// - 태양도와 월영도 사이의 공명 피니시 규칙을 하나의 전투 문법으로 해석한다.
/// - runtime state가 반대 슬롯 data를 직접 수정하지 않도록 coordinator를 통해 양쪽 persistent state 소비만 요청한다.
/// </summary>
public sealed class SunMoonInteractionRule : WeaponPairInteractionRule
{
    public override bool SupportsPair(WeaponDefinition sourceWeapon, WeaponDefinition otherWeapon)
    {
        if (sourceWeapon == null || otherWeapon == null)
            return false;

        return (sourceWeapon.abilityLoadout is SunBladeLoadout && otherWeapon.abilityLoadout is MoonBladeLoadout)
            || (sourceWeapon.abilityLoadout is MoonBladeLoadout && otherWeapon.abilityLoadout is SunBladeLoadout);
    }

    public override bool TryHandleAbilityActivated(
        in WeaponInteractionContext context,
        WeaponRuntimeCoordinator coordinator)
    {
        if (context.SourceWeapon == null || context.ActivatedAbility == null || context.OtherWeapon == null)
            return false;

        if (context.SourceWeapon.abilityLoadout is SunBladeLoadout sunLoadout &&
            context.OtherWeapon.abilityLoadout is MoonBladeLoadout &&
            context.ActivatedAbility == sunLoadout.SolarFinishStarter &&
            context.SourceRuntimeData is SunBladeRuntimeData sunData &&
            context.OtherRuntimeData is MoonBladeRuntimeData moonData)
        {
            if (sunData.HeatStacks < sunLoadout.RequiredHeatForSolarFinish ||
                moonData.ColdStacks < sunLoadout.RequiredMoonColdForSolarFinish)
                return false;

            int consumedHeat = sunData.HeatStacks;
            int consumedCold = moonData.ColdStacks;
            coordinator.TryMutateRuntimeData<SunBladeRuntimeData>(context.SourceSlotIndex, static data => data.ClearHeat());
            coordinator.TryMutateRuntimeData<MoonBladeRuntimeData>(context.OtherSlotIndex, static data => data.ClearCold());
            Debug.Log($"[SunMoonInteractionRule] Solar finish consumed stacks: sun={consumedHeat}, moon={consumedCold}");
            return true;
        }

        if (context.SourceWeapon.abilityLoadout is MoonBladeLoadout moonLoadout &&
            context.OtherWeapon.abilityLoadout is SunBladeLoadout &&
            context.ActivatedAbility == moonLoadout.LunarFinishStarter &&
            context.SourceRuntimeData is MoonBladeRuntimeData sourceMoonData &&
            context.OtherRuntimeData is SunBladeRuntimeData otherSunData)
        {
            if (sourceMoonData.ColdStacks < moonLoadout.RequiredColdForLunarFinish ||
                otherSunData.HeatStacks < moonLoadout.RequiredSunHeatForLunarFinish)
                return false;

            int consumedCold = sourceMoonData.ColdStacks;
            int consumedHeat = otherSunData.HeatStacks;
            coordinator.TryMutateRuntimeData<MoonBladeRuntimeData>(context.SourceSlotIndex, static data => data.ClearCold());
            coordinator.TryMutateRuntimeData<SunBladeRuntimeData>(context.OtherSlotIndex, static data => data.ClearHeat());
            Debug.Log($"[SunMoonInteractionRule] Lunar finish consumed stacks: moon={consumedCold}, sun={consumedHeat}");
            return true;
        }

        return false;
    }
}
