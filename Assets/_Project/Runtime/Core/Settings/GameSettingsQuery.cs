/// <summary>
/// 책임: Core/Presentation 호출자가 구체 GameSettingsService 구현 없이 게임 설정 상태를 조회하게 하는 backend 계약이다.
/// </summary>
public interface IGameSettingsBackend
{
    bool IsScreenShakeEnabled();
}

/// <summary>
/// 책임: Core와 Presentation 계층이 UI 설정 서비스에 직접 의존하지 않고 필요한 게임 설정 값을 조회하게 한다.
/// </summary>
public static class GameSettingsQuery
{
    private static IGameSettingsBackend backend;

    public static void RegisterBackend(IGameSettingsBackend settingsBackend)
    {
        backend = settingsBackend;
    }

    public static bool IsScreenShakeEnabled()
    {
        return backend == null || backend.IsScreenShakeEnabled();
    }
}
