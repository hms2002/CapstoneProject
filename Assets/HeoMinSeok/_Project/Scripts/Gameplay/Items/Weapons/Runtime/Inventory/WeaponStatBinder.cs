using UnityGAS;

public sealed class WeaponStatBinder
{
    private readonly AttributeSet attributeSet;

    public WeaponStatBinder(AttributeSet attributeSet)
    {
        this.attributeSet = attributeSet;
    }

    public void Apply(WeaponDefinition weapon)
    {
        if (attributeSet == null || weapon == null) return;

        attributeSet.RemoveModifiersFromSource(weapon);

        var list = weapon.statModifiers;
        if (list == null) return;

        for (int i = 0; i < list.Count; i++)
        {
            var e = list[i];
            if (e.attribute == null) continue;

            var mod = new AttributeModifier(e.type, e.value, weapon, 0f);
            attributeSet.TryAddModifier(e.attribute, mod);
        }
    }

    public void Remove(WeaponDefinition weapon)
    {
        if (attributeSet == null || weapon == null) return;
        attributeSet.RemoveModifiersFromSource(weapon);
    }
}