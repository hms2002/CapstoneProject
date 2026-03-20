using System;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

public class WeaponInventory2D : MonoBehaviour
{
    // -----------------------
    // Events (UI/HUD Friendly)
    // -----------------------
    public event Action<int, WeaponDefinition, WeaponDefinition> OnSlotChanged; // (slotIndex, prev, now)
    public event Action<int, int, WeaponDefinition, WeaponDefinition> OnEquippedChanged; // (prevIdx, newIdx, prevW, newW)
    public event Action OnInventoryChanged;
    public event Action<WeaponDefinition> OnPickupRejected_Duplicate;

    // -----------------------
    // Refs
    // -----------------------
    [Header("Refs")]
    [SerializeField] private AbilitySystem abilitySystem;
    [SerializeField] private TagSystem tagSystem;
    [SerializeField] private WeaponEquipController equipController;
    [SerializeField] private AttributeSet attributeSet;

    // -----------------------
    // Slots
    // -----------------------
    [Header("Slots (2)")]
    [SerializeField] private WeaponDefinition[] slots = new WeaponDefinition[2];

    [Tooltip("장착 중인 슬롯 인덱스. 장착 없음이면 -1")]
    [SerializeField] private int activeIndex = -1;

    [Header("Drop")]
    [SerializeField] private WeaponDrop2D dropPrefab;

    [Header("Policy")]
    [SerializeField] private bool disallowDuplicateWeapons = true;

    // -----------------------
    // Runtime helpers
    // -----------------------
    private WeaponAbilityOwnershipBinder abilityBinder;
    private WeaponStatBinder statBinder;
    private WeaponPresentationBinder presentationBinder;
    private WeaponEquipRuntime equipRuntime;

    // -----------------------
    // Public getters
    // -----------------------
    public int ActiveIndex => activeIndex;
    public WeaponDefinition ActiveWeapon => IsValidSlot(activeIndex) ? slots[activeIndex] : null;
    public bool HasEquippedWeapon => activeIndex >= 0 && ActiveWeapon != null;
    public int SlotCount => slots.Length;

    public WeaponDefinition GetWeaponInSlot(int slotIndex)
        => IsValidSlot(slotIndex) ? slots[slotIndex] : null;

    public bool HasWeapon(int slotIndex)
        => GetWeaponInSlot(slotIndex) != null;

    private void Awake()
    {
        if (abilitySystem == null) abilitySystem = GetComponent<AbilitySystem>();
        if (tagSystem == null) tagSystem = GetComponent<TagSystem>();
        if (equipController == null) equipController = GetComponentInChildren<WeaponEquipController>();
        if (attributeSet == null) attributeSet = GetComponent<AttributeSet>();

        abilityBinder = new WeaponAbilityOwnershipBinder(abilitySystem);
        statBinder = new WeaponStatBinder(attributeSet);
        presentationBinder = new WeaponPresentationBinder(tagSystem, equipController);
        equipRuntime = new WeaponEquipRuntime(statBinder, presentationBinder);

        RebuildAbilityOwnershipState();
        equipRuntime.Initialize(activeIndex, ActiveWeapon);
    }

    private void Start()
    {
        if (HasEquippedWeapon)
        {
            var result = equipRuntime.Equip(activeIndex, ActiveWeapon);
            SyncActiveStateFromRuntime();

            if (result.Changed)
                OnEquippedChanged?.Invoke(result.PreviousIndex, result.NewIndex, result.PreviousWeapon, result.NewWeapon);
        }
    }

    // -----------------------
    // Public API
    // -----------------------
    public bool TryPickupWeapon(WeaponDefinition weapon)
    {
        if (weapon == null) return false;

        if (disallowDuplicateWeapons && ContainsWeaponId(weapon.weaponId))
        {
            OnPickupRejected_Duplicate?.Invoke(weapon);
            return false;
        }

        int targetIndex = FindEmptySlot();
        bool replaced = false;
        bool replacedWasActive = false;

        if (targetIndex < 0)
        {
            replaced = true;

            int other = (activeIndex == 0) ? 1 : 0;
            targetIndex = (slots[other] != null) ? other : Mathf.Clamp(activeIndex, 0, slots.Length - 1);
            replacedWasActive = (targetIndex == activeIndex);

            DropSlot(targetIndex);
        }

        SetSlot(targetIndex, weapon);
        abilityBinder.OnWeaponAdded(weapon);

        if (!HasEquippedWeapon)
        {
            Equip(targetIndex);
            NotifyInventoryChanged();
            return true;
        }

        if (replaced && replacedWasActive)
        {
            Equip(targetIndex);
            NotifyInventoryChanged();
            return true;
        }

        NotifyInventoryChanged();
        return true;
    }

