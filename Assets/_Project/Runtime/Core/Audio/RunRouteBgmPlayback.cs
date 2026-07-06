/// <summary>
/// 책임 : Gameplay 계층이 구체 런 경로 BGM 서비스 없이 전투/씬 흐름 BGM 이벤트를 알리게 하는 계약이다.
/// </summary>
public interface IRunRouteBgmBackend
{
    void NotifyBossCombatStarted();
}

/// <summary>
/// 책임 : Gameplay/Core 호출자가 Infrastructure BGM 서비스 타입을 직접 참조하지 않고 런 경로 BGM 이벤트를 전달하게 한다.
/// </summary>
public static class RunRouteBgmPlayback
{
    private static IRunRouteBgmBackend backend;

    public static void RegisterBackend(IRunRouteBgmBackend bgmBackend)
    {
        backend = bgmBackend;
    }

    public static void NotifyBossCombatStarted()
    {
        backend?.NotifyBossCombatStarted();
    }
}
