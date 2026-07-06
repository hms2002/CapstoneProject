using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - CombatHeightState2D의 시각 높이 표현 컴포넌트가 외부에 제공해야 하는 최소 조회/동기화 계약을 정의한다.
    /// - Gameplay 이동/로깅 로직이 concrete height presentation 구현을 직접 참조하지 않게 한다.
    /// </summary>
    public interface ICombatHeightPresentation2D
    {
        Transform VisualRoot { get; }
        float CurrentVisualHeight { get; }
        Vector3 VisualBaseLocalPosition { get; }
        void SnapToCurrentState();
    }
}
