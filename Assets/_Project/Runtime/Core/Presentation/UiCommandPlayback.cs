/// <summary>
/// 책임 : Core/Gameplay 계층이 구체 UI 구현 없이 공통 UI 정리/프롬프트 명령을 요청하게 하는 backend 계약이다.
/// </summary>
public interface IUiCommandBackend
{
    void CloseAllPopups(bool force);
    void HideHoverImmediate();
    void HideWorldPrompt();
    void RefreshWorldPrompt(IInteractable target);
}

/// <summary>
/// 책임 : 월드 상호작용 프롬프트 뷰가 Core 상호작용 대상 정보를 직접 표시할 수 있게 하는 최소 표시 계약이다.
/// </summary>
public interface IWorldInteractionPromptView
{
    void Refresh(IInteractable target);
    void Hide();
}

/// <summary>
/// 책임 : Gameplay 호출자가 구체 UI 구현을 참조하지 않고 공통 UI 명령을 요청하게 한다.
/// </summary>
public static class UiCommandPlayback
{
    private static IUiCommandBackend backend;

    public static void RegisterBackend(IUiCommandBackend commandBackend)
    {
        backend = commandBackend;
    }

    public static void CloseAllPopups(bool force = true)
    {
        backend?.CloseAllPopups(force);
    }

    public static void HideHoverImmediate()
    {
        backend?.HideHoverImmediate();
    }

    public static bool HideWorldPrompt()
    {
        if (backend == null)
            return false;

        backend.HideWorldPrompt();
        return true;
    }

    public static bool RefreshWorldPrompt(IInteractable target)
    {
        if (backend == null)
            return false;

        backend.RefreshWorldPrompt(target);
        return true;
    }
}
