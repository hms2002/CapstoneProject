using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

public static class DemonKingCombatUtil
{
    public const float TopDownCircleWarningYScale = AttackTelegraphSpec.TopDownCircleWarningYScale;

    private static readonly Collider2D[] OverlapBuffer = new Collider2D[32];

    /// <summary>
    /// 책임 :
    /// - 지속 판정 공격에서 대상별 다음 피해 가능 시각을 보관한다.
    /// - 레이저처럼 오래 남는 공격이 무적 중에는 쿨다운을 소비하지 않고, 실제 피해 성공 후에만 재타격 시간을 밀도록 돕는다.
    /// </summary>
    public sealed class DamageCooldownRegistry : Dictionary<GameObject, float>
    {
    }

    public static AttackTelegraphSpec CreateTopDownCircleWarningSpec(
        DemonKingController demon,
        Vector2 center,
        float diameter,
        float duration)
    {
        return AttackTelegraphSpec.CreateTopDownCircle(
            center,
            diameter,
            duration,
            demon != null ? demon.DefaultWarningStyle : null);
    }

    public static CombatHitPayload MakePayload(
        DemonKingController demon,
        GE_Damage_Spec damageEffect,
        float damageAmount,
        float knockbackImpulse = 0f)
    {
        if (demon == null || damageEffect == null || damageAmount < 0f)
            return null;

        if (damageAmount <= 0f && knockbackImpulse <= 0f)
            return null;

        CombatDamageSnapshot snapshot = new(
            finalHpDamage: damageAmount,
            finalStaggerBuildUp: 0f,
            finalKnockbackImpulse: knockbackImpulse,
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

    public static bool ApplyCircleDamage(
        DemonKingController demon,
        Vector2 center,
        float radius,
        GE_Damage_Spec damageEffect,
        float damageAmount,
        HashSet<GameObject> damagedTargets = null,
        float knockbackImpulse = 0f)
    {
        if (demon == null || radius <= 0f)
            return false;

        CombatHitPayload payload = MakePayload(demon, damageEffect, damageAmount, knockbackImpulse);
        if (payload == null)
            return false;

        int count = Physics2D.OverlapCircle(center, radius, CreateTargetFilter(demon.TargetMask), OverlapBuffer);
        return ApplyToHits(center, count, payload, demon.gameObject, damagedTargets);
    }

    public static void ApplyTopDownEllipseDamage(
        DemonKingController demon,
        Vector2 center,
        float diameter,
        GE_Damage_Spec damageEffect,
        float damageAmount,
        HashSet<GameObject> damagedTargets = null,
        float knockbackImpulse = 0f)
    {
        if (demon == null || diameter <= 0f)
            return;

        CombatHitPayload payload = MakePayload(demon, damageEffect, damageAmount, knockbackImpulse);
        if (payload == null)
            return;

        float broadphaseRadius = Mathf.Max(0.05f, diameter * 0.5f);
        int count = Physics2D.OverlapCircle(center, broadphaseRadius, CreateTargetFilter(demon.TargetMask), OverlapBuffer);
        ApplyToHits(center, count, payload, demon.gameObject, damagedTargets, topDownEllipseDiameter: diameter);
    }

    public static bool ApplyRectangleDamage(
        DemonKingController demon,
        Vector2 center,
        Vector2 size,
        float rotationDeg,
        GE_Damage_Spec damageEffect,
        float damageAmount,
        HashSet<GameObject> damagedTargets = null,
        float knockbackImpulse = 0f,
        DamageCooldownRegistry damageCooldowns = null,
        float damageIntervalSeconds = 0f)
    {
        if (demon == null || size.x <= 0f || size.y <= 0f)
            return false;

        CombatHitPayload payload = MakePayload(demon, damageEffect, damageAmount, knockbackImpulse);
        if (payload == null)
            return false;

        int count = Physics2D.OverlapBox(center, size, rotationDeg, CreateTargetFilter(demon.TargetMask), OverlapBuffer);
        return ApplyToHits(center, count, payload, demon.gameObject, damagedTargets, damageCooldowns, damageIntervalSeconds);
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

            if (damagedTargets.Contains(targetRoot))
                continue;

            if (CombatHitPayloadApplier.Apply(targetRoot, payload, hit.ClosestPoint(origin)))
                damagedTargets.Add(targetRoot);
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

    private static bool ApplyToHits(
        Vector2 hitOrigin,
        int count,
        CombatHitPayload payload,
        GameObject self,
        HashSet<GameObject> damagedTargets,
        DamageCooldownRegistry damageCooldowns = null,
        float damageIntervalSeconds = 0f,
        float topDownEllipseDiameter = 0f)
    {
        bool appliedAny = false;
        for (int i = 0; i < count; i++)
        {
            Collider2D hit = OverlapBuffer[i];
            if (hit == null)
                continue;

            if (topDownEllipseDiameter > 0f
                && !TopDownEllipseHitUtility2D.ContainsCollider(hit, hitOrigin, topDownEllipseDiameter))
            {
                continue;
            }

            GameObject targetRoot = CombatTargetResolver2D.ResolveDamageTarget(hit);
            if (targetRoot == null || targetRoot == self)
                continue;

            if (damagedTargets != null && damagedTargets.Contains(targetRoot))
                continue;

            if (damageCooldowns != null &&
                damageCooldowns.TryGetValue(targetRoot, out float nextAllowedTime) &&
                Time.time < nextAllowedTime)
            {
                continue;
            }

            if (!CombatHitPayloadApplier.Apply(targetRoot, payload, hit.ClosestPoint(hitOrigin)))
                continue;

            damagedTargets?.Add(targetRoot);
            if (damageCooldowns != null)
                damageCooldowns[targetRoot] = Time.time + Mathf.Max(0f, damageIntervalSeconds);

            appliedAny = true;
        }

        return appliedAny;
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
