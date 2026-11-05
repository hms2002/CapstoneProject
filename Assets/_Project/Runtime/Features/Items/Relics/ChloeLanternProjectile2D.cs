using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>발사 시점 payload를 유지하며 강한 유도와 첫 접촉 시 소멸을 담당한다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CircleCollider2D), typeof(Rigidbody2D))]
public sealed class ChloeLanternProjectile2D : AttackBase
{
    private readonly List<RaycastHit2D> sweepHits = new(16);
    private Enemy target;
    private Vector2 direction;
    private float speed;
    private float turnRate;
    private float radius;
    private bool consumed;

    public void Launch(ProjectileAttackSpawnContext context, Enemy homingTarget, float degreesPerSecond)
    {
        target = homingTarget;
        direction = context.direction.sqrMagnitude > 0.0001f ? context.direction.normalized : Vector2.right;
        speed = Mathf.Max(0f, context.speed);
        turnRate = Mathf.Max(1f, degreesPerSecond);
        CircleCollider2D collider = GetComponent<CircleCollider2D>();
        Vector3 scale = transform.lossyScale;
        radius = collider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
        consumed = false;
        SetupBase(context);
        FaceDirection();
    }

    protected override void TickAttack(float deltaTime)
    {
        if (consumed) return;
        if (ChloeLanternOrbitController.IsDialogueBlockingCombat(OwnerSystem))
        {
            DiscardWithoutHit();
            return;
        }
        if (deltaTime <= 0f) return;
        if (OwnerSystem == null || !OwnerSystem.gameObject.activeInHierarchy ||
            CombatTargetDeathUtility.IsPlayerDeathSequenceRunning(OwnerSystem.transform))
        {
            consumed = true;
            DestroySelf();
            return;
        }
        if (target != null && target.isActiveAndEnabled && !target.IsDead)
        {
            Vector2 aim = CommonMonsterCombatUtility.ResolveAimPoint(target.gameObject) - (Vector2)transform.position;
            if (aim.sqrMagnitude > 0.0001f)
            {
                float angle = Vector2.SignedAngle(direction, aim);
                direction = (Quaternion.Euler(0f, 0f, Mathf.Clamp(angle, -turnRate * deltaTime, turnRate * deltaTime)) * direction).normalized;
            }
        }
        else target = null; // A lost target leaves the already launched shot flying forward.
        FaceDirection();

        float distance = speed * deltaTime;
        var filter = new ContactFilter2D { useTriggers = true };
        filter.SetLayerMask(WallLayers.value | DamageLayers.value);
        Physics2D.CircleCast(transform.position, radius, direction, filter, sweepHits, distance);
        Collider2D first = null;
        float firstDistance = float.PositiveInfinity;
        foreach (RaycastHit2D hit in sweepHits)
        {
            if (hit.distance >= firstDistance || !IsContact(hit.collider)) continue;
            first = hit.collider;
            firstDistance = hit.distance;
        }
        if (first != null)
        {
            transform.position += (Vector3)(direction * firstDistance);
            ConsumeContact(first);
        }
        else transform.position += (Vector3)(direction * distance);
    }

    private void FaceDirection() => transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

    private bool IsContact(Collider2D other)
    {
        if (other == null || other.gameObject == gameObject) return false;
        if ((WallLayers.value & (1 << other.gameObject.layer)) != 0) return true;
        if (other.GetComponent<CombatHurtbox2D>() == null) return false;
        GameObject root = CombatTargetResolver2D.ResolveDamageTarget(other);
        if (root == null || IsIgnoredTarget(root) || (DamageLayers.value & (1 << root.layer)) == 0) return false;
        Enemy enemy = root.GetComponent<Enemy>();
        return enemy != null && enemy.isActiveAndEnabled && !enemy.IsDead;
    }

    private void ConsumeContact(Collider2D other)
    {
        if (consumed) return;
        // Physics callbacks can precede the owner's Update; do not rely only on the firing gate.
        if (ChloeLanternOrbitController.IsDialogueBlockingCombat(OwnerSystem))
        {
            DiscardWithoutHit();
            return;
        }
        if (!IsContact(other)) return;
        consumed = true; // Guard before applying damage: multiple hurtboxes may report in the same physics step.
        if ((WallLayers.value & (1 << other.gameObject.layer)) == 0)
            TryApplyHit(CombatTargetResolver2D.ResolveDamageTarget(other), other);
        DestroySelf();
    }

    private void DiscardWithoutHit()
    {
        consumed = true;
        gameObject.SetActive(false);
        DestroySelf();
    }

    protected override void OnTriggerEnter2D(Collider2D other) => ConsumeContact(other);
    protected override void OnTriggerStay2D(Collider2D other) => ConsumeContact(other);
}
