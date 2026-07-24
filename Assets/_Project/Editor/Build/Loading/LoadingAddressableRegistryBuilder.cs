using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 책임 :
/// - 실제 로딩 시스템이 소유한 manifest asset들을 Addressables entry와 registry key로 동기화한다.
/// - 직접 참조 fallback을 유지하되, Addressables mode에서 사용할 주소 lookup table을 공식 빌드한다.
/// </summary>
public static class LoadingAddressableRegistryBuilder
{
    private const string AutoGroupName = "CapstoneLoadingRuntime";
    private const int MissingPreviewCount = 8;
    private static readonly char[] InvalidAddressCharacters = { '[', ']' };

    [MenuItem("Tools/Loading/Build Addressable Registry")]
    private static void BuildRegistryMenu()
    {
        BuildRegistry();
    }

    public static string BuildRegistry()
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
        if (settings == null)
        {
            const string message = "[LoadingAddressableRegistryBuilder] Failed to create or load Addressables settings.";
            Debug.LogError(message);
            return null;
        }

        AddressableAssetGroup targetGroup = EnsureTargetGroup(settings);
        if (targetGroup == null)
        {
            const string message = "[LoadingAddressableRegistryBuilder] Failed to find or create an Addressables group.";
            Debug.LogError(message);
            return null;
        }

        LoadingAddressableRegistrySO registry =
            AssetDatabase.LoadAssetAtPath<LoadingAddressableRegistrySO>(LoadingAddressableRegistrySO.DefaultAssetPath);
        if (registry == null)
        {
            registry = ScriptableObject.CreateInstance<LoadingAddressableRegistrySO>();
            AssetDatabase.CreateAsset(registry, LoadingAddressableRegistrySO.DefaultAssetPath);
        }

        var registryEntries = new List<AddressableAssetKeyEntry>();
        var seenAssetPaths = new HashSet<string>();
        var usedAddressKeys = new HashSet<string>(StringComparer.Ordinal);
        var missingAssetPaths = new List<string>();
        int createdEntryCount = 0;
        int existingEntryCount = 0;
        int updatedAddressCount = 0;
        List<LoadingManifestEditorCollector.ManifestUsage> manifestUsages =
            LoadingManifestEditorCollector.CollectOwnedManifestUsages();
        LogManifestAuthoringIssues();

        for (int i = 0; i < manifestUsages.Count; i++)
        {
            LoadManifestSO manifest = manifestUsages[i].Manifest;
            if (manifest == null)
                continue;

            foreach (Object asset in manifest.EnumerateReferencedAssets())
            {
                if (!TryGetRuntimeAssetPath(asset, seenAssetPaths, out string assetPath))
                    continue;

                AddressableAssetEntry entry = EnsureAddressableEntry(settings, targetGroup, assetPath, out bool createdNow);
                if (entry == null)
                {
                    missingAssetPaths.Add(assetPath);
                    continue;
                }

                if (createdNow)
                    createdEntryCount++;
                else
                    existingEntryCount++;

                if (EnsureSafeAddress(entry, assetPath, usedAddressKeys))
                    updatedAddressCount++;

                registryEntries.Add(new AddressableAssetKeyEntry(asset, entry.address));
            }
        }

        registry.ReplaceEntries(registryEntries);
        EditorUtility.SetDirty(registry);
        EditorUtility.SetDirty(settings);

