using UnityEngine;

/// <summary>
/// 책임 :
/// - 과거 전역 bootstrap 진입점과의 컴파일 호환성을 잠시 유지하는 빈 호환 셸이다.
/// - 실제 Player 상태 HUD source 부착 책임은 이제 PlayerStatusRuntime이 직접 소유하며, 이 클래스는 더 이상 런타임 초기화에 관여하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerStatusHudBootstrap : MonoBehaviour
{
}
