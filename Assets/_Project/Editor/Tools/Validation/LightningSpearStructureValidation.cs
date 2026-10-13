using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Runs the existing synchronous placement fixtures without authoring a scene or gameplay asset.</summary>
[InitializeOnLoad]
public static class LightningSpearStructureValidation
{
    private const string Pending = "LightningSpearStructureValidation.Pending";
    private const string Finishing = "LightningSpearStructureValidation.Finishing";
    private const string ExitCode = "LightningSpearStructureValidation.ExitCode";

    static LightningSpearStructureValidation() => EditorApplication.update += Update;

    public static void Run()
    {
        if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Use a closed project in batch mode for the structure comparison.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetInt(ExitCode, 0);
        SessionState.SetBool(Pending, true);
        EditorApplication.isPlaying = true;
    }

    private static void Update()
    {
        if (SessionState.GetBool(Finishing, false) && !EditorApplication.isPlaying)
        {
            SessionState.SetBool(Finishing, false);
            EditorApplication.Exit(SessionState.GetInt(ExitCode, 1));
            return;
        }
        if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        if (Time.frameCount < 8) return;
        try
        {
            LightningSpearPlacementBenchmark.RunFixtureSuite();
            string[] args = Environment.GetCommandLineArgs();
            int argument = Array.IndexOf(args, "-lightningStructureReport");
            if (argument < 0 || argument + 1 >= args.Length) throw new InvalidOperationException("A report destination is required.");
            string destination = Path.GetFullPath(args[argument + 1]);
            Directory.CreateDirectory(destination);
            foreach (string report in Directory.GetFiles("Library/LightningSpearPlacementBenchmark", "*.json"))
                File.Copy(report, Path.Combine(destination, Path.GetFileName(report)), true);
            Debug.Log("LIGHTNING_STRUCTURE_VALIDATION_PASS: " + destination);
        }
        catch (Exception exception) { Debug.LogException(exception); SessionState.SetInt(ExitCode, 1); }
        finally
        {
            SessionState.SetBool(Pending, false);
            SessionState.SetBool(Finishing, true);
            EditorApplication.isPlaying = false;
        }
    }
}
