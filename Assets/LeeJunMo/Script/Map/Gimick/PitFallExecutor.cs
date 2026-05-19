using System.Collections;
using UnityEngine;
using UnityGAS;

public static class PitFallExecutor
{
    public static IEnumerator Execute(PitFallContext context)
    {
        if (!context.IsValid)
            yield break;

        SlimeQueenBossBase slimeQueenTarget = context.TargetTransform.GetComponent<SlimeQueenBossBase>();

        try
        {
            if (slimeQueenTarget != null)
                slimeQueenTarget.SetPitFallRuntimeLock(true);

            ApplyFallingEffect(context);
            ResetPhysicsVelocity(context.TargetTransform);

            if (context.FallDuration > 0f)
                yield return new WaitForSeconds(context.FallDuration);

            ApplyTrapDamage(context);
            MoveToRespawnPosition(context);
        }
        finally
        {
            ResetPhysicsVelocity(context.TargetTransform);
            RemoveFallingEffect(context);
            if (slimeQueenTarget != null)
                slimeQueenTarget.SetPitFallRuntimeLock(false);
        }
    }

    private static void ApplyFallingEffect(PitFallContext context)
    {
        if (context.FallingEffect == null || context.AbilitySystem == null || context.TargetObject == null)
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
        if (targetTransform == null)
            return;

        Rigidbody2D body = targetTransform.GetComponent<Rigidbody2D>();
        if (body == null)
            return;

        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
    }

    private static void ApplyTrapDamage(PitFallContext context)
    {
        if (context.DamageEffect == null || context.AbilitySystem == null || context.TargetObject == null)
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
        if (context.TargetTransform == null)
            return;

        context.TargetTransform.position = context.RespawnPosition;
    }

    private static void RemoveFallingEffect(PitFallContext context)
    {
        if (context.FallingEffect == null || context.AbilitySystem == null || context.TargetObject == null)
            return;

        context.AbilitySystem.EffectRunner.RemoveEffect(context.FallingEffect, context.TargetObject);
    }
}
