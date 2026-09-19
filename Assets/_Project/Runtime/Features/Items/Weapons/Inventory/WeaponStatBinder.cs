using UnityGAS;

public sealed class WeaponStatBinder
{
    public static void ProjectChange(System.Collections.Generic.Dictionary<AttributeDefinition, AttributeValue> snapshot,
        WeaponDefinition previous, WeaponDefinition next)
    {
        if (snapshot == null || previous == next) return;
        foreach (var value in snapshot.Values)
        {
            if (previous != null) value.RemoveModifiersFromSource(previous);
            if (next != null) value.RemoveModifiersFromSource(next);
        }
        if (next != null && next.statModifiers != null)
            foreach (var modifier in next.statModifiers)
                if (modifier.attribute != null && !modifier.attribute.IsBaseOnly() &&
                    snapshot.TryGetValue(modifier.attribute, out var value))
                    value.AddModifier(new AttributeModifier(modifier.type, modifier.value, next));
        foreach (var value in snapshot.Values) value.ForceRecalculate();
        // Max-linked values must see the recalculated maximum even if catalog order differs.
        foreach (var value in snapshot.Values)
            if (value.MaxValueGetter != null) value.ForceRecalculate();
    }

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
