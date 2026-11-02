using System;
using System.Collections.Generic;
using System.IO;
using CapstonePresentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 책임: 에디터 플레이 중 presentation prefab prewarm 누락 이력을 기록하고 분석 데이터를 만든다.
/// </summary>
[InitializeOnLoad]
public static class PrewarmTraceRuntime
{
    /// <summary>
    /// 책임: prefab 하나의 prewarm 기록 통계를 JSON으로 저장하기 위한 데이터이다.
    /// </summary>
    [Serializable]
    public sealed class PrewarmTraceEntryData
    {
        public string prefabPath;
        public string prefabName;
        public int totalSpawns;
        public int coldSpawns;
        public float firstSeenSeconds;
        public float lastSeenSeconds;
        public string firstSceneName;
        public string lastSceneName;
    }

    /// <summary>
    /// 책임: 한 번의 플레이 세션에서 수집한 prewarm 기록 묶음을 저장한다.
    /// </summary>
    [Serializable]
    public sealed class PrewarmTraceSessionData
    {
        public string sessionId;
        public string startedUtc;
        public string endedUtc;
        public List<PrewarmTraceEntryData> entries = new();
    }

    /// <summary>
    /// 책임: 여러 플레이 세션의 prewarm 기록을 누적 저장한다.
    /// </summary>
    [Serializable]
    public sealed class PrewarmTraceHistoryData
    {
        public List<PrewarmTraceSessionData> sessions = new();
    }

    /// <summary>
    /// 책임: Core playback 요청을 에디터 prewarm trace 기록기로 연결한다.
    /// </summary>
    private sealed class PrewarmTraceBackend : IPresentationPrewarmTraceBackend
    {
        public void RecordSpawn(GameObject prefab, bool coldSpawn)
        {
            PrewarmTraceRuntime.RecordSpawn(prefab, coldSpawn);
        }
    }

    /// <summary>
    /// 책임: 현재 플레이 세션에서 prefab 하나의 누적 spawn 통계를 갱신 가능한 형태로 보관한다.
    /// </summary>
    private sealed class MutableTraceEntry
    {
        public string prefabPath;
        public string prefabName;
        public int totalSpawns;
        public int coldSpawns;
        public float firstSeenSeconds;
        public float lastSeenSeconds;
        public string firstSceneName;
        public string lastSceneName;

        public PrewarmTraceEntryData ToData()
        {
            return new PrewarmTraceEntryData
            {
                prefabPath = prefabPath,
                prefabName = prefabName,
                totalSpawns = totalSpawns,
                coldSpawns = coldSpawns,
                firstSeenSeconds = firstSeenSeconds,
                lastSeenSeconds = lastSeenSeconds,
                firstSceneName = firstSceneName,
                lastSceneName = lastSceneName
            };
        }
    }

    public const string EditorTraceRelativePath = "Assets/_Project/Data/SceneFlow/LoadingManifests/PrewarmTrace.json";
    public const string LegacyEditorTraceRelativePath = "Assets/LeeJunMo/Datas/Loading/PrewarmTrace.json";
    public const string EditorTraceDirectoryRelativePath = "Assets/_Project/Data/SceneFlow/LoadingManifests";
    private const string TraceFilePrefix = "PrewarmTrace_";
    private const string TraceFileExtension = ".json";
    private const float FlushIntervalSeconds = 1f;
    private const int MaxSessionCount = 20;

    private static readonly PrewarmTraceBackend Backend = new();
    private static readonly Dictionary<string, MutableTraceEntry> CurrentEntries = new(StringComparer.OrdinalIgnoreCase);

    private static PrewarmTraceHistoryData history;
    private static PrewarmTraceSessionData currentSession;
    private static float sessionStartRealtime;
    private static float lastFlushRealtime;
    private static bool isDirty;

    static PrewarmTraceRuntime()
    {
        PresentationPrewarmTracePlayback.RegisterBackend(Backend);
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        EditorApplication.update += FlushIfNeeded;
    }

    public static void RecordSpawn(GameObject prefab, bool coldSpawn)
    {
        if (prefab == null || !EditorApplication.isPlaying)
            return;

        EnsureSession();
        if (currentSession == null)
            return;

        string prefabPath = ResolvePrefabPath(prefab);
        if (string.IsNullOrEmpty(prefabPath))
            return;

        if (!CurrentEntries.TryGetValue(prefabPath, out MutableTraceEntry entry))
        {
            string sceneName = SceneManager.GetActiveScene().name;
            float nowSeconds = Mathf.Max(0f, Time.realtimeSinceStartup - sessionStartRealtime);
            entry = new MutableTraceEntry
            {
                prefabPath = prefabPath,
                prefabName = prefab.name,
                totalSpawns = 0,
                coldSpawns = 0,
                firstSeenSeconds = nowSeconds,
                lastSeenSeconds = nowSeconds,
                firstSceneName = sceneName,
                lastSceneName = sceneName
            };
            CurrentEntries.Add(prefabPath, entry);
        }

        entry.totalSpawns++;
        if (coldSpawn)
            entry.coldSpawns++;

        entry.lastSeenSeconds = Mathf.Max(0f, Time.realtimeSinceStartup - sessionStartRealtime);
        entry.lastSceneName = SceneManager.GetActiveScene().name;
        isDirty = true;
    }

