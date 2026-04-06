using System.Collections;
using UnityEngine;
using UnityGAS;

// 에디터 메뉴: Create > GAS > Ability Logic > Tackle
[CreateAssetMenu(fileName = "AL_Tackle", menuName = "GAS/Ability Logic/Tackle")]
public class AL_Tackle : AbilityLogic
{
    [Header("Damage")]
    [Tooltip("Tackle이 적용할 데미지 GameplayEffect입니다.")]
    [SerializeField] private GE_Damage_Spec damageEffect;

    [Tooltip("Tackle 적중 시 적용할 HP 데미지입니다.")]
    [SerializeField] private float damageAmount = 10.0f;

    [Space(8)]

    [Header("Prepared Tackle")]
    [Tooltip("공격 범위를 고정한 뒤 실제 돌진하기 전까지 대기하는 시간입니다.")]
    [SerializeField] private float attackReadyTime = 2.0f;

    [Tooltip("고정된 지점을 향해 돌진하는 데 걸리는 시간입니다.")]
    [SerializeField] private float lungeDuration = 0.25f;

    [Tooltip("돌진 종료 시 고정된 공격 지점 주변에서 피해를 판정할 반지름입니다.")]
    [SerializeField] private float impactDamageRadius = 0.75f;

    [Space(8)]

    [Header("Optional")]
    [Tooltip("Tackle 적중 시 함께 적용할 넉백 GameplayEffect입니다.")]
    [SerializeField] private GE_Knockback_Spec knockbackEffect;

    [Tooltip("Tackle 적중 시 적용할 넉백 세기입니다.")]
    [SerializeField] private float knockbackImpulse = 0f;

    [Tooltip("Tackle 적중 시 발행할 Hit Confirm 태그입니다.")]
    [SerializeField] private GameplayTag hitConfirmedTag;

    public override IEnumerator Activate(AbilitySystem caster, AbilitySpec spec, GameObject target)
    {
        if (caster == null || target == null || damageEffect == null)
            yield break;

        Mob mob = caster.GetComponent<Mob>();
        if (mob != null && mob.TryConsumePreparedTackleContext(out Mob.PreparedTackleContext context))
        {
            yield return ActivatePreparedTackle(caster, spec, target, context);
            yield break;
        }

        ApplyTackleDamage(caster, spec, target);
    }

    public override void CleanupForSceneTransition(AbilitySystem system, AbilitySpec spec, GameObject target)
    {
        AbilityMotionController2D motionController = system != null
            ? system.GetComponent<AbilityMotionController2D>()
            : null;

        if (motionController != null)
            motionController.CancelMotion();
    }

    private IEnumerator ActivatePreparedTackle(AbilitySystem caster, AbilitySpec spec, GameObject fallbackTarget, Mob.PreparedTackleContext context)
    {
        if (attackReadyTime > 0f)
            yield return AbilityTasks.WaitDelay(caster, spec, attackReadyTime);

        if (IsCancelled(spec))
            yield break;

        AbilityMotionController2D motionController = caster.GetComponent<AbilityMotionController2D>();
        float finalLungeDuration = Mathf.Max(0f, lungeDuration);
        float finalLungeDistance = Mathf.Max(0f, context.LungeDistance);

        if (motionController != null && finalLungeDuration > 0f && finalLungeDistance > 0f)
        {
            motionController.StartLunge(
                context.StartPosition,
                context.Direction,
                finalLungeDistance,
                finalLungeDuration);

            yield return AbilityTasks.WaitDelay(caster, spec, finalLungeDuration);
        }

        if (IsCancelled(spec))
            yield break;

        GameObject finalTarget = context.Target != null ? context.Target : fallbackTarget;
        if (IsTargetInsideFixedImpactArea(finalTarget, context.ImpactPosition))
            ApplyTackleDamage(caster, spec, finalTarget);
    }

    private bool IsTargetInsideFixedImpactArea(GameObject target, Vector2 impactPosition)
    {
        if (target == null)
            return false;

        float radius = Mathf.Max(0f, impactDamageRadius);
        if (radius <= 0f)
            return false;

        Vector2 toTarget = (Vector2)target.transform.position - impactPosition;
        return toTarget.sqrMagnitude <= radius * radius;
    }

    private void ApplyTackleDamage(AbilitySystem caster, AbilitySpec spec, GameObject target)
    {
        if (caster == null || target == null || damageEffect == null)
            return;

        CombatDamageSnapshot snapshot = new CombatDamageSnapshot(
            finalHpDamage: damageAmount,
            finalStaggerBuildUp: 0f,
            finalKnockbackImpulse: knockbackImpulse,
            elementBuildUps: null,
            isCriticalHit: false
        );

        CombatDamageAction.ApplyDamageAndEmitHit(
            system: caster,
            spec: spec,
            damageEffect: damageEffect,
            knockbackEffect: knockbackEffect,
            target: target,
            finalHpDamage: snapshot.FinalHpDamage,
            finalStaggerBuildUp: snapshot.FinalStaggerBuildUp,
            elementBuildUps: snapshot.ElementBuildUps,
            finalKnockbackImpulse: snapshot.FinalKnockbackImpulse,
            hitConfirmedTag: hitConfirmedTag,
            causer: caster.gameObject
        );

        Debug.Log($"[GAS] {caster.name} hit {target.name} for {damageAmount}");
    }

    private static bool IsCancelled(AbilitySpec spec)
    {
        return spec != null && spec.Token != null && spec.Token.IsCancelled;
    }
}
