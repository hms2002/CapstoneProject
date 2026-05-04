using System.Collections;
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

    private IEnumerator RunCircle(
        DemonKingController owner,
        Vector2 center,
        float diameter,
        float warningSeconds,
        float damage,
        bool ignoreOwnerGroggy)
    {
        DemonKingPrimitiveVisual.SpawnSquare(
            center,
            new Vector2(diameter, diameter),
            0f,
            warningSeconds,
            WarningColor,
            "DemonKing_ExplosionSquareWarning");

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

            DemonKingPrimitiveVisual.SpawnSquare(
                center,
                new Vector2(diameter, diameter),
                0f,
                AttackFlashSeconds,
                AttackColor,
                "DemonKing_ExplosionSquareAttack");
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
