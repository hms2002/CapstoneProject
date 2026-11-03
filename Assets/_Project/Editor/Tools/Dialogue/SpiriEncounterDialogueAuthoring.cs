using System;
using UnityEditor;
using UnityGAS;
using UnityEngine;

/// <summary>
/// Writes Spiri's conditional encounter rules through Unity serialization.
/// The first encounter deliberately has no matching rule, preserving DRAGON_01.
/// </summary>
internal static class SpiriEncounterDialogueAuthoring
{
    private const string NpcPath = "Assets/_Project/Data/Dialogue/NPC/DragonBossNpc.asset";
    private const string EncounterInkPath = "Assets/_Project/Data/Dialogue/Ink/SpiriBossEncounter.json";
    private const string HealthPath = "Assets/_Project/Data/Attributes/Definitions/HealthAttribute.asset";
    private const string MaxHealthPath = "Assets/_Project/Data/Attributes/Definitions/MaxHealthAttribute.asset";
    private const string CrimsonBoundaryWeaponId = "Weapon.CrimsonBoundary";

    [MenuItem("Tools/Dialogue/Apply Spiri Encounter Dialogue")]
    public static void Apply()
    {
        ApplyInternal();
        AssetDatabase.SaveAssets();
    }

    public static void ApplyBatch()
    {
        try
        {
            Apply();
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void ApplyInternal()
    {
        NPCData npc = AssetDatabase.LoadAssetAtPath<NPCData>(NpcPath);
        TextAsset encounterInk = AssetDatabase.LoadAssetAtPath<TextAsset>(EncounterInkPath);
        AttributeDefinition health = AssetDatabase.LoadAssetAtPath<AttributeDefinition>(HealthPath);
        AttributeDefinition maxHealth = AssetDatabase.LoadAssetAtPath<AttributeDefinition>(MaxHealthPath);

        if (npc == null || encounterInk == null || health == null || maxHealth == null)
            throw new InvalidOperationException("Spiri encounter authoring references are missing.");

        SerializedObject serializedNpc = new SerializedObject(npc);
        serializedNpc.FindProperty("bossEncounterInk").objectReferenceValue = encounterInk;
        SerializedProperty entries = serializedNpc.FindProperty("bossEncounterDialogues");
        entries.ClearArray();

        AddEntry(entries, "마왕의 인내심 임박", "SPIRI_TIME_LOW", 1000,
            MetBoss(), Float(BossEncounterDialogueConditionType.RunRemainingSeconds, BossDialogueComparison.LessOrEqual, 60f));
        AddEntry(entries, "체력 부족", "SPIRI_LOW_HEALTH", 900,
            MetBoss(), HealthRatio(BossDialogueComparison.LessOrEqual, 0.3f, health, maxHealth));
        AddEntry(entries, "홍련의 한계선 소지", "SPIRI_CRIMSON_BOUNDARY", 800,
            MetBoss(), Item(BossEncounterDialogueConditionType.PlayerHasWeapon, CrimsonBoundaryWeaponId));
        AddEntry(entries, "스피리 이전 승리", "SPIRI_PREVIOUS_VICTORY", 700,
            MetBoss(), Int(BossEncounterDialogueConditionType.BossDefeatCount, BossDialogueComparison.GreaterOrEqual, 1));
        AddEntry(entries, "스피리 이전 패배", "SPIRI_PREVIOUS_DEFEAT", 650,
            MetBoss(), Int(BossEncounterDialogueConditionType.BossVictoryCount, BossDialogueComparison.GreaterOrEqual, 1));
        AddEntry(entries, "가방 가득 참", "SPIRI_BACKPACK_FULL", 600,
            MetBoss(), Bool(BossEncounterDialogueConditionType.BackpackIsFull, true));
        AddEntry(entries, "스피리 호감도 높음", "SPIRI_HIGH_AFFECTION", 500,
            MetBoss(), Int(BossEncounterDialogueConditionType.Affection, BossDialogueComparison.GreaterOrEqual, 5));
        AddEntry(entries, "스피리 재조우", "SPIRI_REENCOUNTER", 100, MetBoss());
        AddEntry(entries, "기본", "SPIRI_FALLBACK", 0, MetBoss());

        serializedNpc.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(npc);
        AssetDatabase.SaveAssets();
    }

    private static void AddEntry(SerializedProperty entries, string label, string startPath, int priority, params ConditionSpec[] conditions)
    {
        int index = entries.arraySize;
        entries.InsertArrayElementAtIndex(index);
        SerializedProperty entry = entries.GetArrayElementAtIndex(index);
        entry.FindPropertyRelative("label").stringValue = label;
        entry.FindPropertyRelative("startPath").stringValue = startPath;
        entry.FindPropertyRelative("inkJSON").objectReferenceValue = null;
        entry.FindPropertyRelative("priority").intValue = priority;

        SerializedProperty serializedConditions = entry.FindPropertyRelative("conditions");
        serializedConditions.ClearArray();
        for (int i = 0; i < conditions.Length; i++)
        {
            ConditionSpec condition = conditions[i];
            serializedConditions.InsertArrayElementAtIndex(i);
            SerializedProperty property = serializedConditions.GetArrayElementAtIndex(i);
            property.FindPropertyRelative("type").enumValueIndex = (int)condition.Type;
            property.FindPropertyRelative("comparison").enumValueIndex = (int)condition.Comparison;
            property.FindPropertyRelative("expectedBool").boolValue = condition.ExpectedBool;
            property.FindPropertyRelative("intValue").intValue = condition.IntValue;
            property.FindPropertyRelative("floatValue").floatValue = condition.FloatValue;
            property.FindPropertyRelative("stringValue").stringValue = condition.StringValue ?? string.Empty;
            property.FindPropertyRelative("attribute").objectReferenceValue = condition.Attribute;
            property.FindPropertyRelative("maxAttribute").objectReferenceValue = condition.MaxAttribute;
        }
    }

    private static ConditionSpec MetBoss() => Bool(BossEncounterDialogueConditionType.HasMetBoss, true);

    private static ConditionSpec Bool(BossEncounterDialogueConditionType type, bool value) =>
        new ConditionSpec(type, BossDialogueComparison.Equal, value, 0, 0f, null, null, null);

    private static ConditionSpec Int(BossEncounterDialogueConditionType type, BossDialogueComparison comparison, int value) =>
        new ConditionSpec(type, comparison, false, value, 0f, null, null, null);

    private static ConditionSpec Float(BossEncounterDialogueConditionType type, BossDialogueComparison comparison, float value) =>
        new ConditionSpec(type, comparison, false, 0, value, null, null, null);

    private static ConditionSpec Item(BossEncounterDialogueConditionType type, string itemId) =>
        new ConditionSpec(type, BossDialogueComparison.Equal, true, 0, 0f, itemId, null, null);

    private static ConditionSpec HealthRatio(BossDialogueComparison comparison, float value, AttributeDefinition health, AttributeDefinition maxHealth) =>
        new ConditionSpec(BossEncounterDialogueConditionType.PlayerHealthRatio01, comparison, false, 0, value, null, health, maxHealth);

    private readonly struct ConditionSpec
    {
        public ConditionSpec(BossEncounterDialogueConditionType type, BossDialogueComparison comparison, bool expectedBool, int intValue, float floatValue, string stringValue, AttributeDefinition attribute, AttributeDefinition maxAttribute)
        {
            Type = type;
            Comparison = comparison;
            ExpectedBool = expectedBool;
            IntValue = intValue;
            FloatValue = floatValue;
            StringValue = stringValue;
            Attribute = attribute;
            MaxAttribute = maxAttribute;
        }

        public BossEncounterDialogueConditionType Type { get; }
        public BossDialogueComparison Comparison { get; }
        public bool ExpectedBool { get; }
        public int IntValue { get; }
        public float FloatValue { get; }
        public string StringValue { get; }
        public AttributeDefinition Attribute { get; }
        public AttributeDefinition MaxAttribute { get; }
    }
}
