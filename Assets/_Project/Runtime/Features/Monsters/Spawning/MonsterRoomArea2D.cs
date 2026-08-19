using UnityEngine;

/// <summary>
/// 책임:
/// - 몬스터가 소속된 방의 월드 경계를 제공한다.
/// - 특정 월드 좌표나 물리 몸체 bounds 전체가 이 방 안에 포함되는지 판단한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class MonsterRoomArea2D : MonoBehaviour
{
    [SerializeField] private Collider2D areaCollider;

    private void Awake()
    {
        if (areaCollider == null)
            areaCollider = GetComponent<Collider2D>();
    }

    /// <summary>런타임 생성 방이 사용할 경계 콜라이더를 명시적으로 연결합니다.</summary>
    public void Configure(Collider2D roomAreaCollider)
    {
        areaCollider = roomAreaCollider;
    }

    /// <summary>지정한 월드 좌표가 이 방 경계 안에 있는지 반환합니다.</summary>
    public bool Contains(Vector2 worldPosition)
    {
        if (areaCollider == null)
            return false;

        return areaCollider.OverlapPoint(worldPosition);
    }

    /// <summary>지정한 월드 bounds의 모서리와 변 중앙, 중심이 모두 방 경계 안에 있는지 반환합니다.</summary>
    public bool Contains(Bounds worldBounds)
    {
        if (areaCollider == null)
            return false;

        Vector2 min = new(worldBounds.min.x, worldBounds.min.y);
        Vector2 max = new(worldBounds.max.x, worldBounds.max.y);
        Vector2 center = new(worldBounds.center.x, worldBounds.center.y);
        return areaCollider.OverlapPoint(new Vector2(min.x, min.y)) &&
               areaCollider.OverlapPoint(new Vector2(min.x, max.y)) &&
               areaCollider.OverlapPoint(new Vector2(max.x, min.y)) &&
               areaCollider.OverlapPoint(new Vector2(max.x, max.y)) &&
               areaCollider.OverlapPoint(new Vector2(center.x, min.y)) &&
               areaCollider.OverlapPoint(new Vector2(center.x, max.y)) &&
               areaCollider.OverlapPoint(new Vector2(min.x, center.y)) &&
               areaCollider.OverlapPoint(new Vector2(max.x, center.y)) &&
               areaCollider.OverlapPoint(center);
    }

    /// <summary>방 경계 콜라이더를 외부에 제공합니다.</summary>
    public Collider2D AreaCollider => areaCollider;
}
