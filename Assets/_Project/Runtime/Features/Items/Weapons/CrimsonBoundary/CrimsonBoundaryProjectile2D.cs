using UnityEngine;
using UnityGAS;

public sealed class CrimsonBoundaryProjectile2D : AttackBase
{
    private readonly RaycastHit2D[] sweepHits = new RaycastHit2D[12];
    private Vector2 direction;
    private float speed;
    private int burnStacks;
    private GameplayEffect burnDamageEffect;
    private Collider2D ownCollider;

    public void Setup(ProjectileAttackSpawnContext context, int stacks, GameplayEffect effect)
    {
        direction = context.direction.sqrMagnitude > 0.0001f ? context.direction.normalized : Vector2.right;
        speed = Mathf.Max(0f, context.speed);
        burnStacks = Mathf.Max(0, stacks);
        burnDamageEffect = effect;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        SetupBase(context);
    }

    protected override void OnSetupCompleted() => ownCollider = GetComponent<Collider2D>();

    protected override void TickAttack(float deltaTime)
    {
        Vector2 displacement = direction * speed * deltaTime;
        float distance = displacement.magnitude;
        int mask = WallLayers.value | DamageLayers.value;
        int count = distance > 0f && mask != 0
            ? Physics2D.BoxCastNonAlloc(transform.position, new Vector2(0.28f, 0.28f), transform.eulerAngles.z, direction, sweepHits, distance, mask)
            : 0;

        if (count > 1)
            System.Array.Sort(sweepHits, 0, count, RaycastHitDistanceComparer.Instance);

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = sweepHits[i].collider;
            if (hit == null || hit == ownCollider) continue;
            int bit = 1 << hit.gameObject.layer;
            if ((WallLayers.value & bit) != 0)
            {
                OnHitWall(hit.gameObject, hit);
                return;
            }

            GameObject target = CombatTargetResolver2D.ResolveDamageTarget(hit);
            if (target == null || IsIgnoredTarget(target)) continue;
            if ((DamageLayers.value & (1 << target.layer)) == 0) continue;
            transform.position = sweepHits[i].centroid;
            if (TryApplyHit(target, hit))
                OnHitTarget(target, hit);
            return;
        }

        transform.position += (Vector3)displacement;
    }

    protected override void OnHitTarget(GameObject target, Collider2D hitCollider)
    {
        BurnStatus2D.Apply(target, OwnerSystem, burnDamageEffect, Causer, burnStacks);
        base.OnHitTarget(target, hitCollider);
    }

    private sealed class RaycastHitDistanceComparer : System.Collections.Generic.IComparer<RaycastHit2D>
    {
        public static readonly RaycastHitDistanceComparer Instance = new();
        public int Compare(RaycastHit2D x, RaycastHit2D y) => x.distance.CompareTo(y.distance);
    }
}
