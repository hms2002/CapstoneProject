using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public enum LoadingManifestUsageKind
{
    Boot,
    RunCommon,
    RouteShared,
    RouteCorridor,
    RouteBoss
}

/// <summary>
/// 책임 :
/// - Addressables/Loading 에디터 도구가 LoadManifestSO를 전역 스캔하지 않도록, manifest의 소유 맥락을 함께 수집한다.
/// - Bootstrap, RunRouteCatalog, CorridorBossRouteSetSO 경로를 기준으로 실제 로딩 시스템과 같은 manifest window를 재구성한다.
/// </summary>
public static class LoadingManifestEditorCollector
{
    /// <summary>
    /// 책임 : 하나의 LoadManifestSO가 어느 로딩 슬롯/route set에서 사용되는지 표현한다.
    /// </summary>
    public readonly struct ManifestUsage
    {
        public ManifestUsage(
            LoadingManifestUsageKind kind,
            LoadManifestSO manifest,
            string scopeLabel,
            CorridorBossRouteSetSO routeSet = null)
        {
            Kind = kind;
            Manifest = manifest;
            ScopeLabel = string.IsNullOrWhiteSpace(scopeLabel) ? "<unknown>" : scopeLabel;
            RouteSet = routeSet;
        }

        public LoadingManifestUsageKind Kind { get; }
        public LoadManifestSO Manifest { get; }
        public string ScopeLabel { get; }
        public CorridorBossRouteSetSO RouteSet { get; }
    }

    /// <summary>
    /// 책임 : RouteSet/Manifest 연결 데이터의 authoring 오류를 사람이 읽을 수 있는 메시지로 보관한다.
    /// </summary>
    public readonly struct ManifestIssue
    {
        public ManifestIssue(string severity, string message, Object context = null)
        {
            Severity = string.IsNullOrWhiteSpace(severity) ? "Info" : severity;
            Message = string.IsNullOrWhiteSpace(message) ? "<empty>" : message;
            Context = context;
        }

        public string Severity { get; }
        public string Message { get; }
        public Object Context { get; }
    }

    public static List<ManifestUsage> CollectOwnedManifestUsages()
    {
        var usages = new List<ManifestUsage>();

        LoadingBootstrapConfigSO bootstrapConfig =
            AssetDatabase.LoadAssetAtPath<LoadingBootstrapConfigSO>(LoadingBootstrapConfigSO.SourceAssetPath);
        if (bootstrapConfig != null && bootstrapConfig.BootManifest != null)
            usages.Add(new ManifestUsage(LoadingManifestUsageKind.Boot, bootstrapConfig.BootManifest, "Boot"));

        foreach (RunRouteCatalogSO catalog in EnumerateRunRouteCatalogs())
        {
            if (catalog.RunCommonLoadManifest != null)
                usages.Add(new ManifestUsage(LoadingManifestUsageKind.RunCommon, catalog.RunCommonLoadManifest, catalog.name));
        }

        foreach (CorridorBossRouteSetSO routeSet in EnumerateRouteSets())
            AddRouteSetManifestUsages(routeSet, usages);

        return usages;
    }

    public static HashSet<string> CollectOwnedManifestAssetPaths()
    {
        var assetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<ManifestUsage> usages = CollectOwnedManifestUsages();
        for (int i = 0; i < usages.Count; i++)
        {
            LoadManifestSO manifest = usages[i].Manifest;
            if (manifest == null)
                continue;

            foreach (Object asset in manifest.EnumerateReferencedAssets())
            {
                string assetPath = AssetDatabase.GetAssetPath(asset);
                if (IsRuntimeAssetPath(assetPath))
                    assetPaths.Add(assetPath);
            }
        }

        return assetPaths;
    }

    public static List<ManifestIssue> CollectManifestIssues()
    {
        var issues = new List<ManifestIssue>();
        ValidateRouteSetManifestAssignments(issues);
        ValidateRunCommonManifestAssignments(issues);
        return issues;
    }

