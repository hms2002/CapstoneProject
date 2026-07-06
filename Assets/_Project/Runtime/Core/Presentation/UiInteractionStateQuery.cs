/// <summary>
/// 책임 : Core/Gameplay 계층이 구체 UI 구현 없이 현재 UI 입력 차단 상태를 질의하게 하는 backend 계약이다.
/// </summary>
public interface IUiInteractionStateBackend
{
    bool HasBlockingUI();
    bool HasActivePopup();
    bool IsExternalUiInputBlocked { get; }
}

/// <summary>
/// 책임 : Gameplay 호출자가 구체 UI 구현을 참조하지 않고 UI 입력 차단/팝업 상태를 조회하게 한다.
/// </summary>
public static class UiInteractionStateQuery
{
    private static IUiInteractionStateBackend backend;

    public static void RegisterBackend(IUiInteractionStateBackend interactionStateBackend)
    {
        backend = interactionStateBackend;
    }

    public static bool HasBlockingUI()
    {
        return backend != null && backend.HasBlockingUI();
    }

    public static bool HasActivePopup()
    {
        return backend != null && backend.HasActivePopup();
    }

    public static bool IsExternalUiInputBlocked()
    {
        return backend != null && backend.IsExternalUiInputBlocked;
    }
}
