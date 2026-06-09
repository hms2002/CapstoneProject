using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// - 전투 밸런싱에 필요한 몬스터 Attribute override profile 상태를 한 창에서 점검한다.
/// - 몬스터 본체 프리팹에 누락된 전용 AttributeInitProfileSO를 생성하고 연결한다.
/// </summary>
public sealed class CombatBalanceEditorWindow : EditorWindow
{
    private static readonly string[] EnemyPrefabRoots = { "Assets/_Project/Prefabs/Monsters", "Assets/_Project/Prefabs/Bosses" };
    private const string StageHpScalingSettingsPath = "Assets/_Project/Resources/MonsterStageHpScalingSettings.asset";
    private const string MobProfileFolder = "Assets/_Project/Data/Attributes/InitProfiles/Enemies/Mobs";
    private const string BossProfileFolder = "Assets/_Project/Data/Attributes/InitProfiles/Enemies/Bosses";
    private const string MiscProfileFolder = "Assets/_Project/Data/Attributes/InitProfiles/Enemies/Misc";
    private const string WeaponDefinitionRoot = "Assets/_Project/Data/Items/Weapons/Definitions";

    private readonly List<MonsterProfileRow> monsterRows = new();
    private readonly List<WeaponBalanceRow> weaponRows = new();
    private Vector2 monsterScroll;
    private Vector2 detailScroll;
    private Vector2 weaponScroll;
    private Vector2 weaponDetailScroll;
    private string monsterSearch = string.Empty;
    private string weaponSearch = string.Empty;
    private MonsterProfileRow selectedRow;
    private WeaponBalanceRow selectedWeaponRow;
    private MonsterStageHpScalingSettings stageHpScalingSettings;
    private BalanceToolTab activeTab = BalanceToolTab.MonsterStats;

    [MenuItem("Tools/Balance/Monster Stats")]
    public static void OpenMonsterStats()
    {
        OpenWindow(BalanceToolTab.MonsterStats);
    }

    [MenuItem("Tools/Balance/Weapon Stats")]
    public static void OpenWeaponStats()
    {
        OpenWindow(BalanceToolTab.WeaponStats);
    }

    private static void OpenWindow(BalanceToolTab initialTab)
    {
        CombatBalanceEditorWindow window = GetWindow<CombatBalanceEditorWindow>("Balance Tools");
        window.minSize = new Vector2(720f, 520f);
        window.activeTab = initialTab;
        window.RefreshAll();
        window.Show();
    }

    private void OnEnable()
    {
        LoadStageHpScalingSettings();
        RefreshAll();
    }

