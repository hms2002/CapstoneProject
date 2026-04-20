using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

public static class LoadingAddressableRegistryBuilder
{
    private const string AutoGroupName = "CapstoneLoadingRuntime";
    private const int MissingPreviewCount = 8;

    [MenuItem("Tools/Loading/Build Addressable Registry")]
    private static void BuildRegistry()
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
        if (settings == null)
        {
            Debug.LogError("[LoadingAddressableRegistryBuilder] Failed to create or load Addressables settings.");
            return;
        }

        AddressableAssetGroup targetGroup = EnsureTargetGroup(settings);
        if (targetGroup == null)
        {
            Debug.LogError("[LoadingAddressableRegistryBuilder] Failed to find or create an Addressables group.");
            return;
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
        var missingAssetPaths = new List<string>();
        string[] manifestGuids = AssetDatabase.FindAssets("t:LoadManifestSO");
        int createdEntryCount = 0;
        int existingEntryCount = 0;

        for (int i = 0; i < manifestGuids.Length; i++)
        {
            string manifestPath = AssetDatabase.GUIDToAssetPath(manifestGuids[i]);
            LoadManifestSO manifest = AssetDatabase.LoadAssetAtPath<LoadManifestSO>(manifestPath);
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

                string addressKey = string.IsNullOrWhiteSpace(entry.address) ? assetPath : entry.address;
                if (string.IsNullOrWhiteSpace(entry.address))
                    entry.address = addressKey;

                registryEntries.Add(new AddressableAssetKeyEntry(asset, addressKey));
            }
        }

        registry.ReplaceEntries(registryEntries);
        EditorUtility.SetDirty(registry);
        EditorUtility.SetDirty(settings);

        LinkBootstrapConfig(registry);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(BuildSummary(registryEntries.Count, createdEntryCount, existingEntryCount, missingAssetPaths), registry);
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

    private static AddressableAssetGroup EnsureTargetGroup(AddressableAssetSettings settings)
    {
        if (settings == null)
            return null;

        for (int i = 0; i < settings.groups.Count; i++)
        {
            AddressableAssetGroup group = settings.groups[i];
            if (group != null && string.Equals(group.Name, AutoGroupName))
                return group;
        }

        AddressableAssetGroup createdGroup = settings.CreateGroup(
            AutoGroupName,
            false,
            false,
            false,
            null,
            typeof(ContentUpdateGroupSchema),
            typeof(BundledAssetGroupSchema));

        return createdGroup ?? settings.DefaultGroup;
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
        List<string> missingAssetPaths)
    {
        var builder = new StringBuilder();
        builder.Append(
            $"[LoadingAddressableRegistryBuilder] Built registry with {registryEntryCount} entries. Existing entries reused: {existingEntryCount}, new addressable entries created: {createdEntryCount}.");

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
