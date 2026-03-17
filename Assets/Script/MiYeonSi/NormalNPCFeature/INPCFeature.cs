using System;

// NPC의 특수 기능들이 공통으로 가져야 할 규격서 (인터페이스)
public interface INPCFeature
{
    // 이 기능의 이름 (예: "Upgrade", "Shop")
    string FeatureName { get; }

    // 기능을 실행하는 함수. (실행이 다 끝나면 onComplete 콜백을 불러주기로 약속함)
    void Execute(Action onComplete);
}