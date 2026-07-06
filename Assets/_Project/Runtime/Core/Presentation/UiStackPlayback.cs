using UnityEngine;

/// <summary>
/// 책임 : Core/Gameplay 계층이 구체 UI 구현 없이 스택 UI 열기와 외부 입력 차단 소유권을 요청하게 하는 backend 계약이다.
/// </summary>
public interface IUiStackBackend
{
    bool SetExternalUiInputBlocked(Object owner, bool blocked);
    bool CanOpenUIForExternalBlockOwner(Object owner, IStackableUI ui);
    bool TryPushUIForExternalBlockOwner(Object owner, IStackableUI ui);
}

/// <summary>
/// 책임 : Gameplay 호출자가 구체 UI 스택 구현을 참조하지 않고 스택 UI 열기와 입력 차단 소유권을 요청하게 한다.
/// </summary>
public static class UiStackPlayback
{
    private static IUiStackBackend backend;

    public static void RegisterBackend(IUiStackBackend stackBackend)
    {
        backend = stackBackend;
    }

    public static bool SetExternalUiInputBlocked(Object owner, bool blocked)
    {
        if (owner == null || backend == null)
            return false;

        return backend.SetExternalUiInputBlocked(owner, blocked);
    }

    public static bool CanOpenForExternalBlockOwner(Object owner, IStackableUI ui)
    {
        if (ui == null)
            return false;

        return backend == null || backend.CanOpenUIForExternalBlockOwner(owner, ui);
    }

    public static bool TryPushForExternalBlockOwner(Object owner, IStackableUI ui)
    {
        if (ui == null)
            return false;

        if (backend != null)
            return backend.TryPushUIForExternalBlockOwner(owner, ui);

        ui.OpenUI();
        return true;
    }
}
