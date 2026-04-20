using System;

/// <summary>
/// 책임 :
/// - WeaponDefinition과 runtime data 타입을 보고 슬롯이 사용할 WeaponRuntimeProcessor 구현체를 생성한다.
/// - 인벤토리/coordinator가 개별 무기 processor 타입 분기를 직접 몰라도 런타임 처리 계층을 구성하게 만든다.
/// </summary>
public static class WeaponRuntimeProcessorFactory
{
    /// <summary>
    /// 책임 :
    /// - 현재 무기 정의가 어떤 runtime processor 타입으로 매핑되는지 editor/validation 계층에 알려준다.
    /// - 비활성 슬롯 시간 경과 규칙이 필요한 loadout이 processor factory에서 빠진 경우를 play 전에 경고하게 한다.
    /// </summary>
    public static Type GetRuntimeProcessorTypeForWeapon(WeaponDefinition weapon)
    {
        if (weapon == null || weapon.abilityLoadout == null)
            return null;

        if (weapon.abilityLoadout is MarkSwordLoadout)
            return typeof(MarkSwordRuntimeProcessor);

        if (weapon.abilityLoadout is SunBladeLoadout)
            return typeof(SunBladeRuntimeProcessor);

        if (weapon.abilityLoadout is MoonBladeLoadout)
            return typeof(MoonBladeRuntimeProcessor);

        if (weapon.abilityLoadout is ExecutionGunLoadout)
            return typeof(ExecutionGunRuntimeProcessor);

        return null;
    }

    public static WeaponRuntimeProcessor CreateForWeapon(WeaponDefinition weapon, WeaponRuntimeData runtimeData)
    {
        if (weapon == null || runtimeData == null)
            return null;

        if (weapon.abilityLoadout is MarkSwordLoadout)
            return new MarkSwordRuntimeProcessor();

        if (weapon.abilityLoadout is SunBladeLoadout)
            return new SunBladeRuntimeProcessor();

        if (weapon.abilityLoadout is MoonBladeLoadout)
            return new MoonBladeRuntimeProcessor();

        if (weapon.abilityLoadout is ExecutionGunLoadout)
            return new ExecutionGunRuntimeProcessor();

        return null;
    }
}
