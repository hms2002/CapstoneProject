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
