using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 책임 :
/// - WeaponDefinition authoring 시 loadout, 전략, 무기 프리팹 runtime state 구성이 서로 맞는지 한 화면에서 검증한다.
/// - loadout 우선순위와 프리팹 누락 같은 실제 세팅 실수를 inspector 단계에서 빠르게 드러내는 편집 진입점을 제공한다.
/// </summary>
[CustomEditor(typeof(WeaponDefinition))]
public sealed class WeaponDefinitionEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(8f);
        DrawValidationSection((WeaponDefinition)target);
    }

    private static void DrawValidationSection(WeaponDefinition weapon)
    {
        EditorGUILayout.LabelField("Authoring Validation", EditorStyles.boldLabel);

        List<string> warnings = new();
        CollectWarnings(weapon, warnings);

        if (warnings.Count == 0)
        {
            EditorGUILayout.HelpBox("검증 통과: 현재 WeaponDefinition 구성에 즉시 보이는 누락이 없습니다.", MessageType.Info);
            return;
        }

        foreach (string warning in warnings)
            EditorGUILayout.HelpBox(warning, MessageType.Warning);
    }

    private static void CollectWarnings(WeaponDefinition weapon, List<string> warnings)
    {
        if (weapon == null)
            return;

        if (weapon.abilityLoadout != null)
        {
            if (weapon.weaponPrefab == null)
                warnings.Add("Ability Loadout을 쓰는 무기인데 Weapon Prefab이 비어 있습니다.");

            if (weapon.attack != null || weapon.skill1 != null || weapon.skill2 != null)
                warnings.Add("Ability Loadout이 있으면 legacy attack/skill1/skill2보다 loadout 구성이 우선합니다.");

            foreach (string error in weapon.abilityLoadout.GetValidationErrors())
                warnings.Add($"Loadout: {error}");

            ValidateRuntimeDataRequirement(weapon, warnings);
            ValidateRuntimeProcessorRequirement(weapon, warnings);
            ValidateRuntimeStateRequirement(weapon, warnings);
            ValidateExecutorRequirement(weapon, warnings);
            return;
        }

        if (weapon.attack == null && weapon.skill1 == null && weapon.skill2 == null)
            warnings.Add("Ability Loadout도 없고 legacy attack/skill1/skill2도 모두 비어 있습니다.");
    }

    /// <summary>
    /// 책임 :
    /// - loadout이 기대하는 persistent runtime data 타입이 현재 factory 매핑과 일치하는지 검사한다.
    /// - 전용 슬롯 상태를 요구하는 무기가 기본 WeaponRuntimeData로만 생성되는 누락을 authoring 단계에서 먼저 경고한다.
    /// </summary>
    private static void ValidateRuntimeDataRequirement(WeaponDefinition weapon, List<string> warnings)
    {
        Type expectedRuntimeDataType = weapon.abilityLoadout.ExpectedRuntimeDataType;
        Type actualRuntimeDataType = WeaponRuntimeDataFactory.GetRuntimeDataTypeForWeapon(weapon);

        if (expectedRuntimeDataType == null)
            return;

        if (actualRuntimeDataType == null)
        {
            warnings.Add(
                $"현재 loadout {weapon.abilityLoadout.name}은 {expectedRuntimeDataType.Name} runtime data를 기대하지만, RuntimeDataFactory 매핑을 찾지 못했습니다.");
            return;
        }

        if (!expectedRuntimeDataType.IsAssignableFrom(actualRuntimeDataType))
        {
            warnings.Add(
                $"현재 loadout {weapon.abilityLoadout.name}은 {expectedRuntimeDataType.Name} runtime data를 기대하지만, RuntimeDataFactory는 {actualRuntimeDataType.Name}를 반환하도록 구성되어 있습니다.");
        }
    }

    /// <summary>
    /// 책임 :
    /// - loadout이 기대하는 runtime processor 타입이 현재 factory 매핑과 일치하는지 검사한다.
    /// - 비활성 슬롯 감쇠/창 만료 같은 시간 경과 규칙이 필요한 무기에서 processor 누락을 inspector 단계에서 먼저 드러낸다.
    /// </summary>
    private static void ValidateRuntimeProcessorRequirement(WeaponDefinition weapon, List<string> warnings)
    {
        Type expectedRuntimeProcessorType = weapon.abilityLoadout.ExpectedRuntimeProcessorType;
        if (expectedRuntimeProcessorType == null)
            return;

        Type actualRuntimeProcessorType = WeaponRuntimeProcessorFactory.GetRuntimeProcessorTypeForWeapon(weapon);
        if (actualRuntimeProcessorType == null)
        {
            warnings.Add(
                $"현재 loadout {weapon.abilityLoadout.name}은 {expectedRuntimeProcessorType.Name} runtime processor를 기대하지만, RuntimeProcessorFactory 매핑을 찾지 못했습니다.");
            return;
        }

        if (!expectedRuntimeProcessorType.IsAssignableFrom(actualRuntimeProcessorType))
        {
            warnings.Add(
                $"현재 loadout {weapon.abilityLoadout.name}은 {expectedRuntimeProcessorType.Name} runtime processor를 기대하지만, RuntimeProcessorFactory는 {actualRuntimeProcessorType.Name}를 반환하도록 구성되어 있습니다.");
        }
    }

    /// <summary>
    /// 책임 :
    /// - loadout 전략이 요구하는 runtime state 타입이 실제 무기 프리팹에 붙어 있는지 검사한다.
    /// - selector는 되는데 runtime state를 못 찾는 authoring 실수를 WeaponDefinition 단계에서 미리 경고한다.
    /// </summary>
    private static void ValidateRuntimeStateRequirement(WeaponDefinition weapon, List<string> warnings)
    {
        WeaponAbilitySelectionStrategy strategy = weapon.abilityLoadout.SelectionStrategy;
        Type expectedRuntimeStateType = strategy?.ExpectedRuntimeStateType;

        if (expectedRuntimeStateType == null || weapon.weaponPrefab == null)
            return;

        Component runtimeState = weapon.weaponPrefab.GetComponent(expectedRuntimeStateType)
            ?? weapon.weaponPrefab.GetComponentInChildren(expectedRuntimeStateType, true);

        if (runtimeState == null)
        {
            warnings.Add(
                $"현재 전략 {strategy.name}은 {expectedRuntimeStateType.Name} runtime state를 기대하지만, Weapon Prefab {weapon.weaponPrefab.name}에서 해당 컴포넌트를 찾지 못했습니다.");
        }
    }

    /// <summary>
    /// 책임 :
    /// - loadout 전략이 긴 실행을 위해 특정 executor를 요구하는 경우 실제 무기 프리팹에 그 컴포넌트가 있는지 검사한다.
    /// - selector/runtime state는 맞는데 시간축 실행기가 빠져 있는 authoring 실수를 WeaponDefinition 단계에서 미리 경고한다.
    /// </summary>
    private static void ValidateExecutorRequirement(WeaponDefinition weapon, List<string> warnings)
    {
        WeaponAbilitySelectionStrategy strategy = weapon.abilityLoadout.SelectionStrategy;
        Type expectedExecutorType = strategy?.ExpectedExecutorType;

        if (expectedExecutorType == null || weapon.weaponPrefab == null)
            return;

        Component executor = weapon.weaponPrefab.GetComponent(expectedExecutorType)
            ?? weapon.weaponPrefab.GetComponentInChildren(expectedExecutorType, true);

        if (executor == null)
        {
            warnings.Add(
                $"현재 전략 {strategy.name}은 {expectedExecutorType.Name} executor를 기대하지만, Weapon Prefab {weapon.weaponPrefab.name}에서 해당 컴포넌트를 찾지 못했습니다.");
        }
    }
}
