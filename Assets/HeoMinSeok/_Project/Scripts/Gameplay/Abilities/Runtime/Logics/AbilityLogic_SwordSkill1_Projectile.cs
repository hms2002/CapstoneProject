using System.Collections;
using UnityEngine;
using UnityGAS;

namespace UnityGAS.Sample
{
    [CreateAssetMenu(fileName = "AL_SwordSkill1_Projectile", menuName = "GAS/Samples/AbilityLogic/Sword Skill1 Projectile")]
    public class AbilityLogic_SwordSkill1_Projectile : AbilityLogic
    {
        public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
        {
            var def = spec?.Definition;
            if (system == null || def == null) yield break;

            var data = def.sourceObject as SwordSkill1ProjectileData;
            if (data == null || data.projectilePrefab == null) yield break;

            Vector2 dir = ResolveAimDirection(system);
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
            dir.Normalize();

            Vector3 spawnPos = system.transform.position + data.spawnOffset;
            var go = Object.Instantiate(data.projectilePrefab, spawnPos, Quaternion.identity);

            // Damage snapshot at cast time
            float finalHp = data.damage;

            var stats = system.DamageProfile != null ? system.DamageProfile.formulaStats : null;

            if (stats != null)
            {
                var attackerAttr = system.AttributeSet;
                var result = DamageFormulaUtil.Compute(
                    attackerAttr,
                    stats,
                    DamageAttackKind.Skill,
                    baseHpDamage: data.damage,
                    baseStaggerDamage: data.baseStaggerDamage,
                    includeElement: data.includeElementDamage,
                    includeStagger: data.includeStaggerDamage
                );
                finalHp = result.hpDamage;
            }
            var proj = go.GetComponent<SwordSkill1Projectile2D>();
            if (proj != null)
            {
                proj.Setup(
                    owner: system,
                    direction: dir,
                    spd: data.projectileSpeed,
                    lifetime: data.lifetime,
                    walls: data.wallLayers,
                    dmgLayers: data.damageLayers,
                    dmgEffect: data.damageEffect,
                    dmg: finalHp,
                    ignore: system.gameObject
                );
            }

            yield break;
        }

        private Vector2 ResolveAimDirection(AbilitySystem system)
        {
            var input = system.GetComponent<PlayerCombatInput2D>();
            if (input != null) return input.AimDirection;

            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 w = cam.ScreenToWorldPoint(Input.mousePosition);
                w.z = 0f;
                Vector2 d = (Vector2)(w - system.transform.position);
                if (d.sqrMagnitude > 0.0001f) return d.normalized;
            }
            return Vector2.right;
        }
    }
}
