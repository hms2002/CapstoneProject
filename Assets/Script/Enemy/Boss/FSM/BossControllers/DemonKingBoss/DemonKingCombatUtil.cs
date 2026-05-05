using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

public static class DemonKingCombatUtil
{
    private static readonly Collider2D[] OverlapBuffer = new Collider2D[32];

    public static CombatHitPayload MakePayload(
        DemonKingController demon,
        GE_Damage_Spec damageEffect,
        float damageAmount,
        float knockbackImpulse = 0f)
    {
        if (demon == null || damageEffect == null || damageAmount <= 0f)
            return null;

        CombatDamageSnapshot snapshot = new(
            finalHpDamage: damageAmount,
            finalStaggerBuildUp: 0f,
            finalKnockbackImpulse: knockbackImpulse,
            elementBuildUps: null,
            isCriticalHit: false);

        return CombatHitPayload.FromSnapshot(
            sourceSystem: demon.AbilitySystem,
            sourceSpec: null,
            damageEffect: damageEffect,
            knockbackEffect: demon.DefaultKnockbackEffect,
            snapshot: snapshot,
            hitConfirmedTag: null,
            causer: demon.gameObject);
    }

    public static void ApplyCircleDamage(
        DemonKingController demon,
        Vector2 center,
        float radius,
        GE_Damage_Spec damageEffect,
        float damageAmount,
        HashSet<GameObject> damagedTargets = null,
        float knockbackImpulse = 0f)
    {
        if (demon == null || radius <= 0f)
            return;

        CombatHitPayload payload = MakePayload(demon, damageEffect, damageAmount, knockbackImpulse);
        if (payload == null)
            return;

        int count = Physics2D.OverlapCircle(center, radius, CreateTargetFilter(demon.TargetMask), OverlapBuffer);
        ApplyToHits(center, count, payload, demon.gameObject, damagedTargets);
    }

    public static void ApplyRectangleDamage(
        DemonKingController demon,
        Vector2 center,
        Vector2 size,
        float rotationDeg,
        GE_Damage_Spec damageEffect,
        float damageAmount,
        HashSet<GameObject> damagedTargets = null,
        float knockbackImpulse = 0f)
    {
        if (demon == null || size.x <= 0f || size.y <= 0f)
            return;

        CombatHitPayload payload = MakePayload(demon, damageEffect, damageAmount, knockbackImpulse);
        if (payload == null)
            return;

        int count = Physics2D.OverlapBox(center, size, rotationDeg, CreateTargetFilter(demon.TargetMask), OverlapBuffer);
        ApplyToHits(center, count, payload, demon.gameObject, damagedTargets);
    }

    public static void ApplySectorDamage(
        DemonKingController demon,
        Vector2 origin,
        Vector2 direction,
        float radius,
        float angleDeg,
        GE_Damage_Spec damageEffect,
        float damageAmount,
        float knockbackImpulse = 0f)
    {
        if (demon == null || radius <= 0f)
            return;

        CombatHitPayload payload = MakePayload(demon, damageEffect, damageAmount, knockbackImpulse);
        if (payload == null)
            return;

        Vector2 forward = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        float halfAngle = Mathf.Clamp(angleDeg, 0f, 360f) * 0.5f;
        int count = Physics2D.OverlapCircle(origin, radius, CreateTargetFilter(demon.TargetMask), OverlapBuffer);
        HashSet<GameObject> damagedTargets = new();

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = OverlapBuffer[i];
            if (hit == null)
                continue;

            GameObject targetRoot = CombatTargetResolver2D.ResolveDamageTarget(hit);
            if (targetRoot == null || targetRoot == demon.gameObject)
                continue;

            Vector2 toTarget = (Vector2)hit.bounds.center - origin;
            if (toTarget.sqrMagnitude > 0.0001f && Vector2.Angle(forward, toTarget.normalized) > halfAngle)
                continue;

            if (!damagedTargets.Add(targetRoot))
                continue;

            CombatHitPayloadApplier.Apply(targetRoot, payload, hit.ClosestPoint(origin));
        }
    }

    public static Vector2 DirectionToTargetOrFacing(DemonKingController demon, Vector2 from)
    {
        if (demon != null && demon.CurrentTarget != null)
        {
            Vector2 delta = (Vector2)demon.CurrentTarget.position - from;
            if (delta.sqrMagnitude > 0.0001f)
                return delta.normalized;
        }

        return demon != null ? demon.FacingDirection : Vector2.right;
    }

    public static float RotationDeg(Vector2 direction)
    {
        Vector2 safeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        return Mathf.Atan2(safeDirection.y, safeDirection.x) * Mathf.Rad2Deg;
    }

    private static void ApplyToHits(
        Vector2 hitOrigin,
        int count,
        CombatHitPayload payload,
        GameObject self,
        HashSet<GameObject> damagedTargets)
    {
        for (int i = 0; i < count; i++)
        {
            Collider2D hit = OverlapBuffer[i];
            if (hit == null)
                continue;

            GameObject targetRoot = CombatTargetResolver2D.ResolveDamageTarget(hit);
            if (targetRoot == null || targetRoot == self)
                continue;

            if (damagedTargets != null && !damagedTargets.Add(targetRoot))
                continue;

            CombatHitPayloadApplier.Apply(targetRoot, payload, hit.ClosestPoint(hitOrigin));
        }
    }

    private static ContactFilter2D CreateTargetFilter(LayerMask targetMask)
    {
        ContactFilter2D filter = new();
        filter.SetLayerMask(targetMask);
        filter.useLayerMask = true;
        filter.useTriggers = true;
        return filter;
    }
}
