using System.Collections;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// - HoleTrap 낙하 문맥을 받아 낙하 연출, 피해 적용, 리스폰/대상별 후처리를 정해진 순서로 실행한다.
/// - 대상별 특수 동작은 IPitFallReaction에 위임해 함정 실행 순서를 공통 규칙으로 유지한다.
/// </summary>
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

            context.Reaction?.OnPitFallStarted(context);
            ApplyFallingEffect(context);
            ResetPhysicsVelocity(context.TargetTransform);

            if (context.FallDuration > 0f)
                yield return new WaitForSeconds(context.FallDuration);

            RemoveFallingEffectBeforeDamageIfOwnedByExecutor(context);
            ApplyTrapDamage(context);
            context.Reaction?.OnPitFallCompleted(context);

            if (context.Reaction == null || context.Reaction.UseDefaultRespawn)
                MoveToRespawnPosition(context);
        }
        finally
        {
            ResetPhysicsVelocity(context.TargetTransform);
            if (context.Reaction == null || context.Reaction.RemoveFallingEffectOnComplete)
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
        {
            LogDebug(context, $"damage skipped: missing refs. target={context.TargetObject}, abilitySystem={context.AbilitySystem}, damageEffect={context.DamageEffect}");
            return;
        }

        LogDebug(context, $"damage request. target={context.TargetObject.name}, damage={context.Damage:0.###}, ignoreInvulnerability=False, ignoreEvasion=True");
        HazardDamageAction.ApplyDamage(
            targetSystem: context.AbilitySystem,
            target: context.TargetObject,
            damageEffect: context.DamageEffect,
            finalHpDamage: context.Damage,
            causer: context.TrapObject,
            sourceObject: context.SourceObject,
            ignoreInvulnerability: false,
            ignoreEvasion: true,
            logDebug: context.LogDebug);
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

    /// <summary>
    /// 책임:
    /// - 낙하 연출용 GE가 부여한 무적 태그가 같은 낙하 피해를 막지 않도록 피해 직전에 정리한다.
    /// - 원래 Executor가 제거 책임을 가진 대상만 선제 제거해 몬스터별 낙하 후처리 연출을 침범하지 않는다.
    /// </summary>
    private static void RemoveFallingEffectBeforeDamageIfOwnedByExecutor(PitFallContext context)
    {
        if (context.Reaction != null && !context.Reaction.RemoveFallingEffectOnComplete)
            return;

        RemoveFallingEffect(context);
    }

    private static void LogDebug(PitFallContext context, string message)
    {
        if (!context.LogDebug)
            return;

        Debug.Log($"[PitFallExecutor] {message}", context.TrapObject);
    }
}
