using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

public sealed class FloweringDashSlashHitboxSpawnContext : AttackSpawnContext
{
    public Vector2 worldPosition;
    public Vector2 hitboxSize = Vector2.one;
    public float rotationDegrees;
    public Vector2 lineOfSightSource;
    public bool hitOncePerTarget = true;
}

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class FloweringDashSlashHitboxActor : AttackBase
{
    private readonly HashSet<int> hitTargetIds = new();
    private BoxCollider2D boxCollider;
    private Vector2 lineOfSightSource;
    private Vector2 hitboxSize = Vector2.one;
    private float rotationDegrees;
    private bool hitOncePerTarget = true;

    public void Setup(FloweringDashSlashHitboxSpawnContext context)
    {
        if (context == null)
        {
            Debug.LogError("[FloweringDashSlashHitboxActor] context is null.", this);
            enabled = false;
            return;
        }

        boxCollider = GetComponent<BoxCollider2D>();
        if (boxCollider == null)
            boxCollider = gameObject.AddComponent<BoxCollider2D>();

        hitboxSize = new Vector2(
            Mathf.Max(0.01f, context.hitboxSize.x),
            Mathf.Max(0.01f, context.hitboxSize.y));
        rotationDegrees = context.rotationDegrees;
        lineOfSightSource = context.lineOfSightSource;
        hitOncePerTarget = context.hitOncePerTarget;
        hitTargetIds.Clear();

        transform.position = context.worldPosition;
        transform.rotation = Quaternion.Euler(0f, 0f, rotationDegrees);
        boxCollider.isTrigger = true;
        boxCollider.size = hitboxSize;

        SetupBase(context);
        PerformOverlapScan();
    }

    protected override void TickAttack(float deltaTime)
    {
        PerformOverlapScan();
    }

    protected override bool CanHitTarget(GameObject target)
    {
        if (!hitOncePerTarget || target == null)
            return true;

        return hitTargetIds.Add(target.GetInstanceID());
    }

    protected override void OnHitTarget(GameObject target, Collider2D hitCollider)
    {
    }

    private void PerformOverlapScan()
    {
        Collider2D[] results = Physics2D.OverlapBoxAll(transform.position, hitboxSize, rotationDegrees);
        if (results == null || results.Length == 0)
            return;

        for (int i = 0; i < results.Length; i++)
        {
            Collider2D other = results[i];
            if (other == null)
                continue;

            int colliderLayerBit = 1 << other.gameObject.layer;
            if ((WallLayers.value & colliderLayerBit) != 0)
                continue;

            GameObject targetRoot = CombatTargetResolver2D.ResolveDamageTarget(other);
            if (targetRoot == null || IsIgnoredTarget(targetRoot))
                continue;

            if (!MatchesDamageLayer(other, targetRoot))
                continue;

            if (!HasLineOfSight(targetRoot, other))
                continue;

            if (!CanHitTarget(targetRoot))
                continue;

            if (!TryApplyHit(targetRoot, other))
                continue;

            OnHitTarget(targetRoot, other);
        }
    }

    private bool MatchesDamageLayer(Collider2D hitCollider, GameObject targetRoot)
    {
        if (DamageLayers.value == 0)
            return true;

        int colliderLayerBit = hitCollider != null ? 1 << hitCollider.gameObject.layer : 0;
        int targetLayerBit = targetRoot != null ? 1 << targetRoot.layer : 0;
        return (DamageLayers.value & colliderLayerBit) != 0
            || (DamageLayers.value & targetLayerBit) != 0;
    }

    private bool HasLineOfSight(GameObject targetRoot, Collider2D hitCollider)
    {
        if (WallLayers.value == 0 || targetRoot == null)
            return true;

        Vector2 targetPoint = targetRoot.transform.position;
        if (hitCollider != null)
            targetPoint = hitCollider.ClosestPoint(lineOfSightSource);

        if ((targetPoint - lineOfSightSource).sqrMagnitude <= 0.0001f)
            return true;

        RaycastHit2D hit = Physics2D.Linecast(lineOfSightSource, targetPoint, WallLayers);
        return hit.collider == null;
    }

    private bool IsIgnoredTarget(GameObject targetRoot)
    {
        if (targetRoot == null || IgnoreTarget == null)
            return false;

        if (targetRoot == IgnoreTarget)
            return true;

        Transform targetTransform = targetRoot.transform;
        Transform ignoreTransform = IgnoreTarget.transform;
        return targetTransform.IsChildOf(ignoreTransform) || ignoreTransform.IsChildOf(targetTransform);
    }
}
