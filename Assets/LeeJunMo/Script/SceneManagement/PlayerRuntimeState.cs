using System;

[Serializable]
public sealed class PlayerRuntimeState
{
    public WeaponInventoryState weaponInventory;
    public RelicInventoryState relicInventory;
    public AttributeRuntimeState attributes;
}

[Serializable]
public sealed class WeaponInventoryState
{
    public string[] slotWeaponIds;
    public int activeSlotIndex;
}

[Serializable]
public sealed class RelicInventoryState
{
    public RelicSlotState[] slots;
}

[Serializable]
public sealed class RelicSlotState
{
    public string relicId;
    public int level;
}

[Serializable]
public sealed class AttributeRuntimeState
{
    public float currentHealth;
}
