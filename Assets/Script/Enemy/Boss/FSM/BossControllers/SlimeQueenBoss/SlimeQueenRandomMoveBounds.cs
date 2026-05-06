using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class SlimeQueenRandomMoveBounds : MonoBehaviour
{
    [Header("Bounds")]
    [Tooltip("패턴 2 랜덤 착지 위치를 뽑을 BoxCollider2D입니다.")]
    [SerializeField] private BoxCollider2D boundsCollider;

    [Header("Gizmos")]
    [Tooltip("씬 뷰에서 표시할 바운더리 기즈모 색상입니다.")]
    [SerializeField] private Color gizmoColor = new Color(0.1f, 0.75f, 1f, 0.25f);

    private void Awake()
    {
        CacheBoundsCollider();
    }

    private void OnValidate()
    {
        CacheBoundsCollider();

        if (boundsCollider != null)
            boundsCollider.isTrigger = true;
    }

    private void OnDrawGizmos()
    {
        CacheBoundsCollider();
        if (boundsCollider == null)
            return;

        Bounds bounds = boundsCollider.bounds;
        Gizmos.color = gizmoColor;
        Gizmos.DrawCube(bounds.center, bounds.size);
        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.9f);
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }

    /// <summary>바운더리 안에서 랜덤 월드 좌표를 반환합니다.</summary>
    public bool TryGetRandomPoint(float z, out Vector3 randomPoint)
    {
        CacheBoundsCollider();
        if (boundsCollider == null)
        {
            randomPoint = Vector3.zero;
            return false;
        }

        Bounds bounds = boundsCollider.bounds;
        if (bounds.size.x <= 0f || bounds.size.y <= 0f)
        {
            randomPoint = Vector3.zero;
            return false;
        }

        randomPoint = new Vector3(
            Random.Range(bounds.min.x, bounds.max.x),
            Random.Range(bounds.min.y, bounds.max.y),
            z);
        return true;
    }

    /// <summary>BoxCollider2D 참조를 자동으로 보정합니다.</summary>
    private void CacheBoundsCollider()
    {
        if (boundsCollider == null)
            boundsCollider = GetComponent<BoxCollider2D>();
    }
}
