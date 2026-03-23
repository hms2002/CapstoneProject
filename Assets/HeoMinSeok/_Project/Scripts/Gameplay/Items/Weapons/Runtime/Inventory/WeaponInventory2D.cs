using System;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 : 플레이어의 무기 슬롯, 활성 장착 무기, 픽업/교체/드롭/스왑 흐름을 관리한다.
/// 일반 플레이 중 장착은 기존 Equip 경로로 처리하고,
/// 씬 복원 시에는 effect-free shell restore와 runtime hook attach 경로를 제공한다.
/// </summary>
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
    public int ActiveIndex => equipRuntime != null ? equipRuntime.ActiveIndex : activeIndex;
    public WeaponDefinition ActiveWeapon => IsValidSlot(ActiveIndex) ? slots[ActiveIndex] : null;
    public bool HasEquippedWeapon => ActiveIndex >= 0 && ActiveWeapon != null;
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

    //private void Start()
    //{
    //    if (HasEquippedWeapon)
    //    {
    //        var result = equipRuntime.Equip(ActiveIndex, ActiveWeapon);
    //        SyncActiveStateFromRuntime();

    //        if (result.Changed)
    //            OnEquippedChanged?.Invoke(result.PreviousIndex, result.NewIndex, result.PreviousWeapon, result.NewWeapon);
    //    }
    //}

    // -----------------------
    // Public API
    // -----------------------
    /// <summary>
    /// 책임 : 무기를 인벤토리에 픽업하고, 필요하면 그 무기 인스턴스의 영속 상태도 함께 복원한다.
    /// 드롭 오브젝트, 상자 보상, 테스트 코드 등 다양한 진입점을 이 API로 통일한다.
    /// </summary>
    public bool TryPickupWeapon(WeaponDefinition weapon, WeaponPersistentStatePayload runtimePayload = null)
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

            int current = ActiveIndex;
            int other = (current == 0) ? 1 : 0;
            targetIndex = (slots[other] != null) ? other : Mathf.Clamp(current, 0, slots.Length - 1);
            replacedWasActive = (targetIndex == current);

            DropSlot(targetIndex);
        }

        SetSlot(targetIndex, weapon);
        abilityBinder.OnWeaponAdded(weapon);
        ApplyWeaponPersistentState(weapon, runtimePayload);

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
        int current = ActiveIndex;

        if (!HasEquippedWeapon)
        {
            int first = FindFirstFilledSlot();
            if (first >= 0) Equip(first);
            NotifyInventoryChanged();
            return;
        }

        int other = 1 - current;
        if (!IsValidSlot(other) || slots[other] == null)
            return;

        Equip(other);
        NotifyInventoryChanged();
    }

    public void DropActive()
    {
        if (!HasEquippedWeapon) return;

        int droppingIndex = ActiveIndex;
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

        bool wasActive = (slotIndex == ActiveIndex);

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
        else if (autoEquipIfNone && ActiveIndex < 0 && newWeapon != null)
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

        int prevIndex = ActiveIndex;
        WeaponDefinition prevWeapon = IsValidSlot(prevIndex) ? slots[prevIndex] : null;

        var wa = slots[a];
        var wb = slots[b];

        slots[a] = wb;
        slots[b] = wa;

        OnSlotChanged?.Invoke(a, wa, slots[a]);
        OnSlotChanged?.Invoke(b, wb, slots[b]);

        int newIndex = prevIndex;
        if (prevIndex == a) newIndex = b;
        else if (prevIndex == b) newIndex = a;

        WeaponDefinition newWeapon = IsValidSlot(newIndex) ? slots[newIndex] : null;

        activeIndex = newIndex;
        equipRuntime.Initialize(newIndex, newWeapon);
        SyncActiveStateFromRuntime();

        if (prevIndex != newIndex && newIndex >= 0)
        {
            OnEquippedChanged?.Invoke(prevIndex, newIndex, prevWeapon, newWeapon);
        }

        NotifyInventoryChanged();
        return true;
    }

    /// <summary>
    /// 책임 : 씬 복원 시 무기 슬롯과 활성 슬롯 정보만 effect 없이 복원한다.
    /// stat/tag/ability는 적용하지 않고, 필요하면 활성 무기의 비주얼만 맞춘다.
    /// </summary>
    public void RestoreShellState(
        WeaponInventoryState state,
        Func<string, WeaponDefinition> weaponResolver,
        bool applyActiveVisual = true)
    {
        if (state == null)
            return;

        if (weaponResolver == null)
        {
            Debug.LogError("[WeaponInventory2D] weaponResolver가 null입니다.");
            return;
        }

        abilityBinder?.ClearReferencesWithoutRemoving();
        presentationBinder?.ClearVisualOnly();

        for (int i = 0; i < slots.Length; i++)
            slots[i] = null;

        if (state.slotWeaponIds != null)
        {
            int copyCount = Mathf.Min(slots.Length, state.slotWeaponIds.Length);
            for (int i = 0; i < copyCount; i++)
            {
                string weaponId = state.slotWeaponIds[i];
                if (string.IsNullOrEmpty(weaponId))
                    continue;

                var resolved = weaponResolver(weaponId);
                if (resolved == null)
                {
                    Debug.LogWarning($"[WeaponInventory2D] 무기 복원 실패: slot={i}, weaponId={weaponId}", this);
                    continue;
                }

                slots[i] = resolved;
            }
        }

        int restoredActiveIndex = state.activeSlotIndex;
        if (restoredActiveIndex < 0 || restoredActiveIndex >= slots.Length || slots[restoredActiveIndex] == null)
            restoredActiveIndex = -1;

        var restoredActiveWeapon =
            restoredActiveIndex >= 0 && restoredActiveIndex < slots.Length
                ? slots[restoredActiveIndex]
                : null;

        activeIndex = restoredActiveIndex;
        equipRuntime.Initialize(restoredActiveIndex, restoredActiveWeapon);

        if (applyActiveVisual)
            presentationBinder?.ApplyVisualOnly(restoredActiveWeapon);
        else
            presentationBinder?.ClearVisualOnly();

        NotifyInventoryChanged();
    }


    /// <summary>
    /// 책임 : 껍데기 복원 후 무기 인벤토리의 내부 런타임 훅을 다시 구성하고,
    /// 현재 슬롯 무기들의 ability grant를 최소 보장한다.
    /// 이후 개별 runtime state 복원이 cooldown, charges, stack, custom vars를 덮어쓴다.
    /// </summary>
    public void AttachRuntimeHooksForRestore()
    {
        abilityBinder?.RebuildReferencesAndEnsureGranted(slots);
    }

    /// <summary>
    /// 책임 : 현재 무기 슬롯 상태를 그대로 저장용 DTO로 캡처한다.
    /// 씬 이동 직전 장비 배치 상태 저장의 공식 창구로 사용한다.
    /// </summary>
    public WeaponInventoryState CaptureInventoryState()
    {
        var state = new WeaponInventoryState
        {
            slotWeaponIds = new string[slots.Length],
            activeSlotIndex = ActiveIndex
        };

        for (int i = 0; i < slots.Length; i++)
            state.slotWeaponIds[i] = slots[i] != null ? slots[i].weaponId : null;

        return state;
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

        bool wasActive = (slotIndex == ActiveIndex);

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

        var payload = CaptureWeaponPersistentState(weapon);

        abilityBinder.OnWeaponRemoved(weapon);

        if (dropPrefab != null)
        {
            var drop = Instantiate(dropPrefab, transform.position, Quaternion.identity);
            drop.SetWeapon(weapon, payload);
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

    /// <summary>
    /// 책임 : 특정 무기 정의에 연결된 ability들의 영속 상태를 추출해 드롭/저장용 payload로 묶는다.
    /// AbilitySystem에서 현재 살아 있는 spec 상태만 읽으며, 진행 중 실행 상태는 포함하지 않는다.
    /// </summary>
    private WeaponPersistentStatePayload CaptureWeaponPersistentState(WeaponDefinition weapon)
    {
        if (weapon == null || abilitySystem == null)
            return null;

        var payload = new WeaponPersistentStatePayload
        {
            weaponId = weapon.weaponId
        };

        AddAbilityPersistentState(payload.abilities, weapon.attack);
        AddAbilityPersistentState(payload.abilities, weapon.skill1);
        AddAbilityPersistentState(payload.abilities, weapon.skill2);

        return payload;
    }

    /// <summary>
    /// 책임 : 드롭/픽업으로 이동한 무기 payload를 현재 AbilitySystem에 다시 주입한다.
    /// 무기를 인벤토리에 추가한 직후, 해당 무기의 ability spec이 생성된 뒤 호출되어야 한다.
    /// </summary>
    private void ApplyWeaponPersistentState(WeaponDefinition weapon, WeaponPersistentStatePayload payload)
    {
        if (weapon == null || payload == null || abilitySystem == null)
            return;

        if (!string.IsNullOrEmpty(payload.weaponId) &&
            !string.Equals(payload.weaponId, weapon.weaponId, StringComparison.Ordinal))
        {
            Debug.LogWarning(
                $"[WeaponInventory2D] 무기 payload 복원 생략: weaponId 불일치 ({payload.weaponId} != {weapon.weaponId})",
                this);
            return;
        }

        if (payload.abilities == null || payload.abilities.Count == 0)
            return;

        for (int i = 0; i < payload.abilities.Count; i++)
        {
            var state = payload.abilities[i];
            if (state == null)
                continue;

            abilitySystem.ImportPersistentState(
                state,
                abilityId => ResolveAbilityOnWeapon(weapon, abilityId));
        }
    }

    /// <summary>
    /// 책임 : payload 목록에 ability 하나의 영속 상태를 추가한다.
    /// spec이 아직 없거나 export 결과가 null이면 조용히 건너뛴다.
    /// </summary>
    private void AddAbilityPersistentState(
        List<AbilityPersistentState> output,
        AbilityDefinition ability)
    {
        if (output == null || ability == null || abilitySystem == null)
            return;

        var state = abilitySystem.ExportPersistentState(ability);
        if (state != null)
            output.Add(state);
    }

    /// <summary>
    /// 책임 : 특정 무기 정의가 가진 attack/skill1/skill2 중 abilityId와 일치하는 AbilityDefinition을 찾는다.
    /// 무기 payload 복원 시 이 무기 소유 능력만 대상으로 import 되도록 제한한다.
    /// </summary>
    private static AbilityDefinition ResolveAbilityOnWeapon(WeaponDefinition weapon, string abilityId)
    {
        if (weapon == null || string.IsNullOrEmpty(abilityId))
            return null;

        if (weapon.attack != null && weapon.attack.name == abilityId)
            return weapon.attack;

        if (weapon.skill1 != null && weapon.skill1.name == abilityId)
            return weapon.skill1;

        if (weapon.skill2 != null && weapon.skill2.name == abilityId)
            return weapon.skill2;

        return null;
    }
}