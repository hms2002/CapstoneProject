using UnityEngine;

/// <summary>
/// 책임 :
/// - Gameplay 스폰/보스 코드가 몬스터 원소 게이지 View 설치와 제거를 요청하는 최소 계약을 제공한다.
/// - 실제 UI View prefab 생성, 바인딩, 정리 구현은 UI 계층에 남긴다.
/// </summary>
public interface IMonsterElementGaugeViewInstaller
{
    Component InstallerComponent { get; }
    void InstallFor(GameObject monster);
    void Uninstall();
}
