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

    private enum ScopeAssetTab
    {
        All,
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
    private readonly Dictionary<string, ScopeAssetTab> scopeTabStates = new();
    private Vector2 leftScroll;
    private Vector2 rightScroll;
    private bool showAssets = true;

    [MenuItem("Tools/Loading/Load Manifest Inspector")]
    public static void ShowWindow()
    {
        GetWindow<LoadManifestInspectorWindow>("Manifest Inspector");
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
            {
                DrawSummary();
            }

            using (new EditorGUILayout.VerticalScope())
            {
                DrawScopes();
            }
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

            string showAssetsLabel = showAssets ? "Assets: On" : "Assets: Off";
            showAssets = GUILayout.Toggle(showAssets, showAssetsLabel, EditorStyles.toolbarButton, GUILayout.Width(90f));

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
                    EditorGUILayout.HelpBox("Assets 목록이 숨겨져 있습니다. 상단 툴바의 'Assets: Off'를 눌러 표시하세요.", MessageType.None);
                    continue;
                }

                ScopeAssetTab currentTab = DrawScopeTabs(scope.Label, scope);
                scopeTabStates[scope.Label] = currentTab;

                List<AssetEntry> filteredAssets = FilterAssets(scope.Assets, currentTab);
                if (filteredAssets.Count == 0)
                {
                    EditorGUILayout.HelpBox("현재 탭에 표시할 asset이 없습니다.", MessageType.None);
                    continue;
                }

                for (int assetIndex = 0; assetIndex < filteredAssets.Count; assetIndex++)
                {
                    AssetEntry entry = filteredAssets[assetIndex];
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
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void Analyze()
    {
        issues.Clear();
        scopes.Clear();

        if (routeSet == null)
        {
            issues.Add(new Issue { severity = Severity.Error, message = "RouteSet이 지정되지 않았습니다." });
            return;
        }

        ResolveDefaultBootstrapConfigIfMissing();

        LoadManifestSO bootManifest = bootstrapConfig != null ? bootstrapConfig.BootManifest : null;
        RouteSetLoadManifestSO routeManifest = routeSet.LoadManifest;
        List<LoadManifestSO> runCommonManifests = FindRunCommonManifests(routeSet);

        if (bootstrapConfig == null)
            issues.Add(new Issue { severity = Severity.Warning, message = "Bootstrap config를 찾지 못했습니다. Boot 검사 범위가 비어 있습니다." });
        else if (bootManifest == null)
            issues.Add(new Issue { severity = Severity.Warning, message = "Bootstrap config에 Boot manifest가 연결되지 않았습니다." });

        if (routeManifest == null)
        {
            issues.Add(new Issue { severity = Severity.Error, message = $"{routeSet.name}에 RouteSetLoadManifest가 없습니다." });
            return;
        }

        if (routeManifest.SharedManifest == null)
            issues.Add(new Issue { severity = Severity.Warning, message = "Shared manifest가 비어 있습니다." });
        if (routeManifest.CorridorManifest == null)
            issues.Add(new Issue { severity = Severity.Warning, message = "Corridor manifest가 비어 있습니다." });
        if (routeManifest.BossManifest == null)
            issues.Add(new Issue { severity = Severity.Warning, message = "Boss manifest가 비어 있습니다." });

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
            issues.Add(new Issue { severity = Severity.Info, message = "중복/누락 경고가 없습니다." });
    }

    private void AddOverlapIssue(
        string leftLabel,
        Dictionary<string, AssetEntry> left,
        string rightLabel,
        Dictionary<string, AssetEntry> right)
    {
        if (left.Count == 0 || right.Count == 0)
            return;

        var overlap = left.Keys.Intersect(right.Keys, StringComparer.Ordinal).Take(5).ToList();
        if (overlap.Count == 0)
            return;

        string sample = string.Join(", ", overlap.Select(key => left.TryGetValue(key, out AssetEntry entry) ? entry.Path : key));
        issues.Add(new Issue
        {
            severity = Severity.Warning,
            message = $"{leftLabel}과 {rightLabel}에 중복 asset이 있습니다. sample: {sample}"
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

    private ScopeAssetTab GetScopeTab(string label)
    {
        if (scopeTabStates.TryGetValue(label, out ScopeAssetTab tab))
            return tab;

        scopeTabStates[label] = ScopeAssetTab.All;
        return ScopeAssetTab.All;
    }

    private ScopeAssetTab DrawScopeTabs(string label, ScopeSnapshot scope)
    {
        ScopeAssetTab currentTab = GetScopeTab(label);
        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField("Categories", EditorStyles.boldLabel);
        ScopeAssetTab[] firstRowTabs =
        {
            ScopeAssetTab.All,
            ScopeAssetTab.Prefab,
            ScopeAssetTab.Audio,
            ScopeAssetTab.Cue
        };
        ScopeAssetTab[] secondRowTabs =
        {
            ScopeAssetTab.Data,
            ScopeAssetTab.Material,
            ScopeAssetTab.Font,
            ScopeAssetTab.Other
        };

        currentTab = DrawTabRow(scope, currentTab, firstRowTabs);
        currentTab = DrawTabRow(scope, currentTab, secondRowTabs);
        EditorGUILayout.LabelField($"Current Filter: {BuildTabLabel(scope, currentTab)}", EditorStyles.miniLabel);
        EditorGUILayout.Space(2f);
        return currentTab;
    }

    private ScopeAssetTab DrawTabRow(ScopeSnapshot scope, ScopeAssetTab currentTab, ScopeAssetTab[] rowTabs)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            for (int i = 0; i < rowTabs.Length; i++)
            {
                ScopeAssetTab rowTab = rowTabs[i];
                bool isSelected = rowTab == currentTab;
                GUIStyle style = GetTabButtonStyle(i, rowTabs.Length);
                bool nextSelected = GUILayout.Toggle(isSelected, BuildTabLabel(scope, rowTab), style, GUILayout.Height(22f));
                if (nextSelected)
                    currentTab = rowTab;
            }
        }

        return currentTab;
    }

    private static GUIStyle GetTabButtonStyle(int index, int length)
    {
        if (length <= 1)
            return EditorStyles.miniButton;

        if (index == 0)
            return EditorStyles.miniButtonLeft;

        if (index == length - 1)
            return EditorStyles.miniButtonRight;

        return EditorStyles.miniButtonMid;
    }

    private static string BuildTabLabel(ScopeSnapshot scope, ScopeAssetTab tab)
    {
        int count = tab == ScopeAssetTab.All
            ? scope.Assets.Count
            : CountAssets(scope.Assets, ToAssetCategory(tab));

        return $"{tab} ({count})";
    }

    private static AssetCategory ToAssetCategory(ScopeAssetTab tab)
    {
        return tab switch
        {
            ScopeAssetTab.Prefab => AssetCategory.Prefab,
            ScopeAssetTab.Audio => AssetCategory.Audio,
            ScopeAssetTab.Cue => AssetCategory.Cue,
            ScopeAssetTab.Data => AssetCategory.Data,
            ScopeAssetTab.Material => AssetCategory.Material,
            ScopeAssetTab.Font => AssetCategory.Font,
            _ => AssetCategory.Other
        };
    }

    private static string[] BuildTabLabels(ScopeSnapshot scope)
    {
        int prefabCount = CountAssets(scope.Assets, AssetCategory.Prefab);
        int audioCount = CountAssets(scope.Assets, AssetCategory.Audio);
        int cueCount = CountAssets(scope.Assets, AssetCategory.Cue);
        int dataCount = CountAssets(scope.Assets, AssetCategory.Data);
        int materialCount = CountAssets(scope.Assets, AssetCategory.Material);
        int fontCount = CountAssets(scope.Assets, AssetCategory.Font);
        int otherCount = CountAssets(scope.Assets, AssetCategory.Other);

        return new[]
        {
            $"All ({scope.Assets.Count})",
            $"Prefab ({prefabCount})",
            $"Audio ({audioCount})",
            $"Cue ({cueCount})",
            $"Data ({dataCount})",
            $"Material ({materialCount})",
            $"Font ({fontCount})",
            $"Other ({otherCount})"
        };
    }

    private static int CountAssets(List<AssetEntry> assets, AssetCategory category)
    {
        int count = 0;
        for (int i = 0; i < assets.Count; i++)
        {
            if (assets[i].Category == category)
                count++;
        }

        return count;
    }

    private static List<AssetEntry> FilterAssets(List<AssetEntry> assets, ScopeAssetTab tab)
    {
        if (tab == ScopeAssetTab.All)
            return assets;

        AssetCategory category = ToAssetCategory(tab);

        var filtered = new List<AssetEntry>();
        for (int i = 0; i < assets.Count; i++)
        {
            if (assets[i].Category == category)
                filtered.Add(assets[i]);
        }

        return filtered;
    }
}
