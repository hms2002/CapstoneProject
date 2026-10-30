using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityGAS;
using UnityEngine;

/// <summary>
/// Writes Demon King's conditional encounter rules through Unity serialization.
/// The first encounter deliberately has no matching rule, preserving the existing primary Ink dialogue.
/// </summary>
internal static class DemonKingEncounterDialogueAuthoring
{
    private const string DemonKingNpcPath = "Assets/_Project/Data/Dialogue/NPC/DarkLordNpcData.asset";
    private const string MeltaNpcPath = "Assets/_Project/Data/Dialogue/NPC/SlimeQueenBossNpc.asset";
    private const string SpiriNpcPath = "Assets/_Project/Data/Dialogue/NPC/DragonBossNpc.asset";
    private const string EncounterInkSourcePath = "Assets/_Project/Data/Dialogue/Ink/DemonKingBossEncounter.ink";
    private const string EncounterInkJsonPath = "Assets/_Project/Data/Dialogue/Ink/DemonKingBossEncounter.json";
    private const string HealthPath = "Assets/_Project/Data/Attributes/Definitions/HealthAttribute.asset";
    private const string MaxHealthPath = "Assets/_Project/Data/Attributes/Definitions/MaxHealthAttribute.asset";
    private const string MeltaCitizenshipMottoFlag = "melta.citizenship.motto_selected";

    [MenuItem("Tools/Dialogue/Apply Demon King Encounter Dialogue")]
    public static void Apply()
    {
        CompileEncounterInk();
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
        NPCData demonKing = AssetDatabase.LoadAssetAtPath<NPCData>(DemonKingNpcPath);
        NPCData melta = AssetDatabase.LoadAssetAtPath<NPCData>(MeltaNpcPath);
        NPCData spiri = AssetDatabase.LoadAssetAtPath<NPCData>(SpiriNpcPath);
        TextAsset encounterInk = AssetDatabase.LoadAssetAtPath<TextAsset>(EncounterInkJsonPath);
        AttributeDefinition health = AssetDatabase.LoadAssetAtPath<AttributeDefinition>(HealthPath);
        AttributeDefinition maxHealth = AssetDatabase.LoadAssetAtPath<AttributeDefinition>(MaxHealthPath);

        if (demonKing == null || melta == null || spiri == null || encounterInk == null || health == null || maxHealth == null)
            throw new InvalidOperationException("Demon King encounter authoring references are missing.");

        SerializedObject serializedNpc = new SerializedObject(demonKing);
        serializedNpc.FindProperty("bossEncounterInk").objectReferenceValue = encounterInk;
        SerializedProperty entries = serializedNpc.FindProperty("bossEncounterDialogues");
        entries.ClearArray();

        AddEntry(entries, "마왕의 인내심 임박", "DEMONKING_TIME_LOW", 1000,
            MetBoss(), Float(BossEncounterDialogueConditionType.RunRemainingSeconds, BossDialogueComparison.LessOrEqual, 60f));
        AddEntry(entries, "체력 부족", "DEMONKING_LOW_HEALTH", 900,
            MetBoss(), HealthRatio(BossDialogueComparison.LessOrEqual, 0.3f, health, maxHealth));
        AddEntry(entries, "직전 마왕전 패배", "DEMONKING_PREVIOUS_PLAYER_DEFEAT", 850,
            MetBoss(), LastOutcome(BossDialogueOutcome.PlayerDefeat));
        AddEntry(entries, "직전 마왕전 승리", "DEMONKING_PREVIOUS_PLAYER_VICTORY", 840,
            MetBoss(), LastOutcome(BossDialogueOutcome.PlayerVictory));
        AddEntry(entries, "직전 런 시간 초과", "DEMONKING_LAST_RUN_TIME_OVER", 820,
            MetBoss(), RunEnd(RunEndReason.TimeOver));
        AddEntry(entries, "일찍 도착", "DEMONKING_TIME_EARLY", 800,
            MetBoss(), Float(BossEncounterDialogueConditionType.RunRemainingSeconds, BossDialogueComparison.GreaterOrEqual, 480f));
        AddEntry(entries, "멜타 시민권 선택", "DEMONKING_MELTA_CITIZENSHIP", 760,
            MetBoss(), Flag(melta, MeltaCitizenshipMottoFlag));
        AddEntry(entries, "스피리 호감도", "DEMONKING_SPIRI_COMPANION", 750,
            MetBoss(), Int(BossEncounterDialogueConditionType.Affection, BossDialogueComparison.GreaterOrEqual, 1, spiri));
        AddEntry(entries, "마왕 호감도 높음", "DEMONKING_HIGH_AFFECTION", 720,
            MetBoss(), Int(BossEncounterDialogueConditionType.Affection, BossDialogueComparison.GreaterOrEqual, 5));
        AddEntry(entries, "반복 조우", "DEMONKING_FREQUENT_ENCOUNTER", 300,
            MetBoss(),
            Int(BossEncounterDialogueConditionType.EncounterCount, BossDialogueComparison.GreaterOrEqual, 5));
        AddEntry(entries, "마왕 재조우", "DEMONKING_REENCOUNTER", 100, MetBoss());

        serializedNpc.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(demonKing);
        AssetDatabase.SaveAssets();
    }