    public static IEnumerable<CorridorBossRouteSetSO> EnumerateRouteSets()
    {
        string[] routeSetGuids = AssetDatabase.FindAssets("t:CorridorBossRouteSetSO");
        var seenRouteSetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < routeSetGuids.Length; i++)
        {
            string routeSetPath = AssetDatabase.GUIDToAssetPath(routeSetGuids[i]);
            CorridorBossRouteSetSO routeSet = AssetDatabase.LoadAssetAtPath<CorridorBossRouteSetSO>(routeSetPath);
            if (routeSet == null || !seenRouteSetPaths.Add(routeSetPath))
                continue;

            yield return routeSet;
        }
    }

    private static IEnumerable<RunRouteCatalogSO> EnumerateRunRouteCatalogs()
    {
        string[] catalogGuids = AssetDatabase.FindAssets("t:RunRouteCatalogSO");
        var seenCatalogPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < catalogGuids.Length; i++)
        {
            string catalogPath = AssetDatabase.GUIDToAssetPath(catalogGuids[i]);
            RunRouteCatalogSO catalog = AssetDatabase.LoadAssetAtPath<RunRouteCatalogSO>(catalogPath);
            if (catalog == null || !seenCatalogPaths.Add(catalogPath))
                continue;

            yield return catalog;
        }
    }

    private static void AddRouteSetManifestUsages(CorridorBossRouteSetSO routeSet, List<ManifestUsage> usages)
    {
        if (routeSet == null || routeSet.LoadManifest == null || usages == null)
            return;

        RouteSetLoadManifestSO routeManifest = routeSet.LoadManifest;
        if (routeManifest.SharedManifest != null)
            usages.Add(new ManifestUsage(LoadingManifestUsageKind.RouteShared, routeManifest.SharedManifest, routeSet.name, routeSet));

        if (routeManifest.CorridorManifest != null)
            usages.Add(new ManifestUsage(LoadingManifestUsageKind.RouteCorridor, routeManifest.CorridorManifest, routeSet.name, routeSet));

        if (routeManifest.BossManifest != null)
            usages.Add(new ManifestUsage(LoadingManifestUsageKind.RouteBoss, routeManifest.BossManifest, routeSet.name, routeSet));
    }

    private static void ValidateRouteSetManifestAssignments(List<ManifestIssue> issues)
    {
        var routeSetsByManifestPath = new Dictionary<string, List<CorridorBossRouteSetSO>>(StringComparer.OrdinalIgnoreCase);

        foreach (CorridorBossRouteSetSO routeSet in EnumerateRouteSets())
        {
            if (routeSet.LoadManifest == null)
            {
                issues.Add(new ManifestIssue("Warning", $"{routeSet.name} has no RouteSetLoadManifest assigned.", routeSet));
                continue;
            }

            string manifestPath = AssetDatabase.GetAssetPath(routeSet.LoadManifest);
            if (string.IsNullOrWhiteSpace(manifestPath))
                manifestPath = routeSet.LoadManifest.name;

            if (!routeSetsByManifestPath.TryGetValue(manifestPath, out List<CorridorBossRouteSetSO> owners))
            {
                owners = new List<CorridorBossRouteSetSO>();
                routeSetsByManifestPath.Add(manifestPath, owners);
            }

            owners.Add(routeSet);

            if (routeSet.LoadManifest.SharedManifest == null)
                issues.Add(new ManifestIssue("Warning", $"{routeSet.name} load manifest has no sharedManifest.", routeSet));

            if (routeSet.LoadManifest.CorridorManifest == null)
                issues.Add(new ManifestIssue("Warning", $"{routeSet.name} load manifest has no corridorManifest.", routeSet));

            if (routeSet.LoadManifest.BossManifest == null)
                issues.Add(new ManifestIssue("Warning", $"{routeSet.name} load manifest has no bossManifest.", routeSet));
        }

        foreach (KeyValuePair<string, List<CorridorBossRouteSetSO>> pair in routeSetsByManifestPath)
        {
            if (pair.Value.Count <= 1)
                continue;

            string ownerNames = string.Join(", ", pair.Value.ConvertAll(routeSet => routeSet != null ? routeSet.name : "<null>"));
            issues.Add(new ManifestIssue(
                "Error",
                $"Multiple CorridorBossRouteSetSO assets share the same RouteSetLoadManifestSO: {ownerNames}",
                pair.Value[0]));
        }
    }

    private static void ValidateRunCommonManifestAssignments(List<ManifestIssue> issues)
    {
        foreach (RunRouteCatalogSO catalog in EnumerateRunRouteCatalogs())
        {
            if (catalog.RunCommonLoadManifest == null)
                issues.Add(new ManifestIssue("Info", $"{catalog.name} has no runCommonLoadManifest assigned.", catalog));
        }
    }

    private static bool IsRuntimeAssetPath(string assetPath)
    {
        return !string.IsNullOrWhiteSpace(assetPath) && assetPath.StartsWith("Assets/", StringComparison.Ordinal);
    }
}
