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
/// Compiles the authored Demon King primary Ink and links its repeat-dialogue selector
/// to the authored Demon King boss scene through Unity serialization.
/// </summary>
internal static class DemonKingLessonDialogueAuthoring
{
    private const string InkSourcePath = "Assets/_Project/Data/Dialogue/Ink/AnimatedVariants/DemonKingIntroDialogue_Animated.ink";
    private const string InkJsonPath = "Assets/_Project/Data/Dialogue/Ink/AnimatedVariants/DemonKingIntroDialogue_Animated.json";
    private const string DemonKingScenePath = "Assets/_Project/Scenes/LeeJunmo_Boss_DemonKing.unity";
    private static readonly string[] RepeatStartKnots =
    {
        "DEMONKING_LESSON",
        "DEMONKING_REPAIR_COST"
    };

    [MenuItem("Tools/Dialogue/Apply Demon King Lesson Dialogue")]
    public static void Apply()
    {
        CompileInk();
        LinkSelectorToBossScene();
        LocalizationSheetSetup.Import();
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

    private static void CompileInk()
    {
        if (!File.Exists(InkSourcePath))
            throw new FileNotFoundException("Demon King primary Ink source is missing.", InkSourcePath);

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
            throw new InvalidOperationException("Demon King lesson Ink compile failed.\n" + string.Join("\n", errors));

        // The Ink asset postprocessor can keep the current JSON file open while
        // this menu command runs. Match the project-wide compiler's atomic
        // replacement flow so we do not write through that cached handle.
        AssetDatabase.ReleaseCachedFileHandles();
        string temporaryJsonPath = InkJsonPath + ".lesson-tmp";
        File.WriteAllText(temporaryJsonPath, story.ToJson(), new UTF8Encoding(false));

        if (File.Exists(InkJsonPath))
            File.Replace(temporaryJsonPath, InkJsonPath, null);
        else
            File.Move(temporaryJsonPath, InkJsonPath);

        AssetDatabase.ImportAsset(InkJsonPath);
    }

    private static void LinkSelectorToBossScene()
    {
        Scene scene = EditorSceneManager.OpenScene(DemonKingScenePath, OpenSceneMode.Single);
        BossDialogueRunner[] runners = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<BossDialogueRunner>(true))
            .ToArray();

        if (runners.Length != 1)
            throw new InvalidOperationException($"Expected one Demon King BossDialogueRunner, found {runners.Length}.");

        BossDialogueRunner runner = runners[0];
        DemonKingDialogueStartKnotSelector selector = runner.GetComponent<DemonKingDialogueStartKnotSelector>();
        if (selector == null)
            selector = runner.gameObject.AddComponent<DemonKingDialogueStartKnotSelector>();

        SerializedObject serializedRunner = new SerializedObject(runner);
        serializedRunner.FindProperty("startKnotSelectorBehaviour").objectReferenceValue = selector;
        serializedRunner.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject serializedSelector = new SerializedObject(selector);
        SerializedProperty repeatStartKnots = serializedSelector.FindProperty("repeatStartKnots");
        repeatStartKnots.arraySize = RepeatStartKnots.Length;
        for (int index = 0; index < RepeatStartKnots.Length; index++)
            repeatStartKnots.GetArrayElementAtIndex(index).stringValue = RepeatStartKnots[index];

        serializedSelector.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(runner);
        EditorUtility.SetDirty(selector);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
