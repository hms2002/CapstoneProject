using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.Localization.Plugins.CSV;
using UnityEditor.Localization.Plugins.CSV.Columns;
using UnityEngine;
using UnityGAS;

/// <summary>기존 유물 7종의 공격력·깃털 피해를 승인된 고정 수치로 이행한다.</summary>
public static class FixedAttackRelicAuthoring
{
    private const string Root = "Assets/_Project/Data/Items/Relics/";
    private const string ReportFolder = "Library/CodexFixedAttackRelics";
    private const string FeatherCodeKey = "code.reliclogic_featherorbit_managed.cef8ff8aec";
    private static readonly string[] Names = { "WornWhetstone", "MasterWhetstone", "HeavyBracelet", "LastBreath", "RedContract", "RotatingIornBall", "FeatherOrbit" };
    private static readonly string[] StrategyNames =
    {
        "Relic Logic_Common_WornWhetstone", "Relic Logic_Common_MasterWhetstone", "Relic Logic_Common_HeavyBracelet",
        "Relic Logic_Common_LastBreath", "Relic Logic_Common_RedContract", "Relic Logic_Move Speed On Kill_Managed", "Relic Logic_Feather Orbit_Managed"
    };
    private static readonly string[] DefinitionGuids =
    {
        "eef19b108eae4400bb49fc1753e4aa14", "5dfae16cf310446b821ffaf6e7a1bae3", "16ad7526693d4a44a3183bcc3df90f61",
        "4a278341ae1b4ef4b8886c04f3517f71", "6e61f67002664297a735902776929cf5", "d492a6f1c1a7fc84bb3b44a619dcfd4f", "eb057c0a7b239b742a001ee568ef4027"
    };
    private static readonly string[] StrategyGuids =
    {
        "348ad3a604f1416bad4cdbf897ace578", "c09af03ddc034e87b1f9044305ef296b", "f6f4ed2e6a75450a9200b6c6db29ef75",
        "fbdac07eef844692b91bd97f7db59ce3", "44860a1e49e148fa8cdb24678044cc9e", "24602c35b51a9f64f8622e1094c8f5b2", "74bcbc64a07fc854ca2dade608e8c24c"
    };
    private static readonly string[] Descriptions =
    {
        "공격력이 고정 수치로 증가합니다.",
        "공격력이 고정 수치로 증가하고, 기본공격 피해가 비율로 증가합니다.",
        "이동속도가 비율로 감소하고, 공격력이 고정 수치로 증가합니다.",
        "체력이 50% 이하일 때 공격력이 3 증가합니다.",
        "적 처치 시 4초 동안 공격력이 2 증가합니다.",
        "적 처치 시 4초 동안 공격력이 2 증가하고 이동속도가 10% 증가합니다.",
        "플레이어 주위를 도는 깃털을 생성합니다. 레벨에 따라 고정 피해 3/4/5/6/7을 주며, 이동속도가 높을수록 더 빠르게 회전합니다."
    };
    private static readonly string[] Templates =
    {
        "● 체력이 {max_ratio} 이하일 때 [[{stat}]] {value}",
        "● {trigger} {duration} 동안 [[{stat}]] {value}",
        "● 적 처치 시 {duration} 동안 [[공격력]] {attack_bonus}\n● [[이동속도]] {move_speed_bonus}",
        "● 깃털 {feather_count}개가 주위를 회전하며 적을 공격\n● 깃털 피해 {damage_coef}\n● 회전 반경 {radius}\n● 같은 적을 다시 공격하는 기본 간격 {hit_cooldown}"
    };

    public static void ApplyBatch() => Run(true);
    public static void ReadbackBatch() => Run(false);