    public static string GetTraceFilePath()
    {
        return Path.Combine(GetTraceDirectoryPath(), BuildTraceFileName());
    }

    public static string GetTraceDirectoryPath()
    {
        return Path.Combine(Application.dataPath, "_Project", "Data", "SceneFlow", "LoadingManifests");
    }

    public static string GetLegacyTraceFilePath()
    {
        return Path.Combine(Application.dataPath, "LeeJunMo", "Datas", "Loading", "PrewarmTrace.json");
    }

    public static List<string> GetTraceFilePathsForRead()
    {
        var paths = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddPathIfExists(GetTraceFilePath(), paths, seen);

        string directory = GetTraceDirectoryPath();
        if (Directory.Exists(directory))
        {
            string[] traceFiles = Directory.GetFiles(directory, $"{TraceFilePrefix}*{TraceFileExtension}");
            Array.Sort(traceFiles, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < traceFiles.Length; i++)
                AddPathIfExists(traceFiles[i], paths, seen);
        }

        AddPathIfExists(GetLegacyTraceFilePath(), paths, seen);
        return paths;
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            ResetSession();
            EnsureSession();
            return;
        }

        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            Flush();
            return;
        }

        if (state == PlayModeStateChange.EnteredEditMode)
            ResetSession();
    }

    private static void FlushIfNeeded()
    {
        if (!isDirty || currentSession == null)
            return;

        float now = Time.realtimeSinceStartup;
        if (now - lastFlushRealtime < FlushIntervalSeconds)
            return;

        Flush();
    }

    private static void EnsureSession()
    {
        if (currentSession != null)
            return;

        history = LoadHistory();
        currentSession = new PrewarmTraceSessionData
        {
            sessionId = Guid.NewGuid().ToString("N"),
            startedUtc = DateTime.UtcNow.ToString("O"),
            endedUtc = string.Empty,
            entries = new List<PrewarmTraceEntryData>()
        };

        history.sessions ??= new List<PrewarmTraceSessionData>();
        history.sessions.Add(currentSession);
        if (history.sessions.Count > MaxSessionCount)
            history.sessions.RemoveRange(0, history.sessions.Count - MaxSessionCount);

        CurrentEntries.Clear();
        sessionStartRealtime = Time.realtimeSinceStartup;
        lastFlushRealtime = sessionStartRealtime;
        isDirty = true;
    }

    private static void ResetSession()
    {
        history = null;
        currentSession = null;
        CurrentEntries.Clear();
        sessionStartRealtime = 0f;
        lastFlushRealtime = 0f;
        isDirty = false;
    }

    private static void Flush()
    {
        if (!isDirty || history == null || currentSession == null)
            return;

        currentSession.endedUtc = DateTime.UtcNow.ToString("O");
        currentSession.entries = BuildSortedEntries();

        string path = GetTraceFilePath();
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        string json = JsonUtility.ToJson(history, true);
        File.WriteAllText(path, json);

        isDirty = false;
        lastFlushRealtime = Time.realtimeSinceStartup;
    }

    private static List<PrewarmTraceEntryData> BuildSortedEntries()
    {
        var results = new List<PrewarmTraceEntryData>(CurrentEntries.Count);
        foreach (MutableTraceEntry entry in CurrentEntries.Values)
            results.Add(entry.ToData());

        results.Sort((left, right) =>
        {
            int coldCompare = right.coldSpawns.CompareTo(left.coldSpawns);
            if (coldCompare != 0)
                return coldCompare;

            return right.totalSpawns.CompareTo(left.totalSpawns);
        });
        return results;
    }

    private static PrewarmTraceHistoryData LoadHistory()
    {
        string path = GetTraceFilePath();
        if (!File.Exists(path))
            return new PrewarmTraceHistoryData();

        try
        {
            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
                return new PrewarmTraceHistoryData();

            PrewarmTraceHistoryData data = JsonUtility.FromJson<PrewarmTraceHistoryData>(json);
            return data ?? new PrewarmTraceHistoryData();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[PrewarmTraceRuntime] Failed to load trace history: {ex.Message}");
            return new PrewarmTraceHistoryData();
        }
    }

    private static string ResolvePrefabPath(GameObject prefab)
    {
        string path = AssetDatabase.GetAssetPath(prefab);
        return string.IsNullOrEmpty(path) ? prefab.name : path;
    }

    private static string BuildTraceFileName()
    {
        string testerId = $"{Environment.UserName}_{Environment.MachineName}";
        testerId = SanitizeFileNameSegment(testerId);
        if (string.IsNullOrWhiteSpace(testerId))
            testerId = "unknown_tester";

        return $"{TraceFilePrefix}{testerId}{TraceFileExtension}";
    }

    private static string SanitizeFileNameSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        char[] invalidChars = Path.GetInvalidFileNameChars();
        var chars = value.Trim().ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(invalidChars, chars[i]) >= 0 || char.IsWhiteSpace(chars[i]))
                chars[i] = '_';
        }

        return new string(chars);
    }

    private static void AddPathIfExists(string path, List<string> paths, HashSet<string> seen)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        string fullPath = Path.GetFullPath(path);
        if (seen.Add(fullPath))
            paths.Add(fullPath);
    }
}
