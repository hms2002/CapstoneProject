using System;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;
using CapstoneAudio;

/// <summary>
/// 책임 : 플레이어의 무기 슬롯, 활성 장착 무기, 픽업/교체/드롭/스왑 흐름을 관리한다.
/// 일반 플레이 중 장착은 기존 Equip 경로로 처리하고,
/// 씬 복원 시에는 effect-free shell restore와 runtime hook attach 경로를 제공한다.
/// </summary>
public class WeaponInventory2D : MonoBehaviour
{
    /// <summary>
    /// 책임 :
    /// - 런타임 무기 스왑 성공 시 재생할 공용 사운드 키를 한 곳에 고정한다.
    /// - 인벤토리 UI 장착 변경과 구분되는 전투 중 스왑 피드백을 안정적으로 재사용하게 한다.
    /// </summary>
    private static readonly SoundRef ChangeWeaponSound = SoundRef.FromKey("weapon.swap");

    /// <summary>
    /// 책임 :
    /// - 무기 획득 시도가 어떤 결과로 끝났는지 도메인 수준에서 구분한다.
    /// - 상위 호출부가 인벤토리 가득 참 같은 실패 사유를 UI 경고로 정확히 전달할 수 있게 한다.
    /// </summary>
    public enum AcquireResult
    {
        Success = 0,
        InvalidDefinition,
        InventoryFull,
        DuplicateRejected
    }

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
    [NonSerialized] private WeaponRuntimeData[] runtimeSlots;
    [NonSerialized] private WeaponRuntimeCoordinator runtimeCoordinator;
    [NonSerialized] private IWeaponInteractionLayer interactionLayer;

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
    public WeaponRuntimeData ActiveRuntimeData => GetRuntimeDataInSlot(ActiveIndex);
    public IWeaponInteractionLayer InteractionLayer => interactionLayer;
    public bool HasEquippedWeapon => ActiveIndex >= 0 && ActiveWeapon != null;
    public int SlotCount => slots.Length;
    public WeaponEquipController EquipController => equipController;

    public WeaponDefinition GetWeaponInSlot(int slotIndex)
        => IsValidSlot(slotIndex) ? slots[slotIndex] : null;

    /// <summary>
    /// 책임 :
    /// - 지정 슬롯 무기의 persistent runtime data를 반환한다.
    /// - 선택 전략, 저장/복원, 장착 중 live adapter가 같은 슬롯 상태를 공유하게 하는 공식 창구다.
    /// </summary>
    public WeaponRuntimeData GetRuntimeDataInSlot(int slotIndex)
    {
        EnsureRuntimeSlotCapacity();
        return IsValidSlot(slotIndex) ? runtimeSlots[slotIndex] : null;
    }

    /// <summary>
    /// 책임 :
    /// - 현재 슬롯과 짝을 이루는 반대 슬롯 인덱스를 계산한다.
    /// - 쌍무기 전략과 런타임 processor가 인벤토리 배치 규칙을 직접 몰라도 다른 무기 상태를 조회하게 한다.
    /// </summary>
    public int GetOtherSlotIndex(int slotIndex)
    {
        if (!IsValidSlot(slotIndex))
            return -1;

        if (slots.Length == 2)
            return slotIndex == 0 ? 1 : 0;

        for (int i = 0; i < slots.Length; i++)
        {
            if (i != slotIndex)
                return i;
        }

        return -1;
    }

    /// <summary>
    /// 책임 :
    /// - 지정 슬롯과 짝을 이루는 반대 슬롯 무기 정의를 반환한다.
    /// - 쌍무기 선택 규칙이 다른 슬롯 weaponId나 loadout을 읽을 공식 창구를 제공한다.
    /// </summary>
    public WeaponDefinition GetOtherWeaponInSlot(int slotIndex)
    {
        int otherSlotIndex = GetOtherSlotIndex(slotIndex);
        return GetWeaponInSlot(otherSlotIndex);
    }

