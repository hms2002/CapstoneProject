using System;
using System.Collections.Generic;
using System.Linq;
using CapstonePresentation;
using UnityEditor;
using UnityEngine;

public sealed class LoadManifestInspectorWindow : EditorWindow
{
    private enum AssetCategory
    {
        Prefab,
        Audio,
        Cue,
        Data,
        Material,
        Font,
        Other
    }

    private enum Severity
    {
        Info,
        Warning,
        Error
    }

    private sealed class Issue
    {
        public Severity severity;
        public string message;
    }

    private sealed class AssetEntry
    {
        public AssetEntry(string path, UnityEngine.Object asset)
        {
            Path = path;
            Asset = asset;
            Category = ResolveCategory(asset);
        }

        public string Path { get; }
        public UnityEngine.Object Asset { get; }
        public AssetCategory Category { get; }

        private static AssetCategory ResolveCategory(UnityEngine.Object asset)
        {
            if (asset == null)
                return AssetCategory.Other;

            if (asset is GameObject)
                return AssetCategory.Prefab;

            if (asset is AudioClip)
                return AssetCategory.Audio;

            if (asset is PresentationCueSO)
                return AssetCategory.Cue;

            if (asset is Material)
                return AssetCategory.Material;

            if (asset is Font || asset.GetType().Name == "TMP_FontAsset")
                return AssetCategory.Font;

            if (asset is ScriptableObject)
                return AssetCategory.Data;

            return AssetCategory.Other;
        }
    }

    private sealed class ScopeSnapshot
    {
        public string Label;
        public LoadManifestSO EditableManifest;
        public bool SupportsPrewarmEditing;
        public List<AssetEntry> Assets = new();
    }

    private CorridorBossRouteSetSO routeSet;
    private LoadingBootstrapConfigSO bootstrapConfig;
    private readonly List<Issue> issues = new();
    private readonly List<ScopeSnapshot> scopes = new();
    private readonly Dictionary<string, bool> scopeExpandedStates = new();
    private readonly Dictionary<string, bool> categoryExpandedStates = new();
    private Vector2 leftScroll;
    private Vector2 rightScroll;
    private bool showAssets = true;

    [MenuItem("Tools/Loading/Load Manifest Inspector")]
    public static void ShowWindow()
    {
        GetWindow<LoadManifestInspectorWindow>("Manifest Inspector v2");
    }

    private void OnEnable()
    {
        ResolveDefaultBootstrapConfig();
    }

