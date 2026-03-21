using UnityEngine;
using UnityGAS;

public sealed class PlayerSceneTransitionFacade : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private WeaponInventory2D weaponInventory;
    [SerializeField] private RelicInventory relicInventory;
    [SerializeField] private AttributeSet attributeSet;

    [Header("Attributes")]
    [SerializeField] private AttributeDefinition healthAttribute;

    private void Awake()
    {
        if (weaponInventory == null) weaponInventory = GetComponent<WeaponInventory2D>();
        if (relicInventory == null) relicInventory = GetComponent<RelicInventory>();
        if (attributeSet == null) attributeSet = GetComponent<AttributeSet>();
    }

    public PlayerRuntimeState CaptureRuntimeState()
    {
        return new PlayerRuntimeState
        {
            weaponInventory = CaptureWeaponInventory(),
            relicInventory = CaptureRelicInventory(),
            attributes = CaptureAttributes()
        };
    }

    public void RestoreRuntimeState(PlayerRuntimeState state)
    {
        if (state == null)
            return;

        RestoreWeaponInventory(state.weaponInventory);
        RestoreRelicInventory(state.relicInventory);
        RestoreAttributes(state.attributes);
    }

    private WeaponInventoryState CaptureWeaponInventory()
    {
        if (weaponInventory == null)
            return null;

        var ids = new string[weaponInventory.SlotCount];
        for (int i = 0; i < weaponInventory.SlotCount; i++)
        {
            var weapon = weaponInventory.GetWeaponInSlot(i);
            ids[i] = weapon != null ? weapon.weaponId : null;
        }

        return new WeaponInventoryState
        {
            slotWeaponIds = ids,
            activeSlotIndex = weaponInventory.ActiveIndex
        };
    }

    private RelicInventoryState CaptureRelicInventory()
    {
        if (relicInventory == null)
            return null;

        var slots = new RelicSlotState[relicInventory.Capacity];
        for (int i = 0; i < relicInventory.Capacity; i++)
        {
            var relic = relicInventory.GetRelicInSlot(i);
            slots[i] = new RelicSlotState
            {
                relicId = relic != null ? relic.relicId : null,
                level = relic != null ? relicInventory.GetRelicLevelInSlot(i) : 0
            };
        }

        return new RelicInventoryState
        {
            slots = slots
        };
    }

    private AttributeRuntimeState CaptureAttributes()
    {
        if (attributeSet == null || healthAttribute == null)
            return null;

        return new AttributeRuntimeState
        {
            currentHealth = attributeSet.GetAttributeValue(healthAttribute)
        };
    }

    private void RestoreWeaponInventory(WeaponInventoryState state)
    {
        if (weaponInventory == null || state == null)
            return;

        for (int i = 0; i < weaponInventory.SlotCount; i++)
            weaponInventory.TrySetWeaponSlot(i, null, autoEquipIfNone: false);

        if (state.slotWeaponIds != null)
        {
            for (int i = 0; i < Mathf.Min(state.slotWeaponIds.Length, weaponInventory.SlotCount); i++)
            {
                var weaponId = state.slotWeaponIds[i];
                if (string.IsNullOrEmpty(weaponId))
                    continue;

                var weaponDef = ItemManager.Instance != null ? ItemManager.Instance.GetWeaponData(weaponId) : null;
                if (weaponDef != null)
                    weaponInventory.TrySetWeaponSlot(i, weaponDef, autoEquipIfNone: false);
            }
        }

        if (state.activeSlotIndex >= 0 && state.activeSlotIndex < weaponInventory.SlotCount)
        {
            if (weaponInventory.GetWeaponInSlot(state.activeSlotIndex) != null)
                weaponInventory.Equip(state.activeSlotIndex);
        }
    }

    private void RestoreRelicInventory(RelicInventoryState state)
    {
        if (relicInventory == null || state == null)
            return;

        for (int i = 0; i < relicInventory.Capacity; i++)
            relicInventory.TrySetRelicSlot(i, null);

        if (state.slots == null)
            return;

        for (int i = 0; i < Mathf.Min(state.slots.Length, relicInventory.Capacity); i++)
        {
            var slot = state.slots[i];
            if (slot == null || string.IsNullOrEmpty(slot.relicId))
                continue;

            var relicDef = ItemManager.Instance != null ? ItemManager.Instance.GetRelicData(slot.relicId) : null;
            if (relicDef != null)
                relicInventory.TrySetRelicSlotWithLevel(i, relicDef, slot.level);
        }
    }

    private void RestoreAttributes(AttributeRuntimeState state)
    {
        if (attributeSet == null || healthAttribute == null || state == null)
            return;

        attributeSet.TrySetBaseValue(healthAttribute, state.currentHealth, this);
    }
}
