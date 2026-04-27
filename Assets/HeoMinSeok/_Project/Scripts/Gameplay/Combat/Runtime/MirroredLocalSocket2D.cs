using UnityEngine;

/// <summary>
/// 책임:
/// 기준 Transform의 로컬 오프셋을 월드 소켓 위치로 변환하고, SpriteRenderer.flipX 상태에 따라 X 오프셋을 반전한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class MirroredLocalSocket2D : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform referenceTransform;
    [SerializeField] private SpriteRenderer facingSprite;

    [Header("Offset")]
    [SerializeField] private bool useTransformPositionAsRightFacingOffset = true;
    [SerializeField] private Vector2 rightFacingLocalOffset = Vector2.zero;
    [SerializeField] private bool mirrorXBySpriteFlip = true;

    public Vector3 WorldPosition => ResolveWorldPosition();

    /// <summary>현재 facing 상태를 기준으로 계산한 월드 소켓 위치를 반환한다.</summary>
    public Vector3 ResolveWorldPosition()
    {
        Transform reference = ResolveReferenceTransform();
        Vector2 offset = useTransformPositionAsRightFacingOffset
            ? (Vector2)reference.InverseTransformPoint(transform.position) + rightFacingLocalOffset
            : rightFacingLocalOffset;

        if (mirrorXBySpriteFlip && facingSprite != null && facingSprite.flipX)
            offset.x *= -1f;

        return reference.TransformPoint(offset);
    }

    private Transform ResolveReferenceTransform()
    {
        if (referenceTransform != null && referenceTransform != transform)
            return referenceTransform;

        if (transform.parent != null)
            return transform.parent;

        return referenceTransform != null ? referenceTransform : transform;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(ResolveWorldPosition(), 0.08f);
    }
#endif
}
