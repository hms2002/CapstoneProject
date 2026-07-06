using System;
using UnityEngine;

/// <summary>
/// 책임 : 비-UI 계층이 구체 GlobalUIRoot 없이 전역 캔버스와 UI 서비스 루트에 접근하게 하는 backend 계약이다.
/// </summary>
public interface IGlobalCanvasBackend
{
    Canvas GetCanvas(GlobalCanvasLayer layer);
    void AdoptService(Transform target);
    T[] GetComponentsInRoot<T>(bool includeInactive) where T : Component;
}

/// <summary>
/// 책임 : 전역 UI 루트 조회와 서비스 부모 지정 요청을 현재 등록된 UI backend로 중계한다.
/// </summary>
public static class GlobalCanvasPlayback
{
    private static IGlobalCanvasBackend backend;

    public static void RegisterBackend(IGlobalCanvasBackend canvasBackend)
    {
        backend = canvasBackend;
    }

    public static Canvas GetCanvas(GlobalCanvasLayer layer)
    {
        return backend != null ? backend.GetCanvas(layer) : null;
    }

    public static void AdoptService(Transform target)
    {
        backend?.AdoptService(target);
    }

    public static T[] GetComponentsInRoot<T>(bool includeInactive = true) where T : Component
    {
        return backend != null ? backend.GetComponentsInRoot<T>(includeInactive) : Array.Empty<T>();
    }
}
