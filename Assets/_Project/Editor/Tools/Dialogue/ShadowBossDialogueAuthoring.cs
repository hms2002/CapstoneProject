using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 클로에의 Primary Ink를 컴파일하고 보스 씬에 재조우 본대사 선택기를 연결한다.
/// </summary>
internal static class ShadowBossDialogueAuthoring
{
    private const string InkSourcePath = "Assets/_Project/Data/Dialogue/Ink/AnimatedVariants/ShadowBossIntroDialogue_Animated.ink";
    private const string InkJsonPath = "Assets/_Project/Data/Dialogue/Ink/AnimatedVariants/ShadowBossIntroDialogue_Animated.json";
    private const string BossScenePath = "Assets/_Project/Scenes/HeoMinSeok_Boss_Shadow.unity";
    private static readonly string[] RepeatStartKnots =
    {
        "CHLOE_SILENT_WARDEN_PRACTICE",
        "CHLOE_MONTHLY_PRISONER_REPORT"
    };

    [MenuItem("Tools/Dialogue/Apply Chloe Dialogue")]
    public static void Apply()
    {
        CompileInk();
        LinkSelectorToBossScene();
        LocalizationSheetSetup.Import();
        AssetDatabase.SaveAssets();
        Debug.Log("[ChloeDialogue] Primary Ink compiled, repeat dialogue pool linked, and localization imported.");
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

    private static void CompileInk()
    {
        var errors = new List<string>();
        var compiler = new Ink.Compiler(File.ReadAllText(InkSourcePath), new Ink.Compiler.Options
        {
            sourceFilename = InkSourcePath,
            errorHandler = (message, kind) =>
            {
                if (kind == Ink.ErrorType.Error)
                    errors.Add(message);
            }
        });

        Ink.Runtime.Story story = compiler.Compile();
        if (story == null || errors.Count > 0)
            throw new InvalidOperationException("Chloe Ink compilation failed.\n" + string.Join("\n", errors));

        AssetDatabase.ReleaseCachedFileHandles();
        string temporaryJsonPath = InkJsonPath + ".chloe-tmp";
        File.WriteAllText(temporaryJsonPath, story.ToJson(), new UTF8Encoding(false));
        if (File.Exists(InkJsonPath))
            File.Replace(temporaryJsonPath, InkJsonPath, null);
        else
            File.Move(temporaryJsonPath, InkJsonPath);
        AssetDatabase.ImportAsset(InkJsonPath);
    }

    private static void LinkSelectorToBossScene()
    {
        Scene scene = EditorSceneManager.OpenScene(BossScenePath, OpenSceneMode.Single);
        BossDialogueRunner[] runners = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<BossDialogueRunner>(true))
            .ToArray();
        if (runners.Length == 0)
            throw new InvalidOperationException("The shadow boss scene has no dialogue runner.");

        // The scene has both a prefab runner and a scene-added runner on Chloe.
        // Both must resolve the same body regardless of which director invokes them.
        foreach (BossDialogueRunner runner in runners)
        {
            SerializedObject serializedRunner = new SerializedObject(runner);
            NPCData npcData = serializedRunner.FindProperty("npcData").objectReferenceValue as NPCData;
            if (npcData == null || npcData.id != 1003)
                throw new InvalidOperationException("The shadow boss runner is not bound to Chloe (1003).");

            ShadowBossDialogueStartKnotSelector selector = runner.GetComponent<ShadowBossDialogueStartKnotSelector>();
            if (selector == null)
                selector = runner.gameObject.AddComponent<ShadowBossDialogueStartKnotSelector>();

            serializedRunner.Update();
            serializedRunner.FindProperty("startKnotSelectorBehaviour").objectReferenceValue = selector;
            serializedRunner.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject serializedSelector = new SerializedObject(selector);
            SerializedProperty candidates = serializedSelector.FindProperty("repeatStartKnots");
            candidates.arraySize = RepeatStartKnots.Length;
            for (int index = 0; index < RepeatStartKnots.Length; index++)
                candidates.GetArrayElementAtIndex(index).stringValue = RepeatStartKnots[index];
            serializedSelector.ApplyModifiedPropertiesWithoutUndo();

            if (PrefabUtility.IsPartOfPrefabInstance(runner))
                PrefabUtility.RecordPrefabInstancePropertyModifications(runner);
            if (PrefabUtility.IsPartOfPrefabInstance(selector))
                PrefabUtility.RecordPrefabInstancePropertyModifications(selector);
            EditorUtility.SetDirty(runner);
            EditorUtility.SetDirty(selector);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