    private void OnGUI()
    {
        DrawToolbar();
        DrawMonsterStatsTab();
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                RefreshAll();

            GUILayout.Space(8f);
            activeTab = (BalanceToolTab)GUILayout.Toolbar((int)activeTab, new[] { "Monster Stats", "Weapon Stats" }, EditorStyles.toolbarButton, GUILayout.Width(230f));
            GUILayout.FlexibleSpace();
        }
    }

    private void DrawMonsterStatsTab()
    {
        switch (activeTab)
        {
            case BalanceToolTab.WeaponStats:
                DrawWeaponStatsTab();
                break;
            case BalanceToolTab.MonsterStats:
            default:
                DrawMonsterStatsContent();
                break;
        }
    }

    private void DrawMonsterStatsContent()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawStageHpScalingSettings();
            DrawMonsterList();
            DrawSelectedMonsterDetail();
        }
    }

    private void DrawWeaponStatsTab()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawWeaponList();
            DrawSelectedWeaponDetail();
        }
    }

    private void DrawWeaponList()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(330f)))
        {
            EditorGUILayout.LabelField("Weapon Definitions", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                weaponSearch = EditorGUILayout.TextField(weaponSearch, GUI.skin.FindStyle("ToolbarSearchTextField") ?? EditorStyles.textField);
                if (GUILayout.Button("Clear", GUILayout.Width(52f)))
                    weaponSearch = string.Empty;
            }

            EditorGUILayout.Space(4f);

            weaponScroll = EditorGUILayout.BeginScrollView(weaponScroll);
            foreach (WeaponBalanceRow row in GetFilteredWeaponRows())
            {
                DrawWeaponRowButton(row);
            }
            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawWeaponRowButton(WeaponBalanceRow row)
    {
        GUIStyle style = row == selectedWeaponRow ? EditorStyles.helpBox : GUI.skin.button;
        using (new EditorGUILayout.HorizontalScope(style))
        {
            Rect thumbnailRect = GUILayoutUtility.GetRect(32f, 32f, GUILayout.Width(36f), GUILayout.Height(36f));
            DrawListPreviewSprite(thumbnailRect, row.Icon);

            if (GUILayout.Button(row.DisplayName, GUIStyle.none, GUILayout.Height(36f)))
                selectedWeaponRow = row;

            GUILayout.Label(row.AssetName, EditorStyles.miniLabel, GUILayout.Width(120f));
        }
    }

    private void DrawSelectedWeaponDetail()
    {
        weaponDetailScroll = EditorGUILayout.BeginScrollView(weaponDetailScroll);
        using (new EditorGUILayout.VerticalScope())
        {
            if (selectedWeaponRow == null)
            {
                EditorGUILayout.HelpBox("왼쪽에서 무기 정의를 선택하세요.", MessageType.Info);
                EditorGUILayout.EndScrollView();
                return;
            }

            WeaponDefinition weapon = selectedWeaponRow.Weapon;
            SerializedObject serializedWeapon = new(weapon);

            EditorGUILayout.LabelField(weapon.name, EditorStyles.boldLabel);
            EditorGUILayout.ObjectField("Definition", weapon, typeof(WeaponDefinition), false);
            EditorGUILayout.LabelField("Path", selectedWeaponRow.AssetPath);

            EditorGUILayout.Space(6f);
            DrawWeaponVisualPreview(weapon);

            EditorGUILayout.Space(8f);
            DrawWeaponDefinitionFields(serializedWeapon);

            EditorGUILayout.Space(8f);
            DrawWeaponStatModifiers(serializedWeapon);

            serializedWeapon.ApplyModifiedProperties();

            EditorGUILayout.Space(8f);
            DrawWeaponDirectAbilities(weapon);

            EditorGUILayout.Space(8f);
            DrawWeaponLoadout(weapon);

            EditorGUILayout.Space(8f);
            DrawWeaponAbilityBalanceParameters(weapon);
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawWeaponVisualPreview(WeaponDefinition weapon)
    {
        EditorGUILayout.LabelField("Weapon Visual Preview", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            Rect previewRect = GUILayoutUtility.GetRect(64f, 64f, GUILayout.Width(80f), GUILayout.Height(80f));
            DrawListPreviewSprite(previewRect, weapon != null ? weapon.icon : null);

            using (new EditorGUILayout.VerticalScope())
            {
                EditorGUILayout.LabelField("Icon", weapon != null && weapon.icon != null ? weapon.icon.name : "(None)");
                EditorGUILayout.ObjectField("Prefab", weapon != null ? weapon.weaponPrefab : null, typeof(GameObject), false);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUI.enabled = weapon != null && weapon.icon != null;
                    if (GUILayout.Button("Ping Icon", GUILayout.Width(90f)))
                        EditorGUIUtility.PingObject(weapon.icon);

                    GUI.enabled = weapon != null && weapon.weaponPrefab != null;
                    if (GUILayout.Button("Ping Prefab", GUILayout.Width(90f)))
                        EditorGUIUtility.PingObject(weapon.weaponPrefab);

                    GUI.enabled = true;
                }
            }
        }
    }

    private static void DrawWeaponDefinitionFields(SerializedObject serializedWeapon)
    {
        EditorGUILayout.LabelField("Weapon Definition", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(serializedWeapon.FindProperty("weaponId"));
        EditorGUILayout.PropertyField(serializedWeapon.FindProperty("displayName"));
        EditorGUILayout.PropertyField(serializedWeapon.FindProperty("icon"));
        EditorGUILayout.PropertyField(serializedWeapon.FindProperty("weaponPrefab"));
        EditorGUILayout.PropertyField(serializedWeapon.FindProperty("displayVisualProfile"));
        EditorGUILayout.PropertyField(serializedWeapon.FindProperty("swapSoundOverride"));
        EditorGUILayout.Space(4f);
        EditorGUILayout.PropertyField(serializedWeapon.FindProperty("attack"));
        EditorGUILayout.PropertyField(serializedWeapon.FindProperty("skill1"));
        EditorGUILayout.PropertyField(serializedWeapon.FindProperty("skill2"));
        EditorGUILayout.PropertyField(serializedWeapon.FindProperty("abilityLoadout"));
        if (EditorGUI.EndChangeCheck())
            serializedWeapon.ApplyModifiedProperties();
    }

    private static void DrawWeaponStatModifiers(SerializedObject serializedWeapon)
    {
        EditorGUILayout.LabelField("Equipped Stat Modifiers", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(serializedWeapon.FindProperty("statModifiers"), includeChildren: true);
        if (EditorGUI.EndChangeCheck())
            serializedWeapon.ApplyModifiedProperties();
    }

    private void DrawWeaponDirectAbilities(WeaponDefinition weapon)
    {
        EditorGUILayout.LabelField("Direct Slot Abilities", EditorStyles.boldLabel);

        DrawWeaponAbilitySlotRow("Attack", weapon != null ? weapon.attack : null);
        DrawWeaponAbilitySlotRow("Skill1", weapon != null ? weapon.skill1 : null);
        DrawWeaponAbilitySlotRow("Skill2", weapon != null ? weapon.skill2 : null);
    }

    private static void DrawWeaponAbilitySlotRow(string label, AbilityDefinition ability)
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            GUILayout.Label(label, GUILayout.Width(56f));
            DrawAbilityTimingFields(ability);
        }
    }

    private void DrawWeaponLoadout(WeaponDefinition weapon)
    {
        EditorGUILayout.LabelField("Weapon Ability Loadout", EditorStyles.boldLabel);
        WeaponAbilityLoadout loadout = weapon != null ? weapon.abilityLoadout : null;
        EditorGUILayout.ObjectField("Loadout", loadout, typeof(WeaponAbilityLoadout), false);

        if (loadout == null)
        {
            EditorGUILayout.HelpBox("Ability Loadout이 없습니다. 직접 슬롯 AD만 사용합니다.", MessageType.Info);
            return;
        }

        SerializedObject serializedLoadout = new(loadout);
        EditorGUILayout.PropertyField(serializedLoadout.FindProperty("selectionStrategy"));
        serializedLoadout.ApplyModifiedProperties();

        List<string> validationErrors = loadout.GetValidationErrors().ToList();
        if (validationErrors.Count > 0)
        {
            foreach (string error in validationErrors)
                EditorGUILayout.HelpBox(error, MessageType.Warning);
        }

        EditorGUILayout.LabelField("Granted Ability Definitions", EditorStyles.boldLabel);
        foreach (AbilityDefinition ability in loadout.EnumerateGrantedAbilities().Where(ability => ability != null).Distinct())
        {
            DrawAbilityTimingFields(ability);
        }
    }

    private static void DrawAbilityTimingFields(AbilityDefinition ability)
    {
        EditorGUILayout.ObjectField(ability, typeof(AbilityDefinition), false, GUILayout.Width(220f));
        if (ability == null)
        {
            GUILayout.Label("(None)", EditorStyles.miniLabel);
            return;
        }

        EditorGUI.BeginChangeCheck();
        float cooldown = Mathf.Max(0f, EditorGUILayout.FloatField("Cooldown", ability.cooldown, GUILayout.Width(170f)));
        float castTime = Mathf.Max(0f, EditorGUILayout.FloatField("Cast", ability.castTime, GUILayout.Width(145f)));
        float recovery = Mathf.Max(0f, EditorGUILayout.FloatField("Recovery", ability.recoveryTime, GUILayout.Width(170f)));
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(ability, "Edit Weapon Ability Timing");
            ability.cooldown = cooldown;
            ability.castTime = castTime;
            ability.recoveryTime = recovery;
            EditorUtility.SetDirty(ability);
        }

        if (GUILayout.Button("Ping", GUILayout.Width(50f)))
            EditorGUIUtility.PingObject(ability);
    }

    private void DrawWeaponAbilityBalanceParameters(WeaponDefinition weapon)
    {
        EditorGUILayout.LabelField("Ability Balance Parameters", EditorStyles.boldLabel);
        List<WeaponAbilityBalanceBlock> blocks = BuildWeaponAbilityBalanceBlocks(weapon);
        if (blocks.Count == 0)
        {
            EditorGUILayout.HelpBox("이 무기에서 참조하는 AbilityDefinition이 없습니다.", MessageType.Info);
            return;
        }

        Dictionary<ScaledStatFormula, int> formulaUseCounts = CountFormulaReferences(blocks);
        foreach (WeaponAbilityBalanceBlock block in blocks)
            DrawWeaponAbilityBalanceBlock(block, formulaUseCounts);
    }

    private static List<WeaponAbilityBalanceBlock> BuildWeaponAbilityBalanceBlocks(WeaponDefinition weapon)
    {
        List<WeaponAbilityBalanceBlock> blocks = new();
        Dictionary<AbilityDefinition, WeaponAbilityBalanceBlock> byAbility = new();

        AddWeaponAbilityBalanceBlock(byAbility, blocks, weapon != null ? weapon.attack : null, "Direct: Attack");
        AddWeaponAbilityBalanceBlock(byAbility, blocks, weapon != null ? weapon.skill1 : null, "Direct: Skill1");
        AddWeaponAbilityBalanceBlock(byAbility, blocks, weapon != null ? weapon.skill2 : null, "Direct: Skill2");

        WeaponAbilityLoadout loadout = weapon != null ? weapon.abilityLoadout : null;
        if (loadout != null)
        {
            foreach (AbilityDefinition ability in loadout.EnumerateGrantedAbilities().Where(ability => ability != null))
                AddWeaponAbilityBalanceBlock(byAbility, blocks, ability, "Loadout Grant");
        }

        return blocks;
    }

    private static void AddWeaponAbilityBalanceBlock(
        Dictionary<AbilityDefinition, WeaponAbilityBalanceBlock> byAbility,
        List<WeaponAbilityBalanceBlock> blocks,
        AbilityDefinition ability,
        string sourceLabel)
    {
        if (ability == null)
            return;

        if (byAbility.TryGetValue(ability, out WeaponAbilityBalanceBlock existing))
        {
            existing.AddSourceLabel(sourceLabel);
            return;
        }

        WeaponAbilityBalanceBlock block = new(ability, sourceLabel);
        byAbility.Add(ability, block);
        blocks.Add(block);
    }

    private static Dictionary<ScaledStatFormula, int> CountFormulaReferences(List<WeaponAbilityBalanceBlock> blocks)
    {
        Dictionary<ScaledStatFormula, int> counts = new();
        foreach (WeaponAbilityBalanceBlock block in blocks)
        {
            if (block.Ability == null || block.Ability.sourceObject is not ScriptableObject sourceObject)
                continue;

            foreach (ScaledFormulaReference formulaReference in ScaledFormulaEditorDrawer.FindFormulaReferences(sourceObject))
            {
                if (formulaReference.Formula == null)
                    continue;

                counts.TryGetValue(formulaReference.Formula, out int count);
                counts[formulaReference.Formula] = count + 1;
            }
        }

        return counts;
    }

    private static void DrawWeaponAbilityBalanceBlock(
        WeaponAbilityBalanceBlock block,
        Dictionary<ScaledStatFormula, int> formulaUseCounts)
    {
        AbilityDefinition ability = block.Ability;
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.ObjectField(ability, typeof(AbilityDefinition), false, GUILayout.Width(240f));
                GUILayout.Label(block.SourceLabel, EditorStyles.miniLabel, GUILayout.MinWidth(140f));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Ping AD", GUILayout.Width(70f)))
                    EditorGUIUtility.PingObject(ability);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawAbilityTimingFields(ability);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.ObjectField("Logic (Read Only)", ability != null ? ability.logic : null, typeof(AbilityLogic), false);
                GUI.enabled = ability != null && ability.logic != null;
                if (GUILayout.Button("Ping Logic", GUILayout.Width(82f)))
                    EditorGUIUtility.PingObject(ability.logic);
                GUI.enabled = true;
            }

            UnityEngine.Object sourceObject = ability != null ? ability.sourceObject : null;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.ObjectField("Source Object", sourceObject, typeof(UnityEngine.Object), false);
                GUI.enabled = sourceObject != null;
                if (GUILayout.Button("Ping Source", GUILayout.Width(90f)))
                    EditorGUIUtility.PingObject(sourceObject);
                GUI.enabled = true;
            }

            if (sourceObject == null)
            {
                EditorGUILayout.HelpBox("sourceObject가 없어 AD timing만 편집합니다. 공유 AbilityLogic은 여기서 직접 편집하지 않습니다.", MessageType.Info);
                return;
            }

            if (sourceObject is not ScriptableObject scriptableSource)
            {
                EditorGUILayout.HelpBox("sourceObject가 ScriptableObject가 아니므로 자동 밸런싱 필드를 편집하지 않습니다.", MessageType.Info);
                return;
            }

            DrawScriptableSourceBalanceFields(scriptableSource, formulaUseCounts);
        }
    }

    private static void DrawScriptableSourceBalanceFields(
        ScriptableObject sourceObject,
        Dictionary<ScaledStatFormula, int> formulaUseCounts)
    {
        EditorGUILayout.LabelField("Tunable Fields", EditorStyles.boldLabel);
        SerializedObject serializedSource = new(sourceObject);
        serializedSource.Update();

        List<string> tunablePropertyPaths = BalanceTunablePropertyFilter.FindTunablePropertyPaths(serializedSource);
        if (tunablePropertyPaths.Count == 0)
            EditorGUILayout.HelpBox("자동 추출된 일반 밸런싱 수치 필드가 없습니다.", MessageType.None);

        EditorGUI.BeginChangeCheck();
        foreach (string propertyPath in tunablePropertyPaths)
        {
            SerializedProperty property = serializedSource.FindProperty(propertyPath);
            if (property == null)
                continue;

            DrawReadableTunableProperty(property);
        }

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(sourceObject, "Edit Weapon Ability Source Data");
            serializedSource.ApplyModifiedProperties();
            EditorUtility.SetDirty(sourceObject);
        }
        else
        {
            serializedSource.ApplyModifiedProperties();
        }

        List<ScaledFormulaReference> formulaReferences = ScaledFormulaEditorDrawer.FindFormulaReferences(sourceObject);
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Scaled Stat Formulas", EditorStyles.boldLabel);
        if (formulaReferences.Count == 0)
        {
            EditorGUILayout.HelpBox("이 sourceObject에서 ScaledStatFormula 참조를 찾지 못했습니다.", MessageType.None);
            return;
        }

        foreach (ScaledFormulaReference formulaReference in formulaReferences)
            ScaledFormulaEditorDrawer.DrawFormulaReference(formulaReference, formulaUseCounts);
    }

    private static void DrawReadableTunableProperty(SerializedProperty property)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(BuildReadablePropertyLabel(property.propertyPath), GUILayout.Width(280f));
            EditorGUILayout.PropertyField(property, GUIContent.none, includeChildren: true);
        }
    }

    private static string BuildReadablePropertyLabel(string propertyPath)
    {
        if (string.IsNullOrWhiteSpace(propertyPath))
            return "(Unknown)";

        string[] rawParts = propertyPath.Split('.');
        List<string> labels = new();
        for (int i = 0; i < rawParts.Length; i++)
        {
            string part = rawParts[i];
            if (part == "Array")
                continue;

            if (part.StartsWith("data[", StringComparison.Ordinal))
            {
                int index = ParseArrayIndex(part);
                string context = labels.Count > 0 ? labels[labels.Count - 1] : "Element";
                if (labels.Count > 0)
                    labels.RemoveAt(labels.Count - 1);

                labels.Add($"{SingularizeLabel(context)} {index + 1}");
                continue;
            }

            labels.Add(ObjectNames.NicifyVariableName(part));
        }

        return string.Join(" / ", labels);
    }

    private static int ParseArrayIndex(string arrayDataPart)
    {
        int start = arrayDataPart.IndexOf('[', StringComparison.Ordinal);
        int end = arrayDataPart.IndexOf(']', StringComparison.Ordinal);
        if (start < 0 || end <= start)
            return 0;

        string indexText = arrayDataPart.Substring(start + 1, end - start - 1);
        return int.TryParse(indexText, out int index) ? Mathf.Max(0, index) : 0;
    }

    private static string SingularizeLabel(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
            return "Element";

        return label.EndsWith("s", StringComparison.OrdinalIgnoreCase) && label.Length > 1
            ? label.Substring(0, label.Length - 1)
            : label;
    }

    private void DrawStageHpScalingSettings()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(260f)))
        {
            EditorGUILayout.LabelField("Stage HP Scaling", EditorStyles.boldLabel);

            if (stageHpScalingSettings == null)
            {
                EditorGUILayout.HelpBox("MonsterStageHpScalingSettings asset이 없습니다.", MessageType.Warning);
                if (GUILayout.Button("Reload Settings"))
                    LoadStageHpScalingSettings();
                return;
            }

            EditorGUILayout.ObjectField("Settings", stageHpScalingSettings, typeof(MonsterStageHpScalingSettings), false);

            SerializedObject serializedSettings = new(stageHpScalingSettings);
            SerializedProperty enabledProperty = serializedSettings.FindProperty("enabled");
            SerializedProperty bonusProperty = serializedSettings.FindProperty("hpMultiplierPerClearedStage");
            SerializedProperty attackSpeedBonusProperty = serializedSettings.FindProperty("attackSpeedMultiplierPerClearedStage");
            SerializedProperty scaleWarningProperty = serializedSettings.FindProperty("scaleAttackWarning");
            SerializedProperty scaleRecoveryProperty = serializedSettings.FindProperty("scaleAttackRecovery");
            SerializedProperty scaleIntervalProperty = serializedSettings.FindProperty("scaleAttackInterval");
            SerializedProperty scaleAbilityCastProperty = serializedSettings.FindProperty("scaleAbilityCast");
            SerializedProperty scaleAbilityRecoveryProperty = serializedSettings.FindProperty("scaleAbilityRecovery");
            SerializedProperty scaleAbilityCooldownProperty = serializedSettings.FindProperty("scaleAbilityCooldown");
            SerializedProperty minimumScaledSecondsProperty = serializedSettings.FindProperty("minimumScaledSeconds");
            SerializedProperty logStageScalingDebugProperty = serializedSettings.FindProperty("logStageScalingDebug");
            SerializedProperty logCombatTimingDebugProperty = serializedSettings.FindProperty("logCombatTimingDebug");

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(enabledProperty, new GUIContent("Enable Stage HP Scaling"));
            EditorGUILayout.PropertyField(bonusProperty, new GUIContent("HP Bonus Per Stage"));
            EditorGUILayout.PropertyField(attackSpeedBonusProperty, new GUIContent("Attack Speed Bonus Per Stage"));
            EditorGUILayout.PropertyField(
                scaleWarningProperty,
                new GUIContent(
                    "Telegraph Uses Attack Speed",
                    "몬스터 공격 경고/telegraph 시간이 공격속도 보정에 따라 짧아질지 결정합니다. 몬스터별 override가 있으면 그 값이 우선됩니다."));
            EditorGUILayout.PropertyField(scaleRecoveryProperty, new GUIContent("Scale Recovery"));
            EditorGUILayout.PropertyField(scaleIntervalProperty, new GUIContent("Scale Interval"));
            EditorGUILayout.PropertyField(scaleAbilityCastProperty, new GUIContent("Scale Ability Cast"));
            EditorGUILayout.PropertyField(scaleAbilityRecoveryProperty, new GUIContent("Scale Ability Recovery"));
            EditorGUILayout.PropertyField(scaleAbilityCooldownProperty, new GUIContent("Scale Ability Cooldown"));
            EditorGUILayout.PropertyField(minimumScaledSecondsProperty, new GUIContent("Min Scaled Seconds"));
            EditorGUILayout.Space(3f);
            EditorGUILayout.PropertyField(logStageScalingDebugProperty, new GUIContent("Log Stage Scaling"));
            EditorGUILayout.PropertyField(logCombatTimingDebugProperty, new GUIContent("Log Combat Timing"));

            if (EditorGUI.EndChangeCheck())
            {
                serializedSettings.ApplyModifiedProperties();
                EditorUtility.SetDirty(stageHpScalingSettings);
            }

            float bonus = ResolvePreviewHpBonusPerStage();
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Preview Multipliers");
            EditorGUILayout.LabelField("Stage 0", "x1.00");
            EditorGUILayout.LabelField("Stage 1", $"x{CalculatePreviewStageMultiplier(1, bonus):0.##}");
            EditorGUILayout.LabelField("Stage 2", $"x{CalculatePreviewStageMultiplier(2, bonus):0.##}");
            EditorGUILayout.LabelField("Stage 3", $"x{CalculatePreviewStageMultiplier(3, bonus):0.##}");
            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Attack Speed");
            EditorGUILayout.LabelField("Stage 0", "x1.00");
            EditorGUILayout.LabelField("Stage 1", $"x{stageHpScalingSettings.CalculateStageAttackSpeedMultiplier(1):0.##}");
            EditorGUILayout.LabelField("Stage 2", $"x{stageHpScalingSettings.CalculateStageAttackSpeedMultiplier(2):0.##}");
            EditorGUILayout.LabelField("Stage 3", $"x{stageHpScalingSettings.CalculateStageAttackSpeedMultiplier(3):0.##}");

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Ping"))
                    EditorGUIUtility.PingObject(stageHpScalingSettings);

                if (GUILayout.Button("Save"))
                    AssetDatabase.SaveAssets();
            }
        }
    }

    private void DrawMonsterList()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(330f)))
        {
            EditorGUILayout.LabelField("Monster Override Profiles", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                monsterSearch = EditorGUILayout.TextField(monsterSearch, GUI.skin.FindStyle("ToolbarSearchTextField") ?? EditorStyles.textField);
                if (GUILayout.Button("Clear", GUILayout.Width(52f)))
                    monsterSearch = string.Empty;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create Missing Override Profiles"))
                {
                    CreateMissingOverrideProfiles();
                }
            }

            EditorGUILayout.Space(4f);

            monsterScroll = EditorGUILayout.BeginScrollView(monsterScroll);
            foreach (MonsterProfileRow row in GetFilteredRows())
            {
                DrawMonsterRowButton(row);
            }
            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawMonsterRowButton(MonsterProfileRow row)
    {
        GUIStyle style = row == selectedRow ? EditorStyles.helpBox : GUI.skin.button;
        using (new EditorGUILayout.HorizontalScope(style))
        {
            Rect thumbnailRect = GUILayoutUtility.GetRect(32f, 32f, GUILayout.Width(36f), GUILayout.Height(36f));
            DrawListPreviewSprite(thumbnailRect, row.ListPreviewSprite);

            if (GUILayout.Button(row.Prefab.name, GUIStyle.none, GUILayout.Height(36f)))
                selectedRow = row;

            GUILayout.Label(row.StatusLabel, GetStatusStyle(row.Status), GUILayout.Width(86f));
        }
    }

    private static void DrawListPreviewSprite(Rect rect, Sprite sprite)
    {
        Rect paddedRect = new(rect.x + 2f, rect.y + 2f, 32f, 32f);
        EditorGUI.DrawRect(paddedRect, new Color(0.13f, 0.13f, 0.13f, 1f));
        if (sprite == null || sprite.texture == null)
        {
            EditorGUI.LabelField(paddedRect, "-", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        Rect spriteTextureRect = sprite.textureRect;
        Rect fittedRect = FitRectByAspect(paddedRect, spriteTextureRect.width, spriteTextureRect.height);
        Rect textureCoords = new(
            spriteTextureRect.x / sprite.texture.width,
            spriteTextureRect.y / sprite.texture.height,
            spriteTextureRect.width / sprite.texture.width,
            spriteTextureRect.height / sprite.texture.height);

        GUI.DrawTextureWithTexCoords(fittedRect, sprite.texture, textureCoords, alphaBlend: true);
    }

    private void DrawSelectedMonsterDetail()
    {
        detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
        using (new EditorGUILayout.VerticalScope())
        {
            if (selectedRow == null)
            {
                EditorGUILayout.HelpBox("왼쪽에서 몬스터 프리팹을 선택하세요.", MessageType.Info);
                EditorGUILayout.EndScrollView();
                return;
            }

            EditorGUILayout.LabelField(selectedRow.Prefab.name, EditorStyles.boldLabel);
            EditorGUILayout.ObjectField("Prefab", selectedRow.Prefab, typeof(GameObject), false);
            EditorGUILayout.LabelField("Status", selectedRow.StatusLabel);
            EditorGUILayout.LabelField("Expected Profile", selectedRow.ExpectedProfilePath);

            EditorGUILayout.Space(6f);
            DrawMonsterVisualPreview(selectedRow);

            EditorGUILayout.Space(8f);
            DrawMonsterCombatTimingProfile(selectedRow);

            EditorGUILayout.Space(8f);
            DrawProfileObjectFields(selectedRow);

            EditorGUILayout.Space(8f);
            DrawSelectedActions(selectedRow);

            EditorGUILayout.Space(8f);
            DrawEditableMonsterStats(selectedRow);

            EditorGUILayout.Space(8f);
            DrawCoreAttributePreview(selectedRow);

            EditorGUILayout.Space(8f);
            DrawMonsterAbilityReferences(selectedRow);
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawMonsterVisualPreview(MonsterProfileRow row)
    {
        EditorGUILayout.LabelField("Monster Visual Preview", EditorStyles.boldLabel);

        MonsterVisualPreviewInfo preview = ResolveMonsterVisualPreview(row.Prefab);
        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            Rect previewRect = GUILayoutUtility.GetRect(96f, 96f, GUILayout.Width(112f), GUILayout.Height(112f));
            DrawPreviewTexture(previewRect, preview.Texture);

            using (new EditorGUILayout.VerticalScope())
            {
                EditorGUILayout.LabelField("Sprite", preview.Sprite != null ? preview.Sprite.name : "(None)");
                EditorGUILayout.LabelField("Renderer", string.IsNullOrWhiteSpace(preview.RendererPath) ? "(None)" : preview.RendererPath);
                EditorGUILayout.LabelField("Source", preview.SourceLabel);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUI.enabled = preview.Sprite != null;
                    if (GUILayout.Button("Ping Sprite", GUILayout.Width(90f)))
                        EditorGUIUtility.PingObject(preview.Sprite);

                    GUI.enabled = row.Prefab != null;
                    if (GUILayout.Button("Ping Prefab", GUILayout.Width(90f)))
                        EditorGUIUtility.PingObject(row.Prefab);

                    GUI.enabled = true;
                }

                if (preview.Texture == null)
                    EditorGUILayout.HelpBox("대표 SpriteRenderer 또는 prefab preview를 찾지 못했습니다.", MessageType.Info);
            }
        }
    }

    private void DrawMonsterCombatTimingProfile(MonsterProfileRow row)
    {
        EditorGUILayout.LabelField("Combat Timing Override", EditorStyles.boldLabel);

        if (row.Prefab == null)
        {
            EditorGUILayout.HelpBox("선택된 프리팹이 없어 전투 타이밍 override를 편집할 수 없습니다.", MessageType.Info);
            return;
        }

        MonsterCombatTimingProfile profile = row.Prefab.GetComponent<MonsterCombatTimingProfile>();
        MonsterTimingOverrideMode currentMode = profile != null
            ? profile.AttackWarningTiming
            : MonsterTimingOverrideMode.UseGlobal;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.HelpBox(
                "Attack Telegraph Time은 공격 경고/telegraph 지속 시간이 스테이지 공격속도 보정에 영향을 받을지 정합니다. Use Global이면 왼쪽 전역 설정을 따릅니다.",
                MessageType.None);

            EditorGUI.BeginChangeCheck();
            MonsterTimingOverrideMode nextMode = (MonsterTimingOverrideMode)EditorGUILayout.EnumPopup(
                new GUIContent("Attack Telegraph Time", "이 몬스터의 경고 telegraph 시간이 공격속도에 따라 짧아질지 override합니다."),
                currentMode);

            if (EditorGUI.EndChangeCheck())
            {
                ApplyMonsterCombatTimingProfile(row.Prefab, profile, nextMode);
                profile = row.Prefab.GetComponent<MonsterCombatTimingProfile>();
                currentMode = nextMode;
                Repaint();
            }

            EditorGUILayout.LabelField("Effective Source", ResolveTimingOverrideSource(profile, currentMode));
        }
    }

    private static string ResolveTimingOverrideSource(MonsterCombatTimingProfile profile, MonsterTimingOverrideMode mode)
    {
        if (profile == null || mode == MonsterTimingOverrideMode.UseGlobal)
            return "Uses global MonsterStageHpScalingSettings";

        return mode == MonsterTimingOverrideMode.ForceEnabled
            ? "Monster override: enabled"
            : "Monster override: disabled";
    }

    private static void ApplyMonsterCombatTimingProfile(
        GameObject prefab,
        MonsterCombatTimingProfile profile,
        MonsterTimingOverrideMode nextMode)
    {
        if (prefab == null)
            return;

        if (profile == null)
        {
            if (nextMode == MonsterTimingOverrideMode.UseGlobal)
                return;

            profile = Undo.AddComponent<MonsterCombatTimingProfile>(prefab);
        }

        Undo.RecordObject(profile, "Change Monster Combat Timing Profile");

        SerializedObject serializedProfile = new(profile);
        SerializedProperty attackWarningTiming = serializedProfile.FindProperty("attackWarningTiming");
        if (attackWarningTiming != null)
            attackWarningTiming.enumValueIndex = (int)nextMode;

        serializedProfile.ApplyModifiedProperties();
        EditorUtility.SetDirty(profile);
        EditorUtility.SetDirty(prefab);
        PrefabUtility.SavePrefabAsset(prefab);
        AssetDatabase.SaveAssets();
    }

    private static void DrawPreviewTexture(Rect rect, Texture2D texture)
    {
        EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f, 1f));
        if (texture == null)
        {
            EditorGUI.LabelField(rect, "No Preview", EditorStyles.centeredGreyMiniLabel);
            return;
        }

        Rect fittedRect = FitTextureRect(rect, texture);
        EditorGUI.DrawPreviewTexture(fittedRect, texture, null, ScaleMode.ScaleToFit);
    }

    private static Rect FitTextureRect(Rect outerRect, Texture2D texture)
    {
        if (texture == null || texture.width <= 0 || texture.height <= 0)
            return outerRect;

        return FitRectByAspect(outerRect, texture.width, texture.height);
    }

    private static Rect FitRectByAspect(Rect outerRect, float width, float height)
    {
        if (width <= 0f || height <= 0f)
            return outerRect;

        float textureAspect = width / height;
        float rectAspect = outerRect.width / outerRect.height;
        if (textureAspect > rectAspect)
        {
            float fittedHeight = outerRect.width / textureAspect;
            return new Rect(outerRect.x, outerRect.y + (outerRect.height - fittedHeight) * 0.5f, outerRect.width, fittedHeight);
        }

        float fittedWidth = outerRect.height * textureAspect;
        return new Rect(outerRect.x + (outerRect.width - fittedWidth) * 0.5f, outerRect.y, fittedWidth, outerRect.height);
    }

    private static MonsterVisualPreviewInfo ResolveMonsterVisualPreview(GameObject prefab)
    {
        if (prefab == null)
            return MonsterVisualPreviewInfo.Empty;

        SpriteRenderer representativeRenderer = FindRepresentativeSpriteRenderer(prefab);
        if (representativeRenderer != null && representativeRenderer.sprite != null)
        {
            Texture2D spritePreview = AssetPreview.GetAssetPreview(representativeRenderer.sprite);
            if (spritePreview == null)
                spritePreview = AssetPreview.GetMiniThumbnail(representativeRenderer.sprite);

            return new MonsterVisualPreviewInfo(
                representativeRenderer.sprite,
                spritePreview,
                GetTransformPath(representativeRenderer.transform, prefab.transform),
                "SpriteRenderer");
        }

        Texture2D prefabPreview = AssetPreview.GetAssetPreview(prefab);
        if (prefabPreview == null)
            prefabPreview = AssetPreview.GetMiniThumbnail(prefab);

        return new MonsterVisualPreviewInfo(null, prefabPreview, string.Empty, "Prefab Preview");
    }

    private static SpriteRenderer FindRepresentativeSpriteRenderer(GameObject prefab)
    {
        SpriteRenderer[] renderers = prefab.GetComponentsInChildren<SpriteRenderer>(true);
        if (renderers == null || renderers.Length == 0)
            return null;

        SpriteRenderer bestRenderer = null;
        int bestScore = int.MinValue;
        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer == null || renderer.sprite == null)
                continue;

            int score = CalculateRepresentativeRendererScore(renderer, prefab.transform);
            if (score <= -1000 || score <= bestScore)
                continue;

            bestScore = score;
            bestRenderer = renderer;
        }

        return bestRenderer;
    }

    private static int CalculateRepresentativeRendererScore(SpriteRenderer renderer, Transform root)
    {
        string rendererName = renderer.transform.name;
        string path = GetTransformPath(renderer.transform, root);
        string normalizedPath = path.ToLowerInvariant();
        int score = 0;

        if (ContainsAny(normalizedPath, "shadow", "hitbox", "hurtbox", "collider", "telegraph", "effect", "vfx", "outline"))
            score -= 1000;

        if (ContainsAny(normalizedPath, "visualroot", "/visual", "body", "sprite", "render", "renderer"))
            score += 100;

        if (ContainsAny(rendererName.ToLowerInvariant(), "body", "sprite", "visual", "render"))
            score += 40;

        if (renderer.GetComponent<Animator>() != null || renderer.GetComponentInParent<Animator>() != null)
            score += 25;

        if (renderer.enabled)
            score += 10;

        score += Mathf.Clamp(Mathf.RoundToInt(renderer.bounds.size.y * 10f), 0, 30);
        return score;
    }

    private static bool ContainsAny(string value, params string[] tokens)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        foreach (string token in tokens)
        {
            if (value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    private static string GetTransformPath(Transform target, Transform root)
    {
        if (target == null)
            return string.Empty;

        List<string> parts = new();
        Transform current = target;
        while (current != null)
        {
            parts.Add(current.name);
            if (current == root)
                break;

            current = current.parent;
        }

        parts.Reverse();
        return string.Join("/", parts);
    }

    private void DrawProfileObjectFields(MonsterProfileRow row)
    {
        EditorGUILayout.ObjectField("Base Profile", row.BaseProfile, typeof(AttributeInitProfileSO), false);

        if (row.OverrideProfiles.Count == 0)
        {
            EditorGUILayout.HelpBox("전용 override profile이 없습니다.", MessageType.Warning);
            return;
        }

        for (int i = 0; i < row.OverrideProfiles.Count; i++)
        {
            EditorGUILayout.ObjectField($"Override {i}", row.OverrideProfiles[i], typeof(AttributeInitProfileSO), false);
        }
    }

    private void DrawSelectedActions(MonsterProfileRow row)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = row.Status == MonsterProfileStatus.Missing;
            if (GUILayout.Button("Create Override Profile"))
            {
                CreateOverrideProfileForRow(row, createDedicatedCopy: false);
                RefreshMonsters();
            }

            GUI.enabled = row.Status == MonsterProfileStatus.SharedOrLegacy;
            if (GUILayout.Button("Create Dedicated Copy"))
            {
                CreateOverrideProfileForRow(row, createDedicatedCopy: true);
                RefreshMonsters();
            }

            GUI.enabled = true;
        }
    }

    private void DrawEditableMonsterStats(MonsterProfileRow row)
    {
        EditorGUILayout.LabelField("Editable Monster Stats", EditorStyles.boldLabel);

        AttributeInitProfileSO editableProfile = AssetDatabase.LoadAssetAtPath<AttributeInitProfileSO>(row.ExpectedProfilePath);
        if (row.Status != MonsterProfileStatus.Ready || editableProfile == null)
        {
            EditorGUILayout.HelpBox("전용 override profile이 있어야 안전하게 스탯을 편집할 수 있습니다. Missing이면 생성하고, Shared/Legacy면 전용 복사본을 만든 뒤 편집하세요.", MessageType.Warning);
            DrawSelectedActions(row);
            return;
        }

        IReadOnlyList<AttributeProfileEntry> entries = BuildCoreEffectiveEntries(row);
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUILayout.Label("Stat", GUILayout.Width(140f));
            GUILayout.Label("Value", GUILayout.Width(90f));
            GUILayout.Label("Write Target", GUILayout.MinWidth(260f));
        }

        bool changed = false;
        changed |= DrawLinkedAttributeFloatField(
            editableProfile,
            entries,
            "HP",
            new[] { "maxhealth", "health" },
            new[] { "health", "maxhealth" },
            "Health + MaxHealth");
        changed |= DrawLinkedAttributeFloatField(
            editableProfile,
            entries,
            "Move Speed",
            new[] { "movespeed" },
            new[] { "movespeed" },
            "MoveSpeed");
        changed |= DrawLinkedAttributeFloatField(
            editableProfile,
            entries,
            "Attack Speed",
            new[] { "attackspeedbase" },
            new[] { "attackspeedbase" },
            "AttackSpeedBase");
        changed |= DrawLinkedAttributeFloatField(
            editableProfile,
            entries,
            "Attack",
            new[] { "attackbase", "attack" },
            new[] { "attack", "attackbase" },
            "Attack + AttackBase");
        changed |= DrawLinkedAttributeFloatField(
            editableProfile,
            entries,
            "Max Stagger",
            new[] { "maxstagger" },
            new[] { "maxstagger" },
            "MaxStagger");
        changed |= DrawLinkedAttributeFloatField(
            editableProfile,
            entries,
            "Stagger Resist",
            new[] { "staggerresistance" },
            new[] { "staggerresistance" },
            "StaggerResistance");
        changed |= DrawLinkedAttributeFloatField(
            editableProfile,
            entries,
            "Knockback Resist",
            new[] { "knockbackresistance" },
            new[] { "knockbackresistance" },
            "KnockbackResistance");

        if (changed)
        {
            EditorUtility.SetDirty(editableProfile);
            Repaint();
        }
    }

    private void DrawCoreAttributePreview(MonsterProfileRow row)
    {
        EditorGUILayout.LabelField("Core Attribute Preview", EditorStyles.boldLabel);

        IReadOnlyList<AttributeProfileEntry> entries = BuildCoreEffectiveEntries(row);
        if (entries.Count == 0)
        {
            EditorGUILayout.HelpBox("핵심 Attribute가 base/override profile에서 발견되지 않았습니다.", MessageType.Info);
            return;
        }

        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUILayout.Label("Attribute", GUILayout.Width(220f));
            GUILayout.Label("Base", GUILayout.Width(80f));
            if (row.UsesStageHpScaling)
            {
                GUILayout.Label("Stage 1", GUILayout.Width(80f));
                GUILayout.Label("Stage 2", GUILayout.Width(80f));
                GUILayout.Label("Stage 3", GUILayout.Width(80f));
            }
            else
            {
                GUILayout.Label("Stage Scaling", GUILayout.Width(250f));
            }
        }

        foreach (AttributeProfileEntry entry in entries)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(GetAttributeDisplayName(entry.Attribute), GUILayout.Width(220f));
                GUILayout.Label(entry.Value.ToString("0.##"), GUILayout.Width(80f));
                if (row.UsesStageHpScaling)
                {
                    float bonus = ResolvePreviewHpBonusPerStage();
                    GUILayout.Label((entry.Value * CalculatePreviewStageMultiplier(1, bonus)).ToString("0.##"), GUILayout.Width(80f));
                    GUILayout.Label((entry.Value * CalculatePreviewStageMultiplier(2, bonus)).ToString("0.##"), GUILayout.Width(80f));
                    GUILayout.Label((entry.Value * CalculatePreviewStageMultiplier(3, bonus)).ToString("0.##"), GUILayout.Width(80f));
                }
                else
                {
                    GUILayout.Label("N/A - Boss/Test prefab is not scaled by MonsterSpawner stage HP.", GUILayout.Width(350f));
                }
            }
        }
    }

    private void DrawMonsterAbilityReferences(MonsterProfileRow row)
    {
        EditorGUILayout.LabelField("Linked Ability Definitions", EditorStyles.boldLabel);

        List<AbilityReferenceInfo> abilityReferences = CollectAbilityReferences(row.Prefab);
        if (abilityReferences.Count == 0)
        {
            EditorGUILayout.HelpBox("프리팹 내부에서 직접 연결된 AbilityDefinition을 찾지 못했습니다. 보스 PhaseConfig처럼 외부 에셋을 거치는 참조는 후속 확장 대상입니다.", MessageType.Info);
            return;
        }

        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUILayout.Label("Ability", GUILayout.Width(190f));
            GUILayout.Label("Owner / Field", GUILayout.MinWidth(190f));
            GUILayout.Label("Cooldown", GUILayout.Width(70f));
            GUILayout.Label("Cast", GUILayout.Width(60f));
            GUILayout.Label("Recovery", GUILayout.Width(70f));
            GUILayout.Label("Logic", GUILayout.Width(150f));
            GUILayout.Label("Source", GUILayout.Width(150f));
            GUILayout.Space(50f);
        }

        foreach (AbilityReferenceInfo info in abilityReferences)
        {
            DrawAbilityReferenceRow(info);
        }
    }

    private static void DrawAbilityReferenceRow(AbilityReferenceInfo info)
    {
        AbilityDefinition ability = info.Ability;
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.ObjectField(ability, typeof(AbilityDefinition), false, GUILayout.Width(190f));
            GUILayout.Label($"{info.OwnerName}.{info.PropertyPath}", GUILayout.MinWidth(190f));

            if (ability != null)
            {
                EditorGUI.BeginChangeCheck();
                float cooldown = Mathf.Max(0f, EditorGUILayout.FloatField(ability.cooldown, GUILayout.Width(70f)));
                float castTime = Mathf.Max(0f, EditorGUILayout.FloatField(ability.castTime, GUILayout.Width(60f)));
                float recovery = Mathf.Max(0f, EditorGUILayout.FloatField(ability.recoveryTime, GUILayout.Width(70f)));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(ability, "Edit Monster Ability Timing");
                    ability.cooldown = cooldown;
                    ability.castTime = castTime;
                    ability.recoveryTime = recovery;
                    EditorUtility.SetDirty(ability);
                }
            }
            else
            {
                GUILayout.Space(200f);
            }

            EditorGUILayout.ObjectField(ability != null ? ability.logic : null, typeof(AbilityLogic), false, GUILayout.Width(150f));
            EditorGUILayout.ObjectField(ability != null ? ability.sourceObject : null, typeof(UnityEngine.Object), false, GUILayout.Width(150f));

            if (GUILayout.Button("Ping", GUILayout.Width(50f)) && ability != null)
                EditorGUIUtility.PingObject(ability);
        }
    }

    private void LoadStageHpScalingSettings()
    {
        stageHpScalingSettings = AssetDatabase.LoadAssetAtPath<MonsterStageHpScalingSettings>(StageHpScalingSettingsPath);
    }

    private float ResolvePreviewHpBonusPerStage()
    {
        return stageHpScalingSettings != null && stageHpScalingSettings.Enabled
            ? stageHpScalingSettings.HpMultiplierPerClearedStage
            : 0f;
    }

    private static float CalculatePreviewStageMultiplier(int stageIndex, float hpBonusPerStage)
    {
        return 1f + Mathf.Max(0f, hpBonusPerStage) * Mathf.Max(0, stageIndex);
    }

    private IEnumerable<MonsterProfileRow> GetFilteredRows()
    {
        if (string.IsNullOrWhiteSpace(monsterSearch))
            return monsterRows;

        return monsterRows.Where(row => row.Prefab.name.IndexOf(monsterSearch, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private IEnumerable<WeaponBalanceRow> GetFilteredWeaponRows()
    {
        if (string.IsNullOrWhiteSpace(weaponSearch))
            return weaponRows;

        return weaponRows.Where(row =>
            row.AssetName.IndexOf(weaponSearch, StringComparison.OrdinalIgnoreCase) >= 0 ||
            row.DisplayName.IndexOf(weaponSearch, StringComparison.OrdinalIgnoreCase) >= 0 ||
            (row.Weapon.weaponId ?? string.Empty).IndexOf(weaponSearch, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static GUIStyle GetStatusStyle(MonsterProfileStatus status)
    {
        return status switch
        {
            MonsterProfileStatus.Ready => EditorStyles.miniLabel,
            MonsterProfileStatus.Missing => EditorStyles.boldLabel,
            MonsterProfileStatus.SharedOrLegacy => EditorStyles.miniBoldLabel,
            _ => EditorStyles.label
        };
    }

    private void RefreshAll()
    {
        RefreshMonsters();
        RefreshWeapons();
    }

    private void RefreshMonsters()
    {
        monsterRows.Clear();

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", EnemyPrefabRoots))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!IsBalanceTarget(prefab))
                continue;

            MonsterProfileRow row = BuildMonsterProfileRow(prefab, path);
            monsterRows.Add(row);
        }

        monsterRows.Sort((a, b) => string.Compare(a.Prefab.name, b.Prefab.name, StringComparison.OrdinalIgnoreCase));

        if (selectedRow != null)
            selectedRow = monsterRows.FirstOrDefault(row => row.Prefab == selectedRow.Prefab);

        Repaint();
    }

    private void RefreshWeapons()
    {
        weaponRows.Clear();

        foreach (string guid in AssetDatabase.FindAssets("t:WeaponDefinition", new[] { WeaponDefinitionRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            WeaponDefinition weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
            if (weapon == null)
                continue;

            weaponRows.Add(new WeaponBalanceRow(weapon, path));
        }

        weaponRows.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));

        if (selectedWeaponRow != null)
            selectedWeaponRow = weaponRows.FirstOrDefault(row => row.Weapon == selectedWeaponRow.Weapon);

        Repaint();
    }

    private static bool IsBalanceTarget(GameObject prefab)
    {
        if (prefab == null)
            return false;

        if (IsExcludedByName(prefab.name))
            return false;

        AttributeSet attributeSet = prefab.GetComponent<AttributeSet>();
        if (attributeSet == null)
            return false;

        return prefab.GetComponent<Mob>() != null ||
               prefab.GetComponent<BossControllerBase>() != null ||
               prefab.GetComponent<Enemy>() != null ||
               prefab.GetComponent<TrainingDummy2D>() != null;
    }

    private static bool IsExcludedByName(string prefabName)
    {
        string name = prefabName ?? string.Empty;
        string[] excludedTokens =
        {
            "Projectile",
            "Puddle",
            "Effect",
            "Fog",
            "Bead",
            "Keg",
            "Tile",
            "Visual"
        };

        return excludedTokens.Any(token => name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static MonsterProfileRow BuildMonsterProfileRow(GameObject prefab, string prefabPath)
    {
        AttributeSet attributeSet = prefab.GetComponent<AttributeSet>();
        SerializedObject serializedAttributeSet = new(attributeSet);
        AttributeInitProfileSO baseProfile = serializedAttributeSet.FindProperty("baseInitProfile")?.objectReferenceValue as AttributeInitProfileSO;
        List<AttributeInitProfileSO> overrideProfiles = ReadOverrideProfiles(serializedAttributeSet);
        string expectedPath = BuildExpectedProfilePath(prefab, prefabPath);
        MonsterProfileStatus status = ResolveStatus(prefab, overrideProfiles, expectedPath);
        MonsterListPreviewInfo listPreview = ResolveMonsterListPreview(prefab);

        bool usesStageHpScaling = prefab.GetComponent<Mob>() != null && prefab.GetComponent<BossControllerBase>() == null;
        return new MonsterProfileRow(prefab, prefabPath, attributeSet, baseProfile, overrideProfiles, expectedPath, status, usesStageHpScaling, listPreview);
    }

    private static MonsterListPreviewInfo ResolveMonsterListPreview(GameObject prefab)
    {
        if (prefab == null)
            return MonsterListPreviewInfo.Empty;

        SpriteRenderer representativeRenderer = FindRepresentativeSpriteRenderer(prefab);
        if (representativeRenderer == null || representativeRenderer.sprite == null)
            return MonsterListPreviewInfo.Empty;

        return new MonsterListPreviewInfo(
            representativeRenderer.sprite,
            GetTransformPath(representativeRenderer.transform, prefab.transform));
    }

    private static List<AbilityReferenceInfo> CollectAbilityReferences(GameObject prefab)
    {
        List<AbilityReferenceInfo> result = new();
        if (prefab == null)
            return result;

        HashSet<AbilityDefinition> seenAbilities = new();
        MonoBehaviour[] behaviours = prefab.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null)
                continue;

            SerializedObject serializedBehaviour = new(behaviour);
            SerializedProperty iterator = serializedBehaviour.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = true;
                if (iterator.propertyType != SerializedPropertyType.ObjectReference)
                    continue;

                if (iterator.objectReferenceValue is not AbilityDefinition ability)
                    continue;

                if (!seenAbilities.Add(ability))
                    continue;

                result.Add(new AbilityReferenceInfo(
                    ability,
                    behaviour.GetType().Name,
                    iterator.propertyPath));
            }
        }

        return result
            .OrderBy(info => info.Ability != null ? info.Ability.name : string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<AttributeInitProfileSO> ReadOverrideProfiles(SerializedObject serializedAttributeSet)
    {
        List<AttributeInitProfileSO> result = new();
        SerializedProperty overrides = serializedAttributeSet.FindProperty("overrideInitProfiles");
        if (overrides == null || !overrides.isArray)
            return result;

        for (int i = 0; i < overrides.arraySize; i++)
        {
            if (overrides.GetArrayElementAtIndex(i).objectReferenceValue is AttributeInitProfileSO profile)
                result.Add(profile);
        }

        return result;
    }

    private static MonsterProfileStatus ResolveStatus(GameObject prefab, List<AttributeInitProfileSO> overrideProfiles, string expectedPath)
    {
        if (overrideProfiles.Count == 0)
            return MonsterProfileStatus.Missing;

        string expectedName = BuildExpectedProfileName(prefab);
        bool hasExpectedProfile = overrideProfiles.Any(profile =>
            profile != null &&
            profile.name == expectedName &&
            AssetDatabase.GetAssetPath(profile) == expectedPath);

        return hasExpectedProfile ? MonsterProfileStatus.Ready : MonsterProfileStatus.SharedOrLegacy;
    }

    private void CreateMissingOverrideProfiles()
    {
        int created = 0;
        foreach (MonsterProfileRow row in monsterRows)
        {
            if (row.Status != MonsterProfileStatus.Missing)
                continue;

            if (CreateOverrideProfileForRow(row, createDedicatedCopy: false))
                created++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        RefreshMonsters();
        Debug.Log($"[CombatBalanceEditor] Created {created} monster override profile(s).");
    }

    private static bool CreateOverrideProfileForRow(MonsterProfileRow row, bool createDedicatedCopy)
    {
        if (row == null || row.Prefab == null || row.AttributeSet == null)
            return false;

        if (!createDedicatedCopy && row.OverrideProfiles.Count > 0)
            return false;

        EnsureFolderExists(Path.GetDirectoryName(row.ExpectedProfilePath)?.Replace('\\', '/'));

        AttributeInitProfileSO profile = LoadOrCreateExpectedProfile(row, out bool createdProfile);

        List<AttributeProfileEntry> coreEntries = BuildCoreEffectiveEntries(row).ToList();
        if (createdProfile || IsProfileEntryListEmpty(profile))
            WriteProfileEntries(profile, coreEntries);

        ConnectProfileToPrefab(row, profile, replaceExisting: createDedicatedCopy);

        EditorUtility.SetDirty(profile);
        PrefabUtility.SavePrefabAsset(row.Prefab);
        return true;
    }

    private static AttributeInitProfileSO LoadOrCreateExpectedProfile(MonsterProfileRow row, out bool createdProfile)
    {
        AttributeInitProfileSO existingProfile = AssetDatabase.LoadAssetAtPath<AttributeInitProfileSO>(row.ExpectedProfilePath);
        if (existingProfile != null)
        {
            createdProfile = false;
            return existingProfile;
        }

        AttributeInitProfileSO profile = ScriptableObject.CreateInstance<AttributeInitProfileSO>();
        profile.name = BuildExpectedProfileName(row.Prefab);
        AssetDatabase.CreateAsset(profile, row.ExpectedProfilePath);
        createdProfile = true;
        return profile;
    }

    private static bool IsProfileEntryListEmpty(AttributeInitProfileSO profile)
    {
        if (profile == null)
            return true;

        SerializedObject serializedProfile = new(profile);
        SerializedProperty entries = serializedProfile.FindProperty("entries");
        return entries == null || !entries.isArray || entries.arraySize == 0;
    }

    private static void ConnectProfileToPrefab(MonsterProfileRow row, AttributeInitProfileSO profile, bool replaceExisting)
    {
        SerializedObject serializedAttributeSet = new(row.AttributeSet);
        SerializedProperty overrides = serializedAttributeSet.FindProperty("overrideInitProfiles");
        if (overrides == null || !overrides.isArray)
            return;

        Undo.RecordObject(row.AttributeSet, "Connect Monster Attribute Override Profile");

        if (replaceExisting)
        {
            overrides.arraySize = 1;
            overrides.GetArrayElementAtIndex(0).objectReferenceValue = profile;
        }
        else
        {
            overrides.InsertArrayElementAtIndex(overrides.arraySize);
            overrides.GetArrayElementAtIndex(overrides.arraySize - 1).objectReferenceValue = profile;
        }

        serializedAttributeSet.ApplyModifiedProperties();
        EditorUtility.SetDirty(row.AttributeSet);
    }

    private static IReadOnlyList<AttributeProfileEntry> BuildCoreEffectiveEntries(MonsterProfileRow row)
    {
        Dictionary<AttributeDefinition, float> values = new();
        ApplyProfileToMap(row.BaseProfile, values);

        foreach (AttributeInitProfileSO profile in row.OverrideProfiles)
            ApplyProfileToMap(profile, values);

        return values
            .Where(pair => IsCoreAttribute(pair.Key))
            .Select(pair => new AttributeProfileEntry(pair.Key, pair.Value))
            .OrderBy(entry => GetCoreSortOrder(entry.Attribute))
            .ThenBy(entry => GetAttributeDisplayName(entry.Attribute), StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void ApplyProfileToMap(AttributeInitProfileSO profile, Dictionary<AttributeDefinition, float> values)
    {
        if (profile == null)
            return;

        SerializedObject serializedProfile = new(profile);
        SerializedProperty entries = serializedProfile.FindProperty("entries");
        if (entries == null || !entries.isArray)
            return;

        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            AttributeDefinition attribute = entry.FindPropertyRelative("attribute")?.objectReferenceValue as AttributeDefinition;
            if (attribute == null)
                continue;

            float value = entry.FindPropertyRelative("baseValue")?.floatValue ?? 0f;
            values[attribute] = value;
        }
    }

    private static void WriteProfileEntries(AttributeInitProfileSO profile, IReadOnlyList<AttributeProfileEntry> entries)
    {
        SerializedObject serializedProfile = new(profile);
        SerializedProperty serializedEntries = serializedProfile.FindProperty("entries");
        if (serializedEntries == null || !serializedEntries.isArray)
            return;

        serializedEntries.arraySize = entries.Count;
        for (int i = 0; i < entries.Count; i++)
        {
            SerializedProperty entry = serializedEntries.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("attribute").objectReferenceValue = entries[i].Attribute;
            entry.FindPropertyRelative("baseValue").floatValue = entries[i].Value;
        }

        serializedProfile.ApplyModifiedProperties();
    }

    private static bool DrawLinkedAttributeFloatField(
        AttributeInitProfileSO editableProfile,
        IReadOnlyList<AttributeProfileEntry> effectiveEntries,
        string label,
        IReadOnlyList<string> readNormalizedNames,
        IReadOnlyList<string> writeNormalizedNames,
        string writeTargetLabel)
    {
        if (!TryFindEffectiveAttributeEntry(effectiveEntries, readNormalizedNames, out AttributeProfileEntry readEntry))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(label, GUILayout.Width(140f));
                GUILayout.Label("N/A", GUILayout.Width(90f));
                GUILayout.Label($"Attribute not found: {string.Join(", ", readNormalizedNames)}", EditorStyles.miniLabel);
            }

            return false;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label(label, GUILayout.Width(140f));
            EditorGUI.BeginChangeCheck();
            float nextValue = EditorGUILayout.FloatField(readEntry.Value, GUILayout.Width(90f));
            GUILayout.Label(writeTargetLabel, GUILayout.MinWidth(260f));

            if (!EditorGUI.EndChangeCheck())
                return false;

            Undo.RecordObject(editableProfile, $"Edit {label}");
            foreach (string normalizedName in writeNormalizedNames)
            {
                if (TryFindEffectiveAttributeEntry(effectiveEntries, new[] { normalizedName }, out AttributeProfileEntry targetEntry))
                    WriteProfileEntryValue(editableProfile, targetEntry.Attribute, nextValue);
            }

            return true;
        }
    }

    private static bool TryFindEffectiveAttributeEntry(
        IReadOnlyList<AttributeProfileEntry> entries,
        IReadOnlyList<string> normalizedNames,
        out AttributeProfileEntry result)
    {
        foreach (string normalizedName in normalizedNames)
        {
            foreach (AttributeProfileEntry entry in entries)
            {
                if (NormalizeAttributeName(entry.Attribute) != normalizedName)
                    continue;

                result = entry;
                return true;
            }
        }

        result = default;
        return false;
    }

    private static void WriteProfileEntryValue(AttributeInitProfileSO profile, AttributeDefinition attribute, float value)
    {
        if (profile == null || attribute == null)
            return;

        SerializedObject serializedProfile = new(profile);
        SerializedProperty entries = serializedProfile.FindProperty("entries");
        if (entries == null || !entries.isArray)
            return;

        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("attribute")?.objectReferenceValue != attribute)
                continue;

            entry.FindPropertyRelative("baseValue").floatValue = value;
            serializedProfile.ApplyModifiedProperties();
            return;
        }

        entries.InsertArrayElementAtIndex(entries.arraySize);
        SerializedProperty newEntry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
        newEntry.FindPropertyRelative("attribute").objectReferenceValue = attribute;
        newEntry.FindPropertyRelative("baseValue").floatValue = value;
        serializedProfile.ApplyModifiedProperties();
    }

    private static bool IsCoreAttribute(AttributeDefinition attribute)
    {
        if (attribute == null)
            return false;

        string normalized = NormalizeAttributeName(attribute);
        return normalized is "health" or
            "maxhealth" or
            "movespeed" or
            "attackspeedbase" or
            "maxstagger" or
            "staggerresistance" or
            "knockbackresistance" or
            "attack" or
            "attackbase";
    }

    private static int GetCoreSortOrder(AttributeDefinition attribute)
    {
        string normalized = NormalizeAttributeName(attribute);
        return normalized switch
        {
            "health" => 0,
            "maxhealth" => 1,
            "movespeed" => 2,
            "attackspeedbase" => 3,
            "maxstagger" => 4,
            "staggerresistance" => 5,
            "knockbackresistance" => 6,
            "attack" => 7,
            "attackbase" => 8,
            _ => 100
        };
    }

    private static string NormalizeAttributeName(AttributeDefinition attribute)
    {
        string raw = !string.IsNullOrWhiteSpace(attribute.attributeName) ? attribute.attributeName : attribute.name;
        return raw
            .Replace("Attribute", string.Empty)
            .Replace("attribute", string.Empty)
            .Replace(" ", string.Empty)
            .Replace("_", string.Empty)
            .ToLowerInvariant();
    }

    private static string GetAttributeDisplayName(AttributeDefinition attribute)
    {
        if (attribute == null)
            return "(Missing)";

        return !string.IsNullOrWhiteSpace(attribute.attributeName) ? attribute.attributeName : attribute.name;
    }

    private static string BuildExpectedProfilePath(GameObject prefab, string prefabPath)
    {
        string folder = ResolveProfileFolder(prefabPath);
        return $"{folder}/{BuildExpectedProfileName(prefab)}.asset";
    }

    private static string BuildExpectedProfileName(GameObject prefab)
    {
        return $"{SanitizeFileName(prefab.name)}AttributeOverrideInitProfile";
    }

    private static string ResolveProfileFolder(string prefabPath)
    {
        if (prefabPath.IndexOf("/Bosses/", StringComparison.OrdinalIgnoreCase) >= 0)
            return BossProfileFolder;

        if (prefabPath.IndexOf("/Monsters/", StringComparison.OrdinalIgnoreCase) >= 0)
            return MobProfileFolder;

        return MiscProfileFolder;
    }

    private static string SanitizeFileName(string value)
    {
        string sanitized = value;
        foreach (char invalid in Path.GetInvalidFileNameChars())
            sanitized = sanitized.Replace(invalid, '_');

        return sanitized.Replace(' ', '_');
    }

    private static void EnsureFolderExists(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || AssetDatabase.IsValidFolder(folderPath))
            return;

        string[] parts = folderPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private enum BalanceToolTab
    {
        MonsterStats,
        WeaponStats
    }

    private enum MonsterProfileStatus
    {
        Ready,
        Missing,
        SharedOrLegacy
    }

    /// <summary>
    /// 책임:
    /// - 밸런스 에디터가 한 무기 정의의 목록/검색/아이콘 표시와 상세 편집에 필요한 읽기 모델을 보관한다.
    /// </summary>
    private sealed class WeaponBalanceRow
    {
        public WeaponBalanceRow(WeaponDefinition weapon, string assetPath)
        {
            Weapon = weapon;
            AssetPath = assetPath;
        }

        public WeaponDefinition Weapon { get; }
        public string AssetPath { get; }
        public string AssetName => Weapon != null ? Weapon.name : "(Missing)";
        public string DisplayName => Weapon != null && !string.IsNullOrWhiteSpace(Weapon.displayName) ? Weapon.displayName : AssetName;
        public Sprite Icon => Weapon != null ? Weapon.icon : null;
    }

    /// <summary>
    /// 책임:
    /// - 선택된 무기에서 발견한 한 AbilityDefinition과 그 참조 출처를 AD 밸런싱 패널에 전달한다.
    /// </summary>
    private sealed class WeaponAbilityBalanceBlock
    {
        private readonly List<string> sourceLabels = new();

        public WeaponAbilityBalanceBlock(AbilityDefinition ability, string sourceLabel)
        {
            Ability = ability;
            AddSourceLabel(sourceLabel);
        }

        public AbilityDefinition Ability { get; }
        public string SourceLabel => string.Join(", ", sourceLabels);

        public void AddSourceLabel(string sourceLabel)
        {
            if (string.IsNullOrWhiteSpace(sourceLabel) || sourceLabels.Contains(sourceLabel))
                return;

            sourceLabels.Add(sourceLabel);
        }
    }

    /// <summary>
    /// 책임:
    /// - SerializedProperty가 무기/Ability 밸런싱 수치로 보여줄 만한 필드인지 판정한다.
    /// - VFX/SFX/프리팹 같은 표현 데이터가 밸런스 패널에 섞이지 않도록 제외한다.
    /// </summary>
    private static class BalanceTunablePropertyFilter
    {
        private static readonly string[] IncludeKeywords =
        {
            "fixeddamage", "legacydamage", "damage", "stagger", "knockback",
            "scale", "multiplier", "coefficient", "radius", "range", "distance", "size", "length",
            "speed", "duration", "interval", "delay", "count", "spread", "ammo"
        };

        private static readonly string[] ExcludeKeywords =
        {
            "prefab", "sprite", "audio", "sound", "vfx", "effect", "material", "anim", "visual", "layer"
        };

        public static List<string> FindTunablePropertyPaths(SerializedObject serializedObject)
        {
            List<string> paths = new();
            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = true;
                if (ShouldDrawAsTunable(iterator))
                    paths.Add(iterator.propertyPath);
            }

            return paths;
        }

        private static bool ShouldDrawAsTunable(SerializedProperty property)
        {
            if (property == null || property.propertyPath == "m_Script")
                return false;

            if (property.propertyType == SerializedPropertyType.ObjectReference &&
                IsFormulaReferenceProperty(property))
            {
                return false;
            }

            string key = Normalize(property.propertyPath);
            if (ContainsAny(key, ExcludeKeywords))
                return false;

            if (!ContainsAny(key, IncludeKeywords))
                return false;

            return property.propertyType switch
            {
                SerializedPropertyType.Integer => true,
                SerializedPropertyType.Boolean => true,
                SerializedPropertyType.Float => true,
                SerializedPropertyType.Enum => true,
                SerializedPropertyType.Vector2 => true,
                SerializedPropertyType.Vector3 => true,
                SerializedPropertyType.Vector2Int => true,
                SerializedPropertyType.Vector3Int => true,
                SerializedPropertyType.ObjectReference => true,
                _ => false
            };
        }

        private static bool ContainsAny(string value, string[] keywords)
        {
            for (int i = 0; i < keywords.Length; i++)
            {
                if (value.IndexOf(keywords[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace(" ", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
        }

        private static bool IsFormulaReferenceProperty(SerializedProperty property)
        {
            if (property.objectReferenceValue is ScaledStatFormula)
                return true;

            string key = Normalize(property.propertyPath);
            return key.EndsWith("damageformula", StringComparison.OrdinalIgnoreCase) ||
                   key.EndsWith("knockbackformula", StringComparison.OrdinalIgnoreCase) ||
                   key.EndsWith("staggerformula", StringComparison.OrdinalIgnoreCase) ||
                   key.EndsWith("elementformulas.formula", StringComparison.OrdinalIgnoreCase) ||
                   key.EndsWith("formula", StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// 책임:
    /// - sourceObject 내부에서 발견한 ScaledStatFormula 참조 위치와 실제 Formula 에셋을 함께 전달한다.
    /// </summary>
    private readonly struct ScaledFormulaReference
    {
        public ScaledFormulaReference(string propertyPath, ScaledStatFormula formula)
        {
            PropertyPath = propertyPath;
            Formula = formula;
        }

        public string PropertyPath { get; }
        public ScaledStatFormula Formula { get; }
    }

    /// <summary>
    /// 책임:
    /// - ScaledStatFormula 참조를 sourceObject에서 수집하고 terms 계수 편집 UI를 그린다.
    /// - Formula가 공유될 수 있음을 명확히 보여줘 밸런싱 사고를 줄인다.
    /// </summary>
    private static class ScaledFormulaEditorDrawer
    {
        public static List<ScaledFormulaReference> FindFormulaReferences(ScriptableObject sourceObject)
        {
            List<ScaledFormulaReference> references = new();
            if (sourceObject == null)
                return references;

            SerializedObject serializedObject = new(sourceObject);
            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = true;
                if (iterator.propertyPath == "m_Script" ||
                    iterator.propertyType != SerializedPropertyType.ObjectReference ||
                    iterator.objectReferenceValue is not ScaledStatFormula formula)
                {
                    continue;
                }

                references.Add(new ScaledFormulaReference(iterator.propertyPath, formula));
            }

            return references;
        }

        public static void DrawFormulaReference(
            ScaledFormulaReference formulaReference,
            Dictionary<ScaledStatFormula, int> formulaUseCounts)
        {
            ScaledStatFormula formula = formulaReference.Formula;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.ObjectField(
                        BuildReadablePropertyLabel(formulaReference.PropertyPath),
                        formula,
                        typeof(ScaledStatFormula),
                        false);
                    GUI.enabled = formula != null;
                    if (GUILayout.Button("Ping Formula", GUILayout.Width(96f)))
                        EditorGUIUtility.PingObject(formula);
                    GUI.enabled = true;
                }

                if (formula == null)
                {
                    EditorGUILayout.HelpBox("Formula 참조가 비어 있습니다.", MessageType.None);
                    return;
                }

                if (formulaUseCounts != null &&
                    formulaUseCounts.TryGetValue(formula, out int count) &&
                    count > 1)
                {
                    EditorGUILayout.HelpBox($"Shared Formula: 현재 선택 무기 안에서 {count}회 참조됩니다. 수정 시 여러 공격 값이 함께 바뀔 수 있습니다.", MessageType.Warning);
                }

                string formulaPath = AssetDatabase.GetAssetPath(formula);
                if (!string.IsNullOrWhiteSpace(formulaPath))
                    EditorGUILayout.LabelField("Path", formulaPath, EditorStyles.miniLabel);

                SerializedObject serializedFormula = new(formula);
                serializedFormula.Update();
                SerializedProperty termsProperty = serializedFormula.FindProperty("terms");
                if (termsProperty == null)
                {
                    EditorGUILayout.HelpBox("terms 필드를 찾지 못했습니다.", MessageType.Warning);
                    return;
                }

                EditorGUI.BeginChangeCheck();
                DrawFormulaTerms(termsProperty);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(formula, "Edit Scaled Stat Formula");
                    serializedFormula.ApplyModifiedProperties();
                    EditorUtility.SetDirty(formula);
                }
                else
                {
                    serializedFormula.ApplyModifiedProperties();
                }
            }
        }

        private static void DrawFormulaTerms(SerializedProperty termsProperty)
        {
            int size = Mathf.Max(0, EditorGUILayout.IntField("Term Count", termsProperty.arraySize));
            if (size != termsProperty.arraySize)
                termsProperty.arraySize = size;

            for (int i = 0; i < termsProperty.arraySize; i++)
            {
                SerializedProperty term = termsProperty.GetArrayElementAtIndex(i);
                SerializedProperty useStatId = term.FindPropertyRelative("useStatId");
                SerializedProperty statId = term.FindPropertyRelative("statId");
                SerializedProperty sourceAttribute = term.FindPropertyRelative("sourceAttribute");
                SerializedProperty rate = term.FindPropertyRelative("rate");
                SerializedProperty flat = term.FindPropertyRelative("flat");

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField($"Term {i + 1}", EditorStyles.boldLabel);
                    if (useStatId != null)
                        EditorGUILayout.PropertyField(useStatId, new GUIContent("Use StatId"));

                    if (useStatId != null && useStatId.boolValue)
                    {
                        if (statId != null)
                            EditorGUILayout.PropertyField(statId, new GUIContent("Stat Id"));
                    }
                    else if (sourceAttribute != null)
                    {
                        EditorGUILayout.PropertyField(sourceAttribute, new GUIContent("Source Attribute"));
                    }

                    if (rate != null)
                        EditorGUILayout.PropertyField(rate, new GUIContent("Rate (1.0 = 100%)"));
                    if (flat != null)
                        EditorGUILayout.PropertyField(flat, new GUIContent("Flat Add"));
                }
            }
        }
    }

    /// <summary>
    /// 책임:
    /// - 밸런스 에디터가 한 몬스터 프리팹의 Attribute profile 연결 상태를 표시하는 데 필요한 읽기 모델을 보관한다.
    /// </summary>
    private sealed class MonsterProfileRow
    {
        public MonsterProfileRow(
            GameObject prefab,
            string prefabPath,
            AttributeSet attributeSet,
            AttributeInitProfileSO baseProfile,
            List<AttributeInitProfileSO> overrideProfiles,
            string expectedProfilePath,
            MonsterProfileStatus status,
            bool usesStageHpScaling,
            MonsterListPreviewInfo listPreview)
        {
            Prefab = prefab;
            PrefabPath = prefabPath;
            AttributeSet = attributeSet;
            BaseProfile = baseProfile;
            OverrideProfiles = overrideProfiles;
            ExpectedProfilePath = expectedProfilePath;
            Status = status;
            UsesStageHpScaling = usesStageHpScaling;
            ListPreviewSprite = listPreview.Sprite;
            ListPreviewRendererPath = listPreview.RendererPath;
        }

        public GameObject Prefab { get; }
        public string PrefabPath { get; }
        public AttributeSet AttributeSet { get; }
        public AttributeInitProfileSO BaseProfile { get; }
        public List<AttributeInitProfileSO> OverrideProfiles { get; }
        public string ExpectedProfilePath { get; }
        public MonsterProfileStatus Status { get; }
        public bool UsesStageHpScaling { get; }
        public Sprite ListPreviewSprite { get; }
        public string ListPreviewRendererPath { get; }

        public string StatusLabel => Status switch
        {
            MonsterProfileStatus.Ready => "Ready",
            MonsterProfileStatus.Missing => "Missing",
            MonsterProfileStatus.SharedOrLegacy => "Shared/Legacy",
            _ => Status.ToString()
        };
    }

    /// <summary>
    /// 책임:
    /// - AttributeInitProfileSO의 한 Entry를 에디터 계산과 생성 로직에서 안전하게 전달한다.
    /// </summary>
    private readonly struct AttributeProfileEntry
    {
        public AttributeProfileEntry(AttributeDefinition attribute, float value)
        {
            Attribute = attribute;
            Value = value;
        }

        public AttributeDefinition Attribute { get; }
        public float Value { get; }
    }

    /// <summary>
    /// 책임:
    /// - 몬스터 목록 행에서 사용할 작은 정적 sprite preview 정보를 캐싱한다.
    /// </summary>
    private readonly struct MonsterListPreviewInfo
    {
        public MonsterListPreviewInfo(Sprite sprite, string rendererPath)
        {
            Sprite = sprite;
            RendererPath = rendererPath;
        }

        public Sprite Sprite { get; }
        public string RendererPath { get; }

        public static MonsterListPreviewInfo Empty => new(null, string.Empty);
    }

    /// <summary>
    /// 책임:
    /// - 선택된 몬스터 프리팹의 대표 비주얼 프리뷰를 그리는 데 필요한 sprite, texture, renderer 출처를 전달한다.
    /// </summary>
    private readonly struct MonsterVisualPreviewInfo
    {
        public MonsterVisualPreviewInfo(Sprite sprite, Texture2D texture, string rendererPath, string sourceLabel)
        {
            Sprite = sprite;
            Texture = texture;
            RendererPath = rendererPath;
            SourceLabel = sourceLabel;
        }

        public Sprite Sprite { get; }
        public Texture2D Texture { get; }
        public string RendererPath { get; }
        public string SourceLabel { get; }

        public static MonsterVisualPreviewInfo Empty => new(null, null, string.Empty, "None");
    }

    /// <summary>
    /// 책임:
    /// - 선택된 몬스터 프리팹 내부에서 발견한 AbilityDefinition 참조의 출처를 표시한다.
    /// </summary>
    private readonly struct AbilityReferenceInfo
    {
        public AbilityReferenceInfo(AbilityDefinition ability, string ownerName, string propertyPath)
        {
            Ability = ability;
            OwnerName = ownerName;
            PropertyPath = propertyPath;
        }

        public AbilityDefinition Ability { get; }
        public string OwnerName { get; }
        public string PropertyPath { get; }
    }
}