    private static void CompileEncounterInk()
    {
        if (!File.Exists(EncounterInkSourcePath))
            throw new FileNotFoundException("Demon King encounter Ink source is missing.", EncounterInkSourcePath);

        var errors = new List<string>();
        var compiler = new Ink.Compiler(File.ReadAllText(EncounterInkSourcePath), new Ink.Compiler.Options
        {
            sourceFilename = EncounterInkSourcePath,
            errorHandler = (message, kind) =>
            {
                if (kind == Ink.ErrorType.Error)
                    errors.Add(message);
            }
        });

        Ink.Runtime.Story story = compiler.Compile();
        if (story == null || errors.Count > 0)
            throw new InvalidOperationException("Demon King encounter Ink compile failed.\n" + string.Join("\n", errors));

        File.WriteAllText(EncounterInkJsonPath, story.ToJson(), new UTF8Encoding(false));
        AssetDatabase.ImportAsset(EncounterInkJsonPath);
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
            property.FindPropertyRelative("targetNpcOverride").objectReferenceValue = condition.TargetNpc;
            property.FindPropertyRelative("attribute").objectReferenceValue = condition.Attribute;
            property.FindPropertyRelative("maxAttribute").objectReferenceValue = condition.MaxAttribute;
            property.FindPropertyRelative("runEndReason").enumValueIndex = (int)condition.RunEndReason;
            property.FindPropertyRelative("lastBossEncounterOutcome").enumValueIndex = (int)condition.LastOutcome;
        }
    }

    private static ConditionSpec MetBoss() => Bool(BossEncounterDialogueConditionType.HasMetBoss, true);

    private static ConditionSpec Bool(BossEncounterDialogueConditionType type, bool value) =>
        new ConditionSpec(type, BossDialogueComparison.Equal, value, 0, 0f, null, null, null, null, RunEndReason.None, BossDialogueOutcome.None);

    private static ConditionSpec Int(BossEncounterDialogueConditionType type, BossDialogueComparison comparison, int value, NPCData targetNpc = null) =>
        new ConditionSpec(type, comparison, false, value, 0f, null, targetNpc, null, null, RunEndReason.None, BossDialogueOutcome.None);

    private static ConditionSpec Float(BossEncounterDialogueConditionType type, BossDialogueComparison comparison, float value) =>
        new ConditionSpec(type, comparison, false, 0, value, null, null, null, null, RunEndReason.None, BossDialogueOutcome.None);

    private static ConditionSpec HealthRatio(BossDialogueComparison comparison, float value, AttributeDefinition health, AttributeDefinition maxHealth) =>
        new ConditionSpec(BossEncounterDialogueConditionType.PlayerHealthRatio01, comparison, false, 0, value, null, null, health, maxHealth, RunEndReason.None, BossDialogueOutcome.None);

    private static ConditionSpec Flag(NPCData targetNpc, string flagId) =>
        new ConditionSpec(BossEncounterDialogueConditionType.BossDialogueFlag, BossDialogueComparison.Equal, true, 0, 0f, flagId, targetNpc, null, null, RunEndReason.None, BossDialogueOutcome.None);

    private static ConditionSpec RunEnd(RunEndReason reason) =>
        new ConditionSpec(BossEncounterDialogueConditionType.LastRunEndReason, BossDialogueComparison.Equal, true, 0, 0f, null, null, null, null, reason, BossDialogueOutcome.None);

    private static ConditionSpec LastOutcome(BossDialogueOutcome outcome) =>
        new ConditionSpec(BossEncounterDialogueConditionType.LastBossEncounterOutcome, BossDialogueComparison.Equal, true, 0, 0f, null, null, null, null, RunEndReason.None, outcome);

    private readonly struct ConditionSpec
    {
        public ConditionSpec(
            BossEncounterDialogueConditionType type,
            BossDialogueComparison comparison,
            bool expectedBool,
            int intValue,
            float floatValue,
            string stringValue,
            NPCData targetNpc,
            AttributeDefinition attribute,
            AttributeDefinition maxAttribute,
            RunEndReason runEndReason,
            BossDialogueOutcome lastOutcome)
        {
            Type = type;
            Comparison = comparison;
            ExpectedBool = expectedBool;
            IntValue = intValue;
            FloatValue = floatValue;
            StringValue = stringValue;
            TargetNpc = targetNpc;
            Attribute = attribute;
            MaxAttribute = maxAttribute;
            RunEndReason = runEndReason;
            LastOutcome = lastOutcome;
        }

        public BossEncounterDialogueConditionType Type { get; }
        public BossDialogueComparison Comparison { get; }
        public bool ExpectedBool { get; }
        public int IntValue { get; }
        public float FloatValue { get; }
        public string StringValue { get; }
        public NPCData TargetNpc { get; }
        public AttributeDefinition Attribute { get; }
        public AttributeDefinition MaxAttribute { get; }
        public RunEndReason RunEndReason { get; }
        public BossDialogueOutcome LastOutcome { get; }
    }
}
