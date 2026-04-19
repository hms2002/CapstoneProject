using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public static class LoadingBootstrapConfigResolver
{
    public static LoadingBootstrapConfigSO Load()
    {
        LoadingBootstrapConfigSO config = FindPreloaded();
#if UNITY_EDITOR
        if (config == null)
            config = AssetDatabase.LoadAssetAtPath<LoadingBootstrapConfigSO>(LoadingBootstrapConfigSO.SourceAssetPath);
#endif
        return config;
    }

    public static LoadingBootstrapConfigSO FindPreloaded()
    {
        LoadingBootstrapConfigSO[] loadedConfigs = Resources.FindObjectsOfTypeAll<LoadingBootstrapConfigSO>();
        if (loadedConfigs == null || loadedConfigs.Length == 0)
            return null;

#if UNITY_EDITOR
        for (int i = 0; i < loadedConfigs.Length; i++)
        {
            LoadingBootstrapConfigSO candidate = loadedConfigs[i];
            if (candidate == null)
                continue;

            string assetPath = AssetDatabase.GetAssetPath(candidate);
            if (string.Equals(assetPath, LoadingBootstrapConfigSO.SourceAssetPath, System.StringComparison.OrdinalIgnoreCase))
                return candidate;
        }
#endif

        return loadedConfigs[0];
    }
}
