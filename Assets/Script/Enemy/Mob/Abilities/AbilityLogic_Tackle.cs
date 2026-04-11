using System.Collections;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(fileName = "AL_Tackle", menuName = "GAS/Ability Logic/Tackle")]
public class AL_Tackle : AbilityLogic
{
    [Header("데미지")]
    [Tooltip("태클 데미지에 사용할 이펙트입니다.")]
    [SerializeField] private GE_Damage_Spec damageEffect;

    [Tooltip("태클 피해량입니다.")]
    [SerializeField] private float damageAmount = 10f;

    [Header("준비")]
    [Tooltip("경고를 보여줄 시간입니다.")]
    [SerializeField] private float readyTime = 0.5f;

    [Tooltip("태클 시작 속도입니다.")]
    [SerializeField] private float tackleSpeed = 20f;

    [Tooltip("태클 감속값입니다.")]
    [SerializeField] private float tackleDamping = 3f;

    [Tooltip("태클 지속 시간입니다.")]
    [SerializeField] private float lungeTime = 0.5f;

    [Header("옵션")]
    [Tooltip("넉백에 사용할 이펙트입니다.")]
    [SerializeField] private GE_Knockback_Spec knockbackEffect;

    [Tooltip("넉백 세기입니다.")]
    [SerializeField] private float knockbackImpulse = 0f;

    [Tooltip("적중 확인 태그입니다.")]
    [SerializeField] private GameplayTag hitConfirmedTag;

    [Header("경고")]
    [Tooltip("태클 경고 스타일입니다.")]
    [SerializeField] private AttackTelegraphStyle telegraphStyle;

    public override IEnumerator Activate(AbilitySystem caster, AbilitySpec spec, GameObject target)
    {
        if (caster == null || target == null || damageEffect == null)
            yield break;

        TackleAttack tackle = caster.GetComponent<TackleAttack>();
        if (tackle != null && tackle.TryGetContext(out TackleAttack.TackleContext context))
        {
            yield return RunPreparedTackle(caster, spec, target, tackle, context);
            yield break;
        }

        ApplyDamage(caster, spec, target);
    }

    public override void CleanupForSceneTransition(AbilitySystem system, AbilitySpec spec, GameObject target)
    {
        AbilityMotionController2D motion = system != null
            ? system.GetComponent<AbilityMotionController2D>()
            : null;

        if (motion != null)
            motion.CancelMotion();

        HideTelegraph(system);
    }

    /// <summary>준비된 태클을 실행합니다.</summary>
    private IEnumerator RunPreparedTackle(
        AbilitySystem caster,
        AbilitySpec spec,
        GameObject fallbackTarget,
        TackleAttack tackle,
        TackleAttack.TackleContext context)
    {
        tackle.ShowTelegraph(context, readyTime, telegraphStyle);

        if (readyTime > 0f)
            yield return AbilityTasks.WaitDelay(caster, spec, readyTime);

        if (IsCancelled(spec))
        {
            tackle.HideTelegraph();
            yield break;
        }

        tackle.HideTelegraph();

        AbilityMotionController2D motion = GetMotion(caster);
        float finalLungeTime = Mathf.Max(0f, lungeTime);
        float finalSpeed = Mathf.Max(0f, tackleSpeed);
        float finalDamping = Mathf.Max(0f, tackleDamping);

        if (motion != null && finalLungeTime > 0f && finalSpeed > 0f)
        {
            motion.StartDampedDash(
                context.Direction,
                finalSpeed,
                finalLungeTime,
                finalDamping);

            yield return AbilityTasks.WaitDelay(caster, spec, finalLungeTime);
        }

        if (IsCancelled(spec))
            yield break;

        GameObject finalTarget = context.Target != null ? context.Target : fallbackTarget;
        if (!tackle.HasDelay && InBox(finalTarget, context) && ApplyDamage(caster, spec, finalTarget))
            tackle.StartDelay();
    }

    /// <summary>타겟이 고정된 태클 범위 안에 있는지 확인합니다.</summary>
    private bool InBox(GameObject target, TackleAttack.TackleContext context)
    {
        if (target == null)
            return false;

        float length = Mathf.Max(0f, context.LungeDistance);
        float halfWidth = Mathf.Max(0.01f, context.TelegraphWidth * 0.5f);
        if (length <= 0f)
            return false;

        Vector2 direction = context.Direction.sqrMagnitude > 0.0001f
            ? context.Direction.normalized
            : Vector2.right;

        Vector2 targetPos = target.transform.position;
        Vector2 toTarget = targetPos - context.StartPos;
        float forward = Vector2.Dot(toTarget, direction);

        if (forward < 0f || forward > length)
            return false;

        Vector2 closest = context.StartPos + direction * forward;
        Vector2 side = targetPos - closest;
        return side.sqrMagnitude <= halfWidth * halfWidth;
    }

    /// <summary>접촉 피해를 바로 적용합니다.</summary>
    public bool TryApplyContactDamage(AbilitySystem caster, AbilitySpec spec, GameObject target)
    {
        return ApplyDamage(caster, spec, target);
    }

    /// <summary>태클 피해를 적용합니다.</summary>
    private bool ApplyDamage(AbilitySystem caster, AbilitySpec spec, GameObject target)
    {
        if (caster == null || target == null || damageEffect == null)
            return false;

        CombatDamageSnapshot snapshot = new CombatDamageSnapshot(
            finalHpDamage: damageAmount,
            finalStaggerBuildUp: 0f,
            finalKnockbackImpulse: knockbackImpulse,
            elementBuildUps: null,
            isCriticalHit: false);

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
            causer: caster.gameObject);

        Debug.Log($"[GAS] {caster.name} hit {target.name} for {damageAmount}");
        return true;
    }

    /// <summary>어빌리티가 취소됐는지 확인합니다.</summary>
    private static bool IsCancelled(AbilitySpec spec)
    {
        return spec != null && spec.Token != null && spec.Token.IsCancelled;
    }

    /// <summary>이동 컨트롤러를 가져오거나 추가합니다.</summary>
    private static AbilityMotionController2D GetMotion(AbilitySystem caster)
    {
        if (caster == null)
            return null;

        AbilityMotionController2D motion = caster.GetComponent<AbilityMotionController2D>();
        if (motion != null)
            return motion;

        return caster.gameObject.AddComponent<AbilityMotionController2D>();
    }

    /// <summary>남아 있는 태클 경고를 정리합니다.</summary>
    private static void HideTelegraph(AbilitySystem caster)
    {
        if (caster == null)
            return;

        TackleAttack tackle = caster.GetComponent<TackleAttack>();
        if (tackle != null)
            tackle.HideTelegraph();
    }
}
