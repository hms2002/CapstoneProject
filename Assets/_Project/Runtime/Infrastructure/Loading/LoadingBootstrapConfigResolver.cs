using UnityEngine;

/// <summary>
/// 책임 : 로딩 부트스트랩 설정 에셋을 현재 로드된 객체나 에디터 asset path fallback에서 찾아 반환한다.
/// </summary>
public static class LoadingBootstrapConfigResolver
{
    public static LoadingBootstrapConfigSO Load()
    {
        LoadingBootstrapConfigSO config = FindPreloaded();
#if UNITY_EDITOR
        if (config == null)
            config = EditorAuthoringPlayback.LoadAssetAtPath<LoadingBootstrapConfigSO>(LoadingBootstrapConfigSO.SourceAssetPath);
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

            string assetPath = EditorAuthoringPlayback.GetAssetPath(candidate);
            if (string.Equals(assetPath, LoadingBootstrapConfigSO.SourceAssetPath, System.StringComparison.OrdinalIgnoreCase))
                return candidate;
        }
#endif

        return loadedConfigs[0];
    }
}
