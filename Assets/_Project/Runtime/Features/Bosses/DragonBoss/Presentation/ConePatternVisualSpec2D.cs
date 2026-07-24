using UnityEngine;

/// <summary>
/// 책임:
/// 부채꼴/원뿔형 패턴 연출에 필요한 위치, 방향, 범위, 각도, 지속시간 정보를 전달한다.
/// </summary>
public readonly struct ConePatternVisualSpec2D
{
    public ConePatternVisualSpec2D(
        Vector2 origin,
        Vector2 direction,
        float range,
        float angleDegrees,
        float durationSeconds)
    {
        Origin = origin;
        Direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        Range = Mathf.Max(0.01f, range);
        AngleDegrees = Mathf.Clamp(angleDegrees, 1f, 180f);
        DurationSeconds = Mathf.Max(0f, durationSeconds);
    }

    public Vector2 Origin { get; }
    public Vector2 Direction { get; }
    public float Range { get; }
    public float AngleDegrees { get; }
    public float DurationSeconds { get; }
    public float RotationDegrees => Mathf.Atan2(Direction.y, Direction.x) * Mathf.Rad2Deg;
}
