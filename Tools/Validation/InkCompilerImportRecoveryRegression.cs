// Run only in an isolated Unity project containing Assets/Ink and this Editor script.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Reflection;
using Ink.UnityIntegration;
using UnityEditor;
using UnityEngine;
using Story = Ink.Runtime.Story;

public static class InkCompilerImportRecoveryRegression
{
    private const string InkPath = "Assets/ImportRecovery/Dialogue.ink";
    private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
    private static readonly List<string> Errors = new List<string>();
    private static int checks;

    public static void Run()
    {
        try
        {
            InkSettings.instance.compileAllFilesAutomatically = false;
            Application.logMessageReceived += CaptureError;
            Directory.CreateDirectory("Assets/ImportRecovery");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            WriteInk("Original merchant dialogue.");
            Compile();
            Check(ReadDialogue() == "Original merchant dialogue.", "Successful compilation changed dialogue");
            Check(Errors.Count == 0, "Successful compilation logged an error");
            CheckReleased("success");

            string jsonPath = Path.GetFullPath(GetInkFile().jsonPath);
            byte[] originalJson = File.ReadAllBytes(jsonPath);
            WriteInk("상인 대사 재컴파일.");
            InkCompiler.SetBuildBlocked();
            SetPrivate("playModeBlocked", true);

            // A live mapped section reproduces Windows 1224 when File.WriteAllText truncates the JSON.
            using (var stream = new FileStream(jsonPath, FileMode.Open, FileAccess.Read,
                       FileShare.ReadWrite | FileShare.Delete))
            using (var mapped = MemoryMappedFile.CreateFromFile(stream, null, 0,
                       MemoryMappedFileAccess.Read, HandleInheritability.None, true))
            using (var view = mapped.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read))
            {
                Compile();
                Check(view.ReadByte(0) == originalJson[0], "Failed save modified the mapped JSON");
            }

            Check(File.ReadAllBytes(jsonPath).SequenceEqual(originalJson), "Failed save changed existing JSON");
            InkFile failedFile = GetInkFile();
            Check(failedFile.hasUnhandledCompileErrors, "Write failure was not retained as a compiler error");
            Check(failedFile.unhandledCompileErrors.Any(error => error.Contains(jsonPath)),
                "Write error omitted the failed JSON path");
            Check(Errors.Any(error => error.Contains("Failed to write compiled Ink JSON")),
                "Write error was not reported to the Console");
            Check(Errors.Any(error => error.Contains("1224")), "Fixture did not reproduce Windows 1224");
            Check(Errors.Any(error => error.Contains("Ink compilation completed with errors")),
                "Failed compilation was reported as successful");
            Check(!GetPrivate<bool>("playModeBlocked") && !EditorApplication.isPlayingOrWillChangePlaymode,
                "Failed compilation attempted to enter Play Mode");
            CheckReleased("locked-json");

            Errors.Clear();
            Compile();
            Check(ReadDialogue() == "상인 대사 재컴파일.", "Retry did not write the new UTF-8 dialogue");
            Check(!GetInkFile().hasUnhandledCompileErrors, "Retry retained the previous write error");
            Check(Errors.Count == 0, "Successful retry logged an error");
            CheckReleased("retry");

            // Preserve the existing syntax-error path, where there is no compiled JSON to write.
            WriteInk("-> missing_knot");
            byte[] beforeSyntaxError = File.ReadAllBytes(jsonPath);
            Compile();
            Check(GetInkFile().hasErrors, "Invalid Ink was not reported as a syntax error");
            Check(File.ReadAllBytes(jsonPath).SequenceEqual(beforeSyntaxError),
                "Syntax error replaced the previous compiled story");
            CheckReleased("syntax-error");

            string result = "INK_IMPORT_RECOVERY_PASS: " + checks +
                            " checks; success, Windows 1224, Console error, preserved JSON, " +
                            "import/auto-refresh/progress/build/reload cleanup, UTF-8 retry, syntax error.";
            File.WriteAllText("../native-validation.txt", result);
            Debug.Log(result);
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            File.WriteAllText("../native-validation.txt", "INK_IMPORT_RECOVERY_FAIL: " + error);
            EditorApplication.Exit(1);
        }
        finally
        {
            Application.logMessageReceived -= CaptureError;
        }
    }

    private static void WriteInk(string text)
    {
        File.WriteAllText(InkPath, text + "\n-> END\n");
        AssetDatabase.ImportAsset(InkPath, ImportAssetOptions.ForceSynchronousImport);
        InkLibrary.CreateOrReadUpdatedInkFiles(new List<string> { InkPath });
    }

    private static InkFile GetInkFile()
    {
        InkFile file = InkLibrary.GetInkFileWithPath(InkPath);
        if (file == null) throw new Exception("Fixture Ink file was not imported");
        return file;
    }

    private static void Compile()
    {
        InkCompiler.CompileInk(new[] { GetInkFile() }, true);
    }

    private static string ReadDialogue()
    {
        var story = new Story(File.ReadAllText(GetInkFile().jsonPath));
        return story.Continue().Trim();
    }

    private static void CheckReleased(string phase)
    {
        Check(!InkCompiler.executingCompilationStack, phase + ": compilation queue was not cleared");
        Check(!(bool)typeof(InkCompiler).GetProperty("disallowedAutoRefresh", PrivateStatic).GetValue(null),
            phase + ": auto refresh was not restored");
        Check(!GetPrivate<bool>("buildBlocked"), phase + ": build remained blocked");
        Check(GetPrivate<int>("compileProgressID") == -1, phase + ": compilation progress remained active");
        typeof(InkCompiler).GetMethod("Update", PrivateStatic).Invoke(null, null);
        Check(!GetPrivate<bool>("hasLockedUnityCompilation"), phase + ": assembly reload remained locked");

        string probePath = "Assets/ImportRecovery/" + phase + ".txt";
        File.WriteAllText(probePath, phase);
        AssetDatabase.ImportAsset(probePath, ImportAssetOptions.ForceSynchronousImport);
        var probe = AssetDatabase.LoadAssetAtPath<TextAsset>(probePath);
        Check(probe != null && probe.text == phase, phase + ": asset import did not resume");
    }

    private static T GetPrivate<T>(string name)
    {
        return (T)typeof(InkCompiler).GetField(name, PrivateStatic).GetValue(null);
    }

    private static void SetPrivate(string name, object value)
    {
        typeof(InkCompiler).GetField(name, PrivateStatic).SetValue(null, value);
    }

    private static void CaptureError(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            Errors.Add(message);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
    }
}
