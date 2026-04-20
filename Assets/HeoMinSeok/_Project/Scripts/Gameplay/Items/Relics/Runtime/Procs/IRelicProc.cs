using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 유물이 런타임 중 반응해야 하는 이벤트 처리, 시간 경과 갱신, 정리를 공통 규약으로 제공한다.
/// - RelicProcManager가 각 유물 proc를 같은 방식으로 다룰 수 있게 만드는 최소 실행 인터페이스다.
/// </summary>
public interface IRelicProc
{
    Object Token { get; }
    void Handle(GameplayTag tag, AbilityEventData data);
    void Tick(float deltaTime);
    void Dispose();
}