    private static void Run(bool apply)
    {
        try
        {
            var attackAdd = Load<AttributeDefinition>("Assets/_Project/Data/Attributes/Definitions/DamageCalcAttribute/AttackAddAttribute.asset");
            var attackMul = Load<AttributeDefinition>("Assets/_Project/Data/Attributes/Definitions/DamageCalcAttribute/AttackMulAttribute.asset");
            var database = Load<ItemDatabase>("Assets/_Project/Data/Items/ItemDatabase.asset");
            var report = new Report();
            for (int i = 0; i < Names.Length; i++)
            {
                string definitionPath = (Names[i] == "FeatherOrbit"
                    ? "Assets/_Project/Unused/Items/Relics/" : Root + "Definitions/") + "RD_" + Names[i] + ".asset";
                string strategyPath = Root + "Strategies/" + StrategyNames[i] + ".asset";
                var definition = Load<RelicDefinition>(definitionPath);
                var logic = definition.logic;
                Require(logic != null && AssetDatabase.GetAssetPath(logic) == strategyPath, "Unexpected strategy: " + definitionPath);
                Require(AssetDatabase.AssetPathToGUID(definitionPath) == DefinitionGuids[i]
                    && AssetDatabase.AssetPathToGUID(strategyPath) == StrategyGuids[i], "Asset identity changed: " + Names[i]);
                Require(definition.relicId == "RD_" + Names[i] && definition.icon != null
                    && database.allRelics.Count(r => r == definition) == 1, "Definition identity or registration mismatch: " + Names[i]);
                var serialized = new SerializedObject(logic);
                float[] curve = i == 2 ? new[] { 2f, 3f, 4f } : new[] { 1f, 2f, 3f };
                if (i < 3)
                {
                    Require(logic is RelicLogic_StatModifiers, "Unexpected generic strategy type.");
                    var entry = serialized.FindProperty("entries").GetArrayElementAtIndex(i == 2 ? 1 : 0);
                    if (apply)
                    {
                        entry.FindPropertyRelative("attribute").objectReferenceValue = attackAdd;
                        entry.FindPropertyRelative("type").intValue = (int)ModifierType.Flat;
                        entry.FindPropertyRelative("value").floatValue = curve[0];
                        SetCurve(entry.FindPropertyRelative("valueByLevel"), curve);
                    }
                    Require(entry.FindPropertyRelative("attribute").objectReferenceValue == attackAdd
                        && entry.FindPropertyRelative("type").intValue == (int)ModifierType.Flat
                        && Mathf.Approximately(entry.FindPropertyRelative("value").floatValue, curve[0])
                        && CurveMatches(entry.FindPropertyRelative("valueByLevel"), curve), "Flat attack curve mismatch: " + Names[i]);
                    if (i == 1) ValidateOtherEntry(serialized, 1, "f412c7da2733d71489ec2687fd2f7134", 0.03f, new[] { 0.03f, 0.05f, 0.08f });
                    if (i == 2) ValidateOtherEntry(serialized, 0, "23db6468e859c6f42b0230a5af7ca81c", -0.02f, new[] { -0.04f, -0.03f, -0.02f });
                }
                else if (i < 5)
                {
                    Require(i == 3 ? logic is RelicLogic_StatWhileHealthRatio_Managed : logic is RelicLogic_TimedStatOnGameplayEvent_Managed,
                        "Unexpected conditional strategy type.");
                    if (apply)
                    {
                        serialized.FindProperty("attribute").objectReferenceValue = attackAdd;
                        serialized.FindProperty("modifierType").intValue = (int)ModifierType.Flat;
                        serialized.FindProperty("value").floatValue = i == 3 ? 3f : 2f;
                    }
                    Require(serialized.FindProperty("attribute").objectReferenceValue == attackAdd
                        && serialized.FindProperty("modifierType").intValue == (int)ModifierType.Flat
                        && Mathf.Approximately(serialized.FindProperty("value").floatValue, i == 3 ? 3f : 2f), "Conditional attack mismatch: " + Names[i]);
                    if (i == 3) Require(serialized.FindProperty("minHealthRatioInclusive").floatValue == 0f
                        && serialized.FindProperty("maxHealthRatioInclusive").floatValue == 0.5f, "Health condition changed.");
                    if (i == 4) Require(serialized.FindProperty("durationSeconds").floatValue == 4f
                        && serialized.FindProperty("refreshDuration").boolValue, "Red Contract duration policy changed.");
                }
                else if (i == 5)
                {
                    Require(logic is RelicLogic_MoveSpeedOnKill_Managed, "Unexpected iron ball strategy type.");
                    if (apply)
                    {
                        serialized.FindProperty("attackAttribute").objectReferenceValue = attackAdd;
                        serialized.FindProperty("attackPercentBonus").floatValue = 2f;
                    }
                    Require(serialized.FindProperty("attackAttribute").objectReferenceValue == attackAdd
                        && serialized.FindProperty("attackPercentBonus").floatValue == 2f
                        && serialized.FindProperty("percentBonus").floatValue == 0.1f
                        && serialized.FindProperty("durationSeconds").floatValue == 4f
                        && serialized.FindProperty("refreshDuration").boolValue, "Iron ball attack or retained movement policy mismatch.");
                }
                else
                {
                    Require(logic is RelicLogic_FeatherOrbit_Managed, "Unexpected feather strategy type.");
                    float[] featherCurve = { 3f, 4f, 5f, 6f, 7f };
                    if (apply)
                    {
                        serialized.FindProperty("damageCoef").floatValue = 3f;
                        SetCurve(serialized.FindProperty("damageCoefByLevel"), featherCurve);
                    }
                    Require(serialized.FindProperty("damageCoef").floatValue == 3f
                        && CurveMatches(serialized.FindProperty("damageCoefByLevel"), featherCurve)
                        && serialized.FindProperty("attackStatId").intValue == 13, "Fixed feather damage or compatibility field mismatch.");
                }
                if (i >= 3)
                {
                    if (apply) serialized.FindProperty("effectTemplate").stringValue = Templates[i - 3];
                    Require(serialized.FindProperty("effectTemplate").stringValue == Templates[i - 3], "Template mismatch: " + Names[i]);
                }
                if (apply)
                {
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    definition.description = Descriptions[i];
                    EditorUtility.SetDirty(logic); EditorUtility.SetDirty(definition);
                }
                Require(definition.description == Descriptions[i], "Description mismatch: " + Names[i]);
                report.entries.Add(new Entry { definitionPath = definitionPath, strategyPath = strategyPath,
                    definitionGuid = DefinitionGuids[i], strategyGuid = StrategyGuids[i], relicId = definition.relicId });
                report.texts.Add(Text(definition, "description", Descriptions[i]));
                if (i >= 3) report.texts.Add(Text(logic, "effectTemplate", Templates[i - 3]));
            }
            if (apply) AssetDatabase.SaveAssets();
            foreach (var definition in database.allRelics.Where(r => r != null && r.logic != null))
            {
                var property = new SerializedObject(definition.logic).GetIterator();
                while (property.NextVisible(true))
                    Require(property.propertyType != SerializedPropertyType.ObjectReference || property.objectReferenceValue != attackMul,
                        "Registered relic still references attack multiplier: " + definition.name + "/" + property.propertyPath);
            }
            Directory.CreateDirectory(ReportFolder);
            File.WriteAllText(ReportFolder + "/authoring.json", JsonUtility.ToJson(report, true));
            Debug.Log("[FixedAttackRelics] Seven relics validated; no registered relic AttackMul references remain.");
            EditorApplication.delayCall += () => EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.delayCall += () => EditorApplication.Exit(1); }
    }

