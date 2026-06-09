#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class DemonKingEndingAuthoringTool
{
    private const string TargetSceneName = "LeeJunmo_Boss_DemonKing";
    private const string EndingDialoguePath = "Assets/_Project/Data/Dialogue/Ink/DemonKingEndingDialogue.json";
    private const string ExpectedNpcDataPath = "Assets/_Project/Data/Dialogue/NPC/DarkLordNpcData.asset";

    [MenuItem("Tools/DemonKing/Ending/Wire Ending Dialogue To Active Scene")]
    public static void WireEndingDialogueToActiveScene()
    {
        if (!CanEditActiveScene(out Scene scene))
            return;

        TextAsset endingDialogue = AssetDatabase.LoadAssetAtPath<TextAsset>(EndingDialoguePath);
        if (endingDialogue == null)
        {
            Debug.LogError($"[DemonKingEndingAuthoring] Missing ending dialogue asset at {EndingDialoguePath}.");
            return;
        }

        List<BossDefeatEndingSequence> sequences = FindSceneComponents<BossDefeatEndingSequence>(scene);
        if (sequences.Count != 1)
        {
            Debug.LogError($"[DemonKingEndingAuthoring] Expected exactly one BossDefeatEndingSequence in {TargetSceneName}, found {sequences.Count}.");
            return;
        }

        BossDefeatEndingSequence sequence = sequences[0];
        Undo.RecordObject(sequence, "Wire DemonKing ending dialogue");

        SerializedObject serialized = new(sequence);
        SerializedProperty dialogueInkProperty = serialized.FindProperty("dialogueInk");
        if (dialogueInkProperty == null)
        {
            Debug.LogError("[DemonKingEndingAuthoring] BossDefeatEndingSequence.dialogueInk property was not found.");
            return;
        }

        dialogueInkProperty.objectReferenceValue = endingDialogue;
        bool changed = serialized.ApplyModifiedProperties();

        if (changed)
        {
            EditorUtility.SetDirty(sequence);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        Debug.Log("[DemonKingEndingAuthoring] Wired BossDefeatEndingSequence.dialogueInk to DemonKingEndingDialogue.json. Existing NPC, outro, run-end, and scene transition fields were not changed.");
        ValidateActiveScene();
    }

    [MenuItem("Tools/DemonKing/Ending/Validate Active Scene")]
    public static void ValidateActiveScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        List<string> errors = new();
        List<string> warnings = new();

        if (!scene.IsValid() || !scene.isLoaded)
        {
            errors.Add("Active scene is not loaded.");
            LogValidation(errors, warnings);
            return;
        }

        if (!string.Equals(scene.name, TargetSceneName, StringComparison.Ordinal))
            errors.Add($"Active scene must be {TargetSceneName}; current scene is {scene.name}.");

        TextAsset expectedDialogue = AssetDatabase.LoadAssetAtPath<TextAsset>(EndingDialoguePath);
        if (expectedDialogue == null)
            errors.Add($"Missing ending dialogue asset at {EndingDialoguePath}.");

        NPCData expectedNpcData = AssetDatabase.LoadAssetAtPath<NPCData>(ExpectedNpcDataPath);
        if (expectedNpcData == null)
            warnings.Add($"Expected NPCData was not found at {ExpectedNpcDataPath}.");

        List<BossDefeatEndingSequence> sequences = FindSceneComponents<BossDefeatEndingSequence>(scene);
        if (sequences.Count == 0)
        {
            errors.Add("BossDefeatEndingSequence is missing.");
        }
        else
        {
            if (sequences.Count > 1)
                errors.Add($"Expected exactly one BossDefeatEndingSequence, found {sequences.Count}.");

            ValidateSequence(sequences[0], expectedDialogue, expectedNpcData, errors, warnings);
        }

        LogValidation(errors, warnings);
    }

    [MenuItem("Tools/DemonKing/Ending/Wire Ending Dialogue To Active Scene", true)]
    private static bool CanWireEndingDialogueToActiveScene()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    [MenuItem("Tools/DemonKing/Ending/Validate Active Scene", true)]
    private static bool CanValidateActiveScene()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    private static bool CanEditActiveScene(out Scene scene)
    {
        scene = SceneManager.GetActiveScene();

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[DemonKingEndingAuthoring] Cannot edit while Play Mode is active or changing.");
            return false;
        }

        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("[DemonKingEndingAuthoring] Active scene is not loaded.");
            return false;
        }

        if (!string.Equals(scene.name, TargetSceneName, StringComparison.Ordinal))
        {
            Debug.LogError($"[DemonKingEndingAuthoring] Active scene must be {TargetSceneName}; current scene is {scene.name}.");
            return false;
        }

        return true;
    }

    private static void ValidateSequence(
        BossDefeatEndingSequence sequence,
        TextAsset expectedDialogue,
        NPCData expectedNpcData,
        List<string> errors,
        List<string> warnings)
    {
        SerializedObject serialized = new(sequence);

        Object dialogueInk = GetObjectReference(serialized, "dialogueInk");
        if (dialogueInk == null)
            errors.Add("BossDefeatEndingSequence.dialogueInk is missing.");
        else if (expectedDialogue != null && dialogueInk != expectedDialogue)
            errors.Add("BossDefeatEndingSequence.dialogueInk is not DemonKingEndingDialogue.json.");

        Object dialogueNpcData = GetObjectReference(serialized, "dialogueNpcData");
        if (dialogueNpcData == null)
        {
            errors.Add("BossDefeatEndingSequence.dialogueNpcData is missing.");
        }
        else if (expectedNpcData != null && dialogueNpcData != expectedNpcData)
        {
            warnings.Add("BossDefeatEndingSequence.dialogueNpcData is not DarkLordNpcData.asset. The wiring tool preserves this field by design.");
        }

        if (GetObjectReference(serialized, "outroPlayer") == null)
            warnings.Add("BossDefeatEndingSequence.outroPlayer is missing.");

        string targetSceneName = GetString(serialized, "targetSceneName");
        if (string.IsNullOrWhiteSpace(targetSceneName))
            warnings.Add("BossDefeatEndingSequence.targetSceneName is empty.");
    }

    private static Object GetObjectReference(SerializedObject serialized, string propertyName)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        return property != null ? property.objectReferenceValue : null;
    }

    private static string GetString(SerializedObject serialized, string propertyName)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        return property != null ? property.stringValue : string.Empty;
    }

    private static List<T> FindSceneComponents<T>(Scene scene) where T : Component
    {
        List<T> results = new();
        T[] candidates = Resources.FindObjectsOfTypeAll<T>();

        for (int i = 0; i < candidates.Length; i++)
        {
            T candidate = candidates[i];
            if (candidate == null || EditorUtility.IsPersistent(candidate))
                continue;

            if (candidate.gameObject.scene == scene)
                results.Add(candidate);
        }

        return results;
    }

    private static void LogValidation(List<string> errors, List<string> warnings)
    {
        if (errors.Count == 0 && warnings.Count == 0)
        {
            Debug.Log("[DemonKingEndingAuthoring] Validation passed.");
            return;
        }

        for (int i = 0; i < errors.Count; i++)
            Debug.LogError($"[DemonKingEndingAuthoring] {errors[i]}");

        for (int i = 0; i < warnings.Count; i++)
            Debug.LogWarning($"[DemonKingEndingAuthoring] {warnings[i]}");
    }
}
#endif


