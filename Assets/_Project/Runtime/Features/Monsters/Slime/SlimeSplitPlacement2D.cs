using System.Collections.Generic;
using UnityEngine;

/// <summary>Resolves a new split child's safe ground start and landing using its actual physical bodies, not a point probe.</summary>
public sealed class SlimeSplitPlacement2D
{
    private readonly Transform root;
    private readonly Transform parentToIgnore;
    private readonly Rigidbody2D rigidbody;
    private readonly List<Collider2D> bodies = new();
    private readonly List<Collider2D> overlaps = new();
    private readonly List<RaycastHit2D> hits = new();
    private readonly ContactFilter2D filter;
    private readonly MonsterRoomArea2D room;
    private readonly float skin;
    private readonly int iterations;

    public SlimeSplitPlacement2D(GameObject child, Transform parentToIgnore, LayerMask layers,
        float skin, int iterations, MonsterRoomArea2D room)
    {
        root = child.transform;
        rigidbody = child.GetComponent<Rigidbody2D>();
        this.parentToIgnore = parentToIgnore;
        this.room = room;
        this.skin = Mathf.Max(0.02f, skin);
        this.iterations = Mathf.Max(4, iterations);
        filter = new ContactFilter2D { useLayerMask = true, layerMask = layers, useTriggers = false };
        foreach (Collider2D collider in child.GetComponentsInChildren<Collider2D>())
            if (collider.enabled && !collider.isTrigger && collider.attachedRigidbody == rigidbody)
                bodies.Add(collider);
    }

    public bool TryResolve(Vector2 origin, Vector2 direction, float spread, out Vector2 start, out Vector2 landing)
    {
        start = landing = origin;
        if (bodies.Count == 0) return false;
        SetPosition(origin);
        float maximumCorrection = 1f;
        foreach (Collider2D body in bodies) maximumCorrection = Mathf.Max(maximumCorrection, body.bounds.size.magnitude);

        for (int pass = 0; pass < iterations; pass++)
        {
            bool corrected = false;
            foreach (Collider2D body in bodies)
            {
                Physics2D.OverlapBox(body.bounds.center, (Vector2)body.bounds.size + Vector2.one * (skin * 2f),
                    0f, filter, overlaps);
                foreach (Collider2D obstacle in overlaps)
                {
                    if (Ignore(obstacle)) continue;
                    ColliderDistance2D distance = body.Distance(obstacle);
                    if (!distance.isValid || distance.distance >= skin) continue;
                    Vector2 correction = distance.normal * (distance.distance - skin);
                    if (!float.IsFinite(correction.x) || !float.IsFinite(correction.y)) return false;
                    Vector2 candidate = (Vector2)root.position + correction;
                    if (Vector2.Distance(candidate, origin) > maximumCorrection) return false;
                    SetPosition(candidate);
                    corrected = true;
                }
            }
            if (!corrected) break;
        }

        if (!IsClear()) return false;
        start = landing = root.position;
        Vector2 forward = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        // Try the intended spread first, then alternatives on the same side of obstacles.
        for (int sample = 0; sample < 8 && spread > 0f; sample++)
        {
            float angle = sample == 0 ? 0f : ((sample + 1) / 2) * 45f * (sample % 2 == 1 ? 1f : -1f);
            Vector2 travel = Quaternion.Euler(0f, 0f, angle) * forward;
            SetPosition(start);
            float allowedDistance = spread;
            foreach (Collider2D body in bodies)
            {
                body.Cast(travel, filter, hits, spread + skin);
                foreach (RaycastHit2D hit in hits)
                    if (!Ignore(hit.collider)) allowedDistance = Mathf.Min(allowedDistance, Mathf.Max(0f, hit.distance - skin));
            }
            if (allowedDistance < 0.02f) continue;
            SetPosition(start + travel * allowedDistance);
            if (!IsClear()) continue;
            landing = root.position;
            SetPosition(start);
            return true;
        }
        SetPosition(start);
        return true; // A verified safe start is preferable to the old unchecked parent-position fallback.
    }

    private bool IsClear()
    {
        foreach (Collider2D body in bodies)
        {
            if (room != null && !room.Contains(body.bounds)) return false;
            Physics2D.OverlapBox(body.bounds.center, (Vector2)body.bounds.size + Vector2.one * skin,
                0f, filter, overlaps);
            foreach (Collider2D obstacle in overlaps)
            {
                if (Ignore(obstacle)) continue;
                ColliderDistance2D separation = body.Distance(obstacle);
                if (!separation.isValid || separation.distance < skin * 0.5f) return false;
            }
        }
        return true;
    }

    private bool Ignore(Collider2D collider) => collider == null || collider.transform.IsChildOf(root) ||
        (parentToIgnore != null && collider.transform.IsChildOf(parentToIgnore));

    private void SetPosition(Vector2 position)
    {
        root.position = new Vector3(position.x, position.y, root.position.z);
        if (rigidbody != null) rigidbody.position = position;
        Physics2D.SyncTransforms();
    }
}