    public static void LocalizeBatch()
    {
        try
        {
            var collection = Load<StringTableCollection>("Assets/_Project/Data/Localization/GameText.asset");
            var report = JsonUtility.FromJson<Report>(File.ReadAllText(ReportFolder + "/authoring.json"));
            string[] codes = { "ko", "en", "ja", "zh-Hans", "zh-Hant" };
            var columns = new List<CsvColumns> { new KeyIdColumns { KeyFieldName = "key", IncludeId = false, IncludeSharedComments = false } };
            foreach (string code in codes) columns.Add(new LocaleColumns { LocaleIdentifier = code, FieldName = code, IncludeComments = false });
            using (var reader = File.OpenText(ReportFolder + "/content.csv")) Csv.ImportInto(reader, collection, columns, true);
            foreach (string code in codes)
            {
                var table = collection.StringTables.Single(t => t.LocaleIdentifier.Code == code);
                foreach (string key in report.texts.Select(r => r.key).Append(FeatherCodeKey))
                    Require(!string.IsNullOrWhiteSpace(table.GetEntry(key)?.Value), "Missing fixed attack translation: " + code + "/" + key);
                EditorUtility.SetDirty(table);
            }
            EditorUtility.SetDirty(collection.SharedData); AssetDatabase.SaveAssets();
            Debug.Log("[FixedAttackRelics] Twelve reviewed text keys populated in five locales.");
            Run(false);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.delayCall += () => EditorApplication.Exit(1); }
    }

    private static TextRow Text(UnityEngine.Object asset, string field, string text)
    {
        string path = AssetDatabase.GetAssetPath(asset);
        return new TextRow { path = path, type = asset.GetType().Name, owner = asset.name, field = field, text = text,
            identity = asset.GetType().Name + "/" + asset.name + "/" + field, key = "asset." + AssetDatabase.AssetPathToGUID(path) + "." + field };
    }

    private static void ValidateOtherEntry(SerializedObject serialized, int index, string guid, float value, float[] curve)
    {
        var entry = serialized.FindProperty("entries").GetArrayElementAtIndex(index);
        Require(AssetDatabase.GetAssetPath(entry.FindPropertyRelative("attribute").objectReferenceValue) == AssetDatabase.GUIDToAssetPath(guid)
            && entry.FindPropertyRelative("type").intValue == (int)ModifierType.Percent
            && Mathf.Approximately(entry.FindPropertyRelative("value").floatValue, value)
            && CurveMatches(entry.FindPropertyRelative("valueByLevel"), curve), "Non-attack percentage entry changed.");
    }

    private static void SetCurve(SerializedProperty property, float[] values)
    {
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).floatValue = values[i];
    }

    private static bool CurveMatches(SerializedProperty property, float[] values) => property.arraySize == values.Length
        && Enumerable.Range(0, values.Length).All(i => Mathf.Approximately(property.GetArrayElementAtIndex(i).floatValue, values[i]));
    private static T Load<T>(string path) where T : UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(path)
        ?? throw new InvalidOperationException("Missing or incompatible asset: " + path);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    [Serializable] private sealed class Report { public List<Entry> entries = new(); public List<TextRow> texts = new(); }
    [Serializable] private sealed class Entry { public string definitionPath, strategyPath, definitionGuid, strategyGuid, relicId; }
    [Serializable] private sealed class TextRow { public string path, type, owner, field, text, identity, key; }
}
