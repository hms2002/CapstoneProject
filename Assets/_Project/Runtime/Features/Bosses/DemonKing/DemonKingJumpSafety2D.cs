using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Validates body-sized jump destinations and owns temporary airborne physics and ground-position restoration.</summary>
public sealed class DemonKingJumpSafety2D : IDisposable
{
    private const float Skin = 0.04f;
    private readonly Transform root;
    private readonly Rigidbody2D body;
    private readonly List<Collider2D> shapes = new();
    private readonly List<Collider2D> overlaps = new();
    private readonly List<RaycastHit2D> hits = new();
    private readonly ContactFilter2D filter;
    private Vector3 safeStart;
    private Vector3 destination;
    private bool originalSimulation;
    private bool active;

    public DemonKingJumpSafety2D(GameObject owner, LayerMask walls)
    {
        root = owner.transform;
        body = owner.GetComponent<Rigidbody2D>();
        filter = new ContactFilter2D { useLayerMask = true, layerMask = walls, useTriggers = false };
        foreach (Collider2D shape in owner.GetComponentsInChildren<Collider2D>())
            if (shape.enabled && !shape.isTrigger && shape.attachedRigidbody == body) shapes.Add(shape);
    }

    public bool TryResolve(Vector2 requested, out Vector2 start, out Vector2 target)
    {
        Vector3 original = root.position;
        start = target = original;
        if (body == null || !body.simulated || shapes.Count == 0) return false;
        try
        {
            // Repair a shallow initial overlap before casting; never search across an entire wall.
            float limit = 1f;
            foreach (Collider2D shape in shapes) limit = Mathf.Max(limit, shape.bounds.size.magnitude);
            for (int pass = 0; pass < 8; pass++)
            {
                bool moved = false;
                foreach (Collider2D shape in shapes)
                {
                    FindNearby(shape);
                    foreach (Collider2D obstacle in overlaps)
                    {
                        if (Ignore(obstacle)) continue;
                        ColliderDistance2D distance = shape.Distance(obstacle);
                        if (!distance.isValid) return false;
                        if (distance.distance >= Skin) continue;
                        Vector2 position = (Vector2)root.position + distance.normal * (distance.distance - Skin);
                        if (!float.IsFinite(position.x) || !float.IsFinite(position.y) ||
                            Vector2.Distance(original, position) > limit) return false;
                        Move(position);
                        moved = true;
                    }
                }
                if (!moved) break;
            }
            if (!IsClear()) return false;
            start = root.position;
            Vector2 delta = requested - start;
            float travel = delta.magnitude;
            if (travel > 0.0001f)
            {
                foreach (Collider2D shape in shapes)
                {
                    shape.Cast(delta.normalized, filter, hits, travel + Skin);
                    foreach (RaycastHit2D hit in hits)
                        if (!Ignore(hit.collider)) travel = Mathf.Min(travel, Mathf.Max(0f, hit.distance - Skin));
                }
                Move(start + delta.normalized * travel);
            }
            target = IsClear() ? (Vector2)root.position : start;
            safeStart = new Vector3(start.x, start.y, original.z);
            destination = new Vector3(target.x, target.y, original.z);
            return true;
        }
        finally { Move(original); }
    }

    public void Begin()
    {
        originalSimulation = body.simulated;
        active = true;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        body.simulated = false;
        root.position = safeStart;
    }

    public void SetPose(Vector2 ground, float height)
    {
        if (active) root.position = new Vector3(ground.x, ground.y + height, safeStart.z);
    }

    public bool Complete() => Restore(destination);
    public void Dispose() => Restore(safeStart);

    private bool Restore(Vector3 ground)
    {
        if (!active) return false;
        active = false;
        if (root == null || body == null) return false;
        body.simulated = originalSimulation;
        Move(ground);
        // A wall may have changed during flight. Never deal landing damage at an unchecked point.
        bool clear = IsClear();
        if (!clear) Move(safeStart);
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        return clear;
    }

    private void FindNearby(Collider2D shape) => Physics2D.OverlapBox(shape.bounds.center,
        (Vector2)shape.bounds.size + Vector2.one * Skin * 2f, 0f, filter, overlaps);

    private bool IsClear()
    {
        foreach (Collider2D shape in shapes)
        {
            FindNearby(shape);
            foreach (Collider2D obstacle in overlaps)
            {
                if (Ignore(obstacle)) continue;
                ColliderDistance2D distance = shape.Distance(obstacle);
                if (!distance.isValid || distance.distance < Skin * 0.5f) return false;
            }
        }
        return true;
    }

    private bool Ignore(Collider2D other) => other == null || other.transform.IsChildOf(root);

    private void Move(Vector3 position)
    {
        root.position = new Vector3(position.x, position.y, root.position.z);
        if (body != null) body.position = position;
        Physics2D.SyncTransforms();
    }
}
