using System.Collections;
using UnityEngine;
using UnityGAS;

public static class PitFallExecutor
{
    public static IEnumerator Execute(PitFallContext context)
    {
        if (!context.IsValid)
            yield break;

        ApplyFallingEffect(context);
        ResetPhysicsVelocity(context.TargetTransform);

        if (context.FallDuration > 0f)
            yield return new WaitForSeconds(context.FallDuration);

        ApplyTrapDamage(context);
        MoveToRespawnPosition(context);
        RemoveFallingEffect(context);
    }

    private static void ApplyFallingEffect(PitFallContext context)
    {
        if (context.FallingEffect == null)
            return;

        GameplayEffectSpec statusSpec = context.AbilitySystem.MakeSpec(
            context.FallingEffect,
            context.TrapObject,
            context.SourceObject);

        statusSpec.Context.SetWorldPosition(context.FallCenter, Vector3.up);
        context.AbilitySystem.EffectRunner.ApplyEffectSpec(statusSpec, context.TargetObject);
    }

    private static void ResetPhysicsVelocity(Transform targetTransform)
    {
        Rigidbody2D body = targetTransform.GetComponent<Rigidbody2D>();
        if (body == null)
            return;

        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
    }

    private static void ApplyTrapDamage(PitFallContext context)
    {
        if (context.DamageEffect == null)
            return;

        HazardDamageAction.ApplyDamage(
            targetSystem: context.AbilitySystem,
            target: context.TargetObject,
            damageEffect: context.DamageEffect,
            finalHpDamage: context.Damage,
            causer: context.TrapObject,
            sourceObject: context.SourceObject,
            ignoreInvulnerability: true);
    }

    private static void MoveToRespawnPosition(PitFallContext context)
    {
        context.TargetTransform.position = context.RespawnPosition;
    }

    private static void RemoveFallingEffect(PitFallContext context)
    {
        if (context.FallingEffect == null)
            return;

        context.AbilitySystem.EffectRunner.RemoveEffect(context.FallingEffect, context.TargetObject);
    }
}
