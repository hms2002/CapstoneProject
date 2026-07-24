#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using CapstoneRuntime;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-845)]
[DisallowMultipleComponent]
public sealed class PrewarmTraceRuntime : MonoBehaviour
{
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

    [Serializable]
    public sealed class PrewarmTraceSessionData
    {
        public string sessionId;
        public string startedUtc;
        public string endedUtc;
        public List<PrewarmTraceEntryData> entries = new();
    }

    [Serializable]
    public sealed class PrewarmTraceHistoryData
    {
        public List<PrewarmTraceSessionData> sessions = new();
    }

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

    public static PrewarmTraceRuntime Instance { get; private set; }

    private static bool s_isQuitting;

    private readonly Dictionary<string, MutableTraceEntry> currentEntries = new(StringComparer.OrdinalIgnoreCase);
    private PrewarmTraceHistoryData history;
    private PrewarmTraceSessionData currentSession;
    private float sessionStartRealtime;
    private float lastFlushRealtime;
    private bool isDirty;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoBootstrap()
    {
        if (s_isQuitting || Instance != null)
            return;

        EnsureInstance();
    }

    public static PrewarmTraceRuntime EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        PrewarmTraceRuntime existing = RuntimeServiceOwnership.FindExistingService<PrewarmTraceRuntime>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject host = RuntimeServiceOwnership.CreateServiceHost(nameof(PrewarmTraceRuntime));
        return host.AddComponent<PrewarmTraceRuntime>();
    }

    public static void RecordSpawn(GameObject prefab, bool coldSpawn)
    {
        if (prefab == null)
            return;

        PrewarmTraceRuntime service = EnsureInstance();
        service?.RecordSpawnInternal(prefab, coldSpawn);
    }

    public static string GetTraceFilePath()
    {
#if UNITY_EDITOR
        return Path.Combine(GetTraceDirectoryPath(), BuildTraceFileName());
#else
        return Path.Combine(Application.persistentDataPath, "PrewarmTrace.json");
#endif
    }

    public static string GetTraceDirectoryPath()
    {
#if UNITY_EDITOR
        return Path.Combine(Application.dataPath, "_Project", "Data", "SceneFlow", "LoadingManifests");
#else
        return Application.persistentDataPath;
#endif
    }

    public static string GetLegacyTraceFilePath()
    {
#if UNITY_EDITOR
        return Path.Combine(Application.dataPath, "LeeJunMo", "Datas", "Loading", "PrewarmTrace.json");
#else
        return Path.Combine(Application.persistentDataPath, "PrewarmTrace.json");
#endif
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

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        RuntimeServiceOwnership.Adopt(this);
        InitializeSession();
    }

    private void Update()
    {
        if (!isDirty)
            return;

        float now = Time.realtimeSinceStartup;
        if (now - lastFlushRealtime < FlushIntervalSeconds)
            return;

        Flush();
    }

    private void OnDestroy()
    {
        Flush();

        if (Instance == this)
            Instance = null;
    }

    private void OnApplicationQuit()
    {
        s_isQuitting = true;
        Flush();
    }

    private void InitializeSession()
    {
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

        sessionStartRealtime = Time.realtimeSinceStartup;
        lastFlushRealtime = sessionStartRealtime;
        isDirty = true;
    }

    private void RecordSpawnInternal(GameObject prefab, bool coldSpawn)
    {
        string prefabPath = ResolvePrefabPath(prefab);
        if (string.IsNullOrEmpty(prefabPath))
            return;

        if (!currentEntries.TryGetValue(prefabPath, out MutableTraceEntry entry))
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
            currentEntries.Add(prefabPath, entry);
        }

        entry.totalSpawns++;
        if (coldSpawn)
            entry.coldSpawns++;

        entry.lastSeenSeconds = Mathf.Max(0f, Time.realtimeSinceStartup - sessionStartRealtime);
        entry.lastSceneName = SceneManager.GetActiveScene().name;
        isDirty = true;
    }

    private void Flush()
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

    private List<PrewarmTraceEntryData> BuildSortedEntries()
    {
        var results = new List<PrewarmTraceEntryData>(currentEntries.Count);
        foreach (MutableTraceEntry entry in currentEntries.Values)
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
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.GetAssetPath(prefab);
#else
        return prefab.name;
#endif
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

#endif
