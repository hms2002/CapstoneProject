using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임 :
/// - 현재 플레이어 인벤토리의 태양도/월영도 runtime data를 상태 HUD 엔트리로 투영한다.
/// - 열기/냉기 스택과 감쇠 시간을 이름, 서사 설명, 효과 설명이 포함된 공통 상태 모델로 바꿔 실제 전투 HUD에 노출한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class SunMoonStatusHudSource : MonoBehaviour, IStatusHudSource
{
    [SerializeField] private WeaponInventory2D inventory;
    [Header("Status Definitions")]
    [SerializeField] private StatusHudDefinition heatDefinition;
    [SerializeField] private StatusHudDefinition coldDefinition;

    public static SunMoonStatusHudSource GetOrAdd(GameObject owner)
    {
        if (owner == null)
            return null;

        SunMoonStatusHudSource existing = owner.GetComponent<SunMoonStatusHudSource>();
        return existing != null ? existing : owner.AddComponent<SunMoonStatusHudSource>();
    }

    private void Awake()
    {
        inventory ??= GetComponent<WeaponInventory2D>();
    }

    private void OnEnable()
    {
        inventory ??= GetComponent<WeaponInventory2D>();
        StatusHudSourceRegistry.RegisterSource(this);
    }

    private void OnDisable()
    {
        StatusHudSourceRegistry.UnregisterSource(this);
    }

    public void CollectStatusHudEntries(List<StatusHudEntry> buffer)
    {
        if (buffer == null || inventory == null)
            return;

        for (int i = 0; i < inventory.SlotCount; i++)
        {
            WeaponDefinition weapon = inventory.GetWeaponInSlot(i);
            WeaponRuntimeData runtimeData = inventory.GetRuntimeDataInSlot(i);
            if (weapon == null || runtimeData == null)
                continue;

            if (runtimeData is SunBladeRuntimeData sunData && sunData.HeatStacks > 0)
            {
                buffer.Add(CreateHeatEntry(i, weapon, sunData));
            }

            if (runtimeData is MoonBladeRuntimeData moonData && moonData.ColdStacks > 0)
            {
                buffer.Add(CreateColdEntry(i, weapon, moonData));
            }
        }
    }

    private StatusHudEntry CreateHeatEntry(int slotIndex, WeaponDefinition weapon, SunBladeRuntimeData sunData)
    {
        if (heatDefinition != null)
        {
            return heatDefinition.CreateEntry(
                $"weapon.slot{slotIndex}.sunblade",
                sunData.HeatStacks,
                sunData.HeatDecayRemaining,
                sunData.HeatDecaySeconds,
                sunData.HeatStacks >= sunData.MaxHeatStacks,
                true,
                iconOverride: weapon != null && weapon.Icon != null ? weapon.Icon : null,
                showDurationOverride: sunData.HeatDecaySeconds > 0f);
        }

        return new StatusHudEntry(
            $"weapon.slot{slotIndex}.sunblade",
            "heat",
            "열기",
            "날이 스친 자리마다 태양의 잔열이 남아, 칼끝이 아직 식지 않은 듯 숨을 쉽니다.",
            "열기가 높을수록 월영도의 일반 공격이 강화되고, 공명 피니시 조건으로 사용됩니다.",
            weapon != null ? weapon.Icon : null,
            sunData.HeatStacks,
            true,
            sunData.HeatDecayRemaining,
            sunData.HeatDecaySeconds,
            sunData.HeatDecaySeconds > 0f,
            StatusHudGroup.Weapon,
            100,
            sunData.HeatStacks >= sunData.MaxHeatStacks,
            true);
    }

    private StatusHudEntry CreateColdEntry(int slotIndex, WeaponDefinition weapon, MoonBladeRuntimeData moonData)
    {
        if (coldDefinition != null)
        {
            return coldDefinition.CreateEntry(
                $"weapon.slot{slotIndex}.moonblade",
                moonData.ColdStacks,
                moonData.ColdDecayRemaining,
                moonData.ColdDecaySeconds,
                moonData.ColdStacks >= moonData.MaxColdStacks,
                true,
                iconOverride: weapon != null && weapon.Icon != null ? weapon.Icon : null,
                showDurationOverride: moonData.ColdDecaySeconds > 0f);
        }

        return new StatusHudEntry(
            $"weapon.slot{slotIndex}.moonblade",
            "cold",
            "냉기",
            "달빛이 남긴 서늘함이 검신을 타고 흐르며, 베어낸 자리에 차가운 숨결을 남깁니다.",
            "냉기가 높을수록 태양도의 일반 공격이 강화되고, 공명 피니시 조건으로 사용됩니다.",
            weapon != null ? weapon.Icon : null,
            moonData.ColdStacks,
            true,
            moonData.ColdDecayRemaining,
            moonData.ColdDecaySeconds,
            moonData.ColdDecaySeconds > 0f,
            StatusHudGroup.Weapon,
            100,
            moonData.ColdStacks >= moonData.MaxColdStacks,
            true);
    }
}
