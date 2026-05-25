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
    private const string EnemyPrefabRoot = "Assets/Prefabs/Enemies";
    private const string StageHpScalingSettingsPath = "Assets/Resources/MonsterStageHpScalingSettings.asset";
    private const string MobProfileFolder = "Assets/HeoMinSeok/_Project/Data/Abilities/Attribute/AttributeManageSO/Enemies/Mobs";
    private const string BossProfileFolder = "Assets/HeoMinSeok/_Project/Data/Abilities/Attribute/AttributeManageSO/Enemies/Bosses";
    private const string MiscProfileFolder = "Assets/HeoMinSeok/_Project/Data/Abilities/Attribute/AttributeManageSO/Enemies/Misc";

    private readonly List<MonsterProfileRow> monsterRows = new();
    private Vector2 monsterScroll;
    private Vector2 detailScroll;
    private string monsterSearch = string.Empty;
    private MonsterProfileRow selectedRow;
    private MonsterStageHpScalingSettings stageHpScalingSettings;

    [MenuItem("Tools/Combat/Combat Balance Editor")]
    public static void OpenWindow()
    {
        CombatBalanceEditorWindow window = GetWindow<CombatBalanceEditorWindow>("Combat Balance");
        window.minSize = new Vector2(720f, 520f);
        window.RefreshMonsters();
    }

    private void OnEnable()
    {
        LoadStageHpScalingSettings();
        RefreshMonsters();
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
                RefreshMonsters();

            GUILayout.Space(8f);
            GUILayout.Label("Monster Stats", EditorStyles.boldLabel, GUILayout.Width(110f));
            GUILayout.FlexibleSpace();
        }
    }

    private void DrawMonsterStatsTab()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawStageHpScalingSettings();
            DrawMonsterList();
            DrawSelectedMonsterDetail();
        }
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

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(enabledProperty, new GUIContent("Enable Stage HP Scaling"));
            EditorGUILayout.PropertyField(bonusProperty, new GUIContent("HP Bonus Per Stage"));

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
            if (GUILayout.Button(row.Prefab.name, GUIStyle.none, GUILayout.Height(24f)))
                selectedRow = row;

            GUILayout.Label(row.StatusLabel, GetStatusStyle(row.Status), GUILayout.Width(86f));
        }
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
            DrawProfileObjectFields(selectedRow);

            EditorGUILayout.Space(8f);
            DrawSelectedActions(selectedRow);

            EditorGUILayout.Space(8f);
            DrawMonsterAbilityReferences(selectedRow);

            EditorGUILayout.Space(8f);
            DrawCoreAttributePreview(selectedRow);
        }
        EditorGUILayout.EndScrollView();
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
            GUILayout.Label("Ability", GUILayout.Width(220f));
            GUILayout.Label("Owner / Field", GUILayout.MinWidth(220f));
            GUILayout.Label("Logic", GUILayout.Width(170f));
            GUILayout.Label("Source", GUILayout.Width(170f));
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
            EditorGUILayout.ObjectField(ability, typeof(AbilityDefinition), false, GUILayout.Width(220f));
            GUILayout.Label($"{info.OwnerName}.{info.PropertyPath}", GUILayout.MinWidth(220f));
            EditorGUILayout.ObjectField(ability != null ? ability.logic : null, typeof(AbilityLogic), false, GUILayout.Width(170f));
            EditorGUILayout.ObjectField(ability != null ? ability.sourceObject : null, typeof(UnityEngine.Object), false, GUILayout.Width(170f));

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

    private void RefreshMonsters()
    {
        monsterRows.Clear();

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { EnemyPrefabRoot }))
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

        bool usesStageHpScaling = prefab.GetComponent<Mob>() != null && prefab.GetComponent<BossControllerBase>() == null;
        return new MonsterProfileRow(prefab, prefabPath, attributeSet, baseProfile, overrideProfiles, expectedPath, status, usesStageHpScaling);
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

    private static bool IsCoreAttribute(AttributeDefinition attribute)
    {
        if (attribute == null)
            return false;

        string normalized = NormalizeAttributeName(attribute);
        return normalized is "health" or
            "maxhealth" or
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
            "maxstagger" => 2,
            "staggerresistance" => 3,
            "knockbackresistance" => 4,
            "attack" => 5,
            "attackbase" => 6,
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

        if (prefabPath.IndexOf("/Mobs/", StringComparison.OrdinalIgnoreCase) >= 0)
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

    private enum MonsterProfileStatus
    {
        Ready,
        Missing,
        SharedOrLegacy
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
            bool usesStageHpScaling)
        {
            Prefab = prefab;
            PrefabPath = prefabPath;
            AttributeSet = attributeSet;
            BaseProfile = baseProfile;
            OverrideProfiles = overrideProfiles;
            ExpectedProfilePath = expectedProfilePath;
            Status = status;
            UsesStageHpScaling = usesStageHpScaling;
        }

        public GameObject Prefab { get; }
        public string PrefabPath { get; }
        public AttributeSet AttributeSet { get; }
        public AttributeInitProfileSO BaseProfile { get; }
        public List<AttributeInitProfileSO> OverrideProfiles { get; }
        public string ExpectedProfilePath { get; }
        public MonsterProfileStatus Status { get; }
        public bool UsesStageHpScaling { get; }

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
