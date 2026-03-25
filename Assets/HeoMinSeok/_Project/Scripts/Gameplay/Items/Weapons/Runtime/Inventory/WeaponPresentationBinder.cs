using UnityGAS;

/// <summary>
/// 책임 : 무기 장착에 따른 프레젠테이션(태그, 비주얼)을 반영한다.
/// 일반 장착에서는 태그와 비주얼을 함께 다루고,
/// 복원 장착에서는 비주얼만 맞추는 보조 API도 제공한다.
/// </summary>
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

        ApplyVisualOnly(weapon);
    }

    public void Remove(WeaponDefinition weapon)
    {
        if (weapon != null && weapon.equippedTag != null && tagSystem != null)
            tagSystem.RemoveTag(weapon.equippedTag);

        ClearVisualOnly();
    }

    /// <summary>
    /// 책임 : 복원용 껍데기 장착에서 무기 비주얼만 동기화한다.
    /// 태그 추가/제거는 수행하지 않는다.
    /// </summary>
    public void ApplyVisualOnly(WeaponDefinition weapon)
    {
        if (equipController == null)
            return;

        if (weapon != null && weapon.weaponPrefab != null)
            equipController.Equip(weapon.weaponPrefab);
        else
            equipController.Clear();
    }

    /// <summary>
    /// 책임 : 복원용 경로에서 현재 무기 비주얼만 정리한다.
    /// 태그 제거는 수행하지 않는다.
    /// </summary>
    public void ClearVisualOnly()
    {
        if (equipController != null)
            equipController.Clear();
    }
}