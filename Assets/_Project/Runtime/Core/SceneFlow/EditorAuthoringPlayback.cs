using System;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 책임 : 런타임 어셈블리가 구체 에디터 API 없이 authoring 보조 기능을 요청하게 하는 backend 계약이다.
/// </summary>
public interface IEditorAuthoringBackend
{
    void MarkDirty(Object target);
    T LoadAssetAtPath<T>(string assetPath) where T : Object;
    T[] LoadAllAssetsAtPath<T>(string assetPath) where T : Object;
    string[] FindAssetPaths(string filter);
    string GetAssetPath(Object target);
    void CreateAsset(Object asset, string assetPath);
    void SaveAssets();
    void RefreshAssets();
    bool IsPersistent(Object target);
    bool IsPrefabAsset(Object target);
    bool IsSelectedGameObject(GameObject gameObject);
    bool IsUpdatingOrCompiling();
    bool IsPlayingOrWillChangePlaymode();
    double GetTimeSinceStartup();
    void QueueDelayCall(Action action);
    void DrawHandleLabel(Vector3 position, string text, Color color, bool bold);
}

/// <summary>
/// 책임 : 에디터 authoring 요청을 현재 등록된 Editor backend로 전달하고, 런타임에서는 안전한 no-op을 제공한다.
/// </summary>
public static class EditorAuthoringPlayback
{
    private static IEditorAuthoringBackend backend;

    public static void RegisterBackend(IEditorAuthoringBackend authoringBackend)
    {
        backend = authoringBackend;
    }

    public static void UnregisterBackend(IEditorAuthoringBackend authoringBackend)
    {
        if (ReferenceEquals(backend, authoringBackend))
            backend = null;
    }

    public static void MarkDirty(Object target)
    {
        if (target != null)
            backend?.MarkDirty(target);
    }

    public static T LoadAssetAtPath<T>(string assetPath) where T : Object
    {
        return backend != null && !string.IsNullOrWhiteSpace(assetPath)
            ? backend.LoadAssetAtPath<T>(assetPath)
            : null;
    }

    public static T[] LoadAllAssetsAtPath<T>(string assetPath) where T : Object
    {
        return backend != null && !string.IsNullOrWhiteSpace(assetPath)
            ? backend.LoadAllAssetsAtPath<T>(assetPath)
            : Array.Empty<T>();
    }

    public static string[] FindAssetPaths(string filter)
    {
        return backend != null && !string.IsNullOrWhiteSpace(filter)
            ? backend.FindAssetPaths(filter)
            : Array.Empty<string>();
    }

    public static string GetAssetPath(Object target)
    {
        return target != null && backend != null ? backend.GetAssetPath(target) : string.Empty;
    }

    public static void CreateAsset(Object asset, string assetPath)
    {
        if (asset != null && backend != null && !string.IsNullOrWhiteSpace(assetPath))
            backend.CreateAsset(asset, assetPath);
    }

    public static void SaveAssets()
    {
        backend?.SaveAssets();
    }

    public static void RefreshAssets()
    {
        backend?.RefreshAssets();
    }

    public static bool IsPersistent(Object target)
    {
        return target != null && backend != null && backend.IsPersistent(target);
    }

    public static bool IsPrefabAsset(Object target)
    {
        return target != null && backend != null && backend.IsPrefabAsset(target);
    }

    public static bool IsSelectedGameObject(GameObject gameObject)
    {
        return gameObject != null && backend != null && backend.IsSelectedGameObject(gameObject);
    }

    public static bool IsUpdatingOrCompiling()
    {
        return backend != null && backend.IsUpdatingOrCompiling();
    }

    public static bool IsPlayingOrWillChangePlaymode()
    {
        return backend != null && backend.IsPlayingOrWillChangePlaymode();
    }

    public static double GetTimeSinceStartup()
    {
        return backend != null ? backend.GetTimeSinceStartup() : Time.realtimeSinceStartupAsDouble;
    }

    public static void QueueDelayCall(Action action)
    {
        if (action != null)
            backend?.QueueDelayCall(action);
    }

    public static void DrawHandleLabel(Vector3 position, string text, Color color, bool bold = false)
    {
        if (!string.IsNullOrEmpty(text))
            backend?.DrawHandleLabel(position, text, color, bold);
    }
}
