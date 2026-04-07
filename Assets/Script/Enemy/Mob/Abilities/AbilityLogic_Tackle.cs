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
    [SerializeField] private float attackReadyTime = 0.5f;

    [Tooltip("Tackle 돌진 시작 시 적용할 초기 이동 속도입니다.")]
    [SerializeField] private float tackleSpeed = 20.0f;

    [Tooltip("Tackle 돌진 속도가 줄어드는 감쇠 강도입니다.")]
    [SerializeField] private float tackleDamping = 3.0f;

    [Tooltip("감쇠 Tackle을 유지하는 시간입니다.")]
    [SerializeField] private float lungeDuration = 0.5f;

    [Header("Optional")]
    [Tooltip("Tackle 적중 시 함께 적용할 넉백 GameplayEffect입니다.")]
    [SerializeField] private GE_Knockback_Spec knockbackEffect;

    [Tooltip("Tackle 적중 시 적용할 넉백 세기입니다.")]
    [SerializeField] private float knockbackImpulse = 0f;

    [Tooltip("Tackle 적중 시 발행할 Hit Confirm 태그입니다.")]
    [SerializeField] private GameplayTag hitConfirmedTag;

    /// <summary>Tackle Ability를 실행하고 준비된 태클 정보가 있으면 돌진 패턴으로 처리합니다.</summary>
    public override IEnumerator Activate(AbilitySystem caster, AbilitySpec spec, GameObject target)
    {
        if (caster == null || target == null || damageEffect == null)
            yield break;

        Mob mob = caster.GetComponent<Mob>();
        if (mob != null && mob.TryConsumePreparedTackleContext(out Mob.PreparedTackleContext context))
        {
            yield return ActivatePreparedTackle(caster, spec, target, mob, context);
            yield break;
        }

        TryApplyTackleDamage(caster, spec, target);
    }

    /// <summary>씬 전환 시 진행 중인 Tackle 이동을 정리합니다.</summary>
    public override void CleanupForSceneTransition(AbilitySystem system, AbilitySpec spec, GameObject target)
    {
        AbilityMotionController2D motionController = system != null
            ? system.GetComponent<AbilityMotionController2D>()
            : null;

        if (motionController != null)
            motionController.CancelMotion();
    }

    /// <summary>고정된 예고 방향으로 대기 후 감쇠 Tackle 돌진을 실행합니다.</summary>
    private IEnumerator ActivatePreparedTackle(AbilitySystem caster, AbilitySpec spec, GameObject fallbackTarget, Mob mob, Mob.PreparedTackleContext context)
    {
        if (attackReadyTime > 0f)
            yield return AbilityTasks.WaitDelay(caster, spec, attackReadyTime);

        if (IsCancelled(spec))
        {
            mob.HidePreparedTackleTelegraph();
            yield break;
        }

        mob.HidePreparedTackleTelegraph();

        AbilityMotionController2D motionController = GetOrAddMotionController(caster);
        float finalLungeDuration = Mathf.Max(0f, lungeDuration);
        float finalTackleSpeed = Mathf.Max(0f, tackleSpeed);
        float finalTackleDamping = Mathf.Max(0f, tackleDamping);

        if (motionController != null && finalLungeDuration > 0f && finalTackleSpeed > 0f)
        {
            motionController.StartDampedDash(
                context.Direction,
                finalTackleSpeed,
                finalLungeDuration,
                finalTackleDamping);

            yield return AbilityTasks.WaitDelay(caster, spec, finalLungeDuration);
        }

        if (IsCancelled(spec))
            yield break;

        GameObject finalTarget = context.Target != null ? context.Target : fallbackTarget;
        if (!mob.HasTackleHitCooldown &&
            IsTargetInsideFixedImpactArea(finalTarget, context) &&
            TryApplyTackleDamage(caster, spec, finalTarget))
        {
            mob.StartTackleHitCooldown();
        }
    }

    /// <summary>타겟이 준비 시점에 고정된 Tackle 직사각형 판정 안에 있는지 확인합니다.</summary>
    private bool IsTargetInsideFixedImpactArea(GameObject target, Mob.PreparedTackleContext context)
    {
        if (target == null)
            return false;

        float lungeDistance = Mathf.Max(0f, context.LungeDistance);
        float halfWidth = Mathf.Max(0.01f, context.TelegraphWidth * 0.5f);
        if (lungeDistance <= 0f)
            return false;

        Vector2 direction = context.Direction.sqrMagnitude > 0.0001f
            ? context.Direction.normalized
            : Vector2.right;

        Vector2 targetPosition = target.transform.position;
        Vector2 toTarget = targetPosition - context.StartPosition;
        float forwardDistance = Vector2.Dot(toTarget, direction);

        if (forwardDistance < 0f || forwardDistance > lungeDistance)
            return false;

        Vector2 closestPointOnLine = context.StartPosition + direction * forwardDistance;
        Vector2 lateralOffset = targetPosition - closestPointOnLine;
        return lateralOffset.sqrMagnitude <= halfWidth * halfWidth;
    }

    /// <summary>Mob과 접촉 중인 타겟에게 Tackle 피해를 즉시 적용합니다.</summary>
    public bool TryApplyContactDamage(AbilitySystem caster, AbilitySpec spec, GameObject target)
    {
        return TryApplyTackleDamage(caster, spec, target);
    }

    /// <summary>Tackle 피해와 부가 전투 효과를 중앙 전투 파이프라인으로 적용합니다.</summary>
    private bool TryApplyTackleDamage(AbilitySystem caster, AbilitySpec spec, GameObject target)
    {
        if (caster == null || target == null || damageEffect == null)
            return false;

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
        return true;
    }

    /// <summary>현재 Ability 실행 토큰이 취소되었는지 확인합니다.</summary>
    private static bool IsCancelled(AbilitySpec spec)
    {
        return spec != null && spec.Token != null && spec.Token.IsCancelled;
    }

    /// <summary>시전자에게 AbilityMotionController2D가 없으면 추가해서 반환합니다.</summary>
    private static AbilityMotionController2D GetOrAddMotionController(AbilitySystem caster)
    {
        if (caster == null)
            return null;

        AbilityMotionController2D motionController = caster.GetComponent<AbilityMotionController2D>();
        if (motionController != null)
            return motionController;

        return caster.gameObject.AddComponent<AbilityMotionController2D>();
    }
}
