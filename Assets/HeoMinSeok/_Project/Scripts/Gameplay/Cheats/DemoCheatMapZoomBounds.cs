using UnityEngine;

[DisallowMultipleComponent]
public sealed class DemoCheatMapZoomBounds : MonoBehaviour
{
    [SerializeField] private Vector2 size = new Vector2(40f, 24f);
    [SerializeField] private BoxCollider2D boundsCollider;
    [SerializeField] private bool preferBoxCollider = true;
    [SerializeField, Min(0f)] private float extraPadding;
    [SerializeField] private Color gizmoColor = new Color(0.25f, 0.85f, 1f, 0.85f);

    public bool TryGetZoomBounds(out Vector2 center, out Vector2 boundsSize, out float padding)
    {
        if (TryGetColliderBounds(out Bounds colliderBounds))
        {
            center = colliderBounds.center;
            boundsSize = new Vector2(
                Mathf.Max(0f, colliderBounds.size.x),
                Mathf.Max(0f, colliderBounds.size.y));
        }
        else
        {
            center = transform.position;
            boundsSize = new Vector2(
                Mathf.Abs(size.x),
                Mathf.Abs(size.y));
        }

        padding = Mathf.Max(0f, extraPadding);
        return boundsSize.x > 0f && boundsSize.y > 0f;
    }

    private bool TryGetColliderBounds(out Bounds bounds)
    {
        bounds = default;
        if (!preferBoxCollider)
            return false;

        BoxCollider2D collider = boundsCollider != null ? boundsCollider : GetComponent<BoxCollider2D>();
        if (collider == null)
            return false;

        bounds = collider.bounds;
        return bounds.size.x > 0f && bounds.size.y > 0f;
    }

    private void Reset()
    {
        boundsCollider = GetComponent<BoxCollider2D>();
    }

    private void OnValidate()
    {
        size.x = Mathf.Max(0.01f, Mathf.Abs(size.x));
        size.y = Mathf.Max(0.01f, Mathf.Abs(size.y));

        if (boundsCollider == null)
            boundsCollider = GetComponent<BoxCollider2D>();
    }

    private void OnDrawGizmos()
    {
        if (!TryGetZoomBounds(out Vector2 center, out Vector2 boundsSize, out _))
            return;

        Gizmos.color = gizmoColor;
        Gizmos.DrawWireCube(center, new Vector3(boundsSize.x, boundsSize.y, 0f));
        const float CrossHalfSize = 0.35f;
        Gizmos.DrawLine(
            new Vector3(center.x - CrossHalfSize, center.y, 0f),
            new Vector3(center.x + CrossHalfSize, center.y, 0f));
        Gizmos.DrawLine(
            new Vector3(center.x, center.y - CrossHalfSize, 0f),
            new Vector3(center.x, center.y + CrossHalfSize, 0f));
    }
}
