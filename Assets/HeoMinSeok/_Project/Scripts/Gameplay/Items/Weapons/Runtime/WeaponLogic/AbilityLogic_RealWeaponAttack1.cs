using System.Collections;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// "진짜 무기" - 일반 공격(1타)
    /// - OverlapBox 1회
    /// - 피해/넉백은 ScaledStatFormula 기반
    /// </summary>
    [CreateAssetMenu(fileName = "AL_RW_Attack1", menuName = "GAS/Weapon/RealWeapon/Logic Attack1")]
    public sealed class AbilityLogic_RealWeaponAttack1 : AbilityLogic
    {
        public RealWeaponAttack1Data data;

        public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
        {
            if (system == null || data == null) yield break;
            if (data.damageEffect == null) yield break;
            if (system.AttributeSet == null) yield break;

            Vector2 dir = AbilityAimResolver2D.Resolve(system.gameObject, Vector2.right);

            Vector2 center = (Vector2)system.transform.position + dir * data.forwardOffset;
            var td = AbilityTargetData2D.FromOverlapBox(
                center,
                data.hitboxSize,
                0f,
                data.hitLayers,
                ignore: system.gameObject);

            if (td.Targets.Count == 0)
                yield break;

            IStatProvider statProvider = AbilityStatProviderFactory.Create(system);

            var snapshot = DamageSnapshotBuilder.Build(
                attributeSet: system.AttributeSet,
                statProvider: statProvider,
                config: data.DamageConfig,
                damageFormula: data.damageFormula,
                knockbackFormula: data.knockbackFormula
            );

            CombatDamageApplicator.ApplyToTargets(
                system: system,
                spec: spec,
                damageEffect: data.damageEffect,
                knockbackEffect: data.knockbackEffect,
                targets: td.Targets,
                snapshot: snapshot,
                hitConfirmedTag: null,
                causer: system.gameObject
            );

            yield break;
        }
    }
}