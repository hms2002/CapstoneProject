using UnityEngine;

/// <summary>
/// 책임:
/// - 몬스터 속성 게이지 월드 UI가 따라갈 전용 기준점을 제공한다.
/// - 명시 Transform이 없으면 몬스터 root 기준 fallback offset으로 안정적인 게이지 위치를 제공한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class MonsterElementGaugeAnchor2D : MonoBehaviour
{
    [SerializeField] private Transform anchor;
    [SerializeField] private Vector2 fallbackOffset = new(0f, -1f);

    public Vector3 Resolve()
    {
        if (anchor != null)
            return anchor.position;

        return transform.position + (Vector3)fallbackOffset;
    }
}
