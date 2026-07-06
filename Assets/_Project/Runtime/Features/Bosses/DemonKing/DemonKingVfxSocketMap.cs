using System;
using UnityEngine;

/// <summary>
/// 책임: 데몬킹 VFX가 참조하는 논리 소켓 식별자를 정의한다.
/// </summary>
public enum DemonKingVfxSocketId
{
    EyeFlash,
    HandCast,
    HandCounterImpact,
    SwordCounterOrigin,
    SwordStabOrigin,
    FootLandingImpact,
    ChargeLoop,
    ChargeDisappear,
    HomingStockCenter,
    SwordThrowOrigin,
    SwordThrowReturnOrigin,
    SwordThrowEffectOrigin,
    EyeFlashSecondary,
    SwordSlashOrigin
}

/// <summary>
/// 책임: 데몬킹 VFX 소켓 하나의 anchor, 좌우 반전 offset, 에디터 표시 설정을 보관한다.
/// </summary>
[Serializable]
public sealed class DemonKingVfxSocketEntry
{
    [SerializeField] private DemonKingVfxSocketId id;
    [SerializeField] private bool enabled = true;
    [SerializeField] private Transform anchor;
    [SerializeField] private Vector2 leftFacingLocalOffset;
    [SerializeField] private bool mirrorXByFacing = true;
    [SerializeField] private Color gizmoColor = Color.white;
    [SerializeField, Min(0.01f)] private float gizmoRadius = 0.12f;

    public DemonKingVfxSocketId Id => id;
    public bool Enabled => enabled;
    public Transform Anchor => anchor;
    public Vector2 LeftFacingLocalOffset => leftFacingLocalOffset;
    public bool MirrorXByFacing => mirrorXByFacing;
    public Color GizmoColor => gizmoColor;
    public float GizmoRadius => gizmoRadius;

    public DemonKingVfxSocketEntry()
    {
    }

    public DemonKingVfxSocketEntry(
        DemonKingVfxSocketId id,
        Vector2 leftFacingLocalOffset,
        Color gizmoColor,
        float gizmoRadius = 0.12f)
    {
        this.id = id;
        this.leftFacingLocalOffset = leftFacingLocalOffset;
        this.gizmoColor = gizmoColor;
        this.gizmoRadius = gizmoRadius;
    }
}

