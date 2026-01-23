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

    // -----------------------
    // Refs
    // -----------------------
    [Header("Refs")]
    [SerializeField] private AbilitySystem abilitySystem;
    [SerializeField] private TagSystem tagSystem;
    [SerializeField] private WeaponEquipController equipController;

    // -----------------------
    // Slots
    // -----------------------
    [Header("Slots (2)")]
    [SerializeField] private WeaponDefinition[] slots = new WeaponDefinition[2];

    [Tooltip("장착 중인 슬롯 인덱스. 장착 없음이면 -1")]
    [SerializeField] private int activeIndex = -1;

    [Header("Drop")]
    [SerializeField] private WeaponDrop2D dropPrefab;

    // Ability ref-count (무기 제거 시 중복 방지)
    private readonly Dictionary<AbilityDefinition, int> abilityRefCount = new();

    // -----------------------
    // Public getters
    // -----------------------
    public int ActiveIndex => activeIndex;
    public WeaponDefinition ActiveWeapon => IsValidSlot(activeIndex) ? slots[activeIndex] : null;
    public bool HasEquippedWeapon => activeIndex >= 0 && ActiveWeapon != null;

    public WeaponDefinition GetWeaponInSlot(int slotIndex) => IsValidSlot(slotIndex) ? slots[slotIndex] : null;
    public bool HasWeapon(int slotIndex) => GetWeaponInSlot(slotIndex) != null;

    private void Awake()
    {
        if (abilitySystem == null) abilitySystem = GetComponent<AbilitySystem>();
        if (tagSystem == null) tagSystem = GetComponent<TagSystem>();
        if (equipController == null) equipController = GetComponentInChildren<WeaponEquipController>();
    }


    // -----------------------
    // Public API
    // -----------------------
    public bool TryPickupWeapon(WeaponDefinition weapon)
    {
        if (weapon == null) return false;

        // ✅ SetSlot/DropSlot로 active 슬롯이 바뀌기 전에 "이전 장착 정보"를 먼저 캡처
        int prevIndex = activeIndex;
        var prevWeapon = ActiveWeapon;
        bool hadEquippedBefore = HasEquippedWeapon;

        // 1) 넣을 슬롯 결정
        int idx = FindEmptySlot();
        bool replaced = false;
        bool replacedWasActive = false;

        if (idx < 0)
        {
            replaced = true;

            // 빈 슬롯 없으면 "비활성 슬롯" 우선, 없으면 활성 슬롯 교체
            int other = (activeIndex == 0) ? 1 : 0;
            idx = (slots[other] != null) ? other : Mathf.Clamp(activeIndex, 0, slots.Length - 1);

            replacedWasActive = (idx == activeIndex);

            // 기존 무기 제거(드랍 포함) - 이 과정에서 activeIndex가 -1이 될 수 있음
            DropSlot(idx);
        }

        // 2) 슬롯에 장착(인벤토리 등록)
        SetSlot(idx, weapon);

        // 3) 획득 시 Ability Give
        GiveWeaponAbilities(weapon);

        // 4) 장착이 없던 상태였다면 첫 무기 자동 장착
        if (!hadEquippedBefore)
        {
            // prevWeapon은 null일 가능성이 높으므로 removePrevTag=false여도 무방
            EquipCore(idx, prevIndex, prevWeapon, removePrevTag: false);
            NotifyInventoryChanged();
            return true;
        }

        // 5) “활성 슬롯을 교체해서 주운 경우” → 새 무기 자동 장착
        if (replaced && replacedWasActive)
        {
            // DropSlot에서 이미 장착 태그 제거 / 비주얼 clear가 이뤄졌으므로 removePrevTag=false
            EquipCore(idx, prevIndex, prevWeapon, removePrevTag: false);
            NotifyInventoryChanged();
            return true;
        }

        NotifyInventoryChanged();
        return true;
    }

    public void Equip(int slotIndex)
    {
        if (!IsValidSlot(slotIndex)) return;

        var nowWeapon = slots[slotIndex];
        if (nowWeapon == null) return;

        int prevIndex = activeIndex;
        var prevWeapon = ActiveWeapon;

        // 이미 같은 슬롯/같은 무기면 스킵
        if (prevIndex == slotIndex && prevWeapon == nowWeapon)
            return;

        EquipCore(slotIndex, prevIndex, prevWeapon, removePrevTag: true);
    }

    public void Unequip()
    {
        if (!HasEquippedWeapon) return;

        int prevIndex = activeIndex;
        var prevWeapon = ActiveWeapon;

        // 태그 제거
        if (prevWeapon != null && prevWeapon.equippedTag != null && tagSystem != null)
            tagSystem.RemoveTag(prevWeapon.equippedTag);

        activeIndex = -1;

        // 비주얼 제거/숨김(캐시 사용 시 숨기기)
        if (equipController != null)
            equipController.Clear();

        OnEquippedChanged?.Invoke(prevIndex, -1, prevWeapon, null);
        NotifyInventoryChanged();
    }

    public void Swap()
    {
        if (!HasEquippedWeapon)
        {
            // 장착이 없다면 첫 유효 슬롯을 장착 시도
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
        DropSlot(droppingIndex); // wasActive면 activeIndex=-1 + (필요 시) equipController.Clear 포함

        // 다른 슬롯이 남아있으면 그걸 장착, 없으면 Unequip 상태 유지
        int other = 1 - droppingIndex;
        if (IsValidSlot(other) && slots[other] != null)
            Equip(other);
        else
            activeIndex = -1;

        NotifyInventoryChanged();
    }

    public AbilityDefinition GetActiveAbility(WeaponAbilitySlot slot)
        => ActiveWeapon != null ? ActiveWeapon.GetAbility(slot) : null;

    // -----------------------
    // Internal helpers
    // -----------------------
    private bool IsValidSlot(int i) => i >= 0 && i < slots.Length;

    private int FindEmptySlot()
    {
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] == null) return i;
        return -1;
    }

    private int FindFirstFilledSlot()
    {
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] != null) return i;
        return -1;
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

    /// <summary>
    /// Equip 로직의 핵심. 호출자가 "prevWeapon/prevIndex"를 캡처해서 넘길 수 있어
    /// SetSlot로 active 슬롯이 덮여도 prev가 깨지지 않음.
    /// </summary>
    private void EquipCore(int newIndex, int prevIndex, WeaponDefinition prevWeapon, bool removePrevTag)
    {
        if (!IsValidSlot(newIndex)) return;

        var nowWeapon = slots[newIndex];
        if (nowWeapon == null) return;

        // 이전 장착 태그 제거 (상황에 따라 호출자가 스킵 가능)
        if (removePrevTag && prevWeapon != null && prevWeapon.equippedTag != null && tagSystem != null)
            tagSystem.RemoveTag(prevWeapon.equippedTag);

        activeIndex = newIndex;

        // 새 장착 태그 추가
        if (nowWeapon.equippedTag != null && tagSystem != null)
            tagSystem.AddTag(nowWeapon.equippedTag);

        // 프리팹 장착
        if (equipController != null && nowWeapon.weaponPrefab != null)
            equipController.Equip(nowWeapon.weaponPrefab);

        OnEquippedChanged?.Invoke(prevIndex, activeIndex, prevWeapon, nowWeapon);
    }

    private void DropSlot(int slotIndex)
    {
        if (!IsValidSlot(slotIndex)) return;

        var weapon = slots[slotIndex];
        if (weapon == null) return;

        bool wasActive = (slotIndex == activeIndex);

        // 장착 중인 슬롯을 드랍한다면, 장착 태그 제거 + 비주얼 제거
        if (wasActive)
        {
            if (weapon.equippedTag != null && tagSystem != null)
                tagSystem.RemoveTag(weapon.equippedTag);

            activeIndex = -1;

            if (equipController != null)
                equipController.Clear();
        }

        // 능력 회수(인벤토리에서 완전히 빠질 때만 Take)
        TakeWeaponAbilities(weapon);

        // 월드 드랍
        if (dropPrefab != null)
        {
            var drop = Instantiate(dropPrefab, transform.position, Quaternion.identity);
            drop.SetWeapon(weapon);
        }

        ClearSlot(slotIndex);
        // (여기서는 자동 Equip 하지 않음. DropActive/TryPickupWeapon에서 결정)
    }

    private void GiveWeaponAbilities(WeaponDefinition w)
    {
        GiveAbilityRef(w.attack);
        GiveAbilityRef(w.skill1);
        GiveAbilityRef(w.skill2);
    }

    private void TakeWeaponAbilities(WeaponDefinition w)
    {
        TakeAbilityRef(w.attack);
        TakeAbilityRef(w.skill1);
        TakeAbilityRef(w.skill2);
    }

    private void GiveAbilityRef(AbilityDefinition def)
    {
        if (def == null || abilitySystem == null) return;

        if (!abilityRefCount.TryGetValue(def, out int c)) c = 0;
        abilityRefCount[def] = c + 1;

        // 최초 1회만 Give
        if (c == 0)
            abilitySystem.GiveAbility(def);
    }

    private void TakeAbilityRef(AbilityDefinition def)
    {
        if (def == null || abilitySystem == null) return;

        if (!abilityRefCount.TryGetValue(def, out int c) || c <= 0) return;

        c--;
        if (c <= 0)
        {
            abilityRefCount.Remove(def);
            abilitySystem.TakeAbility(def);
        }
        else
        {
            abilityRefCount[def] = c;
        }
    }

    private void NotifyInventoryChanged()
    {
        OnInventoryChanged?.Invoke();
    }
}
