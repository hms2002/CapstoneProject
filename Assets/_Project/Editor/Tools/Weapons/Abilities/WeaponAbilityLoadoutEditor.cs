using System.Collections.Generic;
using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 책임 :
/// - WeaponAbilityLoadout 계열 SO가 전략, 슬롯 참조, 검증 결과를 읽기 쉬운 섹션으로 authoring 할 수 있게 한다.
/// - 전용 WAL 타입이 늘어나도 기본 inspector보다 더 명확한 검증 피드백을 제공하는 공용 편집 진입점을 담당한다.
/// </summary>
[CustomEditor(typeof(WeaponAbilityLoadout), true)]
public sealed class WeaponAbilityLoadoutEditor : Editor
{
    private SerializedProperty selectionStrategyProperty;

    private void OnEnable()
    {
        selectionStrategyProperty = serializedObject.FindProperty("selectionStrategy");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawStrategySection();

        if (target is EclipseSwordLoadout)
        {
            DrawEclipseSwordSections();
        }
        else if (target is SunBladeLoadout)
        {
            DrawSunBladeSections();
        }
        else if (target is MoonBladeLoadout)
        {
            DrawMoonBladeSections();
        }
        else if (target is MarkSwordLoadout)
        {
            DrawMarkSwordSections();
        }
        else if (target is ExecutionGunLoadout)
        {
            DrawExecutionGunSections();
        }
        else if (target is ExecutionerGreatswordLoadout)
        {
            DrawExecutionerGreatswordSections();
        }
        else if (target is ChainSpearLoadout)
        {
            DrawChainSpearSections();
        }
        else if (target is LightningSpearLoadout)
        {
            DrawLightningSpearSections();
        }
        else
        {
            DrawPropertiesExcluding(serializedObject, "m_Script", "selectionStrategy");
        }

        serializedObject.ApplyModifiedProperties();

        DrawValidationSection((WeaponAbilityLoadout)target);
    }

