using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 책임 :
/// - LoadManifestSO 사용 맥락을 분석해서 Addressables group 추천안을 만든다.
/// - 추천안을 바로 적용하지 않고, 작업자가 preview diff를 확인한 뒤 선택한 항목만 이동하게 한다.
/// </summary>
public sealed class AddressableBundlePlannerWindow : EditorWindow
{
    private const string BootCommonGroupName = "BootCommon";
    private const string RunCommonGroupName = "RunCommon";
    private const string CombatCommonGroupName = "CombatCommon";
    private const string ReviewNeededGroupName = "ReviewNeeded";

    private readonly List<PlanRow> rows = new();
    private Vector2 scrollPosition;
    private string summary = "Analyze를 눌러 manifest 기반 group 추천안을 생성하세요.";
    private string lastReviewNeededReportPath;
    private string lastClassifiedReportPath;
    private bool showOnlyChanged = true;
    private List<LoadingManifestEditorCollector.ManifestIssue> latestManifestIssues = new();

    [MenuItem("Tools/Loading/Addressable Bundle Planner")]
    public static void Open()
    {
        GetWindow<AddressableBundlePlannerWindow>("Addressable Bundle Planner");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Manifest-First Addressable Bundle Planner", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "LoadManifestSO가 '언제 필요한가'를 결정하고, 이 창은 그 사용 맥락으로 Addressables group을 추천합니다. v1은 Pack Together만 사용합니다.",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Analyze", GUILayout.Width(120)))
                Analyze();

            GUI.enabled = rows.Count > 0;
            if (GUILayout.Button("Apply Selected", GUILayout.Width(120)))
                ApplySelected();

            if (GUILayout.Button("Select Changed", GUILayout.Width(120)))
                SelectChanged();

            if (GUILayout.Button("Clear Selection", GUILayout.Width(120)))
                ClearSelection();
            GUI.enabled = true;

            if (!string.IsNullOrWhiteSpace(lastReviewNeededReportPath) &&
                GUILayout.Button("Reveal Review Report", GUILayout.Width(150)))
            {
                EditorUtility.RevealInFinder(lastReviewNeededReportPath);
            }

            if (!string.IsNullOrWhiteSpace(lastClassifiedReportPath) &&
                GUILayout.Button("Reveal Classified Report", GUILayout.Width(160)))
            {
                EditorUtility.RevealInFinder(lastClassifiedReportPath);
            }

            GUILayout.FlexibleSpace();
            showOnlyChanged = EditorGUILayout.ToggleLeft("Show Changed Only", showOnlyChanged, GUILayout.Width(150));
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField(summary, EditorStyles.wordWrappedLabel);
        EditorGUILayout.Space(4f);

        DrawRows();
    }

    private void Analyze()
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
        var usageByPath = new Dictionary<string, AssetUsage>(StringComparer.OrdinalIgnoreCase);
        latestManifestIssues = LoadingManifestEditorCollector.CollectManifestIssues();

        List<LoadingManifestEditorCollector.ManifestUsage> manifestUsages =
            LoadingManifestEditorCollector.CollectOwnedManifestUsages();
        for (int i = 0; i < manifestUsages.Count; i++)
            AddManifestUsage(usageByPath, manifestUsages[i]);

        rows.Clear();
        foreach (AssetUsage usage in usageByPath.Values)
        {
            string recommendedGroup = RecommendGroup(usage);
            string currentGroup = ResolveCurrentGroup(settings, usage.AssetPath);
            rows.Add(new PlanRow(usage, currentGroup, recommendedGroup));
        }

        rows.Sort((left, right) => string.Compare(left.AssetPath, right.AssetPath, StringComparison.OrdinalIgnoreCase));
        SelectChanged();
        summary = BuildSummary(settings);
        WritePlannerReports();
    }

    private void DrawRows()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        for (int i = 0; i < rows.Count; i++)
        {
            PlanRow row = rows[i];
            if (showOnlyChanged && !row.HasGroupChange)
                continue;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    row.Selected = EditorGUILayout.Toggle(row.Selected, GUILayout.Width(18));
                    EditorGUILayout.ObjectField(row.Asset, typeof(Object), false, GUILayout.Width(220));

                    if (GUILayout.Button("Ping", GUILayout.Width(48)))
                        EditorGUIUtility.PingObject(row.Asset);
                }

                EditorGUILayout.LabelField("Asset", row.AssetPath);
                EditorGUILayout.LabelField("Current", row.CurrentGroup);
                EditorGUILayout.LabelField("Recommended", row.RecommendedGroup);
                EditorGUILayout.LabelField("Usage", row.UsageLabel);
                EditorGUILayout.LabelField("Dependencies", row.DependencyNote);
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void ApplySelected()
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
        if (settings == null)
        {
            Debug.LogError("[AddressableBundlePlannerWindow] Addressables settings를 찾거나 만들 수 없습니다.");
            return;
        }

        int movedCount = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            PlanRow row = rows[i];
            if (!row.Selected || !row.HasGroupChange)
                continue;

            if (string.Equals(row.RecommendedGroup, ReviewNeededGroupName, StringComparison.Ordinal))
                continue;

            AddressableAssetGroup group = EnsureGroup(settings, row.RecommendedGroup);
            if (group == null)
                continue;

            string guid = AssetDatabase.AssetPathToGUID(row.AssetPath);
            if (string.IsNullOrWhiteSpace(guid))
                continue;

            AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group, false, false);
            if (entry == null)
                continue;

            if (string.IsNullOrWhiteSpace(entry.address))
                entry.address = row.AssetPath;

            row.CurrentGroup = row.RecommendedGroup;
            row.Selected = false;
            movedCount++;
        }

        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        summary = $"Applied {movedCount} selected Addressables group changes. Build Addressable Registry를 다시 실행해 registry address를 동기화하세요.";
        Debug.Log($"[AddressableBundlePlannerWindow] Applied {movedCount} selected Addressables group changes.");
    }

    private void SelectChanged()
    {
        for (int i = 0; i < rows.Count; i++)
        {
            PlanRow row = rows[i];
            row.Selected = row.HasGroupChange &&
                           !string.Equals(row.RecommendedGroup, ReviewNeededGroupName, StringComparison.Ordinal);
        }
    }

    private void ClearSelection()
    {
        for (int i = 0; i < rows.Count; i++)
            rows[i].Selected = false;
    }

    private static void AddManifestUsage(
        Dictionary<string, AssetUsage> usageByPath,
        LoadingManifestEditorCollector.ManifestUsage manifestUsage)
    {
        LoadManifestSO manifest = manifestUsage.Manifest;
        if (manifest == null)
            return;

        ManifestUsageKind kind = ConvertManifestUsageKind(manifestUsage.Kind);
        string scopeLabel = manifestUsage.ScopeLabel;
        string groupName = BuildRecommendedGroupName(manifestUsage);
        if (string.IsNullOrWhiteSpace(groupName))
            return;

        foreach (Object asset in manifest.EnumerateReferencedAssets())
        {
            string assetPath = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrWhiteSpace(assetPath) || !assetPath.StartsWith("Assets/"))
                continue;

            if (!usageByPath.TryGetValue(assetPath, out AssetUsage usage))
            {
                usage = new AssetUsage(asset, assetPath);
                usageByPath.Add(assetPath, usage);
            }

            usage.Add(kind, scopeLabel, groupName);
        }
    }

    private static ManifestUsageKind ConvertManifestUsageKind(LoadingManifestUsageKind kind)
    {
        return kind switch
        {
            LoadingManifestUsageKind.Boot => ManifestUsageKind.BootCommon,
            LoadingManifestUsageKind.RunCommon => ManifestUsageKind.RunCommon,
            LoadingManifestUsageKind.RouteShared => ManifestUsageKind.RouteShared,
            LoadingManifestUsageKind.RouteCorridor => ManifestUsageKind.RouteCorridor,
            LoadingManifestUsageKind.RouteBoss => ManifestUsageKind.Boss,
            _ => ManifestUsageKind.RouteShared
        };
    }

    private static string BuildRecommendedGroupName(LoadingManifestEditorCollector.ManifestUsage manifestUsage)
    {
        CorridorBossRouteSetSO routeSet = manifestUsage.RouteSet;
        string routeName = SanitizeGroupToken(routeSet != null ? routeSet.name : manifestUsage.ScopeLabel);
        string bossName = routeSet != null && !string.IsNullOrWhiteSpace(routeSet.BossSceneName)
            ? SanitizeGroupToken(routeSet.BossSceneName)
            : routeName;

        return manifestUsage.Kind switch
        {
            LoadingManifestUsageKind.Boot => BootCommonGroupName,
            LoadingManifestUsageKind.RunCommon => RunCommonGroupName,
            LoadingManifestUsageKind.RouteShared => $"RouteShared_{routeName}",
            LoadingManifestUsageKind.RouteCorridor => $"RouteCorridor_{routeName}",
            LoadingManifestUsageKind.RouteBoss => $"Boss_{bossName}",
            _ => ReviewNeededGroupName
        };
    }

    private static string RecommendGroup(AssetUsage usage)
    {
        if (usage == null)
            return ReviewNeededGroupName;

        if (usage.HasKind(ManifestUsageKind.BootCommon))
            return BootCommonGroupName;

        if (usage.HasKind(ManifestUsageKind.RunCommon))
            return RunCommonGroupName;

        if (IsCombatCommonCandidate(usage.AssetPath))
            return CombatCommonGroupName;

        if (usage.RouteGroupNames.Count == 1)
        {
            foreach (string groupName in usage.RouteGroupNames)
                return groupName;
        }

        return ReviewNeededGroupName;
    }

    private static bool IsCombatCommonCandidate(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            return false;

        string lowerPath = assetPath.ToLowerInvariant();
        return lowerPath.Contains("/telegraph") ||
               lowerPath.Contains("damagepopup") ||
               lowerPath.Contains("/vfx/common") ||
               lowerPath.Contains("/presentation/common") ||
               lowerPath.Contains("hitspark");
    }

    private static string ResolveCurrentGroup(AddressableAssetSettings settings, string assetPath)
    {
        if (settings == null || string.IsNullOrWhiteSpace(assetPath))
            return "<not addressable>";

        string guid = AssetDatabase.AssetPathToGUID(assetPath);
        AddressableAssetEntry entry = !string.IsNullOrWhiteSpace(guid) ? settings.FindAssetEntry(guid) : null;
        if (entry == null || entry.parentGroup == null)
            return "<not addressable>";

        return entry.parentGroup.Name;
    }

    private static AddressableAssetGroup EnsureGroup(AddressableAssetSettings settings, string groupName)
    {
        AddressableAssetGroup group = settings.FindGroup(groupName);
        if (group == null)
        {
            group = settings.CreateGroup(
                groupName,
                false,
                false,
                false,
                null,
                typeof(ContentUpdateGroupSchema),
                typeof(BundledAssetGroupSchema));
        }

        BundledAssetGroupSchema schema = group != null ? group.GetSchema<BundledAssetGroupSchema>() : null;
        if (schema != null)
            schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;

        return group;
    }

    private static string SanitizeGroupToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Unknown";

        var builder = new StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            builder.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
        }

        return builder.ToString();
    }

    private string BuildSummary(AddressableAssetSettings settings)
    {
        int changedCount = 0;
        int reviewCount = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].HasGroupChange)
                changedCount++;

            if (string.Equals(rows[i].RecommendedGroup, ReviewNeededGroupName, StringComparison.Ordinal))
                reviewCount++;
        }

        string settingsState = settings != null ? "Addressables settings found" : "Addressables settings missing";
        return $"{settingsState}. Analyzed {rows.Count} manifest assets. Recommended changes: {changedCount}. ReviewNeeded: {reviewCount}. Manifest issues: {latestManifestIssues.Count}.";
    }

    private enum ManifestUsageKind
    {
        BootCommon,
        RunCommon,
        RouteShared,
        RouteCorridor,
        Boss
    }

    /// <summary>
    /// 책임 : 하나의 에셋이 어떤 manifest scope에서 참조되는지 누적해서 group 추천의 입력값으로 제공한다.
    /// </summary>
    private sealed class AssetUsage
    {
        private readonly HashSet<ManifestUsageKind> usageKinds = new();
        private readonly HashSet<string> usageLabels = new(StringComparer.Ordinal);

        public AssetUsage(Object asset, string assetPath)
        {
            Asset = asset;
            AssetPath = assetPath;
        }

        public Object Asset { get; }
        public string AssetPath { get; }
        public HashSet<string> RouteGroupNames { get; } = new(StringComparer.Ordinal);
        public int RouteGroupCount => RouteGroupNames.Count;

        public void Add(ManifestUsageKind kind, string scopeLabel, string groupName)
        {
            usageKinds.Add(kind);

            if (!string.IsNullOrWhiteSpace(scopeLabel))
                usageLabels.Add($"{kind}:{scopeLabel}");

            if (kind == ManifestUsageKind.RouteShared ||
                kind == ManifestUsageKind.RouteCorridor ||
                kind == ManifestUsageKind.Boss)
            {
                if (!string.IsNullOrWhiteSpace(groupName))
                    RouteGroupNames.Add(groupName);
            }
        }

        public bool HasKind(ManifestUsageKind kind)
        {
            return usageKinds.Contains(kind);
        }

        public string BuildUsageLabel()
        {
            if (usageLabels.Count == 0)
                return "<none>";

            return string.Join(", ", usageLabels);
        }
    }

    /// <summary>
    /// 책임 : Bundle Planner UI에 표시할 asset diff 한 줄의 상태와 적용 선택 여부를 보관한다.
    /// </summary>
    private sealed class PlanRow
    {
        public PlanRow(AssetUsage usage, string currentGroup, string recommendedGroup)
        {
            Asset = usage.Asset;
            AssetPath = usage.AssetPath;
            CurrentGroup = currentGroup;
            RecommendedGroup = recommendedGroup;
            UsageLabel = usage.BuildUsageLabel();
            UsagePatternKey = UsageLabel;
            ReviewReason = BuildReviewReason(usage, recommendedGroup);
            ManualHint = BuildManualHint(ReviewReason, recommendedGroup);

            string[] dependencies = AssetDatabase.GetDependencies(AssetPath, true);
            int dependencyCount = dependencies != null ? Mathf.Max(0, dependencies.Length - 1) : 0;
            DependencyNote = $"{dependencyCount} dependencies found. v1 reports dependencies but moves only the direct manifest asset.";
        }

        public Object Asset { get; }
        public string AssetPath { get; }
        public string CurrentGroup { get; set; }
        public string RecommendedGroup { get; }
        public string UsageLabel { get; }
        public string UsagePatternKey { get; }
        public string ReviewReason { get; }
        public string ManualHint { get; }
        public string DependencyNote { get; }
        public bool Selected { get; set; }

        public bool HasGroupChange => !string.Equals(CurrentGroup, RecommendedGroup, StringComparison.Ordinal);

        private static string BuildReviewReason(AssetUsage usage, string recommendedGroup)
        {
            if (!string.Equals(recommendedGroup, ReviewNeededGroupName, StringComparison.Ordinal))
                return $"classified: {recommendedGroup}";

            if (usage.RouteGroupCount > 1)
            {
                if (usage.HasKind(ManifestUsageKind.Boss))
                    return "review: boss asset referenced by multiple route sets";

                if (usage.HasKind(ManifestUsageKind.RouteCorridor))
                    return "review: corridor asset referenced by multiple route sets";

                if (usage.HasKind(ManifestUsageKind.RouteShared))
                    return "review: shared route asset referenced by multiple route sets";

                return "review: multiple route-specific groups";
            }

            return "review: no safe automatic group rule";
        }

        private static string BuildManualHint(string reviewReason, string recommendedGroup)
        {
            if (!string.Equals(recommendedGroup, ReviewNeededGroupName, StringComparison.Ordinal))
                return "자동 추천을 적용해도 됩니다. 단, 에셋 소유권이 group 이름과 맞는지만 확인하세요.";

            if (reviewReason.Contains("boss asset", StringComparison.Ordinal))
                return "보스 전용인지, 실제 공통 보스 presentation인지 확인하세요. 특정 보스 전용이면 해당 Boss_* group으로 수동 분류하세요.";

            if (reviewReason.Contains("corridor asset", StringComparison.Ordinal))
                return "복도 공통 지형/UI/VFX인지 확인하세요. 두 route가 항상 같은 active window에서 쓰이면 RouteShared 또는 RunCommon 후보입니다.";

            if (reviewReason.Contains("shared route asset", StringComparison.Ordinal))
                return "여러 route에서 의도적으로 공유하는 에셋이면 RunCommon/CombatCommon 후보이고, 우연한 scene dependency면 manifest 정리를 검토하세요.";

            return "사용 맥락을 보고 BootCommon/RunCommon/CombatCommon/Route/Boss 중 하나로 수동 판단하세요.";
        }
    }

    private void WritePlannerReports()
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string reportDirectory = Path.Combine(projectRoot, "Logs", "Addressables");
        Directory.CreateDirectory(reportDirectory);

        lastReviewNeededReportPath = Path.Combine(reportDirectory, "AddressableBundlePlanner_ReviewNeeded.txt");
        lastClassifiedReportPath = Path.Combine(reportDirectory, "AddressableBundlePlanner_ClassifiedAssets.txt");

        WriteRowsReport(
            lastReviewNeededReportPath,
            "Addressable Bundle Planner - ReviewNeeded Assets",
            row => string.Equals(row.RecommendedGroup, ReviewNeededGroupName, StringComparison.Ordinal));

        WriteRowsReport(
            lastClassifiedReportPath,
            "Addressable Bundle Planner - Classified Assets",
            row => !string.Equals(row.RecommendedGroup, ReviewNeededGroupName, StringComparison.Ordinal));

        Debug.Log(
            $"[AddressableBundlePlannerWindow] Reports written:\n - {lastReviewNeededReportPath}\n - {lastClassifiedReportPath}");
    }

    private void WriteRowsReport(string reportPath, string title, Func<PlanRow, bool> rowFilter)
    {
        var filteredRows = new List<PlanRow>();
        for (int i = 0; i < rows.Count; i++)
        {
            PlanRow row = rows[i];
            if (rowFilter(row))
                filteredRows.Add(row);
        }

        var builder = new StringBuilder();
        builder.AppendLine(title);
        builder.AppendLine($"Generated At: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine($"Total Rows: {filteredRows.Count}");
        builder.AppendLine();

        AppendManifestIssues(builder, latestManifestIssues);
        AppendSummary(builder, filteredRows);
        AppendGroupedRows(builder, filteredRows);

        File.WriteAllText(reportPath, builder.ToString(), Encoding.UTF8);
    }

    private static void AppendManifestIssues(
        StringBuilder builder,
        List<LoadingManifestEditorCollector.ManifestIssue> manifestIssues)
    {
        builder.AppendLine("## Manifest Authoring Issues");
        if (manifestIssues == null || manifestIssues.Count == 0)
        {
            builder.AppendLine("- <none>");
            builder.AppendLine();
            return;
        }

        for (int i = 0; i < manifestIssues.Count; i++)
            builder.AppendLine($"- [{manifestIssues[i].Severity}] {manifestIssues[i].Message}");

        builder.AppendLine();
    }

    private static void AppendSummary(StringBuilder builder, List<PlanRow> reportRows)
    {
        AppendCountSection(builder, "Current Group Summary", reportRows, row => row.CurrentGroup);
        AppendCountSection(builder, "Recommended Group Summary", reportRows, row => row.RecommendedGroup);
        AppendCountSection(builder, "Review Reason Summary", reportRows, row => row.ReviewReason);
        AppendCountSection(builder, "Usage Pattern Summary", reportRows, row => row.UsagePatternKey);
    }

    private static void AppendCountSection(
        StringBuilder builder,
        string title,
        List<PlanRow> reportRows,
        Func<PlanRow, string> keySelector)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < reportRows.Count; i++)
        {
            string key = keySelector(reportRows[i]);
            if (string.IsNullOrWhiteSpace(key))
                key = "<none>";

            counts.TryGetValue(key, out int count);
            counts[key] = count + 1;
        }

        var entries = new List<KeyValuePair<string, int>>(counts);
        entries.Sort((left, right) =>
        {
            int countCompare = right.Value.CompareTo(left.Value);
            return countCompare != 0
                ? countCompare
                : string.Compare(left.Key, right.Key, StringComparison.Ordinal);
        });

        builder.AppendLine($"## {title}");
        if (entries.Count == 0)
        {
            builder.AppendLine("- <none>");
            builder.AppendLine();
            return;
        }

        for (int i = 0; i < entries.Count; i++)
            builder.AppendLine($"- {entries[i].Key}: {entries[i].Value}");

        builder.AppendLine();
    }

    private static void AppendGroupedRows(StringBuilder builder, List<PlanRow> reportRows)
    {
        var rowsByReason = new Dictionary<string, List<PlanRow>>(StringComparer.Ordinal);
        for (int i = 0; i < reportRows.Count; i++)
        {
            PlanRow row = reportRows[i];
            if (!rowsByReason.TryGetValue(row.ReviewReason, out List<PlanRow> groupRows))
            {
                groupRows = new List<PlanRow>();
                rowsByReason.Add(row.ReviewReason, groupRows);
            }

            groupRows.Add(row);
        }

        var reasonEntries = new List<KeyValuePair<string, List<PlanRow>>>(rowsByReason);
        reasonEntries.Sort((left, right) => right.Value.Count.CompareTo(left.Value.Count));

        builder.AppendLine("## Assets By Reason");
        if (reasonEntries.Count == 0)
        {
            builder.AppendLine("No assets.");
            return;
        }

        for (int i = 0; i < reasonEntries.Count; i++)
        {
            KeyValuePair<string, List<PlanRow>> reasonEntry = reasonEntries[i];
            builder.AppendLine();
            builder.AppendLine($"### {reasonEntry.Key} ({reasonEntry.Value.Count})");
            reasonEntry.Value.Sort((left, right) =>
            {
                int usageCompare = string.Compare(left.UsagePatternKey, right.UsagePatternKey, StringComparison.Ordinal);
                return usageCompare != 0
                    ? usageCompare
                    : string.Compare(left.AssetPath, right.AssetPath, StringComparison.OrdinalIgnoreCase);
            });

            for (int j = 0; j < reasonEntry.Value.Count; j++)
                AppendRow(builder, j + 1, reasonEntry.Value[j]);
        }
    }

    private static void AppendRow(StringBuilder builder, int index, PlanRow row)
    {
        builder.AppendLine($"[{index}] {row.AssetPath}");
        builder.AppendLine($"Current Group: {row.CurrentGroup}");
        builder.AppendLine($"Recommended Group: {row.RecommendedGroup}");
        builder.AppendLine($"Usage: {row.UsageLabel}");
        builder.AppendLine($"Review Reason: {row.ReviewReason}");
        builder.AppendLine($"Manual Hint: {row.ManualHint}");
        builder.AppendLine($"Dependencies: {row.DependencyNote}");
        builder.AppendLine();
    }
}