        LinkBootstrapConfig(registry);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string summary = BuildSummary(
            registryEntries.Count,
            createdEntryCount,
            existingEntryCount,
            updatedAddressCount,
            missingAssetPaths);
        Debug.Log(summary, registry);
        return summary;
    }

    private static void LogManifestAuthoringIssues()
    {
        List<LoadingManifestEditorCollector.ManifestIssue> issues =
            LoadingManifestEditorCollector.CollectManifestIssues();
        for (int i = 0; i < issues.Count; i++)
        {
            string message = $"[LoadingAddressableRegistryBuilder] Manifest authoring {issues[i].Severity}: {issues[i].Message}";
            if (string.Equals(issues[i].Severity, "Error", StringComparison.OrdinalIgnoreCase))
                Debug.LogError(message, issues[i].Context);
            else
                Debug.LogWarning(message, issues[i].Context);
        }
    }

    private static bool TryGetRuntimeAssetPath(Object asset, HashSet<string> seenAssetPaths, out string assetPath)
    {
        assetPath = null;
        if (asset == null)
            return false;

        assetPath = AssetDatabase.GetAssetPath(asset);
        if (string.IsNullOrWhiteSpace(assetPath) ||
            !assetPath.StartsWith("Assets/") ||
            !seenAssetPaths.Add(assetPath))
        {
            return false;
        }

        return true;
    }

    private static AddressableAssetEntry EnsureAddressableEntry(
        AddressableAssetSettings settings,
        AddressableAssetGroup targetGroup,
        string assetPath,
        out bool createdNow)
    {
        createdNow = false;
        string assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
        if (string.IsNullOrWhiteSpace(assetGuid))
            return null;

        AddressableAssetEntry entry = settings.FindAssetEntry(assetGuid);
        if (entry != null)
            return entry;

        entry = settings.CreateOrMoveEntry(assetGuid, targetGroup, false, false);
        if (entry == null)
            return null;

        entry.address = assetPath;
        createdNow = true;
        return entry;
    }

    private static bool EnsureSafeAddress(
        AddressableAssetEntry entry,
        string assetPath,
        HashSet<string> usedAddressKeys)
    {
        if (entry == null)
            return false;

        string sourceAddress = string.IsNullOrWhiteSpace(entry.address) ? assetPath : entry.address;
        string safeAddress = SanitizeAddressKey(sourceAddress);
        if (string.IsNullOrWhiteSpace(safeAddress))
            safeAddress = SanitizeAddressKey(assetPath);

        string assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
        safeAddress = EnsureUniqueAddressKey(safeAddress, assetGuid, usedAddressKeys);

        if (string.Equals(entry.address, safeAddress, StringComparison.Ordinal))
            return false;

        entry.address = safeAddress;
        return true;
    }

    private static string SanitizeAddressKey(string addressKey)
    {
        if (string.IsNullOrWhiteSpace(addressKey))
            return string.Empty;

        string trimmedAddress = addressKey.Trim();
        if (trimmedAddress.IndexOfAny(InvalidAddressCharacters) < 0)
            return trimmedAddress;

        return trimmedAddress
            .Replace("[", string.Empty)
            .Replace("]", string.Empty);
    }

    private static string EnsureUniqueAddressKey(
        string addressKey,
        string assetGuid,
        HashSet<string> usedAddressKeys)
    {
        if (usedAddressKeys == null)
            return addressKey;

        if (usedAddressKeys.Add(addressKey))
            return addressKey;

        string suffix = string.IsNullOrWhiteSpace(assetGuid)
            ? Guid.NewGuid().ToString("N")[..8]
            : assetGuid[..Mathf.Min(8, assetGuid.Length)];
        string uniqueAddress = $"{addressKey}__{suffix}";
        int duplicateIndex = 2;
        while (!usedAddressKeys.Add(uniqueAddress))
        {
            uniqueAddress = $"{addressKey}__{suffix}_{duplicateIndex}";
            duplicateIndex++;
        }

        return uniqueAddress;
    }

    private static AddressableAssetGroup EnsureTargetGroup(AddressableAssetSettings settings)
    {
        if (settings == null)
            return null;

        for (int i = 0; i < settings.groups.Count; i++)
        {
            AddressableAssetGroup group = settings.groups[i];
            if (group != null && string.Equals(group.Name, AutoGroupName))
            {
                ApplyPackTogether(group);
                return group;
            }
        }

        AddressableAssetGroup createdGroup = settings.CreateGroup(
            AutoGroupName,
            false,
            false,
            false,
            null,
            typeof(ContentUpdateGroupSchema),
            typeof(BundledAssetGroupSchema));

        ApplyPackTogether(createdGroup);
        return createdGroup ?? settings.DefaultGroup;
    }

    private static void ApplyPackTogether(AddressableAssetGroup group)
    {
        if (group == null)
            return;

        BundledAssetGroupSchema schema = group.GetSchema<BundledAssetGroupSchema>();
        if (schema != null)
            schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
    }

    private static void LinkBootstrapConfig(LoadingAddressableRegistrySO registry)
    {
        LoadingBootstrapConfigSO bootstrapConfig =
            AssetDatabase.LoadAssetAtPath<LoadingBootstrapConfigSO>(LoadingBootstrapConfigSO.SourceAssetPath);
        if (bootstrapConfig == null)
            return;

        SerializedObject serializedConfig = new SerializedObject(bootstrapConfig);

        SerializedProperty registryProperty = serializedConfig.FindProperty("addressableRegistry");
        if (registryProperty != null)
            registryProperty.objectReferenceValue = registry;

        SerializedProperty providerModeProperty = serializedConfig.FindProperty("assetProviderMode");
        if (providerModeProperty != null)
            providerModeProperty.enumValueIndex = (int)LoadingAssetProviderMode.Addressables;

        serializedConfig.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(bootstrapConfig);
    }

    private static string BuildSummary(
        int registryEntryCount,
        int createdEntryCount,
        int existingEntryCount,
        int updatedAddressCount,
        List<string> missingAssetPaths)
    {
        var builder = new StringBuilder();
        builder.Append(
            $"[LoadingAddressableRegistryBuilder] Built registry with {registryEntryCount} entries. Existing entries reused: {existingEntryCount}, new addressable entries created: {createdEntryCount}, address keys updated: {updatedAddressCount}.");

        if (missingAssetPaths == null || missingAssetPaths.Count == 0)
            return builder.ToString();

        builder.Append($" Missing entries: {missingAssetPaths.Count}.");
        int previewCount = Mathf.Min(MissingPreviewCount, missingAssetPaths.Count);
        for (int i = 0; i < previewCount; i++)
            builder.Append($"\n - {missingAssetPaths[i]}");

        if (missingAssetPaths.Count > previewCount)
            builder.Append($"\n - ... {missingAssetPaths.Count - previewCount} more");

        return builder.ToString();
    }
}