/// <summary>
/// 책임: 데몬킹 본체 기준 VFX 소켓 위치를 해석하고 에디터 gizmo로 표시한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class DemonKingVfxSocketMap : MonoBehaviour
{
    [Header("Facing")]
    [SerializeField] private Transform referenceTransform;
    [SerializeField] private SpriteRenderer facingSprite;

    [Header("Gizmos")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private bool showOnlyWhenSelected = true;
    [SerializeField] private bool showLabels = true;
    [SerializeField, Min(0.01f)] private float fallbackGizmoRadius = 0.12f;

    [Header("Sockets")]
    [SerializeField] private DemonKingVfxSocketEntry[] sockets =
    {
        new(DemonKingVfxSocketId.EyeFlash, new Vector2(0f, 0.75f), new Color(1f, 0.2f, 0.2f, 1f)),
        new(DemonKingVfxSocketId.HandCast, new Vector2(0f, 0.55f), new Color(0.35f, 0.75f, 1f, 1f)),
        new(DemonKingVfxSocketId.HandCounterImpact, Vector2.zero, new Color(1f, 0.45f, 0.1f, 1f), 0.16f),
        new(DemonKingVfxSocketId.SwordCounterOrigin, Vector2.zero, new Color(1f, 0.9f, 0.15f, 1f), 0.14f),
        new(DemonKingVfxSocketId.SwordStabOrigin, Vector2.zero, new Color(1f, 0.65f, 0.1f, 1f), 0.14f),
        new(DemonKingVfxSocketId.FootLandingImpact, Vector2.zero, new Color(0.25f, 1f, 0.45f, 1f), 0.16f),
        new(DemonKingVfxSocketId.ChargeLoop, Vector2.zero, new Color(0.85f, 0.35f, 1f, 1f), 0.14f),
        new(DemonKingVfxSocketId.ChargeDisappear, Vector2.zero, new Color(0.65f, 0.25f, 1f, 1f), 0.14f),
        new(DemonKingVfxSocketId.HomingStockCenter, new Vector2(0f, 1.6f), new Color(0.25f, 0.85f, 1f, 1f), 0.14f),
        new(DemonKingVfxSocketId.SwordThrowOrigin, new Vector2(-0.35f, 0.45f), new Color(1f, 0.75f, 0.25f, 1f), 0.14f),
        new(DemonKingVfxSocketId.SwordThrowReturnOrigin, new Vector2(-0.25f, 0.55f), new Color(1f, 0.85f, 0.35f, 1f), 0.14f),
        new(DemonKingVfxSocketId.SwordThrowEffectOrigin, new Vector2(-0.35f, 0.45f), new Color(0.35f, 0.95f, 1f, 1f), 0.14f),
        new(DemonKingVfxSocketId.EyeFlashSecondary, new Vector2(-0.15f, 0.75f), new Color(1f, 0.3f, 0.35f, 1f), 0.12f),
        new(DemonKingVfxSocketId.SwordSlashOrigin, Vector2.zero, new Color(1f, 0.55f, 0.15f, 1f), 0.14f),
    };

    public Vector3 ResolveLocalOffset(
        DemonKingVfxSocketId id,
        Vector2 fallbackLeftFacingLocalOffset,
        bool ownerIsFacingLeft)
    {
        if (TryGetEntry(id, out DemonKingVfxSocketEntry entry))
            return ResolveEntryLocalOffset(entry, fallbackLeftFacingLocalOffset, ownerIsFacingLeft);

        return MirrorLeftFacingOffset(fallbackLeftFacingLocalOffset, ownerIsFacingLeft, mirrorX: true);
    }

    public Vector3 ResolveWorldPosition(
        DemonKingVfxSocketId id,
        Vector2 fallbackLeftFacingLocalOffset,
        bool ownerIsFacingLeft)
    {
        Vector3 localOffset = ResolveLocalOffset(id, fallbackLeftFacingLocalOffset, ownerIsFacingLeft);
        return ResolveReferenceTransform().TransformPoint(localOffset);
    }

    public Vector3 ResolveWorldPositionAt(
        DemonKingVfxSocketId id,
        Vector2 baseWorldPosition,
        Vector2 fallbackLeftFacingLocalOffset,
        bool ownerIsFacingLeft)
    {
        Vector3 localOffset = ResolveLocalOffset(id, fallbackLeftFacingLocalOffset, ownerIsFacingLeft);
        Transform reference = ResolveReferenceTransform();
        Vector3 worldOffset = reference.TransformVector(localOffset);
        return new Vector3(baseWorldPosition.x + worldOffset.x, baseWorldPosition.y + worldOffset.y, reference.position.z + worldOffset.z);
    }

    public bool TryGetEntry(DemonKingVfxSocketId id, out DemonKingVfxSocketEntry entry)
    {
        if (sockets != null)
        {
            for (int i = 0; i < sockets.Length; i++)
            {
                DemonKingVfxSocketEntry candidate = sockets[i];
                if (candidate != null && candidate.Enabled && candidate.Id == id)
                {
                    entry = candidate;
                    return true;
                }
            }
        }

        entry = null;
        return false;
    }

    private Vector3 ResolveEntryLocalOffset(
        DemonKingVfxSocketEntry entry,
        Vector2 fallbackLeftFacingLocalOffset,
        bool ownerIsFacingLeft)
    {
        Transform reference = ResolveReferenceTransform();
        Vector2 leftOffset = entry.Anchor != null
            ? (Vector2)reference.InverseTransformPoint(entry.Anchor.position)
            : entry.LeftFacingLocalOffset;

        return MirrorLeftFacingOffset(leftOffset, ownerIsFacingLeft, entry.MirrorXByFacing);
    }

    private Transform ResolveReferenceTransform()
    {
        if (referenceTransform != null)
            return referenceTransform;

        DemonKingController controller = GetComponentInParent<DemonKingController>();
        if (controller != null)
            return controller.transform;

        return transform;
    }

    private static Vector3 MirrorLeftFacingOffset(Vector2 leftOffset, bool ownerIsFacingLeft, bool mirrorX)
    {
        Vector3 offset = leftOffset;
        if (mirrorX && !ownerIsFacingLeft)
            offset.x = -offset.x;

        return offset;
    }

    private bool ResolveFacingLeft()
    {
        DemonKingController controller = GetComponentInParent<DemonKingController>();
        if (controller != null)
            return controller.FacingDirection.x < 0f;

        SpriteRenderer resolvedSprite = facingSprite;
        if (resolvedSprite == null)
            resolvedSprite = GetComponentInParent<SpriteRenderer>();

        return resolvedSprite == null || !resolvedSprite.flipX;
    }

    private void OnDrawGizmos()
    {
        if (showOnlyWhenSelected)
            return;

        DrawSocketGizmos();
    }

    private void OnDrawGizmosSelected()
    {
        DrawSocketGizmos();
    }

    private void DrawSocketGizmos()
    {
        if (!showGizmos || sockets == null)
            return;

        bool facingLeft = ResolveFacingLeft();
        Transform reference = ResolveReferenceTransform();
        for (int i = 0; i < sockets.Length; i++)
        {
            DemonKingVfxSocketEntry entry = sockets[i];
            if (entry == null || !entry.Enabled)
                continue;

            Vector3 worldPosition = ResolveWorldPosition(entry.Id, entry.LeftFacingLocalOffset, facingLeft);
            float radius = Mathf.Max(0.01f, entry.GizmoRadius > 0f ? entry.GizmoRadius : fallbackGizmoRadius);
            Gizmos.color = entry.GizmoColor;
            Gizmos.DrawLine(reference.position, worldPosition);
            Gizmos.DrawWireSphere(worldPosition, radius);

#if UNITY_EDITOR
            if (showLabels)
                EditorAuthoringPlayback.DrawHandleLabel(
                    worldPosition + Vector3.up * (radius + 0.05f),
                    entry.Id.ToString(),
                    entry.GizmoColor);
#endif
        }
    }
}