    private void DrawStrategySection()
    {
        EditorGUILayout.LabelField("Selection Strategy", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(selectionStrategyProperty);

        WeaponAbilityLoadout loadout = (WeaponAbilityLoadout)target;
        WeaponAbilitySelectionStrategy strategy = loadout.SelectionStrategy;

        if (strategy != null)
        {
            Type expectedLoadoutType = strategy.ExpectedLoadoutType;
            if (expectedLoadoutType != null)
            {
                EditorGUILayout.HelpBox(
                    $"이 전략은 {expectedLoadoutType.Name} 타입 WAL을 기대합니다.",
                    strategy.SupportsLoadout(loadout) ? MessageType.Info : MessageType.Warning);
            }

            if (strategy.ExpectedRuntimeStateType != null)
            {
                EditorGUILayout.HelpBox(
                    $"무기 프리팹에는 {strategy.ExpectedRuntimeStateType.Name} runtime state가 필요합니다.",
                    MessageType.Info);
            }

            if (strategy.ExpectedExecutorType != null)
            {
                EditorGUILayout.HelpBox(
                    $"무기 프리팹에는 {strategy.ExpectedExecutorType.Name} executor가 필요합니다.",
                    MessageType.Info);
            }
        }

        EditorGUILayout.Space(4f);
    }

    private void DrawEclipseSwordSections()
    {
        EditorGUILayout.LabelField("Base State", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("baseAttack"), new GUIContent("Base Attack"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("enterStance"), new GUIContent("Enter Stance"));
        EditorGUILayout.Space(4f);

        EditorGUILayout.LabelField("Eclipse Stance", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("stanceAttackA"), new GUIContent("Stance Attack A"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("stanceAttackB"), new GUIContent("Stance Attack B"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("bloomFinish"), new GUIContent("Bloom Finish"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("exitStance"), new GUIContent("Exit Stance"));
        EditorGUILayout.HelpBox("Skill1은 기본 상태에서 Enter Stance, 자세 상태에선 Exit 또는 Bloom Finish로 해석됩니다.", MessageType.None);
        EditorGUILayout.Space(4f);

        EditorGUILayout.LabelField("Other Slots", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("skill2DefaultAbility"), new GUIContent("Skill 2 Default"));
        EditorGUILayout.Space(4f);
    }

    private void DrawExecutionerGreatswordSections()
    {
        EditorGUILayout.LabelField("Core Actions", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("baseAttack"), new GUIContent("Base Attack"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("executionReadyAttack"), new GUIContent("Execution Ready"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("executionFinish"), new GUIContent("Execution Finish"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("executionFallback"), new GUIContent("Execution Fallback"));
        EditorGUILayout.HelpBox("Skill1은 기본 상태에서 Ready, 대기 상태에선 Finish 또는 Fallback으로 해석됩니다.", MessageType.None);
        EditorGUILayout.Space(4f);

        EditorGUILayout.LabelField("Other Slots", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("skill2DefaultAbility"), new GUIContent("Skill 2 Default"));
        EditorGUILayout.Space(4f);
    }

    private void DrawMarkSwordSections()
    {
        EditorGUILayout.LabelField("Core Actions", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("baseAttack"), new GUIContent("Base Attack"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultSkill1"), new GUIContent("Default Skill 1"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("reboundSlash"), new GUIContent("Rebound Slash"));
        EditorGUILayout.HelpBox("Skill1은 총이 연 반격 창이 있을 때만 Rebound Slash로 바뀝니다.", MessageType.None);
        EditorGUILayout.Space(4f);

        EditorGUILayout.LabelField("Runtime Defaults", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("maxMarkStacks"), new GUIContent("Max Mark Stacks"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("markDecaySeconds"), new GUIContent("Mark Decay Seconds"));
        EditorGUILayout.Space(4f);
    }

    private void DrawSunBladeSections()
    {
        EditorGUILayout.LabelField("Core Actions", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("baseAttack"), new GUIContent("Base Attack"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("heatedAttack"), new GUIContent("Heated Attack"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultSkill1"), new GUIContent("Default Skill 1"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("solarFinishStarter"), new GUIContent("Solar Finish Starter"));
        EditorGUILayout.HelpBox("Attack은 월영도의 냉기 수를 읽어 Heated Attack으로 바뀌고, Skill1은 양쪽 공명 조건이 충족되면 Solar Finish Starter로 바뀝니다.", MessageType.None);
        EditorGUILayout.Space(4f);

        EditorGUILayout.LabelField("Runtime Defaults", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("maxHeatStacks"), new GUIContent("Max Heat Stacks"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("heatDecaySeconds"), new GUIContent("Heat Decay Seconds"));
        EditorGUILayout.Space(4f);

        EditorGUILayout.LabelField("Cross-Weapon Thresholds", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("requiredMoonColdForHeatedAttack"), new GUIContent("Required Moon Cold For Heated Attack"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("requiredHeatForSolarFinish"), new GUIContent("Required Heat For Solar Finish"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("requiredMoonColdForSolarFinish"), new GUIContent("Required Moon Cold For Solar Finish"));
        EditorGUILayout.Space(4f);
    }

    private void DrawMoonBladeSections()
    {
        EditorGUILayout.LabelField("Core Actions", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("baseAttack"), new GUIContent("Base Attack"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("frostedAttack"), new GUIContent("Frosted Attack"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultSkill1"), new GUIContent("Default Skill 1"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("lunarFinishStarter"), new GUIContent("Lunar Finish Starter"));
        EditorGUILayout.HelpBox("Attack은 태양도의 열기 수를 읽어 Frosted Attack으로 바뀌고, Skill1은 양쪽 공명 조건이 충족되면 Lunar Finish Starter로 바뀝니다.", MessageType.None);
        EditorGUILayout.Space(4f);

        EditorGUILayout.LabelField("Runtime Defaults", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("maxColdStacks"), new GUIContent("Max Cold Stacks"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("coldDecaySeconds"), new GUIContent("Cold Decay Seconds"));
        EditorGUILayout.Space(4f);

        EditorGUILayout.LabelField("Cross-Weapon Thresholds", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("requiredSunHeatForFrostedAttack"), new GUIContent("Required Sun Heat For Frosted Attack"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("requiredColdForLunarFinish"), new GUIContent("Required Cold For Lunar Finish"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("requiredSunHeatForLunarFinish"), new GUIContent("Required Sun Heat For Lunar Finish"));
        EditorGUILayout.Space(4f);
    }

    private void DrawExecutionGunSections()
    {
        EditorGUILayout.LabelField("Core Actions", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("baseShot"), new GUIContent("Base Shot"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("executionShot"), new GUIContent("Execution Shot"));
        EditorGUILayout.HelpBox("Attack은 반대 슬롯 검의 표식 수를 읽어 Base Shot 또는 Execution Shot으로 바뀝니다.", MessageType.None);
        EditorGUILayout.Space(4f);

        EditorGUILayout.LabelField("Runtime Defaults", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("requiredMarksForExecutionShot"), new GUIContent("Required Marks"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("reboundWindowSeconds"), new GUIContent("Rebound Window Seconds"));
        EditorGUILayout.Space(4f);
    }

    private void DrawChainSpearSections()
    {
        EditorGUILayout.LabelField("Core Actions", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("baseAttack"), new GUIContent("Base Attack"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("chainThrow"), new GUIContent("Chain Throw"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("chainPull"), new GUIContent("Chain Pull"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("chainRecall"), new GUIContent("Chain Recall"));
        EditorGUILayout.HelpBox("Skill1은 기본 상태에서 Throw, 연결 상태에선 Pull로 해석되고 Skill2는 연결 상태에서만 Recall로 활성화됩니다. Throw는 executor가 링크 대기 시간을 운영합니다.", MessageType.None);
        EditorGUILayout.Space(4f);
    }

    private void DrawLightningSpearSections()
    {
        EditorGUILayout.LabelField("Core Actions", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("baseAttack"), new GUIContent("Base Attack"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("markRushOrSweep"), new GUIContent("Q / Skill1 Mark Rush Or Sweep"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("markRain"), new GUIContent("E / Skill2 Mark Rain"));
        EditorGUILayout.Space(4f);

        EditorGUILayout.LabelField("Mark Authoring", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("markPrefab"), new GUIContent("Mark Prefab"));
        EditorGUILayout.Space(4f);

        EditorGUILayout.LabelField("Q - Mark Rush", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("cursorSelectRadius"), new GUIContent("Cursor Select Radius"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("markRushRange"), new GUIContent("Mark Rush Range"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("markRushDuration"), new GUIContent("Mark Rush Duration"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("markRushBodyRadius"), new GUIContent("Rush Body Radius"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("markRushArrivalHitDelay"), new GUIContent("Arrival Hit Delay"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("markRushInternalDelay"), new GUIContent("Internal Input Delay"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("markRushHit"), new GUIContent("Rush Hit"), includeChildren: true);
        EditorGUILayout.Space(4f);

        EditorGUILayout.LabelField("Q - No Mark Sweep", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("noMarkSweepHit"), new GUIContent("No Mark Sweep Hit"), includeChildren: true);
        EditorGUILayout.Space(4f);

        EditorGUILayout.LabelField("E - Mark Rain", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("markLifetimeSeconds"), new GUIContent("Mark Lifetime Seconds"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("markRainCount"), new GUIContent("Mark Rain Count"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("markRainDelay"), new GUIContent("Mark Rain Delay"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("fallbackCombatRadius"), new GUIContent("Fallback Combat Radius"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("minPlayerDistance"), new GUIContent("Min Player Distance"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("minMarkSpacing"), new GUIContent("Min Mark Spacing"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("landingProbeRadius"), new GUIContent("Landing Probe Radius"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("candidateSamples"), new GUIContent("Candidate Samples"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("landingHit"), new GUIContent("Landing Hit"), includeChildren: true);
        EditorGUILayout.Space(4f);

        EditorGUILayout.LabelField("Placement / Movement Layers", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("hardBlockLayers"), new GUIContent("Hard Block Layers"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("softBlockLayers"), new GUIContent("Soft Block Layers"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("landingBlockedLayers"), new GUIContent("Landing Blocked Layers"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("requiredGroundLayers"), new GUIContent("Required Ground Layers"));
        EditorGUILayout.HelpBox("Same-room Q rush ignores Soft Block Layers only for the path check. Landing and fallback placement still reject Hard/Soft blocked positions.", MessageType.None);
        EditorGUILayout.Space(4f);

        EditorGUILayout.LabelField("Feedback Prefabs", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("rushRangeIndicatorPrefab"), new GUIContent("Rush Range Indicator Prefab"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("selectedMarkIndicatorPrefab"), new GUIContent("Selected Mark Indicator Prefab"));
        EditorGUILayout.Space(4f);
    }

    private static void DrawValidationSection(WeaponAbilityLoadout loadout)
    {
        List<string> errors = new(loadout.GetValidationErrors());

        if (errors.Count == 0)
        {
            EditorGUILayout.HelpBox("검증 통과: 현재 WAL 구성에 필수 누락이 없습니다.", MessageType.Info);
            return;
        }

        foreach (string error in errors)
            EditorGUILayout.HelpBox(error, MessageType.Warning);
    }
}
