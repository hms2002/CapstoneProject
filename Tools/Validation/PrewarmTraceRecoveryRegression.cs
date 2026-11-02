// Run only in an isolated Unity project with the production recorder and trace bridge.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class PrewarmTraceRecoveryRegression
{
    private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
    private static readonly List<string> Warnings = new();
    private static readonly List<string> Errors = new();
    private static int checks;

    public static void Run()
    {
        if (!Application.isBatchMode || !File.Exists("PrewarmTraceValidationProject.marker"))
            throw new InvalidOperationException("Use the isolated trace validation project.");

        Application.logMessageReceived += CaptureLog;
        try
        {
            Call("ResetSession");
            Call("EnsureSession");
            SetEntry(1);
            Call("Flush");
            string path = PrewarmTraceRuntime.GetTraceFilePath();
            string temporary = Path.Combine("Library", "PrewarmTrace", Path.GetFileName(path) + ".tmp");
            Check(File.Exists(path), "First save did not create the trace");
            Check(Read().sessions.Count == 1 && LastTotal() == 1, "First save lost measurements");
            Check(!Get<bool>("isDirty") && !Get<bool>("flushFailed"), "First save did not clear pending state");
            Check(!File.Exists(temporary), "First save left a staging file");

            string assetPath = path.Substring(Directory.GetCurrentDirectory().Length + 1).Replace('\\', '/');
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            byte[] metadata = File.ReadAllBytes(path + ".meta");
            string previousSession = JsonUtility.ToJson(Read().sessions[0]);
            byte[] previousJson = File.ReadAllBytes(path);

            Call("ResetSession");
            Call("EnsureSession");
            SetEntry(5);
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                       FileShare.ReadWrite | FileShare.Delete))
            using (var map = MemoryMappedFile.CreateFromFile(stream, null, 0,
                       MemoryMappedFileAccess.Read, HandleInheritability.None, true))
            using (var view = map.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read))
            {
                bool reproduced = false;
                try { File.WriteAllText(path, "invalid legacy overwrite"); }
                catch (IOException error) { reproduced = error.Message.Contains("1224"); }
                Check(reproduced, "Fixture did not reproduce the original Windows 1224");
                Call("Flush");
                Check(!Get<bool>("isDirty") && !Get<bool>("flushFailed"), "Mapped-file replacement did not succeed");
                Check(LastTotal() == 5 && Read().sessions.Count == 2, "Mapped-file save lost the new session");
                byte[] mappedBytes = new byte[previousJson.Length];
                view.ReadArray(0, mappedBytes, 0, mappedBytes.Length);
                Check(mappedBytes.SequenceEqual(previousJson), "Replacement invalidated the existing mapped view");
            }
            Check(JsonUtility.ToJson(Read().sessions[0]) == previousSession, "Replacement changed an older session");
            Check(File.ReadAllBytes(path + ".meta").SequenceEqual(metadata), "Replacement changed asset metadata");
            Check(Warnings.Count == 0, "A successful replacement logged a failure");

            byte[] beforeLock = File.ReadAllBytes(path);
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                SetEntry(9);
                Call("Flush");
                Check(Get<bool>("isDirty") && Get<bool>("flushFailed"), "Locked save dropped pending state");
                Check(File.ReadAllBytes(path).SequenceEqual(beforeLock), "Locked save damaged the existing JSON");
                Check(Warnings.Count == 1 && Warnings[0].Contains(path), "Failure did not report one warning with its path");
                Check(File.Exists(temporary), "Failed replacement did not retain its staging file");
                Check(JsonUtility.FromJson<PrewarmTraceRuntime.PrewarmTraceHistoryData>(File.ReadAllText(temporary))
                    .sessions.Last().entries.Single().totalSpawns == 9, "Staging file was not a complete trace");

                float heldTimestamp = Time.realtimeSinceStartup - 2f;
                Set("lastFlushRealtime", heldTimestamp);
                DateTime stagedAt = File.GetLastWriteTimeUtc(temporary);
                for (int i = 0; i < 1000; i++) Call("FlushIfNeeded");
                Check(Get<float>("lastFlushRealtime") == heldTimestamp, "Failed save retried before its five-second interval");
                Check(File.GetLastWriteTimeUtc(temporary) == stagedAt, "Suppressed updates still performed disk writes");
                Check(Warnings.Count == 1, "Repeated updates flooded the Console");

                SetEntry(10);
                Set("lastFlushRealtime", Time.realtimeSinceStartup - 6f);
                Call("FlushIfNeeded");
                Check(Get<float>("lastFlushRealtime") > heldTimestamp, "Retry did not update the attempt timestamp");
                Check(Get<bool>("isDirty") && Warnings.Count == 1, "Failed retry lost data or repeated its warning");
                Check(File.ReadAllBytes(path).SequenceEqual(beforeLock), "Failed retry changed the existing trace");
                SetEntry(12);
                Call("HandlePlayModeStateChanged", PlayModeStateChange.ExitingPlayMode);
                Check(Get<bool>("isDirty") && Warnings.Count == 1, "Play exit failure escaped normal failure handling");
            }
            Retry();
            Check(LastTotal() == 12, "Recovery did not save the latest accumulated measurements");
            Check(!Get<bool>("isDirty") && !Get<bool>("flushFailed"), "Recovery did not restore normal state");
            Check(!File.Exists(temporary), "Recovery left a staging file");
            Check(JsonUtility.ToJson(Read().sessions[0]) == previousSession, "Recovery changed older measurements");

            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                SetEntry(13);
                Call("Flush");
                Check(Warnings.Count == 2, "A new failure episode was not reported after recovery");
            }
            Retry();
            Check(LastTotal() == 13 && !Get<bool>("flushFailed"), "Second episode did not recover");

            byte[] beforeReadOnly = File.ReadAllBytes(path);
            File.WriteAllText(temporary, "read-only staging fixture");
            File.SetAttributes(temporary, FileAttributes.ReadOnly);
            try
            {
                SetEntry(14);
                Call("Flush");
                Check(Get<bool>("isDirty") && Get<bool>("flushFailed"), "Access failure was not contained");
                Check(File.ReadAllBytes(path).SequenceEqual(beforeReadOnly), "Access failure changed existing JSON");
                Check(Warnings.Count == 3, "Access failure was not reported once");
            }
            finally
            {
                File.SetAttributes(temporary, FileAttributes.Normal);
                File.Delete(temporary);
            }
            Retry();
            Check(LastTotal() == 14 && !Get<bool>("flushFailed"), "Access failure did not recover");

            var seed = new PrewarmTraceRuntime.PrewarmTraceHistoryData();
            for (int i = 0; i < 20; i++)
                seed.sessions.Add(new PrewarmTraceRuntime.PrewarmTraceSessionData { sessionId = "old-" + i });
            Call("WriteTraceFile", path, JsonUtility.ToJson(seed, true));
            Call("ResetSession");
            Call("EnsureSession");
            Call("Flush");
            Check(Read().sessions.Count == 20, "The existing twenty-session limit changed");
            Check(Read().sessions[0].sessionId == "old-1" && Read().sessions[18].sessionId == "old-19",
                "Session retention removed the wrong history");
            Check(PrewarmTraceRuntime.GetTraceFilePathsForRead().Any(file => Path.GetFullPath(file) == Path.GetFullPath(path)),
                "Recommendation reader lost the tester file");
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            Check(AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath) != null, "Saved trace did not import as a TextAsset");
            Check(File.ReadAllBytes(path + ".meta").SequenceEqual(metadata), "Trace metadata changed after importing");
            Call("ResetSession");
            Check(Get<object>("currentSession") == null && !Get<bool>("isDirty") && !Get<bool>("flushFailed"),
                "Session reset retained failure state");
            Check(Errors.Count == 0, "Unexpected exception or error reached the Console");

            string result = "PREWARM_TRACE_RECOVERY_PASS: " + checks +
                " checks; real Windows 1224, mapped replacement, locked/access-denied failure, " +
                "1000 throttled updates, bounded warnings, pending/latest measurements, retry, " +
                "Play exit, previous JSON/meta, history limit, recommendation reader and import.";
            File.WriteAllText("../native-validation.txt", result);
            Debug.Log(result);
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            File.WriteAllText("../native-validation.txt", "PREWARM_TRACE_RECOVERY_FAIL: " + error);
            EditorApplication.Exit(1);
        }
        finally
        {
            Application.logMessageReceived -= CaptureLog;
        }
    }

    private static void Retry()
    {
        Set("lastFlushRealtime", Time.realtimeSinceStartup - 6f);
        Call("FlushIfNeeded");
    }

    private static void SetEntry(int total)
    {
        Type entryType = typeof(PrewarmTraceRuntime).GetNestedType("MutableTraceEntry", BindingFlags.NonPublic);
        object entry = Activator.CreateInstance(entryType, true);
        entryType.GetField("prefabPath").SetValue(entry, "Assets/Fixture.prefab");
        entryType.GetField("prefabName").SetValue(entry, "Fixture");
        entryType.GetField("totalSpawns").SetValue(entry, total);
        entryType.GetField("coldSpawns").SetValue(entry, 1);
        ((IDictionary)Get<object>("CurrentEntries"))["Assets/Fixture.prefab"] = entry;
        Set("isDirty", true);
    }

    private static PrewarmTraceRuntime.PrewarmTraceHistoryData Read() =>
        JsonUtility.FromJson<PrewarmTraceRuntime.PrewarmTraceHistoryData>(File.ReadAllText(PrewarmTraceRuntime.GetTraceFilePath()));

    private static int LastTotal() => Read().sessions.Last().entries.Single().totalSpawns;
    private static T Get<T>(string field) => (T)typeof(PrewarmTraceRuntime).GetField(field, PrivateStatic).GetValue(null);
    private static void Set(string field, object value) => typeof(PrewarmTraceRuntime).GetField(field, PrivateStatic).SetValue(null, value);
    private static void Call(string method, params object[] arguments) =>
        typeof(PrewarmTraceRuntime).GetMethod(method, PrivateStatic).Invoke(null, arguments);

    private static void CaptureLog(string message, string stack, LogType type)
    {
        if (type == LogType.Warning && message.StartsWith("[PrewarmTraceRuntime]")) Warnings.Add(message);
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Errors.Add(message);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
    }
}