    public void Equip(int slotIndex)
    {
        if (!IsValidSlot(slotIndex)) return;

        var newWeapon = slots[slotIndex];
        if (newWeapon == null) return;

        var result = equipRuntime.Equip(slotIndex, newWeapon);
        if (!result.Changed) return;

        SyncActiveStateFromRuntime();
        OnEquippedChanged?.Invoke(result.PreviousIndex, result.NewIndex, result.PreviousWeapon, result.NewWeapon);
        NotifyInventoryChanged();
    }

    public void Unequip()
    {
        if (!HasEquippedWeapon) return;

        var result = equipRuntime.Unequip();
        if (!result.Changed) return;

        SyncActiveStateFromRuntime();
        OnEquippedChanged?.Invoke(result.PreviousIndex, result.NewIndex, result.PreviousWeapon, result.NewWeapon);
        NotifyInventoryChanged();
    }

    public void Swap()
    {
        if (!HasEquippedWeapon)
        {
            int first = FindFirstFilledSlot();
            if (first >= 0) Equip(first);
            NotifyInventoryChanged();
            return;
        }

        int other = 1 - activeIndex;
        if (!IsValidSlot(other) || slots[other] == null) return;

        Equip(other);
        NotifyInventoryChanged();
    }

    public void DropActive()
    {
        if (!HasEquippedWeapon) return;

        int droppingIndex = activeIndex;
        DropSlot(droppingIndex);

        int other = 1 - droppingIndex;
        if (IsValidSlot(other) && slots[other] != null)
            Equip(other);

        NotifyInventoryChanged();
    }

    public AbilityDefinition GetActiveAbility(WeaponAbilitySlot slot)
        => ActiveWeapon != null ? ActiveWeapon.GetAbility(slot) : null;

    /// <summary>
    /// 슬롯에 특정 무기를 놓을 수 있는지(중복 무기 금지 정책 포함)
    /// </summary>
    public bool CanPlaceWeaponInSlot(int slotIndex, WeaponDefinition weapon)
    {
        if (!IsValidSlot(slotIndex)) return false;
        if (weapon == null) return true;

        if (disallowDuplicateWeapons)
        {
            if (ContainsWeaponIdExcept(weapon.weaponId, slotIndex))
                return false;
        }

        return true;
    }

    /// <summary>
    /// 슬롯에 무기를 직접 세팅(교체/제거 포함). 드래그&드롭에서 사용.
    /// </summary>
    public bool TrySetWeaponSlot(int slotIndex, WeaponDefinition newWeapon, bool autoEquipIfNone = true)
    {
        if (!IsValidSlot(slotIndex)) return false;

        var oldWeapon = slots[slotIndex];
        if (oldWeapon == newWeapon) return true;

        if (newWeapon != null && !CanPlaceWeaponInSlot(slotIndex, newWeapon))
            return false;

        bool wasActive = (slotIndex == activeIndex);

        if (wasActive)
        {
            var unequipResult = equipRuntime.Unequip();
            if (unequipResult.Changed)
            {
                SyncActiveStateFromRuntime();
                OnEquippedChanged?.Invoke(
                    unequipResult.PreviousIndex,
                    unequipResult.NewIndex,
                    unequipResult.PreviousWeapon,
                    unequipResult.NewWeapon);
            }
        }

        if (oldWeapon != null)
            abilityBinder.OnWeaponRemoved(oldWeapon);

        SetSlot(slotIndex, newWeapon);

        if (newWeapon != null)
            abilityBinder.OnWeaponAdded(newWeapon);

        if (wasActive && newWeapon != null)
        {
            Equip(slotIndex);
        }
        else if (autoEquipIfNone && activeIndex < 0 && newWeapon != null)
        {
            Equip(slotIndex);
        }

        NotifyInventoryChanged();
        return true;
    }

