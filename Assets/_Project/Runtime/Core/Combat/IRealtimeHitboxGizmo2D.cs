using UnityEngine;

/// <summary>
/// 책임 : Gameplay 공격 로직이 구체 hitbox gizmo 구현 없이 디버그용 hitbox 기록을 요청하게 하는 계약이다.
/// </summary>
public interface IRealtimeHitboxGizmo2D
{
    void RecordBox(Vector2 center, Vector2 size, float angleDeg, float duration, Color color);
}
