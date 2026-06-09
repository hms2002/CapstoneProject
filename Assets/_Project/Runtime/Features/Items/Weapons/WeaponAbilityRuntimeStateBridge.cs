using System;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 : 무기 하나가 소유한 ability들의 영속 상태를 JSON payload로 묶는다.
/// 씬 이동 시 weaponRuntimeStates에 저장되고, 복원 시 다시 AbilitySystem에 주입된다.
/// </summary>
[Serializable]
public sealed class WeaponAbilityRuntimePayload
{
    public string weaponId;
    public List<AbilityPersistentState> abilities = new();
}

/// <summary>
/// 책임 : 무기 소유 ability 상태를 슬롯 기준으로 캡처/복원한다.
/// 플레이어 루트 ability 저장과 분리하여, 무기 상태가 무기와 함께 이동하도록 만든다.
/// </summary>
[DisallowMultipleComponent]
public sealed class WeaponAbilityRuntimeStateBridge : MonoBehaviour, IWeaponRuntimeStateCapturer, IWeaponRuntimeStateRestorer
{
    private const string StateTypeKey = "WeaponAbilityRuntimePayload";

    [Header("Refs")]
    [SerializeField] private AbilitySystem abilitySystem;

    private void Awake()
    {
        if (abilitySystem == null)
            abilitySystem = GetComponent<AbilitySystem>();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (abilitySystem == null)
            abilitySystem = GetComponent<AbilitySystem>();
    }
#endif

    /// <summary>
    /// 책임 : 현재 인벤토리에 들어 있는 각 무기의 ability persistent state를 weaponRuntimeStates로 캡처한다.
    /// 플레이어 루트 ability 저장과 중복되지 않도록 무기 소유 능력만 따로 분리한다.
    /// </summary>
    public void CaptureWeaponRuntimeStates(
        WeaponInventory2D weaponInventory,
        List<WeaponRuntimeState> output)
    {
        if (weaponInventory == null || output == null || abilitySystem == null)
            return;

        for (int slotIndex = 0; slotIndex < weaponInventory.SlotCount; slotIndex++)
        {
            var weapon = weaponInventory.GetWeaponInSlot(slotIndex);
            if (weapon == null)
                continue;

            var payload = BuildPayload(weapon);
            if (payload == null)
                continue;

            output.Add(new WeaponRuntimeState
            {
                slotIndex = slotIndex,
                weaponId = weapon.weaponId,
                stateType = StateTypeKey,
                json = JsonUtility.ToJson(payload)
            });
        }

        CaptureCustomRuntimeState(weaponInventory, output);
    }

    /// <summary>
    /// 책임 : 저장된 무기 ability payload를 읽어 해당 슬롯 무기의 ability spec 상태를 복원한다.
    /// weaponInventory shell restore 이후 호출되어야 하며, weapon slot과 weaponId를 함께 검증한다.
    /// </summary>
    public void RestoreWeaponRuntimeState(
        WeaponInventory2D weaponInventory,
        WeaponRuntimeState state,
        IPlayerRuntimeResolver resolver)
    {
        if (weaponInventory == null || state == null || abilitySystem == null)
            return;

        if (string.Equals(state.stateType, StateTypeKey, StringComparison.Ordinal))
        {
            RestoreAbilityRuntimeState(weaponInventory, state);
            return;
        }

        RestoreCustomRuntimeState(weaponInventory, state);
    }

    /// <summary>
    /// 책임 : 저장된 무기 ability payload를 읽어 해당 슬롯 무기의 ability spec 상태를 복원한다.
    /// weaponInventory shell restore 이후 호출되어야 하며, weapon slot과 weaponId를 함께 검증한다.
    /// </summary>
    private void RestoreAbilityRuntimeState(
        WeaponInventory2D weaponInventory,
        WeaponRuntimeState state)
    {
        if (weaponInventory == null || state == null)
            return;

        var weapon = weaponInventory.GetWeaponInSlot(state.slotIndex);
        if (weapon == null)
        {
            Debug.LogWarning($"[WeaponAbilityRuntimeStateBridge] 슬롯 {state.slotIndex}에 무기가 없어 ability runtime 복원을 건너뜁니다.", this);
            return;
        }

        if (!string.IsNullOrEmpty(state.weaponId) &&
            !string.Equals(state.weaponId, weapon.weaponId, StringComparison.Ordinal))
        {
            Debug.LogWarning(
                $"[WeaponAbilityRuntimeStateBridge] weaponId 불일치로 복원을 건너뜁니다. saved={state.weaponId}, current={weapon.weaponId}",
                this);
            return;
        }

        if (string.IsNullOrWhiteSpace(state.json))
            return;

        var payload = JsonUtility.FromJson<WeaponAbilityRuntimePayload>(state.json);
        if (payload == null || payload.abilities == null)
            return;

        for (int i = 0; i < payload.abilities.Count; i++)
        {
            var abilityState = payload.abilities[i];
            if (abilityState == null)
                continue;

            abilitySystem.ImportPersistentState(
                abilityState,
                abilityId => ResolveAbilityOnWeapon(weapon, abilityId));
        }
    }

