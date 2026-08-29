/// <summary>
/// 책임 : 이동 요청이 상호작용과 자동 트리거 중 어느 매개체에서 시작됐는지 실행 계층에 전달한다.
/// </summary>
public enum SceneTravelActivationKind
{
    Interaction = 0,
    Trigger = 1
}

/// <summary>
/// 책임 : Gameplay 이동 매개체가 구체 Infrastructure 구현 없이 공통 씬 연결 이동을 요청하게 하는 backend 계약이다.
/// </summary>
public interface ISceneTravelBackend
{
    bool TryTravel(
        SceneTravelEndpoint endpoint,
        IPlayerInteractor player,
        SceneTravelActivationKind activationKind);
}

/// <summary>
/// 책임 : 상호작용·트리거 이동 매개체가 같은 씬 연결 실행 backend를 사용하도록 정적 관문을 제공한다.
/// </summary>
public static class SceneTravelPlayback
{
    private static ISceneTravelBackend backend;

    public static void RegisterBackend(ISceneTravelBackend travelBackend)
    {
        backend = travelBackend;
    }

    public static void UnregisterBackend(ISceneTravelBackend travelBackend)
    {
        if (ReferenceEquals(backend, travelBackend))
            backend = null;
    }

    public static bool TryTravel(
        SceneTravelEndpoint endpoint,
        IPlayerInteractor player,
        SceneTravelActivationKind activationKind)
    {
        return backend != null && backend.TryTravel(endpoint, player, activationKind);
    }
}
