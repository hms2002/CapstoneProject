using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using CapstonePresentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Keeps test fixture presentation spawns out of the project-wide prewarm history.</summary>
[InitializeOnLoad]
public static class TestScenePresentationTraceIsolation
{
    private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
    private static readonly EmptyTrace testTrace = new();
    private static IPresentationPrewarmTraceBackend originalBackend;
    private static Action<PlayModeStateChange> originalPlayCallback;
    private static EditorApplication.CallbackFunction originalUpdateCallback;
    private static bool installed;
    // Explicit validation runs use disposable fixtures even when the last open
    // Editor scene is Title. Their presentation spawns must not enter project history.
    private static readonly bool uiValidationTests = Array.IndexOf(Environment.GetCommandLineArgs(), "-uiBatchValidationTests") >= 0;

    static TestScenePresentationTraceIsolation()
    {
        EditorApplication.update += Update;
        if (uiValidationTests || SessionState.GetBool("TestSceneValidation.Pending", false)) Enter();
    }

    private static void Update()
    {
        if (uiValidationTests || (EditorApplication.isPlayingOrWillChangePlaymode && SceneManager.GetActiveScene().name == "TestScene")) Enter();
        else if (!EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().name != "TestScene") Exit();
    }

    public static void Enter()
    {
        if (installed) return;
        RuntimeHelpers.RunClassConstructor(typeof(PrewarmTraceRuntime).TypeHandle);
        originalBackend = (IPresentationPrewarmTraceBackend)typeof(PresentationPrewarmTracePlayback).GetField("backend", PrivateStatic).GetValue(null);
        originalPlayCallback = (Action<PlayModeStateChange>)Delegate.CreateDelegate(typeof(Action<PlayModeStateChange>), typeof(PrewarmTraceRuntime).GetMethod("HandlePlayModeStateChanged", PrivateStatic));
        originalUpdateCallback = (EditorApplication.CallbackFunction)Delegate.CreateDelegate(typeof(EditorApplication.CallbackFunction), typeof(PrewarmTraceRuntime).GetMethod("FlushIfNeeded", PrivateStatic));
        EditorApplication.playModeStateChanged -= originalPlayCallback;
        EditorApplication.update -= originalUpdateCallback;
        PresentationPrewarmTracePlayback.RegisterBackend(testTrace);
        installed = true;
    }

    private static void Exit()
    {
        if (!installed) return;
        // Only the temporary test session is discarded; existing history files are untouched.
        typeof(PrewarmTraceRuntime).GetMethod("ResetSession", PrivateStatic).Invoke(null, null);
        PresentationPrewarmTracePlayback.RegisterBackend(originalBackend);
        EditorApplication.playModeStateChanged += originalPlayCallback;
        EditorApplication.update += originalUpdateCallback;
        installed = false;
    }

    private sealed class EmptyTrace : IPresentationPrewarmTraceBackend
    {
        public void RecordSpawn(GameObject prefab, bool coldSpawn) { }
    }
}
