using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 책임 : Core의 에디터 authoring 요청을 UnityEditor API 호출로 변환한다.
/// </summary>
[InitializeOnLoad]
public sealed class EditorAuthoringEditorBackend : IEditorAuthoringBackend
{
    private static readonly EditorAuthoringEditorBackend Instance = new EditorAuthoringEditorBackend();

    static EditorAuthoringEditorBackend()
    {
        EditorAuthoringPlayback.RegisterBackend(Instance);
    }

    public void MarkDirty(Object target)
    {
        if (target != null)
            EditorUtility.SetDirty(target);
    }

    public T LoadAssetAtPath<T>(string assetPath) where T : Object
    {
        return string.IsNullOrWhiteSpace(assetPath)
            ? null
            : AssetDatabase.LoadAssetAtPath<T>(assetPath);
    }

    public T[] LoadAllAssetsAtPath<T>(string assetPath) where T : Object
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            return Array.Empty<T>();

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        if (assets == null || assets.Length == 0)
            return Array.Empty<T>();

        int count = 0;
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is T)
                count++;
        }

        if (count == 0)
            return Array.Empty<T>();

        T[] typedAssets = new T[count];
        int index = 0;
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is T typedAsset)
                typedAssets[index++] = typedAsset;
        }

        return typedAssets;
    }

    public string[] FindAssetPaths(string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return Array.Empty<string>();

        string[] guids = AssetDatabase.FindAssets(filter);
        if (guids == null || guids.Length == 0)
            return Array.Empty<string>();

        string[] paths = new string[guids.Length];
        int count = 0;
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (!string.IsNullOrWhiteSpace(path))
                paths[count++] = path;
        }

        if (count == paths.Length)
            return paths;

        Array.Resize(ref paths, count);
        return paths;
    }

    public string GetAssetPath(Object target)
    {
        return target != null ? AssetDatabase.GetAssetPath(target) : string.Empty;
    }

    public void CreateAsset(Object asset, string assetPath)
    {
        if (asset != null && !string.IsNullOrWhiteSpace(assetPath))
            AssetDatabase.CreateAsset(asset, assetPath);
    }

    public void SaveAssets()
    {
        AssetDatabase.SaveAssets();
    }

    public void RefreshAssets()
    {
        AssetDatabase.Refresh();
    }

    public bool IsPersistent(Object target)
    {
        return target != null && EditorUtility.IsPersistent(target);
    }

    public bool IsPrefabAsset(Object target)
    {
        return target != null && PrefabUtility.IsPartOfPrefabAsset(target);
    }

    public bool IsSelectedGameObject(GameObject gameObject)
    {
        return gameObject != null && Selection.activeGameObject == gameObject;
    }

    public bool IsUpdatingOrCompiling()
    {
        return EditorApplication.isCompiling || EditorApplication.isUpdating;
    }

    public bool IsPlayingOrWillChangePlaymode()
    {
        return EditorApplication.isPlayingOrWillChangePlaymode;
    }

    public double GetTimeSinceStartup()
    {
        return EditorApplication.timeSinceStartup;
    }

    public void QueueDelayCall(Action action)
    {
        if (action != null)
            EditorApplication.delayCall += () => action();
    }

    public void DrawHandleLabel(Vector3 position, string text, Color color, bool bold)
    {
        if (string.IsNullOrEmpty(text))
            return;

        Color previousColor = Handles.color;
        Handles.color = color;
        if (bold)
        {
            GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
            style.normal.textColor = color;
            Handles.Label(position, text, style);
        }
        else
        {
            Handles.Label(position, text);
        }

        Handles.color = previousColor;
    }
}