    /// <summary>
    /// 책임 :
    /// - 지정 슬롯과 짝을 이루는 반대 슬롯 persistent runtime data를 반환한다.
    /// - 비활성 무기 상태를 읽는 선택 전략, processor, live adapter가 같은 조회 규칙을 공유하게 한다.
    /// </summary>
    public WeaponRuntimeData GetOtherRuntimeData(int slotIndex)
    {
        int otherSlotIndex = GetOtherSlotIndex(slotIndex);
        return GetRuntimeDataInSlot(otherSlotIndex);
    }

    /// <summary>
    /// 책임 :
    /// - 지정 슬롯 무기에 연결된 runtime processor를 반환한다.
    /// - 인벤토리 바깥 계층이 processor 수명주기를 직접 소유하지 않고도 현재 구성 상태를 조회하게 한다.
    /// </summary>
    public WeaponRuntimeProcessor GetRuntimeProcessorInSlot(int slotIndex)
    {
        return runtimeCoordinator != null
            ? runtimeCoordinator.GetProcessorInSlot(slotIndex)
            : null;
    }

    public bool HasWeapon(int slotIndex)
        => GetWeaponInSlot(slotIndex) != null;

    public bool CanAcquireWithoutReplacement(WeaponDefinition weapon)
    {
        if (weapon == null)
            return false;

        if (disallowDuplicateWeapons && ContainsWeaponId(weapon.weaponId))
            return false;

        return FindEmptySlot() >= 0;
    }

    public AcquireResult PreviewAcquireWithoutReplacement(WeaponDefinition weapon)
    {
        if (weapon == null)
            return AcquireResult.InvalidDefinition;

        if (disallowDuplicateWeapons && ContainsWeaponId(weapon.weaponId))
            return AcquireResult.DuplicateRejected;

        return FindEmptySlot() >= 0
            ? AcquireResult.Success
            : AcquireResult.InventoryFull;
    }

    public AcquireResult TryAcquireWithoutReplacementDetailed(WeaponDefinition weapon, WeaponPersistentStatePayload runtimePayload = null)
    {
        AcquireResult previewResult = PreviewAcquireWithoutReplacement(weapon);
        if (previewResult != AcquireResult.Success)
            return previewResult;

        return TryPickupWeapon(weapon, runtimePayload)
            ? AcquireResult.Success
            : AcquireResult.InvalidDefinition;
    }

    /// <summary>
    /// 책임 :
    /// - 무기 획득 시도의 상세 결과를 기존 bool 성공/실패 규약으로 감싼다.
    /// - 호출부 전체를 한 번에 바꾸지 않고도 상세 실패 사유 enum을 점진적으로 도입한다.
    /// </summary>
    public bool TryAcquireWithoutReplacement(WeaponDefinition weapon, WeaponPersistentStatePayload runtimePayload = null)
    {
        return TryAcquireWithoutReplacementDetailed(weapon, runtimePayload) == AcquireResult.Success;
    }

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
        runtimeCoordinator = new WeaponRuntimeCoordinator(this);
        interactionLayer = new WeaponInteractionLayer(runtimeCoordinator);

        EnsureStatusHudSource();

        EnsureRuntimeSlotCapacity();
        RebuildRuntimeDataState();
        RebuildAbilityOwnershipState();
        equipRuntime.Initialize(activeIndex, ActiveWeapon);

