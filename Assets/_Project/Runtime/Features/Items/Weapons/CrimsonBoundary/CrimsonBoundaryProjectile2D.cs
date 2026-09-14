using UnityEngine;
using UnityGAS;

public sealed class CrimsonBoundaryProjectile2D : AttackBase
{
    [SerializeField] private BoxCollider2D wallCollider;
    [SerializeField] private BoxCollider2D damageCollider;
    private readonly System.Collections.Generic.List<RaycastHit2D> wallHits = new(12);
    private readonly System.Collections.Generic.List<RaycastHit2D> damageHits = new(12);
    private Vector2 direction;
    private float speed;
    private int burnStacks;
    private GameplayEffect burnDamageEffect;
    private CrimsonBoundaryVisual2D hitPrefab;
    private CrimsonBoundaryVisual2D burnPrefab;
    private CrimsonBoundaryVisual2D burnSustainPrefab;
    private CrimsonBoundaryRuntimeState visualOwner;
    private bool impactPlayed;

    public void Setup(ProjectileAttackSpawnContext context, int stacks, GameplayEffect effect,
        CrimsonBoundaryVisual2D hitVisual = null, CrimsonBoundaryVisual2D burnVisual = null,
        CrimsonBoundaryRuntimeState owner = null, CrimsonBoundaryVisual2D burnSustainVisual = null)
    {
        direction = context.direction.sqrMagnitude > 0.0001f ? context.direction.normalized : Vector2.right;
        speed = Mathf.Max(0f, context.speed);
        burnStacks = Mathf.Max(0, stacks);
        burnDamageEffect = effect;
        hitPrefab = hitVisual;
        burnPrefab = burnVisual;
        burnSustainPrefab = burnSustainVisual;
        visualOwner = owner;
        impactPlayed = false;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        SetupBase(context);
    }

    protected override void OnSetupCompleted()
    {
        if (wallCollider == null || damageCollider == null || wallCollider == damageCollider)
        {
            Debug.LogError("Crimson projectile requires separate wall and damage colliders.", this);
            enabled = false;
            DestroySelf();
        }
    }

    // Sweeps own both collision channels and their ordering. The larger damage
    // trigger must never invoke AttackBase's wall-destruction path.
    protected override void OnTriggerEnter2D(Collider2D other) { }
    protected override void OnTriggerStay2D(Collider2D other) { }

    protected override void TickAttack(float deltaTime)
    {
        if (impactPlayed) return;
        Vector2 displacement = direction * speed * deltaTime;
        float distance = displacement.magnitude;
        Sweep(wallCollider, WallLayers, wallHits, distance);
        Sweep(damageCollider, DamageLayers, damageHits, distance);
        RaycastHit2D firstWall = default;
        float wallDistance = float.PositiveInfinity;
        for (int i = 0; i < wallHits.Count; i++)
        {
            RaycastHit2D candidate = wallHits[i];
            if (IsOwnCollider(candidate.collider) || !CanHitWall(candidate.collider.gameObject, candidate.collider)) continue;
            if (candidate.distance >= wallDistance) continue;
            firstWall = candidate;
            wallDistance = candidate.distance;
        }

        damageHits.Sort(RaycastHitDistanceComparer.Instance);
        for (int i = 0; i < damageHits.Count; i++)
        {
            RaycastHit2D candidate = damageHits[i];
            if (candidate.distance >= wallDistance) break;
            Collider2D hit = candidate.collider;
            if (IsOwnCollider(hit) || (WallLayers.value & (1 << hit.gameObject.layer)) != 0) continue;
            GameObject target = CombatTargetResolver2D.ResolveDamageTarget(hit);
            if (target == null || IsIgnoredTarget(target)) continue;
            if ((DamageLayers.value & (1 << target.layer)) == 0) continue;
            if (!CanHitTarget(target)) continue;
            Vector3 startPosition = transform.position;
            transform.position += (Vector3)(direction * candidate.distance);
            if (!TryApplyHit(target, hit))
            {
                transform.position = startPosition;
                continue;
            }
            OnHitTarget(target, hit);
            return;
        }

        if (firstWall.collider != null)
        {
            transform.position += (Vector3)(direction * wallDistance);
            OnHitWall(firstWall.collider.gameObject, firstWall.collider);
            return;
        }
        transform.position += (Vector3)displacement;
    }

    private void Sweep(BoxCollider2D collider, LayerMask layers,
        System.Collections.Generic.List<RaycastHit2D> hits, float distance)
    {
        hits.Clear();
        if (layers.value == 0) return;
        var filter = new ContactFilter2D { useLayerMask = true, layerMask = layers, useTriggers = true };
        // Explicit pose also supports same-frame spawn/rotation before physics sync.
        collider.Cast(collider.transform.position, collider.transform.eulerAngles.z,
            direction, filter, hits, distance, true);
    }

    private bool IsOwnCollider(Collider2D collider) => collider == null ||
        collider == wallCollider || collider == damageCollider || collider.transform.IsChildOf(transform);

    protected override void OnHitTarget(GameObject target, Collider2D hitCollider)
    {
        BurnStatus2D.Apply(target, OwnerSystem, burnDamageEffect, Causer, burnStacks, burnPrefab, burnSustainPrefab);
        PlayImpact();
        base.OnHitTarget(target, hitCollider);
    }

    protected override void OnHitWall(GameObject wall, Collider2D hitCollider)
    {
        PlayImpact();
        base.OnHitWall(wall, hitCollider);
    }

    private void PlayImpact()
    {
        if (impactPlayed) return;
        impactPlayed = true;
        CrimsonBoundaryVisual2D.Spawn(hitPrefab, transform.position, transform.rotation, visualOwner);
    }

    private sealed class RaycastHitDistanceComparer : System.Collections.Generic.IComparer<RaycastHit2D>
    {
        public static readonly RaycastHitDistanceComparer Instance = new();
        public int Compare(RaycastHit2D x, RaycastHit2D y) => x.distance.CompareTo(y.distance);
    }
}
