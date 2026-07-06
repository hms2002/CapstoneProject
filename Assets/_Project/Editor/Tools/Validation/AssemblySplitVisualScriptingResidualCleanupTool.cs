using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 책임:
/// - asmdef 분리 검증을 방해하는 Visual Scripting 잔재를 패키지 부재와 참조 단절이 확인된 경우에만 정리한다.
/// - Unity Editor 메뉴와 batchmode executeMethod 양쪽에서 같은 안전 조건을 사용한다.
/// </summary>
public static class AssemblySplitVisualScriptingResidualCleanupTool
{
    private const string ManifestPath = "Packages/manifest.json";
    private const string ProjectSettingsPath = "ProjectSettings/VisualScriptingSettings.asset";
    private const string ProjectSettingsMetaPath = ProjectSettingsPath + ".meta";
    private const string PixelLightTestScenePath = "Assets/_Project/Scenes/PixelLightTest.unity";
    private const string MenuRoot = "Tools/Validation/Assembly Split";
    private const string VisualScriptingSceneVariablesGuid = "765181c9ef4b24d32a4f7cbd2ef370dc";
    private const string VisualScriptingVariablesGuid = "e741851cba3ad425c91ecf922cc6b379";

    private static readonly string[] GraphAssetPaths =
    {
        "Assets/_Project/Data/VisualScripting/Graphs/Hazard.asset",
        "Assets/_Project/Data/VisualScripting/Graphs/Input Movement.asset",
        "Assets/_Project/Data/VisualScripting/Graphs/Scale Wave.asset"
    };

    private static readonly string[] SerializedReferenceScanRoots =
    {
        "Assets/_Project",
        "Assets/AddressableAssetsData",
        "ProjectSettings"
    };

    [MenuItem(MenuRoot + "/Report Visual Scripting Residuals")]
    public static void ReportVisualScriptingResiduals()
    {
        VisualScriptingResidualCleanupReport report = BuildReport();
        Debug.Log(report.FormatForLog());
    }

    [MenuItem(MenuRoot + "/Apply Visual Scripting Residual Cleanup")]
    public static void ApplyVisualScriptingResidualCleanupFromMenu()
    {
        if (!EditorUtility.DisplayDialog(
                "Apply Visual Scripting Residual Cleanup",
                "This removes stale Visual Scripting graph assets, PixelLightTest scene components, and ProjectSettings only when the Visual Scripting package is absent and the graph assets are unreferenced. Continue?",
                "Apply",
                "Cancel"))
        {
            return;
        }

        ApplyVisualScriptingResidualCleanup(exitEditorWhenFinished: false);
    }

    public static void ApplyVisualScriptingResidualCleanupFromCommandLine()
    {
        bool succeeded = ApplyVisualScriptingResidualCleanup(exitEditorWhenFinished: true);
        EditorApplication.Exit(succeeded ? 0 : 1);
    }

    public static bool ApplyVisualScriptingResidualCleanup(bool exitEditorWhenFinished)
    {
        try
        {
            VisualScriptingResidualCleanupReport report = BuildReport();
            if (report.VisualScriptingPackageInstalled)
            {
                Debug.LogError("Visual Scripting package is installed. Cleanup aborted to preserve authored graph content.");
                return false;
            }

            bool pixelLightSceneCleaned = PixelLightTestScaleWaveReplacementTool.ReplaceBeatingSpotLightScaleWaveGraphForAssemblySplitCleanup();
            if (!pixelLightSceneCleaned)
            {
                Debug.LogError("PixelLightTest scene cleanup failed. Visual Scripting asset/settings cleanup aborted to avoid partial cleanup.");
                return false;
            }

            int deletedGraphAssets = DeleteUnreferencedGraphAssets(report);
            int deletedSettingsFiles = DeleteVisualScriptingProjectSettings();

            AssetDatabase.Refresh();

            Debug.Log(
                "Visual Scripting residual cleanup finished. " +
                $"DeletedGraphAssets={deletedGraphAssets}, " +
                $"DeletedSettingsFiles={deletedSettingsFiles}, " +
                $"PixelLightSceneCleaned={pixelLightSceneCleaned}.");

            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return false;
        }
        finally
        {
            if (exitEditorWhenFinished)
                AssetDatabase.Refresh();
        }
    }

    private static VisualScriptingResidualCleanupReport BuildReport()
    {
        VisualScriptingResidualCleanupReport report = new VisualScriptingResidualCleanupReport
        {
            VisualScriptingPackageInstalled = IsVisualScriptingPackageInstalled(),
            ProjectSettingsExists = File.Exists(ToAbsolutePath(ProjectSettingsPath)),
            PixelLightSceneExists = File.Exists(ToAbsolutePath(PixelLightTestScenePath)),
            PixelLightMissingComponentReferences = CountPixelLightMissingComponentReferences()
        };

        for (int i = 0; i < GraphAssetPaths.Length; i++)
        {
            string assetPath = GraphAssetPaths[i];
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            bool exists = !string.IsNullOrWhiteSpace(guid);
            bool externallyReferenced = exists && HasExternalGuidReference(guid, assetPath);
            report.GraphAssets.Add(new VisualScriptingGraphAssetState(assetPath, guid, exists, externallyReferenced));
        }

        return report;
    }

    private static bool IsVisualScriptingPackageInstalled()
    {
        string manifestFullPath = ToAbsolutePath(ManifestPath);
        if (!File.Exists(manifestFullPath))
            return false;

        string manifestText = File.ReadAllText(manifestFullPath);
        return manifestText.Contains("\"com.unity.visualscripting\"", StringComparison.Ordinal);
    }