    /// <summary>
    /// 인벤토리 슬롯 간 swap.
    /// 현재 장착 무기가 유지되도록 activeIndex를 같이 이동시킨다.
    /// </summary>
    public bool TrySwapWeaponSlots(int a, int b)
    {
        if (!IsValidSlot(a) || !IsValidSlot(b)) return false;
        if (a == b) return true;

        var wa = slots[a];
        var wb = slots[b];

        slots[a] = wb;
        slots[b] = wa;

        OnSlotChanged?.Invoke(a, wa, slots[a]);
        OnSlotChanged?.Invoke(b, wb, slots[b]);

        int prevIndex = activeIndex;

        if (activeIndex == a) activeIndex = b;
        else if (activeIndex == b) activeIndex = a;

        equipRuntime.Initialize(activeIndex, ActiveWeapon);

        if (prevIndex != activeIndex && activeIndex >= 0)
        {
            var equipped = ActiveWeapon;
            OnEquippedChanged?.Invoke(prevIndex, activeIndex, equipped, equipped);
        }

        NotifyInventoryChanged();
        return true;
    }

    /// <summary>
    /// 현재 인벤토리(슬롯)에 존재하는 모든 무기의 ID 리스트를 반환함.
    /// </summary>
    public List<string> GetAllWeaponIDs()
    {
        List<string> ids = new List<string>();

        foreach (var weapon in slots)
        {
            if (weapon != null && !string.IsNullOrEmpty(weapon.weaponId))
                ids.Add(weapon.weaponId);
        }

        return ids;
    }

    // -----------------------
    // Internal helpers
    // -----------------------
    private bool IsValidSlot(int i) => i >= 0 && i < slots.Length;

    private int FindEmptySlot()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null)
                return i;
        }
        return -1;
    }

    private int FindFirstFilledSlot()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null)
                return i;
        }
        return -1;
    }

    private bool ContainsWeaponId(string weaponId)
    {
        if (string.IsNullOrEmpty(weaponId)) return false;

        for (int i = 0; i < slots.Length; i++)
        {
            var w = slots[i];
            if (w != null && w.weaponId == weaponId)
                return true;
        }

        return false;
    }

    private bool ContainsWeaponIdExcept(string weaponId, int exceptSlotIndex)
    {
        if (string.IsNullOrEmpty(weaponId)) return false;

        for (int i = 0; i < slots.Length; i++)
        {
            if (i == exceptSlotIndex) continue;

            var w = slots[i];
            if (w != null && w.weaponId == weaponId)
                return true;
        }

        return false;
    }

    private void SetSlot(int slotIndex, WeaponDefinition newWeapon)
    {
        if (!IsValidSlot(slotIndex)) return;

        var prev = slots[slotIndex];
        if (prev == newWeapon) return;

        slots[slotIndex] = newWeapon;
        OnSlotChanged?.Invoke(slotIndex, prev, newWeapon);
    }

    private void ClearSlot(int slotIndex)
    {
        if (!IsValidSlot(slotIndex)) return;
        SetSlot(slotIndex, null);
    }

    private void DropSlot(int slotIndex)
    {
        if (!IsValidSlot(slotIndex)) return;

        var weapon = slots[slotIndex];
        if (weapon == null) return;

        bool wasActive = (slotIndex == activeIndex);

        if (wasActive)
        {
            var unequipResult = equipRuntime.Unequip();
            if (unequipResult.Changed)
            {
                SyncActiveStateFromRuntime();
                OnEquippedChanged?.Invoke(
                    unequipResult.PreviousIndex,
                    unequipResult.NewIndex,
                    unequipResult.PreviousWeapon,
                    unequipResult.NewWeapon);
            }
        }

        abilityBinder.OnWeaponRemoved(weapon);

        if (dropPrefab != null)
        {
            var drop = Instantiate(dropPrefab, transform.position, Quaternion.identity);
            drop.SetWeapon(weapon);
        }

        ClearSlot(slotIndex);
    }

    private void NotifyInventoryChanged()
    {
        OnInventoryChanged?.Invoke();
    }

    private void SyncActiveStateFromRuntime()
    {
        activeIndex = equipRuntime.ActiveIndex;
    }

    private void RebuildAbilityOwnershipState()
    {
        if (abilityBinder == null) return;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null)
                abilityBinder.OnWeaponAdded(slots[i]);
        }
    }
}