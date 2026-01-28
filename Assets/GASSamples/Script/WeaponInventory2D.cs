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

    // 중복된 무기 습득 block
    [SerializeField] private bool disallowDuplicateWeapons = true;

    // (선택) UI용 피드백 이벤트
    public event Action<WeaponDefinition> OnPickupRejected_Duplicate;

    private void Awake()
    {
        if (abilitySystem == null) abilitySystem = GetComponent<AbilitySystem>();
        if (tagSystem == null) tagSystem = GetComponent<TagSystem>();
        if (equipController == null) equipController = GetComponentInChildren<WeaponEquipController>();
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

    // -----------------------
    // Drag&Drop Minimal API
    // -----------------------
    public int SlotCount => slots.Length;

    /// <summary>
    /// 슬롯에 특정 무기를 놓을 수 있는지(중복 무기 금지 정책 포함)
    /// </summary>
    public bool CanPlaceWeaponInSlot(int slotIndex, WeaponDefinition weapon)
    {
        if (!IsValidSlot(slotIndex)) return false;
        if (weapon == null) return true;

        if (disallowDuplicateWeapons)
        {
            // 교체될 슬롯은 제외하고 중복 검사
            if (ContainsWeaponIdExcept(weapon.weaponId, slotIndex))
                return false;
        }

        return true;
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

    /// <summary>
    /// 슬롯에 무기를 직접 세팅(교체/제거 포함). 드래그&드롭에서 사용.
    /// - 기존 무기는 인벤에서 빠지므로 Ability Take
    /// - 새 무기는 인벤에 들어오므로 Ability Give
    /// - active 슬롯이면 태그/비주얼/이벤트까지 올바르게 갱신
    /// </summary>
    public bool TrySetWeaponSlot(int slotIndex, WeaponDefinition newWeapon, bool autoEquipIfNone = true)
    {
        if (!IsValidSlot(slotIndex)) return false;

        var oldWeapon = slots[slotIndex];
        if (oldWeapon == newWeapon) return true;

        // 중복 무기 금지 정책
        if (newWeapon != null && !CanPlaceWeaponInSlot(slotIndex, newWeapon))
            return false;

        bool wasActive = (slotIndex == activeIndex);
        int prevIndex = activeIndex;

        // prevWeapon은 "교체 전" 기준으로 잡아야 한다
        // (active 슬롯 교체 시 ActiveWeapon이 덮이는 문제 방지)
        var prevWeapon = wasActive ? oldWeapon : ActiveWeapon;

        // 1) active 슬롯이었다면, 먼저 기존 장착 태그 제거 + 비주얼/상태 정리
        if (wasActive && oldWeapon != null && oldWeapon.equippedTag != null && tagSystem != null)
            tagSystem.RemoveTag(oldWeapon.equippedTag);

        if (wasActive && newWeapon == null)
        {
            // 무기 제거 후 장착 없음 상태가 되므로 비주얼도 제거
            activeIndex = -1;
            if (equipController != null) equipController.Clear();
        }

        // 2) 기존 무기는 인벤에서 빠지므로 능력 회수
        if (oldWeapon != null)
            TakeWeaponAbilities(oldWeapon);

        // 3) 슬롯 갱신 (OnSlotChanged 발생)
        SetSlot(slotIndex, newWeapon);

        // 4) 새 무기는 인벤에 들어오므로 능력 부여
        if (newWeapon != null)
            GiveWeaponAbilities(newWeapon);

        // 5) active 슬롯이었는데 새 무기가 들어온 경우: 그 무기를 즉시 장착 상태로 갱신
        if (wasActive && newWeapon != null)
        {
            // 여기선 이미 이전 태그를 제거했으므로 removePrevTag=false로 코어 호출
            EquipCore(slotIndex, prevIndex, oldWeapon, removePrevTag: false);
        }
        else
        {
            // 6) 장착이 없는 상태(-1)인데 새 무기가 들어왔으면 자동 장착(옵션)
            if (autoEquipIfNone && activeIndex < 0 && newWeapon != null)
                Equip(slotIndex);
        }

        NotifyInventoryChanged();
        return true;
    }

    /// <summary>
    /// 인벤토리 슬롯 간 swap. (디아블로식)
    /// - 무기 개체는 그대로 옮기고
    /// - "현재 장착 무기"가 유지되도록 activeIndex를 같이 이동시킨다.
    /// </summary>
    public bool TrySwapWeaponSlots(int a, int b)
    {
        if (!IsValidSlot(a) || !IsValidSlot(b)) return false;
        if (a == b) return true;

        var wa = slots[a];
        var wb = slots[b];

        // 그냥 swap
        slots[a] = wb;
        slots[b] = wa;

        OnSlotChanged?.Invoke(a, wa, slots[a]);
        OnSlotChanged?.Invoke(b, wb, slots[b]);

        // activeIndex가 가리키는 "무기"를 유지하도록 activeIndex도 같이 이동
        if (activeIndex == a) activeIndex = b;
        else if (activeIndex == b) activeIndex = a;

        // 장착 무기 자체는 변하지 않으므로 태그/비주얼은 그대로 두는 게 자연스러움
        // 다만 UI는 activeIndex가 바뀔 수 있으니 이벤트는 쏴줌
        if (activeIndex == a || activeIndex == b)
        {
            // 위에서 activeIndex를 이동시켰으므로 "무기는 같고 index만 바뀐" 상황
            // prev/new weapon은 동일한 값으로 보내도 됨(슬롯 강조용)
            var equipped = ActiveWeapon;
            OnEquippedChanged?.Invoke(-2, activeIndex, equipped, equipped); // prevIndex가 의미 없으면 -2 같은 값으로 표기해도 OK
        }

        NotifyInventoryChanged();
        return true;
    }

}