    private static int CountPixelLightMissingComponentReferences()
    {
        string sceneFullPath = ToAbsolutePath(PixelLightTestScenePath);
        if (!File.Exists(sceneFullPath))
            return 0;

        string sceneText = File.ReadAllText(sceneFullPath);
        return CountOccurrences(sceneText, VisualScriptingSceneVariablesGuid) +
               CountOccurrences(sceneText, VisualScriptingVariablesGuid);
    }

    private static int CountOccurrences(string text, string pattern)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(pattern))
            return 0;

        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += pattern.Length;
        }

        return count;
    }

    private static int DeleteUnreferencedGraphAssets(VisualScriptingResidualCleanupReport report)
    {
        int deletedCount = 0;
        for (int i = 0; i < report.GraphAssets.Count; i++)
        {
            VisualScriptingGraphAssetState graphAsset = report.GraphAssets[i];
            if (!graphAsset.Exists || graphAsset.ExternallyReferenced)
                continue;

            if (AssetDatabase.DeleteAsset(graphAsset.AssetPath))
                deletedCount++;
            else
                Debug.LogWarning($"Failed to delete stale Visual Scripting graph asset: {graphAsset.AssetPath}");
        }

        return deletedCount;
    }

    private static int DeleteVisualScriptingProjectSettings()
    {
        int deletedCount = 0;
        deletedCount += DeleteFileIfExists(ProjectSettingsPath);
        deletedCount += DeleteFileIfExists(ProjectSettingsMetaPath);
        return deletedCount;
    }

    private static int DeleteFileIfExists(string projectRelativePath)
    {
        string fullPath = ToAbsolutePath(projectRelativePath);
        if (!File.Exists(fullPath))
            return 0;

        File.Delete(fullPath);
        return 1;
    }

    private static bool HasExternalGuidReference(string guid, string owningAssetPath)
    {
        if (string.IsNullOrWhiteSpace(guid))
            return false;

        string owningMetaPath = owningAssetPath + ".meta";
        for (int i = 0; i < SerializedReferenceScanRoots.Length; i++)
        {
            string root = ToAbsolutePath(SerializedReferenceScanRoots[i]);
            if (!Directory.Exists(root))
                continue;

            foreach (string file in Directory.GetFiles(root, "*.*", SearchOption.AllDirectories))
            {
                string projectRelativePath = ToProjectRelativePath(file);
                if (string.Equals(projectRelativePath, owningAssetPath, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(projectRelativePath, owningMetaPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!IsTextAssetPath(file))
                    continue;

                string text = File.ReadAllText(file);
                if (text.Contains(guid, StringComparison.Ordinal))
                    return true;
            }
        }

        return false;
    }

    private static bool IsTextAssetPath(string path)
    {
        string extension = Path.GetExtension(path);
        return string.Equals(extension, ".asset", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".prefab", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".unity", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".meta", StringComparison.OrdinalIgnoreCase);
    }

    private static string ToAbsolutePath(string projectRelativePath)
    {
        return Path.Combine(Directory.GetParent(Application.dataPath).FullName, projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static string ToProjectRelativePath(string fullPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName.Replace('\\', '/').TrimEnd('/');
        string normalizedPath = Path.GetFullPath(fullPath).Replace('\\', '/');
        if (!normalizedPath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
            return normalizedPath;

        return normalizedPath.Substring(projectRoot.Length + 1);
    }

    /// <summary>
    /// 책임:
    /// - Visual Scripting 그래프 asset 하나의 존재 여부와 외부 GUID 참조 여부를 전달한다.
    /// </summary>
    private readonly struct VisualScriptingGraphAssetState
    {
        public VisualScriptingGraphAssetState(string assetPath, string guid, bool exists, bool externallyReferenced)
        {
            AssetPath = assetPath;
            Guid = guid;
            Exists = exists;
            ExternallyReferenced = externallyReferenced;
        }

        public string AssetPath { get; }
        public string Guid { get; }
        public bool Exists { get; }
        public bool ExternallyReferenced { get; }
    }

    /// <summary>
    /// 책임:
    /// - Visual Scripting 잔재 정리 전 안전 조건과 발견 항목을 로그 출력용으로 모은다.
    /// </summary>
    private sealed class VisualScriptingResidualCleanupReport
    {
        public bool VisualScriptingPackageInstalled;
        public bool ProjectSettingsExists;
        public bool PixelLightSceneExists;
        public int PixelLightMissingComponentReferences;
        public readonly List<VisualScriptingGraphAssetState> GraphAssets = new List<VisualScriptingGraphAssetState>();

        public string FormatForLog()
        {
            List<string> lines = new List<string>
            {
                "Visual Scripting residual report:",
                $"  PackageInstalled={VisualScriptingPackageInstalled}",
                $"  ProjectSettingsExists={ProjectSettingsExists}",
                $"  PixelLightSceneExists={PixelLightSceneExists}",
                $"  PixelLightMissingComponentReferences={PixelLightMissingComponentReferences}"
            };

            for (int i = 0; i < GraphAssets.Count; i++)
            {
                VisualScriptingGraphAssetState asset = GraphAssets[i];
                lines.Add(
                    $"  GraphAsset path='{asset.AssetPath}', exists={asset.Exists}, guid='{asset.Guid}', externallyReferenced={asset.ExternallyReferenced}");
            }

            return string.Join(Environment.NewLine, lines);
        }
    }
}
