using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

/// <summary>
/// 책임 :
/// - 실제 로딩 시스템이 소유한 manifest window를 Addressables의 source of truth로 보고 registry/group/address 준비 상태를 검증한다.
/// - 누락, 빈 주소, stale registry entry를 리포트만 하고 에셋 이동이나 자동 수정은 수행하지 않는다.
/// </summary>
public static class LoadingAddressableReadinessReporter
{
    private const int MaxPreviewCount = 40;

    [MenuItem("Tools/Loading/Addressables Readiness Report")]
    public static void RunReport()
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
        LoadingAddressableRegistrySO registry =
            AssetDatabase.LoadAssetAtPath<LoadingAddressableRegistrySO>(LoadingAddressableRegistrySO.DefaultAssetPath);

        HashSet<string> manifestAssetPaths = LoadingManifestEditorCollector.CollectOwnedManifestAssetPaths();

        var issues = new List<string>();
        AppendManifestAuthoringIssues(issues);

        if (settings == null)
            issues.Add("Addressables settings asset is missing.");

        if (registry == null)
            issues.Add($"Registry asset is missing: {LoadingAddressableRegistrySO.DefaultAssetPath}");

        if (settings != null && registry != null)
            ValidateManifestAssets(settings, registry, manifestAssetPaths, issues);

        if (registry != null)
            ValidateRegistryStaleEntries(registry, manifestAssetPaths, issues);

        string message = BuildReportMessage(manifestAssetPaths.Count, issues);
        if (issues.Count > 0)
            Debug.LogWarning(message, registry);
        else
            Debug.Log(message, registry);
    }

    private static void AppendManifestAuthoringIssues(List<string> issues)
    {
        List<LoadingManifestEditorCollector.ManifestIssue> manifestIssues =
            LoadingManifestEditorCollector.CollectManifestIssues();
        for (int i = 0; i < manifestIssues.Count; i++)
            issues.Add($"Manifest authoring {manifestIssues[i].Severity}: {manifestIssues[i].Message}");
    }

    private static void ValidateManifestAssets(
        AddressableAssetSettings settings,
        LoadingAddressableRegistrySO registry,
        HashSet<string> manifestAssetPaths,
        List<string> issues)
    {
        foreach (string assetPath in manifestAssetPaths)
        {
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            if (asset == null)
            {
                issues.Add($"Manifest asset is missing from AssetDatabase: {assetPath}");
                continue;
            }

            if (!registry.TryGetAddressKey(asset, out string registryAddress))
            {
                issues.Add($"Registry entry missing or empty address: {assetPath}");
            }

            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            AddressableAssetEntry addressableEntry = !string.IsNullOrWhiteSpace(guid)
                ? settings.FindAssetEntry(guid)
                : null;
            if (addressableEntry == null)
            {
                issues.Add($"Addressables entry missing: {assetPath}");
                continue;
            }

            if (addressableEntry.parentGroup == null)
                issues.Add($"Addressables group missing: {assetPath}");

            if (string.IsNullOrWhiteSpace(addressableEntry.address))
                issues.Add($"Addressables address is empty: {assetPath}");

            if (!string.IsNullOrWhiteSpace(registryAddress) &&
                !string.IsNullOrWhiteSpace(addressableEntry.address) &&
                !string.Equals(registryAddress, addressableEntry.address, System.StringComparison.Ordinal))
            {
                issues.Add($"Registry/address mismatch: {assetPath} registry='{registryAddress}', addressables='{addressableEntry.address}'");
            }
        }
    }

    private static void ValidateRegistryStaleEntries(
        LoadingAddressableRegistrySO registry,
        HashSet<string> manifestAssetPaths,
        List<string> issues)
    {
        IReadOnlyList<AddressableAssetKeyEntry> entries = registry.Entries;
        if (entries == null)
            return;

        for (int i = 0; i < entries.Count; i++)
        {
            AddressableAssetKeyEntry entry = entries[i];
            Object sourceAsset = entry.SourceAsset;
            string assetPath = AssetDatabase.GetAssetPath(sourceAsset);

            if (sourceAsset == null || string.IsNullOrWhiteSpace(assetPath))
            {
                issues.Add($"Registry stale entry has missing source asset at index {i}.");
                continue;
            }

            if (!manifestAssetPaths.Contains(assetPath))
                issues.Add($"Registry stale entry is not referenced by any owned loading manifest: {assetPath}");
        }
    }

    private static string BuildReportMessage(int manifestAssetCount, List<string> issues)
    {
        var builder = new StringBuilder();
        builder.Append($"[LoadingAddressableReadinessReporter] Checked {manifestAssetCount} manifest assets. Issues: {issues.Count}.");

        int previewCount = Mathf.Min(MaxPreviewCount, issues.Count);
        for (int i = 0; i < previewCount; i++)
            builder.Append($"\n - {issues[i]}");

        if (issues.Count > previewCount)
            builder.Append($"\n - ... {issues.Count - previewCount} more");

        return builder.ToString();
    }
}
