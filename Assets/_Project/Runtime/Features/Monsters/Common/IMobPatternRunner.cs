/// <summary>
/// 책임 :
/// - 일반 몬스터 패턴 실행기가 현재 실행 중인지와 강제 취소 가능 여부를 공통 계약으로 제공한다.
/// - MobAbilityCoordinator가 개별 패턴 실행기를 구체 타입 없이 추적하고 정리할 수 있게 한다.
/// </summary>
public interface IMobPatternRunner
{
    bool IsRunning { get; }
    void Cancel();
}

/// <summary>
/// 책임 :
/// - 일반 몬스터의 suppression / death / disable 같은 전역 종료 경로에서 정리해야 하는 presentation 자원을 공통 계약으로 제공한다.
/// - 전투 객체가 개별 runner나 helper의 구체 구현을 몰라도 경고, 마스크, 오버레이 같은 시각 자원을 일괄 정리하게 한다.
/// </summary>
public interface IMobPresentationCleanup
{
    void CleanupPresentation();
}