        if (ActiveWeapon != null)
        {
            // 시작 시 이미 활성 무기 정의가 있는 경우, equipRuntime 상태와 실제 장착 프리팹 인스턴스를 먼저 맞춘다.
            // 그래야 WeaponAbilityRuntimeState를 가진 무기 프리팹이 selector 단계에서 정상적으로 조회된다.
            presentationBinder.ApplyVisualOnly(ActiveWeapon);
        }
    }

    /// <summary>
    /// 책임 :
    /// - 무기 인벤토리 owner에 태양도/월영도 상태 HUD source를 직접 부착해 별도 bootstrap 없이도 무기 상태 HUD가 등록되게 한다.
    /// - 특정 무기마다 별도 bootstrap을 늘리지 않고, 실제 runtime data를 소유한 인벤토리만 공통 HUD 파이프라인에 참여하게 만든다.
    /// </summary>
    private void EnsureStatusHudSource()
    {
        SunMoonStatusHudSource.GetOrAdd(gameObject);
    }

    private void Update()
    {
        runtimeCoordinator?.Tick(Time.deltaTime);
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
    public bool TryPickupWeapon(
        WeaponDefinition weapon,
        WeaponPersistentStatePayload runtimePayload = null,
        Vector3? replacementDropPosition = null)
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

            targetIndex = ResolveReplacementSlotIndex();
            if (!IsValidSlot(targetIndex))
                return false;

            replacedWasActive = (targetIndex == ActiveIndex);
            if (replacedWasActive && IsActiveWeaponChangeBlocked())
                return false;

            DropSlot(targetIndex, replacementDropPosition);
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
        if (slotIndex != ActiveIndex && IsActiveWeaponChangeBlocked()) return;

        var newWeapon = slots[slotIndex];
        if (newWeapon == null) return;
        if (slotIndex != ActiveIndex)
            CleanupTransientAbilitiesForWeaponChange();

        var result = equipRuntime.Equip(slotIndex, newWeapon);
        if (!result.Changed) return;

        SyncActiveStateFromRuntime();
        OnEquippedChanged?.Invoke(result.PreviousIndex, result.NewIndex, result.PreviousWeapon, result.NewWeapon);
        NotifyInventoryChanged();
    }

    public void Unequip()
    {
        if (!HasEquippedWeapon) return;
        if (IsActiveWeaponChangeBlocked()) return;

        CleanupTransientAbilitiesForWeaponChange();

        var result = equipRuntime.Unequip();
        if (!result.Changed) return;

        SyncActiveStateFromRuntime();
        OnEquippedChanged?.Invoke(result.PreviousIndex, result.NewIndex, result.PreviousWeapon, result.NewWeapon);
        NotifyInventoryChanged();
    }

    public void Swap()
    {
        if (IsActiveWeaponChangeBlocked())
            return;

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

        int previousActiveIndex = ActiveIndex;
        Equip(other);
        if (ActiveIndex != previousActiveIndex)
            PlayRuntimeSwapSound();
        NotifyInventoryChanged();
    }

    public void DropActive()
    {
        if (!HasEquippedWeapon) return;
        if (IsActiveWeaponChangeBlocked()) return;

        int droppingIndex = ActiveIndex;
        DropSlot(droppingIndex);

        int other = 1 - droppingIndex;
        if (IsValidSlot(other) && slots[other] != null)
            Equip(other);

        NotifyInventoryChanged();
    }

    /// <summary>
    /// 책임 :
    /// - 현재 활성 슬롯 무기를 월드 드롭 없이 영구 제거한다.
    /// - 기묘한 쇳덩이 투척처럼 무기 자체가 파괴되는 스킬이 DropActive의 드롭 생성 정책을 우회하게 한다.
    /// </summary>
    public bool DestroyActiveWeaponWithoutDrop(bool equipFallback = true)
    {
        if (!HasEquippedWeapon)
            return false;
        if (IsActiveWeaponChangeBlocked())
            return false;

        int destroyingIndex = ActiveIndex;
        WeaponDefinition destroyingWeapon = ActiveWeapon;

        CleanupTransientAbilitiesForWeaponChange();

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

        if (destroyingWeapon != null)
            abilityBinder.OnWeaponRemoved(destroyingWeapon);

        ClearSlot(destroyingIndex);

        if (equipFallback)
        {
            int fallbackIndex = FindFirstFilledSlot();
            if (fallbackIndex >= 0)
                Equip(fallbackIndex);
        }

        NotifyInventoryChanged();
        return true;
    }

    public AbilityDefinition GetActiveAbility(WeaponAbilitySlot slot)
        => ActiveWeapon != null ? ActiveWeapon.GetAbility(slot) : null;

    /// <summary>
    /// 책임 :
    /// - 전투 중 스왑 입력으로 실제 활성 무기가 바뀐 경우에만 스왑 사운드를 1회 재생한다.
    /// - 동일 슬롯 재장착이나 실패한 스왑 시도에서는 불필요한 UI/전투 사운드가 울리지 않게 한다.
    /// </summary>
    private void PlayRuntimeSwapSound()
    {
        SoundRef sound = ResolveRuntimeSwapSound();
        SoundManager.EnsureInstance().Play(sound, new SoundPlaybackContext
        {
            Instigator = gameObject,
            Causer = gameObject,
            Target = gameObject,
            Position = transform.position,
            SourceObject = this
        });
    }

    /// <summary>
    /// 책임 :
    /// - 현재 새로 장착된 무기의 교체음 오버라이드를 우선 사용하고, 없으면 공용 기본 교체음을 반환한다.
    /// - 무기별 authoring 선택과 인벤토리의 재생 타이밍 책임을 분리한다.
    /// </summary>
    private SoundRef ResolveRuntimeSwapSound()
    {
        WeaponDefinition activeWeapon = ActiveWeapon;
        if (activeWeapon != null && activeWeapon.TryGetSwapSoundOverride(out SoundRef overrideSound))
            return overrideSound;

        return ChangeWeaponSound;
    }

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
        if (wasActive && IsActiveWeaponChangeBlocked())
            return false;

        if (wasActive)
        {
            CleanupTransientAbilitiesForWeaponChange();
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
        else if (wasActive && newWeapon == null)
        {
            int fallbackIndex = FindFirstFilledSlot();
            if (fallbackIndex >= 0)
                Equip(fallbackIndex);
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
        if ((a == ActiveIndex || b == ActiveIndex) && IsActiveWeaponChangeBlocked()) return false;

        int prevIndex = ActiveIndex;
        WeaponDefinition prevWeapon = IsValidSlot(prevIndex) ? slots[prevIndex] : null;

        var wa = slots[a];
        var wb = slots[b];
        var ra = GetRuntimeDataInSlot(a);
        var rb = GetRuntimeDataInSlot(b);

        slots[a] = wb;
        slots[b] = wa;
        runtimeSlots[a] = rb;
        runtimeSlots[b] = ra;
        runtimeCoordinator?.Rebuild(slots, runtimeSlots);

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
        {
            slots[i] = null;
            runtimeSlots[i] = null;
        }

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
                runtimeSlots[i] = CreateRuntimeDataForWeapon(resolved);
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
        runtimeCoordinator?.Rebuild(slots, runtimeSlots);

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

        if (ActiveWeapon != null)
            statBinder?.Apply(ActiveWeapon);
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
        runtimeSlots[slotIndex] = CreateRuntimeDataForWeapon(newWeapon);
        runtimeCoordinator?.Rebuild(slots, runtimeSlots);
        OnSlotChanged?.Invoke(slotIndex, prev, newWeapon);
    }

    private void ClearSlot(int slotIndex)
    {
        if (!IsValidSlot(slotIndex)) return;
        SetSlot(slotIndex, null);
    }

    private void DropSlot(int slotIndex, Vector3? worldPositionOverride = null)
    {
        if (!IsValidSlot(slotIndex)) return;

        var weapon = slots[slotIndex];
        if (weapon == null) return;

        bool wasActive = (slotIndex == ActiveIndex);
        if (wasActive && IsActiveWeaponChangeBlocked())
            return;

        if (wasActive)
        {
            CleanupTransientAbilitiesForWeaponChange();
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
            Vector3 startPosition = transform.position;
            Vector3 dropPosition = worldPositionOverride ?? transform.position;
            var drop = Instantiate(dropPrefab, dropPosition, Quaternion.identity);
            drop.SetWeapon(weapon, payload);
            drop.PlayDrop(startPosition, dropPosition);
        }

        ClearSlot(slotIndex);
    }

    private int ResolveReplacementSlotIndex()
    {
        int current = ActiveIndex;
        if (IsValidSlot(current) && slots[current] != null)
            return current;

        return FindFirstFilledSlot();
    }

    private void NotifyInventoryChanged()
    {
        OnInventoryChanged?.Invoke();
    }

    private bool IsActiveWeaponChangeBlocked()
    {
        FloweringRuntimeState floweringState = FloweringRuntimeState.ResolveExisting(abilitySystem);
        return floweringState != null && floweringState.BlocksWeaponSwap;
    }

    /// <summary>
    /// 책임 : 무기 교체/해제 직전에 현재 실행 중인 능력의 일시 상태를 정리한다.
    /// Rush 같은 지속 실행이 이전 무기 문맥을 붙잡고 남지 않도록 하되, 쿨다운/차지 같은 영속 상태는 건드리지 않는다.
    /// </summary>
    private void CleanupTransientAbilitiesForWeaponChange()
    {
        abilitySystem?.ResetTransientRuntimeState();
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

        foreach (var ability in weapon.EnumerateGrantedAbilities())
            AddAbilityPersistentState(payload.abilities, ability);

        if (payload.abilities.Count == 0)
            return null;

        return payload;
    }

    /// <summary>
    /// 책임 :
    /// - 슬롯 배열 길이와 같은 runtime data 배열이 항상 준비되게 보장한다.
    /// - 인벤토리 코드가 슬롯별 지속 상태를 null 참조 없이 다루게 하는 최소 안전망이다.
    /// </summary>
    private void EnsureRuntimeSlotCapacity()
    {
        if (runtimeSlots != null && runtimeSlots.Length == slots.Length)
            return;

        var previous = runtimeSlots;
        runtimeSlots = new WeaponRuntimeData[slots.Length];

        if (previous == null)
            return;

        int copyCount = Mathf.Min(previous.Length, runtimeSlots.Length);
        for (int i = 0; i < copyCount; i++)
            runtimeSlots[i] = previous[i];
    }

    /// <summary>
    /// 책임 :
    /// - 현재 슬롯 배치를 기준으로 slot data가 없는 무기에 기본 WeaponRuntimeData를 생성한다.
    /// - 월식도처럼 전용 data가 필요한 무기도 인벤토리 시작 시점부터 persistent owner를 갖게 만든다.
    /// </summary>
    private void RebuildRuntimeDataState()
    {
        EnsureRuntimeSlotCapacity();

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null)
            {
                runtimeSlots[i] = null;
                continue;
            }

            if (runtimeSlots[i] == null)
                runtimeSlots[i] = CreateRuntimeDataForWeapon(slots[i]);
        }

        runtimeCoordinator?.Rebuild(slots, runtimeSlots);
    }

    /// <summary>
    /// 책임 :
    /// - 슬롯에 새 무기가 배치될 때 그 무기가 사용할 persistent runtime data 구현체를 생성한다.
    /// - 무기별 상태 클래스 선택 규칙은 factory에 위임해 인벤토리가 구체 타입 분기를 직접 알지 않게 한다.
    /// </summary>
    private static WeaponRuntimeData CreateRuntimeDataForWeapon(WeaponDefinition weapon)
    {
        return WeaponRuntimeDataFactory.CreateForWeapon(weapon);
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

        foreach (var ability in weapon.EnumerateGrantedAbilities())
        {
            if (ability != null && ability.name == abilityId)
                return ability;
        }

        return null;
    }
}
