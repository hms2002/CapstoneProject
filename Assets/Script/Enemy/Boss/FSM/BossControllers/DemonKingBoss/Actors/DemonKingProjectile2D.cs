using UnityEngine;
using UnityGAS;

[DisallowMultipleComponent]
public sealed class DemonKingProjectile2D : MonoBehaviour
{
    private DemonKingController owner;
    private Vector2 direction;
    private Transform homingTarget;
    private float speed;
    private float turnRate;
    private float radius;
    private float damage;
    private float lifetime;
    private float elapsed;

    public static DemonKingProjectile2D Spawn(
        DemonKingController owner,
        Vector2 position,
        Vector2 direction,
        Transform homingTarget,
        float speed,
        float turnRate,
        float radius,
        float damage,
        float lifetime)
    {
        GameObject projectileObject = new("DemonKing_MagicProjectile");
        projectileObject.transform.position = position;

        CircleCollider2D collider = projectileObject.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = Mathf.Max(0.05f, radius);

        SpriteRenderer renderer = projectileObject.AddComponent<SpriteRenderer>();
        renderer.sprite = DemonKingPrimitiveVisual.GetCircleSprite();
        renderer.color = new Color(0.55f, 0.15f, 1f, 0.95f);
        DemonKingPrimitiveVisual.ApplyProjectileSorting(renderer, 1100);

        DemonKingProjectile2D projectile = projectileObject.AddComponent<DemonKingProjectile2D>();
        projectile.Initialize(owner, direction, homingTarget, speed, turnRate, radius, damage, lifetime);
        return projectile;
    }

    private void Initialize(
        DemonKingController newOwner,
        Vector2 initialDirection,
        Transform newHomingTarget,
        float newSpeed,
        float newTurnRate,
        float newRadius,
        float newDamage,
        float newLifetime)
    {
        owner = newOwner;
        direction = initialDirection.sqrMagnitude > 0.0001f ? initialDirection.normalized : Vector2.right;
        homingTarget = newHomingTarget;
        speed = Mathf.Max(0.01f, newSpeed);
        turnRate = Mathf.Max(0f, newTurnRate);
        radius = Mathf.Max(0.05f, newRadius);
        damage = Mathf.Max(0f, newDamage);
        lifetime = Mathf.Max(0.1f, newLifetime);

        float diameter = radius * 2f;
        transform.localScale = new Vector3(diameter, diameter, 1f);
    }

    private void Update()
    {
        if (owner == null)
        {
            Destroy(gameObject);
            return;
        }

        elapsed += Time.deltaTime;
        if (elapsed >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        if (homingTarget != null)
        {
            Vector2 toTarget = (Vector2)homingTarget.position - (Vector2)transform.position;
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                float maxStep = turnRate * Time.deltaTime;
                float signedAngle = Vector2.SignedAngle(direction, toTarget.normalized);
                float clampedAngle = Mathf.Clamp(signedAngle, -maxStep, maxStep);
                direction = (Quaternion.Euler(0f, 0f, clampedAngle) * direction).normalized;
            }
        }

        transform.position += (Vector3)(direction * (speed * Time.deltaTime));
        TryHitTarget();
    }

    private void TryHitTarget()
    {
        Collider2D hit = Physics2D.OverlapCircle(transform.position, radius, owner.TargetMask);
        if (hit == null)
            return;

        GameObject targetRoot = CombatTargetResolver2D.ResolveDamageTarget(hit);
        if (targetRoot == null || targetRoot == owner.gameObject)
            return;

        CombatHitPayload payload = DemonKingCombatUtil.MakePayload(owner, owner.DefaultDamageEffect, damage);
        if (payload != null)
            CombatHitPayloadApplier.Apply(targetRoot, payload, hit.ClosestPoint(transform.position));

        Destroy(gameObject);
    }
}
