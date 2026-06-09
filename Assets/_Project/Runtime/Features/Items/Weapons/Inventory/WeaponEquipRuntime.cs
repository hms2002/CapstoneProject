public readonly struct WeaponEquipChangeResult
{
    public readonly bool Changed;
    public readonly int PreviousIndex;
    public readonly int NewIndex;
    public readonly WeaponDefinition PreviousWeapon;
    public readonly WeaponDefinition NewWeapon;

    public WeaponEquipChangeResult(
        bool changed,
        int previousIndex,
        int newIndex,
        WeaponDefinition previousWeapon,
        WeaponDefinition newWeapon)
    {
        Changed = changed;
        PreviousIndex = previousIndex;
        NewIndex = newIndex;
        PreviousWeapon = previousWeapon;
        NewWeapon = newWeapon;
    }

    public static WeaponEquipChangeResult NoChange(
        int previousIndex,
        int newIndex,
        WeaponDefinition previousWeapon,
        WeaponDefinition newWeapon)
    {
        return new WeaponEquipChangeResult(false, previousIndex, newIndex, previousWeapon, newWeapon);
    }

    public static WeaponEquipChangeResult ChangedResult(
        int previousIndex,
        int newIndex,
        WeaponDefinition previousWeapon,
        WeaponDefinition newWeapon)
    {
        return new WeaponEquipChangeResult(true, previousIndex, newIndex, previousWeapon, newWeapon);
    }
}

public sealed class WeaponEquipRuntime
{
    private readonly WeaponStatBinder statBinder;
    private readonly WeaponPresentationBinder presentationBinder;

    public int ActiveIndex { get; private set; } = -1;
    public WeaponDefinition ActiveWeapon { get; private set; }

    public WeaponEquipRuntime(
        WeaponStatBinder statBinder,
        WeaponPresentationBinder presentationBinder)
    {
        this.statBinder = statBinder;
        this.presentationBinder = presentationBinder;
    }

    public void Initialize(int activeIndex, WeaponDefinition activeWeapon)
    {
        ActiveIndex = activeIndex;
        ActiveWeapon = activeWeapon;
    }

    public WeaponEquipChangeResult Equip(int newIndex, WeaponDefinition newWeapon)
    {
        var prevIndex = ActiveIndex;
        var prevWeapon = ActiveWeapon;

        if (prevIndex == newIndex && prevWeapon == newWeapon)
            return WeaponEquipChangeResult.NoChange(prevIndex, newIndex, prevWeapon, newWeapon);

        if (prevWeapon != null)
        {
            statBinder.Remove(prevWeapon);
            presentationBinder.Remove(prevWeapon);
        }

        ActiveIndex = newWeapon != null ? newIndex : -1;
        ActiveWeapon = newWeapon;

        if (newWeapon != null)
        {
            presentationBinder.Apply(newWeapon);
            statBinder.Apply(newWeapon);
        }

        return WeaponEquipChangeResult.ChangedResult(
            prevIndex,
            ActiveIndex,
            prevWeapon,
            ActiveWeapon);
    }

    public WeaponEquipChangeResult Unequip()
    {
        return Equip(-1, null);
    }
}