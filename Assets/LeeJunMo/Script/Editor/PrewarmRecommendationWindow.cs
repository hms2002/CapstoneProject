using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed class PrewarmRecommendationWindow : EditorWindow
{
    private enum RecommendationPriority
    {
        P1,
        P2,
        P3
    }

    private sealed class AggregatedTraceEntry
    {
        public string prefabPath;
        public string prefabName;
        public int totalSpawns;
        public int coldSpawns;
        public float firstSeenSeconds = float.MaxValue;
        public float lastSeenSeconds;
        public int sessionHits;
    }

    private sealed class Recommendation
    {
        public RecommendationPriority priority;
        public int score;
        public int suggestedCount;
        public string targetScopeLabel;
        public string reason;
        public LoadManifestSO targetManifest;
        public GameObject prefab;
        public AggregatedTraceEntry trace;
    }

    private CorridorBossRouteSetSO routeSet;
    private LoadingBootstrapConfigSO bootstrapConfig;
    private readonly List<string> notes = new();
    private readonly List<Recommendation> recommendations = new();
    private readonly List<AggregatedTraceEntry> unmappedEntries = new();
    private Vector2 scrollPosition;

    [MenuItem("Tools/Loading/Prewarm Recommendations")]
    public static void ShowWindow()
    {
        GetWindow<PrewarmRecommendationWindow>("Prewarm Recommendations");
    }

    private void OnEnable()
    {
        ResolveDefaultBootstrapConfig();
    }

    private void OnGUI()
    {
        DrawToolbar();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        DrawSummary();
        DrawRecommendations();
        DrawUnmappedEntries();

        EditorGUILayout.EndScrollView();
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            routeSet = (CorridorBossRouteSetSO)EditorGUILayout.ObjectField(
                routeSet,
                typeof(CorridorBossRouteSetSO),
                false,
                GUILayout.MinWidth(220f));

            bootstrapConfig = (LoadingBootstrapConfigSO)EditorGUILayout.ObjectField(
                bootstrapConfig,
                typeof(LoadingBootstrapConfigSO),
                false,
                GUILayout.MinWidth(220f));

            if (GUILayout.Button("Analyze", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                AnalyzeRecommendations();

            if (GUILayout.Button("Use Default Bootstrap", EditorStyles.toolbarButton, GUILayout.Width(140f)))
            {
                ResolveDefaultBootstrapConfig();
                AnalyzeRecommendations();
            }

            if (GUILayout.Button("Apply P1", EditorStyles.toolbarButton, GUILayout.Width(80f)))
                ApplyRecommendations(rec => rec.priority == RecommendationPriority.P1);

            if (GUILayout.Button("Apply All", EditorStyles.toolbarButton, GUILayout.Width(80f)))
                ApplyRecommendations(_ => true);

            GUILayout.FlexibleSpace();
        }
    }

    private void DrawSummary()
    {
        EditorGUILayout.LabelField("Trace Source", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(PrewarmTraceRuntime.GetTraceFilePath(), MessageType.None);

        if (notes.Count == 0)
            return;

        EditorGUILayout.Space(4f);
        for (int i = 0; i < notes.Count; i++)
            EditorGUILayout.HelpBox(notes[i], MessageType.Info);
    }

    private void DrawRecommendations()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField($"Recommendations ({recommendations.Count})", EditorStyles.boldLabel);

        if (recommendations.Count == 0)
        {
            EditorGUILayout.HelpBox("No recommendations yet. Run Analyze after a traced play session.", MessageType.None);
            return;
        }

        RecommendationPriority[] orderedPriorities =
        {
            RecommendationPriority.P1,
            RecommendationPriority.P2,
            RecommendationPriority.P3
        };

        for (int i = 0; i < orderedPriorities.Length; i++)
        {
            RecommendationPriority priority = orderedPriorities[i];
            List<Recommendation> bucket = recommendations.Where(rec => rec.priority == priority).ToList();
            if (bucket.Count == 0)
                continue;

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField($"{priority} ({bucket.Count})", EditorStyles.boldLabel);

            for (int recIndex = 0; recIndex < bucket.Count; recIndex++)
                DrawRecommendationRow(bucket[recIndex]);
        }
    }

    private void DrawRecommendationRow(Recommendation recommendation)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(recommendation.prefab != null ? recommendation.prefab.name : recommendation.trace.prefabName, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(recommendation.trace.prefabPath, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField(
                $"Target: {recommendation.targetScopeLabel} | Suggested Count: {recommendation.suggestedCount} | Score: {recommendation.score}",
                EditorStyles.miniLabel);
            EditorGUILayout.LabelField(recommendation.reason, EditorStyles.wordWrappedMiniLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (recommendation.prefab != null && GUILayout.Button("Ping Prefab", GUILayout.Width(90f)))
                    EditorGUIUtility.PingObject(recommendation.prefab);

                if (recommendation.targetManifest != null && GUILayout.Button("Ping Manifest", GUILayout.Width(95f)))
                    EditorGUIUtility.PingObject(recommendation.targetManifest);

                if (GUILayout.Button("Apply", GUILayout.Width(70f)))
                    ApplyRecommendation(recommendation);
            }
        }
    }

    private void DrawUnmappedEntries()
    {
        if (unmappedEntries.Count == 0)
            return;

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField($"Unmapped Trace Entries ({unmappedEntries.Count})", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("These prefabs were traced but could not be mapped to Boot/RunCommon/Shared/Corridor/Boss manifests.", MessageType.Warning);

        for (int i = 0; i < unmappedEntries.Count; i++)
        {
            AggregatedTraceEntry entry = unmappedEntries[i];
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(entry.prefabName, EditorStyles.boldLabel);
                EditorGUILayout.LabelField(entry.prefabPath, EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.LabelField(
                    $"cold={entry.coldSpawns}, total={entry.totalSpawns}, first={entry.firstSeenSeconds:0.00}s, sessions={entry.sessionHits}",
                    EditorStyles.miniLabel);
            }
        }
    }

    private void AnalyzeRecommendations()
    {
        notes.Clear();
        recommendations.Clear();
        unmappedEntries.Clear();

        if (routeSet == null)
        {
            notes.Add("RouteSet is not selected.");
            return;
        }

        if (routeSet.LoadManifest == null)
        {
            notes.Add("Selected RouteSet has no RouteSetLoadManifest.");
            return;
        }

        PrewarmTraceRuntime.PrewarmTraceHistoryData history = LoadTraceHistory();
        if (history == null || history.sessions == null || history.sessions.Count == 0)
        {
            notes.Add("No trace history was found.");
            return;
        }

        Dictionary<string, AggregatedTraceEntry> aggregatedEntries = BuildAggregatedEntries(history);
        if (aggregatedEntries.Count == 0)
        {
            notes.Add("Trace file exists, but it does not contain any prefab spawn data.");
            return;
        }

        ManifestLookup lookup = BuildManifestLookup(routeSet, bootstrapConfig);

        foreach (AggregatedTraceEntry entry in aggregatedEntries.Values.OrderByDescending(item => item.coldSpawns).ThenByDescending(item => item.totalSpawns))
        {
            Recommendation recommendation = BuildRecommendation(entry, lookup);
            if (recommendation == null)
            {
                unmappedEntries.Add(entry);
                continue;
            }

            recommendations.Add(recommendation);
        }

        recommendations.Sort((left, right) =>
        {
            int priorityCompare = left.priority.CompareTo(right.priority);
            if (priorityCompare != 0)
                return priorityCompare;

            return right.score.CompareTo(left.score);
        });

        notes.Add($"Loaded {history.sessions.Count} trace session(s).");
        notes.Add($"Mapped {recommendations.Count} prefab recommendation(s).");
        if (unmappedEntries.Count > 0)
            notes.Add($"Unmapped {unmappedEntries.Count} traced prefab(s).");
    }

    private static PrewarmTraceRuntime.PrewarmTraceHistoryData LoadTraceHistory()
    {
        string tracePath = PrewarmTraceRuntime.GetTraceFilePath();
        if (!File.Exists(tracePath))
            return null;

        string json = File.ReadAllText(tracePath);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        return JsonUtility.FromJson<PrewarmTraceRuntime.PrewarmTraceHistoryData>(json);
    }

    private static Dictionary<string, AggregatedTraceEntry> BuildAggregatedEntries(PrewarmTraceRuntime.PrewarmTraceHistoryData history)
    {
        var aggregated = new Dictionary<string, AggregatedTraceEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (PrewarmTraceRuntime.PrewarmTraceSessionData session in history.sessions)
        {
            if (session?.entries == null)
                continue;

            for (int i = 0; i < session.entries.Count; i++)
            {
                PrewarmTraceRuntime.PrewarmTraceEntryData entry = session.entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.prefabPath))
                    continue;

                if (!aggregated.TryGetValue(entry.prefabPath, out AggregatedTraceEntry aggregate))
                {
                    aggregate = new AggregatedTraceEntry
                    {
                        prefabPath = entry.prefabPath,
                        prefabName = entry.prefabName,
                        totalSpawns = 0,
                        coldSpawns = 0,
                        firstSeenSeconds = entry.firstSeenSeconds,
                        lastSeenSeconds = entry.lastSeenSeconds,
                        sessionHits = 0
                    };
                    aggregated.Add(entry.prefabPath, aggregate);
                }

                aggregate.totalSpawns += entry.totalSpawns;
                aggregate.coldSpawns += entry.coldSpawns;
                aggregate.firstSeenSeconds = Mathf.Min(aggregate.firstSeenSeconds, entry.firstSeenSeconds);
                aggregate.lastSeenSeconds = Mathf.Max(aggregate.lastSeenSeconds, entry.lastSeenSeconds);
                aggregate.sessionHits++;
            }
        }

        return aggregated;
    }

    private Recommendation BuildRecommendation(AggregatedTraceEntry entry, ManifestLookup lookup)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(entry.prefabPath);
        if (prefab == null)
            return null;

        if (!lookup.TryResolveTarget(entry.prefabPath, out LoadManifestSO targetManifest, out string targetScopeLabel))
            return null;

        int suggestedCount = SuggestWarmCount(entry);
        RecommendationPriority priority = ResolvePriority(entry);
        int score = BuildScore(entry);
        string reason =
            $"cold={entry.coldSpawns}, total={entry.totalSpawns}, first={entry.firstSeenSeconds:0.00}s, sessions={entry.sessionHits}";

        return new Recommendation
        {
            priority = priority,
            score = score,
            suggestedCount = suggestedCount,
            targetScopeLabel = targetScopeLabel,
            reason = reason,
            targetManifest = targetManifest,
            prefab = prefab,
            trace = entry
        };
    }

    private static int BuildScore(AggregatedTraceEntry entry)
    {
        int earlyBonus = entry.firstSeenSeconds switch
        {
            <= 3f => 50,
            <= 10f => 20,
            <= 30f => 10,
            _ => 0
        };

        return (entry.coldSpawns * 100) + (entry.totalSpawns * 10) + earlyBonus;
    }

    private static RecommendationPriority ResolvePriority(AggregatedTraceEntry entry)
    {
        if (entry.coldSpawns > 0 && entry.firstSeenSeconds <= 5f)
            return RecommendationPriority.P1;

        if (entry.coldSpawns > 0 || entry.totalSpawns >= 5)
            return RecommendationPriority.P2;

        return RecommendationPriority.P3;
    }

    private static int SuggestWarmCount(AggregatedTraceEntry entry)
    {
        if (entry.coldSpawns >= 4 || entry.totalSpawns >= 16)
            return 3;

        if (entry.coldSpawns >= 2 || entry.totalSpawns >= 8)
            return 2;

        return 1;
    }

    private void ApplyRecommendations(Func<Recommendation, bool> predicate)
    {
        int appliedCount = 0;
        for (int i = 0; i < recommendations.Count; i++)
        {
            Recommendation recommendation = recommendations[i];
            if (!predicate(recommendation))
                continue;

            ApplyRecommendation(recommendation);
            appliedCount++;
        }

        if (appliedCount > 0)
            AssetDatabase.SaveAssets();
    }

    private void ApplyRecommendation(Recommendation recommendation)
    {
        if (recommendation == null || recommendation.targetManifest == null || recommendation.prefab == null)
            return;

        int currentCount = GetPrewarmCount(recommendation.targetManifest, recommendation.prefab);
        int desiredCount = Mathf.Max(currentCount, recommendation.suggestedCount);
        SetPrewarmCount(recommendation.targetManifest, recommendation.prefab, desiredCount);
    }

    private static int GetPrewarmCount(LoadManifestSO manifest, GameObject prefab)
    {
        if (manifest == null || prefab == null)
            return 0;

        foreach (PrewarmPrefabEntry entry in manifest.EnumeratePrewarmEntries())
        {
            if (entry.prefab == prefab)
                return entry.EffectiveCount;
        }

        return 0;
    }

    private static void SetPrewarmCount(LoadManifestSO manifest, GameObject prefab, int count)
    {
        if (manifest == null || prefab == null)
            return;

        SerializedObject serializedManifest = new SerializedObject(manifest);
        SerializedProperty prewarmArray = serializedManifest.FindProperty("prewarmPrefabs");
        int existingIndex = -1;

        for (int i = 0; i < prewarmArray.arraySize; i++)
        {
            SerializedProperty element = prewarmArray.GetArrayElementAtIndex(i);
            if (element.FindPropertyRelative("prefab").objectReferenceValue == prefab)
            {
                existingIndex = i;
                break;
            }
        }

        if (existingIndex >= 0)
        {
            SerializedProperty existing = prewarmArray.GetArrayElementAtIndex(existingIndex);
            existing.FindPropertyRelative("count").intValue = Mathf.Max(1, count);
        }
        else
        {
            int index = prewarmArray.arraySize;
            prewarmArray.InsertArrayElementAtIndex(index);
            SerializedProperty added = prewarmArray.GetArrayElementAtIndex(index);
            added.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            added.FindPropertyRelative("count").intValue = Mathf.Max(1, count);
        }

        serializedManifest.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(manifest);
    }

    private void ResolveDefaultBootstrapConfig()
    {
        string[] guids = AssetDatabase.FindAssets("t:LoadingBootstrapConfigSO");
        if (guids == null || guids.Length == 0)
            return;

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        bootstrapConfig = AssetDatabase.LoadAssetAtPath<LoadingBootstrapConfigSO>(path);
    }

    private sealed class ManifestLookup
    {
        private readonly Dictionary<string, LoadManifestSO> manifestByPrefabPath = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> scopeByPrefabPath = new(StringComparer.OrdinalIgnoreCase);

        public void AddManifest(string scopeLabel, LoadManifestSO manifest)
        {
            if (manifest == null)
                return;

            foreach (UnityEngine.Object asset in manifest.EnumerateReferencedAssets())
            {
                if (asset is not GameObject prefab)
                    continue;

                string prefabPath = AssetDatabase.GetAssetPath(prefab);
                if (string.IsNullOrWhiteSpace(prefabPath) || manifestByPrefabPath.ContainsKey(prefabPath))
                    continue;

                manifestByPrefabPath.Add(prefabPath, manifest);
                scopeByPrefabPath.Add(prefabPath, scopeLabel);
            }
        }

        public bool TryResolveTarget(string prefabPath, out LoadManifestSO manifest, out string scopeLabel)
        {
            if (manifestByPrefabPath.TryGetValue(prefabPath, out manifest) &&
                scopeByPrefabPath.TryGetValue(prefabPath, out scopeLabel))
                return true;

            manifest = null;
            scopeLabel = null;
            return false;
        }
    }

    private static ManifestLookup BuildManifestLookup(CorridorBossRouteSetSO routeSet, LoadingBootstrapConfigSO bootstrapConfig)
    {
        var lookup = new ManifestLookup();

        if (bootstrapConfig != null)
            lookup.AddManifest("Boot", bootstrapConfig.BootManifest);

        List<LoadManifestSO> runCommonManifests = FindRunCommonManifests(routeSet);
        if (runCommonManifests.Count == 1)
            lookup.AddManifest("RunCommon", runCommonManifests[0]);

        if (routeSet?.LoadManifest != null)
        {
            lookup.AddManifest("Shared", routeSet.LoadManifest.SharedManifest);
            lookup.AddManifest("Corridor", routeSet.LoadManifest.CorridorManifest);
            lookup.AddManifest("Boss", routeSet.LoadManifest.BossManifest);
        }

        return lookup;
    }
}
