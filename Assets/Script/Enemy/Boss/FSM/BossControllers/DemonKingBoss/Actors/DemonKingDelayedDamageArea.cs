using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

public sealed class DemonKingDelayedDamageArea : MonoBehaviour
{
    private static readonly Color WarningColor = new(1f, 0.15f, 0.08f, 0.35f);
    private static readonly Color AttackColor = new(1f, 0.85f, 0.2f, 0.65f);
    private const float AttackFlashSeconds = 0.12f;

    public static void SpawnCircle(
        DemonKingController owner,
        Vector2 center,
        float diameter,
        float warningSeconds,
        float damage,
        bool ignoreOwnerGroggy = false)
    {
        if (owner == null)
            return;

        GameObject runner = new("DemonKing_DelayedCircle");
        DemonKingDelayedDamageArea area = runner.AddComponent<DemonKingDelayedDamageArea>();
        area.StartCoroutine(area.RunCircle(owner, center, diameter, warningSeconds, damage, ignoreOwnerGroggy));
    }

    public static void SpawnRectangle(
        DemonKingController owner,
        Vector2 center,
        Vector2 size,
        float rotationDeg,
        float warningSeconds,
        float damage,
        bool ignoreOwnerGroggy = false)
    {
        if (owner == null)
            return;

        GameObject runner = new("DemonKing_DelayedRectangle");
        DemonKingDelayedDamageArea area = runner.AddComponent<DemonKingDelayedDamageArea>();
        area.StartCoroutine(area.RunRectangle(owner, center, size, rotationDeg, warningSeconds, damage, ignoreOwnerGroggy));
    }

    public static void SpawnCircleCluster(
        DemonKingController owner,
        IReadOnlyList<Vector2> centers,
        float diameter,
        float warningSeconds,
        float damage,
        bool ignoreOwnerGroggy = false)
    {
        if (owner == null || centers == null || centers.Count == 0)
            return;

        GameObject runner = new("DemonKing_DelayedCircleCluster");
        DemonKingDelayedDamageArea area = runner.AddComponent<DemonKingDelayedDamageArea>();
        area.StartCoroutine(area.RunCircleCluster(owner, centers, diameter, warningSeconds, damage, ignoreOwnerGroggy));
    }

    private IEnumerator RunCircle(
        DemonKingController owner,
        Vector2 center,
        float diameter,
        float warningSeconds,
        float damage,
        bool ignoreOwnerGroggy)
    {
        DemonKingPrimitiveVisual.SpawnCircle(
            center,
            diameter,
            warningSeconds,
            WarningColor,
            "DemonKing_ExplosionCircleWarning");

        owner.GetTelegraphService()?.SpawnDetachedView(
            AttackTelegraphSpec.CreateCircle(center, diameter, warningSeconds, owner.DefaultWarningStyle));

        if (warningSeconds > 0f)
            yield return new WaitForSeconds(warningSeconds);

        if (owner != null && !owner.IsDead && (ignoreOwnerGroggy || !owner.HasGroggyTag()))
        {
            DemonKingCombatUtil.ApplyCircleDamage(
                owner,
                center,
                diameter * 0.5f,
                owner.DefaultDamageEffect,
                damage);

            DemonKingPatternVfx.SpawnExplosionOrFallbackCircle(
                center,
                diameter,
                AttackColor,
                "DemonKing_ExplosionCircleAttack");
        }

        Destroy(gameObject);
    }

    private IEnumerator RunCircleCluster(
        DemonKingController owner,
        IReadOnlyList<Vector2> centers,
        float diameter,
        float warningSeconds,
        float damage,
        bool ignoreOwnerGroggy)
    {
        for (int i = 0; i < centers.Count; i++)
        {
            Vector2 center = centers[i];
            DemonKingPrimitiveVisual.SpawnCircle(
                center,
                diameter,
                warningSeconds,
                WarningColor,
                "DemonKing_ExplosionCircleWarning");

            owner.GetTelegraphService()?.SpawnDetachedView(
                AttackTelegraphSpec.CreateCircle(center, diameter, warningSeconds, owner.DefaultWarningStyle));
        }

        if (warningSeconds > 0f)
            yield return new WaitForSeconds(warningSeconds);

        if (owner != null && !owner.IsDead && (ignoreOwnerGroggy || !owner.HasGroggyTag()))
        {
            HashSet<GameObject> damagedTargets = new();
            for (int i = 0; i < centers.Count; i++)
            {
                Vector2 center = centers[i];
                DemonKingCombatUtil.ApplyCircleDamage(
                    owner,
                    center,
                    diameter * 0.5f,
                    owner.DefaultDamageEffect,
                    damage,
                    damagedTargets);

                DemonKingPatternVfx.SpawnExplosionOrFallbackCircle(
                    center,
                    diameter,
                    AttackColor,
                    "DemonKing_ExplosionCircleAttack");
            }
        }

        Destroy(gameObject);
    }

    private IEnumerator RunRectangle(
        DemonKingController owner,
        Vector2 center,
        Vector2 size,
        float rotationDeg,
        float warningSeconds,
        float damage,
        bool ignoreOwnerGroggy)
    {
        DemonKingPrimitiveVisual.SpawnSquare(
            center,
            size,
            rotationDeg,
            warningSeconds,
            WarningColor,
            "DemonKing_RectSquareWarning");

        owner.GetTelegraphService()?.SpawnDetachedView(
            AttackTelegraphSpec.CreateRectangle(center, size, rotationDeg, warningSeconds, owner.DefaultWarningStyle));

        if (warningSeconds > 0f)
            yield return new WaitForSeconds(warningSeconds);

        if (owner != null && !owner.IsDead && (ignoreOwnerGroggy || !owner.HasGroggyTag()))
        {
            DemonKingCombatUtil.ApplyRectangleDamage(
                owner,
                center,
                size,
                rotationDeg,
                owner.DefaultDamageEffect,
                damage);

            DemonKingPrimitiveVisual.SpawnSquare(
                center,
                size,
                rotationDeg,
                AttackFlashSeconds,
                AttackColor,
                "DemonKing_RectSquareAttack");
        }

        Destroy(gameObject);
    }
}
