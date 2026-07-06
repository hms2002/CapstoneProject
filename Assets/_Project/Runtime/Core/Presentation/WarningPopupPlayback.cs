/// <summary>
/// 책임 : Core/Gameplay 계층이 구체 경고 팝업 UI 없이 표시할 경고 코드나 메시지를 전달하는 요청 값 타입이다.
/// </summary>
public readonly struct WarningPopupRequest
{
    public readonly WarningPopupCode Code;
    public readonly string Message;
    public readonly float Duration;
    public readonly bool HasDuration;

    public WarningPopupRequest(
        WarningPopupCode code,
        string message,
        float duration,
        bool hasDuration)
    {
        Code = code;
        Message = message;
        Duration = duration;
        HasDuration = hasDuration;
    }

    public static WarningPopupRequest FromCode(WarningPopupCode code)
    {
        return new WarningPopupRequest(code, null, 0f, hasDuration: false);
    }

    public static WarningPopupRequest FromCode(WarningPopupCode code, float duration)
    {
        return new WarningPopupRequest(code, null, duration, hasDuration: true);
    }

    public static WarningPopupRequest FromMessage(string message)
    {
        return new WarningPopupRequest(WarningPopupCode.None, message, 0f, hasDuration: false);
    }

    public static WarningPopupRequest FromMessage(string message, float duration)
    {
        return new WarningPopupRequest(WarningPopupCode.None, message, duration, hasDuration: true);
    }
}

/// <summary>
/// 책임 : Core 경고 팝업 요청을 실제 UI 표시 구현으로 넘기는 backend 계약이다.
/// </summary>
public interface IWarningPopupBackend
{
    void ShowWarning(in WarningPopupRequest request);
}

/// <summary>
/// 책임 : Core/Gameplay 호출자가 구체 UI 구현 없이 경고 팝업 표시를 요청하게 한다.
/// </summary>
public static class WarningPopupPlayback
{
    private static IWarningPopupBackend backend;

    public static void RegisterBackend(IWarningPopupBackend warningPopupBackend)
    {
        backend = warningPopupBackend;
    }

    public static bool Show(WarningPopupCode code)
    {
        if (code == WarningPopupCode.None)
            return false;

        if (backend == null)
            return false;

        backend.ShowWarning(WarningPopupRequest.FromCode(code));
        return true;
    }

    public static bool Show(WarningPopupCode code, float duration)
    {
        if (code == WarningPopupCode.None)
            return false;

        if (backend == null)
            return false;

        backend.ShowWarning(WarningPopupRequest.FromCode(code, duration));
        return true;
    }

    public static bool ShowMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;

        if (backend == null)
            return false;

        backend.ShowWarning(WarningPopupRequest.FromMessage(message));
        return true;
    }

    public static bool ShowMessage(string message, float duration)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;

        if (backend == null)
            return false;

        backend.ShowWarning(WarningPopupRequest.FromMessage(message, duration));
        return true;
    }
}
