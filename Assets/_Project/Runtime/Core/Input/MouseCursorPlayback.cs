using UnityEngine;

/// <summary>
/// 책임 : 커서 표시 상태를 제어하는 구체 런타임 서비스를 Core/Gameplay 호출자에게 숨기는 backend 계약이다.
/// </summary>
public interface IMouseCursorBackend
{
    void SetInteractable(Object owner, bool active);
    void SetHidden(Object owner, bool hidden);
}

/// <summary>
/// 책임 : Gameplay 코드가 Infrastructure 커서 서비스 타입 없이 커서 상호작용/숨김 상태를 요청하게 한다.
/// </summary>
public static class MouseCursorPlayback
{
    private static IMouseCursorBackend backend;

    public static bool IsAvailable => IsBackendAlive(backend);

    public static void RegisterBackend(IMouseCursorBackend cursorBackend)
    {
        backend = cursorBackend;
    }

    public static void UnregisterBackend(IMouseCursorBackend cursorBackend)
    {
        if (ReferenceEquals(backend, cursorBackend))
            backend = null;
    }

    public static bool SetInteractable(Object owner, bool active)
    {
        if (!IsAvailable)
            return false;

        backend.SetInteractable(owner, active);
        return true;
    }

    public static bool SetHidden(Object owner, bool hidden)
    {
        if (!IsAvailable)
            return false;

        backend.SetHidden(owner, hidden);
        return true;
    }

    private static bool IsBackendAlive(IMouseCursorBackend cursorBackend)
    {
        if (cursorBackend == null)
            return false;

        if (cursorBackend is Component component)
            return component != null;

        return true;
    }
}