    /// <summary>
    /// 책임 : 슬롯별 persistent runtime data가 저장 계약을 구현한 경우 weaponRuntimeStates에 추가 저장한다.
    /// 활성/비활성 여부와 무관하게 인벤토리가 소유한 data를 기준으로 custom runtime state를 저장한다.
    /// </summary>
    private void CaptureCustomRuntimeState(
        WeaponInventory2D runtimeWeaponInventory,
        List<WeaponRuntimeState> output)
    {
        if (runtimeWeaponInventory == null || output == null)
            return;

        for (int slotIndex = 0; slotIndex < runtimeWeaponInventory.SlotCount; slotIndex++)
        {
            WeaponDefinition weapon = runtimeWeaponInventory.GetWeaponInSlot(slotIndex);
            if (weapon == null)
                continue;

            WeaponRuntimeData runtimeData = runtimeWeaponInventory.GetRuntimeDataInSlot(slotIndex);
            if (runtimeData is not IWeaponRuntimeStatePersistence persistence)
                continue;

            string json = persistence.CaptureStateJson();
            if (string.IsNullOrWhiteSpace(json))
                continue;

            output.Add(new WeaponRuntimeState
            {
                slotIndex = slotIndex,
                weaponId = weapon.weaponId,
                stateType = persistence.StateType,
                json = json
            });
        }
    }

    /// <summary>
    /// 책임 : 저장된 커스텀 runtime state payload를 슬롯이 소유한 persistent runtime data 구현체에 직접 복원한다.
    /// 프리팹 인스턴스 유무와 무관하게 inventory slot data를 진실한 상태 저장소로 유지한다.
    /// </summary>
    private void RestoreCustomRuntimeState(
        WeaponInventory2D runtimeWeaponInventory,
        WeaponRuntimeState state)
    {
        if (runtimeWeaponInventory == null || state == null)
            return;

        var weapon = runtimeWeaponInventory.GetWeaponInSlot(state.slotIndex);
        if (weapon == null)
            return;

        if (!string.IsNullOrEmpty(state.weaponId) &&
            !string.Equals(state.weaponId, weapon.weaponId, StringComparison.Ordinal))
        {
            return;
        }

        WeaponRuntimeData runtimeData = runtimeWeaponInventory.GetRuntimeDataInSlot(state.slotIndex);
        if (runtimeData is not IWeaponRuntimeStatePersistence persistence)
            return;

        if (!string.Equals(persistence.StateType, state.stateType, StringComparison.Ordinal))
            return;

        persistence.RestoreStateJson(state.json);
    }

    /// <summary>
    /// 책임 : 특정 무기가 가진 attack / skill1 / skill2의 persistent state를 payload로 묶는다.
    /// 무기 소유 능력만 저장하며, 플레이어 고유 ability는 포함하지 않는다.
    /// </summary>
    private WeaponAbilityRuntimePayload BuildPayload(WeaponDefinition weapon)
    {
        if (weapon == null)
            return null;

        var payload = new WeaponAbilityRuntimePayload
        {
            weaponId = weapon.weaponId
        };

        foreach (var ability in weapon.EnumerateGrantedAbilities())
            AddAbilityState(payload.abilities, ability);

        if (payload.abilities.Count == 0)
            return null;

        return payload;
    }

    /// <summary>
    /// 책임 : ability 하나의 persistent state를 payload 목록에 추가한다.
    /// 현재 무기가 소유 중이라면 spec이 존재하는 것이 정상이며, 없으면 경고만 남기고 건너뛴다.
    /// </summary>
    private void AddAbilityState(
        List<AbilityPersistentState> output,
        AbilityDefinition ability)
    {
        if (output == null || ability == null || abilitySystem == null)
            return;

        var state = abilitySystem.ExportPersistentState(ability);
        if (state == null)
        {
            Debug.LogWarning($"[WeaponAbilityRuntimeStateBridge] 무기 ability state export 실패: {ability.name}", this);
            return;
        }

        output.Add(state);
    }

    /// <summary>
    /// 책임 : 저장된 abilityId가 현재 무기의 attack / skill1 / skill2 중 어느 것인지 해석한다.
    /// 다른 무기의 ability가 잘못 주입되지 않도록 복원 범위를 현재 무기로 제한한다.
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
