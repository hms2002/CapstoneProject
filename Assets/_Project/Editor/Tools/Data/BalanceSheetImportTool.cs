using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

internal static class BalanceSheetImportTool
{
    private const string InputDirectory = "DataSheets/Balance";
    private const string StagePath = "Assets/_Project/Resources/MonsterStageHpScalingSettings.asset";
    private const string ExperiencePath = "Assets/_Project/Data/Progression/Leveling/LevelProgressionConfig.asset";
    private const string GoldCodePath = "Assets/_Project/Runtime/Core/Scaling/BalanceGoldValues.g.cs";

    [MenuItem("Tools/Balance Sheets/Validate CSV")]
    private static void Validate()
    {
        try
        {
            Load(out BalanceCsvDocument document, out SerializedObject stage, out SerializedObject experience);
            Debug.Log(DescribeChanges(document, stage, experience));
        }
        catch (Exception e) { Debug.LogError($"[Balance Sheets] Validation failed; nothing applied. {e.Message}"); }
    }

    [MenuItem("Tools/Balance Sheets/Open CSV Folder")]
    private static void OpenFolder() => EditorUtility.RevealInFinder(Path.GetFullPath(InputDirectory));

    [MenuItem("Tools/Balance Sheets/Apply CSV")]
    private static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        {
            Debug.LogWarning("[Balance Sheets] Exit Play Mode and wait for compilation before applying.");
            return;
        }

        try
        {
            Load(out BalanceCsvDocument document, out SerializedObject stage, out SerializedObject experience);
            string summary = DescribeChanges(document, stage, experience);
            Debug.Log(summary);
            bool stageChanged = stage.FindProperty("enabled").boolValue != document.ScalingEnabled ||
                                stage.FindProperty("hpMultiplierPerClearedStage").floatValue != document.HpIncrement;
            bool experienceChanged = !ReadRequirements(experience).SequenceEqual(document.Experience);
            string newCode = document.GenerateGoldCode();
            bool codeChanged = Normalize(File.ReadAllText(GoldCodePath)) != Normalize(newCode);
            if (!stageChanged && !experienceChanged && !codeChanged)
            {
                Debug.Log("[Balance Sheets] CSV already matches game data; no files changed.");
                return;
            }
            if (!EditorUtility.DisplayDialog("Apply balance CSV", summary +
                    "\nOnly the listed settings are applied. Gold code changes require recompilation.", "Apply", "Cancel")) return;

            // Preserve pending Inspector edits; never save an unrelated dirty asset through this tool.
            if ((stageChanged && EditorUtility.IsDirty(stage.targetObject)) ||
                (experienceChanged && EditorUtility.IsDirty(experience.targetObject)))
                throw new InvalidOperationException("Save pending Inspector changes in target assets first.");

            string[] paths = new[] { StagePath, ExperiencePath, GoldCodePath };
            byte[][] backups = paths.Select(File.ReadAllBytes).ToArray();
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply balance CSV");
            try
            {
                if (stageChanged)
                {
                    Undo.RecordObject(stage.targetObject, "Apply stage balance CSV");
                    stage.FindProperty("enabled").boolValue = document.ScalingEnabled;
                    stage.FindProperty("hpMultiplierPerClearedStage").floatValue = document.HpIncrement;
                    stage.ApplyModifiedProperties();
                    AssetDatabase.SaveAssetIfDirty(stage.targetObject);
                }
                if (experienceChanged)
                {
                    Undo.RecordObject(experience.targetObject, "Apply experience balance CSV");
                    var array = experience.FindProperty("nextLevelRequirements");
                    array.arraySize = document.Experience.Length;
                    for (int i = 0; i < document.Experience.Length; i++)
                        array.GetArrayElementAtIndex(i).intValue = document.Experience[i];
                    experience.ApplyModifiedProperties();
                    AssetDatabase.SaveAssetIfDirty(experience.targetObject);
                }
                if (codeChanged) File.WriteAllText(GoldCodePath, newCode, new UTF8Encoding(false));
            }
            catch
            {
                Undo.RevertAllDownToGroup(undoGroup);
                for (int i = 0; i < paths.Length; i++) File.WriteAllBytes(paths[i], backups[i]);
                AssetDatabase.ImportAsset(StagePath, ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset(ExperiencePath, ImportAssetOptions.ForceUpdate);
                AssetDatabase.Refresh();
                throw;
            }
            Undo.CollapseUndoOperations(undoGroup);
            if (codeChanged) AssetDatabase.ImportAsset(GoldCodePath);
            Debug.Log("[Balance Sheets] Applied. Asset values support Undo; generated gold code uses version control for rollback.");
        }
        catch (Exception e) { Debug.LogError($"[Balance Sheets] Apply failed: {e.Message}"); }
    }

    private static void Load(out BalanceCsvDocument document, out SerializedObject stage, out SerializedObject experience)
    {
        document = BalanceCsvDocument.Read(InputDirectory);
        stage = Target<MonsterStageHpScalingSettings>(StagePath);
        experience = Target<LevelProgressionConfigSO>(ExperiencePath);
        if (stage.FindProperty("enabled") == null || stage.FindProperty("hpMultiplierPerClearedStage") == null ||
            experience.FindProperty("nextLevelRequirements") == null || !File.Exists(GoldCodePath))
            throw new InvalidOperationException("Expected balance fields or generated gold file are missing.");
    }

    private static SerializedObject Target<T>(string path) where T : UnityEngine.Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) throw new InvalidOperationException($"Missing {typeof(T).Name}: {path}");
        return new SerializedObject(asset);
    }

    private static int[] ReadRequirements(SerializedObject experience)
    {
        var array = experience.FindProperty("nextLevelRequirements");
        return Enumerable.Range(0, array.arraySize).Select(i => array.GetArrayElementAtIndex(i).intValue).ToArray();
    }

    private static string DescribeChanges(BalanceCsvDocument document, SerializedObject stage, SerializedObject experience)
    {
        var summary = new StringBuilder("[Balance Sheets] Validated all 3 CSV files.\n");
        summary.Append("Stage scaling: ").Append(stage.FindProperty("enabled").boolValue).Append(" → ")
            .Append(document.ScalingEnabled).Append("\nHP increment: ")
            .Append(stage.FindProperty("hpMultiplierPerClearedStage").floatValue.ToString(CultureInfo.InvariantCulture))
            .Append(" → ").Append(document.HpIncrement.ToString(CultureInfo.InvariantCulture)).Append('\n');
        summary.Append("Required EXP: ").Append(string.Join(", ", ReadRequirements(experience)))
            .Append(" → ").Append(string.Join(", ", document.Experience)).Append('\n');
        foreach (string key in BalanceCsvDocument.GoldKeys)
        {
            int current = (int)typeof(BalanceGoldValues).GetField(key).GetRawConstantValue();
            summary.Append(key).Append(": ").Append(current).Append(" → ").Append(document.Gold[key]).Append('\n');
        }
        return summary.ToString();
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n");
}
