using UnityEngine;

/// <summary>
/// 책임:
/// - 몬스터가 소속된 방의 월드 경계를 제공한다.
/// - 특정 월드 좌표가 이 방 안에 포함되는지 판단한다.
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

    /// <summary>지정한 월드 좌표가 이 방 경계 안에 있는지 반환합니다.</summary>
    public bool Contains(Vector2 worldPosition)
    {
        if (areaCollider == null)
            return false;

        return areaCollider.OverlapPoint(worldPosition);
    }

    /// <summary>방 경계 콜라이더를 외부에 제공합니다.</summary>
    public Collider2D AreaCollider => areaCollider;
}