    private void OnGUI()
    {
        DrawToolbar();

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(position.width * 0.38f)))
                DrawSummary();

            using (new EditorGUILayout.VerticalScope())
                DrawScopes();
        }
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
                Analyze();

            if (GUILayout.Button("Use Default Bootstrap", EditorStyles.toolbarButton, GUILayout.Width(140f)))
            {
                ResolveDefaultBootstrapConfig();
                Analyze();
            }

            if (GUILayout.Button("Prewarm Recs", EditorStyles.toolbarButton, GUILayout.Width(95f)))
                OpenPrewarmRecommendationsWindow();

            string assetsLabel = showAssets ? "Assets: On" : "Assets: Off";
            showAssets = GUILayout.Toggle(showAssets, assetsLabel, EditorStyles.toolbarButton, GUILayout.Width(90f));

            GUILayout.Space(6f);
            GUILayout.Label("Category View v2", EditorStyles.miniLabel);

            GUILayout.FlexibleSpace();
        }
    }

    private void DrawSummary()
    {
        EditorGUILayout.LabelField("Issues", EditorStyles.boldLabel);
        leftScroll = EditorGUILayout.BeginScrollView(leftScroll);

        if (issues.Count == 0)
        {
            EditorGUILayout.HelpBox("No issues. Pick a RouteSet and click Analyze.", MessageType.Info);
            EditorGUILayout.EndScrollView();
            return;
        }

        for (int i = 0; i < issues.Count; i++)
        {
            Issue issue = issues[i];
            MessageType type = issue.severity switch
            {
                Severity.Error => MessageType.Error,
                Severity.Warning => MessageType.Warning,
                _ => MessageType.Info
            };

            EditorGUILayout.HelpBox(issue.message, type);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawScopes()
    {
        EditorGUILayout.LabelField("Scopes", EditorStyles.boldLabel);
        rightScroll = EditorGUILayout.BeginScrollView(rightScroll);

        if (scopes.Count == 0)
        {
            EditorGUILayout.HelpBox("No analyzed scopes yet.", MessageType.None);
            EditorGUILayout.EndScrollView();
            return;
        }

        for (int i = 0; i < scopes.Count; i++)
        {
            ScopeSnapshot scope = scopes[i];
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                bool expanded = GetScopeExpanded(scope.Label);
                expanded = EditorGUILayout.Foldout(expanded, $"{scope.Label} ({scope.Assets.Count})", true);
                scopeExpandedStates[scope.Label] = expanded;

                if (!expanded)
                    continue;

                if (scope.SupportsPrewarmEditing && scope.EditableManifest != null)
                {
                    int prewarmCount = CountValidPrewarmEntries(scope.EditableManifest);
                    EditorGUILayout.LabelField($"Prewarm Entries: {prewarmCount}", EditorStyles.miniLabel);
                }

                if (!showAssets)
                {
                    EditorGUILayout.HelpBox("Asset list is hidden. Toggle 'Assets: Off' in the toolbar to show it.", MessageType.None);
                    continue;
                }

                DrawScopeCategories(scope);
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawScopeCategories(ScopeSnapshot scope)
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.HelpBox("Categories", MessageType.None);

        AssetCategory[] orderedCategories =
        {
            AssetCategory.Prefab,
            AssetCategory.Audio,
            AssetCategory.Cue,
            AssetCategory.Data,
            AssetCategory.Material,
            AssetCategory.Font,
            AssetCategory.Other
        };

        bool drewAnyCategory = false;
        for (int i = 0; i < orderedCategories.Length; i++)
        {
            AssetCategory category = orderedCategories[i];
            List<AssetEntry> categoryAssets = FilterAssets(scope.Assets, category);
            if (categoryAssets.Count == 0)
                continue;

            drewAnyCategory = true;
            DrawCategorySection(scope, category, categoryAssets);
        }

        if (!drewAnyCategory)
            EditorGUILayout.HelpBox("There are no assets in this scope.", MessageType.None);
    }

    private void DrawCategorySection(ScopeSnapshot scope, AssetCategory category, List<AssetEntry> assets)
    {
        string categoryKey = $"{scope.Label}:{category}";
        bool expanded = GetCategoryExpanded(categoryKey);
        expanded = EditorGUILayout.Foldout(expanded, $"{category} ({assets.Count})", true);
        categoryExpandedStates[categoryKey] = expanded;

        if (!expanded)
            return;

        using (new EditorGUI.IndentLevelScope())
        {
            for (int i = 0; i < assets.Count; i++)
                DrawAssetEntry(scope, assets[i]);
        }
    }

    private void DrawAssetEntry(ScopeSnapshot scope, AssetEntry entry)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUILayout.VerticalScope())
            {
                EditorGUILayout.LabelField(entry.Path, EditorStyles.wordWrappedMiniLabel);

                if (scope.SupportsPrewarmEditing && scope.EditableManifest != null && entry.Asset is GameObject prefab)
                {
                    int currentWarmCount = GetPrewarmCount(scope.EditableManifest, prefab);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField($"Prewarm: {currentWarmCount}", GUILayout.Width(80f));

                        if (GUILayout.Button("+1", GUILayout.Width(34f)))
                            SetPrewarmCount(scope.EditableManifest, prefab, currentWarmCount + 1);

                        if (GUILayout.Button("+2", GUILayout.Width(34f)))
                            SetPrewarmCount(scope.EditableManifest, prefab, currentWarmCount + 2);

                        if (GUILayout.Button("+3", GUILayout.Width(34f)))
                            SetPrewarmCount(scope.EditableManifest, prefab, currentWarmCount + 3);

                        using (new EditorGUI.DisabledScope(currentWarmCount <= 0))
                        {
                            if (GUILayout.Button("Clear", GUILayout.Width(48f)))
                                SetPrewarmCount(scope.EditableManifest, prefab, 0);
                        }
                    }
                }
            }

            if (entry.Asset != null && GUILayout.Button("Ping", GUILayout.Width(46f)))
                EditorGUIUtility.PingObject(entry.Asset);
        }
    }

    private void Analyze()
    {
        issues.Clear();
        scopes.Clear();

        if (routeSet == null)
        {
            issues.Add(new Issue { severity = Severity.Error, message = "No RouteSet selected." });
            return;
        }

        ResolveDefaultBootstrapConfigIfMissing();

        LoadManifestSO bootManifest = bootstrapConfig != null ? bootstrapConfig.BootManifest : null;
        RouteSetLoadManifestSO routeManifest = routeSet.LoadManifest;
        List<LoadManifestSO> runCommonManifests = FindRunCommonManifests(routeSet);

        if (bootstrapConfig == null)
            issues.Add(new Issue { severity = Severity.Warning, message = "Bootstrap config was not found. Boot scope will be empty." });
        else if (bootManifest == null)
            issues.Add(new Issue { severity = Severity.Warning, message = "Bootstrap config exists, but Boot manifest is not assigned." });

        if (routeManifest == null)
        {
            issues.Add(new Issue { severity = Severity.Error, message = $"{routeSet.name} has no RouteSetLoadManifest assigned." });
            return;
        }

        if (routeManifest.SharedManifest == null)
            issues.Add(new Issue { severity = Severity.Warning, message = "Shared manifest is empty." });
        if (routeManifest.CorridorManifest == null)
            issues.Add(new Issue { severity = Severity.Warning, message = "Corridor manifest is empty." });
        if (routeManifest.BossManifest == null)
            issues.Add(new Issue { severity = Severity.Warning, message = "Boss manifest is empty." });

        Dictionary<string, AssetEntry> bootAssets = BuildAssetMap(bootManifest);
        Dictionary<string, AssetEntry> runCommonAssets = MergeManifests(runCommonManifests);
        Dictionary<string, AssetEntry> sharedAssets = BuildAssetMap(routeManifest.SharedManifest);
        Dictionary<string, AssetEntry> corridorAssets = BuildAssetMap(routeManifest.CorridorManifest);
        Dictionary<string, AssetEntry> bossAssets = BuildAssetMap(routeManifest.BossManifest);

        scopes.Add(new ScopeSnapshot
        {
            Label = "Boot",
            EditableManifest = bootManifest,
            SupportsPrewarmEditing = bootManifest != null,
            Assets = bootAssets.Values.OrderBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase).ToList()
        });
        scopes.Add(new ScopeSnapshot
        {
            Label = "RunCommon",
            SupportsPrewarmEditing = false,
            Assets = runCommonAssets.Values.OrderBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase).ToList()
        });
        scopes.Add(new ScopeSnapshot
        {
            Label = "Shared",
            EditableManifest = routeManifest.SharedManifest,
            SupportsPrewarmEditing = routeManifest.SharedManifest != null,
            Assets = sharedAssets.Values.OrderBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase).ToList()
        });
        scopes.Add(new ScopeSnapshot
        {
            Label = "Corridor",
            EditableManifest = routeManifest.CorridorManifest,
            SupportsPrewarmEditing = routeManifest.CorridorManifest != null,
            Assets = corridorAssets.Values.OrderBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase).ToList()
        });
        scopes.Add(new ScopeSnapshot
        {
            Label = "Boss",
            EditableManifest = routeManifest.BossManifest,
            SupportsPrewarmEditing = routeManifest.BossManifest != null,
            Assets = bossAssets.Values.OrderBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase).ToList()
        });

        AddOverlapIssue("Boot", bootAssets, "Shared", sharedAssets);
        AddOverlapIssue("Boot", bootAssets, "Corridor", corridorAssets);
        AddOverlapIssue("Boot", bootAssets, "Boss", bossAssets);
        AddOverlapIssue("RunCommon", runCommonAssets, "Shared", sharedAssets);
        AddOverlapIssue("RunCommon", runCommonAssets, "Corridor", corridorAssets);
        AddOverlapIssue("RunCommon", runCommonAssets, "Boss", bossAssets);
        AddOverlapIssue("Shared", sharedAssets, "Corridor", corridorAssets);
        AddOverlapIssue("Shared", sharedAssets, "Boss", bossAssets);
        AddOverlapIssue("Corridor", corridorAssets, "Boss", bossAssets);

        if (issues.Count == 0)
            issues.Add(new Issue { severity = Severity.Info, message = "No overlap or missing-asset issues were detected." });
    }

    private void AddOverlapIssue(
        string leftLabel,
        Dictionary<string, AssetEntry> left,
        string rightLabel,
        Dictionary<string, AssetEntry> right)
    {
        if (left.Count == 0 || right.Count == 0)
            return;

        List<string> overlap = left.Keys.Intersect(right.Keys, StringComparer.Ordinal).Take(5).ToList();
        if (overlap.Count == 0)
            return;

        string sample = string.Join(", ", overlap.Select(key => left.TryGetValue(key, out AssetEntry entry) ? entry.Path : key));
        issues.Add(new Issue
        {
            severity = Severity.Warning,
            message = $"{leftLabel} and {rightLabel} overlap. sample: {sample}"
        });
    }

    private static Dictionary<string, AssetEntry> MergeManifests(List<LoadManifestSO> manifests)
    {
        var merged = new Dictionary<string, AssetEntry>(StringComparer.Ordinal);
        for (int i = 0; i < manifests.Count; i++)
        {
            foreach (KeyValuePair<string, AssetEntry> pair in BuildAssetMap(manifests[i]))
            {
                if (!merged.ContainsKey(pair.Key))
                    merged.Add(pair.Key, pair.Value);
            }
        }

        return merged;
    }

    private static Dictionary<string, AssetEntry> BuildAssetMap(LoadManifestSO manifest)
    {
        var map = new Dictionary<string, AssetEntry>(StringComparer.Ordinal);
        if (manifest == null)
            return map;

        foreach (UnityEngine.Object asset in manifest.EnumerateReferencedAssets())
        {
            if (asset == null)
                continue;

            string key = GlobalObjectId.GetGlobalObjectIdSlow(asset).ToString();
            if (map.ContainsKey(key))
                continue;

            string assetPath = AssetDatabase.GetAssetPath(asset);
            map.Add(key, new AssetEntry(assetPath, asset));
        }

        return map;
    }

    private static int CountValidPrewarmEntries(LoadManifestSO manifest)
    {
        if (manifest == null)
            return 0;

        int count = 0;
        foreach (PrewarmPrefabEntry entry in manifest.EnumeratePrewarmEntries())
        {
            if (entry.IsValid)
                count++;
        }

        return count;
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

    private void SetPrewarmCount(LoadManifestSO manifest, GameObject prefab, int count)
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

        if (count <= 0)
        {
            if (existingIndex >= 0)
                prewarmArray.DeleteArrayElementAtIndex(existingIndex);
        }
        else if (existingIndex >= 0)
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
        AssetDatabase.SaveAssets();
        Repaint();
    }

    private static List<LoadManifestSO> FindRunCommonManifests(CorridorBossRouteSetSO targetRouteSet)
    {
        var manifests = new List<LoadManifestSO>();
        if (targetRouteSet == null)
            return manifests;

        string[] guids = AssetDatabase.FindAssets("t:RunRouteCatalogSO");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            RunRouteCatalogSO catalog = AssetDatabase.LoadAssetAtPath<RunRouteCatalogSO>(path);
            if (catalog == null || catalog.RunCommonLoadManifest == null)
                continue;

            if (ReferencesRouteSet(catalog, targetRouteSet) && !manifests.Contains(catalog.RunCommonLoadManifest))
                manifests.Add(catalog.RunCommonLoadManifest);
        }

        return manifests;
    }

    private static bool ReferencesRouteSet(RunRouteCatalogSO catalog, CorridorBossRouteSetSO targetRouteSet)
    {
        if (catalog == null || targetRouteSet == null)
            return false;

        if (catalog.FinalRouteSet == targetRouteSet)
            return true;

        IReadOnlyList<CorridorBossRouteSetSO> normalRoutes = catalog.NormalRouteSets;
        for (int i = 0; i < normalRoutes.Count; i++)
        {
            if (normalRoutes[i] == targetRouteSet)
                return true;
        }

        return false;
    }

    private void ResolveDefaultBootstrapConfigIfMissing()
    {
        if (bootstrapConfig != null)
            return;

        ResolveDefaultBootstrapConfig();
    }

    private void ResolveDefaultBootstrapConfig()
    {
        string[] guids = AssetDatabase.FindAssets("t:LoadingBootstrapConfigSO");
        if (guids == null || guids.Length == 0)
            return;

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        bootstrapConfig = AssetDatabase.LoadAssetAtPath<LoadingBootstrapConfigSO>(path);
    }

    private bool GetScopeExpanded(string label)
    {
        if (scopeExpandedStates.TryGetValue(label, out bool expanded))
            return expanded;

        scopeExpandedStates[label] = true;
        return true;
    }

    private bool GetCategoryExpanded(string key)
    {
        if (categoryExpandedStates.TryGetValue(key, out bool expanded))
            return expanded;

        categoryExpandedStates[key] = true;
        return true;
    }

    private static List<AssetEntry> FilterAssets(List<AssetEntry> assets, AssetCategory category)
    {
        var filtered = new List<AssetEntry>();
        for (int i = 0; i < assets.Count; i++)
        {
            if (assets[i].Category == category)
                filtered.Add(assets[i]);
        }

        return filtered;
    }

    private static void OpenPrewarmRecommendationsWindow()
    {
        Type windowType = Type.GetType("PrewarmRecommendationWindow")
            ?? AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("PrewarmRecommendationWindow"))
                .FirstOrDefault(type => type != null);

        if (windowType == null)
        {
            EditorUtility.DisplayDialog(
                "Prewarm Recommendations",
                "PrewarmRecommendationWindow type was not found. Check for editor compile errors.",
                "OK");
            return;
        }

        var showWindowMethod = windowType.GetMethod(
            "ShowWindow",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        if (showWindowMethod != null)
        {
            showWindowMethod.Invoke(null, null);
            EditorApplication.delayCall += () =>
            {
                EditorWindow window = EditorWindow.GetWindow(windowType, false, "Prewarm Recommendations");
                if (window != null)
                {
                    window.Show();
                    window.Focus();
                }
            };
            return;
        }

        EditorWindow fallbackWindow = EditorWindow.GetWindow(windowType, false, "Prewarm Recommendations");
        if (fallbackWindow != null)
        {
            fallbackWindow.Show();
            fallbackWindow.Focus();
            return;
        }

        EditorUtility.DisplayDialog(
            "Prewarm Recommendations",
            "The window type was found, but Unity did not open the editor window.",
            "OK");
    }
}
