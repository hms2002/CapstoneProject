using System;

/// <summary>
/// 책임 : 호감도 gameplay 흐름이 concrete UI 구현 없이 현재 호감도와 증가 연출을 요청하는 계약을 제공한다.
/// </summary>
public interface IAffectionPresentationView
{
    bool IsPresentationActive { get; }

    void Setup(int currentAffection);

    void PlayGainAnimation(int prevAffection, int newAffection, Action onComplete);
}
