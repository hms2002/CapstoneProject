using UnityEngine.Events;

/// <summary>
/// 책임 : 튜토리얼 보스 컷씬 로직이 구체 HP UI view 없이 presentation HP를 갱신하고 depletion 이벤트를 구독하게 하는 계약이다.
/// </summary>
public interface ITutorialPresentationHpView
{
    int CurrentHp { get; }
    UnityEvent OnDepleted { get; }
    void ResetToMax();
    void SetVisible(bool visible);
    void ReduceOne();
    void Refresh();
}
