using System.Collections;
using UnityEngine;
using UnityGAS;

// 에디터 메뉴: Create > GAS > Ability Logic > Tackle
[CreateAssetMenu(fileName = "AL_Tackle", menuName = "GAS/Ability Logic/Tackle")]
public class AL_Tackle : AbilityLogic
{
    [Header("Tackle Settings")]
    [SerializeField] private GE_Damage_Spec damageEffect;         // GE_MobContactDamage 등
    [SerializeField] private float damageAmount = 10.0f;          // 데미지 수치

    [Header("Optional")]
    [SerializeField] private GE_Knockback_Spec knockbackEffect;   // 없으면 null 가능
    [SerializeField] private float knockbackImpulse = 0f;
    [SerializeField] private GameplayTag hitConfirmedTag;

    public override IEnumerator Activate(AbilitySystem caster, AbilitySpec spec, GameObject target)
    {
        if (caster == null || target == null || damageEffect == null)
            yield break;

        var snapshot = new CombatDamageSnapshot(
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
        yield break;
    }
}
