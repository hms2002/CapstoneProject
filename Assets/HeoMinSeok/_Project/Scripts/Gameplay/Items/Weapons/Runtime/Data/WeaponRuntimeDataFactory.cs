using System;

/// <summary>
/// 책임 :
/// - WeaponDefinition과 loadout 타입을 보고 슬롯이 소유할 WeaponRuntimeData 구현체를 생성한다.
/// - 무기별 지속 상태 생성 규칙을 인벤토리 밖으로 분리해 slot data 초기화 책임을 한 곳에 모은다.
/// </summary>
public static class WeaponRuntimeDataFactory
{
    /// <summary>
    /// 책임 :
    /// - 현재 무기 정의가 어떤 runtime data 타입으로 매핑되는지 editor/validation 계층에 알려준다.
    /// - 전용 persistent state를 요구하는 loadout이 factory coverage에서 빠진 경우를 실제 생성 전에 감지하게 한다.
    /// </summary>
    public static Type GetRuntimeDataTypeForWeapon(WeaponDefinition weapon)
    {
        if (weapon == null)
            return null;

        if (weapon.abilityLoadout is MarkSwordLoadout)
            return typeof(MarkSwordRuntimeData);

        if (weapon.abilityLoadout is SunBladeLoadout)
            return typeof(SunBladeRuntimeData);

        if (weapon.abilityLoadout is MoonBladeLoadout)
            return typeof(MoonBladeRuntimeData);

        if (weapon.abilityLoadout is ExecutionGunLoadout)
            return typeof(ExecutionGunRuntimeData);

        if (weapon.abilityLoadout is EclipseSwordLoadout)
            return typeof(EclipseSwordRuntimeData);

        return typeof(WeaponRuntimeData);
    }

    public static WeaponRuntimeData CreateForWeapon(WeaponDefinition weapon)
    {
        if (weapon == null)
            return null;

        if (weapon.abilityLoadout is MarkSwordLoadout markSwordLoadout)
        {
            var data = new MarkSwordRuntimeData();
            data.ApplyDefaults(markSwordLoadout);
            return data;
        }

        if (weapon.abilityLoadout is SunBladeLoadout sunBladeLoadout)
        {
            var data = new SunBladeRuntimeData();
            data.ApplyDefaults(sunBladeLoadout);
            return data;
        }

        if (weapon.abilityLoadout is MoonBladeLoadout moonBladeLoadout)
        {
            var data = new MoonBladeRuntimeData();
            data.ApplyDefaults(moonBladeLoadout);
            return data;
        }

        if (weapon.abilityLoadout is ExecutionGunLoadout executionGunLoadout)
        {
            var data = new ExecutionGunRuntimeData();
            data.ApplyDefaults(executionGunLoadout);
            return data;
        }

        if (weapon.abilityLoadout is EclipseSwordLoadout eclipseLoadout)
        {
            var data = new EclipseSwordRuntimeData();
            data.ApplyDefaults(eclipseLoadout);
            return data;
        }

        return new WeaponRuntimeData();
    }
}
